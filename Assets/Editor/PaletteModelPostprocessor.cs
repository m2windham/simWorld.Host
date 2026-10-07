using UnityEditor;
using UnityEngine;

namespace SimWorldHost.EditorTools
{
    /// <summary>
    /// Every model under Resources/Models draws with the one shared palette material, and its
    /// UCX_ collision children are never drawn. See simWorld.Model/orders/MANIFEST.md, "Style:
    /// two registers": colour lives in each face's UVs into Assets/Art/Palette/Palette.png, so
    /// the FBX's own materials are irrelevant and are not imported at all.
    /// </summary>
    public sealed class PaletteModelPostprocessor : AssetPostprocessor
    {
        private const string ModelsRoot = "Assets/Resources/Models/";
        private const string PalettePng = "Assets/Art/Palette/Palette.png";
        private const string PaletteMaterial = "Assets/Art/Palette/M_Palette.mat";

        // The four standard collision-mesh prefixes: convex hull, box, sphere, capsule. The
        // pipeline's convex generator emits UCX_, its box generator (walls, doors) emits UBX_.
        private static readonly string[] CollisionPrefixes = { "UCX_", "UBX_", "USP_", "UCP_" };

        private static bool IsCollisionHull(string name)
        {
            foreach (string prefix in CollisionPrefixes)
                if (name.StartsWith(prefix)) return true;
            return false;
        }

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelsRoot)) return;
            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        private void OnPreprocessTexture()
        {
            if (assetPath != PalettePng) return;
            // A swatch atlas: nearest sampling, no mipmaps (they would bleed neighbouring swatches
            // together at ViewSize 60), no compression, sRGB.
            var importer = (TextureImporter)assetImporter;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(ModelsRoot)) return;
            // Declared so the import result is reproducible (and reimports when the material changes);
            // without it the importer reports "generated inconsistent result" for every model.
            context.DependsOnSourceAsset(PaletteMaterial);
            Material palette = AssetDatabase.LoadAssetAtPath<Material>(PaletteMaterial);

            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (IsCollisionHull(renderer.gameObject.name))
                {
                    // Collision hull: keep the MeshFilter for a collider, never render it.
                    Object.DestroyImmediate(renderer);
                    continue;
                }

                if (palette == null) continue;
                var materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (int i = 0; i < materials.Length; i++) materials[i] = palette;
                renderer.sharedMaterials = materials;
            }
        }
    }
}
