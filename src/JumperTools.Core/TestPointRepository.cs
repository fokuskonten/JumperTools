using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using JumperTools.Data;
using JumperTools.Models;

namespace JumperTools.Core
{
    /// <summary>
    /// Repository and Offline Catalog for smartphone Test Point and ISP Pinout diagrams
    /// </summary>
    public class TestPointRepository
    {
        private readonly List<TestPointInfo> _catalog;
        private readonly DatabaseContext? _db;

        public TestPointRepository(DatabaseContext? db = null)
        {
            _db = db;
            _catalog = InitializeSeedCatalog();

            // Sync with local SQLite cache if available
            if (_db != null)
            {
                try
                {
                    var dbPoints = _db.GetAllTestPoints();
                    foreach (var p in _catalog)
                    {
                        if (!dbPoints.Any(d => d.MarketingName == p.MarketingName && d.Brand == p.Brand))
                        {
                            _db.SaveTestPoint(p);
                        }
                    }
                    foreach (var p in dbPoints)
                    {
                        if (!_catalog.Any(c => c.MarketingName == p.MarketingName && c.Brand == p.Brand))
                        {
                            _catalog.Add(p);
                        }
                    }
                }
                catch { }
            }
        }

        public void AddOrUpdate(TestPointInfo tp)
        {
            var idx = _catalog.FindIndex(c => c.Id == tp.Id);
            if (idx >= 0) _catalog[idx] = tp;
            else _catalog.Add(tp);

            try
            {
                _db?.SaveTestPoint(tp);
            }
            catch { }
        }

        public List<TestPointInfo> GetAll() => _catalog.ToList();

