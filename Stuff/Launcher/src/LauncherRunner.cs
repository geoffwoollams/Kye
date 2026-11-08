using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;

namespace GameLauncher;

internal sealed class LauncherRunner
{
    private static readonly TimeSpan ManifestTimeout = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan LogoTimeout = TimeSpan.FromSeconds(5);

    private readonly string[] _args;
    private readonly IniFile _ini;
    private readonly HttpClient _http;
    private readonly Uri _baseUri;
    private readonly string _installDir;
    private LauncherWindow? _window;

    public LauncherRunner(string[] args)
    {
        _args = args;
        _ini = new IniFile(Path.Combine(Program.LauncherDir, "settings.ini"));
        _baseUri = new Uri(Program.BaseUrl.TrimEnd('/') + "/");

        string dir = _ini.Get("App", "InstallDir") is { Length: > 0 } d ? d : "app";
        _installDir = Path.GetFullPath(Path.Combine(Program.LauncherDir, Environment.ExpandEnvironmentVariables(dir)));

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"{Program.AppName}Launcher/{Program.CurrentVersion}");
    }

    public async Task<int> RunAsync()
    {
        Updater.CleanupLeftovers(Program.LauncherDir);
        Updater.CleanupLeftovers(_installDir);
        try
        {
            return await RunCoreAsync();
        }
        catch (OperationCanceledException) when (_window?.Cancel.IsCancellationRequested == true)
        {
            Log.Info("Cancelled by user.");
            return 0;
        }
        finally
        {
            _window?.Close();
            _ini.Save();
        }
    }

    private async Task<int> RunCoreAsync()
    {
        // 1. Fetch the manifest. If we can't, launch whatever is installed straight away.
        Manifest? m;
        while ((m = await FetchManifestAsync()) == null)
        {
            if (CanLaunch()) return Launch();

            ShowWindow(null, Array.Empty<string>());
            if (!await _window!.ShowErrorAsync(
                $"Couldn't reach the server, and {Program.AppName} isn't installed yet.\nCheck your connection or firewall.")) return 1;
            _window.SetStatus("Connecting...");
        }

        // 2. Decide what (if anything) needs updating.
        Version? installed = Manifest.ParseVersion(_ini.Get("App", "InstalledVersion"));
        string? exeRel = m.AppExecutable ?? _ini.Get("App", "Executable");
        bool restarted = _args.Contains("--updated");   // guards against update loops

        bool launcherUpdate = !restarted && m.LauncherUrl != null &&
        m.LauncherVersion is { } lv && lv > Program.CurrentVersion;
        bool appUpdate = m.AppUrl != null && m.AppVersion is { } av &&
        (installed == null || av > installed || !ExeExists(exeRel));

        // 3. Everything current: no window, just go.
        if (!launcherUpdate && !appUpdate)
        {
            SyncSettings(m);
            _ini.Save();
            return Launch();
        }

        // 4. Download the logo first, then show the window, then update.
        var logo = await LoadLogoAsync(m.LogoUrl);
        ShowWindow(logo, m.News);
        var window = _window!;
        var ct = window.Cancel.Token;

        if (launcherUpdate && await TryUpdateLauncherAsync(m, ct))
            return 0;   // new launcher is starting; this process exits

            while (appUpdate)
            {
                try
                {
                    await InstallAppAsync(m, installed == null, ct);
                    break;
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    Log.Error("App update failed", ex);
                    if (CanLaunch())
                    {
                        window.SetStatus("Update failed - starting the installed version...");
                        window.SetProgress(null);
                        await Task.Delay(2000, ct);
                        return Launch();
                    }
                    if (!await window.ShowErrorAsync($"Couldn't install {Program.AppName}:\n{ex.Message}")) return 1;
                }
            }

            if (!appUpdate) SyncSettings(m);
            _ini.Save();

        window.SetStatus($"Starting {Program.AppName}...");
        window.SetProgress(null);
        return Launch();
    }

    // ---------- Updates ----------

    private async Task<bool> TryUpdateLauncherAsync(Manifest m, CancellationToken ct)
    {
        var window = _window!;
        try
        {
            window.SetStatus("Updating launcher...");
            string zip = await Updater.DownloadAsync(_http, m.LauncherUrl!, m.LauncherSha256,
                                                     Program.LauncherDir, ReportDownload, ct);
            try
            {
                window.SetProgress(null, "");
                string[] keep = { "settings.ini", "launcher.log" };
                await Task.Run(() => Updater.InstallZip(zip, Program.LauncherDir, keep, null), ct);
            }
            finally { Updater.TryDelete(zip); }

            Log.Info($"Launcher updated to {m.LauncherVersion}; restarting.");
            Process.Start(new ProcessStartInfo(Program.ExePath, "--updated")
            {
                UseShellExecute = true,
                WorkingDirectory = Program.LauncherDir
            });
            return true;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Not fatal: carry on with the current launcher and try again next time.
            Log.Error("Launcher update failed", ex);
            return false;
        }
    }

    private async Task InstallAppAsync(Manifest m, bool firstInstall, CancellationToken ct)
    {
        var window = _window!;
        window.SetStatus(firstInstall ? $"Downloading {Program.AppName}..." : "Downloading update...");
        window.SetProgress(null);

        string zip = await Updater.DownloadAsync(_http, m.AppUrl!, m.AppSha256, _installDir, ReportDownload, ct);
        try
        {
            window.SetStatus(firstInstall ? $"Installing {Program.AppName}..." : "Installing update...");
            window.SetProgress(0);
            var progress = new Progress<double>(f => window.SetProgress(f));
            await Task.Run(() => Updater.InstallZip(zip, _installDir, m.Preserve, progress), ct);
        }
        finally { Updater.TryDelete(zip); }

        _ini.Set("App", "InstalledVersion", m.AppVersion!.ToString(3));
        SyncSettings(m);
        _ini.Save();
        Log.Info($"Installed {Program.AppName} {m.AppVersion}.");
    }

    private void ReportDownload(long done, long? total, double speed)
    {
        string detail = total is { } t
        ? $"{Fmt.Bytes(done)} / {Fmt.Bytes(t)}  •  {Fmt.Bytes((long)speed)}/s"
        : $"{Fmt.Bytes(done)}  •  {Fmt.Bytes((long)speed)}/s";
        double? fraction = total is { } t2 && t2 > 0 ? (double)done / t2 : null;
        _window?.SetProgress(fraction, detail);
    }

    // ---------- Manifest / logo ----------

    private async Task<Manifest?> FetchManifestAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(ManifestTimeout);
            var uri = new Uri(_baseUri, $"launcher.xml?t={DateTime.UtcNow.Ticks}");
            Updater.RequireSecure(uri);

            var work = DownloadManifestAsync(uri, cts.Token);
            if (await Task.WhenAny(work, Task.Delay(ManifestTimeout + TimeSpan.FromSeconds(1))) != work)
                throw new TimeoutException("Timed out contacting the server.");
            return await work;
        }
        catch (Exception ex)
        {
            Log.Info($"Manifest unavailable ({ex.GetType().Name}: {ex.Message}); using saved settings.");
            return null;
        }
    }

    private async Task<Manifest> DownloadManifestAsync(Uri uri, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        req.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return Manifest.Parse(await resp.Content.ReadAsStringAsync(), _baseUri);
    }

    private async Task<BitmapImage?> LoadLogoAsync(Uri? url)
    {
        if (url == null) return null;
        try
        {
            Updater.RequireSecure(url);
            using var cts = new CancellationTokenSource(LogoTimeout);
            using var resp = await _http.GetAsync(url, cts.Token);
            resp.EnsureSuccessStatusCode();
            byte[] bytes = await resp.Content.ReadAsByteArrayAsync();

            using var ms = new MemoryStream(bytes);
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = ms;
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch (Exception ex)
        {
            Log.Info($"Logo unavailable: {ex.Message}");
            return null;
        }
    }

    private void ShowWindow(BitmapImage? logo, IReadOnlyList<string> news)
    {
        if (_window != null) return;
        _window = new LauncherWindow(Program.AppName, logo, news);
        _window.Show();
    }

    // ---------- Settings / launching ----------

    /// <summary>Keeps executable path and arguments in settings.ini in step with the manifest.</summary>
    private void SyncSettings(Manifest m)
    {
        if (m.AppExecutable != null) _ini.Set("App", "Executable", m.AppExecutable);
        if (m.AppArguments != null) _ini.Set("App", "Arguments", m.AppArguments);
    }

    private string? ResolveExe(string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative)) return null;
        string root = Path.GetFullPath(_installDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full : null;   // must stay inside install dir
    }

    private bool ExeExists(string? relative) => ResolveExe(relative) is { } p && File.Exists(p);

    private bool CanLaunch() => ExeExists(_ini.Get("App", "Executable"));

    private int Launch()
    {
        string? setting = _ini.Get("App", "Executable");
        try
        {
            string? exe = ResolveExe(setting);
            if (exe == null || !File.Exists(exe))
                throw new FileNotFoundException($"Executable not found: {setting ?? "(not set)"}");

            var psi = new ProcessStartInfo(exe)
            {
                WorkingDirectory = Path.GetDirectoryName(exe)!,
                UseShellExecute = true
            };
            if (_ini.Get("App", "Arguments") is { Length: > 0 } args) psi.Arguments = args;

            Log.Info($"Launching {exe}");
            Process.Start(psi);
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error("Launch failed", ex);
            MessageBox.Show($"Couldn't start {Program.AppName}.\n\n{ex.Message}", Program.AppName,
                            MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
    }
}
