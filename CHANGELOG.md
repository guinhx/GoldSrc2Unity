# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-07-03

### Added

- MDL v10 (GoldSrc/Half-Life) parser with full format support
  - Header, bones, bone controllers, hitboxes, sequences, textures, body parts
  - Body part -> model -> mesh -> triangle strip/fan decoding pipeline
  - RLE animation decompression with correct relative offsets
  - External texture file (T.mdl) and sequence group file (01.mdl, 02.mdl) support
- MDL version detection (IDST v10, IDPO v6, IDSQ sequence, Source v44-49)
- VPK v1 and v2 archive parser
  - Directory tree parsing (extension/path/filename grouping)
  - File extraction from directory-embedded data or numbered archive chunks
  - Multi-archive stream management
- Unity ScriptedImporter for .mdl files
  - Bone hierarchy generation with proper transforms
  - Skinned mesh rendering with bone weights and bind poses
  - AnimationClip generation with quaternion continuity
  - Texture importing with alpha, masked, additive, chrome, and fullbright support
  - GoldSrc to Unity coordinate system conversion
- Unity ScriptedImporter for .vpk files
- VPK Browser EditorWindow with search, preview, and extraction
- Pure C# Runtime layer with no Unity dependencies
- UPM package structure installable from Git URL
