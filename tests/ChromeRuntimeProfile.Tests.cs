using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;

namespace Tawop.Tests
{
    public static class ChromeRuntimeProfileTests
    {
        public static void Run()
        {
            string parent = Path.Combine(Path.GetTempPath(), "LowConsuming-ChromePolicy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(parent);
            try
            {
                ApplyAndRestore(Path.Combine(parent, "roundtrip.json"));
                ForeignPolicy(Path.Combine(parent, "foreign.json"));
                FailedWrite(Path.Combine(parent, "failed.json"));
                CancelApply(Path.Combine(parent, "cancel.json"));
                ExternalChange(Path.Combine(parent, "conflict.json"));
                CancelRestore(Path.Combine(parent, "restore.json"));
                TamperedBackup(Path.Combine(parent, "tamper.json"));
                Detection(parent);
            }
            finally
            {
                string full = Path.GetFullPath(parent);
                if (!Paths.IsInside(Path.GetFullPath(Path.GetTempPath()), full) || !Path.GetFileName(full).StartsWith("LowConsuming-ChromePolicy-", StringComparison.Ordinal))
                    throw new InvalidOperationException("Unsafe temporary Chrome test cleanup target.");
                Directory.Delete(full, true);
            }
        }

        private static AutoSetupResult Apply(MemoryPolicies store, string path, CancellationToken token, Action<string> progress)
        {
            return ChromeRuntimeProfile.RunCore("synthetic Chrome installation", false, progress, token, store, path);
        }

        private static AutoSetupResult Undo(MemoryPolicies store, string path, CancellationToken token, Action<string> progress)
        {
            return ChromeRuntimeProfile.RunCore(null, true, progress, token, store, path);
        }

        private static void ApplyAndRestore(string path)
        {
            var store = new MemoryPolicies();
            store.BeforeWrite = delegate
            {
                Check(File.Exists(path), "The backup must exist before the first policy write.");
                string text = File.ReadAllText(path);
                Check(!text.Contains("synthetic Chrome installation") && !text.Contains("InstallRoot"), "The backup must not store installation paths.");
            };
            AutoSetupResult result = Apply(store, path, CancellationToken.None, null);
            Check(result.Inspection != null && result.Inspection.BackupPath == path && !result.AlreadyOptimized, "Apply must return its backup and status.");
            Check(store.Values.Count == 4 && store.Values["HighEfficiencyModeEnabled"].Value == 1 && store.Values["BackgroundModeEnabled"].Value == 0
                && store.Values["NetworkPredictionOptions"].Value == 2 && store.Values["MemorySaverModeSavings"].Value == 2, "Only the four expected native performance policies must be enabled.");
            int writes = store.Writes;
            Check(Apply(store, path, CancellationToken.None, null).AlreadyOptimized && store.Writes == writes, "A repeated apply must reuse the verified backup without changing policies.");
            store.BeforeWrite = null;
            Undo(store, path, CancellationToken.None, null);
            Check(store.Values.Count == 0 && !File.Exists(path), "Undo must restore absence and remove only the completed journal.");
        }

        private static void ForeignPolicy(string path)
        {
            var user = new MemoryPolicies();
            user.Values["HighEfficiencyModeEnabled"] = new ChromePolicyValue { Exists = true, IsDword = true, Value = 0 };
            Reject(delegate { Apply(user, path, CancellationToken.None, null); }, "An existing user performance policy must be preserved.");
            Check(user.Writes == 0 && !File.Exists(path) && user.Values["HighEfficiencyModeEnabled"].Value == 0, "Foreign user policy was changed.");
            var machine = new MemoryPolicies();
            machine.Machine.Add("MemorySaverModeSavings");
            Reject(delegate { Apply(machine, path, CancellationToken.None, null); }, "A machine performance policy must not be overridden.");
            Check(machine.Writes == 0 && !File.Exists(path), "Machine-policy rejection must happen before creating a transaction.");
            var wrongType = new MemoryPolicies();
            wrongType.Values["BackgroundModeEnabled"] = new ChromePolicyValue { Exists = true, IsDword = false };
            Reject(delegate { Apply(wrongType, path, CancellationToken.None, null); }, "A foreign non-DWORD value must never be overwritten.");
            Check(wrongType.Writes == 0 && !File.Exists(path), "Unsupported user-policy type was overwritten.");
        }

        private static void FailedWrite(string path)
        {
            var store = new MemoryPolicies { FailAfterWriteOnce = 2 };
            Reject(delegate { Apply(store, path, CancellationToken.None, null); }, "A registry write failure must be reported.");
            Check(store.Values.Count == 0 && !File.Exists(path), "A partially failed write must roll back all attempted values.");
        }

        private static void CancelApply(string path)
        {
            using (var source = new CancellationTokenSource())
            {
                var store = new MemoryPolicies();
                store.AfterWrite = delegate { if (store.Writes == 1) source.Cancel(); };
                bool stopped = false;
                try { Apply(store, path, source.Token, null); }
                catch (OperationCanceledException) { stopped = true; }
                Check(stopped && store.Values.Count == 0 && !File.Exists(path), "Stop after a policy write must complete uncancelable rollback.");
            }
        }

        private static void ExternalChange(string path)
        {
            var store = new MemoryPolicies();
            Apply(store, path, CancellationToken.None, null);
            store.Values["NetworkPredictionOptions"].Value = 0;
            Reject(delegate { Undo(store, path, CancellationToken.None, null); }, "External edits must leave an incomplete-undo result.");
            Check(File.Exists(path) && store.Values.Count == 1 && store.Values["NetworkPredictionOptions"].Value == 0, "Undo must preserve the external value and restore unrelated owned policies.");
            store.Values["NetworkPredictionOptions"].Value = 2;
            Undo(store, path, CancellationToken.None, null);
            Check(store.Values.Count == 0 && !File.Exists(path), "Undo must resume a previously interrupted restoration.");
        }

        private static void CancelRestore(string path)
        {
            var store = new MemoryPolicies();
            Apply(store, path, CancellationToken.None, null);
            using (var source = new CancellationTokenSource())
            {
                int startingWrites = store.Writes;
                store.AfterWrite = delegate { if (store.Writes == startingWrites + 1) source.Cancel(); };
                bool stopped = false;
                try { Undo(store, path, source.Token, null); }
                catch (OperationCanceledException) { stopped = true; }
                Check(stopped && File.Exists(path) && store.Values.Count == 3, "Undo Stop must finish the current value and keep remaining recovery state.");
                store.AfterWrite = null;
            }
            Undo(store, path, CancellationToken.None, null);
            Check(store.Values.Count == 0 && !File.Exists(path), "Undo must continue after a safe stop.");
        }

        private static void TamperedBackup(string path)
        {
            var store = new MemoryPolicies();
            Apply(store, path, CancellationToken.None, null);
            string original = File.ReadAllText(path);
            File.WriteAllText(path, original.Replace("HighEfficiencyModeEnabled", "UnsafePolicy"));
            int writes = store.Writes;
            Reject(delegate { Undo(store, path, CancellationToken.None, null); }, "A policy outside the allowlist must be rejected.");
            Check(store.Writes == writes && store.Values.Count == 4, "A tampered backup must not change any registry values.");
            File.WriteAllText(path, original.Replace("\"BeforeValue\":0", "\"BeforeValue\":7"));
            Reject(delegate { Undo(store, path, CancellationToken.None, null); }, "A crafted nonzero absent baseline must be rejected.");
            Check(store.Writes == writes && store.Values.Count == 4, "A crafted baseline must never reach the registry.");
            File.WriteAllText(path, original);
            Undo(store, path, CancellationToken.None, null);
        }

        private static void Detection(string parent)
        {
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                bool stopped = false;
                try { ChromeRuntimeProfile.Detect(source.Token); }
                catch (OperationCanceledException) { stopped = true; }
                Check(stopped, "Automatic Chrome detection must honor Stop immediately.");
            }
            string fake = Path.Combine(parent, "not-Chrome");
            Directory.CreateDirectory(fake);
            File.WriteAllBytes(Path.Combine(fake, "chrome.exe"), new byte[] { 77, 90, 0, 0 });
            Reject(delegate { ChromeRuntimeProfile.Resolve(fake, CancellationToken.None); }, "A file merely named chrome.exe must not qualify as supported Google Chrome.");
        }

