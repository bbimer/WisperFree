using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FreeFlowWin.Core.QA
{
    public enum DiffTokenType
    {
        Match,
        Mismatch,
        Deletion,
        Insertion
    }

    public class DiffToken
    {
        public DiffTokenType Type { get; set; }
        public DiffTokenType TokenType { get; set; }
        public string Original { get; set; } = string.Empty;
        public string Transcribed { get; set; } = string.Empty;
        public string Expected { get; set; } = string.Empty;
        public string Actual { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public class DiffResult
    {
        public List<DiffToken> Tokens { get; set; } = new List<DiffToken>();
        public int AccuracyPercent { get; set; } = 100;
        public int MatchesCount { get; set; }
        public int ErrorsCount { get; set; }
        public int DeletionsCount { get; set; }
        public int InsertionsCount { get; set; }
        public int TotalScriptWords { get; set; }
        public int TotalTranscribedWords { get; set; }
    }

    public static class DiffUtility
    {
        private static string CleanWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return string.Empty;
            return Regex.Replace(word.ToLowerInvariant(), @"[.,/#!$%^&*;:{}=\-_`~()?""'’]", "").Trim();
        }

        public static DiffResult ComputeDiff(string originalText, string transcribedText)
        {
            var result = new DiffResult();
            var origWords = (originalText ?? "").Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var transWords = (transcribedText ?? "").Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            result.TotalScriptWords = origWords.Length;
            result.TotalTranscribedWords = transWords.Length;

            if (origWords.Length == 0 && transWords.Length == 0)
            {
                return result;
            }

            var origClean = origWords.Select(CleanWord).ToArray();
            var transClean = transWords.Select(CleanWord).ToArray();

            int m = origClean.Length;
            int n = transClean.Length;
            int[,] dp = new int[m + 1, n + 1];

            for (int i = 1; i <= m; i++)
            {
                for (int j = 1; j <= n; j++)
                {
                    if (origClean[i - 1] == transClean[j - 1])
                    {
                        dp[i, j] = dp[i - 1, j - 1] + 1;
                    }
                    else
                    {
                        dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                    }
                }
            }

            int currI = m;
            int currJ = n;
            var rawList = new List<DiffToken>();

            while (currI > 0 || currJ > 0)
            {
                if (currI > 0 && currJ > 0 && origClean[currI - 1] == transClean[currJ - 1])
                {
                    rawList.Add(new DiffToken
                    {
                        TokenType = DiffTokenType.Match,
                        Original = origWords[currI - 1],
                        Transcribed = transWords[currJ - 1]
                    });
                    currI--;
                    currJ--;
                }
                else if (currJ > 0 && (currI == 0 || dp[currI, currJ - 1] >= dp[currI - 1, currJ]))
                {
                    rawList.Add(new DiffToken
                    {
                        TokenType = DiffTokenType.Insertion,
                        Actual = transWords[currJ - 1],
                        Reason = $"Extra word: '{transWords[currJ - 1]}'"
                    });
                    currJ--;
                }
                else if (currI > 0 && (currJ == 0 || dp[currI, currJ - 1] < dp[currI - 1, currJ]))
                {
                    rawList.Add(new DiffToken
                    {
                        TokenType = DiffTokenType.Deletion,
                        Expected = origWords[currI - 1],
                        Reason = $"Missing word: '{origWords[currI - 1]}'"
                    });
                    currI--;
                }
            }

            rawList.Reverse();

            // Merge deletion + insertion into Mismatch
            for (int idx = 0; idx < rawList.Count; idx++)
            {
                var curr = rawList[idx];
                var next = (idx + 1 < rawList.Count) ? rawList[idx + 1] : null;

                if (curr.TokenType == DiffTokenType.Match)
                {
                    result.Tokens.Add(curr);
                    result.MatchesCount++;
                }
                else if (curr.TokenType == DiffTokenType.Deletion && next != null && next.TokenType == DiffTokenType.Insertion)
                {
                    result.Tokens.Add(new DiffToken
                    {
                        TokenType = DiffTokenType.Mismatch,
                        Expected = curr.Expected,
                        Actual = next.Actual,
                        Reason = $"Expected '{curr.Expected}', heard '{next.Actual}'"
                    });
                    result.ErrorsCount++;
                    idx++; // skip next
                }
                else if (curr.TokenType == DiffTokenType.Deletion)
                {
                    result.Tokens.Add(curr);
                    result.DeletionsCount++;
                }
                else if (curr.TokenType == DiffTokenType.Insertion)
                {
                    result.Tokens.Add(curr);
                    result.InsertionsCount++;
                }
            }

            int totalOriginal = origWords.Length;
            if (totalOriginal > 0)
            {
                int score = (int)Math.Round(((double)result.MatchesCount / Math.Max(totalOriginal, totalOriginal + result.InsertionsCount)) * 100);
                result.AccuracyPercent = Math.Clamp(score, 0, 100);
            }
            else
            {
                result.AccuracyPercent = 100;
            }

            return result;
        }
    }
}
