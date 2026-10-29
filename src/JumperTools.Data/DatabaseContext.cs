using SQLite;
using JumperTools.Models;

namespace JumperTools.Data
{
    /// <summary>
    /// Local SQLite database context for offline caching and logging
    /// </summary>
    public class DatabaseContext : IDisposable
    {
        private readonly string _databasePath;
        private SQLiteConnection? _connection;

        public DatabaseContext(string databasePath)
        {
            _databasePath = databasePath;
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            _connection = new SQLiteConnection(_databasePath);
            
            // Enforce Enterprise WAL Mode & High-Performance Pragmas
            try
            {
                _connection.Execute("PRAGMA journal_mode = WAL;");
                _connection.Execute("PRAGMA synchronous = NORMAL;");
                _connection.Execute("PRAGMA foreign_keys = ON;");
                _connection.Execute("PRAGMA busy_timeout = 5000;");
            }
            catch { }

            _connection.CreateTable<Device>();
            _connection.CreateTable<Operation>();
            _connection.CreateTable<RomMetadata>();
            _connection.CreateTable<License>();
            _connection.CreateTable<DeviceCompatibility>();
            _connection.CreateTable<OperationLog>();
            _connection.CreateTable<TestPointInfo>();
            _connection.CreateTable<JobJournalRecord>();
            _connection.CreateTable<BackupManifestRecord>();
            _connection.CreateTable<JobOrderInfo>();

            // Create High-Performance Query Indices
            try
            {
                _connection.Execute("CREATE INDEX IF NOT EXISTS idx_device_status ON Device(Status);");
                _connection.Execute("CREATE INDEX IF NOT EXISTS idx_op_device ON Operation(DeviceId);");
                _connection.Execute("CREATE INDEX IF NOT EXISTS idx_op_time ON Operation(StartTime);");
                _connection.Execute("CREATE INDEX IF NOT EXISTS idx_oplog_device ON OperationLog(DeviceId);");
                _connection.Execute("CREATE INDEX IF NOT EXISTS idx_tp_brand ON TestPointInfo(Brand);");
                _connection.Execute("CREATE INDEX IF NOT EXISTS idx_tp_model ON TestPointInfo(ModelName);");
                _connection.Execute("CREATE INDEX IF NOT EXISTS idx_job_start ON JobJournalRecord(StartTime);");
                _connection.Execute("CREATE INDEX IF NOT EXISTS idx_job_interrupted ON JobJournalRecord(IsInterrupted);");
                _connection.Execute("CREATE INDEX IF NOT EXISTS idx_joborder_created ON JobOrderInfo(CreatedAt);");
            }
            catch { }
        }

        public SQLiteConnection Connection => _connection ?? throw new InvalidOperationException("Database not initialized");

        // Device operations
        public void SaveDevice(Device device)
        {
            Connection.InsertOrReplace(device);
        }

        public Device? GetDevice(string deviceId)
        {
            return Connection.Table<Device>().FirstOrDefault(d => d.DeviceId == deviceId);
        }

        public System.Collections.Generic.List<Device> GetAllDevices()
        {
            return Connection.Table<Device>().ToList();
        }

        public void DeleteDevice(string deviceId)
        {
            Connection.Delete<Device>(deviceId);
        }

        // Operation operations
        public void SaveOperation(Operation operation)
        {
            Connection.InsertOrReplace(operation);
        }

        public Operation? GetOperation(string operationId)
        {
            return Connection.Table<Operation>().FirstOrDefault(o => o.Id == operationId);
        }

        public System.Collections.Generic.List<Operation> GetOperationsByDevice(string deviceId)
        {
            return Connection.Table<Operation>().Where(o => o.DeviceId == deviceId).ToList();
        }

        public System.Collections.Generic.List<Operation> GetRecentOperations(int count = 50)
        {
            return Connection.Table<Operation>().OrderByDescending(o => o.StartTime).Take(count).ToList();
        }

        // ROM metadata cache
        public void SaveRomMetadata(RomMetadata rom)
        {
            Connection.InsertOrReplace(rom);
        }

        public RomMetadata? GetRomMetadata(string romId)
        {
            return Connection.Table<RomMetadata>().FirstOrDefault(r => r.Id == romId);
        }

        public System.Collections.Generic.List<RomMetadata> GetRomsByModel(string model)
        {
            return Connection.Table<RomMetadata>().Where(r => r.Model == model).ToList();
        }

