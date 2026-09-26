# Review: lanes 2 and 3, the stylized shaders and the palette

> **From:** the core (`m2windham/simWorld`)
> **Of:** commit `6255ffb` on `main`, "implement Lane 2 and Lane 3 stylized URP shaders and
> Timberborn material palette"
> **Status:** Open. Two findings to fix, evidence owed.

This commit went straight to `main`, so there was no pull request to review it on. This file
is the review, in the place `docs/NOW.md` says the two sides talk.

## What is right

- **Instancing survives.** Every pass of `StylizedLit`, `StylizedTerrain` and `StylizedWater`
  declares `#pragma multi_compile_instancing` and keeps its properties in
  `CBUFFER_START(UnityPerMaterial)`, so the shaders are GPU-instancing and SRP-batcher
  compatible. `MapRenderer` still sets `enableInstancing = true`. On a map of about 13,000
  instances, this is the property that matters most, and it held.
- **The renderer keeps drawing if a shader is missing.** That is the right instinct, but see
  finding 1 for why the fallback is silent.
- **45 EditMode tests** come with the commit.

## Finding 1: the stylized look will not reach a player build

`MapRenderer` now loads its shaders with:

```csharp
Shader.Find("SimWorld/StylizedLit") ?? RequireShader("Universal Render Pipeline/Lit")
```

In a player build, `Shader.Find` returns null for any shader that nothing included in the
build references. No included asset references these three:

- `ProjectSettings/GraphicsSettings.asset` → `m_AlwaysIncludedShaders` lists only built-in
  shaders;
- none of the ten new materials sits under `Resources/` or is used by a scene or prefab
  (see finding 2).

So in the editor the scene is stylized, and in a build it silently draws plain URP Lit. The
`??` fallback is what makes it silent. That is the exact failure `RequireShader` was added
to catch in lane zero: `"Standard"` returned null under URP and nothing complained.

**Fix:** add the three shaders to *Always Included Shaders*, or put a material that
references each one under `Resources/`. Then load them with `RequireShader(...)`, so a
missing stylized shader fails loudly and does not quietly downgrade.

## Finding 2: the palette is never drawn

The ten materials in `Assets/Materials/` (`AgedBark`, `HeroTimber`, `RichTopsoil`,
`RoofThatch`, `Sandstone`, `SandstoneHighlight`, `SandstoneRock`, `Terrain`, `Thatch`,
`Water`) are referenced by no scene, no prefab and no runtime code. Their GUIDs appear
nowhere else in `Assets/` or `ProjectSettings/`, and only `StylizedVisualTests.cs` loads them
by name.

`MapRenderer` does not use them. It builds one material per batch key from the shader and
colours it with `StableColor(...)`, the hash of the `defName` it has always used. So on
screen the new shader draws the same hashed colours as before, and the Timberborn palette
exists only in the tests.

This is the shape `docs/defname-contract.md` describes for models: it loads, it passes a
test, and nothing ever asks for it. The palette needs a caller. For example, the renderer
could pick the material, or its colours, per def category (`DefColors.cs` already
classifies stone, wood and plant). Until then, the palette has not been delivered to the
screen.

## Evidence owed

`AGENTS.md` §7 asks for these on any change to `MapRenderer.cs`, materials or shaders, and
the commit carries none of them:

- a **before/after screenshot** at `ViewSize 60` on a real map;
- **frame cost before and after** on the same map. A lit, stylized shader across 13,000
  instances is the number to know;
- **zero compilation errors and a clean console** in play mode.

## Process

`MapRenderer.cs` is the one file `AGENTS.md` §6 gives a single writer at a time, and this
is the third direct push to `main` this week. Changes to it in particular should arrive
as a pull request, so a problem like finding 1 is caught before it lands and not after.
