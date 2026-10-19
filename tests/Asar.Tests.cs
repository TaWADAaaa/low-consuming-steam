using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Tawop.Tests
{
    public static class AsarTests
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public static void Run()
        {
            RoundTripAndLongUnicode();
            RejectCorruptIntegrity();
            RejectUnsafeMetadata();
            DiscordPreparationAndFusePreflight();
            DiscordFuseLayoutValidation();
            Console.WriteLine("ASAR and Discord preparation: passed synthetic checks.");
        }

        private static void RoundTripAndLongUnicode()
        {
            byte[] stable = new byte[4 * 1024 * 1024 + 137];
            for (int i = 0; i < stable.Length; i++)
            {
                stable[i] = (byte)(i * 37);
            }

            var files = new Dictionary<string, byte[]>
            {
                {
                    "bundle.js",
                    Utf8.GetBytes("/* original fixture */\n")
                },
                {
                    "datos/archivo-\u00f1.txt",
                    Utf8.GetBytes("inicial")
                },
                {
                    "unchanged.bin",
                    stable
                },
                {
                    "empty.bin",
                    new byte[0]
                }
            };
            byte[] first = AsarArchive.Create(files);
            AsarArchive archive = AsarArchive.Read(first);
            foreach (KeyValuePair<string, byte[]> pair in files)
            {
                AssertBytes(pair.Value, archive.GetFile(pair.Key), "Initial fixture member round-trip");
            }

            string changed = "/* TAWOP_MOD_V1 */\n" + new string ('\u00f1', 9001) + " \ud83d\ude80 \u6e2c\u8a66";
            byte[] changedBytes = Utf8.GetBytes(changed);
            archive.SetFile("datos/archivo-\u00f1.txt", changedBytes);
            byte[] repacked = archive.Repack();
            AsarArchive reread = AsarArchive.Read(repacked);
            AssertBytes(changedBytes, reread.GetFile("datos/archivo-\u00f1.txt"), "Long Unicode replacement");
            AssertBytes(stable, reread.GetFile("unchanged.bin"), "Unmodified binary payload preservation");
            AssertBytes(files["bundle.js"], reread.GetFile("bundle.js"), "Unmodified source preservation");
            AssertBytes(new byte[0], reread.GetFile("empty.bin"), "Empty payload preservation");
            byte[] clone = reread.GetFile("unchanged.bin");
            clone[0] ^= 0xff;
            AssertBytes(stable, reread.GetFile("unchanged.bin"), "GetFile must return a defensive copy");
            VerifyIndependentHashes(repacked);
            AssertBytes(repacked, reread.Repack(), "Repeated repack is deterministic");
        }

        private static void RejectCorruptIntegrity()
        {
            byte[] archive = AsarArchive.Create(new Dictionary<string, byte[]> { { "fixture.txt", Utf8.GetBytes("Synthetic payload used only for integrity testing.") } });
            byte[] corrupt = (byte[])archive.Clone();
            corrupt[corrupt.Length - 1] ^= 1;
            MustReject(delegate
            {
                AsarArchive.Read(corrupt);
            }, "Corrupt member hash");
            byte[] corruptBlocks = RewriteHeader(archive, delegate (Dictionary<string, object> root)
            {
                var file = Node(root, "fixture.txt");
                var integrity = (Dictionary<string, object>)file["integrity"];
                integrity["blocks"] = new object[]
                {
                    new string ('0', 64)
                };
            });
            MustReject(delegate
            {
                AsarArchive.Read(corruptBlocks);
            }, "Corrupt per-block hash");
            byte[] missingHash = RewriteHeader(archive, delegate (Dictionary<string, object> root)
            {
                Node(root, "fixture.txt").Remove("integrity");
            });
            MustReject(delegate
            {
                AsarArchive.Read(missingHash);
            }, "Missing required integrity");
        }

        private static void RejectUnsafeMetadata()
        {
            byte[] archive = AsarArchive.Create(new Dictionary<string, byte[]> { { "fixture.txt", Utf8.GetBytes("fixture") }, { "second.txt", Utf8.GetBytes("second!") } });
            foreach (string unsafeName in new string[]
            {
                "../outside",
                "..",
                "/absolute",
                "C:escape",
                "a\\b",
                "NUL",
                "trailing."
            }

            )
            {
                string name = unsafeName;
                byte[] unsafeArchive = RewriteHeader(archive, delegate (Dictionary<string, object> root)
                {
                    var files = (Dictionary<string, object>)root["files"];
                    object node = files["fixture.txt"];
                    files.Remove("fixture.txt");
                    files[name] = node;
                });
                MustReject(delegate
                {
                    AsarArchive.Read(unsafeArchive);
                }, "Unsafe path: " + name);
            }

            byte[] linked = RewriteHeader(archive, delegate (Dictionary<string, object> root)
            {
                Node(root, "fixture.txt")["link"] = "second.txt";
            });
            MustReject(delegate
            {
                AsarArchive.Read(linked);
            }, "Symbolic-link entry");
            byte[] unpacked = RewriteHeader(archive, delegate (Dictionary<string, object> root)
            {
                Node(root, "fixture.txt")["unpacked"] = true;
            });
            MustReject(delegate
            {
                AsarArchive.Read(unpacked);
            }, "External unpacked entry");
            byte[] outOfRange = RewriteHeader(archive, delegate (Dictionary<string, object> root)
            {
                Node(root, "fixture.txt")["offset"] = "999999999";
            });
            MustReject(delegate
            {
                AsarArchive.Read(outOfRange);
            }, "Out-of-range payload offset");
            byte[] colliding = RewriteHeader(archive, delegate (Dictionary<string, object> root)
            {
                var files = (Dictionary<string, object>)root["files"];
                files["FIXTURE.TXT"] = files["fixture.txt"];
            });
            MustReject(delegate
            {
                AsarArchive.Read(colliding);
            }, "Windows case-colliding names");
            byte[] repeatedPayload = AsarArchive.Create(new Dictionary<string, byte[]> { { "first.bin", new byte[] { 1, 2, 3 } }, { "second.bin", new byte[] { 1, 2, 3 } } });
            byte[] overlapping = RewriteHeader(repeatedPayload, delegate (Dictionary<string, object> root)
            {
                Node(root, "second.bin")["offset"] = Node(root, "first.bin")["offset"];
            });
            MustReject(delegate
            {
                AsarArchive.Read(overlapping);
            }, "Overlapping entries with otherwise-valid integrity");
            MustReject(delegate
            {
                AsarArchive.Create(new Dictionary<string, byte[]> { { "../escape", new byte[0] } });
            }, "Unsafe creation path");
        }

        private static void DiscordPreparationAndFusePreflight()
        {
            string temporary = Path.Combine(Path.GetTempPath(), "Tawop-Asar-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            try
            {
                string install = Path.Combine(temporary, "app-fixture");
                string resources = Path.Combine(install, "resources");
                string core = Path.Combine(install, "modules\\discord_desktop_core-1\\discord_desktop_core");
                string assets = Path.Combine(temporary, "assets");
                Directory.CreateDirectory(resources);
                Directory.CreateDirectory(core);
                Directory.CreateDirectory(Path.Combine(assets, "discord"));
                File.WriteAllText(Path.Combine(assets, "discord\\tawop.css"), ":root { " + DiscordMod.CssMarker + " --fixture:\"\u00f1\"; }", Utf8);
                File.WriteAllText(Path.Combine(assets, "discord\\preload.js"), DiscordMod.PreloadMarker + "\n;(function(){var css=__TAWOP_STYLESHEET_JSON__;void css;}());", Utf8);
                byte[] app = AsarArchive.Create(new Dictionary<string, byte[]> { { "splash/index.css", Utf8.GetBytes("body{color:white;}") }, { "untouched.bin", new byte[] { 0, 1, 255, 2 } } });
                byte[] mainPreload = Utf8.GetBytes("/* synthetic original preload */\n");
                byte[] coreArchive = AsarArchive.Create(new Dictionary<string, byte[]> { { "mainScreenPreload.js", mainPreload }, { "untouched.bin", new byte[] { 9, 0, 8, 255 } } });
                string appFile = Path.Combine(resources, "app.asar");
                string coreFile = Path.Combine(core, "core.asar");
                string exe = Path.Combine(install, "Discord.exe");
                File.WriteAllBytes(appFile, app);
                File.WriteAllBytes(coreFile, coreArchive);
                File.WriteAllBytes(exe, FuseFixture(false));
                List<PatchEdit> edits = DiscordMod.Prepare(install, assets);
                Assert(edits.Count == 2, "Prepare should stage splash and desktop core changes");
                AssertBytes(app, File.ReadAllBytes(appFile), "Prepare must leave original app.asar untouched");
                AssertBytes(coreArchive, File.ReadAllBytes(coreFile), "Prepare must leave original core.asar untouched");
                foreach (PatchEdit edit in edits)
                {
                    bool isCore = edit.RelativePath.IndexOf("modules/", StringComparison.Ordinal) == 0;
                    byte[] original = isCore ? coreArchive : app;
                    Assert(edit.ExpectedBeforeHash == ModCore.Hash(original), "Edit must bind to original source hash");
                    AsarArchive patched = AsarArchive.Read(edit.Content);
                    string entry = isCore ? "mainScreenPreload.js" : "splash/index.css";
                    string text = Utf8.GetString(patched.GetFile(entry));
                    string marker = isCore ? DiscordMod.PreloadMarker : DiscordMod.CssMarker;
                    Assert(text.IndexOf(marker, StringComparison.Ordinal) >= 0, "Patch marker is present");
                    Assert(text.IndexOf("__TAWOP_STYLESHEET_JSON__", StringComparison.Ordinal) < 0, "Stylesheet placeholder is replaced");
                    if (isCore)
                    {
                        Assert(text.StartsWith(Utf8.GetString(mainPreload), StringComparison.Ordinal), "Original preload is preserved verbatim");
                        AssertBytes(new byte[] { 9, 0, 8, 255 }, patched.GetFile("untouched.bin"), "Discord core payload preservation");
                    }

                    File.WriteAllBytes(isCore ? coreFile : appFile, edit.Content);
                }

                MustReject(delegate
                {
                    DiscordMod.Prepare(install, assets);
                }, "Repeated Discord patch must fail closed");
                File.WriteAllBytes(exe, FuseFixture(true));
                MustReject(delegate
                {
                    DiscordMod.VerifyElectronAsarIntegrity(exe);
                }, "Enabled embedded validation must be respected");
                File.WriteAllBytes(exe, Utf8.GetBytes("MZ synthetic executable without a fuse wire"));
                MustReject(delegate
                {
                    DiscordMod.VerifyElectronAsarIntegrity(exe);
                }, "Unknown Electron fuse layout must fail closed");
            }
            finally
            {
                string tempPrefix = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string target = Path.GetFullPath(temporary);
                if (!target.StartsWith(tempPrefix, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(target).StartsWith("Tawop-Asar-", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Refusing to remove a test directory outside its temporary root.");
                }

                if (Directory.Exists(target))
                {
                    Directory.Delete(target, true);
                }
            }
        }

        private static byte[] FuseFixture(bool validationEnabled)
        {
            return FuseFixture(65530, 1, Encoding.ASCII.GetBytes(validationEnabled ? "001010011" : "001000011"));
        }

        private static void DiscordFuseLayoutValidation()
        {
            string temporary = Path.Combine(Path.GetTempPath(), "Tawop-Fuse-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            try
            {
                string exe = Path.Combine(temporary, "Discord.exe");
                byte[] disabled = Encoding.ASCII.GetBytes("001000011");
                foreach (int start in new int[] { 2, 65506, 65507, 65530, 65537, 65538, 131070 })
                {
                    VerifyFuseFixture(exe, FuseFixture(start, 1, disabled), true, "A valid wire spanning or next to a scan boundary must pass");
                }

                VerifyFuseFixture(exe, FuseFixture(64, 1, Encoding.ASCII.GetBytes("r01000011")), true, "Removed unrelated fuses are supported");
                VerifyFuseFixture(exe, FuseFixture(64, 1, Encoding.ASCII.GetBytes("00100")), true, "The minimum supported wire must pass");
                VerifyFuseFixture(exe, FuseFixture(64, 1, Encoding.ASCII.GetBytes(new string('0', 255))), true, "The one-byte wire length must be supported without an arbitrary limit");
                VerifyFuseFixture(exe, FuseFixture(64, 2, disabled), false, "An unsupported wire version must fail");
                VerifyFuseFixture(exe, FuseFixture(64, 1, Encoding.ASCII.GetBytes("0010")), false, "A wire without the ASAR integrity fuse must fail");
                VerifyFuseFixture(exe, FuseFixture(64, 1, Encoding.ASCII.GetBytes("001010011")), false, "Enabled ASAR protection must fail");
                VerifyFuseFixture(exe, FuseFixture(64, 1, Encoding.ASCII.GetBytes("0010r0011")), false, "Removed ASAR protection is not an explicit disabled state");
                VerifyFuseFixture(exe, FuseFixture(64, 1, Encoding.ASCII.GetBytes("x01000011")), false, "An unknown unrelated fuse state must fail");
                byte[] inherited = (byte[])disabled.Clone();
                inherited[0] = 0x90;
                VerifyFuseFixture(exe, FuseFixture(64, 1, inherited), false, "An unresolved inherited fuse state is not a packaged fuse state");

                byte[] truncated = FuseFixture(64, 1, disabled);
                Array.Resize(ref truncated, truncated.Length - 1);
                VerifyFuseFixture(exe, truncated, false, "A truncated fuse wire must fail");
                Array.Resize(ref truncated, 64 + Encoding.ASCII.GetByteCount("dL7pKGdnNz796PbbjQWNKmHXBZaB9tsX"));
                VerifyFuseFixture(exe, truncated, false, "A missing fuse header must fail");

                byte[] first = FuseFixture(64, 1, disabled);
                byte[] second = FuseFixture(64, 1, Encoding.ASCII.GetBytes("001010011"));
                byte[] duplicate = new byte[first.Length + second.Length];
                Buffer.BlockCopy(first, 0, duplicate, 0, first.Length);
                Buffer.BlockCopy(second, 0, duplicate, first.Length, second.Length);
                VerifyFuseFixture(exe, duplicate, false, "A second Windows fuse wire must fail even when the first one is disabled");
                VerifyFuseFixture(exe, Utf8.GetBytes("MZ synthetic Discord updater without Electron"), false, "An updater executable must not be accepted as the Electron application");
            }
            finally
            {
                string prefix = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string target = Path.GetFullPath(temporary);
                if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(target).StartsWith("Tawop-Fuse-", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Refusing to remove a test directory outside its temporary root.");
                }

                if (Directory.Exists(target))
                {
                    Directory.Delete(target, true);
                }
            }
        }

        private static byte[] FuseFixture(int start, byte version, byte[] wire)
        {
            byte[] sentinel = Encoding.ASCII.GetBytes("dL7pKGdnNz796PbbjQWNKmHXBZaB9tsX");
            byte[] bytes = new byte[start + sentinel.Length + 2 + wire.Length];
            bytes[0] = (byte)'M';
            bytes[1] = (byte)'Z';
            Buffer.BlockCopy(sentinel, 0, bytes, start, sentinel.Length);
            bytes[start + sentinel.Length] = version;
            bytes[start + sentinel.Length + 1] = (byte)wire.Length;
            Buffer.BlockCopy(wire, 0, bytes, start + sentinel.Length + 2, wire.Length);
            return bytes;
        }

        private static void VerifyFuseFixture(string exe, byte[] bytes, bool supported, string message)
        {
            File.WriteAllBytes(exe, bytes);
            if (supported)
            {
                DiscordMod.VerifyElectronAsarIntegrity(exe);
            }
            else
            {
                MustReject(delegate
                {
                    DiscordMod.VerifyElectronAsarIntegrity(exe);
                }, message);
            }

            AssertBytes(bytes, File.ReadAllBytes(exe), "Fuse verification must never modify an executable");
        }

        private static void VerifyIndependentHashes(byte[] archive)
        {
            int payloadBase = 8 + BitConverter.ToInt32(archive, 4);
            var root = Header(archive);
            VerifyDirectory((Dictionary<string, object>)root["files"], archive, payloadBase);
        }

        private static void VerifyDirectory(Dictionary<string, object> files, byte[] archive, int payloadBase)
        {
            foreach (object value in files.Values)
            {
                var node = (Dictionary<string, object>)value;
                if (node.ContainsKey("files"))
                {
                    VerifyDirectory((Dictionary<string, object>)node["files"], archive, payloadBase);
                    continue;
                }

                int offset = Int32.Parse((string)node["offset"], System.Globalization.CultureInfo.InvariantCulture);
                int size = Convert.ToInt32(node["size"]);
                var integrity = (Dictionary<string, object>)node["integrity"];
                using (SHA256 hash = SHA256.Create())
                {
                    string actual = BitConverter.ToString(hash.ComputeHash(archive, payloadBase + offset, size)).Replace("-", "").ToLowerInvariant();
                    Assert(actual == (string)integrity["hash"], "Independent SHA256 file verification");
                }
            }
        }

        private static Dictionary<string, object> Node(Dictionary<string, object> root, string name)
        {
            return (Dictionary<string, object>)((Dictionary<string, object>)root["files"])[name];
        }

        private static Dictionary<string, object> Header(byte[] archive)
        {
            return new JavaScriptSerializer().DeserializeObject(Utf8.GetString(archive, 16, BitConverter.ToInt32(archive, 12))) as Dictionary<string, object>;
        }

        private static byte[] RewriteHeader(byte[] archive, Action<Dictionary<string, object>> change)
        {
            Dictionary<string, object> header = Header(archive);
            change(header);
            byte[] json = Utf8.GetBytes(new JavaScriptSerializer().Serialize(header));
            int oldBase = 8 + BitConverter.ToInt32(archive, 4);
            int headerPayload = (4 + json.Length + 3) & ~3;
            int headerBuffer = 4 + headerPayload;
            int newBase = 8 + headerBuffer;
            byte[] result = new byte[newBase + archive.Length - oldBase];
            Buffer.BlockCopy(BitConverter.GetBytes(4), 0, result, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(headerBuffer), 0, result, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(headerPayload), 0, result, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(json.Length), 0, result, 12, 4);
            Buffer.BlockCopy(json, 0, result, 16, json.Length);
            Buffer.BlockCopy(archive, oldBase, result, newBase, archive.Length - oldBase);
            return result;
        }

        private static void MustReject(Action action, string message)
        {
            try
            {
                action();
            }
            catch (InvalidDataException)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                return;
            }

            throw new Exception("Expected rejection: " + message);
        }

        private static void AssertBytes(byte[] expected, byte[] actual, string message)
        {
            Assert(expected.Length == actual.Length, message + " (length)");
            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i])
                {
                    throw new Exception(message + " (byte " + i + ")");
                }
            }
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
