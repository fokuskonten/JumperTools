using System;
using System.Threading;
using System.Threading.Tasks;
using JumperTools.Models;

namespace JumperTools.Core.Interfaces
{
    /// <summary>
    /// Unified contract for hardware protocol communication engines (Fastboot, EDL, BROM, SPD, Kirin, Apple, Samsung AT)
    /// </summary>
    public interface IHardwareProtocolEngine
    {
        string ProtocolName { get; }
        DeviceMode SupportedMode { get; }

        event Action<string>? OnLog;
        event Action<string>? OnUserActionRequired;

        Task<bool> ProbeDeviceAsync(string? deviceIdOrPort, CancellationToken cancellationToken = default);
        Task<bool> RebootDeviceAsync(string? deviceIdOrPort, string targetMode = "system", CancellationToken cancellationToken = default);
    }
}
