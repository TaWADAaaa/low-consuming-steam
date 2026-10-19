using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace Tawop
{
    public static class DiscordMod
    {
        public const string CssMarker = "--tawop-mod-version: \"TAWOP_MOD_V3\";";
        public const string PreloadMarker = "var tawopModVersion = \"TAWOP_MOD_V3\";";
        private const string LegacyMarker = "/* TAWOP_MOD_V1 */";
        private const string CssPlaceholder = "__TAWOP_STYLESHEET_JSON__";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public static List<PatchEdit> Prepare(string installPath, string assetsPath)
        {
            string root = ExistingDirectory(installPath);
            string assets = ExistingDirectory(assetsPath);
            VerifyElectronAsarIntegrity(SafePath(root, "Discord.exe"));
            string css = ReadText(SafePath(assets, "discord/tawop.css"), 1024 * 1024);
            string preload = ReadText(SafePath(assets, "discord/preload.js"), 1024 * 1024);
            if (css.IndexOf(CssMarker, StringComparison.Ordinal) < 0 || preload.IndexOf(PreloadMarker, StringComparison.Ordinal) < 0)
            {
                throw new InvalidDataException("Discord assets do not contain the Tawop version marker.");
            }

            int placeholder = preload.IndexOf(CssPlaceholder, StringComparison.Ordinal);
            if (placeholder < 0 || preload.IndexOf(CssPlaceholder, placeholder + CssPlaceholder.Length, StringComparison.Ordinal) >= 0)
            {
                throw new InvalidDataException("Discord preload must contain exactly one stylesheet placeholder.");
            }

            var serializer = new JavaScriptSerializer
            {
                MaxJsonLength = 2 * 1024 * 1024
            };
            preload = preload.Replace(CssPlaceholder, serializer.Serialize(css));
            var edits = new List<PatchEdit>();
            string appPath = SafePath(root, "resources/app.asar");
            byte[] appBytes = ReadBytes(appPath, AsarArchive.MaximumArchiveBytes);
            AsarArchive app = AsarArchive.Read(appBytes);
            if (app.ContainsFile("splash/index.css"))
            {
                string original = DecodeSource(app.GetFile("splash/index.css"), "splash/index.css");
                RejectPatched(original, "Discord splash");
                app.SetFile("splash/index.css", Utf8.GetBytes(original + "\n\n" + css + "\n"));
                edits.Add(new PatchEdit { RelativePath = "resources/app.asar", Content = app.Repack(), ExpectedBeforeHash = ModCore.Hash(appBytes) });
            }

            string modules = SafePath(root, "modules");
            ExistingDirectory(modules);
            string[] candidates = Directory.GetDirectories(modules, "discord_desktop_core-*", SearchOption.TopDirectoryOnly);
            Array.Sort(candidates, StringComparer.OrdinalIgnoreCase);
            if (candidates.Length == 0 || candidates.Length > 16)
            {
                throw new InvalidDataException("Expected 1 to 16 installed Discord desktop core modules.");
            }

            foreach (string candidate in candidates)
            {
                ExistingDirectory(candidate);
                string moduleName = Path.GetFileName(candidate);
                string relative = "modules/" + moduleName + "/discord_desktop_core/core.asar";
                string corePath = SafePath(root, relative);
                byte[] coreBytes = ReadBytes(corePath, AsarArchive.MaximumArchiveBytes);
                AsarArchive core = AsarArchive.Read(coreBytes);
                if (!core.ContainsFile("mainScreenPreload.js"))
                {
                    throw new InvalidDataException("Discord core has no supported mainScreenPreload.js entry.");
                }

                string original = DecodeSource(core.GetFile("mainScreenPreload.js"), "mainScreenPreload.js");
                RejectPatched(original, "Discord core");
                core.SetFile("mainScreenPreload.js", Utf8.GetBytes(original + "\n;\n" + preload + "\n"));
                edits.Add(new PatchEdit { RelativePath = relative, Content = core.Repack(), ExpectedBeforeHash = ModCore.Hash(coreBytes) });
            }

            return edits;
        }

        public static void VerifyElectronAsarIntegrity(string exePath)
        {
            EnsureNoReparsePoints(exePath);
            byte[] sentinel = Encoding.ASCII.GetBytes("dL7pKGdnNz796PbbjQWNKmHXBZaB9tsX");
            int[] prefix = new int[sentinel.Length];
            for (int i = 1, matched = 0; i < sentinel.Length; i++)
            {
                while (matched > 0 && sentinel[i] != sentinel[matched])
                {
                    matched = prefix[matched - 1];
                }

                if (sentinel[i] == sentinel[matched])
                {
                    matched++;
                }

                prefix[i] = matched;
            }

            using (var stream = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length < 16 || stream.Length >= 512L * 1024 * 1024 || stream.ReadByte() != 'M' || stream.ReadByte() != 'Z')
                {
                    throw new InvalidDataException("Discord.exe is not a supported Windows executable.");
                }

                byte[] buffer = new byte[65536];
                int matched = 0;
                int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        while (matched > 0 && buffer[i] != sentinel[matched])
                        {
                            matched = prefix[matched - 1];
                        }

                        if (buffer[i] == sentinel[matched])
                        {
                            matched++;
                        }

                        if (matched != sentinel.Length)
                        {
                            continue;
                        }

                        stream.Position = stream.Position - count + i + 1;
                        int version = stream.ReadByte();
                        int wireLength = stream.ReadByte();
                        if (version != 1 || wireLength < 5 || wireLength > 64 || stream.Length - stream.Position < wireLength)
                        {
                            throw new InvalidDataException("Unsupported Electron fuse wire. No files have been changed.");
                        }

                        for (int index = 0; index < wireLength; index++)
                        {
                            int state = stream.ReadByte();
                            if (index == 4 && state != '0')
                            {
                                throw new InvalidOperationException("Discord requires embedded ASAR integrity validation or has an unknown fuse state. This patcher will not modify its executable or bypass validation.");
                            }
                        }

                        return;
                    }
                }
            }

            throw new InvalidDataException("Electron fuse wire was not found. No files have been changed.");
        }

        private static void RejectPatched(string original, string label)
        {
            if (original.IndexOf(CssMarker, StringComparison.Ordinal) >= 0 || original.IndexOf(PreloadMarker, StringComparison.Ordinal) >= 0 || original.IndexOf(LegacyMarker, StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException(label + " already contains a Tawop patch. Restore its original backup before applying again.");
            }
        }

        private static string DecodeSource(byte[] bytes, string path)
        {
            try
            {
                return Utf8.GetString(bytes);
            }
            catch (DecoderFallbackException ex)
            {
                throw new InvalidDataException("Discord source is not valid UTF-8: " + path, ex);
            }
        }

        private static string ReadText(string path, int maximumBytes)
        {
            string result = DecodeSource(ReadBytes(path, maximumBytes), path);
            if (result.Length > 0 && result[0] == '\uFEFF')
            {
                result = result.Substring(1);
            }

            return result;
        }

        private static byte[] ReadBytes(string path, int maximumBytes)
        {
            EnsureNoReparsePoints(path);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length < 1 || stream.Length >= maximumBytes)
                {
                    throw new InvalidDataException("File is empty or exceeds the supported size: " + path);
                }

                byte[] bytes = new byte[(int)stream.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int count = stream.Read(bytes, offset, bytes.Length - offset);
                    if (count == 0)
                    {
                        throw new EndOfStreamException("File changed while reading: " + path);
                    }

                    offset += count;
                }

                return bytes;
            }
        }

        private static string ExistingDirectory(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A directory path is required.");
            }

            string full = Path.GetFullPath(path);
            if (!Directory.Exists(full))
            {
                throw new DirectoryNotFoundException(full);
            }

            EnsureNoReparsePoints(full);
            return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string SafePath(string root, string relative)
        {
            if (String.IsNullOrEmpty(relative) || Path.IsPathRooted(relative) || relative.IndexOf(':') >= 0)
            {
                throw new InvalidDataException("Unsafe Discord patch path.");
            }

            foreach (string part in relative.Replace('\\', '/').Split('/'))
            {
                if (part.Length == 0 || part == "." || part == "..")
                {
                    throw new InvalidDataException("Unsafe Discord patch path.");
                }
            }

            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(prefix, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Discord patch path leaves the selected directory.");
            }

            EnsureNoReparsePoints(path);
            return path;
        }

        private static void EnsureNoReparsePoints(string path)
        {
            string current = Path.GetFullPath(path);
            while (!String.IsNullOrEmpty(current))
            {
                if (File.Exists(current) || Directory.Exists(current))
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidDataException("Symbolic links and junctions are unsupported: " + current);
                    }
                }

                string parent = Path.GetDirectoryName(current);
                if (parent == current)
                {
                    break;
                }

                current = parent;
            }
        }
    }
}
