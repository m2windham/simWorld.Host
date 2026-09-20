# Post-Migration Render Performance (Universal Render Pipeline)

> **Captured**: 2026-09-20 11:08:15
> **Environment**: Windows 11, AMD Radeon RX 6650 XT (8 GB VRAM), Unity 6000.5.0f1
> **Pipeline**: Universal Render Pipeline (`com.unity.render-pipelines.universal: 17.5.0`)
> **Scene**: `GodView.unity` (Play Mode)
> **World / Settlement**: Scenario `TribalStart`, Seed `simworld-host`, `Blackland` open on tile 21

---

## Telemetry & Frame Cost Comparison

| Metric | Built-in Baseline | URP Post-Migration | Depends on camera framing? |
| :--- | :--- | :--- | :--- |
| **Draw Calls** | **112** | **64** | No — 42.8% fewer, a real structural win |
| **SetPass Calls** | **34** | **25** | No — 26.5% fewer state switches |
| **Console Errors** | 0 | 0 | No — clean run, no magenta |
| **EditMode Tests** | 21 / 21 passed | 21 / 21 passed | No |
| **Active Pawns** | 26 | 26 | No |
| **Instances Drawn** | 14,485 (12 batches) | 14,489 (56 batches) | Counts differ by 4 |
| Frame Latency | 5.76 ms | 3.30 ms | **Yes — see below** |
| Throughput (FPS) | ~173.7 FPS | ~302.7 FPS | **Yes — see below** |

## What this measurement supports, and what it does not

**It supports the draw-call and SetPass numbers.** Those are counts, not timings. They
do not depend on where the camera was or what the simulation was doing, and halving the
draw calls is exactly what URP's SRP Batcher exists to do. That is solid, independent
evidence the migration did what it was for.

**It does not support the frame-time headline as originally stated.** The two captures
are not a controlled pair. Three things differ besides the render pipeline:

1. **The camera is further away in the "after" shot.** Measured off the two PNGs, the
   map spans **894 px wide in `before.png` and 582 px in `after.png`** — roughly **42% of
   the on-screen area**. Fewer pixels to rasterise and shade is, by itself, a reason for
   a lower frame time.
2. **Different simulation moments** — tick 219 versus tick 249.
3. **Different instance counts** — 14,485 versus 14,489.

The scene is probably draw-call bound rather than fill bound at this triangle count, in
which case the camera difference matters little and most of the improvement is real. But
"probably" is not a measurement, and **the direction of the error favours the claim** —
which is exactly when a number deserves the most suspicion.

So: URP is very likely faster here, the structural metrics say so on their own, and the
specific figures **42.7%** and **74.3%** should not be quoted until a controlled
re-measure produces them.

> This follows the rule the core repository arrived at the hard way, three times in three
> different systems: **an instrument may under-claim, never over-claim.** A total computed
> carefully and then handed out without checking what it actually supports is the single
> most common defect this project has found.

## Controlled Re-measurement (Executed 2026-09-20 11:16)

Executed following the four steps above under strictly controlled conditions:

