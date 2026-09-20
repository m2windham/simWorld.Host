using NUnit.Framework;
using SimWorldHost;
using UnityEditor;
using UnityEngine;

namespace SimWorldHost.Tests
{
    /// <summary>
    /// Verifies the Lane 1 lighting, post-processing, and diurnal environment pipeline.
    /// Pins the canonical Timberborn-style look: golden key light (pitch 35°, yaw 45°),
    /// SSAO contact shadows, soft shadow distance >= 700, and global volume profile overrides.
    /// </summary>
    public class LightingAndPostProcessingTests
    {
        [Test]
        public void URP_Asset_Has_Sufficient_Shadow_Distance_And_Soft_Shadows()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Settings/UniversalRP.asset");
            Assert.IsNotNull(asset, "UniversalRP asset must exist at Assets/Settings/UniversalRP.asset.");

            var so = new SerializedObject(asset);
            float shadowDistance = so.FindProperty("m_ShadowDistance").floatValue;
            bool softShadows = so.FindProperty("m_SoftShadowsSupported").boolValue;

            Assert.GreaterOrEqual(shadowDistance, 700f,
                "Shadow distance must be >= 700 to cover isometric orthographic framing.");
            Assert.IsTrue(softShadows, "Soft shadows must be enabled on URP asset.");
        }

        [Test]
        public void Universal_Renderer_Has_Active_SSAO_Feature()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Settings/UniversalRenderer.asset");
            Assert.IsNotNull(asset, "UniversalRendererData must exist at Assets/Settings/UniversalRenderer.asset.");

            var so = new SerializedObject(asset);
            var features = so.FindProperty("m_RendererFeatures");
            Assert.Greater(features.arraySize, 0, "UniversalRendererData must have at least one renderer feature.");

            bool foundSSAO = false;
            for (int i = 0; i < features.arraySize; i++)
            {
                var featureRef = features.GetArrayElementAtIndex(i).objectReferenceValue;
                if (featureRef != null && featureRef.GetType().Name == "ScreenSpaceAmbientOcclusion")
                {
                    foundSSAO = true;
                    var featureSo = new SerializedObject(featureRef);
                    bool active = featureSo.FindProperty("m_Active").boolValue;
                    float radius = featureSo.FindProperty("m_Settings.Radius").floatValue;
                    float intensity = featureSo.FindProperty("m_Settings.Intensity").floatValue;
                    float directLight = featureSo.FindProperty("m_Settings.DirectLightingStrength").floatValue;

                    Assert.IsTrue(active, "SSAO feature must be active.");
                    Assert.AreEqual(0.35f, radius, 0.01f, "SSAO radius must match reference board specification (~0.35).");
                    Assert.AreEqual(1.2f, intensity, 0.05f, "SSAO intensity must match reference board specification (~1.2).");
                    Assert.AreEqual(0.25f, directLight, 0.05f, "SSAO direct lighting strength must be ~0.25.");
                    break;
                }
            }

            Assert.IsTrue(foundSSAO, "ScreenSpaceAmbientOcclusion feature must be found on UniversalRendererData.");
        }

        [Test]
        public void Global_Volume_Profile_Contains_Required_Overrides()
        {
            var profile = AssetDatabase.LoadMainAssetAtPath("Assets/Settings/GlobalVolumeProfile.asset");
            Assert.IsNotNull(profile, "GlobalVolumeProfile asset must exist at Assets/Settings/GlobalVolumeProfile.asset.");

            var profileSo = new SerializedObject(profile);
            var components = profileSo.FindProperty("components");
            Assert.GreaterOrEqual(components.arraySize, 4, "GlobalVolumeProfile must have at least 4 overrides.");

            bool foundTonemapping = false;
            bool foundColorAdjustments = false;
            bool foundWhiteBalance = false;
            bool foundVignette = false;

            for (int i = 0; i < components.arraySize; i++)
            {
                var comp = components.GetArrayElementAtIndex(i).objectReferenceValue;
                if (comp == null) continue;

                var compSo = new SerializedObject(comp);
                string typeName = comp.GetType().Name;

                if (typeName == "Tonemapping")
                {
                    foundTonemapping = true;
                    int mode = compSo.FindProperty("mode.m_Value").intValue;
                    // Neutral mode = 1 in TonemappingMode enum
                    Assert.AreEqual(1, mode, "Tonemapping mode must be Neutral (1).");
                }
                else if (typeName == "ColorAdjustments")
                {
                    foundColorAdjustments = true;
                    float postExposure = compSo.FindProperty("postExposure.m_Value").floatValue;
                    float contrast = compSo.FindProperty("contrast.m_Value").floatValue;
                    float saturation = compSo.FindProperty("saturation.m_Value").floatValue;

                    Assert.AreEqual(0.15f, postExposure, 0.01f, "Post exposure must be +0.15.");
                    Assert.AreEqual(14.0f, contrast, 0.5f, "Contrast must be +14.0.");
                    Assert.AreEqual(10.0f, saturation, 0.5f, "Saturation must be +10.0.");
                }
                else if (typeName == "WhiteBalance")
                {
                    foundWhiteBalance = true;
                    float temp = compSo.FindProperty("temperature.m_Value").floatValue;
                    Assert.AreEqual(10.0f, temp, 0.5f, "White balance temperature must be +10.0.");
                }
                else if (typeName == "Vignette")
                {
                    foundVignette = true;
                    float intensity = compSo.FindProperty("intensity.m_Value").floatValue;
                    float smoothness = compSo.FindProperty("smoothness.m_Value").floatValue;

                    Assert.AreEqual(0.22f, intensity, 0.02f, "Vignette intensity must be 0.22.");
                    Assert.AreEqual(0.35f, smoothness, 0.02f, "Vignette smoothness must be 0.35.");
                }
            }

            Assert.IsTrue(foundTonemapping, "Tonemapping override must be present in profile.");
            Assert.IsTrue(foundColorAdjustments, "ColorAdjustments override must be present in profile.");
            Assert.IsTrue(foundWhiteBalance, "WhiteBalance override must be present in profile.");
            Assert.IsTrue(foundVignette, "Vignette override must be present in profile.");
        }

        [Test]
        public void TimeOfDay_Calculates_Canonical_Baseline_At_Hour_14()
        {
            var go = new GameObject("TestLight");
            var light = go.AddComponent<Light>();
            var tod = go.AddComponent<TimeOfDay>();

            tod.SetTime(14f);

            Assert.AreEqual(14f, tod.Hour, 0.001f);
            Assert.AreEqual(35f, go.transform.eulerAngles.x, 0.5f, "Sun pitch should be ~35° at canonical hour 14.");
            Assert.AreEqual(45f, go.transform.eulerAngles.y, 0.5f, "Sun yaw should be ~45° at canonical hour 14.");
            Assert.AreEqual(1.25f, light.intensity, 0.05f, "Sun intensity should be 1.25 at canonical hour 14.");
            Assert.AreEqual(LightShadows.Soft, light.shadows, "Sun shadows should be Soft.");

            Object.DestroyImmediate(go);
        }
    }
}
