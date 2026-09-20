#!/usr/bin/env python3
"""
Checks that every model under Assets/Resources/Models/ corresponds to a defName the core
actually ships, and reports which core ThingDefs still have no model.

Why this exists: VisualRegistry resolves a model by defName string, with no era-keyed or
category-keyed indirection (docs/host/rendering-method.md). So a model file is drawn if and only
if some ThingDef in SimWorld.Core carries exactly that defName. A model whose name matches no
def loads fine, passes a registry test, and is never drawn by anything — which is indistinguishable
from working until someone goes looking.

That is the gap this closes. `VisualRegistryTests` can only ask "does this file load"; it cannot
ask "will the simulation ever request this name", because the answer lives in the other
repository. This tool reads it from there.

The core is found via Packages/manifest.json's relative path for com.simworld.core, so it works
wherever the pair of repositories is checked out, and fails loudly rather than silently passing if
the core cannot be found.

Usage:
    python3 tools/check_defnames.py              # check models against the core
    python3 tools/check_defnames.py --unmodelled # also list core defs with no model yet
    python3 tools/check_defnames.py --core PATH  # point at the core explicitly

Exit status is 1 if any model resolves to nothing.
"""

import glob
import json
import os
import re
import sys

VARIANT_SUFFIX = re.compile(r"_[a-z]$")


def find_core(explicit: "str | None") -> str:
    """Locate SimWorld.Core, preferring the path the Unity package manifest actually uses."""
    if explicit:
        return explicit

    manifest = os.path.join("Packages", "manifest.json")
    if os.path.isfile(manifest):
        with open(manifest, encoding="utf-8") as f:
            deps = json.load(f).get("dependencies", {})
        entry = deps.get("com.simworld.core", "")
        if entry.startswith("file:"):
            # The manifest path is relative to Packages/, not to the repository root.
            resolved = os.path.normpath(os.path.join("Packages", entry[len("file:") :]))
            if os.path.isdir(resolved):
                return resolved

    fallback = os.path.normpath(os.path.join("..", "simWorld", "src", "SimWorld.Core"))
    if os.path.isdir(fallback):
        return fallback

    raise SystemExit(
        "Could not find SimWorld.Core. Checked Packages/manifest.json's com.simworld.core "
        "path and ../simWorld/src/SimWorld.Core. Pass --core PATH."
    )


def core_thing_defnames(core: str) -> "set[str]":
    """Every ThingDef defName the core ships. These are the names a Thing can present to the registry."""
    pattern = os.path.join(core, "Data", "Core", "Defs", "ThingDefs_*", "**", "*.xml")
    files = glob.glob(pattern, recursive=True)
    if not files:
        raise SystemExit(f"No ThingDef XML found under {core}. Is that really SimWorld.Core?")
    names = set()
    for path in files:
        with open(path, encoding="utf-8") as f:
            names.update(m.group(1).strip() for m in re.finditer(r"<defName>([^<]+)</defName>", f.read()))
    return names


def model_families() -> "dict[str, list[str]]":
    """Model files grouped by the defName they will be asked for, stripping the _a.._d variant suffix."""
    families: "dict[str, list[str]]" = {}
    for path in sorted(glob.glob(os.path.join("Assets", "Resources", "Models", "*.fbx"))):
        stem = os.path.basename(path)[: -len(".fbx")]
        families.setdefault(VARIANT_SUFFIX.sub("", stem), []).append(os.path.basename(path))
    return families


def main(argv: "list[str]") -> int:
    show_unmodelled = "--unmodelled" in argv
    explicit = None
    if "--core" in argv:
        explicit = argv[argv.index("--core") + 1]

    core = find_core(explicit)
    defnames = core_thing_defnames(core)
    families = model_families()
    if not families:
        raise SystemExit("No models found under Assets/Resources/Models/. Run from the repository root.")

    resolved = {k: v for k, v in families.items() if k in defnames}
    orphaned = {k: v for k, v in families.items() if k not in defnames}

    print(f"Core:   {core}  ({len(defnames)} ThingDefs)")
    print(f"Models: {len(families)} families, {sum(len(v) for v in families.values())} files")
    print()

    if orphaned:
        print("MODELS THAT RESOLVE TO NOTHING")
        print("These load, pass a registry test, and are never drawn: no ThingDef carries the name.")
        print()
        for family, files in sorted(orphaned.items()):
            print(f"  {family:20s}  {len(files)} file(s): {', '.join(files)}")
        print()

    print(f"{len(resolved)} family/families resolve: {', '.join(sorted(resolved))}")

    if show_unmodelled:
        missing = sorted(d for d in defnames if d not in families)
        print()
        print(f"CORE DEFS WITH NO MODEL ({len(missing)}) — these fall back to the primitive cube:")
        for name in missing:
            print(f"  {name}")

    print()
    if orphaned:
        print(f"{len(orphaned)} family/families resolve to nothing. See docs/defname-contract.md.")
        return 1
    print("Every model corresponds to a defName the core ships.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
