using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Tawop
{
    public static class SteamMod
    {
        private const string Marker = "--tawop-mod-version: \"TAWOP_MOD_V3\";";
        private const string LegacyMarker = "/* TAWOP_MOD_V1 */";
        private const long MaxStyleBytes = 64L * 1024L * 1024L;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        public static List<PatchEdit> Prepare(string installPath, string assetsPath)
        {
            string root = ValidateRoot(installPath);
            string assets = ValidateRoot(assetsPath);
            string cssDirectory = ResolvePlainPath(root, Path.Combine("steamui", "css"), true);
            string themePath = ResolvePlainPath(assets, Path.Combine("steam", "tawop.css"), false);
            string theme = DecodeStrict(ReadLimited(themePath, 256L * 1024L), themePath);
            if (theme.IndexOf(Marker, StringComparison.Ordinal) < 0)
            {
                throw new InvalidDataException("The Steam theme is missing its Tawop version marker.");
            }

            byte[] addition = StrictUtf8.GetBytes("\r\n" + theme + "\r\n");
            List<string> targets = new List<string>();
            AddExisting(targets, cssDirectory, "sp.css");
            AddExisting(targets, cssDirectory, "library.css");
            foreach (string file in Directory.GetFiles(cssDirectory, "chunk~*.css", SearchOption.TopDirectoryOnly))
            {
                targets.Add(file);
            }

            targets.Sort(StringComparer.OrdinalIgnoreCase);
            if (targets.Count > 64)
            {
                throw new InvalidDataException("Unexpected number of Steam stylesheets; this build needs review.");
            }

            List<PatchEdit> edits = new List<PatchEdit>();
            foreach (string file in targets)
            {
                string relative = Path.Combine("steamui", "css", Path.GetFileName(file));
                string safeFile = ResolvePlainPath(root, relative, false);
                byte[] original = ReadLimited(safeFile, MaxStyleBytes);
                string source = DecodeStrict(original, relative);
                if (source.IndexOf(Marker, StringComparison.Ordinal) >= 0 || source.IndexOf(LegacyMarker, StringComparison.Ordinal) >= 0)
                {
                    throw new InvalidOperationException("Tawop is already present in " + relative + ". Restore it before applying again.");
                }

                if (!HasDeclaration(source))
                {
                    continue;
                }

                byte[] content = new byte[checked(original.Length + addition.Length)];
                Buffer.BlockCopy(original, 0, content, 0, original.Length);
                Buffer.BlockCopy(addition, 0, content, original.Length, addition.Length);
                edits.Add(new PatchEdit { RelativePath = relative, Content = content, ExpectedBeforeHash = ModCore.Hash(original) });
            }

            if (edits.Count == 0)
            {
                throw new InvalidDataException("No supported Steam UI stylesheets with CSS declarations were found.");
            }

            return edits;
        }

        public static string ReadBuildVersion(string installPath)
        {
            string root = ValidateRoot(installPath);
            string directory = ResolvePlainPath(root, "steamui", true);
            string candidate = Path.Combine(directory, "changelist.txt");
            if (!File.Exists(candidate))
            {
                return "unknown";
            }

            string path = ResolvePlainPath(root, Path.Combine("steamui", "changelist.txt"), false);
            string value = DecodeStrict(ReadLimited(path, 256), path).Trim('\uFEFF', ' ', '\r', '\n', '\t');
            if (value.Length == 0 || value.Length > 32)
            {
                throw new InvalidDataException("Invalid Steam UI changelist.");
            }

            foreach (char character in value)
            {
                if (character < '0' || character > '9')
                {
                    throw new InvalidDataException("Invalid Steam UI changelist.");
                }
            }

            return value;
        }

        private static void AddExisting(List<string> targets, string directory, string name)
        {
            string candidate = Path.Combine(directory, name);
            if (File.Exists(candidate))
            {
                targets.Add(candidate);
            }
        }

        private static byte[] ReadLimited(string path, long limit)
        {
            using (FileStream input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (input.Length > limit)
                {
                    throw new InvalidDataException("File exceeds the supported size: " + Path.GetFileName(path));
                }

                using (MemoryStream output = new MemoryStream())
                {
                    byte[] buffer = new byte[8192];
                    int count;
                    while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        if (output.Length + count > limit)
                        {
                            throw new InvalidDataException("File exceeds the supported size: " + Path.GetFileName(path));
                        }

                        output.Write(buffer, 0, count);
                    }

                    return output.ToArray();
                }
            }
        }

        private static string DecodeStrict(byte[] content, string name)
        {
            try
            {
                return StrictUtf8.GetString(content);
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException("Expected strict UTF-8 in " + Path.GetFileName(name) + ".", exception);
            }
        }

        private static string ValidateRoot(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A directory path is required.", "path");
            }

            if (!Path.IsPathRooted(path))
            {
                throw new ArgumentException("An absolute directory path is required.", "path");
            }

            string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (full.Length == 2 && full[1] == ':')
            {
                full += Path.DirectorySeparatorChar;
            }

            if (!Directory.Exists(full))
            {
                throw new DirectoryNotFoundException("Directory not found: " + full);
            }

            EnsurePlainAncestors(full);
            return full;
        }

        private static string ResolvePlainPath(string root, string relative, bool directory)
        {
            if (String.IsNullOrEmpty(relative) || Path.IsPathRooted(relative) || relative.IndexOf(':') >= 0)
            {
                throw new InvalidDataException("Expected an ordinary relative path.");
            }

            string full = Path.GetFullPath(Path.Combine(root, relative));
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("A resource path escapes its selected directory.");
            }

            if (directory ? !Directory.Exists(full) : !File.Exists(full))
            {
                throw new FileNotFoundException("Required resource not found: " + relative, full);
            }

            EnsurePlainAncestors(full);
            FileAttributes attributes = File.GetAttributes(full);
            if (directory != ((attributes & FileAttributes.Directory) != 0))
            {
                throw new InvalidDataException("Unexpected resource type: " + relative);
            }

            return full;
        }

        private static void EnsurePlainAncestors(string full)
        {
            string current = full;
            while (!String.IsNullOrEmpty(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException("Links and reparse points are not supported: " + current);
                }

                string parent = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (String.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                current = parent;
            }
        }

        private static bool HasDeclaration(string source)
        {
            int depth = 0;
            int propertyStart = 0;
            char quote = '\0';
            for (int index = 0; index < source.Length; index++)
            {
                char character = source[index];
                if (quote != '\0')
                {
                    if (character == '\\')
                    {
                        index++;
                    }
                    else if (character == quote)
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (character == '/' && index + 1 < source.Length && source[index + 1] == '*')
                {
                    int end = source.IndexOf("*/", index + 2, StringComparison.Ordinal);
                    if (end < 0)
                    {
                        return false;
                    }

                    if (String.IsNullOrWhiteSpace(source.Substring(propertyStart, index - propertyStart)))
                    {
                        propertyStart = end + 2;
                    }

                    index = end + 1;
                    continue;
                }

                if (character == '\'' || character == '"')
                {
                    quote = character;
                    continue;
                }

                if (character == '{')
                {
                    depth++;
                    propertyStart = index + 1;
                    continue;
                }

                if (character == '}')
                {
                    depth--;
                    propertyStart = index + 1;
                    continue;
                }

                if (character == ';')
                {
                    propertyStart = index + 1;
                    continue;
                }

                if (character == ':' && depth > 0 && IsPropertyName(source, propertyStart, index))
                {
                    int value = index + 1;
                    while (value < source.Length)
                    {
                        if (Char.IsWhiteSpace(source[value]))
                        {
                            value++;
                            continue;
                        }

                        if (source[value] == '/' && value + 1 < source.Length && source[value + 1] == '*')
                        {
                            int end = source.IndexOf("*/", value + 2, StringComparison.Ordinal);
                            if (end < 0)
                            {
                                return false;
                            }

                            value = end + 2;
                            continue;
                        }

                        return source[value] != ';' && source[value] != '}' && source[value] != '{';
                    }
                }
            }

            return false;
        }

        private static bool IsPropertyName(string source, int start, int end)
        {
            while (start < end && Char.IsWhiteSpace(source[start]))
            {
                start++;
            }

            while (end > start && Char.IsWhiteSpace(source[end - 1]))
            {
                end--;
            }

            if (start == end)
            {
                return false;
            }

            for (int index = start; index < end; index++)
            {
                char character = source[index];
                if (!((character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z') || character == '-' || character == '_' || (index > start && character >= '0' && character <= '9')))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
