# Lane 0: the URP migration, step by step

An ordered, verifiable procedure for the migration approved in `c00b66a`. It
exists so that whoever executes — a Claude agent here, an agent in Antigravity,
or a person — does the steps in an order where a mistake is still cheap.

Read `AGENTS.md` first. This runbook does not replace it; §4 and §12 there carry
the reasoning, and this carries the sequence.

**Lane 0 owns:** `ProjectSettings/*`, `Packages/manifest.json`, the URP pipeline
and renderer assets, and `Assets/Scripts/MapRenderer.cs`. No other lane touches
any of these until Lane 0 merges.

---

## Step 0. The baseline. Do this first; it cannot be recovered later

Before changing a single setting. Once the pipeline is swapped there is no way
back to these numbers without reverting the whole migration, and every claim
afterwards is measured against them.

- [ ] Open the `GodView` scene, enter play mode, generate a **settlement
      interior** (not an empty scene — the point is the ~13,000 instances).
- [ ] `unity_screenshot_game` — save as `docs/baseline/before.png`.
- [ ] Record, in `docs/baseline/before.md`: frame time in milliseconds, the
      instance count actually drawn, `VisualRegistry.LoadedDefNameCount`, batch
      or draw-call count, and the machine it was measured on.
- [ ] Commit those two files on their own, before anything else changes.

If you skip this step you cannot honestly answer "did it get slower", and the
answer to that question is the only thing that makes the migration safe to keep.

## Step 1. Add URP

- [ ] Add `com.unity.render-pipelines.universal` to `Packages/manifest.json`.
- [ ] **Check that `com.simworld.core` still reads
      `file:../../simWorld/src/SimWorld.Core`.** Package Manager rewrites this
      to an absolute path at the slightest provocation, and an absolute path
      works on exactly one machine and fails silently everywhere else. This is
      invariant 3 and it is the one most likely to break by accident.

## Step 2. Create and assign the pipeline

- [ ] Create a URP Asset with a Universal Renderer.
- [ ] Assign it in Graphics Settings and in every Quality level. A quality level
      left unassigned falls back to Built-in and renders a different scene
      depending on the player's settings — which reads as an intermittent bug
      months later.

## Step 3. Convert materials

- [ ] Run **Window > Rendering > Render Pipeline Converter**, Built-in to URP.
- [ ] Convert materials and, separately, the scene's lighting settings.

This handles assets. It does **not** handle materials built in code, which is
the next step and the one that matters most.

## Step 4. `MapRenderer.cs` — the step that actually breaks

`MapRenderer` does not take materials from the project. It builds them from
Built-in shader names at runtime:

| Line | Current | Replace with | Draws |
| --- | --- | --- | --- |
| 214 | `Shader.Find("Unlit/Texture")` | `Universal Render Pipeline/Unlit` | Terrain |
| 248 | `Shader.Find("Unlit/Transparent")` | `Universal Render Pipeline/Unlit`, surface type Transparent | Roofs |
| 563 | `Shader.Find("Standard")` | `Universal Render Pipeline/Lit` | Every instanced thing |

**`"Standard"` does not exist in URP.** `Shader.Find` returns `null`, and
`new Material(null)` is the magenta error material — so all ~13,000 instances
render magenta while the code reports no error at all.

- [ ] Replace all three.
- [ ] Keep `enableInstancing = true` at line 563. URP's Lit shader supports GPU
      instancing; a Shader Graph replacement must have it enabled explicitly.
      This is invariant 1 at the exact line where it applies.

### Make the silent failure loud while you are in there

The reason this defect could exist is that `Shader.Find` returning null is not
an error in C# — it becomes one three frames later, visually, with no log line.
That is the same shape as every defect the core project has spent this phase
finding: something that looks like it works because nothing asked it to prove
it.

Add a single resolve helper that refuses to hand back a null shader, and route
all three calls through it:

```csharp
private static Shader RequireShader(string name)
{
    Shader shader = Shader.Find(name);
    if (shader == null)
        throw new InvalidOperationException(
            $"Shader '{name}' not found. It is almost certainly a Built-in " +
            "pipeline shader that does not exist under the active render " +
            "pipeline. Renderer materials must name URP shaders.");
    return shader;
}
```

A thrown exception on the first frame is worth far more than a magenta map, and
it means the next pipeline change fails at the line that caused it rather than
on somebody's screenshot.

## Step 5. Prove it

- [ ] `unity_get_compilation_errors` — zero.
- [ ] Play mode, generate a settlement interior again, same scenario as Step 0.
- [ ] `unity_console_log` — no exceptions. A scene that renders while throwing
      every frame is not working.
- [ ] **Look at it.** No magenta anywhere. This is the check that the automated
      ones cannot make for you.
- [ ] `unity_screenshot_game` — save as `docs/baseline/after.png`.
- [ ] Record the same numbers as Step 0 in `docs/baseline/after.md`.
- [ ] Run the EditMode tests in `Assets/Tests/EditMode/`. `MapRendererTests` and
      `VisualRegistryTests` must still pass — if the fallback cube path broke,
      that is invariant 2 and the migration is not done.

## Step 6. The pull request

Body must contain, as evidence rather than assertion:

- Before and after screenshots, side by side.
- Before and after frame cost and instance count, with the machine named.
- A line for each of the four invariants, stating how it was verified.
- The `Packages/manifest.json` diff, showing the core path is still relative.

A reviewer who cannot see the two screenshots cannot review this change.

---

## If it goes wrong

The migration is one commit's worth of settings and one file of code. If the
frame cost regresses badly, or instancing silently stops batching, **revert and
report the numbers** rather than tuning under pressure. A migration that halves
the frame rate is not a milestone with a follow-up ticket; it is a change that
has not met invariant 1 yet, and the baseline from Step 0 is what lets anyone
say so with a number instead of an impression.
