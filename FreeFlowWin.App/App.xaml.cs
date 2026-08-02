using System;
using System.IO;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using FreeFlowWin.Core.AI;
using FreeFlowWin.Core.Audio;
using FreeFlowWin.Core.Config;
using FreeFlowWin.Core.Hooks;
using FreeFlowWin.Core.Input;
using FreeFlowWin.Core.Context;
using FreeFlowWin.Core.Telemetry;
using System.Collections.Generic;

namespace FreeFlowWin.App
{
    public partial class App : System.Windows.Application
    {
        private System.Windows.Forms.NotifyIcon? _trayIcon;
        private MainWindow? _mainWindow;
        private RecordingOverlayWindow? _overlayWindow;
        
        private KeyboardHook? _hook;
        private AudioEngine? _audioEngine;
        private LocalTranscriptionEngine? _localEngine;
        private TranscriptionClient? _apiTranscriptionClient;
        private LlmCleanupClient? _cleanupClient;
        
        private SettingsManager? _settingsManager;
        private StatsManager? _statsManager;
        private string _tempFile = "";
        private bool _isRecording = false;
        private DateTime _recordingStartTime;
        private CancellationTokenSource? _liveCts;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine("\n=== FreeFlow Windows App (WPF Background Service) ===");
            }
            catch { }