        private static void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); }
            catch (Exception) { rejected = true; }
            Check(rejected, message);
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private sealed class MemoryPolicies : IChromePolicyStore
        {
            internal readonly Dictionary<string, ChromePolicyValue> Values = new Dictionary<string, ChromePolicyValue>();
            internal readonly HashSet<string> Machine = new HashSet<string>();
            internal int Writes;
            internal int FailAfterWriteOnce;
            internal Action BeforeWrite;
            internal Action AfterWrite;

            public ChromePolicyValue ReadUser(string name)
            {
                ChromePolicyValue value;
                if (!Values.TryGetValue(name, out value)) return new ChromePolicyValue();
                return new ChromePolicyValue { Exists = value.Exists, IsDword = value.IsDword, Value = value.Value };
            }

            public bool HasMachinePolicy(string name) { return Machine.Contains(name); }

            public void WriteUser(string name, ChromePolicyValue value)
            {
                if (BeforeWrite != null) BeforeWrite();
                Writes++;
                if (value.Exists) Values[name] = new ChromePolicyValue { Exists = true, IsDword = value.IsDword, Value = value.Value };
                else Values.Remove(name);
                if (AfterWrite != null) AfterWrite();
                if (Writes == FailAfterWriteOnce)
                {
                    FailAfterWriteOnce = 0;
                    throw new IOException("Synthetic failure after writing one policy value.");
                }
            }
        }
    }
}
