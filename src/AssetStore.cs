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
        private const string BundleVersion = "0.3.0";
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
                throw new InvalidOperationException("No se encontró la carpeta de datos locales del usuario.");
            }

            string cacheRoot = Path.Combine(localData, "TawopModKit", "assets");
            return ResolveDirectory(packageRoot, cacheRoot, Assembly.GetExecutingAssembly());
        }

        internal static string ResolveDirectory(string packageRoot, string cacheRoot, Assembly assetAssembly)
        {
            if (String.IsNullOrWhiteSpace(packageRoot) || String.IsNullOrWhiteSpace(cacheRoot) || assetAssembly == null)
            {
                throw new ArgumentException("Se requieren las carpetas del paquete y de caché y el ensamblado de recursos.");
            }

            string packageDirectory = Path.GetFullPath(packageRoot);
            Paths.RejectReparsePoints(packageDirectory);
            if (!Directory.Exists(packageDirectory))
            {
                throw new DirectoryNotFoundException("No existe la carpeta del ejecutable: " + packageDirectory);
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
                        throw new InvalidOperationException("Otra instancia está preparando los recursos Tawop. Volvé a intentar.");
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
                throw new InvalidDataException("La ruta assets debe ser una carpeta completa: " + directory);
            }

            foreach (string relative in RelativePaths)
            {
                string path = Paths.Child(directory, relative);
                if (!File.Exists(path))
                {
                    throw new InvalidDataException("La carpeta assets está incompleta. Falta " + relative + ". Reinstalá el paquete completo o usá el EXE en una carpeta sin assets.");
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
                        throw new InvalidDataException("El ejecutable no incluye " + resourceName + ". Descargá una versión completa o compilá con los recursos del proyecto.");
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
                        throw new InvalidDataException("Un recurso Tawop supera el límite de 1 MiB: " + name);
                    }

                    output.Write(buffer, 0, count);
                }

                if (output.Length == 0)
                {
                    throw new InvalidDataException("Un recurso Tawop está vacío: " + name);
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
                throw new InvalidDataException("El recurso de caché falta o no es un archivo: " + path);
            }

            using (FileStream input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (ModCore.Hash(ReadLimited(input, path)) != expectedHash)
                {
                    throw new InvalidDataException("El recurso de caché fue alterado y no se sobrescribió: " + path + ". Mové la carpeta de esa versión para regenerarla.");
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
