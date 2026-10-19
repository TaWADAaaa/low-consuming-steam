using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace Tawop
{
    public enum Product
    {
        Chrome,
        Discord,
        Steam
    }

    public sealed class PatchEdit
    {
        public string RelativePath;
        public byte[] Content;
        public string ExpectedBeforeHash;
    }

    public sealed class PatchRecord
    {
        public string RelativePath;
        public string BeforeHash;
        public string AfterHash;
        public long BeforeBytes;
        public long AfterBytes;
        public string BackupFile;
        public bool Restored;
    }

    public sealed class PatchJournal
    {
        public int SchemaVersion = 1;
        public string Product;
        public string InstallRoot;
        public string CreatedUtc;
        public string BackupDirectory;
        public string Status;
        public List<PatchRecord> Files = new List<PatchRecord>();
    }

    public sealed class PatchInspection
    {
        public string Product;
        public string Root;
        public string Kind;
        public string Status;
        public string BackupPath;
        public string MemoryStatus = "Memory savings have not been measured. Functional and performance tests are still required.";
        public List<PatchRecord> Files = new List<PatchRecord>();
    }

    public static class Paths
    {
        public static string Root(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException("Choose an installation folder or a Chromium source folder.");
            }

            string root = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(root))
            {
                throw new DirectoryNotFoundException("Folder not found: " + root);
            }

            if (Path.GetPathRoot(root).TrimEnd('\\', '/') == root)
            {
                throw new InvalidOperationException("Choose the application folder, not the root of a drive.");
            }

            RejectReparsePoints(root);
            return root;
        }

        public static string Child(string root, string relative)
        {
            if (String.IsNullOrEmpty(relative) || Path.IsPathRooted(relative) || relative.IndexOf(':') >= 0 || relative.IndexOf('\0') >= 0)
            {
                throw new InvalidOperationException("Invalid relative path.");
            }

            foreach (string part in relative.Replace('/', '\\').Split('\\'))
            {
                if (part.Length == 0 || part == "." || part == "..")
                {
                    throw new InvalidOperationException("Path is outside the project.");
                }
            }

            string full = Path.GetFullPath(Path.Combine(root, relative));
            if (!IsInside(root, full))
            {
                throw new InvalidOperationException("Path is outside the installation.");
            }

            RejectReparsePoints(full);
            return full;
        }

        public static bool IsInside(string root, string full)
        {
            return full.StartsWith(root.TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        public static void RejectReparsePoints(string path)
        {
            string current = Path.GetFullPath(path);
            while (!String.IsNullOrEmpty(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException("Links and reparse points are not supported: " + current);
                }

                current = Path.GetDirectoryName(current);
            }
        }

        public static void ValidateTarget(Product product, string relative)
        {
            string normalized = relative.Replace('/', '\\');
            bool allowed = false;
            if (product == Product.Chrome)
            {
                allowed = normalized == @"chrome\app\theme\chromium\BRANDING" || normalized == @"components\performance_manager\user_tuning\prefs.cc";
            }
            else if (product == Product.Steam)
            {
                allowed = Regex.IsMatch(normalized, @"^steamui\\css\\[^\\]+\.css$", RegexOptions.IgnoreCase);
            }
            else if (product == Product.Discord)
            {
                allowed = normalized == @"resources\app.asar" || Regex.IsMatch(normalized, @"^modules\\discord_desktop_core-[0-9]+\\discord_desktop_core\\core\.asar$", RegexOptions.IgnoreCase);
            }

            if (!allowed)
            {
                throw new InvalidOperationException("File is outside the allowed patch targets: " + relative);
            }
        }
    }

    public static class ModCore
    {
        private const int MaximumFiles = 64;
        private const long MaximumFileBytes = 256L * 1024 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public static string Hash(byte[] data)
        {
            using (SHA256 hash = SHA256.Create())
            {
                return BitConverter.ToString(hash.ComputeHash(data)).Replace("-", "");
            }
        }

        public static byte[] ReadFile(string path)
        {
            Paths.RejectReparsePoints(path);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("A required file is missing.", path);
            }

            if (new FileInfo(path).Length > MaximumFileBytes)
            {
                throw new InvalidOperationException("File exceeds the supported size: " + path);
            }

            return File.ReadAllBytes(path);
        }

        private static JavaScriptSerializer Serializer()
        {
            return new JavaScriptSerializer
            {
                MaxJsonLength = 8 * 1024 * 1024,
                RecursionLimit = 100
            };
        }

        public static string Json(object value)
        {
            return Serializer().Serialize(value);
        }

        private static string ActivePath(string root, Product product)
        {
            return Paths.Child(root, @".tawop\" + product.ToString().ToLowerInvariant() + ".json");
        }

        private static List<PatchEdit> Prepare(Product product, string root, string assets)
        {
            List<PatchEdit> edits;
            if (product == Product.Discord)
            {
                edits = DiscordMod.Prepare(root, assets);
            }
            else if (product == Product.Steam)
            {
                edits = SteamMod.Prepare(root, assets);
            }
            else
            {
                edits = ChromeMod.Prepare(root, assets);
            }

            if (edits == null || edits.Count == 0 || edits.Count > MaximumFiles)
            {
                throw new InvalidOperationException("Unexpected number of files. Nothing was changed.");
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (PatchEdit edit in edits)
            {
                Paths.ValidateTarget(product, edit.RelativePath);
                string path = Paths.Child(root, edit.RelativePath);
                if (!seen.Add(path) || edit.Content == null || edit.Content.LongLength > MaximumFileBytes)
                {
                    throw new InvalidOperationException("Invalid patch plan.");
                }

                total += edit.Content.LongLength;
                if (total > 512L * 1024 * 1024)
                {
                    throw new InvalidOperationException("Patch plan exceeds the supported size.");
                }

                ReadFile(path);
                if (!Regex.IsMatch(edit.ExpectedBeforeHash ?? "", "^[A-F0-9]{64}$") || Hash(ReadFile(path)) != edit.ExpectedBeforeHash)
                {
                    throw new InvalidOperationException("File changed while preparing the patch: " + edit.RelativePath);
                }
            }

            return edits;
        }

        public static PatchInspection Inspect(Product product, string installPath, string assetsPath)
        {
            string root = Paths.Root(installPath);
            string active = ActivePath(root, product);
            if (File.Exists(active))
            {
                PatchJournal journal = LoadJournal(product, root);
                string status = journal.Status;
                if (status == "Aplicado")
                {
                    foreach (PatchRecord record in journal.Files)
                    {
                        if (!File.Exists(Paths.Child(root, record.RelativePath)) || Hash(ReadFile(Paths.Child(root, record.RelativePath))) != record.AfterHash)
                        {
                            status = "Patch changed by an update or another tool";
                            break;
                        }
                    }
                }

                return new PatchInspection
                {
                    Product = product.ToString(),
                    Root = root,
                    Kind = Kind(product),
                    Status = DisplayStatus(status),
                    BackupPath = active,
                    Files = journal.Files
                };
            }

            List<PatchEdit> edits = Prepare(product, root, assetsPath);
            PatchInspection report = new PatchInspection
            {
                Product = product.ToString(),
                Root = root,
                Kind = Kind(product),
                Status = "Ready; changes have not been applied"
            };
            foreach (PatchEdit edit in edits)
            {
                byte[] before = ReadFile(Paths.Child(root, edit.RelativePath));
                if (Hash(before) != edit.ExpectedBeforeHash)
                {
                    throw new InvalidOperationException("File changed during inspection.");
                }

                report.Files.Add(new PatchRecord
                {
                    RelativePath = edit.RelativePath,
                    BeforeHash = Hash(before),
                    AfterHash = Hash(edit.Content),
                    BeforeBytes = before.LongLength,
                    AfterBytes = edit.Content.LongLength
                });
            }

            return report;
        }

        private static string Kind(Product product)
        {
            return product == Product.Chrome ? "Chromium source patch. Building the browser still requires a C++ toolchain." : "Local client resource patch. The original client engine is retained.";
        }

        private static string DisplayStatus(string status)
        {
            switch (status)
            {
                case "Preparando":
                    return "Preparing";
                case "Aplicando; respaldo recuperable":
                    return "Applying; recovery backup available";
                case "Aplicado":
                    return "Applied";
                case "Aplicación interrumpida":
                    return "Application interrupted";
                case "Conflictos; se conservaron los cambios externos":
                    return "Conflicts; external changes preserved";
                case "Restaurado":
                    return "Restored";
                default:
                    return status;
            }
        }

        public static void EnsureClosed(Product product, string root)
        {
            int ownId;
            using (Process ownProcess = Process.GetCurrentProcess())
            {
                ownId = ownProcess.Id;
            }

            string[] names = product == Product.Steam ? new string[]
            {
                "steam",
                "steamwebhelper"
            }

            : product == Product.Discord ? new string[]
            {
                "Discord",
                "DiscordCanary",
                "DiscordPTB",
                "DiscordDevelopment"
            }

            : new string[]
            {
                "chrome",
                "ChromeTawop"
            };
            foreach (string name in names)
            {
                foreach (Process process in Process.GetProcessesByName(name))
                {
                    using (process)
                    {
                        if (process.Id == ownId)
                        {
                            continue;
                        }

                        string path;
                        try
                        {
                            path = process.MainModule.FileName;
                        }
                        catch
                        {
                            throw new InvalidOperationException("Could not verify a running process for " + product + ". Close it or use the required Windows permissions.");
                        }

                        if (Paths.IsInside(root, Path.GetFullPath(path)))
                        {
                            throw new InvalidOperationException("Close " + product + " from its tray menu before applying or restoring changes.");
                        }
                    }
                }
            }
        }

        public static PatchInspection Apply(Product product, string installPath, string assetsPath)
        {
            string root = Paths.Root(installPath);
            using (PatchLock guard = new PatchLock(root, product))
            {
                EnsureClosed(product, root);
                string active = ActivePath(root, product);
                if (File.Exists(active))
                {
                    throw new InvalidOperationException("A backup is already active. Undo the changes before applying again.");
                }

                List<PatchEdit> edits = Prepare(product, root, assetsPath);
                PatchJournal journal = new PatchJournal
                {
                    Product = product.ToString(),
                    InstallRoot = root,
                    CreatedUtc = DateTime.UtcNow.ToString("o"),
                    BackupDirectory = @".tawop\" + product.ToString().ToLowerInvariant() + "-" + Guid.NewGuid().ToString("N"),
                    Status = "Preparando"
                };
                string backupDirectory = Paths.Child(root, journal.BackupDirectory);
                Directory.CreateDirectory(backupDirectory);
                foreach (PatchEdit edit in edits)
                {
                    byte[] before = ReadFile(Paths.Child(root, edit.RelativePath));
                    if (Hash(before) != edit.ExpectedBeforeHash)
                    {
                        throw new InvalidOperationException("File changed before creating its backup.");
                    }

                    string name = journal.Files.Count.ToString("D4") + ".bin";
                    string backupFile = Paths.Child(backupDirectory, name);
                    using (FileStream stream = new FileStream(backupFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(before, 0, before.Length);
                        stream.Flush(true);
                    }

                    journal.Files.Add(new PatchRecord
                    {
                        RelativePath = edit.RelativePath,
                        BeforeHash = Hash(before),
                        AfterHash = Hash(edit.Content),
                        BeforeBytes = before.LongLength,
                        AfterBytes = edit.Content.LongLength,
                        BackupFile = name
                    });
                }

                journal.Status = "Aplicando; respaldo recuperable";
                SaveJournal(active, journal);
                try
                {
                    for (int i = 0; i < edits.Count; i++)
                    {
                        EnsureClosed(product, root);
                        string target = Paths.Child(root, edits[i].RelativePath);
                        if (Hash(ReadFile(target)) != journal.Files[i].BeforeHash)
                        {
                            throw new InvalidOperationException("Client files changed while preparing the patch: " + edits[i].RelativePath);
                        }

                        ReplaceClientFile(product, root, target, edits[i].Content, journal.Files[i].BeforeHash);
                        if (Hash(ReadFile(target)) != journal.Files[i].AfterHash)
                        {
                            throw new InvalidOperationException("Could not verify " + edits[i].RelativePath);
                        }
                    }

                    journal.Status = "Aplicado";
                    SaveJournal(active, journal);
                    return new PatchInspection
                    {
                        Product = product.ToString(),
                        Root = root,
                        Kind = Kind(product),
                        Status = DisplayStatus(journal.Status),
                        BackupPath = active,
                        Files = journal.Files
                    };
                }
                catch (Exception applyError)
                {
                    journal.Status = "Aplicación interrumpida";
                    string journalWarning = "";
                    try
                    {
                        SaveJournal(active, journal);
                    }
                    catch (Exception journalError)
                    {
                        journalWarning = " / Backup status: " + journalError.Message;
                    }

                    try
                    {
                        RestoreCore(product, root);
                    }
                    catch (Exception rollbackError)
                    {
                        throw new InvalidOperationException("Could not apply changes. Keep the backup and use Undo changes. " + applyError.Message + " / " + rollbackError.Message + journalWarning, applyError);
                    }

                    throw new InvalidOperationException("Changes could not be completed. Original files were restored. " + applyError.Message, applyError);
                }
            }
        }

        public static PatchInspection Restore(Product product, string installPath)
        {
            string root = Paths.Root(installPath);
            using (PatchLock guard = new PatchLock(root, product))
            {
                EnsureClosed(product, root);
                return RestoreCore(product, root);
            }
        }

        private static PatchJournal LoadJournal(Product product, string root)
        {
            string active = ActivePath(root, product);
            if (!File.Exists(active))
            {
                throw new InvalidOperationException("There is no active backup for this installation.");
            }

            if (new FileInfo(active).Length > 2 * 1024 * 1024)
            {
                throw new InvalidOperationException("Invalid backup.");
            }

            PatchJournal journal = Serializer().Deserialize<PatchJournal>(Utf8.GetString(File.ReadAllBytes(active)));
            if (journal == null || journal.SchemaVersion != 1 || journal.Product != product.ToString() || !String.Equals(journal.InstallRoot, root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The backup belongs to a different installation.");
            }

            if (journal.Files == null || journal.Files.Count < 1 || journal.Files.Count > MaximumFiles ||
                !Regex.IsMatch(journal.BackupDirectory ?? "", @"^\.tawop\\" + product.ToString().ToLowerInvariant() + @"-[a-f0-9]{32}$"))
            {
                throw new InvalidOperationException("Invalid backup structure.");
            }

            string backup = Paths.Child(root, journal.BackupDirectory);
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PatchRecord record in journal.Files)
            {
                Paths.ValidateTarget(product, record.RelativePath);
                if (!seen.Add(Paths.Child(root, record.RelativePath)) ||
                    !Regex.IsMatch(record.BackupFile ?? "", @"^[0-9]{4}\.bin$") ||
                    !Regex.IsMatch(record.BeforeHash ?? "", "^[A-F0-9]{64}$") ||
                    !Regex.IsMatch(record.AfterHash ?? "", "^[A-F0-9]{64}$"))
                {
                    throw new InvalidOperationException("Invalid backup entry.");
                }

                if (Hash(ReadFile(Paths.Child(backup, record.BackupFile))) != record.BeforeHash)
                {
                    throw new InvalidOperationException("Backup data is damaged: " + record.RelativePath);
                }
            }

            return journal;
        }

        private static PatchInspection RestoreCore(Product product, string root)
        {
            PatchJournal journal = LoadJournal(product, root);
            string active = ActivePath(root, product);
            string backup = Paths.Child(root, journal.BackupDirectory);
            List<string> conflicts = new List<string>();
            foreach (PatchRecord record in journal.Files)
            {
                string target = Paths.Child(root, record.RelativePath);
                string current = Hash(ReadFile(target));
                if (record.Restored)
                {
                    if (current != record.BeforeHash)
                    {
                        conflicts.Add(record.RelativePath);
                    }

                    continue;
                }

                if (current == record.AfterHash)
                {
                    EnsureClosed(product, root);
                    byte[] before = ReadFile(Paths.Child(backup, record.BackupFile));
                    if (Hash(before) != record.BeforeHash)
                    {
                        throw new InvalidOperationException("Backup data changed during restoration.");
                    }

                    ReplaceClientFile(product, root, target, before, record.AfterHash);
                    if (Hash(ReadFile(target)) != record.BeforeHash)
                    {
                        throw new InvalidOperationException("Could not restore " + record.RelativePath);
                    }

                    record.Restored = true;
                }
                else if (current == record.BeforeHash)
                {
                    record.Restored = true;
                }
                else
                {
                    conflicts.Add(record.RelativePath);
                }

                SaveJournal(active, journal);
            }

            if (conflicts.Count > 0)
            {
                journal.Status = "Conflictos; se conservaron los cambios externos";
                SaveJournal(active, journal);
                throw new InvalidOperationException("External changes were preserved: " + String.Join(", ", conflicts.ToArray()) + ". The backup remains active.");
            }

            journal.Status = "Restaurado";
            SaveJournal(active, journal);
            string archive = Paths.Child(backup, "completed.json");
            AtomicWrite(archive, Utf8.GetBytes(Json(journal)));
            File.Delete(active);
            return new PatchInspection
            {
                Product = product.ToString(),
                Root = root,
                Kind = Kind(product),
                Status = DisplayStatus(journal.Status),
                BackupPath = archive,
                Files = journal.Files
            };
        }

        private static void SaveJournal(string active, PatchJournal journal)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(active));
            AtomicWrite(active, Utf8.GetBytes(Json(journal)));
        }

        public static void AtomicWrite(string path, byte[] content)
        {
            AtomicWriteCore(path, content, null);
        }

        private static void ReplaceClientFile(Product product, string root, string path, byte[] content, string expectedHash)
        {
            AtomicWriteCore(path, content, delegate
            {
                EnsureClosed(product, root);
                Paths.RejectReparsePoints(path);
                if (Hash(ReadFile(path)) != expectedHash)
                {
                    throw new InvalidOperationException("File changed before replacement: " + Path.GetFileName(path));
                }
            });
        }

        private static void AtomicWriteCore(string path, byte[] content, Action beforeReplace)
        {
            Paths.RejectReparsePoints(path);
            string temporary = path + ".tawop-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(content, 0, content.Length);
                    stream.Flush(true);
                }

                if (beforeReplace != null)
                {
                    beforeReplace();
                }

                if (File.Exists(path))
                {
                    File.Replace(temporary, path, null);
                }
                else
                {
                    File.Move(temporary, path);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        private sealed class PatchLock : IDisposable
        {
            private readonly Mutex mutex;
            private bool acquired;
            public PatchLock(string root, Product product)
            {
                string identity = Hash(Encoding.UTF8.GetBytes(root.ToLowerInvariant() + "|" + product)).Substring(0, 32);
                mutex = new Mutex(false, @"Local\TawopMods-" + identity);
                try
                {
                    acquired = mutex.WaitOne(0);
                }
                catch (AbandonedMutexException)
                {
                    acquired = true;
                }

                if (!acquired)
                {
                    mutex.Dispose();
                    throw new InvalidOperationException("Another copy of this tool is modifying this installation.");
                }
            }

            public void Dispose()
            {
                if (acquired)
                {
                    acquired = false;
                    mutex.ReleaseMutex();
                }

                mutex.Dispose();
            }
        }
    }
}
