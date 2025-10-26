using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.Threading.Tasks;
using RadioStream.Core.Models;
using RadioStream.Core.Events;
using System.Runtime.InteropServices;
using NAudio.Gui;


namespace RadioStream.Core.Services
{
    // Volume Meter klasa
    public class VolumeMeter
    {
        private readonly BufferedWaveProvider _bufferProvider;
        private readonly float[] _sampleBuffer;
        private readonly int _bytesPerSample;
        private readonly Timer _meterTimer;
        private readonly object _lockObject = new object();

        public event EventHandler<VolumeLevelEventArgs>? VolumeLevelChanged;

        public VolumeMeter(BufferedWaveProvider bufferProvider, WaveFormat waveFormat)
        {
            _bufferProvider = bufferProvider;

            // Kreiraj sample buffer (100ms audio)
            int sampleRate = waveFormat.SampleRate;
            int channels = waveFormat.Channels;
            _bytesPerSample = waveFormat.BitsPerSample / 8;
            int samplesNeeded = sampleRate / 10; // 100ms
            _sampleBuffer = new float[samplesNeeded * channels];

            _meterTimer = new Timer(UpdateVolumeLevel, null, 0, 50); // 20 FPS
        }

        private void UpdateVolumeLevel(object? state)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_bufferProvider.BufferedBytes == 0)
                    {
                        VolumeLevelChanged?.Invoke(this, new VolumeLevelEventArgs(0.0, 0.0));
                        return;
                    }

                    int bytesToRead = Math.Min(_sampleBuffer.Length * _bytesPerSample, _bufferProvider.BufferedBytes);
                    byte[] byteBuffer = new byte[bytesToRead];
                    int bytesRead = _bufferProvider.Read(byteBuffer, 0, bytesToRead);

                    if (bytesRead == 0) return;

                    // Konvertuj byteove u float sample-ove i izračunaj RMS
                    double rms = CalculateRMS(byteBuffer, bytesRead);

                    // Konvertuj RMS u volume level (0-1)
                    double volumeLevel = Math.Min(1.0, rms * 2.0); // Podešavanje osetljivosti

                    VolumeLevelChanged?.Invoke(this, new VolumeLevelEventArgs(volumeLevel, volumeLevel));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Volume meter error: {ex.Message}");
            }
        }

        private double CalculateRMS(byte[] buffer, int bytesRead)
        {
            if (bytesRead == 0) return 0.0;

            int sampleCount = bytesRead / _bytesPerSample;
            double sumSquares = 0.0;
            int validSamples = 0;

            for (int i = 0; i < bytesRead; i += _bytesPerSample)
            {
                if (i + _bytesPerSample <= bytesRead)
                {
                    // Konvertuj 16-bit sample u float (-1.0 do 1.0)
                    short sample = BitConverter.ToInt16(buffer, i);
                    float sampleFloat = sample / 32768.0f;
                    sumSquares += sampleFloat * sampleFloat;
                    validSamples++;
                }
            }

            return validSamples > 0 ? Math.Sqrt(sumSquares / validSamples) : 0.0;
        }

        public void Dispose()
        {
            _meterTimer?.Dispose();
        }


    }
}
