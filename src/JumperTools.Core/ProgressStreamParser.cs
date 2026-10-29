using System;
using System.Text.RegularExpressions;

namespace JumperTools.Core
{
    /// <summary>
    /// High-performance stream parser that extracts progress percentages (0-100%) from CLI tool outputs in real-time
    /// </summary>
    public static class ProgressStreamParser
    {
        private static readonly Regex[] ProgressPatterns = new[]
        {
            // [ 45% ] or [45%]
            new Regex(@"\[\s*(\d{1,3})%\s*\]", RegexOptions.Compiled),
            
            // 45% or 45.5%
            new Regex(@"\b(\d{1,3}(?:\.\d+)?)%\b", RegexOptions.Compiled),
            
            // Writing 'system' ... 45%
            new Regex(@"\.\.\.\s*(\d{1,3})%", RegexOptions.Compiled),
            
            // Progress: 45 / 100 or (45/100)
            new Regex(@"\(?\s*(\d+)\s*/\s*(\d+)\s*\)?", RegexOptions.Compiled),
            
            // 45MB / 100MB
            new Regex(@"(\d+(?:\.\d+)?)\s*(?:MB|KB|GB|B)\s*/\s*(\d+(?:\.\d+)?)\s*(?:MB|KB|GB|B)", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        };

        /// <summary>
        /// Try to extract a normalized percentage (0 to 100) from a line of CLI output
        /// </summary>
        public static bool TryParseProgress(string line, out int progressPercentage)
        {
            progressPercentage = 0;
            if (string.IsNullOrWhiteSpace(line)) return false;

            // Pattern 1: Direct percentage matches [XX%] or XX%
            for (int i = 0; i < 3; i++)
            {
                var match = ProgressPatterns[i].Match(line);
                if (match.Success && match.Groups.Count > 1)
                {
                    if (double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                    {
                        var clamped = (int)Math.Clamp(Math.Round(val), 0, 100);
                        if (clamped > 0 || line.Contains("0%"))
                        {
                            progressPercentage = clamped;
                            return true;
                        }
                    }
                }
            }

            // Pattern 2: Fraction match (X / Y)
            var fracMatch = ProgressPatterns[3].Match(line);
            if (fracMatch.Success && fracMatch.Groups.Count > 2)
            {
                if (double.TryParse(fracMatch.Groups[1].Value, out double current) &&
                    double.TryParse(fracMatch.Groups[2].Value, out double total) && total > 0)
                {
                    var calc = (int)Math.Clamp(Math.Round((current / total) * 100.0), 0, 100);
                    progressPercentage = calc;
                    return true;
                }
            }

            // Pattern 3: Byte transfer fraction match (X MB / Y MB)
            var byteMatch = ProgressPatterns[4].Match(line);
            if (byteMatch.Success && byteMatch.Groups.Count > 2)
            {
                if (double.TryParse(byteMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double currentBytes) &&
                    double.TryParse(byteMatch.Groups[2].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double totalBytes) && totalBytes > 0)
                {
                    var calc = (int)Math.Clamp(Math.Round((currentBytes / totalBytes) * 100.0), 0, 100);
                    progressPercentage = calc;
                    return true;
                }
            }

            return false;
        }
    }
}
