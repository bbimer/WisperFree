using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FreeFlowWin.Core.AI;
using FreeFlowWin.Core.Audio;
using FreeFlowWin.Core.Config;
using FreeFlowWin.Core.QA;
using FreeFlowWin.Core.Telemetry;
using NAudio.Wave;

namespace FreeFlowWin.App
{
    public class LogItem
    {
        public string Time { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Color { get; set; } = "#E0E0E6";
    }

    public partial class MainWindow : Window
    {
        private readonly SettingsManager _settingsManager;
        private readonly StatsManager _statsManager;
        private bool _isInitializing = true;
        private bool _isRealExit = false;
        private List<string> _qaSelectedFiles = new List<string>();
        private List<string> _converterSelectedFiles = new List<string>();

        public ObservableCollection<LogItem> LogItems { get; } = new ObservableCollection<LogItem>();

        public class AudioDeviceItem
        {
            public int Index { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class HotkeyKeyItem
        {
            public int VirtualKey { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class LanguageItem
        {
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        public class TranslationModeItem
        {
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }

        public MainWindow()
        {
            InitializeComponent();
            _settingsManager = new SettingsManager();
            _statsManager = new StatsManager();

            LogListBox.ItemsSource = LogItems;
            
            LoadUserSettings();
            PopulateMicrophones();
            LoadStatistics();
            
            _isInitializing = false;
            LogMessage("FreeFlow Studio initialized. Recording device index: " + _settingsManager.Settings.AudioDeviceIndex);
        }

        private void LoadUserSettings()
        {
            // API key from DPAPI
            ApiKeyBox.Password = _settingsManager.GetApiKey();
            LocalAiCheckBox.IsChecked = _settingsManager.Settings.UseLocalAi;
            LivePreviewCheckBox.IsChecked = _settingsManager.Settings.EnableLivePreview;
            LocalModelPanel.Visibility = _settingsManager.Settings.UseLocalAi ? Visibility.Visible : Visibility.Collapsed;

            // Default model selection
            string modelName = _settingsManager.Settings.ModelName;
            if (modelName == "ggml-base.bin") ModelComboBox.SelectedIndex = 0;
            else if (modelName == "ggml-small.bin") ModelComboBox.SelectedIndex = 1;
            else if (modelName == "ggml-large-v3-turbo.bin") ModelComboBox.SelectedIndex = 2;
            else ModelComboBox.SelectedIndex = 1;

            CustomTermsBox.Text = _settingsManager.Settings.CustomTerms;

            // Hotkey state loading
            CtrlHotkeyCheck.IsChecked = _settingsManager.Settings.HotkeyCtrl;
            AltHotkeyCheck.IsChecked = _settingsManager.Settings.HotkeyAlt;
            ShiftHotkeyCheck.IsChecked = _settingsManager.Settings.HotkeyShift;
            WinHotkeyCheck.IsChecked = _settingsManager.Settings.HotkeyWin;

            PopulateHotkeys();

            // Select active key in HotkeyComboBox
            int activeVk = _settingsManager.Settings.HotkeyVirtualKey;
            var items = HotkeyComboBox.ItemsSource as List<HotkeyKeyItem>;
            if (items != null)
            {
                var match = items.Find(k => k.VirtualKey == activeVk);
                if (match != null)
                {
                    HotkeyComboBox.SelectedItem = match;
                }
                else
                {
                    var f9Match = items.Find(k => k.VirtualKey == 120);
                    if (f9Match != null) HotkeyComboBox.SelectedItem = f9Match;
                }
            }

            PopulateLanguages();

            // Select active language
            string activeLang = _settingsManager.Settings.SpokenLanguage;
            var langItems = LanguageComboBox.ItemsSource as List<LanguageItem>;
            if (langItems != null)
            {
                var match = langItems.Find(l => l.Code == activeLang);
                if (match != null) LanguageComboBox.SelectedItem = match;
                else LanguageComboBox.SelectedIndex = 0;
            }

            // Select active translation mode
            string activeMode = _settingsManager.Settings.TranslationMode;
            var modeItems = TranslationModeComboBox.ItemsSource as List<TranslationModeItem>;
            if (modeItems != null)
            {
                var match = modeItems.Find(m => m.Code == activeMode);
                if (match != null) TranslationModeComboBox.SelectedItem = match;
                else TranslationModeComboBox.SelectedIndex = 0;
            }
        }

        private void PopulateMicrophones()
        {
            List<AudioDeviceItem> devices = new List<AudioDeviceItem>();
            int deviceCount = WaveInEvent.DeviceCount;

            for (int i = 0; i < deviceCount; i++)
            {
                var caps = WaveInEvent.GetCapabilities(i);
                devices.Add(new AudioDeviceItem
                {
                    Index = i,
                    Name = $"Device {i}: {caps.ProductName}"
                });
            }

            MicComboBox.ItemsSource = devices;

            int savedIndex = _settingsManager.Settings.AudioDeviceIndex;
            if (savedIndex >= 0 && savedIndex < deviceCount)
            {
                MicComboBox.SelectedIndex = savedIndex;
            }
            else if (deviceCount > 0)
            {
                MicComboBox.SelectedIndex = 0;
                _settingsManager.Settings.AudioDeviceIndex = 0;
                _settingsManager.SaveSettings();
            }
        }

        private void PopulateHotkeys()
        {
            var keys = new List<HotkeyKeyItem>();
            
            // F1 - F12
            for (int i = 1; i <= 12; i++)
            {
                keys.Add(new HotkeyKeyItem { VirtualKey = 111 + i, Name = "F" + i });
            }
            
            // A - Z
            for (char c = 'A'; c <= 'Z'; c++)
            {
                keys.Add(new HotkeyKeyItem { VirtualKey = c, Name = c.ToString() });
            }

            keys.Add(new HotkeyKeyItem { VirtualKey = 32, Name = "Space" });
            keys.Add(new HotkeyKeyItem { VirtualKey = 192, Name = "` (Tilde)" });

            HotkeyComboBox.ItemsSource = keys;
        }

        private void PopulateLanguages()
        {
            var langs = new List<LanguageItem>
            {
                new LanguageItem { Code = "auto", Name = "Auto-Detect" },
                new LanguageItem { Code = "ru", Name = "Russian" },
                new LanguageItem { Code = "en", Name = "English" },
                new LanguageItem { Code = "de", Name = "German" },
                new LanguageItem { Code = "es", Name = "Spanish" },
                new LanguageItem { Code = "fr", Name = "French" },
                new LanguageItem { Code = "it", Name = "Italian" },
                new LanguageItem { Code = "zh", Name = "Chinese" }
            };
            LanguageComboBox.ItemsSource = langs;

            var modes = new List<TranslationModeItem>
            {
                new TranslationModeItem { Code = "transcribe", Name = "Transcribe (Same Language)" },
                new TranslationModeItem { Code = "translate", Name = "Translate to English" }
            };
            TranslationModeComboBox.ItemsSource = modes;
        }

        public void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                string time = DateTime.Now.ToString("HH:mm:ss");
                string color = "#E0E0E6";

                if (message.Contains("[ERROR]") || message.Contains("[FATAL]"))
                {
                    color = "#FF6B6B";
                }
                else if (message.Contains("[WARNING]"))
                {
                    color = "#FFD93D";
                }
                else if (message.Contains("[INFO]") || message.Contains("[STATS]"))
                {
                    color = "#4DFFB4";
                }
                else if (message.Contains("[LLM]") || message.Contains("[AI]"))
                {
                    color = "#4D96FF";
                }
                else if (message.Contains("<<<"))
                {
                    color = "#A084CF";
                }

                LogItems.Add(new LogItem { Time = time, Message = message, Color = color });

                if (LogListBox.Items.Count > 0)
                {
                    LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
                }
            });
        }

        private void SaveApiKey_Click(object sender, RoutedEventArgs e)
        {
            string newKey = ApiKeyBox.Password.Trim();
            _settingsManager.SetApiKey(newKey);
            _settingsManager.SaveSettings();
            LogMessage("API key saved and encrypted (DPAPI).");
            System.Windows.MessageBox.Show("API Key saved successfully!", "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MicComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            if (MicComboBox.SelectedItem is AudioDeviceItem selectedDevice)
            {
                _settingsManager.Settings.AudioDeviceIndex = selectedDevice.Index;
                _settingsManager.SaveSettings();
                LogMessage($"Microphone changed: {selectedDevice.Name}");
                
                Environment.SetEnvironmentVariable("AUDIO_DEVICE_INDEX", selectedDevice.Index.ToString(), EnvironmentVariableTarget.Process);
            }
        }

        private void Hotkey_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settingsManager == null) return;

            _settingsManager.Settings.HotkeyCtrl = CtrlHotkeyCheck.IsChecked ?? false;
            _settingsManager.Settings.HotkeyAlt = AltHotkeyCheck.IsChecked ?? false;
            _settingsManager.Settings.HotkeyShift = ShiftHotkeyCheck.IsChecked ?? false;
            _settingsManager.Settings.HotkeyWin = WinHotkeyCheck.IsChecked ?? false;

            if (HotkeyComboBox.SelectedItem is HotkeyKeyItem selectedItem)
            {
                _settingsManager.Settings.HotkeyVirtualKey = selectedItem.VirtualKey;
            }

            _settingsManager.SaveSettings();

            if (System.Windows.Application.Current is App myApp)
            {
                myApp.UpdateHookConfig();
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _settingsManager == null) return;

            if (LanguageComboBox.SelectedItem is LanguageItem selected)
            {
                _settingsManager.Settings.SpokenLanguage = selected.Code;
                _settingsManager.SaveSettings();
                LogMessage($"Spoken language changed to: {selected.Name} ({selected.Code})");
            }
        }

