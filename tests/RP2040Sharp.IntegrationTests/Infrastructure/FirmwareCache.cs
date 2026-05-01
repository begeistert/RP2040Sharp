namespace RP2040Sharp.IntegrationTests.Infrastructure;

/// <summary>
/// Downloads and caches MicroPython UF2 firmware images from the official GitHub releases.
/// Firmware is stored in a local cache directory so subsequent test runs are offline-capable.
/// </summary>
public static class FirmwareCache
{
    private static readonly string CacheDir =
        Path.Combine(Path.GetTempPath(), "rp2040sharp-firmware-cache");

    /// <summary>
    /// Returns the local path to the MicroPython UF2 image for <paramref name="version"/>
    /// (e.g. "v1.21.0"), downloading it from GitHub Releases if not already cached.
    /// Returns <c>null</c> if the download fails (network unavailable, etc.).
    /// </summary>
    public static async Task<string?> GetMicroPythonAsync(string version)
    {
        Directory.CreateDirectory(CacheDir);

        var path = Path.Combine(CacheDir, $"micropython-{version}.uf2");
        if (File.Exists(path) && new FileInfo(path).Length > 0)
            return path;

        // Official MicroPython release URL for Raspberry Pi Pico
        var url = $"https://github.com/micropython/micropython/releases/download/{version}/rp2-pico-{version}.uf2";

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("RP2040Sharp-IntegrationTests/1.0");

            var bytes = await http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }
        catch
        {
            // Network unavailable or release doesn't exist — tests will be skipped
            if (File.Exists(path))
                File.Delete(path);
            return null;
        }
    }

    /// <summary>
    /// Returns the local path to the firmware if already cached, without attempting a download.
    /// Useful for offline CI environments where firmware is pre-seeded.
    /// </summary>
    public static string? GetCachedPath(string version)
    {
        var path = Path.Combine(CacheDir, $"micropython-{version}.uf2");
        return File.Exists(path) && new FileInfo(path).Length > 0 ? path : null;
    }
}
