using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace Tawop.Tests
{
    public static class CancellationTests
    {
        public static void Run(string packageRoot)
        {
            string assets = Path.Combine(packageRoot, "assets");
            PreCancelled();
            BoundedHash();
            using (Fixture fixture = new Fixture())
            using (var stop = new CancellationTokenSource())
            {
                Cancelled(delegate
                {
                    ModCore.Apply(Product.Steam, fixture.Root, assets, stop.Token, delegate(string message)
                    {
                        if (message.StartsWith("Saving backup", StringComparison.Ordinal)) stop.Cancel();
                    });
                });
                fixture.AssertOriginals();
                Check(!File.Exists(fixture.JournalPath), "Stopping before commits must not activate a backup journal.");
                Check(Directory.GetDirectories(Path.Combine(fixture.Root, ".tawop")).Length == 0,
                    "Stopping during backup preparation must remove its uncommitted backup directory.");
            }

            using (Fixture fixture = new Fixture())
            using (var stop = new CancellationTokenSource())
            {
                bool firstChanged = false;
                bool recoveryReported = false;
                Cancelled(delegate
                {
                    ModCore.Apply(Product.Steam, fixture.Root, assets, stop.Token, delegate(string message)
                    {
                        if (message.StartsWith("Applying file 2 ", StringComparison.Ordinal))
                        {
                            firstChanged = !fixture.OriginalsMatch();
                            stop.Cancel();
                        }

                        if (message.StartsWith("Restoring originals before stopping", StringComparison.Ordinal)) recoveryReported = true;
                    });
                });
                Check(firstChanged && recoveryReported, "Mid-apply cancellation must exercise a real committed file and report recovery.");
                fixture.AssertOriginals();
                Check(!File.Exists(fixture.JournalPath), "Verified cancellation rollback must retire the active journal.");
            }

            RestoreStopsBetweenFiles(assets);
            PreparedPlanReused(assets);
            ClosedAppReopensOnStop(assets);
            Upgrade(assets);
        }

        private static void PreCancelled()
        {
            using (var stop = new CancellationTokenSource())
            {
                stop.Cancel();
                Cancelled(delegate { ModCore.Hash(new byte[1], stop.Token); });
                Cancelled(delegate { ModCore.ReadFile("missing-fixture", stop.Token); });
                Cancelled(delegate { AsarArchive.Read(new byte[0], stop.Token); });
                Cancelled(delegate { AutoSetup.Detect(Product.Discord, stop.Token); });
                Cancelled(delegate { AssetStore.ResolveDirectory("missing-fixture", stop.Token); });
            }

            Check(ModCore.Hash(new byte[0]).Length == 64, "A pre-cancelled scope must not leak into the next operation on its worker thread.");
        }

        private static void BoundedHash()
        {
            using (var stop = new CancellationTokenSource())
            using (var input = new StopAfterReadStream(new byte[4 * 65536], stop))
            using (OperationControl.Enter(stop.Token, null))
            {
                Cancelled(delegate { ModCore.HashStream(input); });
                Check(input.ReadCalls == 2 && input.Position == 2 * 65536 && input.Position < input.Length,
                    "Streaming hashes must stop after a deterministic mid-read cancellation instead of reading or hashing the remaining blocks.");
            }
        }

        private static void RestoreStopsBetweenFiles(string assets)
        {
            using (Fixture fixture = new Fixture())
            using (var stop = new CancellationTokenSource())
            {
                PatchInspection applied = ModCore.Apply(Product.Steam, fixture.Root, assets);
                int restoring = 0;
                Cancelled(delegate
                {
                    ModCore.Restore(Product.Steam, fixture.Root, stop.Token, delegate(string message)
                    {
                        if (message.StartsWith("Restoring ", StringComparison.Ordinal) && ++restoring == 2) stop.Cancel();
                    });
                });
                PatchJournal journal = fixture.Journal();
                Check(journal.Files[0].Restored && !journal.Files[1].Restored, "Restore must stop between atomic files with its latest completed file recorded.");
                Check(ModCore.HashFile(Path.Combine(fixture.Root, journal.Files[0].RelativePath)) == journal.Files[0].BeforeHash,
                    "The completed restore file must contain verified original bytes.");
                Check(ModCore.HashFile(Path.Combine(fixture.Root, journal.Files[1].RelativePath)) == applied.Files[1].AfterHash,
                    "The next restore file must remain at its previous complete version.");
                ModCore.Restore(Product.Steam, fixture.Root);
                fixture.AssertOriginals();
                Check(!File.Exists(fixture.JournalPath), "Undo must resume a stopped restoration and retire its journal.");
            }
        }

        private static void PreparedPlanReused(string assets)
        {
            using (Fixture fixture = new Fixture())
            {
                string privateAssets = Path.Combine(fixture.Root, "own-assets", "steam");
                Directory.CreateDirectory(privateAssets);
                string theme = Path.Combine(privateAssets, "tawop.css");
                File.Copy(Path.Combine(assets, "steam", "tawop.css"), theme);
                var patches = new AutoSetupPatches();
                patches.Inspect(Product.Steam, fixture.Root, Path.GetDirectoryName(privateAssets));
                File.Delete(theme);
                patches.Apply(Product.Steam, fixture.Root, Path.GetDirectoryName(privateAssets));
                ModCore.Restore(Product.Steam, fixture.Root);
                fixture.AssertOriginals();
            }

            using (Fixture fixture = new Fixture())
            {
                var patches = new AutoSetupPatches();
                patches.Inspect(Product.Steam, fixture.Root, assets);
                string target = Path.Combine(fixture.Root, "steamui", "css", "sp.css");
                File.AppendAllText(target, "\n.external { color: red; }", new UTF8Encoding(false));
                string updated = ModCore.HashFile(target);
                Exception error = Capture(delegate { patches.Apply(Product.Steam, fixture.Root, assets); });
                Check(error is InvalidOperationException && ModCore.HashFile(target) == updated,
                    "A reused plan must still reject source changes under its apply lock.");
                Check(!File.Exists(fixture.JournalPath), "A source change before commits must not activate a journal.");
            }
        }

        private static void ClosedAppReopensOnStop(string assets)
        {
            using (Fixture fixture = new Fixture())
            using (var stop = new CancellationTokenSource())
            {
                var runtime = new FakeRuntime { OnClose = stop.Cancel };
                using (OperationControl.Enter(stop.Token, null))
                {
                    Cancelled(delegate { AutoSetup.Run(Product.Steam, fixture.Root, assets, false, null, runtime, new AutoSetupPatches()); });
                }

                Check(runtime.CloseCalls == 1 && runtime.ReopenCalls == 1, "Stop after a successful graceful close must reopen the previously running client.");
                fixture.AssertOriginals();
                Check(!File.Exists(fixture.JournalPath), "Stop after graceful close must not apply any patch.");
            }

            using (Fixture fixture = new Fixture())
            using (var stop = new CancellationTokenSource())
            {
                var runtime = new FakeRuntime { OnClose = stop.Cancel, ReopenError = true };
                Exception error;
                using (OperationControl.Enter(stop.Token, null))
                {
                    error = Capture(delegate { AutoSetup.Run(Product.Steam, fixture.Root, assets, false, null, runtime, new AutoSetupPatches()); });
                }

                Check(error is InvalidOperationException && error.InnerException is OperationCanceledException
                    && error.Message.IndexOf("Open Steam normally", StringComparison.Ordinal) >= 0,
                    "A restart failure after Stop must remain an actionable error rather than losing its warning in the GUI cancellation handler.");
                fixture.AssertOriginals();
                Check(runtime.ReopenCalls == 1 && !File.Exists(fixture.JournalPath), "Restart failure must leave original files and no active patch journal.");
            }
        }

        private static void Upgrade(string assets)
        {
            using (Fixture fixture = new Fixture())
            {
                fixture.ApplyLegacy(assets);
                var runtime = new FakeRuntime();
                AutoSetupResult result = AutoSetup.Run(Product.Steam, fixture.Root, assets, false, null, runtime, new AutoSetupPatches());
                Check(!result.AlreadyOptimized && fixture.Journal().ProfileVersion == ModCore.CurrentProfileVersion,
                    "A verified earlier profile must migrate to the current optimization instead of reporting Already optimized.");
                foreach (PatchRecord file in fixture.Journal().Files)
                {
                    Check(file.BeforeHash == fixture.OriginalHashes[file.RelativePath.Replace('/', '\\')], "The new profile backup must contain actual original bytes.");
                }

                result = AutoSetup.Run(Product.Steam, fixture.Root, assets, false, null, runtime, new AutoSetupPatches());
                Check(result.AlreadyOptimized && runtime.CloseCalls == 1, "The current verified profile must remain idempotent.");
                ModCore.Restore(Product.Steam, fixture.Root);
                fixture.AssertOriginals();
            }

            using (Fixture fixture = new Fixture())
            using (var stop = new CancellationTokenSource())
            {
                fixture.ApplyLegacy(assets);
                var runtime = new FakeRuntime();
                using (OperationControl.Enter(stop.Token, null))
                {
                    Cancelled(delegate
                    {
                        AutoSetup.Run(Product.Steam, fixture.Root, assets, false, delegate(string message)
                        {
                            if (message.IndexOf("new optimization profile", StringComparison.Ordinal) >= 0) stop.Cancel();
                        }, runtime, new AutoSetupPatches());
                    });
                }

                fixture.AssertOriginals();
                Check(!File.Exists(fixture.JournalPath) && runtime.ReopenCalls == 1, "Stop between old-profile removal and new-profile application must leave originals and reopen the client.");
            }

            using (Fixture fixture = new Fixture())
            {
                fixture.ApplyLegacy(assets);
                var runtime = new FakeRuntime();
                Exception error = Capture(delegate
                {
                    AutoSetup.Run(Product.Steam, fixture.Root, Path.Combine(fixture.Root, "missing-assets"), false, null, runtime, new AutoSetupPatches());
                });
                fixture.AssertOriginals();
                Check(error.Message.IndexOf("original files were restored", StringComparison.Ordinal) >= 0
                    && runtime.ReopenCalls == 1 && !File.Exists(fixture.JournalPath),
                    "An incompatible new profile must keep the recovered originals, reopen the client and report the outcome.");
            }

            using (Fixture fixture = new Fixture())
            {
                fixture.ApplyLegacy(assets);
                var runtime = new FakeRuntime();
                Exception error;
                using (OperationControl.Enter(CancellationToken.None, delegate(string message)
                {
                    if (message.StartsWith("Applying file 2 ", StringComparison.Ordinal))
                    {
                        PatchJournal current = fixture.Journal();
                        string backup = Path.Combine(fixture.Root, current.BackupDirectory, current.Files[0].BackupFile);
                        File.WriteAllBytes(backup, new byte[] { 1, 2, 3 });
                        throw new InvalidOperationException("Synthetic apply failure after a committed file.");
                    }
                }))
                {
                    error = Capture(delegate { AutoSetup.Run(Product.Steam, fixture.Root, assets, false, null, runtime, new AutoSetupPatches()); });
                }

                Check(error.Message.StartsWith("Could not apply changes. Keep the backup", StringComparison.Ordinal)
                    && !fixture.OriginalsMatch() && File.Exists(fixture.JournalPath) && runtime.ReopenCalls == 1,
                    "A failed new-profile rollback must preserve its recovery error instead of claiming originals are restored after the earlier profile was removed.");
            }

            using (Fixture fixture = new Fixture())
            {
                fixture.ApplyLegacy(assets);
                string target = Path.Combine(fixture.Root, "steamui", "css", "sp.css");
                File.AppendAllText(target, "\n.external { color: red; }", new UTF8Encoding(false));
                string external = ModCore.HashFile(target);
                var runtime = new FakeRuntime();
                Capture(delegate { AutoSetup.Run(Product.Steam, fixture.Root, assets, false, null, runtime, new AutoSetupPatches()); });
                Check(runtime.CloseCalls == 0 && ModCore.HashFile(target) == external && File.Exists(fixture.JournalPath),
                    "Migration must preserve external edits and their active recovery journal without closing the client.");
            }
        }

        private static void Cancelled(Action action)
        {
            Exception error = Capture(action);
            Check(error is OperationCanceledException, "Stop must propagate cancellation instead of success or a generic error: " + error);
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

            throw new InvalidOperationException("CancellationTests: An expected failure did not happen.");
        }

        private static void Check(bool success, string message)
        {
            if (!success) throw new InvalidOperationException("CancellationTests: " + message);
        }

        private sealed class StopAfterReadStream : MemoryStream
        {
            private readonly CancellationTokenSource stop;
            internal int ReadCalls;

            internal StopAfterReadStream(byte[] data, CancellationTokenSource cancellation) : base(data, false)
            {
                stop = cancellation;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = base.Read(buffer, offset, count);
                if (++ReadCalls == 2)
                {
                    stop.Cancel();
                }

                return read;
            }
        }

        private sealed class FakeRuntime : IAutoSetupRuntime
        {
            internal Action OnClose;
            internal int CloseCalls;
            internal int ReopenCalls;
            internal bool ReopenError;

            public AutoSetupCloseResult Close(Product product, string root, Action<string> progress)
            {
                CloseCalls++;
                if (OnClose != null) OnClose();
                return new AutoSetupCloseResult { WasRunning = true, Closed = true };
            }

            public void Reopen(Product product, string root)
            {
                ReopenCalls++;
                if (ReopenError)
                {
                    throw new InvalidOperationException("The synthetic client could not restart.");
                }
            }
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly string Root;
            internal readonly string JournalPath;
            internal readonly Dictionary<string, string> OriginalHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            internal Fixture()
            {
                Root = Path.Combine(Path.GetTempPath(), "LowConsuming-Stop-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path.Combine(Root, "steamui", "css"));
                File.WriteAllBytes(Path.Combine(Root, "steam.exe"), new byte[] { 77, 90, 0, 0 });
                SharedFixtures.WriteSteamBundle(Root);
                File.WriteAllText(Path.Combine(Root, "steamui", "css", "library.css"), ".library { color: white; }\n", new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(Root, "steamui", "css", "sp.css"), ".shell { color: black; }\n", new UTF8Encoding(false));
                foreach (string relative in new string[] { @"steamui\css\library.css", @"steamui\css\sp.css", @"steamui\sp.js" })
                {
                    OriginalHashes.Add(relative, ModCore.HashFile(Path.Combine(Root, relative)));
                }

                JournalPath = Path.Combine(Root, ".tawop", "steam.json");
            }

            internal bool OriginalsMatch()
            {
                foreach (KeyValuePair<string, string> file in OriginalHashes)
                {
                    if (ModCore.HashFile(Path.Combine(Root, file.Key)) != file.Value) return false;
                }

                return true;
            }

            internal void AssertOriginals()
            {
                Check(OriginalsMatch(), "Every target must contain the original bytes after safe recovery.");
            }

            internal PatchJournal Journal()
            {
                return new JavaScriptSerializer().Deserialize<PatchJournal>(File.ReadAllText(JournalPath));
            }

            internal void ApplyLegacy(string assets)
            {
                ModCore.Apply(Product.Steam, Root, assets);
                PatchJournal journal = Journal();
                journal.ProfileVersion = null;
                File.WriteAllText(JournalPath, ModCore.Json(journal), new UTF8Encoding(false));
            }

            public void Dispose()
            {
                string temporary = Path.GetFullPath(Path.GetTempPath());
                string target = Path.GetFullPath(Root);
                if (!Paths.IsInside(temporary, target) || !Path.GetFileName(target).StartsWith("LowConsuming-Stop-", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Unsafe cancellation fixture cleanup target.");
                }

                Directory.Delete(target, true);
            }
        }
    }
}
