using System;
using System.IO;
using System.Text;

namespace Tawop.Tests
{
    public static class CoreTests
    {
        private static string assets;
        public static void Run(string packageRoot)
        {
            assets = Path.Combine(packageRoot, "assets");
            string parent = Path.Combine(Path.GetTempPath(), "TawopCore-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(parent);
            try
            {
                RoundTrip(Path.Combine(parent, "roundtrip"));
                Conflict(Path.Combine(parent, "conflict"));
                PartialFailure(Path.Combine(parent, "partial"));
                Tamper(Path.Combine(parent, "tamper"));
                bool rejected = false;
                try
                {
                    Paths.Child(parent, @"..\escape");
                }
                catch
                {
                    rejected = true;
                }

                Assert(rejected, "Path traversal was accepted.");
            }
            finally
            {
                string expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), Path.GetFileName(parent)));
                if (!String.Equals(expected, Path.GetFullPath(parent), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(parent).StartsWith("TawopCore-", StringComparison.Ordinal))
                {
                    throw new Exception("Unexpected cleanup target.");
                }

                Directory.Delete(parent, true);
            }
        }

        private static void Fixture(string root)
        {
            Directory.CreateDirectory(Path.Combine(root, @"steamui\css"));
            SharedFixtures.WriteSteamBundle(root);
            File.WriteAllText(Path.Combine(root, @"steamui\css\library.css"), "body { color: black; }\r\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root, @"steamui\css\sp.css"), ".box { transition: all 1s; }\n", new UTF8Encoding(false));
        }

        private static string Css(string root, string name)
        {
            return Path.Combine(root, @"steamui\css\" + name);
        }

        private static void RoundTrip(string root)
        {
            Fixture(root);
            byte[] library = File.ReadAllBytes(Css(root, "library.css"));
            byte[] sp = File.ReadAllBytes(Css(root, "sp.css"));
            PatchInspection plan = ModCore.Inspect(Product.Steam, root, assets);
            Assert(plan.Files.Count == 3 && !Directory.Exists(Path.Combine(root, ".tawop")), "Inspection mutated fixture.");
            PatchInspection applied = ModCore.Apply(Product.Steam, root, assets);
            Assert(applied.Status == "Applied" && ModCore.Hash(File.ReadAllBytes(Css(root, "library.css"))) != ModCore.Hash(library), "No actual CSS mutation.");
            Assert(ModCore.Inspect(Product.Steam, root, assets).Status == "Applied", "An active legacy-compatible journal must be displayed in English.");
            bool blocked = false;
            try
            {
                ModCore.Apply(Product.Steam, root, assets);
            }
            catch
            {
                blocked = true;
            }

            Assert(blocked, "Second apply overwrote original backup.");
            PatchInspection restored = ModCore.Restore(Product.Steam, root);
            Assert(restored.Status == "Restored" && ModCore.Hash(File.ReadAllBytes(Css(root, "library.css"))) == ModCore.Hash(library) && ModCore.Hash(File.ReadAllBytes(Css(root, "sp.css"))) == ModCore.Hash(sp), "Original bytes were not restored.");
            Assert(!File.Exists(Path.Combine(root, @".tawop\steam.json")), "Restored journal stayed active.");
        }

        private static void Conflict(string root)
        {
            Fixture(root);
            byte[] original = File.ReadAllBytes(Css(root, "sp.css"));
            ModCore.Apply(Product.Steam, root, assets);
            File.WriteAllText(Css(root, "sp.css"), "/* another updater */", new UTF8Encoding(false));
            bool blocked = false;
            try
            {
                ModCore.Restore(Product.Steam, root);
            }
            catch
            {
                blocked = true;
            }

            Assert(blocked && File.ReadAllText(Css(root, "sp.css")) == "/* another updater */", "External change was overwritten.");
            Assert(File.Exists(Path.Combine(root, @".tawop\steam.json")), "Conflict lost recovery journal.");
            File.WriteAllBytes(Css(root, "sp.css"), original);
            ModCore.Restore(Product.Steam, root);
        }

        private static void PartialFailure(string root)
        {
            Fixture(root);
            byte[] library = File.ReadAllBytes(Css(root, "library.css"));
            byte[] sp = File.ReadAllBytes(Css(root, "sp.css"));
            bool failed = false;
            using (FileStream locked = new FileStream(Css(root, "sp.css"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                try
                {
                    ModCore.Apply(Product.Steam, root, assets);
                }
                catch
                {
                    failed = true;
                }
            }

            Assert(failed, "Expected a locked second file to reject atomic replacement.");
            Assert(ModCore.Hash(File.ReadAllBytes(Css(root, "library.css"))) == ModCore.Hash(library) && ModCore.Hash(File.ReadAllBytes(Css(root, "sp.css"))) == ModCore.Hash(sp), "Partial failure was not rolled back.");
            Assert(!File.Exists(Path.Combine(root, @".tawop\steam.json")), "Clean rollback left active journal.");
        }

        private static void Tamper(string root)
        {
            Fixture(root);
            PatchInspection applied = ModCore.Apply(Product.Steam, root, assets);
            string active = Path.Combine(root, @".tawop\steam.json");
            System.Web.Script.Serialization.JavaScriptSerializer serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
            PatchJournal journal = serializer.Deserialize<PatchJournal>(File.ReadAllText(active));
            string backup = Path.Combine(root, journal.BackupDirectory, journal.Files[0].BackupFile);
            byte[] before = File.ReadAllBytes(backup);
            File.WriteAllBytes(backup, new byte[] { 1, 2, 3 });
            bool rejected = false;
            try
            {
                ModCore.Restore(Product.Steam, root);
            }
            catch
            {
                rejected = true;
            }

            Assert(rejected && ModCore.Hash(File.ReadAllBytes(Css(root, "library.css"))) == applied.Files[0].AfterHash, "Tampered backup changed installation.");
            File.WriteAllBytes(backup, before);
            ModCore.Restore(Product.Steam, root);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }
    }
}
