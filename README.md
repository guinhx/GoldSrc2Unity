# Source2Unity

Import Valve engine assets (GoldSrc and Source) directly into Unity.

## Installation

Add this package to your Unity project via the Package Manager:

1. Open **Window > Package Manager**
2. Click the **+** button in the top-left
3. Select **Add package from git URL...**
4. Enter: `https://github.com/BetelCorp/Source2Unity.git`
5. Click **Add**

Alternatively, add to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.betelcorp.source2unity": "https://github.com/BetelCorp/Source2Unity.git"
  }
}
```

## Supported Formats

| Format | Version | Features |
|--------|---------|----------|
| MDL (Studio Model) | v10 (GoldSrc) | Skeletal mesh, textures, animations, body groups |
| MDL (Studio Model) | v6 (Quake 1) | Detected (not yet parsed) |
| MDL (Studio Model) | v44-49 (Source) | Detected (not yet parsed) |
| VPK (Valve Pak) | v1 | Directory parsing, file extraction |
| VPK (Valve Pak) | v2 | Directory parsing, file extraction, MD5 validation |

## Usage

### MDL Models

Simply drag `.mdl` files into your Unity project. The importer will automatically:

- Parse the model geometry (body parts, meshes, triangle strips/fans)
- Build the bone hierarchy with proper transforms
- Generate skinned meshes with bone weights
- Import textures with palette-to-RGB conversion
- Create animation clips for each sequence
- Handle external texture files (`*T.mdl`) and sequence files (`*01.mdl`, `*02.mdl`)

### VPK Archives

Drag `_dir.vpk` files into Unity, or use the VPK Browser:

1. Open **Tools > Source2Unity > VPK Browser**
2. Click **Open VPK** and select a `_dir.vpk` file
3. Browse, search, preview, and extract files

## Requirements

- Unity 2021.3 or later
- .NET Standard 2.1 / C# 9

## License

Apache License 2.0