        public List<TestPointInfo> GetByBrand(string brand)
        {
            if (string.IsNullOrWhiteSpace(brand) || brand.Equals("Semua", StringComparison.OrdinalIgnoreCase))
                return GetAll();

            return _catalog.Where(t => t.Brand.Equals(brand, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public List<TestPointInfo> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return GetAll();

            var q = query.Trim().ToLower();
            return _catalog.Where(t =>
                t.MarketingName.ToLower().Contains(q) ||
                t.ModelName.ToLower().Contains(q) ||
                t.Brand.ToLower().Contains(q) ||
                t.Chipset.ToLower().Contains(q) ||
                t.Type.ToString().ToLower().Contains(q)
            ).ToList();
        }

        private static List<TestPointInfo> InitializeSeedCatalog()
        {
            var combinedList = new List<TestPointInfo>();
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            // 1. Primary: Load Master Testpoints (3,078+ items)
            var primaryTpPath = AppPathResolver.TestPointsMappingJson;
            if (File.Exists(primaryTpPath))
            {
                try
                {
                    var json = File.ReadAllText(primaryTpPath);
                    var tpList = JsonSerializer.Deserialize<List<TestPointInfo>>(json, options);
                    if (tpList != null) combinedList.AddRange(tpList);
                }
                catch { }
            }

            // 2. Secondary: Load Master ISP Pinouts (1,565+ items)
            var primaryIspPath = AppPathResolver.IspPinoutsMappingJson;
            if (File.Exists(primaryIspPath))
            {
                try
                {
                    var json = File.ReadAllText(primaryIspPath);
                    var ispList = JsonSerializer.Deserialize<List<TestPointInfo>>(json, options);
                    if (ispList != null) combinedList.AddRange(ispList);
                }
                catch { }
            }

            if (combinedList.Count > 0)
            {
                return combinedList;
            }

            // Fallback search paths for portable releases
            var searchPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database", "testpoints_mapping.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "testpoints_mapping.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "database", "testpoints_mapping.json")
            };

            foreach (var jsonPath in searchPaths)
            {
                if (File.Exists(jsonPath))
                {
                    try
                    {
                        var json = File.ReadAllText(jsonPath);
                        var mappedList = JsonSerializer.Deserialize<List<TestPointInfo>>(json, options);
                        if (mappedList != null && mappedList.Count > 0)
                        {
                            return mappedList;
                        }
                    }
                    catch { }
                }
            }

            return GetDefaultHardcodedCatalog();
        }

        private static List<TestPointInfo> GetDefaultHardcodedCatalog()
        {
            return new List<TestPointInfo>
            {
                // =========================================================================
                // 1. XIAOMI / REDMI / POCO
                // =========================================================================
                new TestPointInfo {
                    Brand = "Xiaomi", MarketingName = "Redmi 5A", ModelName = "riva", Chipset = "Snapdragon 425 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di sebelah kanan soket fleksibel LCD",
                    Instructions = "1. Buka penutup belakang.\n2. Lepas baterai.\n3. Hubungkan 2 titik Test Point EDL menggunakan pinset.\n4. Colok kabel USB hingga terdeteksi 'Qualcomm HS-USB QDLoader 9008'.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Xiaomi", MarketingName = "Redmi 6 / 6A", ModelName = "cereus / cactus", Chipset = "Helio P22 / A22 (MT6762 / MT6761)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Titik emas CLK di samping kanan konektor baterai",
                    Instructions = "1. Lepas baterai.\n2. Hubungkan titik Test Point CLK ke Ground (kaleng pelindung PCB).\n3. Colok kabel USB ke PC (Mode BROM).", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Xiaomi", MarketingName = "Redmi 7 / Y3", ModelName = "onclite / onc", Chipset = "Snapdragon 632 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di atas soket konektor baterai",
                    Instructions = "1. Buka backdoor.\n2. Hubungkan 2 titik TP EDL dengan pinset.\n3. Colok kabel USB ke PC hingga terdeteksi Port 9008.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Xiaomi", MarketingName = "Redmi 8 / 8A / 8A Pro", ModelName = "olive / olivelite / olivewood", Chipset = "Snapdragon 439 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di dekat soket kamera belakang",
                    Instructions = "1. Lepas konektor baterai.\n2. Jumper 2 titik Test Point ke Ground.\n3. Colokkan kabel USB ke PC untuk masuk mode EDL 9008.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Xiaomi", MarketingName = "Redmi Note 7 / 7 Pro", ModelName = "lavender / violet", Chipset = "Snapdragon 660 / 675 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di sebelah kiri soket fingerprint",
                    Instructions = "1. Buka backdoor kaca.\n2. Lepas baterai.\n3. Hubungkan 2 titik TP EDL.\n4. Colokkan kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Xiaomi", MarketingName = "Redmi Note 8", ModelName = "ginkgo", Chipset = "Snapdragon 665 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "Sebelah kanan soket fingerprint / di atas soket baterai",
                    Instructions = "1. Buka penutup belakang (backdoor).\n2. Lepas konektor baterai.\n3. Hubungkan 2 titik emas Test Point (TP) menggunakan pinset.\n4. Tahan pinset, lalu colokkan kabel USB ke PC hingga terdeteksi 'Qualcomm HS-USB QDLoader 9008'.\n5. Pasang kembali konektor baterai.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Xiaomi", MarketingName = "Redmi Note 8 Pro", ModelName = "begonia", Chipset = "Helio G90T (MT6785 MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Titik emas di dekat konektor fleksibel sub-board",
                    Instructions = "1. Buka backdoor.\n2. Hubungkan titik CLK Test Point ke Ground.\n3. Colokkan kabel USB ke PC untuk eksekusi Auth Bypass & Flash BROM.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Xiaomi", MarketingName = "Redmi 9", ModelName = "lancelot", Chipset = "Helio G80 (MT6768 MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Di bawah kaleng pelindung IC Power / samping soket LCD",
                    Instructions = "1. Buka penutup mesin belakang.\n2. Lepas baterai.\n3. Hubungkan titik Test Point CLK ke Ground (GND/Kaleng mesin) menggunakan pinset.\n4. Colokkan kabel USB ke PC hingga terdeteksi 'MediaTek USB Port'.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Xiaomi", MarketingName = "Redmi 9A / 9C", ModelName = "dandelion / angelica", Chipset = "Helio G25 / G35 (MT6765 MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Samping soket tombol power & volume",
                    Instructions = "1. Hubungkan titik Test Point ke Ground menggunakan pinset.\n2. Colokkan kabel USB untuk masuk ke mode BROM.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Xiaomi", MarketingName = "Redmi Note 9", ModelName = "merlin", Chipset = "Helio G85 (MT6769 MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Di samping konektor baterai (titik emas ganda)",
                    Instructions = "1. Buka backdoor HP.\n2. Hubungkan titik Test Point ke Ground.\n3. Colok kabel USB ke PC dalam kondisi baterai terpasang/lepas.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Xiaomi", MarketingName = "Redmi Note 10 4G", ModelName = "mojito / sunny", Chipset = "Snapdragon 678 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di sebelah kanan soket fleksibel baterai",
                    Instructions = "1. Buka backdoor.\n2. Lepas konektor baterai.\n3. Hubungkan 2 titik Test Point EDL menggunakan pinset.\n4. Colokkan kabel USB ke PC hingga terdeteksi 'Qualcomm 9008'.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Xiaomi", MarketingName = "Redmi Note 10 Pro", ModelName = "sweet", Chipset = "Snapdragon 732G (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di bawah kamera utama / samping soket LCD",
                    Instructions = "1. Buka penutup mesin.\n2. Jumper 2 titik TP EDL.\n3. Colok USB untuk mode 9008.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Xiaomi", MarketingName = "Redmi Note 11", ModelName = "spes / spesn", Chipset = "Snapdragon 680 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "Dekat konektor fleksibel LCD / bawah kamera",
                    Instructions = "1. Buka penutup mesin belakang.\n2. Hubungkan 2 titik emas EDL.\n3. Colokkan kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Xiaomi", MarketingName = "Redmi Note 12 4G", ModelName = "tapas / topaz", Chipset = "Snapdragon 685 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di samping soket baterai",
                    Instructions = "1. Buka backdoor.\n2. Lepas soket baterai.\n3. Hubungkan 2 titik Test Point EDL.\n4. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Xiaomi", MarketingName = "Redmi 10 / 10 2022", ModelName = "selene", Chipset = "Helio G88 (MT6769 MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Titik emas di dekat konektor fleksibel LCD",
                    Instructions = "1. Hubungkan TP CLK ke Ground.\n2. Colok kabel USB ke PC untuk masuk mode MTK BROM.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Xiaomi", MarketingName = "Redmi 12 / 12C", ModelName = "fire / earth", Chipset = "Helio G88 / G85 (MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Samping soket tombol power & volume",
                    Instructions = "1. Hubungkan Test Point ke Ground.\n2. Colokkan kabel USB untuk Bypass Auth & Flash.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Xiaomi", MarketingName = "Redmi 13C / Poco C65", ModelName = "gale", Chipset = "Helio G85 (MT6769 MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Di bawah soket kamera / samping konektor baterai",
                    Instructions = "1. Lepas baterai.\n2. Hubungkan TP ke GND.\n3. Colok USB untuk mode BROM.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Xiaomi", MarketingName = "Poco X3 NFC / X3 Pro", ModelName = "surya / vayu", Chipset = "Snapdragon 732G / 860 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "Dekat konektor fleksibel sub-board / bawah kamera utama",
                    Instructions = "1. Buka backdoor.\n2. Lepas konektor baterai.\n3. Hubungkan 2 titik Test Point EDL menggunakan pinset.\n4. Colokkan kabel USB ke PC sampai terdeteksi Port 9008.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Xiaomi", MarketingName = "Poco M3 / Redmi 9T", ModelName = "citrus / lime", Chipset = "Snapdragon 662 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di atas soket baterai / samping IC Power",
                    Instructions = "1. Buka penutup mesin.\n2. Jumper 2 titik TP EDL.\n3. Colok USB untuk unbrick & flash 9008.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Xiaomi", MarketingName = "Poco F3", ModelName = "alioth", Chipset = "Snapdragon 870 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas dekat konektor baterai",
                    Instructions = "1. Lepas baterai.\n2. Jumper 2 titik Test Point EDL.\n3. Colok kabel USB ke PC.", IsVerified = true
                },

                // =========================================================================
                // 2. OPPO
                // =========================================================================
                new TestPointInfo {
                    Brand = "Oppo", MarketingName = "Oppo A1k", ModelName = "CPH1923", Chipset = "Helio P22 (MT6762 MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Di samping soket LCD / dekat konektor baterai",
                    Instructions = "1. Hubungkan TP ke Ground.\n2. Colokkan kabel USB untuk Erase FRP / Pola.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Oppo", MarketingName = "Oppo A3s / A5", ModelName = "CPH1803 / CPH1853", Chipset = "Snapdragon 450 (Qualcomm)",
                    Type = PinoutType.ISP_EMMC, TestPointLocation = "Pinout ISP eMMC (CLK, CMD, DAT0, VCC, VCCQ, GND)",
                    Instructions = "1. Buka kaleng eMMC.\n2. Solder kabel jumper pada titik CLK, CMD, DAT0, dan GND.\n3. Pasang ke box adaptor ISP untuk direct format eMMC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Oppo", MarketingName = "Oppo A5s", ModelName = "CPH1909", Chipset = "Helio P35 (MT6765 MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Di atas soket baterai, sebelah kiri IC Charging",
                    Instructions = "1. Buka casing belakang.\n2. Lepas baterai sebentar lalu pasang kembali.\n3. Hubungkan titik Test Point ke Ground (kaleng pelindung).\n4. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Oppo", MarketingName = "Oppo A12 / A15 / A15s", ModelName = "CPH2083 / CPH2185", Chipset = "Helio P35 (MT6765 MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Sebelah kanan soket kamera belakang",
                    Instructions = "1. Buka backdoor.\n2. Hubungkan titik Test Point BROM ke Ground.\n3. Colok USB untuk bypass proteksi SLA/DAA.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Oppo", MarketingName = "Oppo A16 / A16k / A16e", ModelName = "CPH2269 / CPH2349", Chipset = "Helio G35 / Helio P22 (MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Samping soket tombol power & volume",
                    Instructions = "1. Hubungkan Test Point BROM ke Ground.\n2. Colok USB ke PC untuk Erase FRP / Userdata.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Oppo", MarketingName = "Oppo A17 / A17k", ModelName = "CPH2477 / CPH2471", Chipset = "Helio G35 (MT6765 MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Dekat konektor fleksibel baterai",
                    Instructions = "1. Lepas baterai.\n2. Jumper TP ke GND.\n3. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Oppo", MarketingName = "Oppo A53 / A33 2020", ModelName = "CPH2127 / CPH2137", Chipset = "Snapdragon 460 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di samping konektor LCD / bawah kamera",
                    Instructions = "1. Buka penutup mesin.\n2. Jumper 2 titik TP EDL.\n3. Colok USB hingga terdeteksi Port 9008.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Oppo", MarketingName = "Oppo A54", ModelName = "CPH2239", Chipset = "Helio P35 (MT6765 MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Titik emas di samping soket fingerprint",
                    Instructions = "1. Buka backdoor.\n2. Hubungkan TP ke GND.\n3. Colok USB untuk MTK BROM Mode.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Oppo", MarketingName = "Oppo A57 2022 / A77s", ModelName = "CPH2387 / CPH2473", Chipset = "Helio G35 / Snapdragon 680",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Dekat soket tombol volume",
                    Instructions = "1. Lepas baterai.\n2. Hubungkan TP ke Ground.\n3. Colok USB untuk mode BROM/EDL.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Oppo", MarketingName = "Oppo A58 / A78 4G", ModelName = "CPH2577 / CPH2565", Chipset = "Helio G85 / Snapdragon 680",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Samping konektor baterai / dekat IC Power",
                    Instructions = "1. Buka backdoor.\n2. Hubungkan TP ke GND.\n3. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Oppo", MarketingName = "Oppo Reno 4 / 4F", ModelName = "CPH2113 / CPH2209", Chipset = "Snapdragon 720G / Helio P95",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di samping soket baterai",
                    Instructions = "1. Buka backdoor kaca.\n2. Hubungkan 2 titik TP EDL.\n3. Colokkan kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Oppo", MarketingName = "Oppo Reno 5 / 5F", ModelName = "CPH2159 / CPH2217", Chipset = "Snapdragon 720G / Helio P95",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di bawah modul kamera belakang",
                    Instructions = "1. Buka penutup mesin.\n2. Jumper TP EDL.\n3. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Oppo", MarketingName = "Oppo Reno 8T 4G", ModelName = "CPH2481", Chipset = "Helio G99 (MT6789 MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Titik emas di dekat konektor fleksibel sub-board",
                    Instructions = "1. Lepas baterai.\n2. Hubungkan TP ke Ground.\n3. Colok USB untuk MTK BROM Mode.", IsVerified = true
                },

                // =========================================================================
                // 3. VIVO
                // =========================================================================
                new TestPointInfo {
                    Brand = "Vivo", MarketingName = "Vivo Y91 / Y93 / Y95", ModelName = "Vivo 1814 / 1815 / 1807", Chipset = "Snapdragon 439 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "Dekat konektor fingerprint / bawah kamera",
                    Instructions = "1. Hubungkan 2 titik Test Point TP ke GND.\n2. Colokkan kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Vivo", MarketingName = "Vivo Y12 / Y15 / Y17", ModelName = "Vivo 1904 / 1901 / 1902", Chipset = "Helio P22 / P35 (MT6765 MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Di bawah soket fleksibel LCD",
                    Instructions = "1. Buka backdoor.\n2. Hubungkan titik Test Point ke Ground menggunakan pinset.\n3. Colokkan kabel USB ke PC (BROM Mode).", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Vivo", MarketingName = "Vivo Y20 / Y20s / Y20i", ModelName = "V2029 / V2043", Chipset = "Snapdragon 460 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di atas soket baterai",
                    Instructions = "1. Buka backdoor dan penutup mesin.\n2. Hubungkan 2 titik Test Point EDL menggunakan pinset.\n3. Colok USB hingga terdeteksi QDLoader 9008.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Vivo", MarketingName = "Vivo Y01 / Y02 / Y02t", ModelName = "V2140 / V2217", Chipset = "Helio P35 / P22 (MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Samping konektor baterai / dekat tombol power",
                    Instructions = "1. Buka backdoor.\n2. Hubungkan TP ke Ground.\n3. Colok USB untuk mode BROM.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Vivo", MarketingName = "Vivo Y16 / Y03", ModelName = "V2204 / V2332", Chipset = "Helio P35 / Helio G85",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Di bawah modul kamera utama",
                    Instructions = "1. Lepas baterai.\n2. Hubungkan TP ke GND.\n3. Colok USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Vivo", MarketingName = "Vivo Y21 / Y21s / Y21a", ModelName = "V2111 / V2110", Chipset = "Helio P35 / Helio G80 (MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Di samping soket fleksibel LCD",
                    Instructions = "1. Buka backdoor.\n2. Hubungkan titik Test Point ke Ground.\n3. Colok kabel USB untuk masuk ke mode BROM.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Vivo", MarketingName = "Vivo Y22 / Y22s", ModelName = "V2207 / V2206", Chipset = "Helio G85 / Snapdragon 680",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Dekat konektor tombol volume",
                    Instructions = "1. Lepas baterai.\n2. Hubungkan TP ke Ground.\n3. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Vivo", MarketingName = "Vivo Y30 / Y50", ModelName = "Vivo 1938 / 1935", Chipset = "Helio P35 / Snapdragon 665",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di samping soket LCD",
                    Instructions = "1. Buka penutup mesin.\n2. Jumper 2 titik TP EDL.\n3. Colok USB untuk mode 9008/BROM.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Vivo", MarketingName = "Vivo Y35", ModelName = "V2250", Chipset = "Snapdragon 680 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di atas soket baterai",
                    Instructions = "1. Hubungkan 2 titik TP EDL menggunakan pinset.\n2. Colokkan kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Vivo", MarketingName = "Vivo Y17s / Y28", ModelName = "V2310 / V2352", Chipset = "Helio G85 (MT6769 MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Sebelah kiri soket tombol volume / power",
                    Instructions = "1. Buka backdoor.\n2. Hubungkan titik Test Point ke Ground (kaleng mesin).\n3. Colok USB untuk MTK BROM Mode.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Vivo", MarketingName = "Vivo V20 / V20 SE", ModelName = "V2024 / V2022", Chipset = "Snapdragon 720G / 665",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di samping soket baterai",
                    Instructions = "1. Buka backdoor kaca.\n2. Jumper 2 titik TP EDL.\n3. Colok kabel USB ke PC.", IsVerified = true
                },

                // =========================================================================
                // 4. REALME
                // =========================================================================
                new TestPointInfo {
                    Brand = "Realme", MarketingName = "Realme C1 / 2 / 2 Pro", ModelName = "RMX1811 / RMX1801", Chipset = "Snapdragon 450 / 660 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas dekat konektor fleksibel baterai",
                    Instructions = "1. Buka penutup mesin.\n2. Jumper 2 titik TP EDL.\n3. Colok USB untuk mode 9008.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Realme", MarketingName = "Realme C2 / C11 / C12 / C15", ModelName = "RMX1941 / RMX2185 / RMX2180", Chipset = "Helio G35 / P22 (MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Samping soket kamera / dekat konektor baterai",
                    Instructions = "1. Hubungkan titik Test Point ke Ground.\n2. Colok kabel USB ke PC untuk eksekusi Erase FRP / Userdata.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Realme", MarketingName = "Realme C21 / C21Y / C25", ModelName = "RMX3201 / RMX3261 / RMX3193", Chipset = "Helio G35 / Unisoc T610 / Helio G70",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Dekat konektor tombol volume & power",
                    Instructions = "1. Hubungkan TP ke Ground.\n2. Colokkan kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Realme", MarketingName = "Realme C30 / C30s / C31", ModelName = "RMX3581 / RMX3690 / RMX3501", Chipset = "Unisoc T612 / SC9863A",
                    Type = PinoutType.SPREADTRUM_FDL, TestPointLocation = "Titik emas samping konektor sub-board",
                    Instructions = "1. Lepas baterai.\n2. Hubungkan TP ke GND.\n3. Colok USB untuk mode SPRD Download.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Realme", MarketingName = "Realme C35 / C53 / C55", ModelName = "RMX3511 / RMX3760 / RMX3710", Chipset = "Unisoc T616 / T612 / Helio G88",
                    Type = PinoutType.SPREADTRUM_FDL, TestPointLocation = "Dekat konektor fleksibel sub-board",
                    Instructions = "1. Lepas baterai.\n2. Hubungkan titik TP ke Ground.\n3. Colok USB untuk mode Flashing.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Realme", MarketingName = "Realme C67 4G", ModelName = "RMX3890", Chipset = "Snapdragon 685 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di atas soket baterai",
                    Instructions = "1. Buka backdoor.\n2. Hubungkan 2 titik TP EDL.\n3. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Realme", MarketingName = "Realme 5 / 5i / 5 Pro", ModelName = "RMX1911 / RMX2030 / RMX1971", Chipset = "Snapdragon 665 / 712 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas dekat konektor LCD",
                    Instructions = "1. Hubungkan 2 titik TP EDL menggunakan pinset.\n2. Colok kabel USB ke PC untuk mode 9008.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Realme", MarketingName = "Realme 6 / 6 Pro", ModelName = "RMX2001 / RMX2061", Chipset = "Helio G90T / Snapdragon 720G",
                    Type = PinoutType.EDL_9008, TestPointLocation = "Samping soket baterai / dekat kamera",
                    Instructions = "1. Buka backdoor.\n2. Jumper TP ke GND.\n3. Colokkan kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Realme", MarketingName = "Realme 7 / 7i / 7 Pro", ModelName = "RMX2151 / RMX2103 / RMX2170", Chipset = "Helio G95 / Snapdragon 662 / 720G",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di bawah modul kamera belakang",
                    Instructions = "1. Lepas baterai.\n2. Jumper 2 titik TP EDL.\n3. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Realme", MarketingName = "Realme 8 / 8 Pro", ModelName = "RMX3085 / RMX3081", Chipset = "Helio G95 / Snapdragon 720G",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di samping soket LCD",
                    Instructions = "1. Buka backdoor.\n2. Hubungkan 2 titik TP EDL.\n3. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Realme", MarketingName = "Realme 10 / 11 4G", ModelName = "RMX3630 / RMX3780", Chipset = "Helio G99 (MT6789 MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Titik emas di dekat konektor fleksibel sub-board",
                    Instructions = "1. Lepas baterai.\n2. Hubungkan TP ke Ground.\n3. Colok USB untuk mode BROM.", IsVerified = true
                },

                // =========================================================================
                // 5. SAMSUNG
                // =========================================================================
                new TestPointInfo {
                    Brand = "Samsung", MarketingName = "Galaxy A01 / A01 Core", ModelName = "SM-A015F / SM-A013F", Chipset = "Snapdragon 439 / MT6739",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di bawah soket baterai",
                    Instructions = "1. Lepas baterai.\n2. Jumper 2 titik TP EDL.\n3. Colok USB ke PC untuk mode 9008.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Samsung", MarketingName = "Galaxy A02s", ModelName = "SM-A025F", Chipset = "Snapdragon 450 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "Di samping konektor tombol volume",
                    Instructions = "1. Lepas penutup belakang.\n2. Hubungkan 2 titik Test Point ke Ground.\n3. Colok kabel USB untuk mode Qualcomm 9008.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Samsung", MarketingName = "Galaxy A03 Core", ModelName = "SM-A032F", Chipset = "Unisoc SC9863A",
                    Type = PinoutType.SPREADTRUM_FDL, TestPointLocation = "Titik TP D+ / D- dekat konektor fleksibel charger",
                    Instructions = "1. Lepas baterai.\n2. Hubungkan Test Point ke Ground.\n3. Colok USB untuk mode Unisoc SPRD Download.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Samsung", MarketingName = "Galaxy A03 / A03s", ModelName = "SM-A035F / SM-A037F", Chipset = "Unisoc T606 / Helio P35",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Titik emas di sebelah kanan soket baterai",
                    Instructions = "1. Buka penutup mesin.\n2. Hubungkan TP ke GND.\n3. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Samsung", MarketingName = "Galaxy A04 / A04e", ModelName = "SM-A042F / SM-A045F", Chipset = "Helio P35 (MT6765 MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Sebelah kanan soket baterai",
                    Instructions = "1. Buka backdoor.\n2. Hubungkan titik Test Point ke Ground (kaleng mesin).\n3. Colok USB untuk MTK BROM Mode.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Samsung", MarketingName = "Galaxy A05 / A05s", ModelName = "SM-A055F / SM-A057F", Chipset = "Helio G85 / Snapdragon 680",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di samping soket LCD / dekat tombol power",
                    Instructions = "1. Buka backdoor.\n2. Hubungkan 2 titik TP EDL dengan pinset.\n3. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Samsung", MarketingName = "Galaxy A10s", ModelName = "SM-A107F", Chipset = "Helio P22 (MT6762 MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Di samping soket LCD / dekat konektor baterai",
                    Instructions = "1. Hubungkan TP ke Ground.\n2. Colok kabel USB ke PC untuk Erase FRP / Userdata.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Samsung", MarketingName = "Galaxy A11", ModelName = "SM-A115F", Chipset = "Snapdragon 450 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di bawah soket baterai",
                    Instructions = "1. Hubungkan 2 titik Test Point menggunakan pinset.\n2. Colokkan kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Samsung", MarketingName = "Galaxy A12", ModelName = "SM-A125F", Chipset = "Helio P35 (MT6765 MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Samping konektor fleksibel LCD",
                    Instructions = "1. Hubungkan titik Test Point ke Ground.\n2. Colok kabel USB untuk BROM mode.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Samsung", MarketingName = "Galaxy A13 4G", ModelName = "SM-A135F / SM-A137F", Chipset = "Exynos 850 / Helio G80",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Dekat konektor tombol volume & power",
                    Instructions = "1. Buka penutup mesin.\n2. Hubungkan TP ke GND.\n3. Colok USB untuk mode BROM/Download.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Samsung", MarketingName = "Galaxy A14 4G / 5G", ModelName = "SM-A145F / SM-A146P", Chipset = "Helio G80 / Dimensity 700",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Titik emas di samping soket baterai",
                    Instructions = "1. Lepas baterai.\n2. Hubungkan TP ke GND.\n3. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Samsung", MarketingName = "Galaxy A15 4G / 5G", ModelName = "SM-A155F / SM-A156B", Chipset = "Helio G99 / Dimensity 6100+",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Dekat soket fleksibel sub-board",
                    Instructions = "1. Lepas baterai.\n2. Hubungkan TP ke Ground.\n3. Colok kabel USB ke PC untuk mode BROM.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Samsung", MarketingName = "Galaxy A20s", ModelName = "SM-A207F", Chipset = "Snapdragon 450 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik Test Point dekat soket tombol power",
                    Instructions = "1. Hubungkan 2 titik TP ke Ground dengan pinset.\n2. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Samsung", MarketingName = "Galaxy A23 4G", ModelName = "SM-A235F", Chipset = "Snapdragon 680 (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di atas soket baterai",
                    Instructions = "1. Buka penutup mesin.\n2. Hubungkan 2 titik TP EDL.\n3. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Samsung", MarketingName = "Galaxy A50 / A50s / A51", ModelName = "SM-A505F / SM-A507F / SM-A515F", Chipset = "Exynos 9610 / 9611",
                    Type = PinoutType.ISP_EMMC, TestPointLocation = "Pinout ISP UFS / eMMC (CLK, CMD, DAT0, GND)",
                    Instructions = "1. Buka kaleng pelindung UFS/eMMC.\n2. Solder titik ISP untuk Direct Flashing / Unbrick.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Samsung", MarketingName = "Galaxy A52 / A52s", ModelName = "SM-A525F / SM-A528B", Chipset = "Snapdragon 720G / 778G (Qualcomm)",
                    Type = PinoutType.EDL_9008, TestPointLocation = "2 titik emas di samping soket LCD",
                    Instructions = "1. Buka backdoor kaca.\n2. Jumper 2 titik TP EDL.\n3. Colok USB untuk mode 9008.", IsVerified = true
                },

                // =========================================================================
                // 6. INFINIX / TECNO / ITEL
                // =========================================================================
                new TestPointInfo {
                    Brand = "Infinix", MarketingName = "Infinix Smart 4 / 5 / 6", ModelName = "X653 / X657 / X6511", Chipset = "Helio A22 / Unisoc SC9863A",
                    Type = PinoutType.SPREADTRUM_FDL, TestPointLocation = "Titik emas samping konektor tombol volume",
                    Instructions = "1. Lepas baterai.\n2. Hubungkan TP ke GND.\n3. Colok kabel USB untuk mode Download.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Infinix", MarketingName = "Infinix Smart 7 / 8", ModelName = "X6515 / X6525", Chipset = "Unisoc SC9863A1 / Unisoc T606",
                    Type = PinoutType.SPREADTRUM_FDL, TestPointLocation = "Dekat konektor fleksibel sub-board",
                    Instructions = "1. Buka penutup mesin.\n2. Jumper TP ke GND.\n3. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Infinix", MarketingName = "Infinix Hot 8 / 9 / 9 Play", ModelName = "X650 / X655 / X680", Chipset = "Helio P22 / A25 (MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Di bawah kaleng IC Power / dekat konektor baterai",
                    Instructions = "1. Lepas baterai.\n2. Jumper TP ke GND.\n3. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Infinix", MarketingName = "Infinix Hot 10 / 10 Play / 10s", ModelName = "X682 / X688B / X689", Chipset = "Helio G70 / G25 / G85",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Dekat soket fingerprint / samping soket LCD",
                    Instructions = "1. Buka penutup mesin.\n2. Hubungkan titik CLK Test Point ke Ground.\n3. Colokkan kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Infinix", MarketingName = "Infinix Hot 11 / 11S", ModelName = "X662 / X6812", Chipset = "Helio G70 / G88 (MT6769 MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Di samping soket fleksibel LCD",
                    Instructions = "1. Buka backdoor.\n2. Hubungkan TP ke GND.\n3. Colok USB untuk mode BROM.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Infinix", MarketingName = "Infinix Hot 12 / 12 Play / 12i", ModelName = "X6817 / X6816 / X665", Chipset = "Helio G85 / Unisoc T610 / Helio A22",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Samping soket tombol power & volume",
                    Instructions = "1. Lepas baterai.\n2. Hubungkan TP ke Ground.\n3. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Infinix", MarketingName = "Infinix Hot 20 / 20 Play / 20s", ModelName = "X6826 / X6825 / X6827", Chipset = "Helio G85 / G37 / G96",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Dekat soket kamera utama",
                    Instructions = "1. Hubungkan TP ke Ground.\n2. Colokkan kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Infinix", MarketingName = "Infinix Hot 30 / 30i / 30 Play", ModelName = "X669 / X6835", Chipset = "Helio G88 / Unisoc T606",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Di bawah soket baterai",
                    Instructions = "1. Buka penutup mesin.\n2. Jumper TP ke GND.\n3. Colok USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Infinix", MarketingName = "Infinix Hot 40 / 40 Pro", ModelName = "X6836 / X6837", Chipset = "Helio G88 / Helio G99 (MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Dekat soket fleksibel fingerprint",
                    Instructions = "1. Lepas baterai.\n2. Jumper titik Test Point ke Ground.\n3. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Infinix", MarketingName = "Infinix Note 10 / 10 Pro", ModelName = "X693 / X695", Chipset = "Helio G85 / G95 (MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Di samping soket LCD / dekat IC Power",
                    Instructions = "1. Buka penutup mesin.\n2. Hubungkan TP ke GND.\n3. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Infinix", MarketingName = "Infinix Note 11 / 12 / 12 G96", ModelName = "X663 / X670", Chipset = "Helio G88 / G96 (MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Samping soket kamera utama",
                    Instructions = "1. Hubungkan Test Point ke Ground menggunakan pinset presisi.\n2. Colokkan kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Infinix", MarketingName = "Infinix Note 30 / 30 Pro", ModelName = "X6833 / X678B", Chipset = "Helio G99 (MT6789 MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Titik emas di samping konektor baterai",
                    Instructions = "1. Lepas baterai.\n2. Hubungkan TP ke GND.\n3. Colok USB untuk mode BROM.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Infinix", MarketingName = "Infinix Note 40 / 40 Pro", ModelName = "X6853 / X6850", Chipset = "Helio G99 Ultimate / Dimensity 7020",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Dekat konektor fleksibel sub-board",
                    Instructions = "1. Buka penutup mesin.\n2. Hubungkan TP ke GND.\n3. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Infinix", MarketingName = "Infinix GT 10 Pro / GT 20 Pro", ModelName = "X6739 / X6871", Chipset = "Dimensity 8050 / 8200 (MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Titik emas di samping kanan soket kamera utama",
                    Instructions = "1. Buka backdoor.\n2. Hubungkan titik CLK Test Point ke Ground menggunakan pinset.\n3. Colokkan kabel USB ke PC hingga terdeteksi 'MediaTek USB Port'.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Tecno", MarketingName = "Tecno Spark 6 / 7 / 8 / 9", ModelName = "KE7 / KF6 / KG6", Chipset = "Helio G70 / G85 (MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Di bawah kaleng IC Power / dekat konektor baterai",
                    Instructions = "1. Lepas baterai.\n2. Hubungkan TP ke GND.\n3. Colok USB untuk mode BROM.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Tecno", MarketingName = "Tecno Spark 10 / 10 Pro / 20", ModelName = "KI5 / KI7 / KJ5", Chipset = "Helio G37 / G88 / G85",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Samping soket tombol power & volume",
                    Instructions = "1. Buka backdoor.\n2. Jumper TP ke GND.\n3. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Tecno", MarketingName = "Tecno Pova / Pova 2 / 3 / 4 / 5", ModelName = "LD7 / LE7 / LF7 / LG7", Chipset = "Helio G80 / G85 / G88 / G99",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Dekat konektor fleksibel LCD / bawah kamera",
                    Instructions = "1. Buka penutup mesin.\n2. Hubungkan TP ke GND.\n3. Colok kabel USB ke PC.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Itel", MarketingName = "Itel Vision 1 / 1 Plus / 2 / 3", ModelName = "L6005 / L6501 / S661LP", Chipset = "Unisoc SC9863A / Unisoc T606",
                    Type = PinoutType.SPREADTRUM_FDL, TestPointLocation = "Titik emas samping konektor sub-board",
                    Instructions = "1. Lepas baterai.\n2. Hubungkan TP ke GND.\n3. Colok USB untuk mode SPRD Download.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Itel", MarketingName = "Itel S23 / S23 Plus / P40 / P55", ModelName = "S665L / S681LN / P662L", Chipset = "Unisoc T606 / T616 / Dimensity 6080",
                    Type = PinoutType.SPREADTRUM_FDL, TestPointLocation = "Di samping konektor baterai / dekat tombol power",
                    Instructions = "1. Buka backdoor.\n2. Hubungkan TP ke GND.\n3. Colok kabel USB ke PC.", IsVerified = true
                },

                // =========================================================================
                // 7. ADVAN (INDONESIA TABLET & HP)
                // =========================================================================
                new TestPointInfo {
                    Brand = "Advan", MarketingName = "Advan Tab VX / Tab VX Lite", ModelName = "T618_TAB", Chipset = "Unisoc Tiger T618 (UMS512)",
                    Type = PinoutType.SPREADTRUM_FDL, TestPointLocation = "Titik emas bertanda 'KCO' samping konektor baterai",
                    Instructions = "1. Buka casing belakang tablet.\n2. Lepas konektor baterai sejenak.\n3. Hubungkan titik KCO Test Point ke Ground (GND/Kaleng).\n4. Colokkan kabel USB ke PC hingga terdeteksi 'SPRD U2S Diag / Download Port'.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Advan", MarketingName = "Advan Tab Sketsa 2 / Tab Belajar", ModelName = "T606_SKETSA / SC9863A", Chipset = "Unisoc T606 / SC9863A",
                    Type = PinoutType.SPREADTRUM_FDL, TestPointLocation = "Di bawah stiker barcode motherboard dekat slot SIM",
                    Instructions = "1. Lepas baterai.\n2. Jumper titik Test Point ke Ground.\n3. Colok USB untuk mode SPD Download & FRP Wipe.", IsVerified = true
                },

                // =========================================================================
                // 8. HUAWEI & HONOR (HISILICON KIRIN)
                // =========================================================================
                new TestPointInfo {
                    Brand = "Huawei", MarketingName = "Huawei Nova 3i / Y9 Prime 2019", ModelName = "INE-LX2 / STK-L21", Chipset = "HiSilicon Kirin 710 / 710F",
                    Type = PinoutType.KIRIN_COM1, TestPointLocation = "Titik emas tunggal di dekat fleksibel baterai / slot SIM",
                    Instructions = "1. Buka penutup mesin belakang.\n2. Lepaskan konektor baterai.\n3. Hubungkan titik Test Point ke Ground (kaleng pelindung PCB) menggunakan pinset presisi.\n4. Colokkan kabel USB ke PC.\n5. Pasang kembali soket baterai hingga terdeteksi 'HUAWEI USB COM 1.0'.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Huawei", MarketingName = "Huawei Y5 / Y6 2019 / Y6p", ModelName = "AMN-LX9 / MRD-LX1 / MED-LX9", Chipset = "Helio A22 / MT6762 (MediaTek)",
                    Type = PinoutType.MTK_BROM, TestPointLocation = "Samping soket fleksibel LCD / bawah kamera",
                    Instructions = "1. Lepas baterai.\n2. Hubungkan TP ke GND.\n3. Colok kabel USB ke PC untuk mode BROM.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Huawei", MarketingName = "Huawei P30 Lite / Nova 5T", ModelName = "MAR-LX1 / YAL-L21", Chipset = "Kirin 710 / Kirin 980 (HiSilicon)",
                    Type = PinoutType.KIRIN_COM1, TestPointLocation = "Titik emas di bawah soket fingerprint",
                    Instructions = "1. Buka backdoor kaca.\n2. Hubungkan titik TP Kirin ke Ground.\n3. Colok USB untuk mode HUAWEI USB COM 1.0.", IsVerified = true
                },
                new TestPointInfo {
                    Brand = "Huawei", MarketingName = "Honor 8X / 9X / 10 Lite", ModelName = "JSN-L22 / STK-LX1 / HRY-LX1", Chipset = "Kirin 710 / 710F",
                    Type = PinoutType.KIRIN_COM1, TestPointLocation = "Titik emas tunggal dekat konektor baterai",
                    Instructions = "1. Buka penutup mesin.\n2. Jumper TP ke GND.\n3. Colok kabel USB ke PC hingga terdeteksi COM 1.0.", IsVerified = true
                }
            };
        }
    }
}
