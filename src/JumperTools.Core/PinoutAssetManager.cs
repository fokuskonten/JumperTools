using System;
using System.IO;
using System.Threading.Tasks;

namespace JumperTools.Core
{
    /// <summary>
    /// Local Storage & Asset Manager for high-resolution Motherboard PCB Test Point diagrams
    /// </summary>
    public class PinoutAssetManager
    {
        private readonly string _pinoutStoragePath;

        public PinoutAssetManager(string? basePath = null)
        {
            _pinoutStoragePath = basePath ?? Path.Combine(AppPathResolver.DatabaseDirectory, "pinout_images");
            if (!Directory.Exists(_pinoutStoragePath))
            {
                Directory.CreateDirectory(_pinoutStoragePath);
            }
        }

        /// <summary>
        /// Get absolute path to local schematic image, or fallback to placeholder
        /// </summary>
        public string GetSchematicImagePath(string brand, string modelName)
        {
            var safeBrand = brand?.Replace(" ", "_") ?? "Generic";
            var safeModel = modelName?.Replace(" ", "_").Replace("/", "_") ?? "Unknown";

            var exactMatch = Path.Combine(_pinoutStoragePath, safeBrand, $"{safeModel}.jpg");
            if (File.Exists(exactMatch)) return exactMatch;

            var pngMatch = Path.Combine(_pinoutStoragePath, safeBrand, $"{safeModel}.png");
            if (File.Exists(pngMatch)) return pngMatch;

            return string.Empty;
        }
    }
}
