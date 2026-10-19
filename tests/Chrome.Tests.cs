using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Tawop.Tests
{
    public static class ChromeTests
    {
        public static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "Tawop-Chrome-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "chrome/app/theme/chromium"));
                Directory.CreateDirectory(Path.Combine(root, "components/performance_manager/user_tuning"));
                string branding = "COMPANY_FULLNAME=Synthetic Authors\r\nPRODUCT_FULLNAME=Chromium\r\n" + "PRODUCT_SHORTNAME=Chromium\r\nPRODUCT_INSTALLER_FULLNAME=Chromium Installer\r\n" + "PRODUCT_INSTALLER_SHORTNAME=Chromium Installer\r\nCUSTOM_SETTING=preserved\r\n";
                string memory = Registration("kMemorySaverModeState", "MemorySaverModeState", "kDisabled");
                string aggression = Registration("kMemorySaverModeAggressiveness", "MemorySaverModeAggressiveness", "kMedium");
                string prefs = "// Synthetic source fixture\r\nvoid Register() {\r\n" + memory + aggression + "  const char* unrelated = \"keep-me\";\r\n}\r\n";
                Write(root, "chrome/app/theme/chromium/BRANDING", branding, true);
                Write(root, "components/performance_manager/user_tuning/prefs.cc", prefs, true);
                List<PatchEdit> edits = ChromeMod.Prepare(root, root);
                Assert(edits.Count == 2, "Both source files should change.");
                Assert(File.ReadAllText(Path.Combine(root, "chrome/app/theme/chromium/BRANDING")) == branding, "Prepare must not write source files.");
                foreach (PatchEdit edit in edits)
                {
                    byte[] before = File.ReadAllBytes(Path.Combine(root, edit.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
                    Assert(edit.ExpectedBeforeHash == ModCore.Hash(before), "The plan must bind to the bytes that were read.");
                    Assert(edit.Content.Length > 3 && edit.Content[0] == 0xef && edit.Content[1] == 0xbb && edit.Content[2] == 0xbf, "UTF-8 BOM must be preserved.");
                    File.WriteAllBytes(Path.Combine(root, edit.RelativePath.Replace('/', Path.DirectorySeparatorChar)), edit.Content);
                }

                Assert(ChromeMod.Prepare(root, root).Count == 0, "A repeated patch must be a no-op.");
                string patchedPrefs = File.ReadAllText(Path.Combine(root, "components/performance_manager/user_tuning/prefs.cc"));
                Assert(patchedPrefs.Contains("MemorySaverModeState::kEnabled"), "Memory saver must be enabled.");
                Assert(patchedPrefs.Contains("MemorySaverModeAggressiveness::kAggressive"), "Aggressiveness must change.");
                Assert(patchedPrefs.Contains("keep-me"), "Unrelated source must remain.");
                Assert(!patchedPrefs.Replace("\r\n", "").Contains("\n"), "CRLF must be preserved.");
                Assert(File.ReadAllText(Path.Combine(root, "chrome/app/theme/chromium/BRANDING")).Contains("CUSTOM_SETTING=preserved"), "Unknown branding keys must remain.");
                Write(root, "components/performance_manager/user_tuning/prefs.cc", prefs.Replace(memory, memory + memory), false);
                MustFail(delegate
                {
                    ChromeMod.Prepare(root, root);
                }, "Duplicate registration must be rejected.");
                Write(root, "components/performance_manager/user_tuning/prefs.cc", prefs.Replace("::kMedium", "::kConservative"), false);
                MustFail(delegate
                {
                    ChromeMod.Prepare(root, root);
                }, "Unknown defaults must be rejected.");
                Write(root, "components/performance_manager/user_tuning/prefs.cc", prefs, false);
                MustFail(delegate
                {
                    ChromeMod.Prepare(root, root);
                }, "Tawop branding with original defaults must be rejected.");
                Write(root, "chrome/app/theme/chromium/BRANDING", branding.Replace("PRODUCT_FULLNAME=Chromium", "PRODUCT_FULLNAME=ChromeTawop"), false);
                MustFail(delegate
                {
                    ChromeMod.Prepare(root, root);
                }, "Partially changed branding must be rejected.");
                Write(root, "chrome/app/theme/chromium/BRANDING", branding, false);
                Write(root, "components/performance_manager/user_tuning/prefs.cc", patchedPrefs, false);
                MustFail(delegate
                {
                    ChromeMod.Prepare(root, root);
                }, "Changed defaults without Tawop branding must not be adopted.");
                Write(root, "components/performance_manager/user_tuning/prefs.cc", prefs, false);
                Write(root, "chrome/app/theme/chromium/BRANDING", branding.Replace("PRODUCT_FULLNAME=Chromium", "PRODUCT_FULLNAME=OtherFork"), false);
                MustFail(delegate
                {
                    ChromeMod.Prepare(root, root);
                }, "Another fork's branding must be preserved.");
                MustFail(delegate
                {
                    ChromeMod.Prepare(Path.Combine(root, "missing"), root);
                }, "Installed binaries are not source checkouts.");
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static string Registration(string preference, string enumName, string value)
        {
            return "  registry->RegisterIntegerPref(" + preference + ", static_cast<int>(" + enumName + "::" + value + "));\r\n";
        }

        private static void Write(string root, string relative, string text, bool bom)
        {
            File.WriteAllText(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)), text, new UTF8Encoding(bom));
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception("ChromeTests: " + message);
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
            catch (FileNotFoundException)
            {
                return;
            }

            throw new Exception("ChromeTests: " + message);
        }
    }
}
