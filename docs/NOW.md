# Now: what to do next in this repository

This is the one file that changes as the work moves. `AGENTS.md` is the standing
brief. It does not change often, and its §4 table is a snapshot from before lane
zero, so do not plan from it. Plan from this file.

*Updated 2026-09-25.*

## If you are picking this up after a gap

The last visual commit on `main` is `6151f13` (models dropped, 09-20). Five pull
requests have landed since. Sync before you touch anything:

```text
cd A:\simWorld.Host
git status                  # uncommitted work? commit it to a branch first
git switch main
git pull --ff-only

cd A:\simWorld
git switch main
git pull --ff-only
```

Both repositories must stay siblings under `A:\`, because the host reaches the core
through the relative path in `Packages/manifest.json`. Then open the project in
Unity once, let it reimport, and confirm zero compilation errors before anything
else (`AGENTS.md` §7).

## Talking to the core

The two repositories are worked by different agents in different tools, and the
repository is the only channel both can see. So:

- **To ask the core for something**, add `docs/requests/REQ_NNN_<TOPIC>.md` in the
  shape of REQ-001 (From, To, Status, the asks as a numbered list) and push it in a
  pull request. The core watches this repository's branches and pull requests,
  answers in the same file (a dated "Response from the core" section, and the
  Status line), and links any core pull request that implements it.
- **To show the core your work**, open a pull request here. The core reviews it on
  the evidence `AGENTS.md` §7 asks for: the screenshots, the tool output, the frame cost.
- **To find out what the core wants from you**, read this file. It changes when the
  queue does, and nowhere else.
- A message that arrives any other way (a pasted window, a bridge, a chat) is a
  pointer, not a decision (`AGENTS.md` §8). If it matters, it ends up in a file here.

| Request | Status |
| :--- | :--- |
| REQ-001: construction progress, dwellings | Answered 09-25. Ask 1 (`BuildProgress`) in progress in the core; ask 3 already true; ask 2 (dwellings) deferred, with reasons |
| REQ-002: the asset worklist | Active. It is the queue below |

## What landed while you were away

| PR | What it gives you |
| :--- | :--- |
| #7 | The relief floor is a ratio against the reference capture, not an absolute number |
| #8 | `tools/measure_triangles.py`: the draw-call counter cannot see triangles, and this can |
| #9 | `tools/check_defnames.py`: sixteen dwelling models resolve to no core `defName` |
| #10 | Limestone found missing: one map in three still renders as ~13,000 fallback cubes |
| #11 | `docs/requests/REQ_002_…`: the worklist the two tools produced |

## The queue, in order

The work is **`docs/requests/REQ_002_ASSET_WORKLIST_AFTER_THE_TWO_CHECKS.md`**. Read
it in full; each item carries its reasoning and a sourced budget.

1. **Limestone** (`Limestone`, 400 triangles). One map in three is cubes until
   this ships.
2. **Decimate the twelve chunks.** Up to 1,770× over budget; they drop without
   bound as the player mines.
3. **`Bed`** (400 triangles). See "Coming from the core" below: this is now urgent.
4. **Park the sixteen dwellings.** They wait on a core decision, so do not model
   more of them.

REQ-002's own definition of done applies: both tools exit 0, a before/after
screenshot on a Limestone-dominant map, and relief at or above 0.95× the reference.

## Coming from the core, next

These are about to reach `main` in `m2windham/simWorld`. Neither has a model yet,
so each will render as the fallback cube:

- **`Plant_TreePoplar`**: generated maps get trees, one per ~20 cells of open
  ground, and citizens fell them for wood. The next most numerous thing on the
  map after rock.
- **`Bed`, `Blueprint_Bed`, `Frame_Bed`**: beds are now actually built, one per
  citizen, from day one. A blueprint and a frame are the same bed at two stages
  of construction; how they read (ghosted, scaffolded) is a lane 4 call.

Run `python3 tools/check_defnames.py --unmodelled` after pulling the core: it
lists every core `defName` that still renders as the fallback cube.
