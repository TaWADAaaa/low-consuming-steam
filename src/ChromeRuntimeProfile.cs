using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace Tawop
{
    internal sealed class ChromePolicyValue
    {
        public bool Exists;
        public bool IsDword;
        public int Value;
    }

    internal interface IChromePolicyStore
    {
        ChromePolicyValue ReadUser(string name);
        bool HasMachinePolicy(string name);
        void WriteUser(string name, ChromePolicyValue value);
    }

    internal sealed class ChromePolicyRecord
    {
        public string Name;
        public bool BeforeExists;
        public int BeforeValue;
        public int AfterValue;
        public string BeforeHash;
        public string AfterHash;
        public bool Restored;
    }

    internal sealed class ChromePolicyJournal
    {
        public int SchemaVersion = 1;
        public string Profile = "LowConsumingChromePoliciesV5";
        public string Status;
        public List<ChromePolicyRecord> Values = new List<ChromePolicyRecord>();
    }

    public static class ChromeRuntimeProfile
    {
        internal static readonly string[] Names = { "HighEfficiencyModeEnabled", "BackgroundModeEnabled", "NetworkPredictionOptions", "MemorySaverModeSavings" };
        private static readonly int[] Desired = { 1, 0, 2, 2 };
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static string Detect(CancellationToken cancellation)
        {
            foreach (string candidate in Candidates())
            {
                cancellation.ThrowIfCancellationRequested();
                try { return Resolve(candidate, cancellation); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (InvalidOperationException) { }
                catch (ArgumentException) { }
            }
            return null;
        }

        public static string Resolve(string path, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(path))
            {
                string detected = Detect(cancellation);
                if (detected != null) return detected;
                throw new DirectoryNotFoundException("Google Chrome was not found. Install official Google Chrome, or choose its Application folder.");
            }
            string value = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
            if (File.Exists(value))
            {
                if (!String.Equals(Path.GetFileName(value), "chrome.exe", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Choose Google Chrome's Application folder or chrome.exe.");
                value = Path.GetDirectoryName(Path.GetFullPath(value));
            }
            string root = Paths.Root(value);
            foreach (string folder in new string[] { root, Paths.Child(root, "Application") })
            {
                cancellation.ThrowIfCancellationRequested();
                string exe = Paths.Child(folder, "chrome.exe");
                if (!File.Exists(exe)) continue;
                var info = FileVersionInfo.GetVersionInfo(exe);
                if (info.ProductMajorPart >= 126 && info.CompanyName != null && info.CompanyName.StartsWith("Google", StringComparison.OrdinalIgnoreCase)
                    && info.ProductName != null && info.ProductName.IndexOf("Google Chrome", StringComparison.OrdinalIgnoreCase) >= 0)
                    return folder;
            }
            throw new InvalidOperationException("This is not a supported Google Chrome installation. Chrome 126 or later is required; a Chromium source folder is only for the advanced source commands.");
        }

        public static bool HasBackup()
        {
            string path = JournalPath();
            Paths.RejectReparsePoints(path);
            return File.Exists(path);
        }

        public static AutoSetupResult Run(string root, bool restore, Action<string> progress, CancellationToken cancellation)
        {
            string resolved = restore ? root : Resolve(root, cancellation);
            return RunCore(resolved, restore, progress, cancellation, new WindowsChromePolicyStore(), JournalPath());
        }

        internal static AutoSetupResult RunCore(string root, bool restore, Action<string> progress, CancellationToken cancellation,
            IChromePolicyStore store, string journalPath)
        {
            cancellation.ThrowIfCancellationRequested();
            Paths.RejectReparsePoints(journalPath);
            using (var mutex = new Mutex(false, "Local\\LowConsumingChromePoliciesV5"))
            {
                bool held = false;
                try
                {
                    while (!held)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        try { held = mutex.WaitOne(100); }
                        catch (AbandonedMutexException) { held = true; }
                    }
                    return restore ? Restore(root, store, journalPath, progress, cancellation)
                        : Apply(root, store, journalPath, progress, cancellation);
                }
                finally { if (held) mutex.ReleaseMutex(); }
            }
        }

        private static AutoSetupResult Apply(string root, IChromePolicyStore store, string path, Action<string> progress, CancellationToken cancellation)
        {
            Report(progress, "Checking the four Chrome performance policies...", cancellation);
            for (int i = 0; i < Names.Length; i++)
            {
                if (store.HasMachinePolicy(Names[i]))
                    throw new InvalidOperationException("Chrome has an existing machine performance policy. This tool will not override administrator settings. No policies have been changed.");
            }
            if (File.Exists(path))
            {
                ChromePolicyJournal existing = Load(path);
                if (existing.Status == "Applied" && MatchesAll(existing, store, false))
                    return Result(root, path, "Already optimized. Reload chrome://policy or restart Chrome normally when convenient.", true);
                throw new InvalidOperationException("A Chrome policy backup already exists. Use Undo changes first; external policy changes will be preserved.");
            }
            var journal = new ChromePolicyJournal { Status = "Prepared" };
            for (int i = 0; i < Names.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                ChromePolicyValue before = store.ReadUser(Names[i]);
                if (before.Exists)
                    throw new InvalidOperationException("Chrome already has a user performance policy. Its owner is unknown, so this tool will preserve it. No policies have been changed.");
                var after = new ChromePolicyValue { Exists = true, IsDword = true, Value = Desired[i] };
                journal.Values.Add(new ChromePolicyRecord { Name = Names[i], BeforeExists = before.Exists, BeforeValue = before.Value,
                    AfterValue = Desired[i], BeforeHash = Hash(before), AfterHash = Hash(after) });
            }
            Save(path, journal);
            try
            {
                foreach (ChromePolicyRecord record in journal.Values)
                {
                    Report(progress, "Applying Chrome's " + record.Name + " policy...", cancellation);
                    if (store.HasMachinePolicy(record.Name) || Hash(store.ReadUser(record.Name)) != record.BeforeHash)
                        throw new InvalidOperationException("A Chrome policy changed during preparation. Its current value will be preserved.");
                    store.WriteUser(record.Name, After(record));
                    if (Hash(store.ReadUser(record.Name)) != record.AfterHash)
                        throw new IOException("Chrome's policy value could not be verified after writing.");
                }
                cancellation.ThrowIfCancellationRequested();
                journal.Status = "Applied";
                Save(path, journal);
                return Result(root, path, "Chrome policies saved. Reload chrome://policy or restart Chrome normally when convenient.", false);
            }
            catch (Exception error)
            {
                if (progress != null) { try { progress("Recovering the original Chrome policy values..."); } catch { } }
                bool recovered = Recover(journal, store, path);
                if (!recovered)
                    throw new InvalidOperationException("Chrome policy changes stopped, but an external change or lock prevented complete recovery. Keep the backup and use Undo changes. " + error.Message, error);
                if (error is OperationCanceledException)
                    throw new OperationCanceledException("Stopped safely. Original Chrome policy values were restored.", error, cancellation);
                throw new InvalidOperationException("Chrome policy changes were not completed. Original values were restored. " + error.Message, error);
            }
        }

        private static AutoSetupResult Restore(string root, IChromePolicyStore store, string path, Action<string> progress, CancellationToken cancellation)
        {
            if (!File.Exists(path)) throw new InvalidOperationException("There are no Chrome policy changes to undo.");
            ChromePolicyJournal journal = Load(path);
            journal.Status = "Restoring";
            Save(path, journal);
            bool complete = true;
            foreach (ChromePolicyRecord record in journal.Values)
            {
                Report(progress, "Restoring Chrome's " + record.Name + " policy...", cancellation);
                string current = Hash(store.ReadUser(record.Name));
                if (current == record.BeforeHash) record.Restored = true;
                else if (!record.Restored && current == record.AfterHash)
                {
                    store.WriteUser(record.Name, Before(record));
                    record.Restored = Hash(store.ReadUser(record.Name)) == record.BeforeHash;
                }
                else record.Restored = false;
                if (!record.Restored) complete = false;
                Save(path, journal);
            }
            cancellation.ThrowIfCancellationRequested();
            if (!complete)
            {
                journal.Status = "External changes preserved";
                Save(path, journal);
                throw new InvalidOperationException("Some Chrome policies changed outside this tool. Those values were preserved, and the backup remains available.");
            }
            DeleteJournal(path);
            return Result(root, null, "Chrome policies restored. Reload chrome://policy or restart Chrome normally when convenient.", false);
        }

        private static bool Recover(ChromePolicyJournal journal, IChromePolicyStore store, string path)
        {
            bool complete = true;
            foreach (ChromePolicyRecord record in journal.Values)
            {
                try
                {
                    string current = Hash(store.ReadUser(record.Name));
                    if (current == record.AfterHash) store.WriteUser(record.Name, Before(record));
                    record.Restored = Hash(store.ReadUser(record.Name)) == record.BeforeHash;
                    if (!record.Restored) complete = false;
                }
                catch { complete = false; }
            }
            try
            {
                if (complete) DeleteJournal(path);
                else { journal.Status = "Recovery needed"; Save(path, journal); }
            }
            catch { complete = false; }
            return complete;
        }

        private static AutoSetupResult Result(string root, string path, string message, bool already)
        {
            return new AutoSetupResult { Root = root, AlreadyOptimized = already, Message = message,
                Inspection = new PatchInspection { Product = "Chrome", Root = root, Kind = "Four current-user policies: Maximum Memory Saver enabled, background mode disabled, predictive page loading disabled. Savings remain unverified.", Status = path == null ? "Restored" : "Applied", BackupPath = path } };
        }

        private static ChromePolicyValue Before(ChromePolicyRecord record)
        {
            return new ChromePolicyValue { Exists = record.BeforeExists, IsDword = record.BeforeExists, Value = record.BeforeValue };
        }

        private static ChromePolicyValue After(ChromePolicyRecord record)
        {
            return new ChromePolicyValue { Exists = true, IsDword = true, Value = record.AfterValue };
        }

        private static bool MatchesAll(ChromePolicyJournal journal, IChromePolicyStore store, bool before)
        {
            foreach (ChromePolicyRecord record in journal.Values)
                if (Hash(store.ReadUser(record.Name)) != (before ? record.BeforeHash : record.AfterHash)) return false;
            return true;
        }

        private static string Hash(ChromePolicyValue value)
        {
            string text = !value.Exists ? "absent" : value.IsDword ? "DWORD:" + value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "foreign-type";
            using (SHA256 algorithm = SHA256.Create()) return BitConverter.ToString(algorithm.ComputeHash(Utf8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }

        private static ChromePolicyJournal Load(string path)
        {
            Paths.RejectReparsePoints(path);
            var file = new FileInfo(path);
            if (file.Length < 2 || file.Length > 65536) throw new InvalidDataException("The Chrome policy backup has an invalid size.");
            var serializer = new JavaScriptSerializer { MaxJsonLength = 65536 };
            ChromePolicyJournal journal = serializer.Deserialize<ChromePolicyJournal>(File.ReadAllText(path, Utf8));
            if (journal == null || journal.SchemaVersion != 1 || journal.Profile != "LowConsumingChromePoliciesV5" || journal.Values == null || journal.Values.Count != Names.Length
                || (journal.Status != "Prepared" && journal.Status != "Applied" && journal.Status != "Restoring" && journal.Status != "Recovery needed" && journal.Status != "External changes preserved"))
                throw new InvalidDataException("The Chrome policy backup is not supported. No policies have been changed.");
            for (int i = 0; i < Names.Length; i++)
            {
                ChromePolicyRecord record = journal.Values[i];
                if (record == null || record.Name != Names[i] || record.BeforeExists || record.BeforeValue != 0 || record.AfterValue != Desired[i]
                    || record.BeforeHash != Hash(Before(record)) || record.AfterHash != Hash(After(record)))
                    throw new InvalidDataException("The Chrome policy backup failed validation. No policies have been changed.");
            }
            return journal;
        }

        private static void Save(string path, ChromePolicyJournal journal)
        {
            Paths.RejectReparsePoints(path);
            string parent = Path.GetDirectoryName(path);
            Directory.CreateDirectory(parent);
            Paths.RejectReparsePoints(parent);
            byte[] bytes = Utf8.GetBytes(new JavaScriptSerializer().Serialize(journal));
            string temporary = path + ".new-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                Paths.RejectReparsePoints(path);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static void DeleteJournal(string path)
        {
            Paths.RejectReparsePoints(path);
            if (File.Exists(path)) File.Delete(path);
        }

        private static void Report(Action<string> progress, string message, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (progress != null) progress(message);
            cancellation.ThrowIfCancellationRequested();
        }

        private static string JournalPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LowConsuming", "Chrome", "policy-backup.json");
        }

        private static IEnumerable<string> Candidates()
        {
            foreach (Environment.SpecialFolder folder in new Environment.SpecialFolder[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.LocalApplicationData })
            {
                string location = Environment.GetFolderPath(folder);
                if (!String.IsNullOrEmpty(location)) yield return Path.Combine(location, "Google", "Chrome", "Application");
            }
        }
    }

    internal sealed class WindowsChromePolicyStore : IChromePolicyStore
    {
        private const string KeyPath = @"Software\Policies\Google\Chrome";
        private static void ValidateName(string name)
        {
            if (Array.IndexOf(ChromeRuntimeProfile.Names, name) < 0) throw new InvalidOperationException("Policy is outside the four performance settings.");
        }

        public ChromePolicyValue ReadUser(string name)
        {
            ValidateName(name);
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath, false))
            {
                if (key == null || !ContainsName(key, name)) return new ChromePolicyValue();
                if (key.GetValueKind(name) != RegistryValueKind.DWord) return new ChromePolicyValue { Exists = true };
                return new ChromePolicyValue { Exists = true, IsDword = true, Value = (int)key.GetValue(name, 0, RegistryValueOptions.DoNotExpandEnvironmentNames) };
            }
        }

        public bool HasMachinePolicy(string name)
        {
            ValidateName(name);
            using (RegistryKey recommended = Registry.CurrentUser.OpenSubKey(KeyPath + @"\Recommended", false))
                if (recommended != null && ContainsName(recommended, name)) return true;
            foreach (RegistryView view in new RegistryView[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using (RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                {
                    foreach (string suffix in new string[] { "", @"\Recommended" })
                    {
                        using (RegistryKey key = machine.OpenSubKey(KeyPath + suffix, false))
                            if (key != null && ContainsName(key, name)) return true;
                    }
                }
            }
            return false;
        }

        private static bool ContainsName(RegistryKey key, string name)
        {
            foreach (string existing in key.GetValueNames())
                if (String.Equals(existing, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public void WriteUser(string name, ChromePolicyValue value)
        {
            ValidateName(name);
            if (value.Exists && !value.IsDword) throw new InvalidOperationException("Only DWORD performance policies can be written.");
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                if (value.Exists) key.SetValue(name, value.Value, RegistryValueKind.DWord);
                else key.DeleteValue(name, false);
                key.Flush();
            }
        }
    }
}
