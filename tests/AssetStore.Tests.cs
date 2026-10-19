using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Tawop.Tests
{
    public static class AssetStoreTests
    {
        public static void Run()
        {
            string temporary = Path.GetFullPath(Path.GetTempPath());
            string fixture = Path.Combine(temporary, "Tawop-AssetStore-" + Guid.NewGuid().ToString("N"));
            string package = Path.Combine(fixture, "package");
            string cache = Path.Combine(fixture, "cache");
            string external = Path.Combine(package, "assets");
            Assembly assembly = Assembly.GetExecutingAssembly();
            Directory.CreateDirectory(package);
            try
            {
                Directory.CreateDirectory(Path.Combine(external, "discord"));
                Directory.CreateDirectory(Path.Combine(external, "steam"));
                string[] paths = { "discord/tawop.css", "discord/preload.js", "steam/tawop.css" };
                foreach (string relative in paths)
                {
                    File.WriteAllText(Path.Combine(external, relative), "editable external asset", new UTF8Encoding(false));
                }

                Assert(AssetStore.ResolveDirectory(package, cache, assembly) == external,
                    "A complete external directory must remain editable and take precedence.");
                Assert(!Directory.Exists(cache), "External resources must not create a cache.");
                File.Delete(Path.Combine(external, "discord/preload.js"));
                MustFail(delegate { AssetStore.ResolveDirectory(package, cache, assembly); },
                    "An incomplete external assets directory must fail instead of mixing resource versions.");
                Directory.Move(external, Path.Combine(fixture, "external-saved"));

                Task<string> first = Task.Factory.StartNew(delegate { return AssetStore.ResolveDirectory(package, cache, assembly); });
                Task<string> second = Task.Factory.StartNew(delegate { return AssetStore.ResolveDirectory(package, cache, assembly); });
                Task.WaitAll(first, second);
                string extracted = first.Result;
                Assert(extracted == second.Result, "Concurrent requests must use the same verified bundle.");
                Assert(Paths.IsInside(cache, extracted), "Embedded resources must remain under the selected cache.");
                Assert(Path.GetFileName(extracted).StartsWith("0.3.0-", StringComparison.Ordinal), "The cache must identify the bundle version and hash.");
                foreach (string relative in paths)
                {
                    string resourceName = "Tawop.Assets." + relative.Replace('/', '.');
                    using (Stream input = assembly.GetManifestResourceStream(resourceName))
                    {
                        Assert(input != null, "The test build must include every embedded release resource.");
                        using (MemoryStream output = new MemoryStream())
                        {
                            input.CopyTo(output);
                            Assert(ModCore.Hash(output.ToArray()) == ModCore.Hash(File.ReadAllBytes(Path.Combine(extracted, relative))),
                                "Every extracted resource must match its embedded SHA256.");
                        }
                    }
                }

                Assert(Directory.GetFiles(extracted, "*.tmp", SearchOption.AllDirectories).Length == 0,
                    "Successful extraction must not leave temporary files.");
                string changedFile = Path.Combine(extracted, "discord/tawop.css");
                File.WriteAllText(changedFile, "preserve this external modification", new UTF8Encoding(false));
                MustFail(delegate { AssetStore.ResolveDirectory(package, cache, assembly); },
                    "An altered cached resource must fail verification.");
                Assert(File.ReadAllText(changedFile) == "preserve this external modification",
                    "Cache verification must never overwrite altered bytes.");

                string missingCache = Path.Combine(fixture, "missing-resources-cache");
                MustFail(delegate { AssetStore.ResolveDirectory(package, missingCache, typeof(string).Assembly); },
                    "An executable without embedded resources must fail clearly.");
                Assert(!Directory.Exists(missingCache), "A missing embedded resource must fail before writing a cache.");
            }
            finally
            {
                string prefix = temporary.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(fixture);
                if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(full).StartsWith("Tawop-AssetStore-", StringComparison.Ordinal) && Directory.Exists(full))
                {
                    Directory.Delete(full, true);
                }
            }
        }

        private static void MustFail(Action action, string message)
        {
            try
            {
                action();
            }
            catch (InvalidDataException)
            {
                return;
            }

            throw new Exception("AssetStoreTests: " + message);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception("AssetStoreTests: " + message);
            }
        }
    }
}
