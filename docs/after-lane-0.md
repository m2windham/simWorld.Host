# After lane zero

Lane zero is **closed and verified**. This says what that bought, what it did not,
and which lane is worth taking next — a recommendation from the core side, not an
instruction. Lane assignment is the host team's call.

## What lane zero actually bought

Verified independently, by reading the files rather than the report:

- URP 17.5.0 in, all three `Shader.Find` call sites migrated, instancing preserved.
- `RequireShader` guarding every one of them, so a missing shader throws on the first
  frame instead of rendering 13,000 magenta rocks.
- The core package path still relative.
- Draw calls 112 → 64, SetPass 34 → 25. Real, camera-independent wins.
- No magenta, confirmed by looking.

**What it did not buy is any change to the look**, and the screenshots say so plainly.
Lane zero purchased Shader Graph, SSAO, colour grading and post-processing. All of it
is still unspent.

## The thing the screenshots make obvious

Two numbers from the baseline, worth sitting with:

| | |
| --- | --- |
| Total instances drawn | 14,485 |
| Sandstone rendered as **fallback cubes** | **12,989** |

**Ninety per cent of what is on screen is the placeholder cube.** Not a rock that needs
better material — a literal unit cube standing in for a rock, because
`Assets/Resources/Models/` has no `Sandstone.fbx`.

That is not a criticism of the renderer; it is what `VisualRegistry`'s fallback is for,
and it is the reason the renderer could be built and profiled before any art existed.
But it does decide what "make it look like Timberborn" means right now, and the answer
is not what most people would guess.

## Recommended order, and the reasoning

### First: lighting and post (lane 1)

**It is the only lane that improves every pixel on screen without a single new asset.**

The current scene reads as a debug view: flat ambient light, no shadows to speak of, no
depth cue, a default sky. Timberborn's look is carried as much by warm soft light,
gentle contact shadows, ambient occlusion and a slight tilt-shift as it is by the
models. Those apply to a cube exactly as well as they apply to a finished mesh.

So lane 1 is the highest ratio of visible change to work in the whole list, and it is
the only one whose result is not gated on the art pipeline. A warm key, a strong ambient
fill, SSAO, a colour-grading LUT and shallow depth of field at god scale would transform
that screenshot this afternoon, with the cubes still in it.

### Second: terrain and water (lane 3)

The ground is most of the frame. A stylised terrain shader — value contrast between
soil, rock and grass, a readable water surface — changes more of the image than any
building ever will, and it is shader work rather than asset work, so it does not queue
behind modelling.

### Third: rock, then structures (lanes 4 and 5)

One `Sandstone.fbx` with three or four variants retires 12,989 cubes. Drop it in
`Assets/Resources/Models/` and `VisualRegistry` picks it up by `defName` with **no code
change and no shared file edited** — the variant suffix convention (`Sandstone_a` …
`Sandstone_d`) is already implemented and chooses deterministically by thing id.

That is the single highest-value asset in the project and it is one model.

## The prerequisite nobody has done yet

`AGENTS.md` §11 step 2, still outstanding:

> Assemble a reference board for the target look and agree on it **before** anyone
> writes a shader. "Timberborn-like" means six different things to six agents, and the
> cheapest time to discover that is now.

This matters more once lane 1 starts than it did during lane zero, because lane 1 is
where taste enters. Two agents with different mental images of "warm" will produce two
scenes that cannot be merged into one look. Pin it first: a handful of reference images,
a palette, and one sentence per material on what it should read as.

## What has not changed

The lever test still outranks the aesthetic (`AGENTS.md` §9). A control may exist only
where the simulation can carry the instruction to its consequence. The settlement
command surface on the core side has just grown — blueprints, growing zones, stockpiles,
with more landing — so there will shortly be more for the UI to legitimately expose. It
will arrive through `Map/View`, as values and `defName` handles, the same as everything
else.