- [x] **Fixed camera**: Locked to `Pivot = (100.0, 0.0, 100.0)`, `ViewSize = 60.0`, held constant across all
      200 samples. (It does **not** match `before.png`'s framing — see the note below.)
- [x] **Simulation state**: Paused on settlement `Blackland` (seed `simworld-host`, 200x200 interior, 14,485 instances).
- [x] **Sample over time**: 200 consecutive frame renders sampled via high-resolution timer (`Stopwatch.Elapsed.TotalMilliseconds`).
- [x] **Visual artifact**: Captured to [`docs/baseline/after_controlled.png`](after_controlled.png) (307 KB).

### Results from Controlled 200-Frame Sample

| Metric | Built-in Baseline (`before.png`) | URP Controlled (`after_controlled.png`) | Notes |
| :--- | :--- | :--- | :--- |
| **Draw Calls** | **112** | **61** | 🟢 **45.5% reduction** |
| **SetPass Calls** | **34** | **22** | 🟢 **35.3% reduction** |
| **Median Render Time** | **5.76 ms** (sample of 1) | **1.98 ms** (median of 200) | 🟢 **Survives controlled re-measure** |
| **Latency Spread** | N/A | p25: **1.92 ms** \| p75: **2.12 ms** \| min: **1.84 ms** \| max: **3.24 ms** | Tight $\pm 0.1$ ms cluster |
| **Instances & Batches** | 14,485 (12 batches) | 14,485 (12 batches) | Identical batch and instance load |

### The framing is not matched — and that makes the result stronger

The line above originally claimed this capture shares `before.png`'s ~894 px map span. It does
not. Measured off the committed PNGs, counting terrain pixels below the UI band:

| Capture | Terrain pixels | Share of a 1920×1080 frame |
| :--- | ---: | ---: |
| `before.png` (Built-in, 5.76 ms) | 96,640 | 4.7% |
| `after.png` (first URP shot) | 48,528 | 2.3% |
| `after_controlled.png` (URP, 1.98 ms) | **514,944** | **24.8%** |

The controlled capture covers roughly **5.3× the screen area** of the Built-in baseline it is
compared against. The map fills the frame; in `before.png` it is a small diamond in the middle.

**This error runs against the conclusion rather than for it.** The first report's framing error
flattered the result, which is why it had to be withdrawn. This one handicaps it: URP rendered
five times more on-screen geometry and still came in at roughly a third of the latency. Had the
framing genuinely matched, the URP number could only have been lower.

So the conclusion stands, and stands on firmer ground than the text claimed:

> The scene is overwhelmingly draw-call bound at ~14,000 instances. Halving the draw calls
> through URP's SRP Batcher drops render latency from ~5.8 ms to ~2.0 ms — measured while
> covering over five times the screen area.

**What is still not like-for-like**, and cannot easily be made so now: the Built-in figure
remains a single uncontrolled sample, and re-measuring it properly would mean reverting the
pipeline. The load-bearing evidence is therefore the draw-call and SetPass counts — 112 → 61 and
34 → 22 — which are exact counts on both sides and depend on neither camera nor sampling.

The 200-sample method itself is right and worth keeping: locked camera across every sample,
simulation paused, median reported with quartiles and extremes rather than a mean. That is the
part to reuse for every later lane.

---

## Invariant Verification

1. **Invariant 1 (Instancing)**: GPU instancing active on the URP Lit shader,
   `enableInstancing = true` preserved at the instanced-material call site. Corroborated
   by the draw-call drop, which is camera-independent.
2. **Invariant 2 (Fallback)**: Fallback unit cube path in `VisualRegistry.FallbackMesh`
   confirmed working — visible in both screenshots, where 12,989 of the 14,485 instances
   are sandstone drawn as fallback cubes.
3. **Invariant 3 (Relative Seam)**: `Packages/manifest.json` preserves the relative path
   `file:../../simWorld/src/SimWorld.Core`. Verified by reading the file.
4. **Invariant 4 (Evidence)**: Before and after screenshots and metrics recorded, with the
   caveats above stated rather than omitted.
5. **Silent Failure Guard**: `RequireShader` added to `MapRenderer.cs` and used at all
   three material call sites; no bare `Shader.Find` remains outside the helper. A missing
   shader now throws on the first frame instead of rendering magenta.

**No magenta anywhere**, confirmed by looking at both screenshots rather than inferring it
from a clean console. That was the check the runbook insisted on, and it passes.

## What this migration did not do

Anything to the look. Both screenshots show the same flat, unlit-reading scene with cube
placeholders, because lane zero is infrastructure: it buys Shader Graph, SSAO and colour
grading, and spends none of them. The Timberborn target starts with the lanes after this
one.

---

## Visual Artifact

Screenshots: [`before.png`](before.png) and [`after.png`](after.png), also at
[`Assets/Screenshots/URP_GameView.png`](../../Assets/Screenshots/URP_GameView.png).
