# Baseline Render Performance (Built-in Pipeline)

> **Captured**: 2026-09-20 10:49:33  
> **Environment**: Windows 11, AMD Radeon RX 6650 XT (8 GB VRAM), Unity 6000.5.0f1  
> **Pipeline**: Legacy Built-in Render Pipeline (`m_CustomRenderPipeline: {fileID: 0}`)  
> **Scene**: `GodView.unity` (Play Mode)  
> **World / Settlement**: Scenario `TribalStart`, Seed `simworld-host`, `Blackland` open on tile 21  

---

## Telemetry & Frame Cost

- **Frame Latency**: **5.76 ms** (smoothDeltaTime) / 9.35 ms (unscaledDeltaTime)
- **Throughput**: **~173.7 FPS**
- **Draw Calls**: **112**
- **SetPass Calls**: **34**
- **Geometry**: **393,190 triangles** | **730,124 vertices**
- **Map Dimensions**: 200 × 200 interior
- **Active Pawns**: 26 (20 citizens, 6 wild animals)
- **Total Thing Instances**: **14,485** across 12 batches
  - `Sandstone`: 12,989 (rendered as fallback cubes)
  - `WildPlant`: 775
  - `Plant_Berry`: 605
  - `Wall`: 74
  - `ChunkGranite`: 18
  - `ChunkLimestone`: 10
  - `ChunkSandstone`: 6
  - Items / Blueprints: 8

---

## Visual Artifact

Screenshot captured to: [`docs/baseline/before.png`](before.png) (and [`Assets/Screenshots/Baseline_BuiltIn_GameView.png`](../../Assets/Screenshots/Baseline_BuiltIn_GameView.png)).
