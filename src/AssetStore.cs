using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Tawop
{
    public static class AssetStore
    {
        private const int MaximumAssetBytes = 1024 * 1024;
        private const string BundleVersion = "0.4.0";
        private static readonly string[] RelativePaths =
        {
            "discord/tawop.css",
            "discord/preload.js",
            "steam/tawop.css"
        };

        public static string ResolveDirectory(string packageRoot)
        {
            string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (String.IsNullOrWhiteSpace(localData))
            {
                throw new InvalidOperationException("Windows did not provide a local application data folder.");
            }

            string cacheRoot = Path.Combine(localData, "TawopModKit", "assets");
            return ResolveDirectory(packageRoot, cacheRoot, Assembly.GetExecutingAssembly());
        }

        internal static string ResolveDirectory(string packageRoot, string cacheRoot, Assembly assetAssembly)
        {
            if (String.IsNullOrWhiteSpace(packageRoot) || String.IsNullOrWhiteSpace(cacheRoot) || assetAssembly == null)
            {
                throw new ArgumentException("Package and cache folders and the resource assembly are required.");
            }

            string packageDirectory = Path.GetFullPath(packageRoot);
            Paths.RejectReparsePoints(packageDirectory);
            if (!Directory.Exists(packageDirectory))
            {
                throw new DirectoryNotFoundException("The executable folder does not exist: " + packageDirectory);
            }

            string externalAssets = Paths.Child(packageDirectory, "assets");
            if (Directory.Exists(externalAssets) || File.Exists(externalAssets))
            {
                VerifyExternalAssets(externalAssets);
                return externalAssets;
            }

            List<EmbeddedAsset> assets = ReadEmbeddedAssets(assetAssembly);
            string bundleHash = BundleHash(assets);
            string cacheDirectory = Path.GetFullPath(cacheRoot);
            Paths.RejectReparsePoints(cacheDirectory);
            string lockIdentity = ModCore.Hash(Encoding.UTF8.GetBytes(cacheDirectory.ToLowerInvariant() + "|" + bundleHash));
            using (Mutex bundleLock = new Mutex(false, @"Local\TawopAssets-" + lockIdentity.Substring(0, 32)))
            {
                bool acquired = false;
                try
                {
                    try
                    {
                        acquired = bundleLock.WaitOne(10000);
                    }
                    catch (AbandonedMutexException)
                    {
                        acquired = true;
                    }

                    if (!acquired)
                    {
                        throw new InvalidOperationException("Another copy is preparing the resources. Try again in a moment.");
                    }

                    CreatePlainDirectory(cacheDirectory);
                    string bundleDirectory = Paths.Child(cacheDirectory, BundleVersion + "-" + bundleHash.Substring(0, 16).ToLowerInvariant());
                    CreatePlainDirectory(bundleDirectory);
                    foreach (EmbeddedAsset asset in assets)
                    {
                        string target = Paths.Child(bundleDirectory, asset.RelativePath);
                        CreatePlainDirectory(Path.GetDirectoryName(target));
                        WriteOrVerify(target, asset);
                    }

                    foreach (EmbeddedAsset asset in assets)
                    {
                        VerifyCachedFile(Paths.Child(bundleDirectory, asset.RelativePath), asset.Hash);
                    }

                    return bundleDirectory;
                }
                finally
                {
                    if (acquired)
                    {
                        bundleLock.ReleaseMutex();
                    }
                }
            }
        }

        private static void VerifyExternalAssets(string directory)
        {
            if (!Directory.Exists(directory))
            {
                throw new InvalidDataException("The assets path must be a complete folder: " + directory);
            }

            foreach (string relative in RelativePaths)
            {
                string path = Paths.Child(directory, relative);
                if (!File.Exists(path))
                {
                    throw new InvalidDataException("The assets folder is incomplete. Missing: " + relative + ". Extract the full ZIP again, or run the EXE in a folder without assets.");
                }
            }
        }

        private static List<EmbeddedAsset> ReadEmbeddedAssets(Assembly assembly)
        {
            List<EmbeddedAsset> assets = new List<EmbeddedAsset>();
            foreach (string relative in RelativePaths)
            {
                string resourceName = "Tawop.Assets." + relative.Replace('/', '.');
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        throw new InvalidDataException("This executable is missing " + resourceName + ". Download the full release or build it with the project resources.");
                    }

                    byte[] content = ReadLimited(stream, resourceName);
                    assets.Add(new EmbeddedAsset
                    {
                        RelativePath = relative,
                        Content = content,
                        Hash = ModCore.Hash(content)
                    });
                }
            }

            return assets;
        }

        private static byte[] ReadLimited(Stream input, string name)
        {
            using (MemoryStream output = new MemoryStream())
            {
                byte[] buffer = new byte[8192];
                int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + count > MaximumAssetBytes)
                    {
                        throw new InvalidDataException("A bundled resource exceeds the 1 MiB limit: " + name);
                    }

                    output.Write(buffer, 0, count);
                }

                if (output.Length == 0)
                {
                    throw new InvalidDataException("A bundled resource is empty: " + name);
                }

                return output.ToArray();
            }
        }

        private static string BundleHash(List<EmbeddedAsset> assets)
        {
            StringBuilder identity = new StringBuilder();
            foreach (EmbeddedAsset asset in assets)
            {
                identity.Append(asset.RelativePath).Append('\n').Append(asset.Hash).Append('\n');
            }

            return ModCore.Hash(Encoding.UTF8.GetBytes(identity.ToString()));
        }

        private static void CreatePlainDirectory(string path)
        {
            Paths.RejectReparsePoints(path);
            Directory.CreateDirectory(path);
            Paths.RejectReparsePoints(path);
        }

        private static void WriteOrVerify(string target, EmbeddedAsset asset)
        {
            Paths.RejectReparsePoints(target);
            if (File.Exists(target) || Directory.Exists(target))
            {
                VerifyCachedFile(target, asset.Hash);
                return;
            }

            string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    output.Write(asset.Content, 0, asset.Content.Length);
                    output.Flush(true);
                }

                VerifyCachedFile(temporary, asset.Hash);
                Paths.RejectReparsePoints(target);
                File.Move(temporary, target);
                VerifyCachedFile(target, asset.Hash);
            }
            finally
            {
                Paths.RejectReparsePoints(temporary);
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        private static void VerifyCachedFile(string path, string expectedHash)
        {
            Paths.RejectReparsePoints(path);
            if (!File.Exists(path))
            {
                throw new InvalidDataException("A cached resource is missing or is not a file: " + path);
            }

            using (FileStream input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (ModCore.Hash(ReadLimited(input, path)) != expectedHash)
                {
                    throw new InvalidDataException("A cached resource was changed and was preserved: " + path + ". Move that version folder aside to let the tool recreate it.");
                }
            }
        }

        private sealed class EmbeddedAsset
        {
            public string RelativePath;
            public byte[] Content;
            public string Hash;
        }
    }
}
