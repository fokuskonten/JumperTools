using System;

namespace JumperTools.Models
{
    public class HardwareTestResult
    {
        public string Device { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string Driver { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public string Tool { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Result { get; set; } = "NOT TESTED"; // PASS, FAIL, NOT TESTED
        public string Log { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public string HardwareType { get; set; } = "PHYSICAL"; // PHYSICAL, NOT DETECTED, NOT TESTED
        public string RecoveryResult { get; set; } = string.Empty;
    }
}
