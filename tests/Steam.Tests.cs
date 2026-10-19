using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Tawop.Tests
{
    public static class SteamTests
    {
        public static void Run()
        {
            string temporary = Path.GetFullPath(Path.GetTempPath());
            string fixture = Path.Combine(temporary, "Tawop-SteamTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fixture);
            try
            {
                string install = Path.Combine(fixture, "Steam");
                string css = Path.Combine(install, "steamui", "css");
                string assets = Path.Combine(fixture, "assets");
                Directory.CreateDirectory(css);
                Directory.CreateDirectory(Path.Combine(assets, "steam"));
                UTF8Encoding utf8 = new UTF8Encoding(false, true);
                byte[] text = utf8.GetBytes("/* espa\u00f1ol */\r\n.Panel { color: #fff; }\r\n");
                byte[] original = new byte[text.Length + 3];
                original[0] = 0xef;
                original[1] = 0xbb;
                original[2] = 0xbf;
                Buffer.BlockCopy(text, 0, original, 3, text.Length);
                string sp = Path.Combine(css, "sp.css");
                File.WriteAllBytes(sp, original);
                File.WriteAllText(Path.Combine(css, "library.css"), ".empty { }", utf8);
                File.WriteAllText(Path.Combine(css, "chunk~a.css"), ".Hero { transition: opacity 1s; }", utf8);
                File.WriteAllText(Path.Combine(css, "chunk~empty.css"), "/* .fake { color: red; } */", utf8);
                File.WriteAllText(Path.Combine(css, "friends.css"), ".Untargeted { color: blue; }", utf8);
                string themePath = Path.Combine(assets, "steam", "tawop.css");
                string theme = ":root { --tawop-mod-version: \"TAWOP_MOD_V3\"; } body { background: transparent; }";
                File.WriteAllText(themePath, theme, utf8);
                string changelist = Path.Combine(install, "steamui", "changelist.txt");
                File.WriteAllText(changelist, "12345678\r\n", utf8);
                List<PatchEdit> edits = SteamMod.Prepare(install, assets);
                Check(edits.Count == 2, "Only existing, supported CSS files with declarations must be edited.");
                PatchEdit patched = null;
                foreach (PatchEdit edit in edits)
                {
                    Check(edit.RelativePath.StartsWith("steamui" + Path.DirectorySeparatorChar + "css" + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Edits must remain under Steam UI CSS.");
                    string output = utf8.GetString(edit.Content);
                    Check(output.EndsWith(theme + "\r\n", StringComparison.Ordinal), "The complete low-cost theme must be appended.");
                    Check(output.IndexOf("data:image", StringComparison.Ordinal) < 0, "The patch must not embed decorative image assets.");
                    string version = "--tawop-mod-version: \"TAWOP_MOD_V3\";";
                    int marker = output.IndexOf(version, StringComparison.Ordinal);
                    Check(marker >= 0 && output.IndexOf(version, marker + 1, StringComparison.Ordinal) < 0, "The marker must occur once.");
                    if (Path.GetFileName(edit.RelativePath) == "sp.css")
                    {
                        patched = edit;
                    }
                }

                Check(patched != null, "sp.css must be included.");
                for (int index = 0; index < original.Length; index++)
                {
                    Check(patched.Content[index] == original[index], "Preparation must preserve all original bytes.");
                }

                Check(Equal(File.ReadAllBytes(sp), original), "Preparation must never modify the installed client.");
                Check(SteamMod.ReadBuildVersion(install) == "12345678", "Build metadata must come from the installed changelist.");
                File.WriteAllBytes(sp, patched.Content);
                Expect<InvalidOperationException>(delegate
                {
                    SteamMod.Prepare(install, assets);
                }, "Applying twice must be rejected.");
                File.WriteAllBytes(sp, original);
                File.WriteAllText(sp, ".old { color: white; } /* TAWOP_MOD_V1 */", utf8);
                Expect<InvalidOperationException>(delegate
                {
                    SteamMod.Prepare(install, assets);
                }, "Legacy patches must require restoration before upgrading.");
                File.WriteAllBytes(sp, original);
                string invalid = Path.Combine(css, "chunk~invalid.css");
                File.WriteAllBytes(invalid, new byte[] { 0xff, 0xfe, 0x41 });
                Expect<InvalidDataException>(delegate
                {
                    SteamMod.Prepare(install, assets);
                }, "Non-UTF-8 client resources must fail closed.");
                File.Delete(invalid);
                File.WriteAllBytes(themePath, new byte[] { 0xff });
                Expect<InvalidDataException>(delegate
                {
                    SteamMod.Prepare(install, assets);
                }, "Non-UTF-8 theme assets must fail closed.");
                File.WriteAllText(themePath, theme, utf8);
                File.WriteAllText(changelist, "12345 private data", utf8);
                Expect<InvalidDataException>(delegate
                {
                    SteamMod.ReadBuildVersion(install);
                }, "Only numeric build metadata may be returned.");
                File.Delete(changelist);
                Check(SteamMod.ReadBuildVersion(install) == "unknown", "Missing changelist metadata must not be invented.");
                Expect<ArgumentException>(delegate
                {
                    SteamMod.Prepare("relative-Steam", assets);
                }, "Relative installation roots must be rejected.");
            }
            finally
            {
                string prefix = temporary.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (Path.GetFullPath(fixture).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(fixture).StartsWith("Tawop-SteamTests-", StringComparison.Ordinal) && Directory.Exists(fixture))
                {
                    Directory.Delete(fixture, true);
                }
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception("Steam test failed: " + message);
            }
        }

        private static bool Equal(byte[] first, byte[] second)
        {
            if (first.Length != second.Length)
            {
                return false;
            }

            for (int index = 0; index < first.Length; index++)
            {
                if (first[index] != second[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static void Expect<T>(Action action, string message)
            where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }

            throw new Exception("Steam test failed: " + message);
        }
    }
}
