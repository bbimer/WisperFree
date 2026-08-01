using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FreeFlowWin.Core.Telemetry
{
    public class DictationSession
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public int WordCount { get; set; }
        public double DurationSeconds { get; set; }
    }

    public class StatsData
    {
        public List<DictationSession> Sessions { get; set; } = new List<DictationSession>();
    }

    public class StatsManager
    {
        private readonly string _statsFilePath;
        private StatsData _data;

        public StatsData Data => _data;

        public StatsManager()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "FreeFlowWindows");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            _statsFilePath = Path.Combine(folder, "stats.json");
            _data = LoadStats();
        }

        public void AddSession(int wordCount, double durationSeconds)
        {
            if (wordCount <= 0) return;
            
            _data.Sessions.Add(new DictationSession
            {
                Timestamp = DateTime.Now,
                WordCount = wordCount,
                DurationSeconds = durationSeconds
            });
            
            SaveStats();
        }

        public int GetWordsToday()
        {
            var today = DateTime.Today;
            return _data.Sessions
                .Where(s => s.Timestamp.Date == today)
                .Sum(s => s.WordCount);
        }

        public int GetWordsThisWeek()
        {
            var weekLimit = DateTime.Today.AddDays(-7);
            return _data.Sessions
                .Where(s => s.Timestamp >= weekLimit)
                .Sum(s => s.WordCount);
        }

        public int GetWordsThisMonth()
        {
            var monthLimit = DateTime.Today.AddDays(-30);
            return _data.Sessions
                .Where(s => s.Timestamp >= monthLimit)
                .Sum(s => s.WordCount);
        }

        public int GetWordsAllTime()
        {
            return _data.Sessions.Sum(s => s.WordCount);
        }

        public double GetSpeedupFactor(double manualWpm = 40)
        {
            if (!_data.Sessions.Any()) return 4.0; // По умолчанию в 4 раза быстрее

            double totalWords = _data.Sessions.Sum(s => s.WordCount);
            double totalSeconds = _data.Sessions.Sum(s => s.DurationSeconds);

            if (totalSeconds <= 0) return 4.0;

            double voiceWpm = (totalWords / totalSeconds) * 60;
            double factor = voiceWpm / manualWpm;

            return Math.Round(Math.Max(1.0, factor), 1);
        }

        public double GetTimeSavedMinutes(double manualWpm = 40)
        {
            double totalWords = _data.Sessions.Sum(s => s.WordCount);
            double totalSeconds = _data.Sessions.Sum(s => s.DurationSeconds);
            
            double manualTimeMinutes = totalWords / manualWpm;
            double voiceTimeMinutes = totalSeconds / 60;
            
            double saved = manualTimeMinutes - voiceTimeMinutes;
            return Math.Round(Math.Max(0.0, saved), 1);
        }

        public int GetTotalSessions()
        {
            return _data.Sessions.Count;
        }

        public double GetSpeechWpm()
        {
            if (!_data.Sessions.Any()) return 140.0; // Средняя скорость речи в русском языке ~140 слов/мин

            double totalWords = _data.Sessions.Sum(s => s.WordCount);
            double totalSeconds = _data.Sessions.Sum(s => s.DurationSeconds);

            if (totalSeconds <= 0) return 140.0;

            return Math.Round((totalWords / totalSeconds) * 60, 1);
        }

        public double GetSpeechWps()
        {
            if (!_data.Sessions.Any()) return 2.3; // 140 / 60 = 2.33

            double totalWords = _data.Sessions.Sum(s => s.WordCount);
            double totalSeconds = _data.Sessions.Sum(s => s.DurationSeconds);

            if (totalSeconds <= 0) return 2.3;

            return Math.Round(totalWords / totalSeconds, 1);
        }

        public StatsData LoadStats()
        {
            if (!File.Exists(_statsFilePath))
            {
                return new StatsData();
            }

            try
            {
                string json = File.ReadAllText(_statsFilePath);
                return JsonSerializer.Deserialize<StatsData>(json) ?? new StatsData();
            }
            catch
            {
                return new StatsData();
            }
        }

        private void SaveStats()
        {
            try
            {
                string json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_statsFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to save statistics: {ex.Message}");
            }
        }
    }
}
