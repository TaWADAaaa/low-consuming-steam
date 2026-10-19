using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace Tawop
{
    public sealed class AutoSetupResult
    {
        public string Root;
        public bool AlreadyOptimized;
        public bool Restarted;
        public string RestartWarning;
        public string Message;
        public PatchInspection Inspection;
    }

    internal sealed class AutoSetupCloseResult
    {
        internal bool WasRunning;
        internal bool Closed;
        internal string Error;
    }

    internal interface IAutoSetupRuntime
    {
        AutoSetupCloseResult Close(Product product, string root, Action<string> progress);
        void Reopen(Product product, string root);
    }

    internal interface IAutoSetupPatches
    {
        PatchInspection Inspect(Product product, string root, string assets);
        PatchInspection Apply(Product product, string root, string assets);
        PatchInspection Restore(Product product, string root);
    }

    public static class AutoSetup
    {
        private const int MaximumVersionFolders = 64;

        public static string Detect(Product product)
        {
            return Detect(product, KnownCandidates(product));
        }

        public static string Detect(Product product, CancellationToken cancellation)
        {
            using (OperationControl.Enter(cancellation, null))
            {
                return Detect(product);
            }
        }

        internal static string Detect(Product product, IEnumerable<string> candidates)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int checkedCount = 0;
            foreach (string candidate in candidates)
            {
                OperationControl.Check();
                if (String.IsNullOrWhiteSpace(candidate) || !seen.Add(candidate))
                {
                    continue;
                }

                if (++checkedCount > 32)
                {
                    break;
                }

                try
                {
                    return Resolve(product, candidate);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (InvalidOperationException)
                {
                }
                catch (ArgumentException)
                {
                }
            }

            return null;
        }

        public static string Resolve(Product product, string selectedPath)
        {
            OperationControl.Check();
            if (String.IsNullOrWhiteSpace(selectedPath))
            {
                string detected = Detect(product);
                if (detected != null)
                {
                    return detected;
                }

                throw new DirectoryNotFoundException(product == Product.Chrome
                    ? "Chromium source files were not found. Choose the Chromium src folder. A normal Chrome installation is not a source checkout."
                    : product + " was not found automatically. Choose its installation folder, then try again.");
            }

            string root = Paths.Root(Environment.ExpandEnvironmentVariables(selectedPath.Trim().Trim('"')));
            if (IsInstallRoot(product, root))
            {
                return root;
            }

            if (product == Product.Discord)
            {
                string best = null;
                Version bestVersion = null;
                int count = 0;
                foreach (string folder in Directory.EnumerateDirectories(root, "app-*", SearchOption.TopDirectoryOnly))
                {
                    OperationControl.Check();
                    if (++count > MaximumVersionFolders)
                    {
                        throw new InvalidOperationException("This Discord folder contains too many app versions. Choose the app-version folder directly.");
                    }

                    Version version;
                    if (!TryDiscordVersion(Path.GetFileName(folder), out version))
                    {
                        continue;
                    }

                    Paths.RejectReparsePoints(folder);
                    if (IsInstallRoot(product, folder) && (bestVersion == null || version.CompareTo(bestVersion) > 0))
                    {
                        best = folder;
                        bestVersion = version;
                    }
                }

                if (best != null)
                {
                    return Paths.Root(best);
                }

                throw new DirectoryNotFoundException("No complete Discord app-version folder was found here. Choose the Discord folder in Local AppData, or its app-version folder. The small launcher in the parent folder cannot be patched.");
            }

            if (product == Product.Chrome)
            {
                foreach (string relative in new string[] { "src", Path.Combine("chromium", "src") })
                {
                    string child = Paths.Child(root, relative);
                    if (Directory.Exists(child) && IsInstallRoot(product, child))
                    {
                        return Paths.Root(child);
                    }
                }

                throw new DirectoryNotFoundException("This folder does not contain the required Chromium source files. Choose the src folder of a Chromium checkout; installed Google Chrome cannot be patched by this source tool.");
            }

            throw new DirectoryNotFoundException("This is not a complete Steam installation. Choose the folder containing steam.exe and steamui.");
        }

        public static AutoSetupResult Run(Product product, string selectedPath, string assetsPath, bool restore, Action<string> progress)
        {
            return Run(product, selectedPath, assetsPath, restore, progress, new AutoSetupRuntime(), new AutoSetupPatches());
        }

        public static AutoSetupResult Run(Product product, string selectedPath, string assetsPath, bool restore,
            Action<string> progress, CancellationToken cancellation)
        {
            using (OperationControl.Enter(cancellation, progress))
            {
                return Run(product, selectedPath, assetsPath, restore, progress);
            }
        }

        public static string Resolve(Product product, string selectedPath, CancellationToken cancellation)
        {
            using (OperationControl.Enter(cancellation, null))
            {
                return Resolve(product, selectedPath);
            }
        }

        internal static AutoSetupResult Run(Product product, string selectedPath, string assetsPath, bool restore,
            Action<string> progress, IAutoSetupRuntime runtime, IAutoSetupPatches patches)
        {
            if (runtime == null || patches == null)
            {
                throw new ArgumentNullException(runtime == null ? "runtime" : "patches");
            }

            Notify(progress, "Finding " + (product == Product.Chrome ? "Chromium source files" : product.ToString()) + "...");
            OperationControl.Check();
            string root = Resolve(product, selectedPath);
            Notify(progress, restore ? "Checking your original backup..." : "Checking compatibility before making any changes...");
            OperationControl.Check();
            PatchInspection inspection = patches.Inspect(product, root, assetsPath);
            OperationControl.Check();
            bool hasBackup = !String.IsNullOrEmpty(inspection.BackupPath);
            bool upgrade = false;
            if (restore && !hasBackup)
            {
                throw new InvalidOperationException("There are no changes to undo in this installation.");
            }

            if (!restore && hasBackup)
            {
                if (MatchesAppliedFiles(root, inspection))
                {
                    if (inspection.ProfileVersion == ModCore.CurrentProfileVersion)
                    {
                        return new AutoSetupResult
                        {
                            Root = root,
                            AlreadyOptimized = true,
                            Inspection = inspection,
                            Message = "Already optimized with the current profile. Your original backup is safe."
                        };
                    }

                    upgrade = true;
                }
                else
                {
                    throw new InvalidOperationException("This installation has an earlier backup and some files have changed. Use Undo changes before trying to optimize again. Your backup has been kept.");
                }
            }

            AutoSetupCloseResult close = new AutoSetupCloseResult { Closed = true };
            AutoSetupResult result = null;
            Exception operationError = null;
            string restartError = null;
            bool originalsRestoredForUpgrade = false;
            bool applyingUpgradedProfile = false;
            try
            {
                if (product != Product.Chrome)
                {
                    Notify(progress, "Asking " + product + " to close safely...");
                    OperationControl.Check();
                    close = runtime.Close(product, root, progress);
                    OperationControl.Check();
                    if (close == null || !close.Closed)
                    {
                        throw new InvalidOperationException(close == null || String.IsNullOrEmpty(close.Error)
                            ? "The app is still running. Quit it from the system tray, then click the main button again. No files have been changed."
                            : close.Error);
                    }
                }

                if (upgrade)
                {
                    Notify(progress, "Replacing the previous profile: restoring your original files first...");
                    OperationControl.Check();
                    patches.Restore(product, root);
                    originalsRestoredForUpgrade = true;
                    Notify(progress, "Checking the new optimization profile...");
                    OperationControl.Check();
                    inspection = patches.Inspect(product, root, assetsPath);
                    OperationControl.Check();
                }

                Notify(progress, restore ? "Restoring your original files..." : "Saving the original files and applying the lighter interface...");
                OperationControl.Check();
                applyingUpgradedProfile = upgrade;
                PatchInspection completed = restore ? patches.Restore(product, root) : patches.Apply(product, root, assetsPath);
                result = new AutoSetupResult
                {
                    Root = root,
                    Inspection = completed,
                    Message = restore ? "Your original files have been restored."
                        : product == Product.Chrome ? "Chromium source changes are ready. The browser still needs a full Chromium build."
                        : "Done. The lighter interface is installed, and your original files are backed up."
                };
            }
            catch (Exception error)
            {
                operationError = originalsRestoredForUpgrade && !applyingUpgradedProfile && !(error is OperationCanceledException)
                    ? new InvalidOperationException("Your original files were restored, but the new optimization could not be applied. " + error.Message, error)
                    : error;
            }
            finally
            {
                if (close != null && close.WasRunning && close.Closed)
                {
                    Notify(progress, "Reopening " + product + "...");
                    try
                    {
                        runtime.Reopen(product, root);
                        if (result != null)
                        {
                            result.Restarted = true;
                        }
                    }
                    catch (Exception error)
                    {
                        restartError = "The app could not be reopened automatically. Open " + product + " normally. " + error.Message;
                    }
                }
            }

            if (operationError != null)
            {
                if (operationError is OperationCanceledException)
                {
                    if (restartError != null)
                    {
                        throw new InvalidOperationException("Optimization stopped safely. " + restartError, operationError);
                    }

                    throw new OperationCanceledException("Stopped safely. Recovery backups have been kept when needed. " + (restartError ?? ""), operationError, OperationControl.Token);
                }

                if (restartError != null)
                {
                    throw new InvalidOperationException(operationError.Message + " " + restartError, operationError);
                }

                throw new InvalidOperationException(operationError.Message, operationError);
            }

            result.RestartWarning = restartError;
            if (restartError != null)
            {
                result.Message += " " + restartError;
            }

            Notify(progress, result.Message);
            return result;
        }

        private static bool MatchesAppliedFiles(string root, PatchInspection inspection)
        {
            if (inspection.Files == null || inspection.Files.Count == 0)
            {
                return false;
            }

            foreach (PatchRecord record in inspection.Files)
            {
                string file = Paths.Child(root, record.RelativePath);
                OperationControl.Check();
                if (record.Restored || !File.Exists(file) || ModCore.HashFile(file) != record.AfterHash)
                {
                    return false;
                }
            }

            return true;
        }

        private static void Notify(Action<string> progress, string message)
        {
            if (progress != null)
            {
                progress(message);
            }
        }

        private static bool IsInstallRoot(Product product, string root)
        {
            if (product == Product.Discord)
            {
                return IsWindowsExecutable(Paths.Child(root, "Discord.exe"))
                    && File.Exists(Paths.Child(root, Path.Combine("resources", "app.asar")))
                    && Directory.Exists(Paths.Child(root, "modules"));
            }

            if (product == Product.Steam)
            {
                return IsWindowsExecutable(Paths.Child(root, "steam.exe"))
                    && Directory.Exists(Paths.Child(root, Path.Combine("steamui", "css")));
            }

            return File.Exists(Paths.Child(root, Path.Combine("chrome", "app", "theme", "chromium", "BRANDING")))
                && File.Exists(Paths.Child(root, Path.Combine("components", "performance_manager", "user_tuning", "prefs.cc")));
        }

        private static bool IsWindowsExecutable(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return file.Length >= 2 && file.ReadByte() == 'M' && file.ReadByte() == 'Z';
            }
        }

        private static bool TryDiscordVersion(string folderName, out Version version)
        {
            version = null;
            return Regex.IsMatch(folderName, @"^app-[0-9]+(?:\.[0-9]+){1,3}$", RegexOptions.CultureInvariant)
                && Version.TryParse(folderName.Substring(4), out version);
        }

        private static IEnumerable<string> KnownCandidates(Product product)
        {
            if (product == Product.Discord)
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!String.IsNullOrWhiteSpace(local))
                {
                    yield return Path.Combine(local, "Discord");
                }

                yield break;
            }

            if (product == Product.Steam)
            {
                foreach (RegistryHive hive in new RegistryHive[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                {
                    foreach (RegistryView view in new RegistryView[] { RegistryView.Registry64, RegistryView.Registry32 })
                    {
                        foreach (string value in new string[] { "SteamPath", "InstallPath" })
                        {
                            string candidate = RegistryPath(hive, view, @"Software\Valve\Steam", value);
                            if (candidate != null)
                            {
                                yield return candidate;
                            }
                        }
                    }
                }

                foreach (Environment.SpecialFolder location in new Environment.SpecialFolder[]
                    { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles })
                {
                    string path = Environment.GetFolderPath(location);
                    if (!String.IsNullOrWhiteSpace(path))
                    {
                        yield return Path.Combine(path, "Steam");
                    }
                }

                yield break;
            }

            yield return Environment.GetEnvironmentVariable("CHROMIUM_SOURCE_ROOT");
            string current = Environment.CurrentDirectory;
            yield return current;
            yield return Path.Combine(current, "src");
            yield return Path.Combine(current, "chromium", "src");
            string own = AppDomain.CurrentDomain.BaseDirectory;
            yield return own;
            yield return Path.Combine(own, "src");
            yield return Path.Combine(own, "chromium", "src");
            string drive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            if (!String.IsNullOrEmpty(drive))
            {
                yield return Path.Combine(drive, "src", "chromium", "src");
            }
        }

        private static string RegistryPath(RegistryHive hive, RegistryView view, string subkey, string valueName)
        {
            try
            {
                using (RegistryKey registry = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = registry.OpenSubKey(subkey, false))
                {
                    return key == null ? null : key.GetValue(valueName) as string;
                }
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (System.Security.SecurityException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }
    }

    internal sealed class AutoSetupPatches : IAutoSetupPatches
    {
        private PatchInspection prepared;

        public PatchInspection Inspect(Product product, string root, string assets)
        {
            prepared = ModCore.Inspect(product, root, assets);
            return prepared;
        }

        public PatchInspection Apply(Product product, string root, string assets)
        {
            PatchInspection inspected = prepared;
            prepared = null;
            return ModCore.ApplyPrepared(product, root, assets, inspected);
        }

        public PatchInspection Restore(Product product, string root)
        {
            return ModCore.Restore(product, root);
        }
    }

    internal sealed class AutoSetupRuntime : IAutoSetupRuntime
    {
        public AutoSetupCloseResult Close(Product product, string root, Action<string> progress)
        {
            OperationControl.Check();
            List<Process> running = ScopedProcesses(product, root);
            bool wasRunning = running.Count > 0;
            try
            {
                foreach (Process process in running)
                {
                    if (OperationControl.Token.IsCancellationRequested)
                    {
                        break;
                    }
                    try
                    {
                        process.CloseMainWindow();
                    }
                    catch (InvalidOperationException)
                    {
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                    }
                }
            }
            finally
            {
                DisposeProcesses(running);
            }

            DateTime until = DateTime.UtcNow.AddSeconds(12);
            do
            {
                running = ScopedProcesses(product, root);
                bool finished = running.Count == 0;
                DisposeProcesses(running);
                if (finished)
                {
                    return new AutoSetupCloseResult { WasRunning = wasRunning, Closed = true };
                }

                if (OperationControl.Wait(200))
                {
                    running = ScopedProcesses(product, root);
                    finished = running.Count == 0;
                    DisposeProcesses(running);
                    return new AutoSetupCloseResult { WasRunning = wasRunning, Closed = finished };
                }
            }
            while (DateTime.UtcNow < until);

            return new AutoSetupCloseResult
            {
                WasRunning = wasRunning,
                Closed = false,
                Error = product + " is still running. Near the Windows clock, right-click its icon and choose "
                    + (product == Product.Discord ? "Quit Discord" : "Exit Steam")
                    + ", then click the main button again. No files have been changed."
            };
        }

        public void Reopen(Product product, string root)
        {
            string path = Paths.Child(root, product == Product.Discord ? "Discord.exe" : "steam.exe");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("The original app executable is missing.", path);
            }

            using (Process started = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                WorkingDirectory = root,
                UseShellExecute = true
            }))
            {
                if (started == null)
                {
                    throw new InvalidOperationException("Windows did not start the app.");
                }
            }
        }

        internal static bool IsSelectedExecutable(Product product, string root, string executable)
        {
            string full = Path.GetFullPath(executable);
            string main = Path.GetFullPath(Path.Combine(root, product == Product.Discord ? "Discord.exe" : "steam.exe"));
            if (String.Equals(full, main, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return product == Product.Steam
                && String.Equals(Path.GetFileName(full), "steamwebhelper.exe", StringComparison.OrdinalIgnoreCase)
                && Paths.IsInside(Path.GetFullPath(Path.Combine(root, "bin")), full);
        }

        private static List<Process> ScopedProcesses(Product product, string root)
        {
            var found = new List<Process>();
            int ownId;
            using (Process own = Process.GetCurrentProcess())
            {
                ownId = own.Id;
            }

            string[] names = product == Product.Discord ? new string[] { "Discord" }
                : new string[] { "steam", "steamwebhelper" };
            try
            {
                foreach (string name in names)
                {
                    foreach (Process process in Process.GetProcessesByName(name))
                    {
                        bool keep = false;
                        try
                        {
                            if (process.Id != ownId && !process.HasExited)
                            {
                                string executable = Path.GetFullPath(process.MainModule.FileName);
                                keep = IsSelectedExecutable(product, root, executable);
                            }
                        }
                        catch (InvalidOperationException)
                        {
                        }
                        catch (System.ComponentModel.Win32Exception)
                        {
                        }
                        finally
                        {
                            if (!keep)
                            {
                                process.Dispose();
                            }
                        }

                        if (keep)
                        {
                            found.Add(process);
                        }
                    }
                }

                return found;
            }
            catch
            {
                DisposeProcesses(found);
                throw;
            }
        }

        private static void DisposeProcesses(List<Process> processes)
        {
            foreach (Process process in processes)
            {
                process.Dispose();
            }
        }
    }
}
