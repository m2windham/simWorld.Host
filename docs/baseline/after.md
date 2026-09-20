# Post-Migration Render Performance (Universal Render Pipeline)

> **Captured**: 2026-09-20 11:08:15  
> **Environment**: Windows 11, AMD Radeon RX 6650 XT (8 GB VRAM), Unity 6000.5.0f1  
> **Pipeline**: Universal Render Pipeline (`com.unity.render-pipelines.universal: 17.5.0`)  
> **Scene**: `GodView.unity` (Play Mode)  
> **World / Settlement**: Scenario `TribalStart`, Seed `simworld-host`, `Blackland` open on tile 21  

---

## Telemetry & Frame Cost Comparison

| Metric | Built-in Baseline | URP Post-Migration | Improvement |
| :--- | :--- | :--- | :--- |
| **Frame Latency** | **5.76 ms** | **3.30 ms** | 🟢 **42.7% reduction** in frame time |
| **Throughput (FPS)** | **~173.7 FPS** | **~302.7 FPS** | 🟢 **74.3% higher framerate** |
| **Draw Calls** | **112** | **64** | 🟢 **42.8% fewer draw calls** |
| **SetPass Calls** | **34** | **25** | 🟢 **26.5% fewer state switches** |
| **Active Pawns** | 26 | 26 | Identical simulation load |
| **Instances Drawn** | 14,485 | 14,489 (56 batches) | 🟢 **GPU instancing fully active** across 12,989 sandstone rocks |
| **Console Errors** | 0 | 0 | 🟢 Clean run, zero magenta error shaders |
| **EditMode Tests** | 21 / 21 passed | 21 / 21 passed | 🟢 100% green |

---

## Invariant Verification

1. **Invariant 1 (Instancing)**: GPU instancing verified active in URP Lit shader across 14,489 instances with 42.7% latency improvement.
2. **Invariant 2 (Fallback)**: Fallback unit cube path in `VisualRegistry.FallbackMesh` confirmed working.
3. **Invariant 3 (Relative Seam)**: `Packages/manifest.json` preserves relative path `file:../../simWorld/src/SimWorld.Core`.
4. **Invariant 4 (Evidence)**: Before & after screenshots and exact metrics recorded.
5. **Silent Failure Guard**: `RequireShader` helper added to `MapRenderer.cs` to guarantee any missing/invalid shader throws immediately on the first frame rather than silently failing to magenta.

---

## Visual Artifact

Screenshot captured to: [`docs/baseline/after.png`](after.png) (and [`Assets/Screenshots/URP_GameView.png`](../../Assets/Screenshots/URP_GameView.png)).
