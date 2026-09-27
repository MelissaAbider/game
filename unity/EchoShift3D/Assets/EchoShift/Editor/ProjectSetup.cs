using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EchoShift.EditorTools
{
    /// <summary>
    /// One-click / batch-mode project configuration:
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod EchoShift.EditorTools.ProjectSetup.Run
    /// </summary>
    public static class ProjectSetup
    {
        const string Root = "Assets/EchoShift";
        const string ScenePath = Root + "/Scenes/Main.unity";

        [MenuItem("EchoShift/Setup Project")]
        public static void Run()
        {
            foreach (var folder in new[] { Root + "/Resources/EchoMaterials", Root + "/Scenes", Root + "/Settings" })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();

            ConfigurePipeline();
            ConfigurePlayer();
            CreateMaterialTemplates();
            CreateScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[EchoShift] Project setup complete.");
        }

        static void ConfigurePipeline()
        {
            var asset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (!asset)
            {
                var data = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(data, Root + "/Settings/EchoRenderer.asset");
                asset = UniversalRenderPipelineAsset.Create(data);
                AssetDatabase.CreateAsset(asset, Root + "/Settings/EchoURP.asset");
                GraphicsSettings.defaultRenderPipeline = asset;
            }
            foreach (var target in new[] { asset, GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset })
            {
                if (!target) continue;
                target.supportsHDR = true;
                target.msaaSampleCount = 4;
                target.shadowDistance = 70f;
                target.maxAdditionalLightsCount = 8;
                EditorUtility.SetDirty(target);
            }
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "EchoShift";
            PlayerSettings.productName = "EchoShift";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;

            // Mobile: landscape only, HTTP to the lab server on the local network, internet permission.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.echoshift.lab");

            // The game uses the classic Input Manager; keep the new Input System available too ("Both").
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings.Length > 0)
            {
                var so = new SerializedObject(settings[0]);
                var handler = so.FindProperty("activeInputHandler");
                if (handler != null && handler.intValue != 2)
                {
                    handler.intValue = 2;
                    so.ApplyModifiedProperties();
                }
            }
        }

        static void CreateMaterialTemplates()
        {
            Save("Lit", Mats.Solid(Color.white));
            Save("LitEmissive", Mats.Emissive(Color.white, 1f));
            Save("LitTransparent", Mats.Glass(Color.white, 0.3f));
            Save("UnlitTransparent", Mats.Hologram(Color.white, 1f, 0.5f, false));
            Save("UnlitAdditive", Mats.Hologram(Color.white, 1f, 0.5f, true));
            var particles = Mats.Particles(Color.white);
            particles.SetTexture("_BaseMap", null);
            Save("ParticlesAdditive", particles);
            var textShader = Shader.Find("EchoShift/WorldText");
            if (textShader) Save("WorldText", new Material(textShader));
        }

        static void Save(string name, Material material)
        {
            var path = $"{Root}/Resources/EchoMaterials/{name}.mat";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(material, path);
        }

        static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            cam.AddComponent<UniversalAdditionalCameraData>();
            new GameObject("EchoShift").AddComponent<GameDirector>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        [MenuItem("EchoShift/Build Android APK")]
        public static void BuildAndroid()
        {
            // Bake this PC's LAN address so the phone can reach the voice/AI server (run it with --host 0.0.0.0).
            string ip = "127.0.0.1";
            foreach (var address in System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()))
                if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(address)) { ip = address.ToString(); break; }
            File.WriteAllText(Root + "/Resources/backend_url.txt", $"http://{ip}:8000");
            AssetDatabase.Refresh();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Build/EchoShift.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            Debug.Log($"[EchoShift] Android build {report.summary.result} (server {ip}:8000)");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        [MenuItem("EchoShift/Build Windows Player")]
        public static void BuildWindows()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Build/EchoShift.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log($"[EchoShift] Build {report.summary.result}: {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
