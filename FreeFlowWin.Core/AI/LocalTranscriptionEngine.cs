using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Whisper.net;

namespace FreeFlowWin.Core.AI
{
    public class LocalTranscriptionEngine : IDisposable
    {
        private readonly string _modelPath;
        private WhisperFactory? _factory;
        private bool _isLoaded;

        public bool IsLoaded => _isLoaded;

        public LocalTranscriptionEngine(string modelDirectory, string modelName = "ggml-base.bin")
        {
            if (!Directory.Exists(modelDirectory))
            {
                Directory.CreateDirectory(modelDirectory);
            }
            _modelPath = Path.Combine(modelDirectory, modelName);
        }

        public async Task LoadModelAsync(Action<double>? progressCallback = null)
        {
            if (_isLoaded) return;

            if (!File.Exists(_modelPath))
            {
                Console.WriteLine($"[LOCAL AI] Model file not found at {_modelPath}. Downloading from HuggingFace...");
                await DownloadModelAsync(progressCallback);
            }

            _factory = WhisperFactory.FromPath(_modelPath);
            _isLoaded = true;
        }

        private async Task DownloadModelAsync(Action<double>? progressCallback)
        {
            var modelName = Path.GetFileName(_modelPath);
            var downloadUrl = $"https://huggingface.co/ggerganov/whisper.cpp/resolve/main/{modelName}";

            using (var client = new HttpClient())
            {
                // Запрашиваем заголовки ответа для получения размера файла
                using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(_modelPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        long totalReadBytes = 0;
                        int readBytes;

                        while ((readBytes = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, readBytes);
                            totalReadBytes += readBytes;

                            if (totalBytes != -1 && progressCallback != null)
                            {
                                var progress = (double)totalReadBytes / totalBytes * 100.0;
                                progressCallback(progress);
                            }
                        }
                    }
                }
            }
            Console.WriteLine("\n[LOCAL AI] Download complete!");
        }

        public async Task<string> TranscribeAsync(string wavFilePath, string language = "ru", string mode = "transcribe")
        {
            if (!_isLoaded || _factory == null)
            {
                throw new InvalidOperationException("Model is not loaded. Call LoadModelAsync() first.");
            }

            if (!File.Exists(wavFilePath))
            {
                throw new FileNotFoundException("WAV file not found.", wavFilePath);
            }

            using (var fileStream = File.OpenRead(wavFilePath))
            {
                return await TranscribeAsync(fileStream, language, mode);
            }
        }

        public async Task<string> TranscribeAsync(Stream wavStream, string language = "ru", string mode = "transcribe")
        {
            if (!_isLoaded || _factory == null)
            {
                throw new InvalidOperationException("Model is not loaded. Call LoadModelAsync() first.");
            }

            if (wavStream.CanSeek)
            {
                wavStream.Position = 0;
            }

            var builder = _factory.CreateBuilder()
                                  .WithLanguage(language)
                                  .WithThreads(Math.Max(2, Math.Min(6, Environment.ProcessorCount)));

            if (mode == "translate")
            {
                builder = builder.WithTranslate();
            }

            using (var processor = builder.Build())
            {
                var text = "";
                await foreach (var segment in processor.ProcessAsync(wavStream))
                {
                    text += segment.Text;
                }
                return text;
            }
        }

        public void Dispose()
        {
            _factory?.Dispose();
            _factory = null;
            _isLoaded = false;
            GC.SuppressFinalize(this);
        }
    }
}
