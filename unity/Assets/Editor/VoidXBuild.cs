using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VoidX
{
    public static class VoidXBuild
    {
        const string ScenePath = "Assets/Scenes/VoidX.unity";
        [MenuItem("VoidX/Prepare project")]
        public static void Prepare()
        {
            Directory.CreateDirectory("Assets/Settings"); Directory.CreateDirectory("Assets/Scenes");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/VoidXRenderer.asset");
            if (!renderer)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>(); renderer.name = "VoidX Mobile Renderer"; AssetDatabase.CreateAsset(renderer, "Assets/Settings/VoidXRenderer.asset");
                // Depth normals provide small-scale contact shading without a costly high-end renderer.
                var ao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>(); ao.name = "Contact shadows"; AssetDatabase.AddObjectToAsset(ao, renderer); renderer.rendererFeatures.Add(ao);
                var aoSettings = new SerializedObject(ao); var settings = aoSettings.FindProperty("m_Settings");
                if (settings != null) { settings.FindPropertyRelative("Downsample").boolValue = true; settings.FindPropertyRelative("Intensity").floatValue = .65f; settings.FindPropertyRelative("Radius").floatValue = .2f; aoSettings.ApplyModifiedPropertiesWithoutUndo(); }
                EditorUtility.SetDirty(renderer);
            }
            renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            if (!renderer.postProcessData) throw new Exception("URP post processing resources are missing"); EditorUtility.SetDirty(renderer);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/VoidXURP.asset");
            if (!pipeline)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer); pipeline.name = "VoidX URP"; pipeline.supportsHDR = true; pipeline.supportsCameraDepthTexture = true; pipeline.msaaSampleCount = 2;
                pipeline.shadowDistance = 45; pipeline.mainLightShadowmapResolution = 2048; pipeline.shadowCascadeCount = 2;
                var pipelineSettings = new SerializedObject(pipeline); pipelineSettings.FindProperty("m_MainLightShadowsSupported").boolValue = true; pipelineSettings.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false; pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(pipeline, "Assets/Settings/VoidXURP.asset");
            }
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline; QualitySettings.vSyncCount = 0;
            // Materials in Resources preserve just the variants used by runtime geometry.
            CreateMaterial("VoidXLit", "Universal Render Pipeline/Lit", false);
            CreateMaterial("VoidXGlow", "Universal Render Pipeline/Lit", true);
            CreateMaterial("VoidXParticles", "Universal Render Pipeline/Particles/Unlit", false);
            CreateMaterial("VoidXEmblem", "VoidX/Emblem", false);
            CreateGrade();
            var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]); var included = graphics.FindProperty("m_AlwaysIncludedShaders");
            for (int i = included.arraySize - 1; i >= 0; i--) if (included.GetArrayElementAtIndex(i).objectReferenceValue is Shader s && s.name == "GUI/Text Shader") { included.GetArrayElementAtIndex(i).objectReferenceValue = null; included.DeleteArrayElementAtIndex(i); }
            foreach (string name in new[] { "Skybox/Procedural" })
            {
                var shader = Shader.Find(name); if (!shader) throw new Exception("Missing shader: " + name);
                bool found = false; for (int i = 0; i < included.arraySize; i++) if (included.GetArrayElementAtIndex(i).objectReferenceValue == shader) found = true;
                if (!found) { int n = included.arraySize; included.InsertArrayElementAtIndex(n); included.GetArrayElementAtIndex(n).objectReferenceValue = shader; }
            }
            graphics.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.companyName = "VoidX"; PlayerSettings.productName = "VoidX"; PlayerSettings.bundleVersion = "0.4.1";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.voidx.game"); PlayerSettings.Android.bundleVersionCode = 5; PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26; PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft; PlayerSettings.allowedAutorotateToPortrait = false; PlayerSettings.allowedAutorotateToPortraitUpsideDown = false; PlayerSettings.allowedAutorotateToLandscapeLeft = true; PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.colorSpace = ColorSpace.Linear; PlayerSettings.runInBackground = false; PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false); PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetNormalMapEncoding(NamedBuildTarget.Android, NormalMapEncoding.XYZ);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP); PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7 | AndroidArchitecture.X86_64;
            var playerSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]); var input = playerSettings.FindProperty("activeInputHandler"); if (input != null) { input.intValue = 0; playerSettings.ApplyModifiedPropertiesWithoutUndo(); }
            var logoImporter = AssetImporter.GetAtPath("Assets/Resources/VX-logo.png") as TextureImporter; if (logoImporter) { logoImporter.textureType = TextureImporterType.Default; logoImporter.mipmapEnabled = false; logoImporter.maxTextureSize = 1024; logoImporter.SaveAndReimport(); }
            var logo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/VX-logo.png");
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] {logo}, IconKind.Any);
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (var icon in icons) icon.SetTextures(Enumerable.Repeat(logo, Mathf.Max(1, icon.minLayerCount)).ToArray());
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); new GameObject("VoidX").AddComponent<VoidXGame>(); EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }; AssetDatabase.SaveAssets();
            Debug.Log("VOIDX_PREPARED: Unity / URP / Android 8+ / ARM and BlueStacks x86_64");
        }
        static void CreateGrade()
        {
            const string path = "Assets/Resources/VoidXGrade.asset"; if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path)) return;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path);
            var colors = profile.Add<ColorAdjustments>(true); colors.saturation.Override(-100); colors.contrast.Override(16); colors.postExposure.Override(.25f);
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(.14f); bloom.threshold.Override(1.2f); bloom.scatter.Override(.6f);
            var vignette = profile.Add<Vignette>(true); vignette.intensity.Override(.22f); vignette.smoothness.Override(.5f);
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile); EditorUtility.SetDirty(profile);
        }
        static void CreateMaterial(string name, string shaderName, bool emission)
        {
            string path = "Assets/Resources/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path); bool exists = m;
            if (!m) m = new Material(Shader.Find(shaderName)) { name = name, enableInstancing = true };
            if (shaderName.EndsWith("/Lit"))
            {
                const string normalPath = "Assets/Resources/FlatNormal.png";
                if (!File.Exists(normalPath)) { var t = new Texture2D(2, 2); t.SetPixels(new[] {new Color(.5f,.5f,1,1),new Color(.5f,.5f,1,1),new Color(.5f,.5f,1,1),new Color(.5f,.5f,1,1)}); t.Apply(); File.WriteAllBytes(normalPath, t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t); AssetDatabase.ImportAsset(normalPath); var importer = (TextureImporter)AssetImporter.GetAtPath(normalPath); importer.textureType = TextureImporterType.NormalMap; importer.SaveAndReimport(); }
                m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath)); m.EnableKeyword("_NORMALMAP");
                if (emission) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.white * 5); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
            }
            else if (shaderName.Contains("Particles")) { m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 1); m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.One); m.SetFloat("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000; }
            if (exists) EditorUtility.SetDirty(m); else AssetDatabase.CreateAsset(m, path);
        }
        [MenuItem("VoidX/Build Android APK")]
        public static void Android()
        {
            Prepare();
            string toolchain = Environment.GetEnvironmentVariable("VOIDX_ANDROID_TOOLS");
            if (!string.IsNullOrEmpty(toolchain))
            {
                var assembly = System.Reflection.Assembly.Load("UnityEditor.Android.Extensions"); var external = assembly.GetType("UnityEditor.Android.AndroidExternalToolsSettings", true);
                foreach (var tool in new[] { ("jdkRootPath", "OpenJDK"), ("sdkRootPath", "SDK"), ("ndkRootPath", "NDK") }) external.GetProperty(tool.Item1).SetValue(null, Path.Combine(toolchain, tool.Item2));
            }
            string key = Environment.GetEnvironmentVariable("VOIDX_KEYSTORE");
            if (string.IsNullOrWhiteSpace(key) || !File.Exists(key)) throw new Exception("Set VOIDX_KEYSTORE to the existing signing key before building an update.");
            PlayerSettings.Android.useCustomKeystore = true; PlayerSettings.Android.keystoreName = key; PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("VOIDX_KEYSTORE_PASSWORD"); PlayerSettings.Android.keyaliasName = "voidx"; PlayerSettings.Android.keyaliasPass = PlayerSettings.Android.keystorePass;
            string destination = Environment.GetEnvironmentVariable("VOIDX_APK") ?? Path.GetFullPath("Builds/VoidX-0.4.1.apk"); Directory.CreateDirectory(Path.GetDirectoryName(destination));
            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = destination, target = BuildTarget.Android, options = BuildOptions.None });
                if (report.summary.result != BuildResult.Succeeded) throw new Exception("Android build failed: " + report.summary.result); Debug.Log("VOIDX_APK: " + destination);
            }
            finally { PlayerSettings.Android.useCustomKeystore = false; PlayerSettings.Android.keystoreName = ""; PlayerSettings.Android.keystorePass = ""; PlayerSettings.Android.keyaliasPass = ""; AssetDatabase.SaveAssets(); }
        }
        public static void Windows()
        {
            Prepare(); string destination = Environment.GetEnvironmentVariable("VOIDX_WINDOWS") ?? Path.GetFullPath("Builds/Windows/VoidX.exe"); Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = destination, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows build failed: " + report.summary.result);
        }
        public static void Validate()
        {
            Prepare();
            var covers = new System.Collections.Generic.List<VoidXWorld.Cover> { new() { centre = Vector2.zero, size = new Vector2(4, 4) } };
            if (VoidXGame.ClearLine(new Vector3(-5, 0, 0), new Vector3(5, 0, 0), covers)) throw new Exception("Bullets passed through cover");
            if (!VoidXGame.ClearLine(new Vector3(-5, 0, 4), new Vector3(5, 0, 4), covers)) throw new Exception("Unblocked sightline rejected");
            var at = VoidXGame.MoveCircle(new Vector3(-4, 0, 0), 8, 0, .42f, covers); if (at.x > -2.4f) throw new Exception("Actor tunnelled through cover");
            var nav = new VoidXGame.FlowMap(new Vector3(6, 0, 0), covers); at = new Vector3(-6, 0, 0);
            for (int i = 0; i < 350; i++) { Vector3 delta = (nav.Next(at) - at).normalized * .1f; at = VoidXGame.MoveCircle(at, delta.x, delta.z, .42f, covers); }
            if (Vector3.Distance(at, new Vector3(6, 0, 0)) > 2) throw new Exception("Opponent could not route around cover");
            Debug.Log("VOIDX_CHECKS_PASSED: cover collision, projectile occlusion, AI route, render pipeline, build setup");
        }
    }
}
