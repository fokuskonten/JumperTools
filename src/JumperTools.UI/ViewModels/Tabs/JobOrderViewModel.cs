using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using JumperTools.Core;
using JumperTools.Models;
using JumperTools.UI.ViewModels.Shared;

namespace JumperTools.UI.ViewModels.Tabs
{
    public class JobOrderViewModel : TabViewModelBase
    {
        private readonly JobOrderEngine _jobEngine;

        private string _newCustomerName = string.Empty;
        private string _newCustomerPhone = string.Empty;
        private string _newDeviceBrand = string.Empty;
        private string _newDeviceModel = string.Empty;
        private string _newServiceType = string.Empty;
        private decimal _newCost = 0;
        private int _newWarrantyDays = 0;
        private string _receiptPreviewText = string.Empty;
        private JobOrderInfo? _selectedJobOrder;

        public ObservableCollection<JobOrderInfo> JobOrders { get; }

        public ICommand CreateJobOrderCommand { get; }
        public ICommand AutoFillJobFromDeviceCommand { get; }
        public ICommand PrintReceiptCommand { get; }
        public ICommand SendWhatsAppReceiptCommand { get; }

        public JobOrderViewModel(ISharedWorkspaceContext context, JobOrderEngine jobEngine)
            : base(context)
        {
            _jobEngine = jobEngine;
            JobOrders = new ObservableCollection<JobOrderInfo>();

            try
            {
                var saved = _jobEngine.GetAllSavedJobs();
                foreach (var j in saved)
                {
                    JobOrders.Add(j);
                }
            }
            catch { }

            CreateJobOrderCommand = new RelayCommand(CreateJobOrder);
            AutoFillJobFromDeviceCommand = new RelayCommand(AutoFillJobFromDevice);
            PrintReceiptCommand = new AsyncRelayCommand(PrintReceiptAsync);
            SendWhatsAppReceiptCommand = new RelayCommand(SendWhatsAppReceipt);

            UpdateReceiptPreview();
        }

        public string NewCustomerName
        {
            get => _newCustomerName;
            set { _newCustomerName = value; OnPropertyChanged(); UpdateReceiptPreview(); }
        }

        public string NewCustomerPhone
        {
            get => _newCustomerPhone;
            set { _newCustomerPhone = value; OnPropertyChanged(); UpdateReceiptPreview(); }
        }

        public string NewDeviceBrand
        {
            get => _newDeviceBrand;
            set { _newDeviceBrand = value; OnPropertyChanged(); UpdateReceiptPreview(); }
        }

        public string NewDeviceModel
        {
            get => _newDeviceModel;
            set { _newDeviceModel = value; OnPropertyChanged(); UpdateReceiptPreview(); }
        }

        public string NewServiceType
        {
            get => _newServiceType;
            set { _newServiceType = value; OnPropertyChanged(); UpdateReceiptPreview(); }
        }

        public decimal NewCost
        {
            get => _newCost;
            set { _newCost = value; OnPropertyChanged(); UpdateReceiptPreview(); }
        }

        public int NewWarrantyDays
        {
            get => _newWarrantyDays;
            set { _newWarrantyDays = value; OnPropertyChanged(); UpdateReceiptPreview(); }
        }

        public string ReceiptPreviewText
        {
            get => _receiptPreviewText;
            set { _receiptPreviewText = value; OnPropertyChanged(); }
        }

        public JobOrderInfo? SelectedJobOrder
        {
            get => _selectedJobOrder;
            set
            {
                _selectedJobOrder = value;
                OnPropertyChanged();
                UpdateReceiptPreview();
            }
        }

        public bool HasNoJobOrders => JobOrders.Count == 0;

        public void CreateJobOrder()
        {
            var job = new JobOrderInfo
            {
                JobId = _jobEngine.GenerateJobId(),
                CustomerName = string.IsNullOrWhiteSpace(NewCustomerName) ? "Pelanggan Umum" : NewCustomerName,
                CustomerPhone = NewCustomerPhone,
                DeviceBrand = string.IsNullOrWhiteSpace(NewDeviceBrand) ? "Universal" : NewDeviceBrand,
                DeviceModel = string.IsNullOrWhiteSpace(NewDeviceModel) ? "Smartphone" : NewDeviceModel,
                DeviceImei = Context.SelectedDevice?.Imei ?? "",
                DeviceSerial = Context.SelectedDevice?.SerialNumber ?? "",
                ServiceType = string.IsNullOrWhiteSpace(NewServiceType) ? "Servis Software / Hardware" : NewServiceType,
                Cost = NewCost,
                WarrantyDays = NewWarrantyDays,
                Status = JobOrderStatus.Selesai,
                CreatedAt = DateTime.Now
            };

            JobOrders.Insert(0, job);
            SelectedJobOrder = job;
            try { _jobEngine.SaveJobToDb(job); } catch { }
            OnPropertyChanged(nameof(HasNoJobOrders));
            AddLog($"[Kasir Servis] Nota #{job.JobId} dibuat untuk {job.CustomerName} ({job.DeviceBrand} {job.DeviceModel}).");
        }

        public void AutoFillJobFromDevice()
        {
            if (Context.SelectedDevice != null)
            {
                NewDeviceBrand = string.IsNullOrEmpty(Context.SelectedDevice.Brand) ? "Android" : Context.SelectedDevice.Brand;
                NewDeviceModel = string.IsNullOrEmpty(Context.SelectedDevice.Model) ? "Universal" : Context.SelectedDevice.Model;
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
                var cleanPhone = SelectedJobOrder.CustomerPhone.Replace("-", "").Replace(" ", "").Replace("+", "");
                if (cleanPhone.StartsWith("0")) cleanPhone = "62" + cleanPhone.Substring(1);

                var waUrl = $"https://wa.me/{cleanPhone}?text={Uri.EscapeDataString(msg)}";
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

        public void UpdateReceiptPreview()
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
                    DeviceBrand = string.IsNullOrWhiteSpace(NewDeviceBrand) ? (Context.SelectedDevice?.Brand ?? "Samsung") : NewDeviceBrand,
                    DeviceModel = string.IsNullOrWhiteSpace(NewDeviceModel) ? (Context.SelectedDevice?.Model ?? "Galaxy A52 (SM-A525F)") : NewDeviceModel,
                    DeviceImei = Context.SelectedDevice?.Imei ?? "869123049182341",
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
