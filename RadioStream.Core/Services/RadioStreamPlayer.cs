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


    public async Task PlayAsync(string streamUrl)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(RadioStreamPlayer));

        // Validacija URL-a
        if (string.IsNullOrWhiteSpace(streamUrl) ||
            (!streamUrl.StartsWith("http://") && !streamUrl.StartsWith("https://")))
        {
            throw new ArgumentException("Invalid stream URL");
        }

        try
        {
            await StopAsync();

            OnStatusChanged("Povezivanje na stream...");
            OnDebugMessage($"Povezujem se na: {streamUrl}");

            CurrentState = PlaybackState.Buffering;

            // ✅ JEDNOSTAVNO - bez dodatnih settings
            _mediaReader = new MediaFoundationReader(streamUrl);

            // Proveri da li je stream validan
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
        _isReading = false;
        CurrentState = PlaybackState.Stopped;

        try
        {
            _cancellationTokenSource?.Cancel();
            await Task.Delay(100);

            _outputDevice?.Stop();
            _mediaReader?.Dispose();
            _mediaReader = null;
            _bufferProvider = null;

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

        OnDebugMessage($"Buffer: {BUFFER_DURATION_SECONDS}s, Format: {waveFormat.SampleRate}Hz");
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

        _outputDevice?.Dispose();
        _mediaReader?.Dispose();

        _cancellationTokenSource?.Dispose();

        OnDebugMessage("RadioStreamPlayer disposan");
    }
}