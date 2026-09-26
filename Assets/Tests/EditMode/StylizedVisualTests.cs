using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SimWorldHost;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SimWorldHost.Tests
{
    /// <summary>
    /// Milestone 1: Test Infrastructure & Invariants Suite.
    /// Comprehensive EditMode test fixture covering:
    /// - Tier 1: Feature Coverage (Shader presence & property contracts, Timberborn palette values)
    /// - Tier 2: Boundary & Invariants (GPU instancing pragmas, SRP Batcher CBUFFER compatibility, URP depth texture)
    /// - Tier 3: Cross-Feature & Model Resolution (71 Resources/Models resolution, MapRenderer fallback cube)
    /// - Tier 4: Seam Invariants (Packages/manifest.json relative path, engine-free SimWorld.Core assembly)
    ///
    /// Follows Progressive Testability: tests gracefully check for shader/material assets and assert exact
    /// property and pragma contracts when present, providing clear dependency hints when deferred to M2-M4.
    /// </summary>
    public sealed class StylizedVisualTests
    {
        private const string StylizedLitShaderName = "SimWorld/StylizedLit";
        private const string StylizedTerrainShaderName = "SimWorld/StylizedTerrain";
        private const string StylizedWaterShaderName = "SimWorld/StylizedWater";

        private const string StylizedLitShaderPath = "Assets/Shaders/StylizedLit.shader";
        private const string StylizedTerrainShaderPath = "Assets/Shaders/StylizedTerrain.shader";
        private const string StylizedWaterShaderPath = "Assets/Shaders/StylizedWater.shader";

        [SetUp]
        public void SetUp()
        {
            VisualRegistry.Reload();
        }

        // =====================================================================
        // TIER 1: FEATURE COVERAGE (SHADERS & PALETTE)
        // =====================================================================

        [Test]
        public void StylizedLit_Shader_Presence_And_Validity()
        {
            Shader shader = LoadShaderProgressively(StylizedLitShaderName, StylizedLitShaderPath, "Milestone 2 deliverable");
            Assert.IsNotNull(shader, $"Shader '{StylizedLitShaderName}' must be loaded.");
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), $"Shader '{StylizedLitShaderName}' has compilation errors.");
            Assert.IsTrue(shader.isSupported, $"Shader '{StylizedLitShaderName}' is not supported on the current graphics device.");
        }

        [Test]
        public void StylizedLit_Shader_Properties_Match_Contract()
        {
            Shader shader = LoadShaderProgressively(StylizedLitShaderName, StylizedLitShaderPath, "Milestone 2 deliverable");

            // Required contract properties from PROJECT.md & Lane 2 specification:
            // _BaseColor (Color), _BaseMap (Texture), _Smoothness (Float/Range), _Wrap (Float/Range),
            // _RampThreshold (Float/Range), _RampSmoothness (Float/Range), _ShadowTint (Color)
            AssertShaderHasProperty(shader, "_BaseColor", ShaderPropertyType.Color);
            AssertShaderHasProperty(shader, "_BaseMap", ShaderPropertyType.Texture);
            AssertShaderHasScalarProperty(shader, "_Smoothness");
            AssertShaderHasScalarProperty(shader, "_Wrap");
            AssertShaderHasScalarProperty(shader, "_RampThreshold");
            AssertShaderHasScalarProperty(shader, "_RampSmoothness");
            AssertShaderHasProperty(shader, "_ShadowTint", ShaderPropertyType.Color);
        }

        [Test]
        public void StylizedTerrain_Shader_Presence_And_Validity()
        {
            Shader shader = LoadShaderProgressively(StylizedTerrainShaderName, StylizedTerrainShaderPath, "Milestone 3 deliverable");
            Assert.IsNotNull(shader, $"Shader '{StylizedTerrainShaderName}' must be loaded.");
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), $"Shader '{StylizedTerrainShaderName}' has compilation errors.");
            Assert.IsTrue(shader.isSupported, $"Shader '{StylizedTerrainShaderName}' is not supported on the current graphics device.");
        }

        [Test]
        public void StylizedTerrain_Shader_Properties_Match_Contract()
        {
            Shader shader = LoadShaderProgressively(StylizedTerrainShaderName, StylizedTerrainShaderPath, "Milestone 3 deliverable");

            // Required contract properties from PROJECT.md & Lane 3 specification:
            // _MainTex (Texture), _MeadowColor (Color), _RidgeColor (Color), _CliffColor (Color),
            // _StepHeight (Float/Range), _CliffSlopeThreshold (Float/Range)
            AssertShaderHasProperty(shader, "_MainTex", ShaderPropertyType.Texture);
            AssertShaderHasProperty(shader, "_MeadowColor", ShaderPropertyType.Color);
            AssertShaderHasProperty(shader, "_RidgeColor", ShaderPropertyType.Color);
            AssertShaderHasProperty(shader, "_CliffColor", ShaderPropertyType.Color);
            AssertShaderHasScalarProperty(shader, "_StepHeight");
            AssertShaderHasScalarProperty(shader, "_CliffSlopeThreshold");
        }

        [Test]
        public void StylizedWater_Shader_Presence_And_Validity()
        {
            Shader shader = LoadShaderProgressively(StylizedWaterShaderName, StylizedWaterShaderPath, "Milestone 4 deliverable");
            Assert.IsNotNull(shader, $"Shader '{StylizedWaterShaderName}' must be loaded.");
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), $"Shader '{StylizedWaterShaderName}' has compilation errors.");
            Assert.IsTrue(shader.isSupported, $"Shader '{StylizedWaterShaderName}' is not supported on the current graphics device.");
        }

        [Test]
        public void StylizedWater_Shader_Properties_Match_Contract()
        {
            Shader shader = LoadShaderProgressively(StylizedWaterShaderName, StylizedWaterShaderPath, "Milestone 4 deliverable");

            // Required contract properties from PROJECT.md & Lane 3 specification:
            // _ShallowColor (Color), _DeepColor (Color), _DepthDistance (Float/Range),
            // _FoamColor (Color), _FoamDistance (Float/Range), _WaveSpeed (Vector), _WaveScale (Float/Range)
            AssertShaderHasProperty(shader, "_ShallowColor", ShaderPropertyType.Color);
            AssertShaderHasProperty(shader, "_DeepColor", ShaderPropertyType.Color);
            AssertShaderHasScalarProperty(shader, "_DepthDistance");
            AssertShaderHasProperty(shader, "_FoamColor", ShaderPropertyType.Color);
            AssertShaderHasScalarProperty(shader, "_FoamDistance");
            AssertShaderHasProperty(shader, "_WaveSpeed", ShaderPropertyType.Vector);
            AssertShaderHasScalarProperty(shader, "_WaveScale");
        }

        [Test]
        public void Palette_Colors_Match_Timberborn_ReferenceBoard_Specifications()
        {
            // Hex specifications from docs/reference-board.md §2:
            // Hero Timber: #C2844B  sRGB (0.76, 0.52, 0.29) -> Linear (0.536, 0.233, 0.069)
            // Aged Bark:   #7A4B24  sRGB (0.48, 0.29, 0.14) -> Linear (0.197, 0.070, 0.017)
            // Sandstone:   #6E685F  sRGB (0.43, 0.41, 0.37) -> Linear (0.156, 0.141, 0.114)
            // Thatch:      #B88B4A  sRGB (0.72, 0.55, 0.29) -> Linear (0.473, 0.264, 0.070)

            AssertHexColorMatches("#C2844B", new Color(0.76f, 0.52f, 0.29f, 1f), new Color(0.536f, 0.233f, 0.069f, 1f), "Hero Timber");
            AssertHexColorMatches("#7A4B24", new Color(0.48f, 0.29f, 0.14f, 1f), new Color(0.197f, 0.070f, 0.017f, 1f), "Aged Bark");
            AssertHexColorMatches("#6E685F", new Color(0.43f, 0.41f, 0.37f, 1f), new Color(0.156f, 0.141f, 0.114f, 1f), "Sandstone");
            AssertHexColorMatches("#B88B4A", new Color(0.72f, 0.55f, 0.29f, 1f), new Color(0.473f, 0.264f, 0.070f, 1f), "Roof Thatch");
        }

        [Test]
        public void Material_Palette_Assets_Match_Target_Colors_When_Present()
        {
            var paletteChecks = new[]
            {
                (path: "Assets/Materials/HeroTimber.mat", hex: "#C2844B", name: "Hero Timber"),
                (path: "Assets/Materials/AgedBark.mat", hex: "#7A4B24", name: "Aged Bark"),
                (path: "Assets/Materials/Sandstone.mat", hex: "#6E685F", name: "Sandstone"),
                (path: "Assets/Materials/Thatch.mat", hex: "#B88B4A", name: "Thatch")
            };

            int checkedMaterials = 0;
            foreach (var item in paletteChecks)
            {
                if (!File.Exists(item.path)) continue;

                Material mat = AssetDatabase.LoadAssetAtPath<Material>(item.path);
                Assert.IsNotNull(mat, $"Failed to load material asset at {item.path}");

                ColorUtility.TryParseHtmlString(item.hex, out Color expectedSrgb);
                Color expectedLinear = expectedSrgb.linear;

                if (mat.HasProperty("_BaseColor"))
                {
                    Color actual = mat.GetColor("_BaseColor");
                    // In Unity, serialized material color values can be either linear or sRGB depending on inspector mode.
                    // Validate that actual matches either expectedLinear or expectedSrgb within 0.03f tolerance.
                    bool matchesLinear = ColorsNear(actual, expectedLinear, 0.03f);
                    bool matchesSrgb = ColorsNear(actual, expectedSrgb, 0.03f);
                    Assert.IsTrue(matchesLinear || matchesSrgb,
                        $"Material {item.name} ({item.path}) _BaseColor {actual} does not match expected hex {item.hex} (linear: {expectedLinear}, sRGB: {expectedSrgb}).");
                    checkedMaterials++;
                }
            }

            if (checkedMaterials == 0)
            {
                Assert.Ignore("Palette material assets in Assets/Materials/*.mat are not yet authored (Milestone 2 dependency).");
            }
        }

        // =====================================================================
        // TIER 2: BOUNDARY & INVARIANTS (INSTANCING, SRP BATCHER, PIPELINE)
        // =====================================================================

        [Test]
        public void StylizedLit_Shader_Supports_GPU_Instancing()
        {
            if (!File.Exists(StylizedLitShaderPath))
            {
                Assert.Ignore($"Shader file '{StylizedLitShaderPath}' not yet authored (Milestone 2 dependency).");
            }

            string source = File.ReadAllText(StylizedLitShaderPath);
            Assert.IsTrue(source.Contains("#pragma multi_compile_instancing"),
                "StylizedLit.shader must declare '#pragma multi_compile_instancing' to support instanced object drawing.");
            Assert.IsTrue(source.Contains("UNITY_SETUP_INSTANCE_ID") || source.Contains("UNITY_INSTANCING_BUFFER_START"),
                "StylizedLit.shader must implement Unity GPU instancing setup / buffer definitions.");
        }

        [Test]
        public void StylizedLit_Shader_Is_SRP_Batcher_Compatible()
        {
            if (!File.Exists(StylizedLitShaderPath))
            {
                Assert.Ignore($"Shader file '{StylizedLitShaderPath}' not yet authored (Milestone 2 dependency).");
            }

            string source = File.ReadAllText(StylizedLitShaderPath);
            Assert.IsTrue(source.Contains("CBUFFER_START(UnityPerMaterial)"),
                "StylizedLit.shader must enclose uniform material properties in 'CBUFFER_START(UnityPerMaterial)' for SRP Batcher compatibility.");
            Assert.IsTrue(source.Contains("CBUFFER_END"),
                "StylizedLit.shader must close UnityPerMaterial block with 'CBUFFER_END'.");
        }

        [Test]
        public void StylizedTerrain_Shader_Is_SRP_Batcher_Compatible()
        {
            if (!File.Exists(StylizedTerrainShaderPath))
            {
                Assert.Ignore($"Shader file '{StylizedTerrainShaderPath}' not yet authored (Milestone 3 dependency).");
            }

            string source = File.ReadAllText(StylizedTerrainShaderPath);
            Assert.IsTrue(source.Contains("CBUFFER_START(UnityPerMaterial)"),
                "StylizedTerrain.shader must enclose uniform material properties in 'CBUFFER_START(UnityPerMaterial)' for SRP Batcher compatibility.");
            Assert.IsTrue(source.Contains("CBUFFER_END"),
                "StylizedTerrain.shader must close UnityPerMaterial block with 'CBUFFER_END'.");
        }

        [Test]
        public void StylizedWater_Shader_Is_SRP_Batcher_Compatible()
        {
            if (!File.Exists(StylizedWaterShaderPath))
            {
                Assert.Ignore($"Shader file '{StylizedWaterShaderPath}' not yet authored (Milestone 4 dependency).");
            }

            string source = File.ReadAllText(StylizedWaterShaderPath);
            Assert.IsTrue(source.Contains("CBUFFER_START(UnityPerMaterial)"),
                "StylizedWater.shader must enclose uniform material properties in 'CBUFFER_START(UnityPerMaterial)' for SRP Batcher compatibility.");
            Assert.IsTrue(source.Contains("CBUFFER_END"),
                "StylizedWater.shader must close UnityPerMaterial block with 'CBUFFER_END'.");
        }

        [Test]
        public void Pipeline_Depth_Texture_Prerequisite_Is_Active()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Settings/UniversalRP.asset");
            Assert.IsNotNull(asset, "UniversalRP asset must exist at Assets/Settings/UniversalRP.asset.");

            var so = new SerializedObject(asset);
            var depthProp = so.FindProperty("m_RequireDepthTexture");
            Assert.IsNotNull(depthProp, "m_RequireDepthTexture property not found on UniversalRP asset.");
            Assert.IsTrue(depthProp.boolValue,
                "UniversalRP asset must have m_RequireDepthTexture enabled so water depth tinting and shoreline foam work.");
        }

        // =====================================================================
        // TIER 3: CROSS-FEATURE & MODEL RESOLUTION
        // =====================================================================

        [Test]
        public void VisualRegistry_Resolves_All_71_Models_To_Valid_Meshes()
        {
            const string modelsDir = "Assets/Resources/Models";
            Assert.IsTrue(Directory.Exists(modelsDir), $"Models directory '{modelsDir}' must exist.");

            string[] fbxFiles = Directory.GetFiles(modelsDir, "*.fbx");
            Assert.AreEqual(71, fbxFiles.Length, "Assets/Resources/Models must contain exactly 71 FBX model files.");

            VisualRegistry.Reload();
            Assert.Greater(VisualRegistry.LoadedDefNameCount, 0, "VisualRegistry must have loaded defNames.");

            var invalidModels = new List<string>();

            foreach (string fbxPath in fbxFiles)
            {
                string filename = Path.GetFileNameWithoutExtension(fbxPath);
                GameObject prefab = Resources.Load<GameObject>("Models/" + filename);
                if (prefab == null)
                {
                    invalidModels.Add($"{filename}: Failed to load via Resources.Load");
                    continue;
                }

                MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>();
                SkinnedMeshRenderer smr = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
                Mesh mesh = mf != null ? mf.sharedMesh : (smr != null ? smr.sharedMesh : null);

                if (mesh == null)
                {
                    invalidModels.Add($"{filename}: No MeshFilter or SkinnedMeshRenderer found on prefab");
                }
                else if (mesh.vertexCount == 0)
                {
                    invalidModels.Add($"{filename}: Mesh has 0 vertices");
                }
            }

            Assert.IsEmpty(invalidModels,
                $"The following models failed mesh validation:\n  {string.Join("\n  ", invalidModels)}");
        }

        [Test]
        public void VisualRegistry_Base_DefNames_Resolve_Deterministically_For_All_Models()
        {
            string[] fbxFiles = Directory.GetFiles("Assets/Resources/Models", "*.fbx");
            Assert.AreEqual(71, fbxFiles.Length);

            VisualRegistry.Reload();
            var distinctDefs = new HashSet<string>();

            foreach (string fbxPath in fbxFiles)
            {
                string rawName = Path.GetFileNameWithoutExtension(fbxPath);
                string baseName = VisualRegistry.BaseNameOf(rawName);
                distinctDefs.Add(baseName);
            }

            Assert.Greater(distinctDefs.Count, 0);

            foreach (string defName in distinctDefs)
            {
                Assert.IsTrue(VisualRegistry.Has(defName), $"VisualRegistry.Has('{defName}') should be true.");
                Assert.Greater(VisualRegistry.VariantCount(defName), 0, $"VariantCount for '{defName}' must be > 0.");

                // Check resolution stability and non-null prefab
                GameObject resolved0 = VisualRegistry.Resolve(defName, 0);
                Assert.IsNotNull(resolved0, $"VisualRegistry.Resolve('{defName}', 0) returned null.");

                // Edge case / adversarial test: negative and extreme thing IDs must resolve safely
                Assert.DoesNotThrow(() => VisualRegistry.Resolve(defName, -1));
                Assert.DoesNotThrow(() => VisualRegistry.Resolve(defName, int.MinValue));
                Assert.DoesNotThrow(() => VisualRegistry.Resolve(defName, int.MaxValue));
            }
        }

        [Test]
        public void MapRenderer_Fallback_Cube_Safety()
        {
            Mesh fallback = VisualRegistry.FallbackMesh;
            Assert.IsNotNull(fallback, "VisualRegistry.FallbackMesh must never be null.");
            Assert.Greater(fallback.vertexCount, 0, "Fallback mesh must have vertices.");
            Assert.AreEqual(24, fallback.vertexCount, "Fallback primitive cube in Unity standard has 24 vertices.");

            // Bound size must be a unit cube (1, 1, 1)
            Vector3 size = fallback.bounds.size;
            Assert.AreEqual(1f, size.x, 0.001f, "Fallback mesh bound X must be 1.0.");
            Assert.AreEqual(1f, size.y, 0.001f, "Fallback mesh bound Y must be 1.0.");
            Assert.AreEqual(1f, size.z, 0.001f, "Fallback mesh bound Z must be 1.0.");

            // Unknown defName must cleanly resolve to null so MapRenderer triggers fallback mesh assignment
            Assert.IsNull(VisualRegistry.Resolve("NonExistent_Unknown_Def_For_Fallback_Test", 0),
                "Unknown defName must return null to allow MapRenderer fallback cube substitution.");

            // Idempotency: multiple calls must return identical cached mesh reference
            Mesh second = VisualRegistry.FallbackMesh;
            Assert.AreSame(fallback, second, "VisualRegistry.FallbackMesh must return cached shared instance.");
        }

        // =====================================================================
        // TIER 4: SEAM INVARIANTS (RELATIVE MANIFEST & ENGINE-FREE CORE)
        // =====================================================================

        [Test]
        public void Packages_Manifest_Preserves_Relative_Path_For_SimWorldCore()
        {
            string manifestPath = Path.Combine(Directory.GetCurrentDirectory(), "Packages", "manifest.json");
            Assert.IsTrue(File.Exists(manifestPath), $"Packages/manifest.json must exist at {manifestPath}.");

            string manifestJson = File.ReadAllText(manifestPath);

            // Invariant 3 from AGENTS.md §4 & §12:
            // "com.simworld.core": "file:../../simWorld/src/SimWorld.Core"
            // Unity's Package Manager writes an absolute path by default. An absolute path works on exactly one machine.
            Assert.IsTrue(manifestJson.Contains("\"com.simworld.core\": \"file:../../simWorld/src/SimWorld.Core\""),
                "Packages/manifest.json must reference com.simworld.core via exact relative path 'file:../../simWorld/src/SimWorld.Core'.");

            // Adversarial check: verify no drive letter or absolute user path
            bool hasDriveLetter = Regex.IsMatch(manifestJson, @"""com\.simworld\.core""\s*:\s*""(file:)?([A-Za-z]:|/Users/|/home/)");
            Assert.IsFalse(hasDriveLetter, "Packages/manifest.json com.simworld.core must not contain an absolute system path.");
        }

        [Test]
        public void SimWorldCore_Namespace_Has_Zero_UnityEngine_References()
        {
            // Core assembly must be engine-free by rule. No reference to UnityEngine or UnityEditor allowed.
            Assembly coreAssembly = typeof(SimWorld.Defs.Def).Assembly;
            Assert.IsNotNull(coreAssembly, "Failed to locate SimWorld.Core assembly.");

            // 1. Verify assembly references
            AssemblyName[] referencedAssemblies = coreAssembly.GetReferencedAssemblies();
            foreach (AssemblyName refName in referencedAssemblies)
            {
                Assert.IsFalse(refName.Name.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase),
                    $"SimWorld.Core references {refName.Name}, violating the engine-free core rule.");
                Assert.IsFalse(refName.Name.StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase),
                    $"SimWorld.Core references {refName.Name}, violating the engine-free core rule.");
            }

            // 2. Inspect all types in SimWorld.Core assembly
            var offenders = new List<string>();
            foreach (Type type in coreAssembly.GetTypes())
            {
                string ns = type.Namespace ?? string.Empty;
                if (ns.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase) ||
                    ns.StartsWith("UnityEditor", StringComparison.OrdinalIgnoreCase))
                {
                    offenders.Add($"Type {type.FullName} has Unity namespace {ns}");
                }

                if (type.BaseType != null && (type.BaseType.Namespace?.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase) == true))
                {
                    offenders.Add($"Type {type.FullName} inherits from Unity type {type.BaseType.FullName}");
                }

                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    if (field.FieldType.Namespace?.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        offenders.Add($"Field {type.FullName}.{field.Name} is typed as Unity {field.FieldType.FullName}");
                    }
                }

                foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    if (prop.PropertyType.Namespace?.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        offenders.Add($"Property {type.FullName}.{prop.Name} is typed as Unity {prop.PropertyType.FullName}");
                    }
                }
            }

            Assert.IsEmpty(offenders,
                $"SimWorld.Core contains {offenders.Count} UnityEngine references:\n  {string.Join("\n  ", offenders)}");
        }

        // =====================================================================
        // HELPER METHODS
        // =====================================================================

        private static Shader LoadShaderProgressively(string shaderName, string assetPath, string milestoneNote)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null && File.Exists(assetPath))
            {
                shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
            }

            if (shader == null)
            {
                Assert.Ignore($"Shader '{shaderName}' ({assetPath}) is not yet authored/imported ({milestoneNote}).");
            }
            return shader;
        }

        private static void AssertShaderHasProperty(Shader shader, string propName, ShaderPropertyType expectedType)
        {
            int idx = shader.FindPropertyIndex(propName);
            Assert.AreNotEqual(-1, idx, $"Shader '{shader.name}' is missing required property '{propName}'.");
            ShaderPropertyType actualType = shader.GetPropertyType(idx);
            Assert.AreEqual(expectedType, actualType,
                $"Property '{propName}' on shader '{shader.name}' has type {actualType}, expected {expectedType}.");
        }

        private static void AssertShaderHasScalarProperty(Shader shader, string propName)
        {
            int idx = shader.FindPropertyIndex(propName);
            Assert.AreNotEqual(-1, idx, $"Shader '{shader.name}' is missing required property '{propName}'.");
            ShaderPropertyType actualType = shader.GetPropertyType(idx);
            Assert.IsTrue(actualType == ShaderPropertyType.Float || actualType == ShaderPropertyType.Range,
                $"Property '{propName}' on shader '{shader.name}' has type {actualType}, expected Float or Range.");
        }

        private static void AssertHexColorMatches(string hex, Color expectedSrgb, Color expectedLinear, string swatchName)
        {
            bool parsed = ColorUtility.TryParseHtmlString(hex, out Color parsedSrgb);
            Assert.IsTrue(parsed, $"Failed to parse hex color '{hex}' for {swatchName}.");

            // Validate sRGB channels within tolerance
            Assert.AreEqual(expectedSrgb.r, parsedSrgb.r, 0.02f, $"{swatchName} ({hex}) sRGB Red mismatch");
            Assert.AreEqual(expectedSrgb.g, parsedSrgb.g, 0.02f, $"{swatchName} ({hex}) sRGB Green mismatch");
            Assert.AreEqual(expectedSrgb.b, parsedSrgb.b, 0.02f, $"{swatchName} ({hex}) sRGB Blue mismatch");

            // Validate Linear channels within tolerance
            Color parsedLinear = parsedSrgb.linear;
            Assert.AreEqual(expectedLinear.r, parsedLinear.r, 0.02f, $"{swatchName} ({hex}) Linear Red mismatch");
            Assert.AreEqual(expectedLinear.g, parsedLinear.g, 0.02f, $"{swatchName} ({hex}) Linear Green mismatch");
            Assert.AreEqual(expectedLinear.b, parsedLinear.b, 0.02f, $"{swatchName} ({hex}) Linear Blue mismatch");
        }

        private static bool ColorsNear(Color a, Color b, float tolerance)
        {
            return Mathf.Abs(a.r - b.r) <= tolerance &&
                   Mathf.Abs(a.g - b.g) <= tolerance &&
                   Mathf.Abs(a.b - b.b) <= tolerance;
        }
    }
}
