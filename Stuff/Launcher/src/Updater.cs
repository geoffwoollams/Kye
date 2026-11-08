using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace GameLauncher;

internal static class Updater
{
    private const int MaxAttempts = 3;
    private const string BackupPrefix = ".update-backup-";
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(30);

    public static void RequireSecure(Uri url)
    {
        if (url.Scheme != Uri.UriSchemeHttps && !(Program.AllowInsecureHttp && url.Scheme == Uri.UriSchemeHttp))
            throw new InvalidOperationException($"Refusing non-HTTPS URL: {url}");
    }

    /// <summary>Downloads to a temp file with retries, a stall timeout and SHA-256 verification.</summary>
    public static async Task<string> DownloadAsync(HttpClient http, Uri url, string? expectedSha256,
                                                   string targetDir, Action<long, long?, double> report, CancellationToken ct)
    {
        RequireSecure(url);
        string temp = Path.Combine(Path.GetTempPath(), $"{Program.AppName}-{Guid.NewGuid():N}.tmp");

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();
                long? total = resp.Content.Headers.ContentLength;
                EnsureDiskSpace(targetDir, total);

                string actual;
                using (var sha = SHA256.Create())
                using (var src = await resp.Content.ReadAsStreamAsync())
                using (var dst = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920];
                    long done = 0, lastTick = 0;
                    var sw = Stopwatch.StartNew();

                    while (true)
                    {
                        int n = await ReadWithStallTimeoutAsync(src, buffer, ct);
                        if (n == 0) break;

                        await dst.WriteAsync(buffer, 0, n, ct);
                        sha.TransformBlock(buffer, 0, n, null, 0);
                        done += n;

                        if (sw.ElapsedMilliseconds - lastTick >= 100)
                        {
                            lastTick = sw.ElapsedMilliseconds;
                            report(done, total, done / Math.Max(sw.Elapsed.TotalSeconds, 0.001));
                        }
                    }
                    report(done, total, done / Math.Max(sw.Elapsed.TotalSeconds, 0.001));

                    sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    actual = BitConverter.ToString(sha.Hash!).Replace("-", "");
                }

                if (expectedSha256 != null)
                {
                    if (!actual.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Download failed its integrity check (SHA-256 mismatch).");
                }
                else Log.Info($"WARNING: no SHA-256 supplied for {url}; skipping verification.");

                return temp;
            }
            catch (Exception ex) when (attempt < MaxAttempts && !ct.IsCancellationRequested)
            {
                Log.Info($"Download attempt {attempt} failed: {ex.Message}");
                TryDelete(temp);
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
            }
            catch
            {
                TryDelete(temp);
                throw;
            }
        }
    }

    // .NET Framework network streams ignore cancellation tokens, so enforce the stall timeout manually.
    private static async Task<int> ReadWithStallTimeoutAsync(Stream src, byte[] buffer, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var read = src.ReadAsync(buffer, 0, buffer.Length, cts.Token);
        var timeout = Task.Delay(StallTimeout, cts.Token);

        if (await Task.WhenAny(read, timeout) == read)
        {
            cts.Cancel();   // stops the timer
            return await read;
        }

        ct.ThrowIfCancellationRequested();
        throw new TimeoutException("Download stalled.");
    }

    /// <summary>
    /// Extracts a zip into destDir. Existing files are moved into a backup folder first and restored
    /// if anything fails, so a failed update never leaves a half-installed app.
    /// Files under a preserved path are never overwritten if they already exist.
    /// </summary>
    public static void InstallZip(string zipPath, string destDir, IReadOnlyCollection<string> preserve, IProgress<double>? progress)
    {
        destDir = Path.GetFullPath(destDir);
        Directory.CreateDirectory(destDir);
        string rootPrefix = destDir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string backupDir = Path.Combine(destDir, BackupPrefix + Guid.NewGuid().ToString("N"));

        using var zip = ZipFile.OpenRead(zipPath);

        // Validate every entry before touching anything (zip-slip protection).
        var plan = new List<(ZipArchiveEntry Entry, string Target, string Rel)>();
        foreach (var e in zip.Entries)
        {
            if (string.IsNullOrEmpty(e.Name)) continue;   // directory entry
            string target = Path.GetFullPath(Path.Combine(destDir, e.FullName));
            if (!target.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Unsafe path in zip: {e.FullName}");
            plan.Add((e, target, e.FullName.Replace('\\', '/')));
        }

        var written = new List<string>();
        var moved = new List<(string Original, string Backup)>();
        try
        {
            for (int i = 0; i < plan.Count; i++)
            {
                var (entry, target, rel) = plan[i];
                bool exists = File.Exists(target);

                if (exists && IsPreserved(rel, preserve)) { progress?.Report((i + 1.0) / plan.Count); continue; }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (exists)
                {
                    string bak = Path.Combine(backupDir, target.Substring(rootPrefix.Length));
                    Directory.CreateDirectory(Path.GetDirectoryName(bak)!);
                    File.Move(target, bak);   // works even for the running launcher exe
                    moved.Add((target, bak));
                }

                entry.ExtractToFile(target, false);
                written.Add(target);
                progress?.Report((i + 1.0) / plan.Count);
            }
        }
        catch
        {
            foreach (var w in written) TryDelete(w);
            for (int i = moved.Count - 1; i >= 0; i--)
            {
                try
                {
                    TryDelete(moved[i].Original);
                    File.Move(moved[i].Backup, moved[i].Original);
                }
                catch (Exception ex) { Log.Error($"Rollback failed for {moved[i].Original}", ex); }
            }
            try { if (Directory.Exists(backupDir)) Directory.Delete(backupDir, true); } catch { }
            throw;
        }

        // Success. If this was the launcher, its old exe is still running and can't be deleted yet;
        // CleanupLeftovers() removes it on the next start.
        try { Directory.Delete(backupDir, true); } catch { }
    }

    public static void CleanupLeftovers(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return;
            foreach (var d in Directory.GetDirectories(dir, BackupPrefix + "*"))
            {
                try { Directory.Delete(d, true); } catch { }
            }
        }
        catch { }
    }

    public static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static bool IsPreserved(string rel, IReadOnlyCollection<string> preserve) =>
    preserve.Any(p => rel.Equals(p, StringComparison.OrdinalIgnoreCase) ||
    rel.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase));

    private static void EnsureDiskSpace(string dir, long? zipSize)
    {
        if (zipSize is not { } size) return;
        try
        {
            string? root = Path.GetPathRoot(Path.GetFullPath(dir));
            if (string.IsNullOrEmpty(root)) return;
            long free = new DriveInfo(root).AvailableFreeSpace;
            if (free < size * 3)
                throw new IOException($"Not enough disk space: about {Fmt.Bytes(size * 3)} needed, {Fmt.Bytes(free)} free.");
        }
        catch (ArgumentException) { /* UNC or unusual path: skip the check */ }
    }
}
