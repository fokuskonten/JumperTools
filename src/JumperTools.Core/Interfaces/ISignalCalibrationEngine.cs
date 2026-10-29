using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Models;

namespace JumperTools.Core.Interfaces
{
    /// <summary>
    /// Unified contract for Modem, Baseband, NVRAM, QCN, and IMEI calibration operations
    /// </summary>
    public interface ISignalCalibrationEngine
    {
        List<QcnFileInfo> ScanAvailableQcnFiles();
        List<NvramFileInfo> ScanAvailableNvramFiles();
        Task<bool> WriteQcnToDiagPortAsync(string comPort, string qcnFilePath, CancellationToken cancellationToken = default);
        Task<string?> BackupMediaTekNvramAsync(string outputDirectory, CancellationToken cancellationToken = default);
        Task<bool> RestoreMediaTekNvramAsync(string nvramBackupDirectory, CancellationToken cancellationToken = default);
        Task<bool> EnableQualcommDiagPortViaAdbAsync(string? serial = null, CancellationToken cancellationToken = default);
    }
}
