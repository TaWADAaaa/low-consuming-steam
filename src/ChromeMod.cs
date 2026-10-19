using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Tawop
{
    public static class ChromeMod
    {
        private const string BrandingPath = "chrome/app/theme/chromium/BRANDING";
        private const string PrefsPath = "components/performance_manager/user_tuning/prefs.cc";
        public static List<PatchEdit> Prepare(string installPath, string assetsPath)
        {
            OperationControl.Report("Preparing Chromium memory saver defaults...");
            if (String.IsNullOrWhiteSpace(installPath))
            {
                throw new ArgumentException("Choose the src folder of a Chromium source checkout.");
            }

            string root = Path.GetFullPath(installPath);
            SourceText branding = ReadSource(root, BrandingPath);
            SourceText prefs = ReadSource(root, PrefsPath);
            bool alreadyPatched = HasTawopBranding(branding.Text);
            string updatedBranding = branding.Text;
            updatedBranding = SetBranding(updatedBranding, "PRODUCT_FULLNAME", "Chromium", "ChromeTawop");
            updatedBranding = SetBranding(updatedBranding, "PRODUCT_SHORTNAME", "Chromium", "ChromeTawop");
            updatedBranding = SetBranding(updatedBranding, "PRODUCT_INSTALLER_FULLNAME", "Chromium Installer", "ChromeTawop Installer");
            updatedBranding = SetBranding(updatedBranding, "PRODUCT_INSTALLER_SHORTNAME", "Chromium Installer", "ChromeTawop Installer");
            string updatedPrefs = prefs.Text;
            updatedPrefs = SetDefault(updatedPrefs, "kMemorySaverModeState", "MemorySaverModeState", "kDisabled", "kEnabled", alreadyPatched);
            updatedPrefs = SetDefault(updatedPrefs, "kMemorySaverModeAggressiveness", "MemorySaverModeAggressiveness", "kMedium", "kAggressive", alreadyPatched);
            List<PatchEdit> edits = new List<PatchEdit>();
            AddIfChanged(edits, BrandingPath, branding, updatedBranding);
            AddIfChanged(edits, PrefsPath, prefs, updatedPrefs);
            return edits;
        }

        private static bool HasTawopBranding(string text)
        {
            string[] keys =
            {
                "PRODUCT_FULLNAME",
                "PRODUCT_SHORTNAME",
                "PRODUCT_INSTALLER_FULLNAME",
                "PRODUCT_INSTALLER_SHORTNAME"
            };
            int tawopValues = 0;
            foreach (string key in keys)
            {
                OperationControl.Check();
                Regex line = new Regex("^" + Regex.Escape(key) + "=([^\\r\\n]*)(\\r?)$", RegexOptions.Multiline);
                MatchCollection values = line.Matches(text);
                if (values.Count != 1)
                {
                    throw new InvalidOperationException("Unsupported BRANDING file: expected one " + key + ".");
                }

                string expected = key.IndexOf("INSTALLER", StringComparison.Ordinal) >= 0 ? "ChromeTawop Installer" : "ChromeTawop";
                if (values[0].Groups[1].Value == expected)
                {
                    tawopValues++;
                }
            }

            if (tawopValues != 0 && tawopValues != keys.Length)
            {
                throw new InvalidOperationException("BRANDING contains an incomplete patch. Undo the changes before continuing.");
            }

            return tawopValues == keys.Length;
        }

        private static string SetBranding(string text, string key, string original, string replacement)
        {
            Regex line = new Regex("^(" + Regex.Escape(key) + "=)([^\\r\\n]*)(\\r?)$", RegexOptions.Multiline);
            MatchCollection found = line.Matches(text);
            if (found.Count != 1)
            {
                throw new InvalidOperationException("Unsupported BRANDING file: expected one " + key + ".");
            }

            string value = found[0].Groups[2].Value;
            if (value == replacement)
            {
                return text;
            }

            if (value != original)
            {
                throw new InvalidOperationException("BRANDING already has a different value for " + key + ". It was left unchanged.");
            }

            return line.Replace(text, delegate (Match match)
            {
                return match.Groups[1].Value + replacement + match.Groups[3].Value;
            }, 1);
        }

        private static string SetDefault(string text, string preference, string enumName, string original, string replacement, bool alreadyPatched)
        {
            string prefix = @"\bRegisterIntegerPref\(\s*" + Regex.Escape(preference) + @"\s*,\s*static_cast<int>\(" + Regex.Escape(enumName) + @"::";
            Regex registration = new Regex("(" + prefix + @")([A-Za-z0-9_]+)(\)\s*\)\s*;)");
            MatchCollection found = registration.Matches(text);
            if (found.Count != 1)
            {
                throw new InvalidOperationException("Unsupported Chromium source: ambiguous or missing registration for " + preference + ".");
            }

            string current = found[0].Groups[2].Value;
            if (alreadyPatched)
            {
                if (current != replacement)
                {
                    throw new InvalidOperationException("The patched branding does not match " + preference + ". Nothing was changed.");
                }

                return text;
            }

            if (current != original)
            {
                throw new InvalidOperationException("The default for " + preference + " changed in this version. It was left unchanged.");
            }

            return registration.Replace(text, delegate (Match match)
            {
                return match.Groups[1].Value + replacement + match.Groups[3].Value;
            }, 1);
        }

        private static SourceText ReadSource(string root, string relative)
        {
            string full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                throw new FileNotFoundException("Chromium source code is required. Installed Google Chrome cannot be used: " + relative, full);
            }

            FileInfo info = new FileInfo(full);
            if (info.Length > 2 * 1024 * 1024)
            {
                throw new InvalidOperationException("Source file exceeds the expected size: " + relative);
            }

            byte[] bytes = ModCore.ReadFile(full);
            bool bom = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf;
            int offset = bom ? 3 : 0;
            string text = new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
            return new SourceText
            {
                Text = text,
                Bom = bom,
                OriginalBytes = bytes
            };
        }

        private static void AddIfChanged(List<PatchEdit> edits, string path, SourceText source, string text)
        {
            if (text == source.Text)
            {
                return;
            }

            UTF8Encoding encoding = new UTF8Encoding(source.Bom, true);
            byte[] body = encoding.GetBytes(text);
            byte[] preamble = encoding.GetPreamble();
            byte[] bytes = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
            edits.Add(new PatchEdit { RelativePath = path, Content = bytes, ExpectedBeforeHash = ModCore.Hash(source.OriginalBytes) });
        }

        private sealed class SourceText
        {
            public string Text;
            public bool Bom;
            public byte[] OriginalBytes;
        }
    }
}
