using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Imaging;

namespace NavBR.Client.Maps;

internal static class WebRoadmapCache
{
    private static readonly object Sync = new();

    public static string CacheRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer",
        "WebCache");

    public static string? TryGetPngUrl(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return null;
        }

        try
        {
            var source = new FileInfo(sourcePath);
            var signature = string.Join(
                "|",
                source.FullName.ToUpperInvariant(),
                source.Length,
                source.LastWriteTimeUtc.Ticks);
            var hash = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(signature)))
                .ToLowerInvariant();

            var relative = Path.Combine("roadmaps", $"{hash}.png");
            var target = Path.Combine(CacheRoot, relative);

            lock (Sync)
            {
                if (!File.Exists(target))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    var temporary = target + ".tmp";

                    using (var input = File.Open(
                               source.FullName,
                               FileMode.Open,
                               FileAccess.Read,
                               FileShare.ReadWrite | FileShare.Delete))
                    {
                        var decoder = BitmapDecoder.Create(
                            input,
                            BitmapCreateOptions.PreservePixelFormat,
                            BitmapCacheOption.OnLoad);
                        if (decoder.Frames.Count == 0)
                        {
                            return null;
                        }

                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(decoder.Frames[0]));

                        using var output = File.Create(temporary);
                        encoder.Save(output);
                    }

                    File.Move(temporary, target, overwrite: true);
                }
            }

            var escaped = string.Join(
                "/",
                relative
                    .Replace('\\', '/')
                    .Split('/', StringSplitOptions.RemoveEmptyEntries)
                    .Select(Uri.EscapeDataString));

            return $"https://navbr-cache.local/{escaped}";
        }
        catch
        {
            return null;
        }
    }
}
