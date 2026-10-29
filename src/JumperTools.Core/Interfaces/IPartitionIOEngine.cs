using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Models;

namespace JumperTools.Core.Interfaces
{
    /// <summary>
    /// Unified contract for physical GPT / Raw partition reading, dumping, flashing, and wiping
    /// </summary>
    public interface IPartitionIOEngine
    {
        Task<List<PartitionTableItem>> ReadLivePartitionTableAsync(string? serial, string? protocolStr, string? portName, CancellationToken cancellationToken = default);
        Task<string?> ReadPartitionAsync(PartitionTableItem partition, string outputDirectory, DeviceConnectionProtocol protocol, string? deviceParam = null, CancellationToken cancellationToken = default);
        Task<bool> WritePartitionAsync(PartitionTableItem partition, string imagePath, DeviceConnectionProtocol protocol, string? deviceParam = null, CancellationToken cancellationToken = default);
        Task<bool> ErasePartitionAsync(PartitionTableItem partition, DeviceConnectionProtocol protocol, string? deviceParam = null, CancellationToken cancellationToken = default);
    }
}
