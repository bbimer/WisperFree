using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace FreeFlowWin.Core.AI
{
    public class TranscriptionClient
    {
        private readonly string _apiKey;
        private readonly string _apiBaseUrl;
        private readonly HttpClient _httpClient;

        public TranscriptionClient(string apiKey, string apiBaseUrl = "https://api.groq.com/openai/v1")
        {
            _apiKey = apiKey;
            _apiBaseUrl = apiBaseUrl.TrimEnd('/');
            _httpClient = new HttpClient();
        }

        public async Task<string> TranscribeAsync(string filePath, string model = "whisper-large-v3", string? language = null, string mode = "transcribe")
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Audio file not found.", filePath);
            }

            var endpoint = mode == "translate" ? "translations" : "transcriptions";
            var requestUrl = $"{_apiBaseUrl}/audio/{endpoint}";

            using (var request = new HttpRequestMessage(HttpMethod.Post, requestUrl))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                var multipartContent = new MultipartFormDataContent();

                // Считываем аудиофайл в байтовый массив
                var fileBytes = await File.ReadAllBytesAsync(filePath);
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
                multipartContent.Add(fileContent, "file", Path.GetFileName(filePath));

                // Указываем модель распознавания
                multipartContent.Add(new StringContent(model), "model");

                // Добавляем язык, если он передан и режим не translate
                if (mode != "translate" && !string.IsNullOrEmpty(language) && language != "auto")
                {
                    multipartContent.Add(new StringContent(language), "language");
                }

                // Задаем формат ответа
                multipartContent.Add(new StringContent("json"), "response_format");

                request.Content = multipartContent;

                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"API returned error code {response.StatusCode}: {responseContent}");
                }

                using (var doc = JsonDocument.Parse(responseContent))
                {
                    if (doc.RootElement.TryGetProperty("text", out var textProp))
                    {
                        return textProp.GetString() ?? string.Empty;
                    }
                }

                return string.Empty;
            }
        }
    }
}
