#!/usr/bin/env python3
"""
Counts triangles in FBX models under Assets/Resources/Models/ and checks them against the
per-class budgets in docs/triangle-budget.md.

Why this exists: VisualRegistry resolves a model by defName and draws it with GPU instancing.
Instancing makes draw calls cheap; it does nothing at all for triangle count. A mesh that is
1000x heavier than its neighbour costs 1000x more to rasterise every frame, and nothing in the
Unity console, the EditMode tests or the draw-call counter will say so. This is the instrument
that says so.

The parser reads the binary FBX node tree directly (no Unity, no FBX SDK) and sums the
PolygonVertexIndex arrays, counting (n - 2) triangles per n-gon. That is the triangle count the
GPU sees after Unity triangulates on import, so it matches the Stats window rather than
approximating it.

Usage:
    python3 tools/measure_triangles.py                       # check every model against budget
    python3 tools/measure_triangles.py Assets/.../Foo.fbx    # measure specific files
    python3 tools/measure_triangles.py --all                 # list every model, no pass/fail

Exit status is 1 if any model is over budget, so this can gate a lane.
"""

import glob
import os
import struct
import sys
import zlib

# Budgets are per docs/triangle-budget.md. Keyed by filename prefix, longest match wins.
#
# Each budget carries whether it is SOURCED or PROPOSED, and only a SOURCED budget fails the
# run. That distinction is the whole point: a sourced budget is derived from something already
# shipped and accepted in this repository, a proposed one is a reviewer's judgement that has not
# been tested against a render. Failing a lane on an opinion would make this tool an argument
# with an exit code. It may under-claim; it must never over-claim.
#
#   SOURCED   ground scatter: Sandstone_a..d shipped in lane 1 at 248-258 triangles, were
#             reviewed, and read correctly at ViewSize 60. That is a real reference for what a
#             scattered ground object needs. 400 leaves headroom above it.
#   PROPOSED  everything else: derived from "one class should not, on its own, exceed the whole
#             pre-asset scene" (393,000 triangles, docs/baseline/after.md) divided by a rough
#             instance count. Reasoning, not measurement. Argue with these on the PR.
SOURCED, PROPOSED = "sourced", "proposed"

BUDGETS = {
    "Chunk": (400, SOURCED),
    "Sandstone": (400, SOURCED),
    "Granite": (400, SOURCED),
    "Limestone": (400, SOURCED),
    "WildPlant": (400, SOURCED),
    "Plant_": (400, SOURCED),
    "WoodLog": (400, SOURCED),
    "Bow_": (400, PROPOSED),
    "Mineable": (1200, PROPOSED),
    "House_": (2500, PROPOSED),
}
DEFAULT_BUDGET = (1200, PROPOSED)


def budget_for(name: str) -> "tuple[int, str, bool]":
    """Returns (budget, basis, classified). Longest matching prefix wins."""
    best, best_len = None, -1
    for prefix, entry in BUDGETS.items():
        if name.startswith(prefix) and len(prefix) > best_len:
            best, best_len = entry, len(prefix)
    if best is None:
        return DEFAULT_BUDGET[0], DEFAULT_BUDGET[1], False
    return best[0], best[1], True


def count(path: str) -> "tuple[int, int]":
    """Returns (vertices, triangles) for a binary FBX. Raises on an ASCII or malformed file."""
    with open(path, "rb") as f:
        header = f.read(27)
        if not header.startswith(b"Kaydara FBX Binary"):
            raise ValueError("not a binary FBX (ASCII FBX is not supported)")
        version = struct.unpack("<I", header[23:27])[0]
        wide = version >= 7500  # 7.5 widened the node record offsets to 64-bit

        verts = 0
        tris = 0

        def walk():
            nonlocal verts, tris
            while True:
                if wide:
                    raw = f.read(25)
                    if len(raw) < 25:
                        return
                    end_offset, num_props, prop_len, name_len = struct.unpack("<QQQB", raw)
                else:
                    raw = f.read(13)
                    if len(raw) < 13:
                        return
                    end_offset, num_props, prop_len, name_len = struct.unpack("<IIIB", raw)
                if end_offset == 0:  # null record: end of this node's child list
                    return
                name = f.read(name_len)
                props_start = f.tell()

                if name in (b"Vertices", b"PolygonVertexIndex") and num_props >= 1:
                    type_code = f.read(1)
                    if type_code in (b"d", b"f", b"i", b"l"):
                        array_len, encoding, compressed_len = struct.unpack("<III", f.read(12))
                        if name == b"Vertices":
                            verts += array_len // 3
                        elif type_code == b"i":
                            payload = f.read(compressed_len)
                            if encoding == 1:
                                payload = zlib.decompress(payload)
                            indices = struct.unpack("<%di" % array_len, payload[: array_len * 4])
                            # FBX marks the last index of each polygon by ones-complementing it,
                            # so a negative value closes the current n-gon.
                            run = 0
                            for value in indices:
                                run += 1
                                if value < 0:
                                    tris += max(run - 2, 0)
                                    run = 0

                f.seek(props_start + prop_len)
                if f.tell() < end_offset:
                    walk()
                f.seek(end_offset)

        walk()
        return verts, tris


def main(argv: "list[str]") -> int:
    show_all = "--all" in argv
    paths = [a for a in argv if not a.startswith("--")]
    if not paths:
        paths = sorted(glob.glob("Assets/Resources/Models/*.fbx"))
    if not paths:
        print("No models found. Run from the repository root.", file=sys.stderr)
        return 2

    rows = []
    for path in paths:
        name = os.path.basename(path)
        try:
            verts, tris = count(path)
        except Exception as exc:  # a file we cannot read is a failure, not a pass
            rows.append((name, os.path.getsize(path), None, None, None, None, str(exc)))
            continue
        budget, basis, classified = budget_for(name.removesuffix(".fbx"))
        note = None if classified else "unclassified, default budget"
        rows.append((name, os.path.getsize(path), verts, tris, budget, basis, note))

    rows.sort(key=lambda r: -(r[3] or 0))

    failures = [r for r in rows if r[3] is not None and r[3] > r[4] and r[5] == SOURCED]
    notes = [r for r in rows if r[3] is not None and r[3] > r[4] and r[5] == PROPOSED]
    unreadable = [r for r in rows if r[3] is None]

    print(f"{'model':30s} {'bytes':>12s} {'tris':>9s} {'budget':>8s}  status")
    print("-" * 80)
    for name, size, verts, tris, budget, basis, note in rows:
        if tris is None:
            print(f"{name:30s} {size:>12,} {'?':>9s} {'?':>8s}  UNREADABLE: {note}")
            continue
        if tris > budget:
            label = "OVER" if basis == SOURCED else "over (proposed budget)"
            status = f"{label} by {tris / budget:.0f}x"
        else:
            status = "ok"
        if note:
            status += f" — {note}"
        if show_all or tris > budget:
            print(f"{name:30s} {size:>12,} {tris:>9,} {budget:>8,}  {status}")

    print()
    total = sum(r[3] for r in rows if r[3] is not None)
    print(f"{len(rows)} models, {total:,} triangles across one instance of each.")
    if unreadable:
        print(f"{len(unreadable)} model(s) could not be read — treated as failures.")
    if failures:
        print(f"{len(failures)} model(s) over a SOURCED budget. See docs/triangle-budget.md.")
    if notes:
        print(f"{len(notes)} model(s) over a PROPOSED budget — a note for review, not a failure.")
    if not failures and not notes and not unreadable:
        print("Every model is within its budget.")
    return 1 if (failures or unreadable) else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
