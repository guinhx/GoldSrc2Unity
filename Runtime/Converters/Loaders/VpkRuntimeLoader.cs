using System;
using Source2Unity.Converters.Mdl;
using Source2Unity.Converters.Pipeline;
using Source2Unity.Converters.Vtf;
using Source2Unity.Formats.Common;
using Source2Unity.Formats.Vpk;
using Source2Unity.Formats.Vpk.Parsers;
using UnityEngine;

namespace Source2Unity.Converters.Loaders
{
    /// <summary>
    /// Mounts a VPK archive at runtime and loads assets from it.
    /// Integrates with the MDL pipeline via <see cref="IContentResolver"/>.
    /// </summary>
    public sealed class VpkRuntimeLoader : IDisposable
    {
        private VpkArchive _archive;

        public VpkArchive Archive => _archive;
        public VpkParseResult Result => _archive?.Result;

        /// <summary>
        /// Opens a VPK directory file and keeps it mounted until disposed.
        /// </summary>
        public void Mount(string vpkDirPath)
        {
            _archive?.Dispose();
            _archive = new VpkArchive();
            _archive.Read(vpkDirPath);
        }

        /// <summary>
        /// Loads an MDL entry from the mounted VPK, resolving external T.mdl and NN.mdl companions inside the archive.
        /// </summary>
        public MdlBuildResult LoadModel(string entryPath)
        {
            return CreateContext().LoadModel(entryPath);
        }

        /// <summary>
        /// Loads a VTF entry from the mounted VPK and converts it to a Unity <see cref="Texture2D"/>.
        /// </summary>
        public Texture2D LoadTexture(string entryPath)
        {
            return AssetConverterRegistry.LoadTexture(entryPath, CreateContext());
        }

        /// <summary>
        /// Loads a VTF entry and returns the full build result (cubemap, animation frames, etc.).
        /// </summary>
        public VtfBuildResult LoadVtf(string entryPath)
        {
            return AssetConverterRegistry.LoadVtf(entryPath, CreateContext());
        }

        /// <summary>
        /// Loads a cubemap VTF from the mounted VPK.
        /// </summary>
        public Cubemap LoadCubemap(string entryPath)
        {
            return AssetConverterRegistry.LoadCubemap(entryPath, CreateContext());
        }

        /// <summary>
        /// Loads a VMT entry from the mounted VPK, resolving referenced VTF textures inside the archive.
        /// </summary>
        public Material LoadMaterial(string entryPath)
        {
            return AssetConverterRegistry.LoadMaterial(entryPath, CreateContext());
        }

        private AssetLoadContext CreateContext()
        {
            if (_archive == null)
                throw new InvalidOperationException("No VPK mounted. Call Mount() first.");

            var resolver = new CompositeContentResolver();
            resolver.Add(new VpkContentResolver(_archive));
            return new AssetLoadContext(resolver);
        }

        /// <summary>
        /// One-shot helper: mount VPK, load model, return result. The VPK is disposed when this returns.
        /// The returned GameObject and sub-assets remain in memory — caller owns their lifecycle.
        /// </summary>
        public static MdlBuildResult LoadModel(string vpkDirPath, string entryPath)
        {
            using var loader = new VpkRuntimeLoader();
            loader.Mount(vpkDirPath);
            return loader.LoadModel(entryPath);
        }

        /// <summary>
        /// One-shot helper: mount VPK, load texture, return result.
        /// </summary>
        public static Texture2D LoadTexture(string vpkDirPath, string entryPath)
        {
            using var loader = new VpkRuntimeLoader();
            loader.Mount(vpkDirPath);
            return loader.LoadTexture(entryPath);
        }

        /// <summary>
        /// One-shot helper: mount VPK, load material, return result.
        /// </summary>
        public static Material LoadMaterial(string vpkDirPath, string entryPath)
        {
            using var loader = new VpkRuntimeLoader();
            loader.Mount(vpkDirPath);
            return loader.LoadMaterial(entryPath);
        }

        public void Dispose()
        {
            _archive?.Dispose();
            _archive = null;
        }
    }
}
