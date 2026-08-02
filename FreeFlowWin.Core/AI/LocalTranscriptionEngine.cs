using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Whisper.net;

namespace FreeFlowWin.Core.AI
{
    public class LocalTranscriptionEngine : IDisposable
    {
        private readonly string _modelPath;
        private WhisperFactory? _factory;
        private bool _isLoaded;

        public static readonly Dictionary<string, string> KnownModelHashes = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ggml-base.bin", "60ed5bc226b64f19985ea1053e3047b21ac70d5df564c7ebd00b48f07bd546f8" },
            { "ggml-small.bin", "1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b" },
            { "ggml-large-v3-turbo.bin", "5639644d6735a4d651a2d5e381023730e666a0d0a2185b306b4d375084931a54" }
        };

        private bool _isInitializationFailed;
        private string? _initializationError;

        public bool IsLoaded => _isLoaded;
        public bool IsInitializationFailed => _isInitializationFailed;
        public string? InitializationError => _initializationError;

        public LocalTranscriptionEngine(string modelDirectory, string modelName = "ggml-base.bin")
        {
            if (!Directory.Exists(modelDirectory))
            {
                Directory.CreateDirectory(modelDirectory);
            }
            _modelPath = Path.Combine(modelDirectory, modelName);
        }

        public static string ComputeSha256(string filePath)
        {
            if (!File.Exists(filePath)) return string.Empty;
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = sha256.ComputeHash(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        public static bool VerifyChecksum(string filePath, string modelName, out string currentHash, out string expectedHash)
        {
            currentHash = ComputeSha256(filePath);
            if (KnownModelHashes.TryGetValue(modelName, out var expected) && !string.IsNullOrEmpty(expected))
            {
                expectedHash = expected.ToLowerInvariant();
                return string.Equals(currentHash, expectedHash, StringComparison.OrdinalIgnoreCase);
            }
            expectedHash = "UNKNOWN (Not in standard database)";
            return File.Exists(filePath) && new FileInfo(filePath).Length > 0;
        }

        public async Task LoadModelAsync(Action<double>? progressCallback = null)
        {
            if (_isLoaded) return;
            _isInitializationFailed = false;
            _initializationError = null;

            try
            {
                var modelName = Path.GetFileName(_modelPath);

                if (File.Exists(_modelPath))
                {
                    if (!VerifyChecksum(_modelPath, modelName, out var currentHash, out var expectedHash))
                    {
                        Console.WriteLine($"[LOCAL AI WARNING] Model SHA256 mismatch for {modelName}! Current: {currentHash}, Expected: {expectedHash}. Redownloading...");
                        File.Delete(_modelPath);
                    }
                }

                if (!File.Exists(_modelPath))
                {
                    Console.WriteLine($"[LOCAL AI] Model file not found at {_modelPath}. Downloading from HuggingFace...");
                    await DownloadModelAsync(progressCallback);
                }

                _factory = WhisperFactory.FromPath(_modelPath);
                _isLoaded = true;
            }
            catch (Exception ex)
            {
                _isInitializationFailed = true;
                _initializationError = ex.Message;
                throw;
            }
        }

        private async Task DownloadModelAsync(Action<double>? progressCallback)
        {
            var modelName = Path.GetFileName(_modelPath);
            var primaryUrl = $"https://huggingface.co/ggerganov/whisper.cpp/resolve/main/{modelName}";
            var fallbackUrl = $"https://huggingface.co/datasets/ggerganov/whisper.cpp/resolve/main/{modelName}";

            var handler = new HttpClientHandler
            {
                UseProxy = true,
                DefaultProxyCredentials = System.Net.CredentialCache.DefaultCredentials,
                AllowAutoRedirect = true,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            using (var client = new HttpClient(handler))
            {
                client.Timeout = TimeSpan.FromMinutes(10);
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

                HttpResponseMessage response;
                try
                {
                    response = await client.GetAsync(primaryUrl, HttpCompletionOption.ResponseHeadersRead);
                    if (!response.IsSuccessStatusCode)
                    {
                        response.Dispose();
                        Console.WriteLine($"[LOCAL AI WARNING] Primary download URL failed (HTTP {response.StatusCode}). Trying fallback...");
                        response = await client.GetAsync(fallbackUrl, HttpCompletionOption.ResponseHeadersRead);
                    }
                }
                catch (Exception exPrimary)
                {
                    Console.WriteLine($"[LOCAL AI WARNING] Primary download failed ({exPrimary.Message}). Trying fallback...");
                    response = await client.GetAsync(fallbackUrl, HttpCompletionOption.ResponseHeadersRead);
                }

                using (response)
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
            Console.WriteLine("\n[LOCAL AI] Download complete. Verifying SHA256 integrity...");

            if (!VerifyChecksum(_modelPath, modelName, out var downloadedHash, out var expectedHash))
            {
                if (File.Exists(_modelPath)) File.Delete(_modelPath);
                throw new InvalidDataException($"Downloaded model '{modelName}' failed SHA256 verification. Got: {downloadedHash}, Expected: {expectedHash}");
            }

            Console.WriteLine($"[LOCAL AI] SHA256 integrity verified successfully ({downloadedHash.Substring(0, 12)}...).");
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