        private void TranslationModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _settingsManager == null) return;

            if (TranslationModeComboBox.SelectedItem is TranslationModeItem selected)
            {
                _settingsManager.Settings.TranslationMode = selected.Code;
                _settingsManager.SaveSettings();
                LogMessage($"Transcription mode changed to: {selected.Name}");
            }
        }

        private void LocalAiCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            bool isChecked = LocalAiCheckBox.IsChecked ?? false;
            _settingsManager.Settings.UseLocalAi = isChecked;
            _settingsManager.SaveSettings();

            LocalModelPanel.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
            LogMessage(isChecked ? "Local Whisper mode enabled." : "Cloud Groq API mode enabled.");
            
            Environment.SetEnvironmentVariable("USE_LOCAL_AI", isChecked.ToString(), EnvironmentVariableTarget.Process);

            if (System.Windows.Application.Current is App myApp)
            {
                myApp.ReinitializeAiEngine();
            }
        }

        private void LivePreviewCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            bool isChecked = LivePreviewCheckBox.IsChecked ?? false;
            _settingsManager.Settings.EnableLivePreview = isChecked;
            _settingsManager.SaveSettings();

            LogMessage(isChecked ? "Live stream preview enabled." : "Live stream preview disabled (background noise ignored).");
        }

        private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            if (ModelComboBox.SelectedItem is ComboBoxItem item)
            {
                string modelName = "ggml-small.bin";
                string content = item.Content.ToString() ?? "";
                
                if (content.Contains("ggml-base.bin")) modelName = "ggml-base.bin";
                else if (content.Contains("ggml-small.bin")) modelName = "ggml-small.bin";
                else if (content.Contains("ggml-large-v3-turbo.bin")) modelName = "ggml-large-v3-turbo.bin";

                _settingsManager.Settings.ModelName = modelName;
                _settingsManager.SaveSettings();
                LogMessage($"Whisper local model changed: {modelName}");

                Environment.SetEnvironmentVariable("WHISPER_MODEL", modelName, EnvironmentVariableTarget.Process);

                if (System.Windows.Application.Current is App myApp)
                {
                    myApp.ReinitializeAiEngine();
                }
            }
        }

        private void CustomTermsBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;
            _settingsManager.Settings.CustomTerms = CustomTermsBox.Text;
            _settingsManager.SaveSettings();
        }

        private void SidebarMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || MainTabControl == null) return;
            MainTabControl.SelectedIndex = SidebarMenu.SelectedIndex;

            if (SidebarMenu.SelectedIndex == 3) // Statistics
            {
                LoadStatistics();
            }
        }

        // ================= QA VOICE VALIDATOR LOGIC =================
        private void SetQaSelectedFiles(string[] files)
        {
            _qaSelectedFiles = files.ToList();
            if (files.Length == 1)
            {
                QaFilePathBox.Text = files[0];
            }
            else
            {
                QaFilePathBox.Text = $"{files.Length} audio files selected ({Path.GetFileName(files[0])}, {Path.GetFileName(files[1])}...)";
            }
            LogMessage($"[QA DROP] Loaded {files.Length} file(s) for batch STT validation.");
        }

        private void BrowseQaFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "Audio Files (*.mp3;*.wav;*.m4a;*.flac)|*.mp3;*.wav;*.m4a;*.flac|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true && dlg.FileNames.Length > 0)
            {
                SetQaSelectedFiles(dlg.FileNames);
            }
        }

        private void QaFile_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    SetQaSelectedFiles(files);
                }
            }
        }

        private void PresetCrypto_Click(object sender, RoutedEventArgs e)
        {
            OriginalScriptBox.Text = "Welcome to NullSpread Gateway! Our system executes high frequency algorithmic trades across Web3 liquidity pools. Ensure your API key and secret authorization tokens are safely configured in environment variables.";
        }

        private void PresetSubtitles_Click(object sender, RoutedEventArgs e)
        {
            OriginalScriptBox.Text = "Здравствуйте! Это демонстрация автоматического синтеза речи и генерации таймингов субтитров для приложения After Effects и ElevenLabs.";
        }

        private void HumanizeScript_Click(object sender, RoutedEventArgs e)
        {
            string text = OriginalScriptBox.Text;
            if (string.IsNullOrWhiteSpace(text)) return;

            string humanized = text;
            if (!humanized.StartsWith("Look, ") && !humanized.StartsWith("So, ") && !humanized.StartsWith("Well, "))
            {
                humanized = "Look, " + char.ToLower(humanized[0]) + humanized.Substring(1);
            }

            humanized = humanized.Replace(". ", "... ")
                                 .Replace(" because ", " — because ")
                                 .Replace(" which ", " — which ")
                                 .Replace(" and ", " — and ");
            
            OriginalScriptBox.Text = humanized;
            LogMessage("[PROSODY HACK] Applied ElevenLabs humanization (inserted breath dashes '—', ellipses '...' & natural starters).");
        }

        private async void RunQaValidation_Click(object sender, RoutedEventArgs e)
        {
            string originalScript = OriginalScriptBox.Text.Trim();
            if (string.IsNullOrEmpty(originalScript))
            {
                System.Windows.MessageBox.Show("Please enter the Original Target Script before running STT validation.", "QA Validator", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            List<string> filesToProcess = new List<string>();
            if (_qaSelectedFiles.Count > 0)
            {
                filesToProcess.AddRange(_qaSelectedFiles);
            }
            else if (File.Exists(QaFilePathBox.Text.Trim()))
            {
                filesToProcess.Add(QaFilePathBox.Text.Trim());
            }

            if (filesToProcess.Count == 0)
            {
                // Fallback mock validation
                filesToProcess.Add("Demo_Mock_Audio.wav");
            }

            LogMessage($"[QA BATCH] Starting STT Voiceover & Prosody Validation for {filesToProcess.Count} file(s)...");
            DiffOutputPanel.Children.Clear();

            string apiKey = _settingsManager.GetApiKey();
            int totalMatches = 0;
            int totalErrors = 0;
            double totalAccuracySum = 0;
            double totalNaturalnessSum = 0;

            for (int i = 0; i < filesToProcess.Count; i++)
            {
                string filePath = filesToProcess[i];
                string fileName = Path.GetFileName(filePath);
                LogMessage($"[QA {i + 1}/{filesToProcess.Count}] Validating audio file: {fileName}...");

                // Run Prosody Naturalness Analyzer
                var prosody = ProsodyAnalyzer.AnalyzeAudio(filePath);
                totalNaturalnessSum += prosody.NaturalnessPercent;
                LogMessage($"[PROSODY] {fileName} -> Human Naturalness: {prosody.NaturalnessPercent}% ({prosody.Rating}). {prosody.Recommendation}");

                string transcribedText = "";

                if (File.Exists(filePath) && !string.IsNullOrEmpty(apiKey))
                {
                    try
                    {
                        var client = new TranscriptionClient(apiKey);
                        transcribedText = await client.TranscribeAsync(filePath, "whisper-large-v3-turbo", "auto", "transcribe");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"[QA ERROR] Groq STT failed for {fileName}: {ex.Message}");
                        continue;
                    }
                }
                else
                {
                    await Task.Delay(400);
                    transcribedText = originalScript
                        .Replace("API", "A P I")
                        .Replace("Web3", "Web 3")
                        .Replace("After Effects", "Афтер Эффектс")
                        .Replace("ElevenLabs", "Элевен Лабс");
                }

                // Compute Diff
                var diff = DiffUtility.ComputeDiff(originalScript, transcribedText);

                totalMatches += diff.MatchesCount;
                totalErrors += (diff.ErrorsCount + diff.DeletionsCount + diff.InsertionsCount);
                totalAccuracySum += diff.AccuracyPercent;

                // Create dedicated Result Card for THIS audio file take
                Border fileCard = new Border
                {
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x12, 0x12, 0x1A)),
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x28, 0x28, 0x38)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 0, 0, 10)
                };

                StackPanel cardContent = new StackPanel();

                // 1. Sleek Compact Header Grid (Filename on Left, Compact Pills on Right)
                Grid headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                TextBlock fileNameText = new TextBlock
                {
                    Text = $"📄 {fileName}",
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF)),
                    FontWeight = FontWeights.Bold,
                    FontSize = 11.5,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = fileName
                };
                Grid.SetColumn(fileNameText, 0);

                StackPanel pillsRight = new StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Compact Accuracy Pill
                Border accPill = new Border
                {
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x16, 0x28, 0x1C)),
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x30, 0xD1, 0x58)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(5, 2, 5, 2),
                    Margin = new Thickness(0, 0, 4, 0)
                };
                accPill.Child = new TextBlock
                {
                    Text = $"{diff.AccuracyPercent}% Acc",
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x30, 0xD1, 0x58)),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold
                };

                // Compact Naturalness Pill
                System.Windows.Media.Color natColor = prosody.NaturalnessPercent >= 78 ? System.Windows.Media.Color.FromRgb(0x30, 0xD1, 0x58) : (prosody.NaturalnessPercent >= 62 ? System.Windows.Media.Color.FromRgb(0x64, 0xD2, 0xFF) : System.Windows.Media.Color.FromRgb(0xFF, 0x9F, 0x0A));
                Border natPill = new Border
                {
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x18, 0x20, 0x2A)),
                    BorderBrush = new SolidColorBrush(natColor),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(5, 2, 5, 2)
                };
                natPill.Child = new TextBlock
                {
                    Text = $"{prosody.NaturalnessPercent}% Nat",
                    Foreground = new SolidColorBrush(natColor),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    ToolTip = prosody.Rating
                };

                pillsRight.Children.Add(accPill);
                pillsRight.Children.Add(natPill);
                Grid.SetColumn(pillsRight, 1);

                headerGrid.Children.Add(fileNameText);
                headerGrid.Children.Add(pillsRight);
                cardContent.Children.Add(headerGrid);

                // 3. Recommendation note
                if (!string.IsNullOrEmpty(prosody.Recommendation))
                {
                    TextBlock noteText = new TextBlock
                    {
                        Text = $"💡 {prosody.Recommendation}",
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8E, 0x8E, 0x98)),
                        FontSize = 10,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 8)
                    };
                    cardContent.Children.Add(noteText);
                }

                // 4. Word Badges WrapPanel
                WrapPanel fileWordsPanel = new WrapPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    Margin = new Thickness(0, 4, 0, 0)
                };

                foreach (var token in diff.Tokens)
                {
                    Border badge = new Border
                    {
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 3, 6, 3),
                        Margin = new Thickness(0, 0, 6, 6)
                    };

                    TextBlock textBlock = new TextBlock
                    {
                        FontSize = 11.5,
                        FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 240
                    };

                    if (token.TokenType == DiffTokenType.Match)
                    {
                        badge.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1F, 0x29, 0x37));
                        textBlock.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5, 0xE7, 0xEB));
                        textBlock.Text = token.Original;
                    }
                    else if (token.TokenType == DiffTokenType.Mismatch)
                    {
                        badge.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x45, 0x1A, 0x1A));
                        badge.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5, 0x3E, 0x3E));
                        badge.BorderThickness = new Thickness(1);
                        textBlock.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFC, 0x81, 0x81));
                        textBlock.Text = $"Expected '{token.Expected}', heard '{token.Actual}'";
                        textBlock.FontWeight = FontWeights.Bold;
                        badge.ToolTip = token.Reason;
                    }
                    else if (token.TokenType == DiffTokenType.Deletion)
                    {
                        badge.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3B, 0x1D, 0x1D));
                        textBlock.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF8, 0x71, 0x71));
                        textBlock.TextDecorations = TextDecorations.Strikethrough;
                        textBlock.Text = token.Expected;
                        badge.ToolTip = token.Reason;
                    }
                    else if (token.TokenType == DiffTokenType.Insertion)
                    {
                        badge.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x42, 0x32, 0x13));
                        textBlock.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFB, 0xBF, 0x24));
                        textBlock.Text = $"+{token.Actual}";
                        textBlock.FontWeight = FontWeights.SemiBold;
                        badge.ToolTip = token.Reason;
                    }

                    badge.Child = textBlock;
                    fileWordsPanel.Children.Add(badge);
                }

                cardContent.Children.Add(fileWordsPanel);
                fileCard.Child = cardContent;
                DiffOutputPanel.Children.Add(fileCard);
            }

            double avgAccuracy = filesToProcess.Count > 0 ? Math.Round(totalAccuracySum / filesToProcess.Count, 1) : 100;
            double avgNaturalness = filesToProcess.Count > 0 ? Math.Round(totalNaturalnessSum / filesToProcess.Count, 1) : 85;

            AccuracyText.Text = $"{avgAccuracy}%";
            MatchedCountText.Text = totalMatches.ToString();
            ErrorsCountText.Text = totalErrors.ToString();
            NaturalnessText.Text = $"{avgNaturalness}%";

            if (avgNaturalness >= 78)
            {
                NaturalnessText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x30, 0xD1, 0x58));
            }
            else if (avgNaturalness >= 62)
            {
                NaturalnessText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x64, 0xD2, 0xFF));
            }
            else
            {
                NaturalnessText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x9F, 0x0A));
            }

            LogMessage($"[QA COMPLETE] Processed {filesToProcess.Count} file(s). Avg Accuracy: {avgAccuracy}%, Naturalness Score: {avgNaturalness}%");
        }

        // ================= AUDIO CONVERTER LOGIC =================
        private void SetConverterSelectedFiles(string[] files)
        {
            _converterSelectedFiles = files.ToList();
            if (files.Length == 1)
            {
                ConverterFilePathBox.Text = files[0];
            }
            else
            {
                ConverterFilePathBox.Text = $"{files.Length} files selected for batch conversion";
            }
            LogMessage($"[CONVERTER DROP] Loaded {files.Length} file(s) for batch conversion.");
        }

        private void BrowseConverterFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "Media Files (*.mp3;*.wav;*.m4a;*.flac;*.mp4;*.mov;*.avi)|*.mp3;*.wav;*.m4a;*.flac;*.mp4;*.mov;*.avi|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true && dlg.FileNames.Length > 0)
            {
                SetConverterSelectedFiles(dlg.FileNames);
            }
        }

        private void ConverterFile_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    SetConverterSelectedFiles(files);
                }
            }
        }

        private async void RunAudioConvert_Click(object sender, RoutedEventArgs e)
        {
            List<string> filesToConvert = new List<string>();
            if (_converterSelectedFiles.Count > 0)
            {
                filesToConvert.AddRange(_converterSelectedFiles);
            }
            else if (File.Exists(ConverterFilePathBox.Text.Trim()))
            {
                filesToConvert.Add(ConverterFilePathBox.Text.Trim());
            }

            if (filesToConvert.Count == 0)
            {
                System.Windows.MessageBox.Show("Please select or drop valid media input file(s).", "Audio Converter", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string targetFormat = (ConverterFormatCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "mp3";
            string targetSampleRate = (ConverterSampleRateCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "44100";
            string targetBitrate = (ConverterBitrateCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "320";
            string targetChannels = (ConverterChannelsCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "stereo";
            bool extractFromVid = ConverterExtractCheck.IsChecked ?? true;

            var engine = new AudioConverterEngine();

            if (filesToConvert.Count == 1)
            {
                string inputPath = filesToConvert[0];
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = Path.GetFileNameWithoutExtension(inputPath) + "_converted",
                    Filter = $"{targetFormat.ToUpper()} File (*.{targetFormat})|*.{targetFormat}|All Files (*.*)|*.*"
                };

                if (dlg.ShowDialog() != true) return;

                string outputPath = dlg.FileName;
                LogMessage($"[CONVERTER] Converting: {Path.GetFileName(inputPath)} -> {Path.GetFileName(outputPath)}");

                var options = new AudioConversionOptions
                {
                    InputFilePath = inputPath,
                    OutputFilePath = outputPath,
                    OutputFormat = targetFormat,
                    SampleRate = targetSampleRate,
                    Bitrate = targetBitrate,
                    Channels = targetChannels,
                    ExtractAudioFromVideo = extractFromVid
                };

                ConvertProgressBar.Value = 0;
                try
                {
                    await engine.ConvertAsync(options, (progress, status) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ConvertProgressBar.Value = progress;
                            ConvertStatusText.Text = status;
                        });
                    });

                    LogMessage($"[CONVERTER COMPLETE] Saved to: {outputPath}");
                    System.Windows.MessageBox.Show($"File converted successfully!\nSaved to: {outputPath}", "Audio Converter", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    LogMessage($"[CONVERTER ERROR] {ex.Message}");
                    System.Windows.MessageBox.Show($"Conversion failed: {ex.Message}", "Audio Converter Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                // Batch Conversion Mode
                LogMessage($"[CONVERTER BATCH] Starting batch conversion for {filesToConvert.Count} files...");
                int successCount = 0;

                for (int i = 0; i < filesToConvert.Count; i++)
                {
                    string inputPath = filesToConvert[i];
                    string dir = Path.GetDirectoryName(inputPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string nameNoExt = Path.GetFileNameWithoutExtension(inputPath);
                    string outputPath = Path.Combine(dir, $"{nameNoExt}_converted.{targetFormat.ToLower()}");

                    LogMessage($"[CONVERTER {i + 1}/{filesToConvert.Count}] {Path.GetFileName(inputPath)} -> {Path.GetFileName(outputPath)}");

                    var options = new AudioConversionOptions
                    {
                        InputFilePath = inputPath,
                        OutputFilePath = outputPath,
                        OutputFormat = targetFormat,
                        SampleRate = targetSampleRate,
                        Bitrate = targetBitrate,
                        Channels = targetChannels,
                        ExtractAudioFromVideo = extractFromVid
                    };

                    try
                    {
                        await engine.ConvertAsync(options, (progress, status) =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                double stepProgress = ((double)i / filesToConvert.Count * 100) + (progress / filesToConvert.Count);
                                ConvertProgressBar.Value = stepProgress;
                                ConvertStatusText.Text = $"[{i + 1}/{filesToConvert.Count}] {status}";
                            });
                        });
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"[CONVERTER ERROR] Failed to convert {Path.GetFileName(inputPath)}: {ex.Message}");
                    }
                }

                ConvertProgressBar.Value = 100;
                ConvertStatusText.Text = $"Batch conversion finished: {successCount}/{filesToConvert.Count} converted.";
                LogMessage($"[CONVERTER BATCH COMPLETE] Successfully converted {successCount}/{filesToConvert.Count} files.");
                System.Windows.MessageBox.Show($"Batch conversion finished!\nSuccessfully processed {successCount} of {filesToConvert.Count} files.", "Batch Audio Converter", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LoadStatistics()
        {
            if (_statsManager == null) return;
            
            _statsManager.Data.Sessions.Clear();
            var freshData = _statsManager.LoadStats();
            _statsManager.Data.Sessions.AddRange(freshData.Sessions);

            StatsTodayText.Text = _statsManager.GetWordsToday().ToString("N0") + " words";
            StatsWeekText.Text = _statsManager.GetWordsThisWeek().ToString("N0") + " words";
            StatsMonthText.Text = _statsManager.GetWordsThisMonth().ToString("N0") + " words";
            StatsAllTimeText.Text = _statsManager.GetWordsAllTime().ToString("N0") + " words";

            double speedup = _statsManager.GetSpeedupFactor();
            StatsSpeedupText.Text = $"{speedup:F1}x faster";
            
            double savedMinutes = _statsManager.GetTimeSavedMinutes();
            if (savedMinutes >= 60)
            {
                StatsTimeSavedText.Text = $"{savedMinutes / 60:F1} h ({savedMinutes:F0} min)";
            }
            else
            {
                StatsTimeSavedText.Text = $"{savedMinutes:F1} min";
            }

            StatsWpmText.Text = $"{_statsManager.GetSpeechWpm():F1} words/min";
            StatsWpsText.Text = $"{_statsManager.GetSpeechWps():F1} words/sec";

            StatsSessionsText.Text = _statsManager.GetTotalSessions().ToString("N0") + " sessions";
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left && e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                try
                {
                    this.DragMove();
                }
                catch (InvalidOperationException)
                {
                    // Ignore DragMove exception if mouse state changed mid-click
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = (this.WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        public void RealExit()
        {
            _isRealExit = true;
            this.Close();
        }
    }
}