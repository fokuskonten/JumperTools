using System;
using SQLite;

namespace JumperTools.Models
{
    public enum JobOrderStatus
    {
        Diterima,
        Diproses,
        Selesai,
        Diambil,
        Batal
    }

    /// <summary>
    /// Model for mobile repair service job orders and customer receipts
    /// </summary>
    public class JobOrderInfo
    {
        [PrimaryKey]
        public string JobId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string DeviceBrand { get; set; } = string.Empty;
        public string DeviceModel { get; set; } = string.Empty;
        public string DeviceImei { get; set; } = string.Empty;
        public string DeviceSerial { get; set; } = string.Empty;
        public string ServiceType { get; set; } = "Bypass FRP / Kunci Layar";
        public string ProblemDescription { get; set; } = "Lupa Akun Google / Pola";
        public decimal Cost { get; set; } = 150000;
        public int WarrantyDays { get; set; } = 7;
        public JobOrderStatus Status { get; set; } = JobOrderStatus.Selesai;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; } = DateTime.Now;
        public string TechnicianName { get; set; } = "Teknisi JumperTools";
        public string Notes { get; set; } = string.Empty;
    }
}
