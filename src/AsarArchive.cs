using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace Tawop
{
    public sealed class AsarArchive
    {
        public const int MaximumArchiveBytes = 256 * 1024 * 1024;
        private const int MaximumHeaderBytes = 16 * 1024 * 1024;
        private const int DefaultBlockSize = 4 * 1024 * 1024;
        private const int MaximumEntries = 100000;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly Dictionary<string, object> root;
        private readonly List<Entry> entries = new List<Entry>();
        private readonly Dictionary<string, Entry> byPath = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private long totalMemberBytes;
        private sealed class Entry
        {
            internal string Path;
            internal Dictionary<string, object> Node;
            internal byte[] Data;
            internal int DataOffset;
            internal int Length;
            internal long OriginalOffset;
            internal int BlockSize;
        }

        private AsarArchive(Dictionary<string, object> rootNode)
        {
            root = rootNode;
        }

        public static AsarArchive Read(byte[] data)
        {
            OperationControl.Check();
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }

            if (data.Length < 16 || data.Length >= MaximumArchiveBytes)
            {
                throw new InvalidDataException("ASAR size must be at least 16 bytes and less than 256 MiB.");
            }

            long headerBufferSize = ReadUInt32(data, 4);
            long headerPayloadSize = ReadUInt32(data, 8);
            long jsonSize = ReadUInt32(data, 12);
            long payloadBase = 8 + headerBufferSize;
            if (ReadUInt32(data, 0) != 4 || headerBufferSize < 8 || headerBufferSize % 4 != 0 || headerPayloadSize != headerBufferSize - 4 || jsonSize < 2 || jsonSize > MaximumHeaderBytes || headerPayloadSize != Align4(4 + jsonSize) || payloadBase > data.Length || 16 + jsonSize > payloadBase)
            {
                throw new InvalidDataException("Invalid ASAR Pickle header.");
            }

            for (long i = 16 + jsonSize; i < payloadBase; i++)
            {
                if (data[(int)i] != 0)
                {
                    throw new InvalidDataException("Nonzero ASAR header padding.");
                }
            }

            Dictionary<string, object> parsedRoot;
            try
            {
                parsedRoot = Serializer().DeserializeObject(Utf8.GetString(data, 16, (int)jsonSize)) as Dictionary<string, object>;
            }
            catch (Exception ex)
            {
                if (!(ex is ArgumentException) && !(ex is InvalidOperationException) && !(ex is DecoderFallbackException))
                {
                    throw;
                }

                throw new InvalidDataException("Invalid ASAR JSON header.", ex);
            }

            if (parsedRoot == null)
            {
                throw new InvalidDataException("ASAR root must be an object.");
            }

            var archive = new AsarArchive(parsedRoot);
            archive.ReadDirectory(GetDirectory(parsedRoot), "", 0, data, payloadBase);
            var ranges = new List<Entry>(archive.entries);
            ranges.Sort(delegate (Entry a, Entry b)
            {
                return a.OriginalOffset.CompareTo(b.OriginalOffset);
            });
            long previousEnd = 0;
            foreach (Entry entry in ranges)
            {
                OperationControl.Check();
                if (entry.Length == 0)
                {
                    continue;
                }

                if (entry.OriginalOffset < previousEnd)
                {
                    throw new InvalidDataException("Overlapping ASAR file data.");
                }

                previousEnd = entry.OriginalOffset + entry.Length;
            }

            return archive;
        }

        public static AsarArchive Read(byte[] data, CancellationToken cancellation)
        {
            using (OperationControl.Enter(cancellation, null))
            {
                return Read(data);
            }
        }

        public bool ContainsFile(string path)
        {
            ValidatePath(path);
            return byPath.ContainsKey(path);
        }

        public byte[] GetFile(string path)
        {
            ValidatePath(path);
            Entry entry;
            if (!byPath.TryGetValue(path, out entry))
            {
                throw new FileNotFoundException("ASAR file not found.", path);
            }

            byte[] result = new byte[entry.Length];
            Copy(entry.Data, entry.DataOffset, result, 0, result.Length);
            return result;
        }

        public void SetFile(string path, byte[] content)
        {
            ValidatePath(path);
            if (content == null)
            {
                throw new ArgumentNullException("content");
            }

            if (content.Length >= MaximumArchiveBytes)
            {
                throw new InvalidDataException("ASAR member is too large.");
            }

            Entry entry;
            if (!byPath.TryGetValue(path, out entry))
            {
                throw new FileNotFoundException("ASAR file not found.", path);
            }

            entry.Data = (byte[])content.Clone();
            entry.DataOffset = 0;
            entry.Length = content.Length;
        }

        public byte[] Repack()
        {
            long dataLength = 0;
            foreach (Entry entry in entries)
            {
                OperationControl.Check();
                entry.Node["size"] = entry.Length;
                entry.Node["offset"] = dataLength.ToString(CultureInfo.InvariantCulture);
                entry.Node["integrity"] = MakeIntegrity(entry.Data, entry.DataOffset, entry.Length, entry.BlockSize);
                dataLength += entry.Length;
                if (dataLength >= MaximumArchiveBytes)
                {
                    throw new InvalidDataException("Repacked ASAR is too large.");
                }
            }

            byte[] json = Utf8.GetBytes(Serializer().Serialize(root));
            if (json.Length > MaximumHeaderBytes)
            {
                throw new InvalidDataException("Repacked ASAR header is too large.");
            }

            long headerPayloadSize = Align4(4L + json.Length);
            long headerBufferSize = 4 + headerPayloadSize;
            long payloadBase = 8 + headerBufferSize;
            long totalLength = payloadBase + dataLength;
            if (totalLength >= MaximumArchiveBytes)
            {
                throw new InvalidDataException("Repacked ASAR is too large.");
            }

            byte[] result = new byte[(int)totalLength];
            WriteUInt32(result, 0, 4);
            WriteUInt32(result, 4, (uint)headerBufferSize);
            WriteUInt32(result, 8, (uint)headerPayloadSize);
            WriteUInt32(result, 12, (uint)json.Length);
            Buffer.BlockCopy(json, 0, result, 16, json.Length);
            int position = (int)payloadBase;
            foreach (Entry entry in entries)
            {
                OperationControl.Check();
                Copy(entry.Data, entry.DataOffset, result, position, entry.Length);
                position += entry.Length;
            }

            Read(result);
            return result;
        }

        public byte[] Repack(CancellationToken cancellation)
        {
            using (OperationControl.Enter(cancellation, null))
            {
                return Repack();
            }
        }

        public static byte[] Create(IDictionary<string, byte[]> files)
        {
            if (files == null)
            {
                throw new ArgumentNullException("files");
            }

            var rootNode = new Dictionary<string, object>();
            var rootFiles = new Dictionary<string, object>(StringComparer.Ordinal);
            rootNode["files"] = rootFiles;
            var archive = new AsarArchive(rootNode);
            var paths = new List<string>(files.Keys);
            paths.Sort(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                OperationControl.Check();
                ValidatePath(path);
                byte[] content = files[path];
                if (content == null || content.Length >= MaximumArchiveBytes)
                {
                    throw new InvalidDataException("Invalid ASAR fixture member.");
                }

                if (archive.entries.Count >= MaximumEntries)
                {
                    throw new InvalidDataException("Too many ASAR entries.");
                }

                string[] parts = path.Split('/');
                Dictionary<string, object> directory = rootFiles;
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    object nodeValue;
                    if (!directory.TryGetValue(parts[i], out nodeValue))
                    {
                        nodeValue = new Dictionary<string, object>
                        {
                            {
                                "files",
                                new Dictionary<string, object>(StringComparer.Ordinal)
                            }
                        };
                        directory.Add(parts[i], nodeValue);
                    }

                    var node = nodeValue as Dictionary<string, object>;
                    if (node == null || !node.ContainsKey("files"))
                    {
                        throw new InvalidDataException("ASAR file/directory name collision.");
                    }

                    directory = GetDirectory(node);
                }

                string name = parts[parts.Length - 1];
                if (directory.ContainsKey(name))
                {
                    throw new InvalidDataException("Duplicate ASAR path.");
                }

                var fileNode = new Dictionary<string, object>();
                directory.Add(name, fileNode);
                var entry = new Entry
                {
                    Path = path,
                    Node = fileNode,
                    Data = (byte[])content.Clone(),
                    Length = content.Length,
                    BlockSize = DefaultBlockSize
                };
                archive.entries.Add(entry);
                archive.byPath.Add(path, entry);
            }

            return archive.Repack();
        }

        private void ReadDirectory(Dictionary<string, object> files, string prefix, int depth, byte[] archive, long payloadBase)
        {
            if (depth > 64)
            {
                throw new InvalidDataException("ASAR directories are too deeply nested.");
            }

            var windowsNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, object> pair in files)
            {
                OperationControl.Check();
                ValidateSegment(pair.Key);
                if (!windowsNames.Add(pair.Key))
                {
                    throw new InvalidDataException("Case-colliding ASAR paths are unsupported.");
                }

                string path = prefix + pair.Key;
                var node = pair.Value as Dictionary<string, object>;
                if (node == null || node.ContainsKey("link") || node.ContainsKey("unpacked"))
                {
                    throw new InvalidDataException("ASAR links and unpacked members are unsupported.");
                }

                if (node.ContainsKey("files"))
                {
                    if (node.ContainsKey("size") || node.ContainsKey("offset"))
                    {
                        throw new InvalidDataException("Invalid mixed ASAR directory/file.");
                    }

                    ReadDirectory(GetDirectory(node), path + "/", depth + 1, archive, payloadBase);
                    continue;
                }

                if (entries.Count >= MaximumEntries)
                {
                    throw new InvalidDataException("Too many ASAR entries.");
                }

                long size = Integer(GetRequired(node, "size"), "size");
                string offsetText = GetRequired(node, "offset") as string;
                long offset;
                if (offsetText == null || offsetText.Length == 0 || offsetText.Length > 15 || !DigitsOnly(offsetText) || !Int64.TryParse(offsetText, NumberStyles.None, CultureInfo.InvariantCulture, out offset))
                {
                    throw new InvalidDataException("Invalid ASAR file offset.");
                }

                if (size < 0 || size >= MaximumArchiveBytes || offset < 0 || offset > archive.Length - payloadBase || size > archive.Length - payloadBase - offset)
                {
                    throw new InvalidDataException("ASAR member points outside the archive.");
                }

                totalMemberBytes += size;
                if (totalMemberBytes >= MaximumArchiveBytes)
                {
                    throw new InvalidDataException("Total ASAR member size exceeds the supported limit.");
                }

                int dataOffset = (int)(payloadBase + offset);
                int blockSize = VerifyIntegrity(GetRequired(node, "integrity"), archive, dataOffset, (int)size, path);
                var entry = new Entry
                {
                    Path = path,
                    Node = node,
                    Data = archive,
                    DataOffset = dataOffset,
                    Length = (int)size,
                    OriginalOffset = offset,
                    BlockSize = blockSize
                };
                entries.Add(entry);
                byPath.Add(path, entry);
            }
        }

        private static int VerifyIntegrity(object value, byte[] content, int dataOffset, int contentLength, string path)
        {
            var integrity = value as Dictionary<string, object>;
            if (integrity == null || !String.Equals(GetRequired(integrity, "algorithm") as string, "SHA256", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Missing or unsupported ASAR integrity: " + path);
            }

            long blockSizeValue = Integer(GetRequired(integrity, "blockSize"), "blockSize");
            if (blockSizeValue < 1 || blockSizeValue > MaximumHeaderBytes)
            {
                throw new InvalidDataException("Invalid ASAR integrity block size.");
            }

            int blockSize = (int)blockSizeValue;
            string expected = GetRequired(integrity, "hash") as string;
            if (!EqualHash(expected, Hash(content, dataOffset, contentLength)))
            {
                throw new InvalidDataException("ASAR integrity hash mismatch: " + path);
            }

            var blocks = GetRequired(integrity, "blocks") as IList;
            int count = (contentLength + blockSize - 1) / blockSize;
            if (blocks == null || blocks.Count != count)
            {
                throw new InvalidDataException("ASAR integrity block count mismatch: " + path);
            }

            for (int i = 0; i < count; i++)
            {
                OperationControl.Check();
                int start = i * blockSize;
                int length = Math.Min(blockSize, contentLength - start);
                if (!EqualHash(blocks[i] as string, Hash(content, dataOffset + start, length)))
                {
                    throw new InvalidDataException("ASAR integrity block mismatch: " + path);
                }
            }

            return blockSize;
        }

        private static Dictionary<string, object> MakeIntegrity(byte[] content, int dataOffset, int contentLength, int blockSize)
        {
            var blocks = new List<string>();
            for (int offset = 0; offset < contentLength; offset += blockSize)
            {
                OperationControl.Check();
                blocks.Add(Hash(content, dataOffset + offset, Math.Min(blockSize, contentLength - offset)));
            }

            return new Dictionary<string, object>
            {
                {
                    "algorithm",
                    "SHA256"
                },
                {
                    "hash",
                    Hash(content, dataOffset, contentLength)
                },
                {
                    "blockSize",
                    blockSize
                },
                {
                    "blocks",
                    blocks
                }
            };
        }

        private static string Hash(byte[] content, int offset, int count)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                int end = offset + count;
                for (int position = offset; position < end; position += 65536)
                {
                    OperationControl.Check();
                    algorithm.TransformBlock(content, position, Math.Min(65536, end - position), null, 0);
                }

                OperationControl.Check();
                algorithm.TransformFinalBlock(new byte[0], 0, 0);
                return BitConverter.ToString(algorithm.Hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static void Copy(byte[] source, int sourceOffset, byte[] target, int targetOffset, int count)
        {
            for (int copied = 0; copied < count; copied += 65536)
            {
                OperationControl.Check();
                int length = Math.Min(65536, count - copied);
                Buffer.BlockCopy(source, sourceOffset + copied, target, targetOffset + copied, length);
            }
        }

        private static bool EqualHash(string expected, string actual)
        {
            if (expected == null || expected.Length != 64)
            {
                return false;
            }

            return String.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }

        private static object GetRequired(Dictionary<string, object> node, string name)
        {
            object value;
            if (!node.TryGetValue(name, out value) || value == null)
            {
                throw new InvalidDataException("Missing ASAR field: " + name);
            }

            return value;
        }

        private static Dictionary<string, object> GetDirectory(Dictionary<string, object> node)
        {
            var result = GetRequired(node, "files") as Dictionary<string, object>;
            if (result == null)
            {
                throw new InvalidDataException("ASAR files must be an object.");
            }

            return result;
        }

        private static long Integer(object value, string name)
        {
            if (!(value is int) && !(value is long))
            {
                throw new InvalidDataException("ASAR " + name + " must be an integer.");
            }

            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        private static bool DigitsOnly(string value)
        {
            foreach (char ch in value)
            {
                if (ch < '0' || ch > '9')
                {
                    return false;
                }
            }

            return true;
        }

        private static void ValidatePath(string path)
        {
            if (String.IsNullOrEmpty(path) || path.Length > 4096)
            {
                throw new InvalidDataException("Invalid ASAR path.");
            }

            string[] parts = path.Split('/');
            if (parts.Length > 65)
            {
                throw new InvalidDataException("ASAR path is too deeply nested.");
            }

            foreach (string part in parts)
            {
                ValidateSegment(part);
            }
        }

        private static void ValidateSegment(string name)
        {
            if (String.IsNullOrEmpty(name) || name == "." || name == ".." || name.Length > 255 || name.EndsWith(".", StringComparison.Ordinal) || name.EndsWith(" ", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Unsafe ASAR path segment.");
            }

            foreach (char ch in name)
            {
                if (ch < 32 || ch == '/' || ch == '\\' || ch == ':' || ch == '*' || ch == '?' || ch == '"' || ch == '<' || ch == '>' || ch == '|')
                {
                    throw new InvalidDataException("Unsafe ASAR path segment.");
                }
            }

            string stem = name.Split('.')[0].ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" || (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && stem[3] >= '1' && stem[3] <= '9'))
            {
                throw new InvalidDataException("Reserved Windows ASAR path segment.");
            }
        }

        private static JavaScriptSerializer Serializer()
        {
            return new JavaScriptSerializer
            {
                MaxJsonLength = MaximumHeaderBytes,
                RecursionLimit = 140
            };
        }

        private static long Align4(long length)
        {
            return (length + 3) & ~3L;
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)(bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24));
        }

        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }
    }
}
