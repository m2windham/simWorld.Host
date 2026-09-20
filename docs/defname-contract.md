# The defName contract, and the test that cannot check it

`VisualRegistry` resolves a model by **defName string**, with no era-keyed or category-keyed
indirection — `docs/host/rendering-method.md` in the core is explicit that "the `defName → mesh`
mapping belongs to the host, as a registry keyed by `defName`."

So a model is drawn **if and only if** some `ThingDef` in `SimWorld.Core` carries exactly that
defName. There is no second path.

That makes the naming a contract between two repositories, and a contract nothing was checking.

## What `VisualRegistryTests` can and cannot establish

From `Assets/Tests/EditMode/VisualRegistryTests.cs`:

```csharp
Assert.AreEqual(4, VisualRegistry.VariantCount(era), $"{era} must have exactly 4 variants imported.");
GameObject go = VisualRegistry.Resolve(era, thingId);
Assert.IsNotNull(go, $"{era} must resolve to a valid prefab for thingId {thingId}");
```

Both assertions are true and both are worth having. They establish that the files imported, that
there are four variants, and that variant selection spreads across thing ids deterministically.

**Neither can establish that anything will ever ask for that name**, because the answer lives in
the other repository. The test loads a file and asks the registry to find it; of course it does.
Nothing in the Unity project knows which defNames the simulation actually spawns.

That is not a flaw in the test. It is the limit of where the test can see, and it is exactly the
shape of defect this project keeps finding on the core side: **built, tested, and nothing ever
calls it.** This is the first instance of it across the repository boundary.

## What the check found

`python3 tools/check_defnames.py`, reading the core through the relative package path in
`Packages/manifest.json`:

| | |
| :--- | ---: |
| Core ThingDefs | 133 |
| Model families | 22 |
| **Resolve** | **18** |
| **Resolve to nothing** | **4** |

The eighteen that resolve are all correct and the naming convention is being followed properly:
`Bow_Short`, `ChunkGranite`, `ChunkLimestone`, `ChunkSandstone`, `Door`, `Granite`, `MineableGold`,
`MineableSilver`, `MineableSteel`, `Plant_Berry`, `Sandstone`, `StorageHut`, `Wall`, `WallGranite`,
`WallLimestone`, `WallSandstone`, `WildPlant`, `WoodLog`.

The four that do not are the dwellings: **`House_Paleo`, `House_Meso`, `House_Neo`, `House_Chalco`
— sixteen files.** No `ThingDef` in the core carries any of those names. The only `House` in the
core's source is `FamilyManager.FoundHousehold`, which is demography and has nothing to do with
buildings.

Two further details confirm the mismatch is structural rather than a typo:

- **The era names do not correspond to anything.** The core's era ladder is `SticksAndStones,
  Agrarian, Bronze, Classical, Medieval, Industrial, Information, Exotic`. Paleo / Meso / Neo /
  Chalco appear nowhere in the core, in any file.
- **A multi-cell house is not representable yet.** `ThingDef.size` defaults to 1×1 and no shipped
  content sets a footprint, so every building in this port is implicitly one cell. A dwelling mesh
  spanning several cells has nothing to attach to.

## What the core actually builds as shelter

Worth stating plainly, because it is thinner than the models assume. `Building.SettlementConstructionInitiative`
computes needs for exactly three things:

| Thing | Target | Cap |
| :--- | :--- | :--- |
| `Bed` | one per citizen | none |
| `Wall` (or the stone variants) | `ceil(citizens × 2)` | **40, whatever the population** |
| `StorageHut` | `ceil(stored goods / 50)` | none |

None of these is placed at map generation — they are built by citizens over subsequent in-game
days. So a settlement's shelter is a bed each and up to forty wall segments. There is no dwelling.

## This is not a modelling mistake, and the fix is not obvious

The sixteen dwellings are good work aimed at a concept the core does not have. They are, if
anything, evidence for something the core is missing: a settlement you watch grow is exactly the
kind of game where seeing a house go up is the progress, and `Bed` + forty walls does not read as
a village. Timberborn and Banished both trade heavily on that legibility.

But **"the core should have dwellings" is a design claim, and a design claim is an unproven claim.**
It is the core's decision, not this repository's, and it has two real blockers (the era ladder and
the 1×1 footprint) that have to be answered before any model name is correct. I have raised it on
the core side. Until it is decided, the honest status of these sixteen files is **parked, not
broken** — nothing about them needs changing if the answer comes back yes.

What should change now is that this is **visible**. A model that resolves to nothing should not
look identical to one that works.

## Running it

```sh
python3 tools/check_defnames.py              # exit 1 if any model resolves to nothing
python3 tools/check_defnames.py --unmodelled # also list core defs still on the fallback cube
```

`--unmodelled` answers the other half of the contract, and is the more useful direction for
planning an asset lane: which of the core's 133 ThingDefs are still drawn as a primitive cube. That
is a worklist rather than a defect.

## What this does not claim

- **Not that the dwelling models are bad.** They are within one order of magnitude of a sensible
  budget and appear to follow the variant convention correctly. The problem is the name, and
  possibly the concept behind it, not the geometry.
- **Not that the check replaces `VisualRegistryTests`.** It answers a different question and both
  are needed: that test proves a file loads and varies; this proves the name is one the simulation
  will use.
- **Not that every resolving model is therefore drawn often.** Resolving is necessary, not
  sufficient — `Door`, for instance, resolves correctly but no system currently places one, so the
  expected on-map count is zero. That is a core-side gap and it is recorded there, not here.
