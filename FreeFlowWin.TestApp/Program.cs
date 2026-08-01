using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FreeFlowWin.Core.AI;
using FreeFlowWin.Core.Audio;
using FreeFlowWin.Core.Hooks;
using FreeFlowWin.Core.Input;
using FreeFlowWin.Core.Context;

namespace FreeFlowWin.TestApp
{
    class Program
    {
        private static KeyboardHook? _hook;
        private static AudioEngine? _audioEngine;
        private static LocalTranscriptionEngine? _localEngine;
        private static TranscriptionClient? _apiTranscriptionClient;
        private static LlmCleanupClient? _cleanupClient;
        private static string _tempFile = "";
        private static bool _isRecording = false;
        private static int _deviceIndex = 0;
        private static CancellationTokenSource? _liveCts;
        private static bool _useLocalAi = false; // По умолчанию переключаем на API-режим (Groq)

        // Код клавиши F9 (0x78 = 120) для удержания во время разговора
        private const int TRIGGER_KEY = 120; 

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== FreeFlow Windows Console Test App ===");

            // Получаем ключ API
            string? apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.Write("Enter your GROQ_API_KEY: ");
                apiKey = Console.ReadLine();
                if (string.IsNullOrEmpty(apiKey))
                {
                    Console.WriteLine("API key is required. Exiting.");
                    return;
                }
            }

            _cleanupClient = new LlmCleanupClient(apiKey);

            // Проверяем режим работы (Local vs API) из переменной окружения
            string? localAiEnv = Environment.GetEnvironmentVariable("USE_LOCAL_AI");
            if (bool.TryParse(localAiEnv, out bool parsedLocal))
            {
                _useLocalAi = parsedLocal;
            }

            if (_useLocalAi)
            {
                Console.WriteLine("[MODE] Running in LOCAL OFFLINE mode.");
                // Определяем путь для хранения моделей
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string modelsDir = Path.Combine(appDir, "Models");
                string modelName = Environment.GetEnvironmentVariable("WHISPER_MODEL") ?? "ggml-small.bin";

                _localEngine = new LocalTranscriptionEngine(modelsDir, modelName);
                
                try
                {
                    await _localEngine.LoadModelAsync(progress =>
                    {
                        Console.Write($"\rDownloading model ({modelName}): {progress:F1}%");
                    });
                    Console.WriteLine($"\rModel {modelName} loaded successfully.                        ");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n[FATAL ERROR] Failed to load local model: {ex.Message}");
                    return;
                }
            }
            else
            {
                Console.WriteLine("[MODE] Running in CLOUD API mode (Groq).");
                _apiTranscriptionClient = new TranscriptionClient(apiKey);
            }

            // Получаем индекс аудиоустройства из переменной окружения
            string? deviceIndexEnv = Environment.GetEnvironmentVariable("AUDIO_DEVICE_INDEX");
            if (int.TryParse(deviceIndexEnv, out int parsedIndex))
            {
                _deviceIndex = parsedIndex;
            }

            _tempFile = Path.Combine(Path.GetTempPath(), "freeflow_test.wav");
            _audioEngine = new AudioEngine();

            // Выводим список доступных микрофонов для диагностики
            Console.WriteLine("\n[AUDIO] Available recording devices:");
            int deviceCount = NAudio.Wave.WaveInEvent.DeviceCount;
            if (deviceCount == 0)
            {
                Console.WriteLine("[WARNING] No recording devices (microphones) found!");
            }
            else
            {
                for (int i = 0; i < deviceCount; i++)
                {
                    var caps = NAudio.Wave.WaveInEvent.GetCapabilities(i);
                    string defaultMarker = (i == _deviceIndex) ? " (SELECTED)" : "";
                    Console.WriteLine($"  Device {i}: {caps.ProductName}{defaultMarker}");
                }
            }

            _hook = new KeyboardHook();
            _hook.KeyDown += OnKeyDown;
            _hook.KeyUp += OnKeyUp;

            Console.WriteLine($"\n[INFO] Hold [F9] key to talk. Release to transcribe.");
            Console.WriteLine("Starting keyboard hook. Press Ctrl+C to exit.");

