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

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("RP2040Sharp-IntegrationTests/1.0");

            // Resolve the exact filename (includes build date) from the download index
            var url = await ResolveMicroPythonUrlAsync(http, version);
            if (url is null) return null;

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

    private static async Task<string?> ResolveMicroPythonUrlAsync(HttpClient http, string version)
    {
        // Firmware listed at https://micropython.org/download/RPI_PICO/
        // Entries look like: /resources/firmware/RPI_PICO-{date}-{version}.uf2
        var page = await http.GetStringAsync("https://micropython.org/download/RPI_PICO/");
        // Filenames are RPI_PICO-{date}-v{semver}.uf2 — keep the v prefix
        var tag = version.StartsWith('v') ? version : "v" + version;
        const string needle = "/resources/firmware/RPI_PICO-";
        var search = $"-{tag}.uf2";  // e.g. "-v1.21.0.uf2" (no trailing quote — rel slice excludes it)

        var start = page.IndexOf(needle, StringComparison.Ordinal);
        while (start >= 0)
        {
            var end = page.IndexOf('"', start + 1);
            if (end < 0) break;
            var rel = page[start..end];
            if (rel.EndsWith(search, StringComparison.OrdinalIgnoreCase))
                return "https://micropython.org" + rel;
            start = page.IndexOf(needle, start + 1, StringComparison.Ordinal);
        }
        return null;
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
