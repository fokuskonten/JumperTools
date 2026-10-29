using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Models;

namespace JumperTools.Core.Interfaces
{
    /// <summary>
    /// Unified contract for custom recovery scanning, flashing, and temporary booting
    /// </summary>
    public interface IRecoveryInstallerEngine
    {
        List<RecoveryItem> ScanAvailableRecoveries();
        Task<bool> InstallRecoveryViaFastbootAsync(string imagePath, string targetPartition, bool disableAvbVerity, string? serial = null, CancellationToken cancellationToken = default);
        Task<bool> TemporaryBootRecoveryAsync(string imagePath, string? serial = null, CancellationToken cancellationToken = default);
    }
}
