using System.Threading;
using System.Threading.Tasks;

namespace JumperTools.Core.Interfaces
{
    /// <summary>
    /// Unified contract for firmware flashing and recovery/FRP partition servicing across protocols
    /// </summary>
    public interface IFirmwareFlasherProtocol
    {
        Task<bool> FlashFirmwareAsync(string firmwarePath, string targetDeviceOrPort, CancellationToken cancellationToken = default);
        Task<bool> EraseFrpAsync(string targetDeviceOrPort, CancellationToken cancellationToken = default);
        Task<bool> FactoryResetAsync(string targetDeviceOrPort, CancellationToken cancellationToken = default);
    }
}
