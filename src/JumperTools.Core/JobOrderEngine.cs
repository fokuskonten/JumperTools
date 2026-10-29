using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using JumperTools.Data;
using JumperTools.Models;

namespace JumperTools.Core
{
    /// <summary>
    /// Engine for managing repair job orders, printing thermal receipts, and formatting WhatsApp CRM notifications
    /// </summary>
    public class JobOrderEngine
    {
        public event Action<string>? OnLog;
        private readonly DatabaseContext? _db;

        public JobOrderEngine(DatabaseContext? db = null)
        {
            _db = db;
        }

        public List<JobOrderInfo> GetAllSavedJobs()
        {
            return _db?.GetAllJobOrders() ?? new List<JobOrderInfo>();
        }

        public void SaveJobToDb(JobOrderInfo job)
        {
            _db?.SaveJobOrder(job);
        }

        public void DeleteJobFromDb(string jobId)
        {
            _db?.DeleteJobOrder(jobId);
        }

        private void Log(string message)
        {
            var formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
            OnLog?.Invoke(formatted);
        }

        /// <summary>
        /// Generate a unique sequential Job ID
        /// </summary>
        public string GenerateJobId()
        {
            var datePart = DateTime.Now.ToString("yyyyMMdd");
            var randPart = new Random().Next(100, 999);
            return $"FK-{datePart}-{randPart}";
        }

        /// <summary>
        /// Generate 58mm / 80mm Plain Text Monospace Receipt for Thermal Printers
        /// </summary>
        public string GenerateThermalReceiptText(JobOrderInfo job, string storeName = "JUMPERTOOLS SERVICE CENTER")
        {
            var sb = new StringBuilder();
            sb.AppendLine("================================");
            sb.AppendLine($"     {storeName}     ");
            sb.AppendLine("  Spesialis Servis & Software HP");
            sb.AppendLine("================================");
            sb.AppendLine($"No. Nota  : {job.JobId}");
            sb.AppendLine($"Tanggal   : {job.CreatedAt:dd/MM/yyyy HH:mm}");
            sb.AppendLine($"Pelanggan : {job.CustomerName}");
            sb.AppendLine($"No. WA    : {job.CustomerPhone}");
            sb.AppendLine("--------------------------------");
            sb.AppendLine($"Tipe HP   : {job.DeviceBrand} {job.DeviceModel}");
            if (!string.IsNullOrEmpty(job.DeviceImei))
                sb.AppendLine($"IMEI      : {job.DeviceImei}");
            sb.AppendLine($"Layanan   : {job.ServiceType}");
            sb.AppendLine($"Keluhan   : {job.ProblemDescription}");
            sb.AppendLine("--------------------------------");
            sb.AppendLine($"Biaya     : Rp {job.Cost:N0}");
            sb.AppendLine($"Garansi   : {job.WarrantyDays} Hari");
            sb.AppendLine($"Status    : {job.Status.ToString().ToUpper()}");
            sb.AppendLine($"Teknisi   : {job.TechnicianName}");
            sb.AppendLine("================================");
            sb.AppendLine("Syarat Garansi:");
            sb.AppendLine("1. Nota wajib dibawa saat klaim.");
            sb.AppendLine("2. Segel utuh & tidak kena air.");
            sb.AppendLine("      Terima Kasih! :)");
            sb.AppendLine("================================\n");

            return sb.ToString();
        }