        // License cache
        public void SaveLicense(License license)
        {
            Connection.InsertOrReplace(license);
        }

        public License? GetLicense(string licenseId)
        {
            return Connection.Table<License>().FirstOrDefault(l => l.Id == licenseId);
        }

        public License? GetLicenseByDevice(string deviceId)
        {
            return Connection.Table<License>().FirstOrDefault(l => l.DeviceId == deviceId);
        }

        // Device compatibility cache
        public void SaveDeviceCompatibility(DeviceCompatibility compatibility)
        {
            Connection.InsertOrReplace(compatibility);
        }

        public DeviceCompatibility? GetDeviceCompatibility(string model)
        {
            return Connection.Table<DeviceCompatibility>().FirstOrDefault(c => c.Model == model);
        }

        // Operation logging
        public void LogOperation(OperationLog log)
        {
            Connection.Insert(log);
        }

        public System.Collections.Generic.List<OperationLog> GetOperationLogs(string deviceId, int limit = 100)
        {
            return Connection.Table<OperationLog>()
                .Where(l => l.DeviceId == deviceId)
                .OrderByDescending(l => l.Timestamp)
                .Take(limit)
                .ToList();
        }

        // TestPoint operations
        public void SaveTestPoint(TestPointInfo tp)
        {
            Connection.InsertOrReplace(tp);
        }

        public System.Collections.Generic.List<TestPointInfo> GetAllTestPoints()
        {
            return Connection.Table<TestPointInfo>().ToList();
        }

        public System.Collections.Generic.List<TestPointInfo> GetTestPointsByBrand(string brand)
        {
            return Connection.Table<TestPointInfo>().Where(t => t.Brand == brand).ToList();
        }

        public System.Collections.Generic.List<TestPointInfo> SearchTestPoints(string query)
        {
            var q = query.ToLower();
            return Connection.Table<TestPointInfo>().ToList()
                .Where(t => t.MarketingName.ToLower().Contains(q) || t.ModelName.ToLower().Contains(q) || t.Chipset.ToLower().Contains(q) || t.Brand.ToLower().Contains(q))
                .ToList();
        }

        // Job Journal operations
        public void SaveJobJournal(JobJournalRecord journal)
        {
            Connection.InsertOrReplace(journal);
        }

        public JobJournalRecord? GetJobJournal(string jobId)
        {
            return Connection.Table<JobJournalRecord>().FirstOrDefault(j => j.JobId == jobId);
        }

        public System.Collections.Generic.List<JobJournalRecord> GetRecentJobJournals(int count = 50)
        {
            return Connection.Table<JobJournalRecord>().OrderByDescending(j => j.StartTime).Take(count).ToList();
        }

        public System.Collections.Generic.List<JobJournalRecord> GetInterruptedJobs()
        {
            return Connection.Table<JobJournalRecord>().Where(j => j.IsInterrupted).OrderByDescending(j => j.StartTime).ToList();
        }

        // Backup Manifest operations
        public void SaveBackupManifest(BackupManifestRecord manifest)
        {
            Connection.InsertOrReplace(manifest);
        }

        // Job Order operations
        public void SaveJobOrder(JobOrderInfo job)
        {
            Connection.InsertOrReplace(job);
        }

        public JobOrderInfo? GetJobOrder(string jobId)
        {
            return Connection.Table<JobOrderInfo>().FirstOrDefault(j => j.JobId == jobId);
        }

        public System.Collections.Generic.List<JobOrderInfo> GetAllJobOrders()
        {
            return Connection.Table<JobOrderInfo>().OrderByDescending(j => j.CreatedAt).ToList();
        }

        public void DeleteJobOrder(string jobId)
        {
            Connection.Delete<JobOrderInfo>(jobId);
        }

        public void Dispose()
        {
            _connection?.Close();
            _connection?.Dispose();
            _connection = null;
        }
    }

    public class DeviceCompatibility
    {
        [PrimaryKey]
        public string Model { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string SupportedOperations { get; set; } = string.Empty; // JSON
        public string KnownIssues { get; set; } = string.Empty; // JSON
        public string SuccessRate { get; set; } = string.Empty; // JSON
        public DateTime LastUpdated { get; set; }
    }

    public class OperationLog
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string OperationType { get; set; } = string.Empty;
        public string ToolUsed { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int DurationMs { get; set; }
        public DateTime Timestamp { get; set; }
        public string DeviceFingerprint { get; set; } = string.Empty; // JSON
    }
}
