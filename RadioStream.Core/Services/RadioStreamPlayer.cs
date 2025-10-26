using System;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using RadioStream.Core.Models;
using RadioStream.Core.Events;
using PlaybackState = RadioStream.Core.Models.PlaybackState;
using System.Runtime.InteropServices;

namespace RadioStream.Core.Services;

public class RadioStreamPlayer : IDisposable
{
    private MediaFoundationReader? _mediaReader;
    private BufferedWaveProvider? _bufferProvider;
    private WaveOutEvent? _outputDevice;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isReading;
    private bool _isDisposed;

    // IsPlaying
    private readonly SemaphoreSlim _playbackLock = new SemaphoreSlim(1, 1);
    public bool IsPlaying => CurrentState == PlaybackState.Playing || CurrentState == PlaybackState.Buffering;
    
    private RadioStation _stationNowPlay;
    public RadioStation StationPlayNow
    {
        get => _stationNowPlay;
        set => _stationNowPlay = value;
    }

    // Konfiguracija
    private const int BUFFER_DURATION_SECONDS = 5;
    private const int READ_BUFFER_SIZE = 4096;
    private const int DESIRED_LATENCY = 200;

    // Eventi
    public event EventHandler<PlaybackEventArgs>? StatusChanged;
    public event EventHandler<BufferEventArgs>? BufferLevelChanged;
    public event EventHandler<ErrorEventArgs>? ErrorOccurred;
    public event EventHandler<PlaybackEventArgs>? DebugMessage;

    public PlaybackState CurrentState { get; private set; } = PlaybackState.Stopped;



    private VolumeMeter? _volumeMeter;
    private WaveFormat? _currentWaveFormat;
    private readonly object _volumeMeterLock = new object();

    public bool IsVolumeMeterEnabled => _volumeMeter != null;
    public event EventHandler<VolumeLevelEventArgs>? VolumeLevelChanged;


    public RadioStreamPlayer()
    {
        InitializeAudioSystem();
    }

    private void InitializeAudioSystem()
    {
        try
        {
            _outputDevice = new WaveOutEvent()
            {
                DeviceNumber = 0,
                DesiredLatency = DESIRED_LATENCY
            };

            _cancellationTokenSource = new CancellationTokenSource();

            OnDebugMessage("Audio sistem inicijalizovan");
            OnStatusChanged("Sistem spreman");
        }
        catch (Exception ex)
        {
            OnError(ex, "Inicijalizacija audio sistema");
            throw;
        }
    }