            try
            {
                _hook.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FATAL ERROR] Failed to start hook: {ex.Message}");
                return;
            }

            // Цикл обработки сообщений Win32 необходим для работы глобальных хуков в консоли
            MSG msg;
            while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            _hook.Dispose();
            _audioEngine.Dispose();
            _localEngine?.Dispose();
        }

        private static void OnKeyDown(int vkCode)
        {
            if (vkCode == TRIGGER_KEY)
            {
                if (!_isRecording)
                {
                    _isRecording = true;
                    Console.WriteLine("\n[RECORDING] <<< Recording started. Speak now...");
                    try
                    {
                        if (File.Exists(_tempFile)) File.Delete(_tempFile);
                        _audioEngine?.StartRecording(_tempFile, _deviceIndex);

                        // Live-транскрипция работает только в локальном режиме (чтобы не спамить Groq API запросами)
                        if (_useLocalAi)
                        {
                            _liveCts = new CancellationTokenSource();
                            _ = Task.Run(() => LiveTranscribeLoopAsync(_liveCts.Token));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Failed to start recording: {ex.Message}");
                        _isRecording = false;
                    }
                }
            }
        }

        private static async void OnKeyUp(int vkCode)
        {
            if (vkCode == TRIGGER_KEY)
            {
                if (_isRecording)
                {
                    _isRecording = false;
                    
                    if (_useLocalAi)
                    {
                        _liveCts?.Cancel(); // Останавливаем live-цикл
                    }

                    // Немедленно захватываем контекст активного приложения
                    string context = ContextExtractor.GetActiveWindowContext();
                    Console.WriteLine($"\n[CONTEXT] Captured context: {context}");

                    Console.WriteLine("[RECORDING] >>> Recording stopped. Transcribing audio...");
                    try
                    {
                        _audioEngine?.StopRecording();
                        
                        // Даем NAudio дописать заголовки файла
                        await Task.Delay(150);

                        // Проверяем размер и существование файла
                        if (File.Exists(_tempFile))
                        {
                            var fileInfo = new FileInfo(_tempFile);
                            Console.WriteLine($"[AUDIO] Audio file size: {fileInfo.Length} bytes.");
                            
                            if (fileInfo.Length <= 44)
                            {
                                Console.WriteLine("[WARNING] Recorded audio file is empty. Please check your default microphone!");
                                return;
                            }
                        }
                        else
                        {
                            Console.WriteLine("[ERROR] Audio file was not created.");
                            return;
                        }

                        string rawResultText = "";
                        
                        if (_useLocalAi)
                        {
                            Console.WriteLine("[LOCAL AI] Transcribing final result locally...");
                            rawResultText = await _localEngine!.TranscribeAsync(_tempFile, language: "ru");
                        }
                        else
                        {
                            Console.WriteLine("[API] Transcribing via Groq Whisper API...");
                            rawResultText = await _apiTranscriptionClient!.TranscribeAsync(_tempFile, language: "ru");
                        }

                        Console.WriteLine($"\n[RAW TRANSCRIBED RESULT]: \"{rawResultText.Trim()}\"");

                        if (!string.IsNullOrWhiteSpace(rawResultText))
                        {
                            Console.WriteLine("[LLM API] Cleaning up text with context...");
                            string cleanedText = await _cleanupClient!.CleanupTextAsync(rawResultText.Trim(), context);
                            Console.WriteLine($"\n[FINAL CLEANED RESULT]: \"{cleanedText}\"");

                            // Эмулируем вставку текста в активное окно
                            if (!string.IsNullOrWhiteSpace(cleanedText))
                            {
                                Console.WriteLine("[INPUT] Simulating paste (Ctrl+V) into the active field...");
                                InputSimulator.PasteText(cleanedText);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Transcription, LLM cleanup or Paste failed: {ex.Message}");
                    }
                }
            }
        }

        private static async Task LiveTranscribeLoopAsync(CancellationToken token)
        {
            try
            {
                // Даем накопиться первому звуку
                await Task.Delay(1000, token);

                while (!token.IsCancellationRequested)
                {
                    if (_audioEngine == null || _localEngine == null) break;

                    // Получаем WAV-байты прямо из оперативной памяти
                    byte[] wavBytes = _audioEngine.GetCurrentAudioWavBytes();
                    if (wavBytes.Length > 44)
                    {
                        using (var ms = new MemoryStream(wavBytes))
                        {
                            string liveText = await _localEngine.TranscribeAsync(ms, language: "ru");
                            
                            // Выводим в реальном времени с перезаписью строки
                            if (!string.IsNullOrWhiteSpace(liveText))
                            {
                                Console.Write($"\r[LIVE]: {liveText.Trim()}...      ");
                            }
                        }
                    }

                    // Интервал обновления live-транскрипции (1.2 сек)
                    await Task.Delay(1200, token);
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[LIVE ERROR] {ex.Message}");
            }
        }

        #region Win32 Message Loop Imports
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
            public uint lPrivate;
        }

        [DllImport("user32.dll")]
        private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);
        #endregion
    }
}
