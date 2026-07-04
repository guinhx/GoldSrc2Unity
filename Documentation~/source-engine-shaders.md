# Source & GoldSrc Engine Shading — Research Notes

Reference material for Source2Unity custom URP shaders. Primary sources:

- [Shading in Valve's Source Engine (SIGGRAPH 2006)](https://cdn.cloudflare.steamstatic.com/apps/valve/2006/SIGGRAPH06_Course_ShadingInValvesSourceEngine.pdf)
- [Valve Developer Community — VMT / shader parameters](https://developer.valvesoftware.com/wiki/Category:Material_system)
- [Valve `vertexlitgeneric` SDK source](https://github.com/ValveSoftware/source-sdk-2013)
- [GoldSrc `studio.h` texture flags](https://github.com/ValveSoftware/halflife/blob/master/engine/studio.h)
- [GoldSrc render modes (the303.org)](https://the303.org/tutorials/gold_qc.htm)

---

## Two engines, two pipelines

| Aspect | GoldSrc (HL1 / MDL v10) | Source (HL2+ / VMT) |
|--------|-------------------------|----------------------|
| Materials | Embedded in MDL (`STUDIO_NF_*` flags) | External `.vmt` + `.vtf` |
| World geometry | Lightmaps (software/OpenGL) | **LightmappedGeneric** + radiosity normal mapping |
| Models | **Vertex lighting** (GPU or software) | **VertexLitGeneric** — ambient cube + 2 local lights + envmap |
| Specular | Chrome = matcap on normals | **Phong** local highlights + cubemap env reflection |
| Diffuse wrap | Implicit soft look | **Half-Lambert** (`$halflambert`) — `(N·L×0.5+0.5)²` |
| PBR | None | None (pre-PBR Blinn-Phong era) |

Source2Unity maps **VMT → Source Standard / Unlit / Sky** and **GoldSrc MDL → Source GoldSrc**.

---

## Source Engine — VertexLitGeneric (models)

### Lighting composition (SIGGRAPH 2006 §8.4)

```
final = albedo × (ambientCube + Σ localDiffuse) + specularPhong + envReflection + selfIllum
```

1. **Ambient Cube** — six directional ambient colors blended by `normal²` (irradiance volume). Unity approximation: `SampleSH(normal)`.
2. **Local lights** — up to 2 diffuse lights; Half-Lambert by default on characters.
3. **Phong** (`$phong`) — Blinn-Phong specular from local lights; exponent from `$phongexponent` / `$phongexponenttexture`; boost from `$phongboost`; rim bias from `$phongfresnelranges "[min mid max]"`.
4. **Envmap** (`$envmap`) — cubemap reflection; tinted by `$envmaptint`; Fresnel via `$envmapfresnel`; **masked** by basetexture alpha (`$basealphaenvmapmask`) or bump alpha (`$normalmapalphaenvmapmask`). **Incompatible with `$envmapmask` when Phong is on** — bump/base alpha used instead.
5. **Self-illum** (`$selfillum`) — additive glow; basetexture alpha often used as mask; **not attenuated by lights**.

### Half-Lambert (from SDK `DiffuseTerm`)

```hlsl
float NDotL = dot(normal, lightDir);
float f = saturate(NDotL * 0.5 + 0.5);
f = f * f;  // square for falloff
```

### Key VMT parameters → Source2Unity shader

| VMT parameter | Engine behavior | Source2Unity mapping |
|---------------|-----------------|----------------------|
| `$basetexture` | Albedo | `_BaseMap` |
| `$color` / `$color2` | Tint | `_BaseColor` |
| `$bumpmap` | Tangent normals | `_BumpMap` + `_NORMALMAP` |
| `$halflambert` | Wrapped diffuse | `_HALFLAMBERT` |
| `$phong` | Specular on | `_PHONG` |
| `$phongboost` | Spec intensity | `_PhongBoost` |
| `$phongexponent` | Spec sharpness | `_PhongExponent` |
| `$phongexponenttexture` | Per-texel spec sharpness | `_PhongExponentMap` + `_PHONG_EXPONENTMAP` |
| `$phongfresnelranges` | View-dependent spec | `_PhongFresnelRanges` |
| `$envmap` | Cubemap reflection | `_EnvCubemap` + `_ENVMAP` |
| `$envmaptint` | Reflection color | `_EnvTint` |
| `$envmapfresnel` | Fresnel power | `_EnvFresnelPower` |
| `$basealphaenvmapmask` | Env mask from base α | `_ENVMAPMASK_BASEALPHA` |
| `$normalmapalphaenvmapmask` | Env mask from bump α | `_ENVMAPMASK_BUMPALPHA` |
| `$selfillum` | Additive glow | `_SELFILLUM` + `_EmissionMap` / base α |
| `$rimlight` | Rim specular | `_RIMLIGHT` |
| `$alphatest` | Cutout | `_ALPHATEST_ON` |
| `$translucent` | Alpha blend | `_SURFACE_TYPE_TRANSPARENT` |
| `$additive` | Additive blend | SrcAlpha One |
| `$nocull` | Double-sided | `_Cull Off` |

---

## Source Engine — LightmappedGeneric (world)

- Uses **lightmaps** + normal maps (**Radiosity Normal Mapping**).
- **Source Lightmapped** shader: UV0 = albedo, UV1 = lightmap.
- **Single lightmap:** `$lightmap` / `$lightmaptexture` → `_LightMap` (keyword `_LIGHTMAP`).
- **RNM (3 lightmaps):** `$lightmap0`–`$lightmap2` → `_LightMapBump0/1/2` (keyword `_RNM`). Blends with tangent-space basis from GDC 2004.
- Without assigned lightmaps, falls back to **ambient cube** (same as models).
- BSP-embedded lightmaps require a future map importer; assign textures manually or via VMT keys above.

### RNM formula (pixel shader)

```
lit = lm0 * dot(basis0, N_ts) + lm1 * dot(basis1, N_ts) + lm2 * dot(basis2, N_ts)
final = albedo * lit + envReflection + selfIllum
```

---

## Ambient Cube (models & lightmap fallback)

Valve SIGGRAPH 2006 Listing 1 — six RGB colors blended by `normal²`:

| Property | Direction |
|----------|-----------|
| `_AmbientCubePX` / `_AmbientCubeNX` | ±X |
| `_AmbientCubePY` / `_AmbientCubeNY` | ±Y (sky / ground defaults from RenderSettings) |
| `_AmbientCubePZ` / `_AmbientCubeNZ` | ±Z |

Keyword `_AMBIENT_CUBE` on **Source Standard** (auto from RenderSettings trilight). When off, uses `SampleSH`.

---

## env_cubemap builtin

VMT `$envmap env_cubemap` references the nearest `env_cubemap` entity cubemap from the map. Source2Unity resolves in order:

1. `AssetLoadContext.FallbackEnvCubemap` (set at runtime for your scene)
2. `RenderSettings.customReflectionTexture`
3. Skip envmap if neither is available

---

## GoldSrc — MDL texture flags (`studio.h`)

| Flag | Value | Behavior |
|------|-------|----------|
| `STUDIO_NF_FLATSHADE` | 0x0001 | Uniform lighting — no per-vertex shadow gradient |
| `STUDIO_NF_CHROME` | 0x0002 | Matcap / environment sphere mapping from normals |
| `STUDIO_NF_FULLBRIGHT` | 0x0004 | Ignore scene lighting |
| `STUDIO_NF_NOMIPS` | 0x0008 | No mipmaps |
| `STUDIO_NF_ALPHA` | 0x0010 | Smooth alpha |
| `STUDIO_NF_ADDITIVE` | 0x0020 | Additive blending |
| `STUDIO_NF_MASKED` | 0x0040 | 1-bit alphatest |

**Chrome** is matcap-style: spherical texture indexed by normal direction (see [chrome tutorial](https://the303.org/tutorials/gold_mdl_chrome.htm)). Name-set chrome uses flatshade lighting; flag-set chrome keeps vertex shading.

**Flatshade** — entire submesh brightens/darkens uniformly with environment, no crisp terminators.

Source2Unity **Source GoldSrc** shader implements Half-Lambert (default), with keywords for flatshade, chrome, fullbright, additive, masked.

---

## Unity shader mapping

| Shader | Use case |
|--------|----------|
| `Source2Unity/Source Standard` | Source VMT — VertexLitGeneric-like (ambient cube + Phong + envmap) |
| `Source2Unity/Source Lightmapped` | LightmappedGeneric world materials (lightmap / RNM + envmap) |
| `Source2Unity/Source Unlit` | UnlitGeneric, sprites |
| `Source2Unity/Source Sky` | Skybox / env-only materials |
| `Source2Unity/Source GoldSrc` | GoldSrc MDL v10 skins |

All require **URP** (`com.unity.render-pipelines.universal`).