    public async Task PlayAsync(RadioStation playingStation)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(RadioStreamPlayer));

        // Validacija URL-a
        if (string.IsNullOrWhiteSpace(playingStation.StreamUrl) ||
            (!playingStation.StreamUrl.StartsWith("http://") && !playingStation.StreamUrl.StartsWith("https://")))
        {
            throw new ArgumentException("Invalid stream URL");
        }

        try
        {
            await StopAsync();

            OnStatusChanged("Povezivanje na stream...");
            OnDebugMessage($"Povezujem se na: {playingStation.StreamUrl}");

            CurrentState = PlaybackState.Buffering;

            _mediaReader = new MediaFoundationReader(playingStation.StreamUrl);

            if (_mediaReader.WaveFormat == null)
            {
                throw new InvalidOperationException("Stream nema audio format");
            }

            InitializeBufferProvider(_mediaReader.WaveFormat);

            _outputDevice!.Init(_bufferProvider);
            _outputDevice.Play();

            _isReading = true;
            _ = Task.Run(() => ReadStreamWorker(_cancellationTokenSource!.Token));

            CurrentState = PlaybackState.Playing;
            StationPlayNow = playingStation;
            OnStatusChanged("Reprodukcija pokrenuta");
            OnDebugMessage($"Format: {_mediaReader.WaveFormat.SampleRate}Hz, {_mediaReader.WaveFormat.Channels}ch");
        }
        catch (COMException comEx)
        {
            CurrentState = PlaybackState.Error;
            string errorMsg = comEx.HResult switch
            {
                unchecked((int)0xC00D001A) => "Format streama nije podržan",
                unchecked((int)0xC00D36B4) => "Stream nema audio sadržaj",
                unchecked((int)0xC00D36FA) => "Nepodržani tip streama",
                _ => $"COM greška: 0x{comEx.HResult:X8}"
            };
            OnError(new Exception(errorMsg), "Pokretanje reprodukcije");
            throw;
        }
        catch (Exception ex)
        {
            CurrentState = PlaybackState.Error;
            OnError(ex, "Pokretanje reprodukcije");
            throw;
        }
    }


    public async Task StopAsync()
    {
        await _playbackLock.WaitAsync();

        try
        {
            await StopInternalAsync();

        }
        catch (Exception ex)
        {
            OnError(ex, "Zaustavljanje reprodukcije");
        }
        finally
        {
            _playbackLock.Release();
        }
    }


    private async Task StopInternalAsync()
    {
        if (!_isReading && CurrentState == PlaybackState.Stopped)
            return; // Već je zaustavljeno

        _isReading = false;
        CurrentState = PlaybackState.Stopped;

        try
        {
            lock (_volumeMeterLock)
            {
                _volumeMeter?.Dispose();
                _volumeMeter = null;
            }

            _cancellationTokenSource?.Cancel();
            await Task.Delay(100);

            _outputDevice?.Stop();
            _mediaReader?.Dispose();
            _mediaReader = null;
            _bufferProvider = null;

            CurrentState = PlaybackState.Stopped;
            OnStatusChanged("Reprodukcija zaustavljena");
            OnDebugMessage("Reprodukcija zaustavljena");

            // Reset cancellation token
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();
            }
        }
        catch (Exception ex)
        {
            CurrentState = PlaybackState.Error;
            OnError(ex, "Zaustavljanje reprodukcije");
        }
    }



    public void SetVolume(float volume)
    {
        if (_outputDevice != null && volume >= 0 && volume <= 1.0f)
        {
            _outputDevice.Volume = volume;
        }
    }

    private void InitializeBufferProvider(WaveFormat waveFormat)
    {
        _bufferProvider = new BufferedWaveProvider(waveFormat)
        {
            BufferDuration = TimeSpan.FromSeconds(BUFFER_DURATION_SECONDS),
            DiscardOnBufferOverflow = true
        };

        InitializeVolumeMeter();

        OnDebugMessage($"Buffer: {BUFFER_DURATION_SECONDS}s, Format: {waveFormat.SampleRate}Hz");
    }


    private void InitializeVolumeMeter()
    {
        lock (_volumeMeterLock)
        {
            _volumeMeter?.Dispose();

            if (_bufferProvider != null && _currentWaveFormat != null)
            {
                _volumeMeter = new VolumeMeter(_bufferProvider, _currentWaveFormat);
                _volumeMeter.VolumeLevelChanged += OnVolumeMeterLevelChanged;
            }
        }
    }

    private void OnVolumeMeterLevelChanged(object? sender, VolumeLevelEventArgs e)
    {
        VolumeLevelChanged?.Invoke(this, e);
    }


    private async Task ReadStreamWorker(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[READ_BUFFER_SIZE];
        int totalReads = 0;
        DateTime lastUpdate = DateTime.Now;

        try
        {
            OnDebugMessage("ReadStreamWorker pokrenut");

            while (_isReading && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_bufferProvider?.BufferedBytes > _bufferProvider?.BufferLength * 0.7)
                    {
                        await Task.Delay(30, cancellationToken);
                        continue;
                    }

                    int bytesRead = await _mediaReader!.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                    if (bytesRead > 0)
                    {
                        totalReads++;
                        _bufferProvider?.AddSamples(buffer, 0, bytesRead);

                        if ((DateTime.Now - lastUpdate).TotalMilliseconds > 500)
                        {
                            double bufferLevel = _bufferProvider != null
                                ? (double)_bufferProvider.BufferedBytes / _bufferProvider.BufferLength * 100
                                : 0;

                            OnBufferLevelChanged(bufferLevel);
                            lastUpdate = DateTime.Now;

                            if (totalReads % 10 == 0)
                            {
                                OnDebugMessage($"Reads: {totalReads}, Buffer: {bufferLevel:F1}%");
                            }
                        }
                    }
                    else if (bytesRead == 0)
                    {
                        await Task.Delay(100, cancellationToken);
                    }

                    await Task.Delay(10, cancellationToken);
                }
                catch (TimeoutException)
                {
                    OnDebugMessage("Timeout u čitanju - nastavljam");
                    await Task.Delay(100, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            OnDebugMessage("ReadStreamWorker otkazan");
        }
        catch (Exception ex)
        {
            OnError(ex, "ReadStreamWorker");
        }
        finally
        {
            OnDebugMessage($"ReadStreamWorker završen (ukupno reads: {totalReads})");
        }
    }

    // Event helper methods
    private void OnStatusChanged(string message)
        => StatusChanged?.Invoke(this, new PlaybackEventArgs(message));

    private void OnBufferLevelChanged(double level)
        => BufferLevelChanged?.Invoke(this, new BufferEventArgs(level));

    private void OnError(Exception ex, string context = "")
        => ErrorOccurred?.Invoke(this, new ErrorEventArgs(ex, context));

    private void OnDebugMessage(string message)
        => DebugMessage?.Invoke(this, new PlaybackEventArgs(message));

    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        _isReading = false;

        _cancellationTokenSource?.Cancel();

        lock (_volumeMeterLock)
        {
            _volumeMeter?.Dispose();
            _volumeMeter = null;
        }

        _outputDevice?.Dispose();
        _mediaReader?.Dispose();

        _cancellationTokenSource?.Dispose();

        OnDebugMessage("RadioStreamPlayer disposan");
    }


    public float Volume
    {
        get => _outputDevice?.Volume ?? 0.5f;
        set
        {
            if (_outputDevice != null && value >= 0 && value <= 1.0f)
            {
                _outputDevice.Volume = value;
                OnVolumeChanged(value);
            }
        }
    }

    public event EventHandler<VolumeEventArgs>? VolumeChanged;

    public class VolumeEventArgs : EventArgs
    {
        public float Volume { get; }
        public VolumeEventArgs(float volume) => Volume = volume;
    }

    private void OnVolumeChanged(float volume)
    {
        VolumeChanged?.Invoke(this, new VolumeEventArgs(volume));
    }



    public void StartVolumeMeter()
    {
        // Automatski se startuje kada se stream pokrene
        OnDebugMessage("Volume meter je automatski aktiviran tokom reprodukcije");
    }

    public void StopVolumeMeter()
    {
        lock (_volumeMeterLock)
        {
            _volumeMeter?.Dispose();
            _volumeMeter = null;
        }
        OnDebugMessage("Volume meter zaustavljen");
    }

    public (double left, double right) GetCurrentVolumeLevel()
    {
        lock (_volumeMeterLock)
        {
            if (_volumeMeter == null)
                return (0.0, 0.0);

            // Ovo je simplified - u pravoj implementaciji bi imali real-time vrednosti
            return (0.0, 0.0);
        }
    }



}