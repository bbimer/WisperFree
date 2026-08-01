using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FreeFlowWin.Core.Config
{
    public class UserSettings
    {
        public string EncryptedApiKey { get; set; } = string.Empty;
        public int AudioDeviceIndex { get; set; } = 0;
        public bool UseLocalAi { get; set; } = false;
        public string ModelName { get; set; } = "ggml-small.bin";
        public string CustomTerms { get; set; } = string.Empty;
        public int HotkeyVirtualKey { get; set; } = 120; // по умолчанию VK_F9
        public bool HotkeyCtrl { get; set; } = false;
        public bool HotkeyAlt { get; set; } = false;
        public bool HotkeyShift { get; set; } = false;
        public bool HotkeyWin { get; set; } = false;
        public string SpokenLanguage { get; set; } = "ru"; // по умолчанию русский
        public string TranslationMode { get; set; } = "transcribe"; // "transcribe" или "translate"
    }

    public class SettingsManager
    {
        private readonly string _settingsFilePath;
        private UserSettings _settings;

        public UserSettings Settings => _settings;

        public SettingsManager()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "FreeFlowWindows");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            _settingsFilePath = Path.Combine(folder, "settings.json");
            _settings = LoadSettings();

            // Автоматически импортируем ключ из окружения при первом запуске, если он задан
            if (string.IsNullOrEmpty(GetApiKey()))
            {
                string? envKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
                if (!string.IsNullOrEmpty(envKey))
                {
                    SetApiKey(envKey);
                    SaveSettings();
                }
            }
        }

        public string GetApiKey()
        {
            if (string.IsNullOrEmpty(_settings.EncryptedApiKey)) return string.Empty;

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(_settings.EncryptedApiKey);
                // Дешифруем ключ с помощью Windows DPAPI (привязано к учетной записи Windows)
                byte[] decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        public void SetApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                _settings.EncryptedApiKey = string.Empty;
                return;
            }

            try
            {
                byte[] rawBytes = Encoding.UTF8.GetBytes(apiKey);
                // Шифруем ключ с помощью Windows DPAPI
                byte[] encryptedBytes = ProtectedData.Protect(rawBytes, null, DataProtectionScope.CurrentUser);
                _settings.EncryptedApiKey = Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] DPAPI Encryption failed: {ex.Message}");
                _settings.EncryptedApiKey = string.Empty;
            }
        }

        public UserSettings LoadSettings()
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new UserSettings();
            }

            try
            {
                string json = File.ReadAllText(_settingsFilePath);
                return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
            }
            catch
            {
                return new UserSettings();
            }
        }

        public void SaveSettings()
        {
            try
            {
                string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to save settings: {ex.Message}");
            }
        }
    }
}
