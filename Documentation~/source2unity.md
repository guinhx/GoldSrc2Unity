# Source2Unity Documentation

## Overview

Source2Unity is a Unity package that imports Valve engine assets (GoldSrc and Source Engine) directly into Unity as native assets.

## Compatibility

| Engine | Format | Versions | Import Result |
|--------|--------|----------|---------------|
| GoldSrc (Half-Life 1) | MDL | v10 | SkinnedMeshRenderer + AnimationClips |
| Source Engine | VPK | v1, v2 | Archive browsing and extraction |
| Quake 1 | MDL | v6 | Detected only (future support) |
| Source Engine | MDL | v44-49 | Detected only (future support) |

## MDL Import Details

When a `.mdl` file is imported, the following Unity assets are created:

- **Root GameObject** with the model name
- **Armature** child with bone hierarchy (Transform tree)
- **Skinned Meshes** per body part/model with:
  - Vertices transformed to Unity coordinate space
  - UV coordinates computed from texture dimensions
  - Bone weights (single bone per vertex, as GoldSrc uses rigid skinning)
  - Sub-meshes per material/texture reference
- **Textures** converted from 8-bit indexed + 256-color palette to RGB24/RGBA32
- **Materials** with Standard shader configured for:
  - Chrome (metallic/smooth)
  - Additive blending
  - Alpha cutout (masked)
  - Fullbright (emission)
- **Animation Clips** (legacy) per sequence with:
  - Per-bone position and rotation curves
  - Loop mode based on sequence flags
  - Quaternion continuity ensured

### External Files

GoldSrc models may be split across multiple files:
- `modelT.mdl` — External textures (loaded when main file has zero textures)
- `model01.mdl`, `model02.mdl`, ... — External sequence groups

These are loaded automatically if present alongside the main `.mdl` file.

### Coordinate System

GoldSrc uses a right-handed, Z-up coordinate system with 1 unit = 1 inch.
Unity uses a left-handed, Y-up coordinate system with 1 unit = 1 meter.

The conversion applies:
- Axis swap: GoldSrc (X, Y, Z) -> Unity (Y, Z, X)
- Scale: multiply by 0.0254 (inches to meters)
- Rotation: negate appropriate axes for handedness

## VPK Archive Details

VPK (Valve Pak) files are uncompressed archives used by Source Engine games.

### Structure

- `pak01_dir.vpk` — Directory file (contains the file index)
- `pak01_000.vpk`, `pak01_001.vpk`, ... — Data archive chunks

### Usage via Editor Window

Open **Tools > Source2Unity > VPK Browser** to:
- Browse the directory tree
- Search files by name or extension
- Preview text-based files (`.txt`, `.cfg`, `.vmt`, `.qc`)
- Extract individual files or entire archives

## Known Limitations

- MDL v6 (Quake 1) and v44+ (Source Engine) are detected but not parsed
- Bone animation uses rigid skinning (1 bone per vertex) — no smooth weights
- Chrome texture reflection mapping is approximated with metallic material
- VPK signature verification is not performed (signatures are read but not validated)
- Large VPK archives (>4GB total) are not tested
