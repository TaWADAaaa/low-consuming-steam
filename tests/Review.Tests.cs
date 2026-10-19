using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace Tawop.Tests
{
    public static class ReviewTests
    {
        public static void Run()
        {
            using (Fixture fixture = new Fixture())
            {
                MustFail(delegate
                {
                    Paths.Child(fixture.Root, "../outside.txt");
                }, "Traversal should fail.");
                MustFail(delegate
                {
                    Paths.Root(Path.GetPathRoot(fixture.Root));
                }, "Disk roots should fail.");
                PatchInspection applied = ModCore.Apply(Product.Chrome, fixture.Root, fixture.Root);
                string beforeBranding = File.ReadAllText(fixture.Branding);
                string beforePrefs = File.ReadAllText(fixture.Prefs);
                PatchJournal journal = fixture.Journal();
                string backup = Path.Combine(fixture.Root, journal.BackupDirectory, journal.Files[0].BackupFile);
                File.WriteAllBytes(backup, new byte[] { 1, 2, 3 });
                MustFail(delegate
                {
                    ModCore.Restore(Product.Chrome, fixture.Root);
                }, "Corrupt backup should fail.");
                Assert(File.ReadAllText(fixture.Branding) == beforeBranding && File.ReadAllText(fixture.Prefs) == beforePrefs, "A corrupt backup must not change any target.");
                Assert(applied.Status == "Aplicado", "Fixture should have applied before the corruption test.");
            }

            using (Fixture fixture = new Fixture())
            {
                ModCore.Apply(Product.Chrome, fixture.Root, fixture.Root);
                PatchJournal journal = fixture.Journal();
                journal.Files[0].RelativePath = "..\\outside.txt";
                File.WriteAllText(fixture.ActiveJournal, ModCore.Json(journal), new UTF8Encoding(false));
                string current = File.ReadAllText(fixture.Branding);
                MustFail(delegate
                {
                    ModCore.Restore(Product.Chrome, fixture.Root);
                }, "A forged target path should fail.");
                Assert(File.ReadAllText(fixture.Branding) == current, "An invalid journal must not alter targets.");
            }

            using (Fixture fixture = new Fixture())
            {
                ModCore.Apply(Product.Chrome, fixture.Root, fixture.Root);
                string patchedBranding = File.ReadAllText(fixture.Branding);
                string externalBranding = patchedBranding + "EXTERNAL_CHANGE=preserve\n";
                File.WriteAllText(fixture.Branding, externalBranding, new UTF8Encoding(false));
                MustFail(delegate
                {
                    ModCore.Restore(Product.Chrome, fixture.Root);
                }, "An external update must prevent a full restore.");
                Assert(File.ReadAllText(fixture.Branding) == externalBranding, "An external update must not be overwritten.");
                Assert(File.ReadAllText(fixture.Prefs) == fixture.OriginalPrefs, "The nonconflicting file should be restored.");
                File.WriteAllText(fixture.Branding, patchedBranding, new UTF8Encoding(false));
                string externalPrefs = fixture.OriginalPrefs + "// external source update\n";
                File.WriteAllText(fixture.Prefs, externalPrefs, new UTF8Encoding(false));
                MustFail(delegate
                {
                    ModCore.Restore(Product.Chrome, fixture.Root);
                }, "A previously restored file changed externally: full restore must still report conflict.");
                Assert(File.ReadAllText(fixture.Prefs) == externalPrefs, "Retry must preserve new external changes.");
                Assert(File.Exists(fixture.ActiveJournal), "The active journal must remain while any conflict exists.");
            }
        }

        private static void MustFail(Action action, string message)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                return;
            }

            throw new Exception("ReviewTests: " + message);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception("ReviewTests: " + message);
            }
        }

        private sealed class Fixture : IDisposable
        {
            public readonly string Root;
            public readonly string Branding;
            public readonly string Prefs;
            public readonly string ActiveJournal;
            public readonly string OriginalPrefs;
            public Fixture()
            {
                Root = Path.Combine(Path.GetTempPath(), "Tawop-review-fixture-" + Guid.NewGuid().ToString("N"));
                Branding = Path.Combine(Root, "chrome/app/theme/chromium/BRANDING");
                Prefs = Path.Combine(Root, "components/performance_manager/user_tuning/prefs.cc");
                ActiveJournal = Path.Combine(Root, ".tawop/chrome.json");
                Directory.CreateDirectory(Path.GetDirectoryName(Branding));
                Directory.CreateDirectory(Path.GetDirectoryName(Prefs));
                File.WriteAllText(Branding, "PRODUCT_FULLNAME=Chromium\nPRODUCT_SHORTNAME=Chromium\n" + "PRODUCT_INSTALLER_FULLNAME=Chromium Installer\nPRODUCT_INSTALLER_SHORTNAME=Chromium Installer\n", new UTF8Encoding(false));
                OriginalPrefs = "void SyntheticRegistration() {\n" + Registration("kMemorySaverModeState", "MemorySaverModeState", "kDisabled") + Registration("kMemorySaverModeAggressiveness", "MemorySaverModeAggressiveness", "kMedium") + "}\n";
                File.WriteAllText(Prefs, OriginalPrefs, new UTF8Encoding(false));
            }

            public PatchJournal Journal()
            {
                return new JavaScriptSerializer().Deserialize<PatchJournal>(File.ReadAllText(ActiveJournal));
            }

            public void Dispose()
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, true);
                }
            }

            private static string Registration(string preference, string type, string value)
            {
                return "  registry->RegisterIntegerPref(" + preference + ", static_cast<int>(" + type + "::" + value + "));\n";
            }
        }
    }
}
