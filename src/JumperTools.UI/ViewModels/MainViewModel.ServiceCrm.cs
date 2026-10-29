using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using JumperTools.Models;

namespace JumperTools.UI.ViewModels
{
    public partial class MainViewModel
    {
        // ==================== JOB ORDER & RECEIPT OPERATIONS ====================

        public void CreateJobOrder()
        {
            var job = new JobOrderInfo
            {
                JobId = _jobEngine.GenerateJobId(),
                CustomerName = string.IsNullOrWhiteSpace(NewCustomerName) ? "Pelanggan Umum" : NewCustomerName,
                CustomerPhone = NewCustomerPhone,
                DeviceBrand = string.IsNullOrWhiteSpace(NewDeviceBrand) ? "Universal" : NewDeviceBrand,
                DeviceModel = string.IsNullOrWhiteSpace(NewDeviceModel) ? "Smartphone" : NewDeviceModel,
                DeviceImei = SelectedDevice?.Imei ?? "",
                DeviceSerial = SelectedDevice?.SerialNumber ?? "",
                ServiceType = string.IsNullOrWhiteSpace(NewServiceType) ? "Servis Software / Hardware" : NewServiceType,
                Cost = NewCost,
                WarrantyDays = NewWarrantyDays,
                Status = JobOrderStatus.Selesai,
                CreatedAt = DateTime.Now
            };

            JobOrders.Insert(0, job);
            SelectedJobOrder = job;
            OnPropertyChanged(nameof(HasNoJobOrders));
            AddLog($"[Kasir Servis] Nota #{job.JobId} dibuat untuk {job.CustomerName} ({job.DeviceBrand} {job.DeviceModel}).");
        }

        public void AutoFillJobFromDevice()
        {
            if (SelectedDevice != null)
            {
                NewDeviceBrand = string.IsNullOrEmpty(SelectedDevice.Brand) ? "Android" : SelectedDevice.Brand;
                NewDeviceModel = string.IsNullOrEmpty(SelectedDevice.Model) ? "Universal" : SelectedDevice.Model;
                AddLog($"[Kasir Servis] Info perangkat otomatis disalin: {NewDeviceBrand} {NewDeviceModel}");
            }
            else
            {
                AddLog("[Peringatan] Tidak ada perangkat yang dipilih untuk disalin.");
            }
        }

        public async Task PrintReceiptAsync()
        {
            if (SelectedJobOrder == null)
            {
                AddLog("[Peringatan] Pilih salah satu nota dari daftar untuk dicetak.");
                return;
            }

            var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "receipts");
            var htmlPath = await _jobEngine.SaveReceiptHtmlAsync(SelectedJobOrder, outputDir);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = htmlPath,
                    UseShellExecute = true
                });
                AddLog($"[Print] Membuka jendela cetak nota {SelectedJobOrder.JobId}...");
            }
            catch (Exception ex)
            {
                AddLog($"[Print Error] {ex.Message}");
            }
        }

        public void SendWhatsAppReceipt()
        {
            if (SelectedJobOrder == null)
            {
                AddLog("[Peringatan] Pilih salah satu nota dari daftar.");
                return;
            }

            var msg = _jobEngine.GenerateWhatsAppMessage(SelectedJobOrder);
            Clipboard.SetText(msg);
            AddLog($"[WhatsApp] Pesan nota untuk {SelectedJobOrder.CustomerName} ({SelectedJobOrder.CustomerPhone}) telah disalin ke Clipboard.");

            if (!string.IsNullOrEmpty(SelectedJobOrder.CustomerPhone))
            {
                var waUrl = _jobEngine.GenerateWhatsAppUrl(SelectedJobOrder);
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = waUrl,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        private void UpdateReceiptPreview()
        {
            if (SelectedJobOrder != null)
            {
                ReceiptPreviewText = _jobEngine.GenerateThermalReceiptText(SelectedJobOrder);
            }
            else
            {
                var draftJob = new JobOrderInfo
                {
                    JobId = "FK-" + DateTime.Now.ToString("yyyyMMdd") + "-001",
                    CustomerName = string.IsNullOrWhiteSpace(NewCustomerName) ? "Contoh: Budi Santoso" : NewCustomerName,
                    CustomerPhone = string.IsNullOrWhiteSpace(NewCustomerPhone) ? "0812-3456-7890" : NewCustomerPhone,
                    DeviceBrand = string.IsNullOrWhiteSpace(NewDeviceBrand) ? (SelectedDevice?.Brand ?? "Samsung") : NewDeviceBrand,
                    DeviceModel = string.IsNullOrWhiteSpace(NewDeviceModel) ? (SelectedDevice?.Model ?? "Galaxy A52 (SM-A525F)") : NewDeviceModel,
                    DeviceImei = SelectedDevice?.Imei ?? "869123049182341",
                    ServiceType = string.IsNullOrWhiteSpace(NewServiceType) ? "Bypass FRP / Flash Firmware" : NewServiceType,
                    ProblemDescription = "Pemeriksaan dan Perbaikan Software Hardware",
                    Cost = NewCost > 0 ? NewCost : 150000,
                    WarrantyDays = NewWarrantyDays > 0 ? NewWarrantyDays : 30,
                    Status = JobOrderStatus.Selesai,
                    TechnicianName = "Teknisi Pro (Master Tech)",
                    CreatedAt = DateTime.Now
                };
                ReceiptPreviewText = _jobEngine.GenerateThermalReceiptText(draftJob);
            }
        }
    }
}
