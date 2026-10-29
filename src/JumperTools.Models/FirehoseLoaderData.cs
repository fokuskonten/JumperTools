using System;

namespace JumperTools.Models
{
    public class FirehoseLoaderData
    {
        public string HWID { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string OEMID { get; set; } = string.Empty;
        public string MODELID { get; set; } = string.Empty;
        public string HASHID { get; set; } = string.Empty;
        public string Trademark { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string AltName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        
        public string DisplayName => $"{Trademark} {Model} {(!string.IsNullOrEmpty(AltName) ? $"({AltName})" : "")}".Trim();
        public bool HasUrl => !string.IsNullOrEmpty(Url);
        public string CleanUrl => Url?.Trim('#') ?? string.Empty;
        public string FileName
        {
            get
            {
                if (!string.IsNullOrEmpty(CleanUrl))
                {
                    var name = System.IO.Path.GetFileName(CleanUrl);
                    if (!string.IsNullOrEmpty(name)) return name;
                }
                return !string.IsNullOrEmpty(FullName) ? FullName : "prog_firehose.mbn";
            }
        }
        public string StatusDisplay => HasUrl ? "Koleksi Online" : "Database Lokal";
    }
}
