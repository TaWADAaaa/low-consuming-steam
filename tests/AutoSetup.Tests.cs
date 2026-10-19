using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Tawop.Tests
{
    public static class AutoSetupTests
    {
        public static void Run(string packageRoot)
        {
            string temporary = Path.GetFullPath(Path.GetTempPath());
            string root = Path.Combine(temporary, "LowConsuming-AutoSetup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                Detection(Path.Combine(root, "detection"));
                ProcessScope(Path.Combine(root, "scope"));
                Transaction(Path.Combine(root, "transaction"), Path.Combine(packageRoot, "assets"));
                Workflow(Path.Combine(root, "workflow"));
            }
            finally
            {
                string full = Path.GetFullPath(root);
                if (!Paths.IsInside(temporary, full) || !Path.GetFileName(full).StartsWith("LowConsuming-AutoSetup-", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Unsafe test cleanup target.");
                }

                Directory.Delete(full, true);
            }
        }

        private static void Detection(string root)
        {
            string parent = Path.Combine(root, "Discord");
            Directory.CreateDirectory(parent);
            File.WriteAllBytes(Path.Combine(parent, "Discord.exe"), new byte[] { 77, 90, 0, 0 });
            string older = DiscordFixture(Path.Combine(parent, "app-1.9.99"));
            string newer = DiscordFixture(Path.Combine(parent, "app-1.10.0"));
            DiscordFixture(Path.Combine(parent, "app-not-a-version"));
            string incomplete = Path.Combine(parent, "app-99.0.1");
            Directory.CreateDirectory(incomplete);
            File.WriteAllBytes(Path.Combine(incomplete, "Discord.exe"), new byte[] { 77, 90 });
            Directory.SetLastWriteTimeUtc(older, DateTime.UtcNow);
            Directory.SetLastWriteTimeUtc(newer, DateTime.UtcNow.AddDays(-30));
            Check(AutoSetup.Resolve(Product.Discord, parent) == newer, "Discord must use numeric version order, not timestamps or lexicographic order.");
            Check(AutoSetup.Resolve(Product.Discord, newer) == newer, "An actual Discord app-version folder must be accepted directly.");
            Check(AutoSetup.Resolve(Product.Discord, "\"" + parent + "\"") == newer, "A pasted quoted parent path must be accepted.");
            Check(AutoSetup.Detect(Product.Discord, new string[] { Path.Combine(root, "missing"), parent }) == newer,
                "Detection must continue from a missing candidate to a valid parent installation.");
            File.Delete(Path.Combine(newer, "Discord.exe"));
            Check(AutoSetup.Resolve(Product.Discord, parent) == older, "Incomplete updates must not be selected as installed clients.");
            File.WriteAllText(Path.Combine(newer, "Discord.exe"), "not a Windows executable");
            Check(AutoSetup.Resolve(Product.Discord, parent) == older, "A non-executable file must not qualify as Discord.exe.");
            Fail(delegate { AutoSetup.Resolve(Product.Discord, incomplete); }, "A launcher alone must not qualify as a patchable Discord client.");
            string excessive = Path.Combine(root, "excessive");
            Directory.CreateDirectory(excessive);
            for (int index = 0; index < 65; index++)
            {
                Directory.CreateDirectory(Path.Combine(excessive, "app-1.0." + index));
            }

            Fail(delegate { AutoSetup.Resolve(Product.Discord, excessive); }, "Discord version discovery must have a fixed bound.");
            string steam = SteamFixture(Path.Combine(root, "Steam"));
            Check(AutoSetup.Resolve(Product.Steam, steam) == steam, "A Steam root must be accepted.");
            File.Delete(Path.Combine(steam, "steam.exe"));
            Fail(delegate { AutoSetup.Resolve(Product.Steam, steam); }, "A directory containing only Steam UI resources is not an installed client.");
            string checkout = ChromiumFixture(Path.Combine(root, "checkout", "src"));
            Check(AutoSetup.Resolve(Product.Chrome, Path.GetDirectoryName(checkout)) == checkout, "A Chromium checkout parent may resolve its src folder.");
            Check(AutoSetup.Resolve(Product.Chrome, checkout) == checkout, "A Chromium src folder must be accepted directly.");
            string deep = Path.Combine(root, "unsearched");
            ChromiumFixture(Path.Combine(deep, "one", "two", "src"));
            Fail(delegate { AutoSetup.Resolve(Product.Chrome, deep); }, "Source discovery must not scan arbitrary descendant folders.");
        }

        private static void ProcessScope(string root)
        {
            string discord = DiscordFixture(Path.Combine(root, "Discord", "app-1.0.1"));
            string steam = SteamFixture(Path.Combine(root, "Steam"));
            Check(AutoSetupRuntime.IsSelectedExecutable(Product.Discord, discord, Path.Combine(discord, "Discord.exe")),
                "The selected Discord version executable must be in scope.");
            Check(!AutoSetupRuntime.IsSelectedExecutable(Product.Discord, discord, Path.Combine(Path.GetDirectoryName(discord), "Discord.exe")),
                "The parent Discord updater must never receive a close request.");
            Check(!AutoSetupRuntime.IsSelectedExecutable(Product.Discord, discord, Path.Combine(discord, "other", "Discord.exe")),
                "An unrelated same-named descendant executable must not receive a close request.");
            Check(AutoSetupRuntime.IsSelectedExecutable(Product.Steam, steam, Path.Combine(steam, "steam.exe")),
                "The selected Steam client executable must be in scope.");
            Check(AutoSetupRuntime.IsSelectedExecutable(Product.Steam, steam, Path.Combine(steam, "bin", "cef", "cef.win7x64", "steamwebhelper.exe")),
                "A Steam web helper in the selected client's bin subtree must be in scope.");
            Check(!AutoSetupRuntime.IsSelectedExecutable(Product.Steam, steam, Path.Combine(steam, "steamapps", "common", "game", "steam.exe")),
                "A game with the same executable name must not receive a close request.");
            Check(!AutoSetupRuntime.IsSelectedExecutable(Product.Steam, steam, Path.Combine(steam, "steamapps", "common", "game", "steamwebhelper.exe")),
                "A same-named helper outside Steam bin must not receive a close request.");
            Check(!AutoSetupRuntime.IsSelectedExecutable(Product.Steam, steam, Path.Combine(steam + "Other", "steam.exe")),
                "A similarly named sibling installation must not receive a close request.");
        }

        private static void Transaction(string root, string assets)
        {
            string steam = SteamFixture(root);
            string file = Path.Combine(steam, "steamui", "css", "sp.css");
            string before = ModCore.Hash(File.ReadAllBytes(file));
            var runtime = new FakeRuntime { WasRunning = true };
            var progress = new List<string>();
            AutoSetupResult result = AutoSetup.Run(Product.Steam, steam, assets, false, progress.Add, runtime, new AutoSetupPatches());
            Check(result.Root == steam && result.Restarted && runtime.CloseCalls == 1 && runtime.ReopenCalls == 1,
                "A running selected client must be closed after preflight and reopened after a successful patch.");
            Check(ModCore.Hash(File.ReadAllBytes(file)) != before && File.Exists(Path.Combine(steam, ".tawop", "steam.json")),
                "Automatic setup must perform a real transactional patch and preserve a recovery journal.");
            result = AutoSetup.Run(Product.Steam, steam, assets, false, progress.Add, runtime, new AutoSetupPatches());
            Check(result.AlreadyOptimized && runtime.CloseCalls == 1 && runtime.ReopenCalls == 1,
                "Clicking Optimize again must recognize verified applied bytes without closing the app or replacing the backup.");
            result = AutoSetup.Run(Product.Steam, steam, null, true, progress.Add, runtime, new AutoSetupPatches());
            Check(result.Restarted && runtime.CloseCalls == 2 && runtime.ReopenCalls == 2,
                "Undo must use the same safe close and reopen workflow.");
            Check(ModCore.Hash(File.ReadAllBytes(file)) == before && !File.Exists(Path.Combine(steam, ".tawop", "steam.json")),
                "Undo must restore the original bytes exactly without requiring patch assets or a working embedded asset cache.");
            Check(progress.Count > 4 && progress[0].StartsWith("Finding", StringComparison.Ordinal), "Progress must use the English automatic workflow.");
        }

        private static void Workflow(string root)
        {
            string steam = SteamFixture(root);
            var patches = new FakePatches();
            var runtime = new FakeRuntime { WasRunning = true };
            patches.InspectError = true;
            Fail(delegate { AutoSetup.Run(Product.Steam, steam, root, false, null, runtime, patches); },
                "Incompatible clients must fail preflight.");
            Check(runtime.CloseCalls == 0 && patches.ApplyCalls == 0, "A failed compatibility check must not close any app or apply any files.");
            patches.InspectError = false;
            patches.ApplyError = true;
            Fail(delegate { AutoSetup.Run(Product.Steam, steam, root, false, null, runtime, patches); },
                "Patch failures must be reported.");
            Check(runtime.CloseCalls == 1 && runtime.ReopenCalls == 1,
                "A client closed by setup must also be reopened after a patch failure.");
            patches = new FakePatches { ApplyFailure = new UnauthorizedAccessException("Fixture backup access denied.") };
            runtime = new FakeRuntime { WasRunning = true };
            Exception permission = Capture(delegate { AutoSetup.Run(Product.Steam, steam, root, false, null, runtime, patches); });
            Check(permission is InvalidOperationException && permission.InnerException is UnauthorizedAccessException
                && runtime.ReopenCalls == 1, "Backup access failures must preserve their cause for the GUI and reopen the previously running app.");
            patches = new FakePatches { RestoreFailure = new IOException("Fixture restore file is locked.") };
            patches.Inspection.BackupPath = "active-journal";
            runtime = new FakeRuntime { WasRunning = true };
            Exception restore = Capture(delegate { AutoSetup.Run(Product.Steam, steam, root, true, null, runtime, patches); });
            Check(restore is InvalidOperationException && restore.InnerException is IOException
                && patches.RestoreCalls == 1 && runtime.ReopenCalls == 1, "Restore failures must preserve the original exception and reopen the client.");
            runtime = new FakeRuntime { WasRunning = true, Closed = false };
            patches = new FakePatches();
            Fail(delegate { AutoSetup.Run(Product.Steam, steam, root, false, null, runtime, patches); },
                "A client that refuses graceful closure must stop setup.");
            Check(patches.ApplyCalls == 0 && runtime.ReopenCalls == 0, "A client still running must not be patched or launched a second time.");
            runtime = new FakeRuntime { WasRunning = false };
            AutoSetupResult result = AutoSetup.Run(Product.Steam, steam, root, false, null, runtime, patches);
            Check(!result.Restarted && runtime.ReopenCalls == 0, "An app that was already closed must remain closed.");
            runtime = new FakeRuntime { WasRunning = true, ReopenError = true };
            result = AutoSetup.Run(Product.Steam, steam, root, false, null, runtime, patches);
            Check(result.RestartWarning != null && !result.Restarted && result.Message.IndexOf("Open Steam normally", StringComparison.Ordinal) >= 0,
                "Successful file changes with a restart failure need an actionable English completion message.");
            patches = new FakePatches();
            runtime = new FakeRuntime();
            Fail(delegate { AutoSetup.Run(Product.Steam, steam, root, true, null, runtime, patches); },
                "Undo without a backup must fail before closing the app.");
            Check(runtime.CloseCalls == 0 && patches.RestoreCalls == 0, "Missing backups must not cause app closure or a restore attempt.");
            patches.Inspection.BackupPath = "active-journal";
            patches.Inspection.Files.Add(new PatchRecord { RelativePath = Path.Combine("steamui", "css", "sp.css"), AfterHash = new string('0', 64) });
            Fail(delegate { AutoSetup.Run(Product.Steam, steam, root, false, null, runtime, patches); },
                "An active journal with external changes must not be blindly reapplied.");
            Check(runtime.CloseCalls == 0 && patches.ApplyCalls == 0, "An unresolved backup conflict must leave the app and files alone.");
            string chromium = ChromiumFixture(Path.Combine(root, "chromium", "src"));
            patches = new FakePatches();
            runtime = new FakeRuntime { WasRunning = true };
            result = AutoSetup.Run(Product.Chrome, chromium, root, false, null, runtime, patches);
            Check(runtime.CloseCalls == 0 && runtime.ReopenCalls == 0 && result.Message.IndexOf("full Chromium build", StringComparison.Ordinal) >= 0,
                "Source preparation must never close a running browser or claim that a browser was built.");
        }

        private static string DiscordFixture(string root)
        {
            Directory.CreateDirectory(Path.Combine(root, "resources"));
            Directory.CreateDirectory(Path.Combine(root, "modules"));
            File.WriteAllBytes(Path.Combine(root, "Discord.exe"), new byte[] { 77, 90, 0, 0 });
            File.WriteAllText(Path.Combine(root, "resources", "app.asar"), "fixture");
            return root;
        }

        private static string SteamFixture(string root)
        {
            Directory.CreateDirectory(Path.Combine(root, "steamui", "css"));
            SharedFixtures.WriteSteamBundle(root);
            File.WriteAllBytes(Path.Combine(root, "steam.exe"), new byte[] { 77, 90, 0, 0 });
            File.WriteAllText(Path.Combine(root, "steamui", "css", "sp.css"), "body { color: white; }\r\n", new UTF8Encoding(false));
            return root;
        }

        private static string ChromiumFixture(string root)
        {
            string branding = Path.Combine(root, "chrome", "app", "theme", "chromium", "BRANDING");
            string prefs = Path.Combine(root, "components", "performance_manager", "user_tuning", "prefs.cc");
            Directory.CreateDirectory(Path.GetDirectoryName(branding));
            Directory.CreateDirectory(Path.GetDirectoryName(prefs));
            File.WriteAllText(branding, "fixture");
            File.WriteAllText(prefs, "fixture");
            return root;
        }

        private static void Check(bool success, string message)
        {
            if (!success)
            {
                throw new InvalidOperationException("AutoSetupTests: " + message);
            }
        }

        private static void Fail(Action action, string message)
        {
            try
            {
                action();
            }
            catch (IOException)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                return;
            }

            throw new InvalidOperationException("AutoSetupTests: " + message);
        }

        private static Exception Capture(Action action)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                return error;
            }

            throw new InvalidOperationException("AutoSetupTests: Expected an operation failure.");
        }

        private sealed class FakeRuntime : IAutoSetupRuntime
        {
            internal bool WasRunning;
            internal bool Closed = true;
            internal bool ReopenError;
            internal int CloseCalls;
            internal int ReopenCalls;

            public AutoSetupCloseResult Close(Product product, string root, Action<string> progress)
            {
                CloseCalls++;
                return new AutoSetupCloseResult { WasRunning = WasRunning, Closed = Closed };
            }

            public void Reopen(Product product, string root)
            {
                ReopenCalls++;
                if (ReopenError)
                {
                    throw new InvalidOperationException("Fixture launch failure.");
                }
            }
        }

        private sealed class FakePatches : IAutoSetupPatches
        {
            internal readonly PatchInspection Inspection = new PatchInspection();
            internal bool InspectError;
            internal bool ApplyError;
            internal Exception ApplyFailure;
            internal Exception RestoreFailure;
            internal int ApplyCalls;
            internal int RestoreCalls;

            public PatchInspection Inspect(Product product, string root, string assets)
            {
                if (InspectError)
                {
                    throw new InvalidOperationException("Unsupported fixture client.");
                }

                return Inspection;
            }

            public PatchInspection Apply(Product product, string root, string assets)
            {
                ApplyCalls++;
                if (ApplyFailure != null)
                {
                    throw ApplyFailure;
                }

                if (ApplyError)
                {
                    throw new InvalidOperationException("Fixture patch failure.");
                }

                return Inspection;
            }

            public PatchInspection Restore(Product product, string root)
            {
                RestoreCalls++;
                if (RestoreFailure != null)
                {
                    throw RestoreFailure;
                }

                return Inspection;
            }
        }
    }
}
