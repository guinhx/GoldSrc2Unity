# Source2Unity

## Purpose

Unity package for importing Valve engine assets (GoldSrc and Source) directly into the Unity Editor. Provides `ScriptedImporter` implementations that allow `.mdl` and `.vpk` files to be dragged into a Unity project and automatically converted to native Unity assets.

Package identifier: `com.betelcorp.source2unity`

## Architecture

```
Editor/           Unity-dependent import logic (ScriptedImporters, EditorWindows)
Runtime/
  Formats/        Pure C# format parsers — zero Unity dependencies
    Common/       Shared infrastructure (binary reading, interfaces)
    Mdl/          MDL model format (GoldSrc v10, with version detection for Quake v6 and Source v44+)
    Vpk/          VPK archive format (Source Engine v1 and v2)
  Extensions/     Utility extension methods (Span, string helpers)
  Internal/       Compiler polyfills (IsExternalInit)
```

The `Runtime/Formats/` layer is intentionally Unity-agnostic. It operates on `System.IO.Stream` and produces plain data objects. Only the `Editor/` layer references `UnityEngine` or `UnityEditor` to convert parsed data into Unity assets.

Assembly definitions:
- `com.betelcorp.source2unity` (Runtime) — `allowUnsafeCode: true`, `noEngineReferences: true`
- `com.betelcorp.source2unity.Editor` (Editor only) — references the Runtime assembly

## Supported Formats

| Format | Versions | Status |
|--------|----------|--------|
| MDL (Studio Model) | v10 (GoldSrc / Half-Life 1) | Fully supported |
| MDL (Studio Model) | v6 (Quake 1, IDPO) | Detected, not parsed |
| MDL (Studio Model) | v44-49 (Source Engine) | Detected, not parsed |
| VPK (Valve Pak) | v1 | Fully supported |
| VPK (Valve Pak) | v2 | Fully supported |

## Coding Standards

### Struct Layout Rules

- Use `[StructLayout(LayoutKind.Sequential, Pack = 1)]` exclusively for structs that map 1:1 to binary file data.
- Fixed-size arrays use `unsafe fixed` syntax (e.g., `public unsafe fixed byte Name[32]`).
- String fields are stored as raw byte arrays; conversion happens via accessor properties or helper methods.
- All struct field names, sizes, and order must exactly match Valve's official `studio.h` or the relevant format specification.

### Naming Conventions

- File names use PascalCase matching the primary type they contain.
- Extension method classes use plural suffix: `ByteSpanExtensions`, `StringExtensions`.
- Format struct names are concise data names: `MdlBone`, `MdlTexture`, `MdlBodyPart`.
- Parser interfaces are prefixed with `I`: `IMdlParser`, `IVpkParser`.
- Enum values are PascalCase without prefix repetition.
- Namespaces follow `Source2Unity.{Layer}.{Format}` pattern.

### General Principles

- No hacks. Every file offset is validated before use.
- Interface-driven version dispatch (Open/Closed principle).
- Parsed results are read-only after construction (use `init` accessors).
- Prefer `Span<byte>` and stack allocation over heap where feasible.
- No Unity dependencies in `Runtime/`.
- Minimum Unity version: 2021.3 (C# 9 / .NET Standard 2.1).

## Key References

- Valve `studio.h` (official MDL structs): https://github.com/ValveSoftware/halflife/blob/master/engine/studio.h
- VPK header specification: https://xonotic.org/doxygen/darkplaces/vpk_8h_source.html
- ValvePak C# reference implementation: https://github.com/ValveResourceFormat/ValvePak
- hlviewer.js MDL format documentation: https://github.com/skyrim/hlviewer.js/blob/master/docs/MDL_Format.md
- assimp Half-Life 1 MDL loader: https://github.com/assimp/assimp/blob/master/code/AssetLib/MDL/HalfLife/HL1MDLLoader.cpp
