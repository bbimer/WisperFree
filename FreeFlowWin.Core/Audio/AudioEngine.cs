using System;
using System.IO;
using NAudio.Wave;

namespace FreeFlowWin.Core.Audio
{
    public class AudioEngine : IDisposable
    {
        private WaveInEvent? _capture;
        private WaveFileWriter? _writer;
        private string? _tempFilePath;
        private bool _isRecording;
        private MemoryStream? _pcmBuffer;

        public bool IsRecording => _isRecording;

        public event Action<float>? VolumeChanged;

        public void StartRecording(string outputFilePath, int deviceNumber = 0)
        {
            if (_isRecording) return;

            _tempFilePath = outputFilePath;
            _pcmBuffer = new MemoryStream();

            // Инициализируем захват звука с указанного устройства
            _capture = new WaveInEvent
            {
                DeviceNumber = deviceNumber,
                // Задаем стандартный формат для Whisper: 16кГц, 16-бит, Моно
                WaveFormat = new WaveFormat(16000, 16, 1)
            };

            _writer = new WaveFileWriter(_tempFilePath, _capture.WaveFormat);

            _capture.DataAvailable += (sender, e) =>
            {
                _writer?.Write(e.Buffer, 0, e.BytesRecorded);
                if (_pcmBuffer != null)
                {
                    lock (_pcmBuffer)
                    {
                        _pcmBuffer.Write(e.Buffer, 0, e.BytesRecorded);
                    }
                }

                // Рассчитываем пиковую громкость аудио-фрейма для живой визуализации
                float maxVal = 0;
                for (int i = 0; i < e.BytesRecorded; i += 2)
                {
                    if (i + 1 < e.BytesRecorded)
                    {
                        short sample = (short)((e.Buffer[i + 1] << 8) | e.Buffer[i]);
                        float sample32 = sample / 32768f;
                        float absVal = Math.Abs(sample32);
                        if (absVal > maxVal)
                        {
                            maxVal = absVal;
                        }
                    }
                }
                VolumeChanged?.Invoke(maxVal);
            };

            _capture.RecordingStopped += (sender, e) =>
            {
                _writer?.Dispose();
                _writer = null;
                _capture?.Dispose();
                _capture = null;
                _pcmBuffer?.Dispose();
                _pcmBuffer = null;
                _isRecording = false;

                if (e.Exception != null)
                {
                    // Пробрасываем ошибку дальше, если запись прервалась сбоем
                    throw e.Exception;
                }
            };

            _capture.StartRecording();
            _isRecording = true;
        }

        public void StopRecording()
        {
            if (!_isRecording || _capture == null) return;
            _capture.StopRecording();
        }

        public byte[] GetCurrentAudioWavBytes()
        {
            if (_pcmBuffer == null) return Array.Empty<byte>();

            byte[] pcmData;
            lock (_pcmBuffer)
            {
                pcmData = _pcmBuffer.ToArray();
            }

            // Формат 16кГц, 16-бит, 1 канал (моно)
            byte[] header = WriteWavHeader(16000, 16, 1, pcmData.Length);
            byte[] wavData = new byte[header.Length + pcmData.Length];
            
            Buffer.BlockCopy(header, 0, wavData, 0, header.Length);
            Buffer.BlockCopy(pcmData, 0, wavData, header.Length, pcmData.Length);

            return wavData;
        }

        private static byte[] WriteWavHeader(int sampleRate, int bitsPerSample, int channels, int dataLength)
        {
            byte[] header = new byte[44];
            
            // "RIFF"
            header[0] = 0x52; header[1] = 0x49; header[2] = 0x46; header[3] = 0x46;
            
            int totalFileSize = dataLength + 36;
            BitConverter.GetBytes(totalFileSize).CopyTo(header, 4);
            
            // "WAVE"
            header[8] = 0x57; header[9] = 0x41; header[10] = 0x56; header[11] = 0x45;
            
            // "fmt "
            header[12] = 0x66; header[13] = 0x6d; header[14] = 0x74; header[15] = 0x20;
            
            BitConverter.GetBytes(16).CopyTo(header, 16); // Subchunk1 size
            BitConverter.GetBytes((ushort)1).CopyTo(header, 20); // PCM format
            BitConverter.GetBytes((ushort)channels).CopyTo(header, 22);
            BitConverter.GetBytes(sampleRate).CopyTo(header, 24);
            
            int byteRate = sampleRate * channels * bitsPerSample / 8;
            BitConverter.GetBytes(byteRate).CopyTo(header, 28);
            
            ushort blockAlign = (ushort)(channels * bitsPerSample / 8);
            BitConverter.GetBytes(blockAlign).CopyTo(header, 32);
            BitConverter.GetBytes((ushort)bitsPerSample).CopyTo(header, 34);
            
            // "data"
            header[36] = 0x64; header[37] = 0x61; header[38] = 0x74; header[39] = 0x61;
            BitConverter.GetBytes(dataLength).CopyTo(header, 40);
            
            return header;
        }

        public void Dispose()
        {
            StopRecording();
            _writer?.Dispose();
            _capture?.Dispose();
            _pcmBuffer?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