        /// <summary>
        /// Generate WhatsApp formatted message text
        /// </summary>
        public string GenerateWhatsAppMessage(JobOrderInfo job, string storeName = "JUMPERTOOLS SERVICE CENTER")
        {
            var sb = new StringBuilder();
            sb.AppendLine($"🧾 *NOTA TANDA TERIMA SERVIS HP*");
            sb.AppendLine($"*{storeName}*");
            sb.AppendLine($"────────────────────────");
            sb.AppendLine($"📌 *No. Nota:* `{job.JobId}`");
            sb.AppendLine($"👤 *Pelanggan:* {job.CustomerName}");
            sb.AppendLine($"📱 *Perangkat:* {job.DeviceBrand} {job.DeviceModel}");
            if (!string.IsNullOrEmpty(job.DeviceImei))
                sb.AppendLine($"🔢 *IMEI:* `{job.DeviceImei}`");
            sb.AppendLine($"🛠️ *Layanan:* {job.ServiceType}");
            sb.AppendLine($"💰 *Total Biaya:* Rp {job.Cost:N0}");
            sb.AppendLine($"🛡️ *Masa Garansi:* {job.WarrantyDays} Hari");
            sb.AppendLine($"✅ *Status:* *{job.Status.ToString().ToUpper()}*");
            sb.AppendLine($"────────────────────────");
            sb.AppendLine($"_Terima kasih telah mempercayakan perbaikan gadget Anda kepada kami._ 🙏");

            return sb.ToString();
        }

        /// <summary>
        /// Generate a valid WhatsApp direct click-to-chat link
        /// </summary>
        public string GenerateWhatsAppUrl(JobOrderInfo job, string storeName = "JUMPERTOOLS SERVICE CENTER")
        {
            var phone = job.CustomerPhone.Trim().Replace("-", "").Replace(" ", "").Replace("+", "");
            if (phone.StartsWith("08") || phone.StartsWith("0"))
            {
                phone = "62" + phone.TrimStart('0');
            }
            var text = GenerateWhatsAppMessage(job, storeName);
            return $"https://wa.me/{phone}?text={Uri.EscapeDataString(text)}";
        }

        /// <summary>
        /// Save receipt to an HTML file for printing via browser or system printer
        /// </summary>
        public async Task<string> SaveReceiptHtmlAsync(JobOrderInfo job, string outputDir, string storeName = "JUMPERTOOLS SERVICE CENTER")
        {
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
            var filePath = Path.Combine(outputDir, $"Nota_{job.JobId}.html");

            var html = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <title>Nota Servis - {job.JobId}</title>
    <style>
        body {{ font-family: 'Courier New', monospace; width: 300px; margin: 0 auto; padding: 10px; font-size: 12px; }}
        .center {{ text-align: center; }}
        .line {{ border-top: 1px dashed #000; margin: 8px 0; }}
        .bold {{ font-weight: bold; }}
        .row {{ display: flex; justify-content: space-between; margin-bottom: 4px; }}
    </style>
</head>
<body onload='window.print()'>
    <div class='center'>
        <div class='bold' style='font-size: 15px;'>{storeName}</div>
        <div>Spesialis Software & Servis HP</div>
    </div>
    <div class='line'></div>
    <div class='row'><span>No. Nota:</span><span class='bold'>{job.JobId}</span></div>
    <div class='row'><span>Tanggal:</span><span>{job.CreatedAt:dd/MM/yyyy HH:mm}</span></div>
    <div class='row'><span>Pelanggan:</span><span>{job.CustomerName}</span></div>
    <div class='row'><span>No. WA:</span><span>{job.CustomerPhone}</span></div>
    <div class='line'></div>
    <div class='row'><span>Perangkat:</span><span class='bold'>{job.DeviceBrand} {job.DeviceModel}</span></div>
    <div class='row'><span>Layanan:</span><span>{job.ServiceType}</span></div>
    <div class='row'><span>Biaya:</span><span class='bold'>Rp {job.Cost:N0}</span></div>
    <div class='row'><span>Garansi:</span><span>{job.WarrantyDays} Hari</span></div>
    <div class='row'><span>Status:</span><span class='bold'>{job.Status.ToString().ToUpper()}</span></div>
    <div class='line'></div>
    <div class='center'>Terima Kasih atas Kunjungan Anda!</div>
</body>
</html>";

            await File.WriteAllTextAsync(filePath, html);
            Log($"[Nota] File nota HTML dibuat di: {filePath}");
            return filePath;
        }
    }
}