            this.DispatcherUnhandledException += (s, args) =>
            {
                Log($"[ERROR] Unhandled UI Exception: {args.Exception.Message}");
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    Log($"[FATAL ERROR] Domain Exception: {ex.Message}");
                }
            };

            // 1. Initialize settings manager
            _settingsManager = new SettingsManager();
            _statsManager = new StatsManager();

            // Auto-register in Windows startup (HKCU, no admin rights needed)
            RegisterAutoStart();
            string apiKey = _settingsManager.GetApiKey();

            // 2. Create settings window and show immediately
            _mainWindow = new MainWindow();
            _mainWindow.Show();

            // 3. Create Windows tray icon
            CreateTrayIcon();

            Log($"Initialization. Settings path: %AppData%/FreeFlowWindows/settings.json");

            if (string.IsNullOrEmpty(apiKey))
            {
                Log("[WARNING] Groq API Key is not set. Please set it in the Settings window.");
            }
            else
            {
                Log("[INFO] Groq API Key loaded successfully.");
            }

            // 4. Initialize audio engine and temp path
            _tempFile = Path.Combine(Path.GetTempPath(), "freeflow_recording.wav");
            _audioEngine = new AudioEngine();

            InitializeAiEngine(apiKey);

            // 5. Initialize and configure keyboard hook
            _hook = new KeyboardHook();
            UpdateHookConfig();
            _hook.KeyDown += OnKeyDown;
            _hook.KeyUp += OnKeyUp;

            try
            {
                _hook.Start();
                Log("[HOOK] Global keyboard hook started.");
            }
            catch (Exception ex)
            {
                Log($"[FATAL ERROR] Failed to start keyboard hook: {ex.Message}");
                System.Windows.MessageBox.Show($"Failed to start keyboard hook: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }

            // 6. Listen to system sleep / wakeup events
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            Log("[POWER] Power state listener activated.");
        }

        public void UpdateHookConfig()
        {
            if (_hook != null && _settingsManager != null)
            {
                _hook.SuppressedKey = _settingsManager.Settings.HotkeyVirtualKey;
                _hook.NeedCtrl = _settingsManager.Settings.HotkeyCtrl;
                _hook.NeedAlt = _settingsManager.Settings.HotkeyAlt;
                _hook.NeedShift = _settingsManager.Settings.HotkeyShift;
                _hook.NeedWin = _settingsManager.Settings.HotkeyWin;
                
                string keysDesc = GetHotkeyDescription(
                    _settingsManager.Settings.HotkeyVirtualKey,
                    _settingsManager.Settings.HotkeyCtrl,
                    _settingsManager.Settings.HotkeyAlt,
                    _settingsManager.Settings.HotkeyShift,
                    _settingsManager.Settings.HotkeyWin
                );
                Log($"[HOOK] Keyboard hook config updated to: {keysDesc}");
            }
        }

        private string GetHotkeyDescription(int vk, bool ctrl, bool alt, bool shift, bool win)
        {
            var parts = new List<string>();
            if (ctrl) parts.Add("Ctrl");
            if (alt) parts.Add("Alt");
            if (shift) parts.Add("Shift");
            if (win) parts.Add("Win");
            
            parts.Add(((System.Windows.Input.Key)System.Windows.Input.KeyInterop.KeyFromVirtualKey(vk)).ToString());
            return string.Join(" + ", parts);
        }

        public void ReinitializeAiEngine()
        {
            _localEngine?.Dispose();
            _localEngine = null;
            string apiKey = _settingsManager?.GetApiKey() ?? "";
            InitializeAiEngine(apiKey);
        }

        private void InitializeAiEngine(string apiKey)
        {
            bool useLocal = _settingsManager?.Settings.UseLocalAi ?? false;
            string modelName = _settingsManager?.Settings.ModelName ?? "ggml-small.bin";

            if (useLocal)
            {
                Log($"[LOCAL AI] Starting Local Whisper mode. Model: {modelName}...");
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string modelsDir = Path.Combine(appData, "FreeFlowWindows", "Models");
                _localEngine = new LocalTranscriptionEngine(modelsDir, modelName);

                int lastLoggedStep = -1;
                Task.Run(async () =>
                {
                    try
                    {
                        await _localEngine.LoadModelAsync(progress =>
                        {
                            int currentStep = ((int)progress) / 10 * 10;
                            if (currentStep != lastLoggedStep)
                            {
                                lastLoggedStep = currentStep;
                                Log($"[LOCAL AI] Downloading model {modelName}: {currentStep}%...");
                            }
                        });
                        Log($"[LOCAL AI] Model {modelName} loaded successfully. Ready for offline use.");
                    }
                    catch (Exception ex)
                    {
                        Log($"[LOCAL AI ERROR] Failed to load local model: {ex.Message}");
                    }
                });
            }
            else
            {
                Log("[API] Starting Cloud Groq API mode...");
                _apiTranscriptionClient = new TranscriptionClient(apiKey);
            }

            if (!string.IsNullOrEmpty(apiKey))
            {
                _cleanupClient = new LlmCleanupClient(apiKey);
            }
        }

        private void CreateTrayIcon()
        {
            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = "FreeFlow Windows",
                Visible = true
            };

            try
            {
                var iconUri = new Uri("pack://application:,,,/FreeFlowWin.App;component/assets/logo.png", UriKind.Absolute);
                var streamInfo = System.Windows.Application.GetResourceStream(iconUri);
                if (streamInfo != null)
                {
                    using (var stream = streamInfo.Stream)
                    {
                        using (var bitmap = new System.Drawing.Bitmap(stream))
                        {
                            IntPtr hIcon = bitmap.GetHicon();
                            _trayIcon.Icon = System.Drawing.Icon.FromHandle(hIcon);
                        }
                    }
                }
                else
                {
                    _trayIcon.Icon = System.Drawing.SystemIcons.Application;
                }
            }
            catch
            {
                _trayIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            contextMenu.Items.Add("Settings", null, (s, ev) => ShowSettings());
            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, (s, ev) => ExitApp());

            _trayIcon.ContextMenuStrip = contextMenu;
            _trayIcon.DoubleClick += (s, ev) => ShowSettings();
        }

        private void ShowSettings()
        {
            if (_mainWindow != null)
            {
                _mainWindow.Show();
                _mainWindow.Activate();
            }
        }

        private void OnKeyDown(int vkCode)
        {
            if (!_isRecording)
            {
                _settingsManager?.Reload();
                bool useLocal = _settingsManager?.Settings.UseLocalAi ?? false;
                string apiKey = _settingsManager?.GetApiKey() ?? "";

                if (!useLocal && string.IsNullOrEmpty(apiKey))
                {
                    Log("[WARNING] Attempted to record without an API Key in Cloud Mode. Cancelled.");
                    return;
                }

                _isRecording = true;
                _recordingStartTime = DateTime.Now;
                Log("<<< Recording started. Speak now...");

                try
                {
                    try { if (File.Exists(_tempFile)) File.Delete(_tempFile); } catch { }
                    
                    int deviceIndex = _settingsManager?.Settings.AudioDeviceIndex ?? 0;
                    _audioEngine?.StartRecording(_tempFile, deviceIndex);
                    if (_audioEngine != null)
                    {
                        _audioEngine.VolumeChanged += OnVolumeChanged;
                    }

                    Dispatcher.Invoke(() =>
                    {
                        _overlayWindow = new RecordingOverlayWindow();
                        _overlayWindow.Show();
                    });

                    bool enableLivePreview = _settingsManager?.Settings.EnableLivePreview ?? false;
                    if (useLocal && enableLivePreview && _localEngine != null && _localEngine.IsLoaded)
                    {
                        _liveCts = new CancellationTokenSource();
                        _ = Task.Run(() => LiveTranscribeLoopAsync(_liveCts.Token));
                    }
                }
                catch (Exception ex)
                {
                    Log($"[ERROR] Failed to start audio recording: {ex.Message}");
                    _isRecording = false;
                }
            }
        }

        private async void OnKeyUp(int vkCode)
        {
            if (_isRecording)
            {
                _isRecording = false;
                double durationSeconds = (DateTime.Now - _recordingStartTime).TotalSeconds;
                _liveCts?.Cancel();

                if (_audioEngine != null)
                {
                    _audioEngine.VolumeChanged -= OnVolumeChanged;
                }

                Dispatcher.Invoke(() =>
                {
                    _overlayWindow?.Close();
                    _overlayWindow = null;
                });

                string context = ContextExtractor.GetActiveWindowContext();
                Log($"[CONTEXT] Active Window: {context}");
                Log("[AI] Launching speech recognition...");

                try
                {
                    _audioEngine?.StopRecording();
                    await Task.Delay(150); // wait for WAV file to finish flushing

                    if (File.Exists(_tempFile))
                    {
                        var fileInfo = new FileInfo(_tempFile);
                        Log($"[AUDIO] Recorded file: {fileInfo.Length} bytes.");
                        if (fileInfo.Length <= 44)
                        {
                            Log("[WARNING] Audio recording is empty (check microphone in settings!).");
                            return;
                        }
                    }
                    else
                    {
                        Log("[ERROR] Recording file not found on disk.");
                        return;
                    }

                    string rawText = "";
                    _settingsManager?.Reload();
                    bool useLocal = _settingsManager?.Settings.UseLocalAi ?? false;
                    string spokenLang = _settingsManager?.Settings.SpokenLanguage ?? "ru";
                    string transMode = _settingsManager?.Settings.TranslationMode ?? "transcribe";

                    if (useLocal)
                    {
                        if (_localEngine == null)
                        {
                            InitializeAiEngine("");
                        }

                        if (_localEngine != null)
                        {
                            if (!_localEngine.IsLoaded && !_localEngine.IsInitializationFailed)
                            {
                                Log("[LOCAL AI] Local Whisper model is initializing/downloading. Waiting for completion...");
                                int waitAttempts = 0;
                                while (!_localEngine.IsLoaded && !_localEngine.IsInitializationFailed && waitAttempts < 600)
                                {
                                    await Task.Delay(100);
                                    waitAttempts++;
                                }
                            }

                            if (_localEngine.IsLoaded)
                            {
                                rawText = await _localEngine.TranscribeAsync(_tempFile, language: spokenLang, mode: transMode);
                            }
                            else if (_localEngine.IsInitializationFailed)
                            {
                                Log($"[ERROR] Local Whisper model failed to initialize: {_localEngine.InitializationError}");
                                return;
                            }
                            else
                            {
                                Log("[ERROR] Local Whisper model download in progress. Please wait a moment and try again.");
                                return;
                            }
                        }
                    }
                    else
                    {
                        string currentKey = _settingsManager?.GetApiKey() ?? "";
                        if (_apiTranscriptionClient == null && !string.IsNullOrEmpty(currentKey))
                        {
                            InitializeAiEngine(currentKey);
                        }

                        if (_apiTranscriptionClient != null)
                        {
                            rawText = await _apiTranscriptionClient.TranscribeAsync(_tempFile, language: spokenLang, mode: transMode);
                        }
                        else
                        {
                            Log("[ERROR] Cloud API client not initialized. Please set Groq API Key in General settings.");
                            return;
                        }
                    }

                    Log($"[RAW RESULT] \"{rawText.Trim()}\"");

                    if (IsHallucinationOrEmpty(rawText))
                    {
                        Log("[INFO] Silence or whisper hallucination detected. Cancelled.");
                        return;
                    }

                    string finalText = rawText.Trim();

                    // Only attempt LLM cleanup if API client is available
                    if (!useLocal)
                    {
                        Log("[LLM] Sending text for context cleanup...");
                        string apiCleanupKey = _settingsManager?.GetApiKey() ?? "";
                        if (_cleanupClient == null && !string.IsNullOrEmpty(apiCleanupKey))
                        {
                            InitializeAiEngine(apiCleanupKey);
                        }

                        if (_cleanupClient != null)
                        {
                            try
                            {
                                string customVocab = _settingsManager?.Settings.CustomTerms ?? "";
                                string cleaned = await _cleanupClient.CleanupTextAsync(rawText.Trim(), $"{context} | Custom Vocabulary: {customVocab}");
                                if (!string.IsNullOrWhiteSpace(cleaned))
                                {
                                    finalText = cleaned;
                                }
                            }
                            catch (Exception exLlm)
                            {
                                Log($"[WARNING] LLM cleanup bypassed ({exLlm.Message}). Using raw transcript.");
                            }
                        }
                    }
                    else
                    {
                        Log("[LOCAL AI] Using local raw transcript (Offline mode).");
                    }

                    Log($"[FINAL RESULT] \"{finalText}\"");

                    if (!string.IsNullOrWhiteSpace(finalText))
                    {
                        Log("[INPUT] Emulating paste (Ctrl+V)...");
                        await Task.Delay(200);
                        InputSimulator.PasteText(finalText);

                        // Save session statistics
                        try
                        {
                            double exactDuration = durationSeconds;
                            if (File.Exists(_tempFile))
                            {
                                var fileInfo = new FileInfo(_tempFile);
                                if (fileInfo.Length > 44)
                                {
                                    exactDuration = (fileInfo.Length - 44) / 32000.0;
                                }
                            }

                            if (exactDuration < 0.1) exactDuration = 0.5;

                            int wordCount = finalText.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
                            _statsManager?.AddSession(wordCount, exactDuration);
                            Log($"[STATS] Recorded session: {wordCount} words in {exactDuration:F1} sec.");
                        }
                        catch (Exception exStats)
                        {
                            Log($"[ERROR] Failed to save statistics: {exStats.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"[ERROR] Exception during processing: {ex.Message}");
                }
            }
        }

        private static bool IsHallucinationOrEmpty(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;

            string normalized = text.Trim().ToLowerInvariant();

            // Ignore bracketed tags e.g. [музыка], (музыка), [аплодисменты], [подписывайтесь на канал]
            if ((normalized.StartsWith("[") && normalized.EndsWith("]")) ||
                (normalized.StartsWith("(") && normalized.EndsWith(")")))
            {
                return true;
            }

            string[] hallucinations = new[]
            {
                "продолжение следует",
                "субтитры",
                "благодарю за внимание",
                "спасибо за просмотр",
                "подпишитесь",
                "подписывайтесь",
                "музыка",
                "аплодисменты",
                "смех",
                "погружение",
                "редактор",
                "корректор",
                "переводчик",
                "music",
                "applause",
                "laughter",
                "thanks for watching",
                "subscribe"
            };

            foreach (var keyword in hallucinations)
            {
                if (normalized.Contains(keyword))
                {
                    if (normalized.Length <= keyword.Length + 12 || normalized.StartsWith("["))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void OnVolumeChanged(float volume)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    _overlayWindow?.UpdateVolume(volume);
                });
            }
            catch { }
        }

        private async Task LiveTranscribeLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(1000, token);
                if (token.IsCancellationRequested) break;

                try
                {
                    byte[] wavBytes = _audioEngine?.GetCurrentAudioWavBytes() ?? Array.Empty<byte>();
                    if (wavBytes.Length > 44 && _localEngine != null && _localEngine.IsLoaded)
                    {
                        string tempLiveFile = Path.Combine(Path.GetTempPath(), "freeflow_live.wav");
                        await File.WriteAllBytesAsync(tempLiveFile, wavBytes, token);

                        string spokenLang = _settingsManager?.Settings.SpokenLanguage ?? "ru";
                        string transMode = _settingsManager?.Settings.TranslationMode ?? "transcribe";
                        string liveText = await _localEngine.TranscribeAsync(tempLiveFile, language: spokenLang, mode: transMode);
                        if (!string.IsNullOrWhiteSpace(liveText) && !IsHallucinationOrEmpty(liveText))
                        {
                            Log($"[LIVE] \"{liveText.Trim()}\"");
                        }

                        try { File.Delete(tempLiveFile); } catch { }
                    }
                }
                catch { }
            }
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
            {
                Log("[POWER] System wake up detected. Re-initializing keyboard hook...");
                try
                {
                    _hook?.Stop();
                    _hook?.Dispose();

                    _hook = new KeyboardHook();
                    UpdateHookConfig();
                    _hook.KeyDown += OnKeyDown;
                    _hook.KeyUp += OnKeyUp;
                    _hook.Start();
                    Log("[POWER] Keyboard hook re-initialized successfully.");
                }
                catch (Exception ex)
                {
                    Log($"[POWER ERROR] Failed to re-initialize keyboard hook: {ex.Message}");
                }
            }
        }

        private void Log(string message)
        {
            _mainWindow?.LogMessage(message);
        }

        private static void RegisterAutoStart()
        {
            try
            {
                const string appName = "FreeFlowWin";
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (string.IsNullOrEmpty(exePath)) return;

                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
                if (key == null) return;

                var existing = key.GetValue(appName) as string;
                if (existing != exePath)
                {
                    key.SetValue(appName, $"\"{exePath}\"");
                }
            }
            catch (Exception ex)
            {
                // Non-critical — app works fine without autostart
                Console.WriteLine($"[AUTOSTART] Failed to register: {ex.Message}");
            }
        }

        private void ExitApp()
        {
            _trayIcon?.Dispose();
            _mainWindow?.RealExit();
            Shutdown();
        }
    }
}
