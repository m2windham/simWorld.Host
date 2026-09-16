using UnityEngine;

namespace SimWorldHost
{
    /// <summary>
    /// Turns a defName into a colour, for a renderer that has no art yet.
    ///
    /// <para/><b>The seam hands the host a defName and nothing else</b> — no colour, no category, by
    /// design. So this is entirely the host's guess, and it is kept in its own file because it is policy
    /// rather than rendering: it will be replaced wholesale when real models land, and a test can pin its
    /// behaviour without standing a renderer up.
    ///
    /// <para/>Two rules it follows. <b>Stable</b>: the same defName is the same colour in every session
    /// and on every machine, because <c>string.GetHashCode</c> is explicitly not stable across processes
    /// and a rock that changes colour between runs is a bug report waiting to happen — the same discipline
    /// that makes the core pick mesh variants from a ThingId rather than a roll. <b>Legible</b>: a pure
    /// hash is stable and distinct but meaningless, and a first render that paints shallow water olive and
    /// soil pink is worse than useless, because it is confidently wrong about the one thing the reader is
    /// checking.
    /// </summary>
    public static class DefColors
    {
        /// <summary>Reserved for an empty or missing defName: nothing in content should ever be this.</summary>
        public static readonly Color Missing = new Color(0.8f, 0f, 0.8f);

        /// <summary>The colour for a defName.</summary>
        public static Color For(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return Missing;

            uint h = Hash(defName);
            float hue = (h % 3600u) / 3600f;
            float sat = 0.32f + ((h >> 12) % 43u) / 100f;   // 0.32 .. 0.74
            float val = 0.42f + ((h >> 21) % 47u) / 100f;   // 0.42 .. 0.88
            return Family(defName, hue, sat, val);
        }

        /// <summary>
        /// Nudges an already-hashed HSV into a family the eye expects, where the name says one plainly.
        ///
        /// <para/><b>This carries no authority.</b> Matching on name stems is the soft form of the mistake
        /// the seam's trap 4 warns about — a content pack whose stone def is not spelled "stone" simply
        /// falls through to the hash, which is exactly why the hash has to stay good on its own rather than
        /// become a fallback nobody looks at. The real fix is for the seam to carry a category per palette
        /// entry; that is a core change to propose, not something to fake convincingly on this side.
        /// </summary>
        public static Color Family(string defName, float hue, float sat, float val)
        {
            if (string.IsNullOrEmpty(defName)) return Missing;

            string k = defName.ToLowerInvariant();
            uint h = Hash(defName);

            float famHue;
            float satScale;
            float valBias;

            // Order matters: "Sandstone" is stone, not sand, and "ChunkGranite" is stone, not a chunk of
            // anything in particular. Most specific first.
            if (Has(k, "water", "marsh")) { famHue = 0.56f; satScale = 1.20f; valBias = -0.05f; }
            else if (Has(k, "ice", "snow")) { famHue = 0.52f; satScale = 0.30f; valBias = 0.25f; }
            else if (Has(k, "gold")) { famHue = 0.13f; satScale = 1.40f; valBias = 0.10f; }
            else if (Has(k, "silver")) { famHue = 0.00f; satScale = 0.08f; valBias = 0.22f; }
            else if (Has(k, "steel")) { famHue = 0.58f; satScale = 0.18f; valBias = 0.00f; }
            else if (Has(k, "stone", "rock", "granite", "limestone", "slate", "marble", "gravel", "chunk"))
            {
                famHue = 0.08f; satScale = 0.22f; valBias = 0.00f;
            }
            else if (Has(k, "plant", "tree", "grass", "bush", "berry")) { famHue = 0.28f; satScale = 1.10f; valBias = 0.00f; }
            else if (Has(k, "sand")) { famHue = 0.11f; satScale = 0.70f; valBias = 0.10f; }
            else if (Has(k, "soil", "mud", "dirt")) { famHue = 0.07f; satScale = 0.75f; valBias = -0.05f; }
            else if (Has(k, "wood", "log")) { famHue = 0.08f; satScale = 0.90f; valBias = 0.00f; }
            else
            {
                // No family the name admits to. The hash alone, which is the honest answer.
                return Color.HSVToRGB(hue, sat, val);
            }

            // Keep a slice of the hash inside the family, so two soils still read apart.
            float shifted = Repeat01(famHue + ((h % 1000u) / 1000f - 0.5f) * 0.05f);
            return Color.HSVToRGB(shifted, Clamp01(sat * satScale), Clamp01(val + valBias));
        }

        /// <summary>
        /// FNV-1a. Chosen over <c>string.GetHashCode</c> for one reason: the runtime is free to randomise
        /// that per process, and this colour has to survive a save, a reload and a different machine.
        /// </summary>
        public static uint Hash(string s)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < s.Length; i++)
                {
                    h ^= s[i];
                    h *= 16777619u;
                }
                return h;
            }
        }

        private static bool Has(string lowered, params string[] stems)
        {
            for (int i = 0; i < stems.Length; i++)
            {
                if (lowered.Contains(stems[i])) return true;
            }
            return false;
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }

        private static float Repeat01(float v)
        {
            v -= Mathf.Floor(v);
            return v < 0f ? v + 1f : v;
        }
    }
}
