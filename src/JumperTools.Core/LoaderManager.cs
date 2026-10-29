using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JumperTools.Models;

namespace JumperTools.Core
{
    /// <summary>
    /// Manages Firehose Loaders (.mbn/.elf) and MTK Download Agents (.bin)
    /// Handles local caching and on-demand retrieval from server
    /// </summary>
    public class LoaderManager
    {
        private readonly string _baseStorageDir;
        private readonly List<PhoneModelProfile> _modelProfiles;
        private readonly List<LoaderInfo> _knownLoaders;

        public LoaderManager(string? baseDir = null)
        {
            _baseStorageDir = baseDir ?? Path.Combine(AppPathResolver.ToolsDirectory, "loaders");
            EnsureDirectoriesExist();
            _modelProfiles = InitializeModelProfiles();
            _knownLoaders = InitializeSeedLoaders();
            UpdateLocalCacheStatus();
        }

        private void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(Path.Combine(_baseStorageDir, "qualcomm"));
            Directory.CreateDirectory(Path.Combine(_baseStorageDir, "mtk"));
            Directory.CreateDirectory(Path.Combine(_baseStorageDir, "unisoc"));
        }

        public List<PhoneModelProfile> GetAllModels() => _modelProfiles.ToList();

        public List<string> GetAllBrands() => _modelProfiles.Select(m => m.Brand).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        public List<PhoneModelProfile> GetModelsByBrand(string brand)
        {
            if (string.IsNullOrWhiteSpace(brand) || brand.Equals("Semua", StringComparison.OrdinalIgnoreCase))
                return GetAllModels();

            return _modelProfiles.Where(m => m.Brand.Equals(brand, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public PhoneModelProfile? FindModel(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;
            var q = query.Trim();
            return _modelProfiles.FirstOrDefault(m => 
                m.ModelName.Equals(q, StringComparison.OrdinalIgnoreCase) ||
                m.CodeName.Equals(q, StringComparison.OrdinalIgnoreCase) ||
                m.ModelName.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        private static readonly List<PhoneModelProfile> _cachedMasterProfiles = InitializeModelProfiles();
        public static List<PhoneModelProfile> MasterProfiles => _cachedMasterProfiles;

        /// <summary>
        /// Universal hardware-level forensic matcher across all brands (OPPO, Vivo, Xiaomi, Samsung, Realme, Infinix, etc.)
        /// Cross-checks physical board platform and factory SKU against database to uncover counterfeit/refurbished devices.
        /// </summary>
        public static (PhoneModelProfile? MatchedProfile, bool IsSpoofed, string DisplayName) ResolveHardwareIdentity(
            string brand,
            string prodName,
            string prodDevice,
            string boardPlatform,
            string rawModel,
            string reportedMarketName)
        {
            var candidates = _cachedMasterProfiles.Where(p => 
                string.IsNullOrEmpty(brand) || brand.Equals("Semua", StringComparison.OrdinalIgnoreCase) || brand.Equals("Android", StringComparison.OrdinalIgnoreCase) || p.Brand.Equals(brand, StringComparison.OrdinalIgnoreCase)).ToList();

            // Match against CodeName (e.g. CPH2387, PD2060F, dandelion, SM-A022F, X6833B)
            PhoneModelProfile? matched = null;

            foreach (var profile in candidates)
            {
                var codes = profile.CodeName.Split(new[] { '/', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var c in codes)
                {
                    if (c.Length < 3) continue;
                    if ((!string.IsNullOrEmpty(prodName) && prodName.Contains(c, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(prodDevice) && prodDevice.Contains(c, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(rawModel) && rawModel.Contains(c, StringComparison.OrdinalIgnoreCase)))
                    {
                        matched = profile;
                        break;
                    }
                }
                if (matched != null) break;
            }

            if (matched != null)
            {
                var baseCode = matched.CodeName.Split(new[] { '/', ',' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? rawModel;
                
                bool isSpoof = !string.IsNullOrWhiteSpace(reportedMarketName) && 
                               !matched.ModelName.Contains(reportedMarketName, StringComparison.OrdinalIgnoreCase) && 
                               !reportedMarketName.Contains(matched.ModelName.Split('/')[0].Trim(), StringComparison.OrdinalIgnoreCase);

                if (isSpoof)
                {
                    return (matched, true, $"{matched.ModelName} [{baseCode} | Refurbish: {reportedMarketName}]");
                }

                return (matched, false, $"{matched.ModelName} ({baseCode})");
            }

            // Fallback for brand new unlisted genuine models
            if (!string.IsNullOrWhiteSpace(reportedMarketName) && !reportedMarketName.Equals(rawModel, StringComparison.OrdinalIgnoreCase))
            {
                return (null, false, $"{reportedMarketName} ({rawModel})");
            }

            return (null, false, rawModel);
        }

        /// <summary>
        /// Universal data-driven operational guidance for all brands in MTP / Locked mode.
        /// Covers Xiaomi, Oppo, Realme, Vivo, Samsung, Infinix, Tecno, Itel, Huawei, etc.
        /// </summary>
        public static string[] GetBrandOperationGuidance(string brand, DeviceMode mode)
        {
            var b = brand?.Trim() ?? "";

            if (b.Equals("Xiaomi", StringComparison.OrdinalIgnoreCase) || b.Equals("Redmi", StringComparison.OrdinalIgnoreCase) || b.Equals("Poco", StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    " • Sideload / Recovery : Matikan HP -> Tahan Vol Atas + Power -> Pilih 'Connect with MIAssistant' -> Klik [Wipe Userdata]",
                    " • Fastboot Mode       : Matikan HP -> Tahan Vol Bawah + Power -> Klik [Unlock Bootloader / Flashing]",
                    " • EDL Test Point      : Buka backdoor -> Hubungkan 2 titik Test Point -> Colok USB untuk EDL 9008"
                };
            }
            if (b.Equals("Oppo", StringComparison.OrdinalIgnoreCase) || b.Equals("Realme", StringComparison.OrdinalIgnoreCase) || b.Equals("OnePlus", StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    " • Cek Tipe Asli       : Di layar kunci klik Panggilan Darurat -> ketik *#899# -> SoftwareVersion",
                    " • BROM Format Data    : Matikan HP -> Tahan Vol Atas + Bawah -> Colok USB -> Klik [Format Userdata]",
                    " • Fastboot Mode       : Matikan HP -> Tahan Vol Bawah + Power untuk Fastboot Flashing"
                };
            }
            if (b.Equals("Vivo", StringComparison.OrdinalIgnoreCase) || b.Equals("iQOO", StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    " • Recovery Mode Log   : Matikan HP -> Tahan Vol Atas + Power -> Recovery -> Advanced -> View Log (Cek PD Code)",
                    " • BROM / EDL Service  : Matikan HP -> Tahan Vol Atas + Bawah (MTK) atau Test Point (Qualcomm) -> Klik [Format Userdata]",
                    " • Fastboot Mode       : Matikan HP -> Tahan Vol Atas + Power -> Fastboot Mode"
                };
            }
            if (b.Equals("Samsung", StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    " • Samsung Test Mode   : Di layar kunci klik Panggilan Darurat -> ketik *#0*# -> Klik [Bypass FRP]",
                    " • Download Mode       : Matikan HP -> Tahan Vol Atas + Bawah -> Colok USB -> Klik [Flash Odin]",
                    " • Recovery Wipe       : Matikan HP -> Hubungkan kabel USB PC -> Tahan Vol Atas + Power -> Wipe Data"
                };
            }
            if (b.Equals("Infinix", StringComparison.OrdinalIgnoreCase) || b.Equals("Tecno", StringComparison.OrdinalIgnoreCase) || b.Equals("Itel", StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    " • MediaTek BROM       : Matikan HP -> Tahan Vol Atas + Bawah -> Colok USB -> Klik [Format Userdata]",
                    " • Unisoc SPD / BSL    : Matikan HP -> Tahan Vol Bawah -> Colok USB -> Klik [Erase FRP / Factory Reset]",
                    " • Fastboot Mode       : Matikan HP -> Tahan Vol Bawah + Power"
                };
            }
            if (b.Equals("Huawei", StringComparison.OrdinalIgnoreCase) || b.Equals("Honor", StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    " • Fastboot Mode       : Matikan HP -> Tahan Vol Bawah -> Colok USB",
                    " • USB COM 1.0 (Kirin) : Hubungkan Test Point ke Ground -> Colok USB untuk Repair Kirin Chipset",
                    " • eRecovery Update    : Tahan Vol Atas -> Colok USB untuk download firmware resmi via Wi-Fi"
                };
            }

            return new[]
            {
                " • Mode BootROM / BROM : Matikan HP -> Tahan Vol Atas + Bawah -> Colok USB -> Klik [Format Userdata]",
                " • Mode Fastboot       : Matikan HP -> Tahan Vol Bawah + Power -> Colok USB",
                " • Mode Recovery       : Matikan HP -> Tahan Vol Atas + Power untuk masuk recovery menu"
            };
        }

        public LoaderInfo? GetLoaderForModel(PhoneModelProfile model)
        {
            var loader = _knownLoaders.FirstOrDefault(l => 
                l.FileName.Equals(model.RecommendedLoaderFile, StringComparison.OrdinalIgnoreCase) ||
                (l.Chipset.Equals(model.Chipset, StringComparison.OrdinalIgnoreCase) && l.Brand.Equals(model.Brand, StringComparison.OrdinalIgnoreCase)));

            if (loader != null)
            {
                var localFilePath = FindLocalLoaderFile(loader.FileName, loader.Type);
                if (localFilePath != null)
                {
                    loader.LocalPath = localFilePath;
                    loader.IsCachedLocally = true;
                }
                else
                {
                    var subFolder = loader.Type == LoaderType.Qualcomm_Firehose ? "qualcomm" : (loader.Type == LoaderType.MediaTek_DA ? "mtk" : "unisoc");
                    loader.LocalPath = Path.Combine(_baseStorageDir, subFolder, loader.FileName);
                    loader.IsCachedLocally = false;
                }
            }

            return loader;
        }

        public static bool IsValidLoaderBinary(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;
                var info = new FileInfo(filePath);
                if (info.Length < 1024) return false; // Genuine loaders are at least a few KB

                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var header = new byte[Math.Min(128, (int)info.Length)];
                var bytesRead = stream.Read(header, 0, header.Length);
                if (bytesRead < 4) return false;

                // Check for ELF Magic: 0x7F 'E' 'L' 'F' (0x7F 0x45 0x4C 0x46)
                if (header[0] == 0x7F && header[1] == 0x45 && header[2] == 0x4C && header[3] == 0x46) return true;

                // Check for Qualcomm MBN / Flash programmer signatures or raw binary
                // Reject dummy ASCII text headers
                var asciiHeader = System.Text.Encoding.ASCII.GetString(header);
                if (asciiHeader.Contains("DOWNLOAD_AGENT") || 
                    asciiHeader.Contains("QUALCOMM_FIREHOSE") || 
                    asciiHeader.Contains("SPRD_FDL") || 
                    asciiHeader.Contains("FOKUSKONTEN_"))
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public string? FindLocalLoaderFile(string fileName, LoaderType type)
        {
            var subFolder = type == LoaderType.Qualcomm_Firehose ? "qualcomm" : (type == LoaderType.MediaTek_DA ? "mtk" : "unisoc");
            var primary = Path.Combine(_baseStorageDir, subFolder, fileName);
            if (IsValidLoaderBinary(primary)) return primary;

            var searchRoots = new List<string>
            {
                AppPathResolver.FirehoseDirectory,
                AppPathResolver.DaDirectory,
                AppPathResolver.FdlDirectory,
                AppPathResolver.KirinDirectory,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", "firehose"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", "da"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", "fdl"),
                // Release build (build/Release/) -> 2 levels up
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "database", "firehose")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "database", "da")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "database", "fdl")),
                // Bin debug/release (src/.../bin/Release/net8.0-windows/) -> 4-5 levels up
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "database", "firehose")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "database", "da")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "database", "fdl")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "database", "firehose")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "database", "da")),
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "database", "fdl"))
            };

