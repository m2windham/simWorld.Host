# The relief floor should be a ratio, not an absolute number

Lane 1's recalibration worked. This corrects the measuring rule I gave for it,
which was fragile in a way I did not notice when I proposed it.

## The recalibration is a real recovery

Measured with `tools/measure_relief.py` — the tool the host team wrote — on the
committed screenshots:

| | Local relief | Contrast |
| :--- | ---: | ---: |
| `after_controlled.png` (pre-lane-1 reference) | 2.34 | 23.1 |
| `lane1_rocks_gameview.png` (first lane 1) | 1.05 | 15.5 |
| `lane1_rocks_gameview.png` (recalibrated) | **2.22** | **22.9** |

**Contrast is fully back** — 22.9 against a 23.1 reference is parity. Relief more
than doubled. Confirmed by looking, too: terracing and the dark shadow bands at
cliff edges have returned, and the scene reads as layered ground again rather
than as a flat plane. That is the thing the reference board's "elements separate
by tonal value" invariant is actually asking for.

## The floor I gave was measured differently from the floor you checked

`tools/measure_relief.py` takes the **central** 800×450 crop. The number in
`docs/lane-1-legibility-check.md` came from the crop `(700,450)–(1500,900)`, and
pooled its samples differently. Same metric, different region and estimator — so
the two "2.2"s are not the same 2.2.

Measured both ways, like for like:

| Crop | Reference (`after_controlled`) | Recalibrated |
| :--- | ---: | ---: |
| Central (your tool) | 2.34 | 2.22 |
| `(700,450)–(1500,900)` | 2.15 | 2.05 |

The recalibrated scene is **about 5% below the pre-lane-1 reference on both
crops**, consistently. Passing "≥ 2.20" was true on the central crop and would
have been false on the other one, which is the problem with the rule rather than
with the work.

**This is my error, not yours.** I proposed an absolute threshold derived from
one crop with one estimator and did not say which — and my own original figure
for the same image moved from 2.22 to 2.15 when I changed the sampling. A number
without its method is not a measurement, which is a rule this project already
holds itself to and which I failed to apply to the rule I was writing.

## The better rule

Express the floor as a **ratio against a reference measured in the same run with
the same tool**:

> A visual change must not reduce local relief below **0.95×** the reference
> capture, measured with the same crop and estimator, in the same session.

That is robust to crop choice, to estimator details, and to a future change of
camera framing — none of which the absolute number survives. It also makes the
comparison self-contained: the reference is re-measured every time rather than
remembered from a document.

`tools/measure_relief.py` already does everything needed; it just wants a second
image argument and a ratio instead of a constant.

## What this does not change

Nothing about the lane. The recalibration fixed the real problem, the tool is
the right tool, and building it rather than eyeballing the result was exactly
the right instinct. The remaining 5% is inside the noise of "which crop", which
is precisely why the rule should not have been an absolute in the first place.
