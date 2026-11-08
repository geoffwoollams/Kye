using System.Reflection;
using System.Windows;

namespace GameLauncher;

internal static class Program
{
    // ===== Per-game configuration: change these for each game/app =====
    public const string AppName = "Kye";
    public const string BaseUrl = "https://playkye.com/";
        public const bool AllowInsecureHttp = false;   // set true only for local testing
        // ==================================================================

        public static readonly string ExePath = Assembly.GetEntryAssembly()!.Location;
        public static readonly string LauncherDir = Path.GetDirectoryName(ExePath)!;

        public static readonly Version CurrentVersion =
        Manifest.ParseVersion(Assembly.GetEntryAssembly()?.GetName().Version?.ToString()) ?? new Version(1, 0, 0, 0);

        [STAThread]
        private static int Main(string[] args)
        {
            Log.Init();
            System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;

            // After a self-update the new process starts while the old one is still exiting,
            // so give it a moment to release the mutex.
            bool restarted = args.Contains("--updated");
            using var mutex = new Mutex(false, $"Local\\GameLauncher_{AppName}");
            bool owned;
            try { owned = mutex.WaitOne(restarted ? TimeSpan.FromSeconds(10) : TimeSpan.Zero); }
            catch (AbandonedMutexException) { owned = true; }
            if (!owned) return 0;

            int exitCode = 0;
            try
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.Startup += async (_, _) =>
                {
                    try { exitCode = await new LauncherRunner(args).RunAsync(); }
                    catch (Exception ex)
                    {
                        Log.Error("Fatal error", ex);
                        MessageBox.Show($"The launcher hit an unexpected error:\n\n{ex.Message}", AppName,
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                        exitCode = 1;
                    }
                    app.Shutdown();
                };
                app.Run();
            }
            finally { mutex.ReleaseMutex(); }

            return exitCode;
        }
}