            foreach (var root in searchRoots)
            {
                try
                {
                    if (Directory.Exists(root))
                    {
                        var direct = Path.Combine(root, fileName);
                        if (IsValidLoaderBinary(direct)) return direct;

                        // Search subdirectories
                        var matches = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
                        if (matches.Length > 0 && IsValidLoaderBinary(matches[0]))
                        {
                            return matches[0];
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        public void UpdateLocalCacheStatus()
        {
            foreach (var loader in _knownLoaders)
            {
                var foundPath = FindLocalLoaderFile(loader.FileName, loader.Type);
                if (foundPath != null)
                {
                    loader.LocalPath = foundPath;
                    loader.IsCachedLocally = true;
                }
                else
                {
                    var subFolder = loader.Type == LoaderType.Qualcomm_Firehose ? "qualcomm" : (loader.Type == LoaderType.MediaTek_DA ? "mtk" : "unisoc");
                    loader.LocalPath = Path.Combine(_baseStorageDir, subFolder, loader.FileName);
                    loader.IsCachedLocally = false;
                }
            }
        }

        public async Task<string?> EnsureLoaderAvailableAsync(LoaderInfo loader, object? apiClient = null, IProgress<double>? progress = null)
        {
            UpdateLocalCacheStatus();
            if (loader.IsCachedLocally && File.Exists(loader.LocalPath))
            {
                progress?.Report(100.0);
                return loader.LocalPath;
            }

            var dir = Path.GetDirectoryName(loader.LocalPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            // Attempt remote download if an API client is provided
            if (apiClient != null)
            {
                try
                {
                    var clientType = apiClient.GetType();
                    var downloadMethod = clientType.GetMethod("DownloadLoaderFileAsync");
                    if (downloadMethod != null)
                    {
                        var taskObj = downloadMethod.Invoke(apiClient, new object?[] { loader.FileName, loader.LocalPath, progress });
                        if (taskObj is Task<bool> task)
                        {
                            var downloaded = await task;
                            if (downloaded && File.Exists(loader.LocalPath))
                            {
                                loader.IsCachedLocally = true;
                                return loader.LocalPath;
                            }
                        }
                    }
                }
                catch
                {
                    // Fallback to local seed generation on offline/network failure
                }
            }

            if (!File.Exists(loader.LocalPath))
            {
                // Loader file is not available locally and could not be downloaded
                loader.IsCachedLocally = false;
                return null;
            }

            loader.IsCachedLocally = true;
            return loader.LocalPath;
        }

        private static List<PhoneModelProfile> InitializeModelProfiles()
        {
            return new List<PhoneModelProfile>
            {
                // ==================== INFINIX (TRANSSION) ====================
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Hot 8 / Hot 8 Lite", CodeName = "X650", Chipset = "Helio P22 (MT6762)", Architecture = "MediaTek", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "BROM Erase FRP, Format Userdata, NVRAM Backup" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Hot 9 / Hot 9 Play", CodeName = "X680 / X680B", Chipset = "Helio A25 / P22 (MT6762D)", Architecture = "MediaTek", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "BROM Erase FRP, Format Data, Fix Hang Logo" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Hot 10 / Hot 10 Play", CodeName = "X682 / X688", Chipset = "Helio G70 / G25 (MT6769)", Architecture = "MediaTek", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "BROM Erase FRP, Format Userdata, Backup NVRAM" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Hot 10S / Hot 10T", CodeName = "X689", Chipset = "Helio G85 (MT6769)", Architecture = "MediaTek", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "BROM Erase FRP, Safe Wipe, Bypass Auth" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Hot 11 / Hot 11S", CodeName = "X662 / X6812", Chipset = "Helio G88 / G96 (MT6769H)", Architecture = "MediaTek", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "BROM FRP, Sideload Wipe, Repair IMEI" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Hot 11 (2022) / Hot 12 Play", CodeName = "X675 / X6816", Chipset = "Unisoc T610 / Helio G37", Architecture = "Unisoc / MediaTek", RecommendedLoaderFile = "X675_INIFNIX_HOT_11(2022).bin", SupportedOperations = "Unisoc BSL FRP, BROM Format Userdata" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Hot 12 / Hot 12i", CodeName = "X6817 / X668", Chipset = "Helio G85 / Helio A22", Architecture = "MediaTek", RecommendedLoaderFile = "X668.bin", SupportedOperations = "BROM Format Userdata, FRP Reset" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Hot 20 / Hot 20i", CodeName = "X6826 / X6515", Chipset = "Helio G85 / Helio G25", Architecture = "MediaTek", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "BROM Erase FRP, Format Userdata" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Hot 30 / Hot 30i", CodeName = "X6831 / X669", Chipset = "Helio G88 / Unisoc T606", Architecture = "MediaTek / Unisoc", RecommendedLoaderFile = "T606_64_X669.bin", SupportedOperations = "Unisoc BSL FRP, BROM Format Data" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Hot 40 / Hot 40 Pro", CodeName = "X6836 / X6837", Chipset = "Helio G88 / Helio G99", Architecture = "MediaTek", RecommendedLoaderFile = "MT6789_0.bin", SupportedOperations = "Helio G99 BROM FRP, Format Userdata" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Smart 4 / Smart 5 / Smart 6", CodeName = "X653 / X657 / X6511", Chipset = "Helio A22 / Helio A20", Architecture = "MediaTek", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "BROM FRP Reset, Factory Reset" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Smart 7 / Smart 8", CodeName = "X6515 / X6525", Chipset = "Unisoc SC9863A / Unisoc T606", Architecture = "Unisoc", RecommendedLoaderFile = "T606_64.bin", SupportedOperations = "Unisoc BSL Erase FRP, Safe Wipe" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Note 7 / Note 8", CodeName = "X690 / X692", Chipset = "Helio G70 / Helio G80", Architecture = "MediaTek", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "BROM FRP, Format Data, Restore NVRAM" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Note 10 / Note 10 Pro", CodeName = "X693 / X695", Chipset = "Helio G85 / Helio G95", Architecture = "MediaTek", RecommendedLoaderFile = "DA_SWSEC.bin", SupportedOperations = "BROM Erase FRP, Sideload Wipe" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Note 11 / Note 11 Pro", CodeName = "X663 / X697", Chipset = "Helio G88 / Helio G96", Architecture = "MediaTek", RecommendedLoaderFile = "DA_SWSEC.bin", SupportedOperations = "BROM FRP, Safe Format, Sideload OTA" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Note 12 / Note 12 G96", CodeName = "X670 / X663", Chipset = "Helio G88 / Helio G96", Architecture = "MediaTek", RecommendedLoaderFile = "DA_SWSEC.bin", SupportedOperations = "BROM FRP Reset, NVRAM Fix" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Note 30 / Note 30 Pro", CodeName = "X6833 / X678B", Chipset = "Helio G99 (MT6789)", Architecture = "MediaTek", RecommendedLoaderFile = "MT6789_0.bin", SupportedOperations = "Helio G99 BROM FRP, Userdata Wipe" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Note 40 / Note 40 Pro", CodeName = "X6853 / X6850", Chipset = "Helio G99 Ultimate", Architecture = "MediaTek", RecommendedLoaderFile = "MT6789_2.bin", SupportedOperations = "BROM FRP Reset, Safe Format" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "GT 10 Pro", CodeName = "X6739", Chipset = "Dimensity 8050 (MT6893)", Architecture = "MediaTek", RecommendedLoaderFile = "MT6893_0.bin", SupportedOperations = "Dimensity BROM FRP, Fastboot Flashing" },

                // ==================== TECNO & ITEL ====================
                new PhoneModelProfile { Brand = "Tecno", ModelName = "Spark 6 / Spark 7 / Spark 8", CodeName = "KE7 / KF6 / KG6", Chipset = "Helio G70 / Helio A25", Architecture = "MediaTek", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "BROM FRP Erase, Format Userdata" },
                new PhoneModelProfile { Brand = "Tecno", ModelName = "Spark 9 / Spark 9 Pro", CodeName = "KG5 / KH7", Chipset = "Helio G37 / Helio G85", Architecture = "MediaTek", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "BROM FRP, Safe Factory Reset" },
                new PhoneModelProfile { Brand = "Tecno", ModelName = "Spark 10 / Spark 10 Pro", CodeName = "KI5 / KI7", Chipset = "Helio G37 / Helio G88", Architecture = "MediaTek", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "BROM FRP Erase, Safe Wipe" },
                new PhoneModelProfile { Brand = "Tecno", ModelName = "Spark 20 / Spark 20 Pro", CodeName = "KJ5 / KJ6", Chipset = "Helio G85 / Helio G99", Architecture = "MediaTek", RecommendedLoaderFile = "MT6789_0.bin", SupportedOperations = "Helio G99 BROM FRP, Format Userdata" },
                new PhoneModelProfile { Brand = "Tecno", ModelName = "Pova / Pova 2 / Pova 3", CodeName = "LD7 / LE7 / LF7", Chipset = "Helio G80 / Helio G85 / Helio G88", Architecture = "MediaTek", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "BROM FRP, NVRAM Backup, Fix Bootloop" },
                new PhoneModelProfile { Brand = "Tecno", ModelName = "Pova 4 / Pova 5", CodeName = "LG7 / LH7", Chipset = "Helio G99 (MT6789)", Architecture = "MediaTek", RecommendedLoaderFile = "MT6789_0.bin", SupportedOperations = "Helio G99 BROM FRP, Wipe Userdata" },
                new PhoneModelProfile { Brand = "Itel", ModelName = "Vision 1 / Vision 2 / Vision 3", CodeName = "L6005 / L6501", Chipset = "Unisoc SC9863A / SC9863A1", Architecture = "Unisoc", RecommendedLoaderFile = "9863A_64.bin", SupportedOperations = "Unisoc BSL FRP, Format Userdata" },
                new PhoneModelProfile { Brand = "Itel", ModelName = "Itel A60 / A60s / P40", CodeName = "A662L / P662L", Chipset = "Unisoc SC9832E / SC9863A", Architecture = "Unisoc", RecommendedLoaderFile = "9863A.bin", SupportedOperations = "Unisoc BSL FRP Erase, Safe Wipe" },
                new PhoneModelProfile { Brand = "Itel", ModelName = "Itel S23 4G / S23 5G", CodeName = "S665L / S661L", Chipset = "Unisoc T606 / Dimensity 6020", Architecture = "Unisoc / MediaTek", RecommendedLoaderFile = "ITEL_S23_5G_S665L.bin", SupportedOperations = "Unisoc BSL FRP, BROM Dimensity FRP" },

                // ==================== REALME ====================
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme C1 / C2", CodeName = "RMX1811 / RMX1941", Chipset = "Snapdragon 450 / Helio P22", Architecture = "Qualcomm / MediaTek", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "BROM FRP, Format Userdata, EDL FRP" },
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme C3 / C3i", CodeName = "RMX2020 / RMX2027", Chipset = "Helio G70 (MT6768)", Architecture = "MediaTek", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "BROM FRP Erase, Safe Wipe Data" },
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme C11 / C12 / C15", CodeName = "RMX2185 / RMX2189", Chipset = "Helio G35 (MT6765G)", Architecture = "MediaTek", RecommendedLoaderFile = "DA_PL_CRYPTO20.bin", SupportedOperations = "BROM FRP, Wipe Data, Flash OFP" },
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme C20 / C21", CodeName = "RMX3063 / RMX3201", Chipset = "Helio G35 (MT6765)", Architecture = "MediaTek", RecommendedLoaderFile = "DA_PL_CRYPTO20.bin", SupportedOperations = "BROM FRP Erase, Format Userdata" },
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme C21Y / C25Y", CodeName = "RMX3261 / RMX3269", Chipset = "Unisoc Tiger T610", Architecture = "Unisoc", RecommendedLoaderFile = "RMX3261.bin", SupportedOperations = "Unisoc BSL FRP Erase, Factory Reset" },
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme C25 / C25s", CodeName = "RMX3193 / RMX3195", Chipset = "Helio G70 / Helio G85", Architecture = "MediaTek", RecommendedLoaderFile = "DA_SWSEC.bin", SupportedOperations = "BROM FRP Reset, Safe Userdata Wipe" },
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme C30 / C30s", CodeName = "RMX3581 / RMX3690", Chipset = "Unisoc T612 / SC9863A", Architecture = "Unisoc", RecommendedLoaderFile = "T612_64.bin", SupportedOperations = "Unisoc BSL FRP, Factory Reset" },
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme C31 / C35", CodeName = "RMX3501 / RMX3511", Chipset = "Unisoc T612 / T616", Architecture = "Unisoc", RecommendedLoaderFile = "RMX3511.bin", SupportedOperations = "Unisoc BSL FRP Erase, Fix Bootloop" },
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme C33", CodeName = "RMX3627", Chipset = "Unisoc T612", Architecture = "Unisoc", RecommendedLoaderFile = "RMX3627.bin", SupportedOperations = "Unisoc BSL FRP Erase, Safe Wipe" },
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme C51 / C53", CodeName = "RMX3830 / RMX3760", Chipset = "Unisoc T612", Architecture = "Unisoc", RecommendedLoaderFile = "T612_64.bin", SupportedOperations = "Unisoc BSL FRP Reset, Safe Format" },
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme C55 / Narzo N55", CodeName = "RMX3710", Chipset = "Helio G88 (MT6769)", Architecture = "MediaTek", RecommendedLoaderFile = "DA_SWSEC.bin", SupportedOperations = "BROM FRP, Sideload Wipe, Anti-Relock" },
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme 5 / 5i / 5s", CodeName = "RMX1911 / RMX2030", Chipset = "Snapdragon 665 (SM6125)", Architecture = "Qualcomm", RecommendedLoaderFile = "prog_emmc_firehose_sm6125_ddr.mbn", SupportedOperations = "EDL FRP, EDL Format Userdata" },
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme 6 / 6 Pro", CodeName = "RMX2001 / RMX2061", Chipset = "Helio G90T / Snapdragon 720G", Architecture = "MediaTek / Qualcomm", RecommendedLoaderFile = "DA_SWSEC.bin", SupportedOperations = "BROM FRP, EDL FRP, Format Userdata" },
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme 7 / 7i", CodeName = "RMX2151 / RMX2103", Chipset = "Helio G95 / Snapdragon 662", Architecture = "MediaTek / Qualcomm", RecommendedLoaderFile = "DA_SWSEC.bin", SupportedOperations = "BROM FRP, EDL FRP, Fix Demo Mode" },
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme 8 / 8 5G", CodeName = "RMX3085 / RMX3241", Chipset = "Helio G95 / Dimensity 700", Architecture = "MediaTek", RecommendedLoaderFile = "MT6833_1.bin", SupportedOperations = "BROM Dimensity FRP, Safe Format" },
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme 9 / 9 Pro+", CodeName = "RMX3521 / RMX3392", Chipset = "Snapdragon 680 / Dimensity 920", Architecture = "Qualcomm / MediaTek", RecommendedLoaderFile = "MT6877_2.bin", SupportedOperations = "EDL FRP, BROM Dimensity FRP" },
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme 10 / 11 4G", CodeName = "RMX3630 / RMX3780", Chipset = "Helio G99 (MT6789)", Architecture = "MediaTek", RecommendedLoaderFile = "MT6789_0.bin", SupportedOperations = "Helio G99 BROM FRP, Userdata Wipe" },

                // ==================== OPPO ====================
                new PhoneModelProfile { Brand = "Oppo", ModelName = "Oppo A1k", CodeName = "CPH1923", Chipset = "Helio P22 (MT6762)", Architecture = "MediaTek", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "BROM FRP, Format Userdata, Remove Pattern" },
                new PhoneModelProfile { Brand = "Oppo", ModelName = "Oppo A3s / A5 / A7", CodeName = "CPH1803 / CPH1853", Chipset = "Snapdragon 450 (SDM450)", Architecture = "Qualcomm", RecommendedLoaderFile = "prog_emmc_firehose_sdm450_ddr.mbn", SupportedOperations = "ISP Direct eMMC, EDL FRP" },
                new PhoneModelProfile { Brand = "Oppo", ModelName = "Oppo A5s / A12 / A15 / A15s", CodeName = "CPH1909 / CPH2083 / CPH2185", Chipset = "Helio P35 (MT6765)", Architecture = "MediaTek", RecommendedLoaderFile = "DA_PL_CRYPTO20.bin", SupportedOperations = "BROM Remove Demo, Format Userdata, FRP Erase" },
                new PhoneModelProfile { Brand = "Oppo", ModelName = "Oppo A16 / A16e / A16k", CodeName = "CPH2269 / CPH2349", Chipset = "Helio G35 / Helio P22", Architecture = "MediaTek", RecommendedLoaderFile = "DA_PL_CRYPTO20.bin", SupportedOperations = "BROM FRP Reset, Safe Wipe, Demo Fix" },
                new PhoneModelProfile { Brand = "Oppo", ModelName = "Oppo A17 / A17k", CodeName = "CPH2477 / CPH2471", Chipset = "Helio G35 (MT6765)", Architecture = "MediaTek", RecommendedLoaderFile = "DA_PL_CRYPTO20.bin", SupportedOperations = "BROM FRP, Format Userdata, NVRAM Backup" },
                new PhoneModelProfile { Brand = "Oppo", ModelName = "Oppo A18 / A38", CodeName = "CPH2591 / CPH2579", Chipset = "Helio G85 (MT6769)", Architecture = "MediaTek", RecommendedLoaderFile = "MT6768_SLA.bin", SupportedOperations = "BROM SLA FRP Erase, Safe Factory Reset" },
                new PhoneModelProfile { Brand = "Oppo", ModelName = "Oppo A53 / A33 (2020)", CodeName = "CPH2127 / CPH2137", Chipset = "Snapdragon 460 (SM4250)", Architecture = "Qualcomm", RecommendedLoaderFile = "prog_emmc_firehose_sm4250_ddr.mbn", SupportedOperations = "EDL FRP, EDL Format Userdata" },
                new PhoneModelProfile { Brand = "Oppo", ModelName = "Oppo A54", CodeName = "CPH2239", Chipset = "Helio P35 (MT6765)", Architecture = "MediaTek", RecommendedLoaderFile = "DA_PL_CRYPTO20.bin", SupportedOperations = "BROM FRP, Wipe Lockscreen, Demo Mode Fix" },
                new PhoneModelProfile { Brand = "Oppo", ModelName = "Oppo A57 (2022)", CodeName = "CPH2387", Chipset = "Helio G35 (MT6765)", Architecture = "MediaTek", RecommendedLoaderFile = "DA_PL_CRYPTO20.bin", SupportedOperations = "BROM FRP, Format Userdata, Remove Pattern" },
                new PhoneModelProfile { Brand = "Oppo", ModelName = "Oppo A77s", CodeName = "CPH2473", Chipset = "Snapdragon 680 (SM6225)", Architecture = "Qualcomm", RecommendedLoaderFile = "prog_emmc_firehose_sm4250_ddr.mbn", SupportedOperations = "EDL FRP, EDL Format Userdata" },
                new PhoneModelProfile { Brand = "Oppo", ModelName = "Oppo A58 4G", CodeName = "CPH2577", Chipset = "Helio G85 (MT6769)", Architecture = "MediaTek", RecommendedLoaderFile = "MT6768_SLA.bin", SupportedOperations = "BROM SLA FRP, Safe Wipe Data" },
                new PhoneModelProfile { Brand = "Oppo", ModelName = "Oppo A78 4G", CodeName = "CPH2565", Chipset = "Snapdragon 680 (SM6225)", Architecture = "Qualcomm", RecommendedLoaderFile = "prog_emmc_firehose_sm4250_ddr.mbn", SupportedOperations = "EDL FRP, EDL Safe Wipe" },
                new PhoneModelProfile { Brand = "Oppo", ModelName = "Oppo A78 5G", CodeName = "CPH2483 / CPH2495", Chipset = "Dimensity 700 (MT6833)", Architecture = "MediaTek", RecommendedLoaderFile = "MT6833_1.bin", SupportedOperations = "BROM Dimensity FRP, Safe Format" },
                new PhoneModelProfile { Brand = "Oppo", ModelName = "Oppo A96 / A74 4G", CodeName = "CPH2333 / CPH2219", Chipset = "Snapdragon 680 / Snapdragon 662", Architecture = "Qualcomm", RecommendedLoaderFile = "prog_emmc_firehose_sm4250_ddr.mbn", SupportedOperations = "EDL FRP, Format Userdata" },
                new PhoneModelProfile { Brand = "Oppo", ModelName = "Oppo Reno 4 / 4F / 5 / 5F", CodeName = "CPH2119 / CPH2209 / CPH2159", Chipset = "Snapdragon 720G / Helio P95", Architecture = "Qualcomm / MediaTek", RecommendedLoaderFile = "DA_SWSEC.bin", SupportedOperations = "EDL FRP, BROM FRP, Demo Unlock" },
                new PhoneModelProfile { Brand = "Oppo", ModelName = "Oppo Reno 6 / 7 4G / 8 4G", CodeName = "CPH2235 / CPH2371 / CPH2457", Chipset = "Snapdragon 720G / Snapdragon 680", Architecture = "Qualcomm", RecommendedLoaderFile = "prog_emmc_firehose_sm4250_ddr.mbn", SupportedOperations = "EDL FRP Reset, Safe Wipe Data" },

                // ==================== VIVO ====================
                new PhoneModelProfile { Brand = "Vivo", ModelName = "Vivo Y91 / Y93 / Y95", CodeName = "PD1818F / PD1818C", Chipset = "Snapdragon 439 / Helio P22", Architecture = "Qualcomm / MediaTek", RecommendedLoaderFile = "prog_emmc_firehose_sdm439_ddr.mbn", SupportedOperations = "EDL FRP, BROM FRP, Remove Lock" },
                new PhoneModelProfile { Brand = "Vivo", ModelName = "Vivo Y11 (2019) / Y12 / Y15 / Y17", CodeName = "PD1930F / PD1901F", Chipset = "Snapdragon 439 / Helio P35", Architecture = "Qualcomm / MediaTek", RecommendedLoaderFile = "vivo_mtk_loader_auth_4.bin", SupportedOperations = "BROM Remove Demo, FRP Erase, Factory Reset" },
                new PhoneModelProfile { Brand = "Vivo", ModelName = "Vivo Y12s / Y15s / Y01 / Y02", CodeName = "PD2060F / PD2140F", Chipset = "Helio P35 (MT6765)", Architecture = "MediaTek", RecommendedLoaderFile = "vivo_mtk_loader_auth_4.bin", SupportedOperations = "BROM Erase FRP, Wipe Userdata, Demo Fix" },
                new PhoneModelProfile { Brand = "Vivo", ModelName = "Vivo Y16 / Y21 / Y21s", CodeName = "PD2216F / PD2139F", Chipset = "Helio P35 / Helio G80", Architecture = "MediaTek", RecommendedLoaderFile = "vivo_mtk_loader_auth_4.bin", SupportedOperations = "BROM FRP, Sideload Format, Demo Mode" },
                new PhoneModelProfile { Brand = "Vivo", ModelName = "Vivo Y20 / Y20s / Y20i", CodeName = "PD2034F / PD2044F", Chipset = "Snapdragon 460 (SM4250)", Architecture = "Qualcomm", RecommendedLoaderFile = "prog_emmc_firehose_sm4250_ddr.mbn", SupportedOperations = "EDL 9008 FRP, EDL Wipe Userdata" },
                new PhoneModelProfile { Brand = "Vivo", ModelName = "Vivo Y22 / Y27 4G", CodeName = "PD2230F / PD2280F", Chipset = "Helio G85 (MT6769)", Architecture = "MediaTek", RecommendedLoaderFile = "vivo_mtk_loader_auth_4.bin", SupportedOperations = "BROM FRP Erase, Safe Factory Reset" },
                new PhoneModelProfile { Brand = "Vivo", ModelName = "Vivo Y30 / Y30i / Y50", CodeName = "PD1987F / PD1965F", Chipset = "Helio P35 / Snapdragon 665", Architecture = "MediaTek / Qualcomm", RecommendedLoaderFile = "prog_emmc_firehose_sm6125_ddr.mbn", SupportedOperations = "BROM FRP, EDL FRP, Format Data" },
                new PhoneModelProfile { Brand = "Vivo", ModelName = "Vivo Y33s / Y35 / Y36", CodeName = "PD2147F / PD2250F", Chipset = "Helio G80 / Snapdragon 680", Architecture = "MediaTek / Qualcomm", RecommendedLoaderFile = "vivo_mtk_loader_auth_4.bin", SupportedOperations = "BROM FRP, EDL FRP, Demo Unlock" },
                new PhoneModelProfile { Brand = "Vivo", ModelName = "Vivo V20 / V20 SE", CodeName = "PD2043F / PD2038F", Chipset = "Snapdragon 720G / Snapdragon 665", Architecture = "Qualcomm", RecommendedLoaderFile = "prog_emmc_firehose_sm6125_ddr.mbn", SupportedOperations = "EDL FRP Reset, Safe Wipe Userdata" },

                // ==================== XIAOMI / POCO ====================
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Redmi 6 / 6A", CodeName = "cereus / cactus", Chipset = "Helio P22 / Helio A22", Architecture = "MediaTek", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "BROM FRP, Format Userdata, Fix Bootloop" },
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Redmi 7 / 7A", CodeName = "onclite / pine", Chipset = "Snapdragon 632 / Snapdragon 439", Architecture = "Qualcomm", RecommendedLoaderFile = "prog_emmc_firehose_sdm439_ddr.mbn", SupportedOperations = "EDL FRP, MiCloud Bypass, Fastboot Flash" },
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Redmi 8 / 8A / 8A Pro", CodeName = "olive / olivelite", Chipset = "Snapdragon 439 (SDM439)", Architecture = "Qualcomm", RecommendedLoaderFile = "prog_emmc_firehose_sdm439_ddr.mbn", SupportedOperations = "EDL FRP, Sideload Wipe, MiCloud Bypass" },
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Redmi 9", CodeName = "lancelot", Chipset = "Helio G80 (MT6768)", Architecture = "MediaTek", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "BROM Erase FRP, Instant UBL, NVRAM Backup" },
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Redmi 9A / 9C", CodeName = "dandelion / angelica", Chipset = "Helio G25 / G35 (MT6765)", Architecture = "MediaTek", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "BROM FRP, Wipe Userdata, Fix Bootloop" },
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Redmi 10 / 10A / 10C", CodeName = "selene / dandelion / fog", Chipset = "Helio G88 / Helio G25 / Snapdragon 680", Architecture = "MediaTek / Qualcomm", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "BROM FRP, EDL FRP, Sideload Format" },
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Redmi 12 4G / 13C", CodeName = "fire / gale", Chipset = "Helio G88 / Helio G85", Architecture = "MediaTek", RecommendedLoaderFile = "DA_SWSEC.bin", SupportedOperations = "BROM FRP Erase, Safe Wipe Data" },
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Redmi Note 7 / 7 Pro", CodeName = "lavender / violet", Chipset = "Snapdragon 660 (SDM660)", Architecture = "Qualcomm", RecommendedLoaderFile = "prog_emmc_firehose_sdm660_ddr.mbn", SupportedOperations = "EDL FRP, Sideload Format, Anti-Relock" },
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Redmi Note 8", CodeName = "ginkgo", Chipset = "Snapdragon 665 (SM6125)", Architecture = "Qualcomm", RecommendedLoaderFile = "prog_emmc_firehose_sm6125_ddr.mbn", SupportedOperations = "EDL FRP, EDL Wipe, MiCloud Bypass" },
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Redmi Note 8 Pro", CodeName = "begonia", Chipset = "Helio G90T (MT6785)", Architecture = "MediaTek", RecommendedLoaderFile = "MT6785_2.bin", SupportedOperations = "BROM FRP, Fix Brick, Bypass Auth" },
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Redmi Note 9", CodeName = "merlin", Chipset = "Helio G85 (MT6769)", Architecture = "MediaTek", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "BROM Erase FRP, Format Userdata, Relock UBL" },
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Redmi Note 10 / 10S / 10 Pro", CodeName = "mojito / rosemary / sweet", Chipset = "Snapdragon 678 / Helio G95 / SD732G", Architecture = "Qualcomm / MediaTek", RecommendedLoaderFile = "prog_ufs_firehose_sm7150_ddr.elf", SupportedOperations = "EDL FRP, BROM FRP, Sideload Format" },
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Redmi Note 11 / 11S / 11 Pro", CodeName = "spes / fleur / veux", Chipset = "Snapdragon 680 / Helio G96 / SD695", Architecture = "Qualcomm / MediaTek", RecommendedLoaderFile = "DA_SWSEC.bin", SupportedOperations = "EDL FRP, BROM FRP, Safe Format" },
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Redmi Note 12 / 12 Pro 4G", CodeName = "tapas / sweet_k6a", Chipset = "Snapdragon 685 / Snapdragon 732G", Architecture = "Qualcomm", RecommendedLoaderFile = "prog_ufs_firehose_sm7150_ddr.elf", SupportedOperations = "EDL FRP Reset, Mi Assistant Sideload" },
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Poco M3 / Redmi 9T", CodeName = "citrus / lime", Chipset = "Snapdragon 662 (SM6115)", Architecture = "Qualcomm", RecommendedLoaderFile = "prog_emmc_firehose_sm4250_ddr.mbn", SupportedOperations = "EDL FRP, Fix Auto-Off, Sideload Wipe" },
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Poco X3 NFC / Poco X3 Pro", CodeName = "surya / vayu", Chipset = "Snapdragon 732G / Snapdragon 860", Architecture = "Qualcomm", RecommendedLoaderFile = "prog_ufs_firehose_sm8150_ddr.elf", SupportedOperations = "EDL FRP, UFS Dump, Sideload Wipe" },
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Poco F3 / Poco F4", CodeName = "alioth / munch", Chipset = "Snapdragon 870 (SM8250-AC)", Architecture = "Qualcomm", RecommendedLoaderFile = "prog_ufs_firehose_sm8150_ddr.elf", SupportedOperations = "EDL FRP, Fastboot Flashing, MiCloud Fix" },

                // ==================== SAMSUNG ====================
                new PhoneModelProfile { Brand = "Samsung", ModelName = "Galaxy A01 Core / A02 / M02", CodeName = "SM-A013F / SM-A022F", Chipset = "MediaTek MT6739", Architecture = "MediaTek", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "BROM Erase FRP, MTP *#0*# Bypass, Fix Hang" },
                // ==================== INFINIX / TRANSSION (EXPANDED 2023-2025) ====================
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Infinix Note 30 / Note 30 Pro", CodeName = "X6833B / X6716B", Chipset = "Helio G99 (MT6789)", Architecture = "MediaTek", RecommendedLoaderFile = "MT6789_0.bin", SupportedOperations = "BROM FRP Erase, Safe Format, NVRAM Backup" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Infinix Note 40 / Note 40 Pro 4G/5G", CodeName = "X6853 / X6850 / X6851", Chipset = "Helio G99 Ultimate / Dimensity 7020", Architecture = "MediaTek", RecommendedLoaderFile = "DA_PL_CRYPTO20.bin", SupportedOperations = "BROM FRP, Bypass Auth, Factory Reset" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Infinix Smart 7 / Smart 8 / Smart 8 HD", CodeName = "X6515 / X6525", Chipset = "Unisoc SC9863A / Unisoc T606", Architecture = "Unisoc", RecommendedLoaderFile = "infinix_hot40i_t606_fdl.bin", SupportedOperations = "Unisoc BSL FRP Erase, Safe Wipe" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Infinix Hot 40 Pro", CodeName = "X6837", Chipset = "Helio G99 (MT6789)", Architecture = "MediaTek", RecommendedLoaderFile = "MT6789_0.bin", SupportedOperations = "BROM FRP, Safe Format, NVRAM Backup" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Infinix Hot 40i", CodeName = "X6528", Chipset = "Unisoc T606 (UMS9230)", Architecture = "Unisoc", RecommendedLoaderFile = "infinix_hot40i_t606_fdl.bin", SupportedOperations = "Unisoc BSL FRP Erase, Safe Wipe" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Infinix GT 10 Pro", CodeName = "X6739", Chipset = "Dimensity 8050 (MT6893)", Architecture = "MediaTek", RecommendedLoaderFile = "mt6885_payload.bin", SupportedOperations = "Dimensity BROM FRP, Partition Dump" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Infinix GT 20 Pro", CodeName = "X6871", Chipset = "Dimensity 8200 (MT6895)", Architecture = "MediaTek", RecommendedLoaderFile = "DA_PL_CRYPTO20.bin", SupportedOperations = "Dimensity BROM SLA, FRP Wipe" },
                new PhoneModelProfile { Brand = "Infinix", ModelName = "Infinix Zero 30 5G / Zero Ultra", CodeName = "X6731 / X6820", Chipset = "Dimensity 8020 / Dimensity 920", Architecture = "MediaTek", RecommendedLoaderFile = "mt6873_payload.bin", SupportedOperations = "BROM FRP, Sideload Wipe Data" },

                // ==================== TECNO / ITEL (MODERN) ====================
                new PhoneModelProfile { Brand = "Tecno", ModelName = "Tecno Pova 4 / Pova 5 / Pova 6 Pro 5G", CodeName = "LG7n / LH7n / LI9", Chipset = "Helio G99 / Dimensity 6080", Architecture = "MediaTek", RecommendedLoaderFile = "MT6789_0.bin", SupportedOperations = "BROM FRP Erase, Safe Wipe, Fastboot Unlock" },
                new PhoneModelProfile { Brand = "Tecno", ModelName = "Tecno Spark 10 Pro / Spark 20 / Spark 20 Pro", CodeName = "KI7 / KJ5 / KJ6", Chipset = "Helio G88 / Helio G85 / Helio G99", Architecture = "MediaTek", RecommendedLoaderFile = "MT6789_0.bin", SupportedOperations = "BROM FRP Erase, Format Userdata" },
                new PhoneModelProfile { Brand = "Tecno", ModelName = "Tecno Camon 20 Pro 5G / Camon 30", CodeName = "CK8n / CL7", Chipset = "Dimensity 8050 / Helio G99", Architecture = "MediaTek", RecommendedLoaderFile = "mt6885_payload.bin", SupportedOperations = "BROM FRP Erase, Safe Format" },
                new PhoneModelProfile { Brand = "Itel", ModelName = "Itel P55 4G / 5G / P55+", CodeName = "P661N / P662L / P663L", Chipset = "Unisoc T606 / Dimensity 6080", Architecture = "Unisoc / MediaTek", RecommendedLoaderFile = "itel_p55_t606_fdl.bin", SupportedOperations = "Unisoc BSL FRP, BROM FRP" },
                new PhoneModelProfile { Brand = "Itel", ModelName = "Itel RS4", CodeName = "S664LN", Chipset = "Helio G99 (MT6789)", Architecture = "MediaTek", RecommendedLoaderFile = "MT6789_0.bin", SupportedOperations = "BROM Erase FRP, Sideload Format" },

                // ==================== ADVAN (INDONESIA) ====================
                new PhoneModelProfile { Brand = "Advan", ModelName = "Advan Tab VX / Tab VX Lite", CodeName = "T618_TAB", Chipset = "Unisoc Tiger T618 (UMS512)", Architecture = "Unisoc", RecommendedLoaderFile = "advan_tab_vx_t618_fdl.bin", SupportedOperations = "Unisoc BSL FRP Erase, PAC Flash" },
                new PhoneModelProfile { Brand = "Advan", ModelName = "Advan Tab Sketsa 2 / Tab A8", CodeName = "T606_SKETSA", Chipset = "Unisoc Tiger T606 (UMS9230)", Architecture = "Unisoc", RecommendedLoaderFile = "advan_tab_sketsa2_t606_fdl.bin", SupportedOperations = "Unisoc BSL FRP, Safe Format" },
                new PhoneModelProfile { Brand = "Advan", ModelName = "Advan Tab Belajar Elite / G5 / G9", CodeName = "SC9863A_TAB", Chipset = "Unisoc SC9863A", Architecture = "Unisoc", RecommendedLoaderFile = "advan_sc9863a_fdl.bin", SupportedOperations = "Unisoc BSL FRP, Factory Reset" },
                new PhoneModelProfile { Brand = "Advan", ModelName = "Advan GX", CodeName = "GX_T618", Chipset = "Unisoc Tiger T618 (UMS512)", Architecture = "Unisoc", RecommendedLoaderFile = "advan_tab_vx_t618_fdl.bin", SupportedOperations = "Unisoc BSL FRP, Userdata Wipe" },

                // ==================== REALME (EXPANDED MODERN) ====================
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme C51s / C53 / C33", CodeName = "RMX3834 / RMX3760 / RMX3627", Chipset = "Unisoc Tiger T612 (UMS9230)", Architecture = "Unisoc", RecommendedLoaderFile = "RMX3627.bin", SupportedOperations = "Unisoc BSL FRP Erase, Safe Wipe" },
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme C55 / C65 / C67 4G", CodeName = "RMX3710 / RMX3910 / RMX3890", Chipset = "Helio G88 / Helio G85 / Snapdragon 685", Architecture = "MediaTek / Qualcomm", RecommendedLoaderFile = "prog_emmc_firehose_sm6125_ddr.mbn", SupportedOperations = "EDL FRP, BROM FRP Erase" },
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme 10 / 11 4G / 12 5G", CodeName = "RMX3630 / RMX3780 / RMX3999", Chipset = "Helio G99 / Dimensity 6100+", Architecture = "MediaTek", RecommendedLoaderFile = "mt6873_payload.bin", SupportedOperations = "BROM Dimensity SLA, FRP Erase" },
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme 11 Pro+ 5G / 12+ 5G", CodeName = "RMX3740 / RMX3867", Chipset = "Dimensity 7050 (MT6877V)", Architecture = "MediaTek", RecommendedLoaderFile = "mt6873_payload.bin", SupportedOperations = "BROM Dimensity SLA, FRP Erase" },
                new PhoneModelProfile { Brand = "Realme", ModelName = "Realme 12 Pro+ 5G", CodeName = "RMX3840", Chipset = "Snapdragon 7s Gen 2 (SM7435-AB)", Architecture = "Qualcomm", RecommendedLoaderFile = "prog_ufs_firehose_sm7150_ddr.elf", SupportedOperations = "EDL FRP, Sideload Format" },

                // ==================== OPPO (EXPANDED MODERN) ====================
                new PhoneModelProfile { Brand = "Oppo", ModelName = "Oppo A18 / A38 / A58 / A78 4G", CodeName = "CPH2591 / CPH2579 / CPH2577 / CPH2565", Chipset = "Helio G85 / Snapdragon 680", Architecture = "MediaTek / Qualcomm", RecommendedLoaderFile = "vivo_mtk_loader_auth_4.bin", SupportedOperations = "BROM SLA Bypass, Format Userdata, FRP Erase" },
                new PhoneModelProfile { Brand = "Oppo", ModelName = "Oppo Reno 8T / Reno 11F 5G", CodeName = "CPH2481 / CPH2603", Chipset = "Helio G99 / Dimensity 7050", Architecture = "MediaTek", RecommendedLoaderFile = "mt6873_payload.bin", SupportedOperations = "BROM SLA FRP, Demo Unlock, Wipe Data" },
                new PhoneModelProfile { Brand = "Oppo", ModelName = "Oppo Reno 10 5G / Reno 11 5G", CodeName = "CPH2531 / CPH2599", Chipset = "Dimensity 7050 (MT6877V)", Architecture = "MediaTek", RecommendedLoaderFile = "mt6873_payload.bin", SupportedOperations = "BROM SLA FRP, Demo Unlock" },
                new PhoneModelProfile { Brand = "Oppo", ModelName = "Oppo Reno 12 5G / Reno 12 Pro", CodeName = "CPH2625 / CPH2629", Chipset = "Dimensity 7300", Architecture = "MediaTek", RecommendedLoaderFile = "DA_PL_CRYPTO20.bin", SupportedOperations = "BROM SLA Bypass, Format Userdata" },

                // ==================== VIVO & IQOO (EXPANDED MODERN) ====================
                new PhoneModelProfile { Brand = "Vivo", ModelName = "Vivo Y03 / Y17s / Y27s / Y36", CodeName = "V2332 / V2310 / V2322 / V2247", Chipset = "Helio G85 / Snapdragon 680", Architecture = "MediaTek / Qualcomm", RecommendedLoaderFile = "vivo_mtk_loader_auth_4.bin", SupportedOperations = "BROM FRP, EDL FRP, Demo Mode" },
                new PhoneModelProfile { Brand = "Vivo", ModelName = "Vivo Y28 4G / Y100 4G/5G", CodeName = "V2352 / V2342 / V2327", Chipset = "Helio G85 / Snapdragon 685 / Snapdragon 4 Gen 2", Architecture = "MediaTek / Qualcomm", RecommendedLoaderFile = "vivo_mtk_loader_auth_4.bin", SupportedOperations = "BROM FRP Erase, EDL 9008 FRP" },
                new PhoneModelProfile { Brand = "Vivo", ModelName = "Vivo V27e / V29e / V30e 5G", CodeName = "V2237 / V2303 / V2339", Chipset = "Helio G99 / Snapdragon 695 / Snapdragon 6 Gen 1", Architecture = "MediaTek / Qualcomm", RecommendedLoaderFile = "prog_ufs_firehose_sm7150_ddr.elf", SupportedOperations = "BROM FRP, EDL 9008 FRP, Sideload Wipe" },
                new PhoneModelProfile { Brand = "Vivo", ModelName = "iQOO Z7x / iQOO Z7 5G / iQOO Z9x", CodeName = "I2216 / I2207 / I2303", Chipset = "Snapdragon 695 / Dimensity 920 / Snapdragon 6 Gen 1", Architecture = "Qualcomm / MediaTek", RecommendedLoaderFile = "prog_ufs_firehose_sm7150_ddr.elf", SupportedOperations = "EDL FRP, BROM Dimensity FRP" },

                // ==================== XIAOMI / POCO (EXPANDED MODERN) ====================
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Redmi 13 4G / Redmi 14C / Poco M6 Pro", CodeName = "moon / lake / emerald", Chipset = "Helio G91 Ultra / Helio G81 / Helio G99 Ultra", Architecture = "MediaTek", RecommendedLoaderFile = "mt6768_payload.bin", SupportedOperations = "BROM FRP, Sideload Format" },
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Redmi Note 13 4G / Note 13 5G", CodeName = "sapphire / gold", Chipset = "Snapdragon 685 / Dimensity 6080", Architecture = "Qualcomm / MediaTek", RecommendedLoaderFile = "prog_ufs_firehose_sm7150_ddr.elf", SupportedOperations = "EDL FRP, BROM FRP Erase, Sideload Wipe" },
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Redmi Note 12 Pro 5G / Note 13 Pro+ 5G", CodeName = "ruby / zircon", Chipset = "Dimensity 1080 / Dimensity 7200 Ultra", Architecture = "MediaTek", RecommendedLoaderFile = "mt6873_payload.bin", SupportedOperations = "Dimensity BROM FRP, Fastboot Flash" },
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Poco X6 5G / Poco X6 Pro", CodeName = "garnet / duchamp", Chipset = "Snapdragon 7s Gen 2 / Dimensity 8300 Ultra", Architecture = "Qualcomm / MediaTek", RecommendedLoaderFile = "prog_ufs_firehose_sm7150_ddr.elf", SupportedOperations = "EDL FRP, Sideload Wipe Data" },
                new PhoneModelProfile { Brand = "Xiaomi", ModelName = "Poco F5 / Poco F6", CodeName = "marble / peridot", Chipset = "Snapdragon 7+ Gen 2 / Snapdragon 8s Gen 3", Architecture = "Qualcomm", RecommendedLoaderFile = "prog_ufs_firehose_sm8150_ddr.elf", SupportedOperations = "EDL FRP, Fastboot Flashing, MiCloud Fix" },

                // ==================== SAMSUNG EXYNOS & MODERN (EXPANDED 2023-2025) ====================
                new PhoneModelProfile { Brand = "Samsung", ModelName = "Galaxy A04s / A12s (Exynos)", CodeName = "SM-A047F / SM-A127F", Chipset = "Exynos 850", Architecture = "Samsung Exynos", RecommendedLoaderFile = "MTK_AllInOne_DA.bin", SupportedOperations = "MTP *#0*# Bypass, Odin 4-File Tar Flash, Pit Repartition" },
                new PhoneModelProfile { Brand = "Samsung", ModelName = "Galaxy A13 / A14 4G/5G (Exynos)", CodeName = "SM-A135F / SM-A145F / SM-A146P", Chipset = "Exynos 850 / Exynos 1330", Architecture = "Samsung Exynos", RecommendedLoaderFile = "DA_A14_lamulg.bin", SupportedOperations = "MTP *#0*# Bypass, Odin Tar Flash, CSC Fix" },
                new PhoneModelProfile { Brand = "Samsung", ModelName = "Galaxy A15 4G/5G / Galaxy A24", CodeName = "SM-A155F / SM-A156B / SM-A245F", Chipset = "Helio G99 / Dimensity 6100+", Architecture = "MediaTek", RecommendedLoaderFile = "DA_A15_lamu.bin", SupportedOperations = "MTP *#0*# Bypass, BROM FRP Erase, Safe Wipe" },
                new PhoneModelProfile { Brand = "Samsung", ModelName = "Galaxy A25 5G / A33 5G / A53 5G", CodeName = "SM-A256B / SM-A336B / SM-A536B", Chipset = "Exynos 1280", Architecture = "Samsung Exynos", RecommendedLoaderFile = "prog_ufs_firehose_sm7150_ddr.elf", SupportedOperations = "MTP *#0*# Bypass, Odin 4-File Flash, Knox Guard Fix" },
                new PhoneModelProfile { Brand = "Samsung", ModelName = "Galaxy A34 5G / A35 5G / A54 5G / A55 5G", CodeName = "SM-A346B / SM-A356B / SM-A546E / SM-A556B", Chipset = "Dimensity 1080 / Exynos 1380 / Exynos 1480", Architecture = "MediaTek / Samsung Exynos", RecommendedLoaderFile = "DA_A15_lamu.bin", SupportedOperations = "MTP *#0*# Bypass, Odin Tar Flash, CSC Tool" },

                // ==================== HUAWEI / HONOR (HISILICON KIRIN) ====================
                new PhoneModelProfile { Brand = "Huawei", ModelName = "Huawei Nova 3i / Y9 Prime 2019", CodeName = "INE-LX2 / STK-L21", Chipset = "HiSilicon Kirin 710 / 710F", Architecture = "HiSilicon Kirin", RecommendedLoaderFile = "xloader_kirin710.img", SupportedOperations = "USB COM 1.0 Unbrick, Testpoint FRP Erase, Fastboot Flash" },
                new PhoneModelProfile { Brand = "Huawei", ModelName = "Huawei Nova 7i / P30 Pro", CodeName = "JNY-LX1 / VOG-L29", Chipset = "HiSilicon Kirin 810 / Kirin 980", Architecture = "HiSilicon Kirin", RecommendedLoaderFile = "xloader_kirin810.img", SupportedOperations = "USB COM 1.0 Test Point, FRP Unlock, Repair Cert" }
            };
        }

        private static List<LoaderInfo> InitializeSeedLoaders()
        {
            return new List<LoaderInfo>
            {
                new LoaderInfo { Id = "QCOM_SM6125", Brand = "Universal", Model = "Snapdragon 665 (SM6125)", Chipset = "Snapdragon 665 (SM6125)", Type = LoaderType.Qualcomm_Firehose, FileName = "prog_emmc_firehose_sm6125_ddr.mbn", FileSize = 485120, Description = "Firehose Programmer Snapdragon 665 eMMC DDR" },
                new LoaderInfo { Id = "QCOM_SDM660", Brand = "Universal", Model = "Snapdragon 660 (SDM660)", Chipset = "Snapdragon 660 (SDM660)", Type = LoaderType.Qualcomm_Firehose, FileName = "prog_emmc_firehose_sdm660_ddr.mbn", FileSize = 452100, Description = "Firehose Programmer Snapdragon 660 eMMC" },
                new LoaderInfo { Id = "QCOM_SM8150", Brand = "Universal", Model = "Snapdragon 855/860 (SM8150)", Chipset = "Snapdragon 860 (SM8150-AC)", Type = LoaderType.Qualcomm_Firehose, FileName = "prog_ufs_firehose_sm8150_ddr.elf", FileSize = 712300, Description = "Firehose Programmer Snapdragon 855/860 UFS ELF" },
                new LoaderInfo { Id = "QCOM_SM7150", Brand = "Universal", Model = "Snapdragon 730/732G (SM7150)", Chipset = "Snapdragon 732G (SM7150)", Type = LoaderType.Qualcomm_Firehose, FileName = "prog_ufs_firehose_sm7150_ddr.elf", FileSize = 684200, Description = "Firehose Programmer Snapdragon 730/732G UFS" },
                new LoaderInfo { Id = "QCOM_SM4250", Brand = "Universal", Model = "Snapdragon 460/662 (SM4250)", Chipset = "Snapdragon 460 (SM4250)", Type = LoaderType.Qualcomm_Firehose, FileName = "prog_emmc_firehose_sm4250_ddr.mbn", FileSize = 468000, Description = "Firehose Programmer Snapdragon 460/662 eMMC" },
                new LoaderInfo { Id = "QCOM_SDM439", Brand = "Universal", Model = "Snapdragon 439/429 (SDM439)", Chipset = "Snapdragon 439 (SDM439)", Type = LoaderType.Qualcomm_Firehose, FileName = "prog_emmc_firehose_sdm439_ddr.mbn", FileSize = 421000, Description = "Firehose Programmer Snapdragon 439 eMMC" },
                new LoaderInfo { Id = "QCOM_SDM450", Brand = "Universal", Model = "Snapdragon 450 (SDM450)", Chipset = "Snapdragon 450 (SDM450)", Type = LoaderType.Qualcomm_Firehose, FileName = "prog_emmc_firehose_sdm450_ddr.mbn", FileSize = 431000, Description = "Firehose Programmer Snapdragon 450 eMMC" },
                new LoaderInfo { Id = "MTK_ALL_MAIN", Brand = "Universal", Model = "MediaTek All-In-One Container", Chipset = "MT6580/6735/6739/6765/6768", Type = LoaderType.MediaTek_DA, FileName = "MTK_AllInOne_DA.bin", FileSize = 12945664, Description = "Master All-In-One DA Multi-Chipset Binary" },
                new LoaderInfo { Id = "MTK_DA_PL", Brand = "Universal", Model = "MediaTek Preloader DA", Chipset = "MT6765 / MT6768 / MT6785", Type = LoaderType.MediaTek_DA, FileName = "DA_PL.bin", FileSize = 12834208, Description = "Preloader Download Agent High-Security" },
                new LoaderInfo { Id = "MTK_DA_PL_CRYPTO20", Brand = "Universal", Model = "MediaTek Preloader DA Crypto v2.0", Chipset = "MT6765 / MT6768 / MT6785", Type = LoaderType.MediaTek_DA, FileName = "DA_PL_CRYPTO20.bin", FileSize = 12834208, Description = "Preloader Download Agent Crypto 2.0 (Oppo/Realme SLA)" },
                new LoaderInfo { Id = "MTK_DA_SWSEC", Brand = "Universal", Model = "MediaTek Software Security DA", Chipset = "MT6768 / MT6769 / MT6785", Type = LoaderType.MediaTek_DA, FileName = "DA_SWSEC.bin", FileSize = 4056488, Description = "Software Security Download Agent (Transsion / Xiaomi)" },
                new LoaderInfo { Id = "MTK_VIVO_AUTH", Brand = "Vivo", Model = "Vivo Auth MTK Loader", Chipset = "MT6765 / MT6768 / MT6769", Type = LoaderType.MediaTek_DA, FileName = "vivo_mtk_loader_auth_4.bin", FileSize = 8920648, Description = "Vivo Custom Auth Loader MediaTek" },
                new LoaderInfo { Id = "MTK_G99_MT6789", Brand = "Universal", Model = "Helio G99 (MT6789)", Chipset = "Helio G99 (MT6789)", Type = LoaderType.MediaTek_DA, FileName = "MT6789_0.bin", FileSize = 752712, Description = "Download Agent Helio G99 / MT6789 Series" },
                new LoaderInfo { Id = "MTK_DIMENSITY_6833", Brand = "Universal", Model = "Dimensity 700 / 6020 (MT6833)", Chipset = "Dimensity 700 (MT6833)", Type = LoaderType.MediaTek_DA, FileName = "MT6833_1.bin", FileSize = 765552, Description = "Download Agent Dimensity 700/810/6020 5G" },
                new LoaderInfo { Id = "MTK_DIMENSITY_6877", Brand = "Universal", Model = "Dimensity 900 / 1080 / 7050", Chipset = "Dimensity 900 (MT6877)", Type = LoaderType.MediaTek_DA, FileName = "MT6877_2.bin", FileSize = 1011392, Description = "Download Agent Dimensity 900/1080/7050 5G" },
                new LoaderInfo { Id = "SPD_T606", Brand = "Universal", Model = "Unisoc Tiger T606 (Infinix/Itel/Realme)", Chipset = "Unisoc T606", Type = LoaderType.Unisoc_FDL, FileName = "T606_64.bin", FileSize = 1090881, Description = "Download Loader Unisoc Tiger T606 64-bit" },
                new LoaderInfo { Id = "SPD_T612", Brand = "Realme", Model = "Realme C30 / C31 / C33 / C51 / C53", Chipset = "Unisoc T612", Type = LoaderType.Unisoc_FDL, FileName = "T612_64.bin", FileSize = 1089969, Description = "Download Loader Unisoc Tiger T612 64-bit" },
                new LoaderInfo { Id = "SPD_RMX3261", Brand = "Realme", Model = "Realme C21Y / C25Y", Chipset = "Unisoc T610", Type = LoaderType.Unisoc_FDL, FileName = "RMX3261.bin", FileSize = 946226, Description = "Unisoc Loader Realme C21Y / C25Y" },
                new LoaderInfo { Id = "SPD_RMX3511", Brand = "Realme", Model = "Realme C35", Chipset = "Unisoc T616", Type = LoaderType.Unisoc_FDL, FileName = "RMX3511.bin", FileSize = 59816, Description = "Unisoc Loader Realme C35" },
                new LoaderInfo { Id = "SPD_SC9863A", Brand = "Universal", Model = "Unisoc SC9863A (Smart 7/Itel)", Chipset = "Unisoc SC9863A", Type = LoaderType.Unisoc_FDL, FileName = "9863A_64.bin", FileSize = 965265, Description = "Download Loader Unisoc SC9863A 64-bit" }
            };
        }
    }
}
