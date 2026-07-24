using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MusicRoad.Editor
{
    public static class MusicRoadProjectSetup
    {
        private const string MainScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Music Road/Create or Refresh Main Scene")]
        public static void CreateSceneAndConfigure()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MusicRoadBootstrap bootstrap = new GameObject("Music Road Bootstrap").AddComponent<MusicRoadBootstrap>();
            Shader runtimeShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/MusicRoadReactive.shader");
            if (runtimeShader == null)
            {
                throw new BuildFailedException("Assets/Shaders/MusicRoadReactive.shader could not be loaded.");
            }
            bootstrap.SetRuntimeShader(runtimeShader);
            GameObject[] vehiclePrefabs =
            {
                LoadPrefab("Assets/Awb-Free Low Poly Vehicles/Prefabs/Sport Car_39.prefab"),
                LoadPrefab("Assets/Awb-Free Low Poly Vehicles/Prefabs/N_Muscle Car_10.prefab"),
                LoadPrefab("Assets/Awb-Free Low Poly Vehicles/Prefabs/Hatchback Car_15.prefab"),
                LoadPrefab("Assets/Awb-Free Low Poly Vehicles/Prefabs/Classic Car_9.prefab"),
                LoadPrefab("Assets/Awb-Free Low Poly Vehicles/Prefabs/Pick Up_11.prefab"),
                LoadPrefab("Assets/Awb-Free Low Poly Vehicles/Prefabs/N Van_10.prefab"),
                LoadPrefab("Assets/Awb-Free Low Poly Vehicles/Prefabs/Monster Truck_12.prefab")
            };
            GameObject[] environmentPrefabs =
            {
                LoadPrefab("Assets/Supercyan Free Forest Sample/Prefabs/Mobile/Tree/Fir/Mobile_forestpack_tree_fir_tall.prefab"),
                LoadPrefab("Assets/Supercyan Free Forest Sample/Prefabs/Mobile/Tree/Leaf/Normal/Mobile_forestpack_tree_1_leaf_1.prefab"),
                LoadPrefab("Assets/Supercyan Free Forest Sample/Prefabs/Mobile/Tree/Treestump/Mobile_forestpack_tree_stump_1.prefab"),
                LoadPrefab("Assets/Supercyan Free Forest Sample/Prefabs/Mobile/Stone/Mobile_forestpack_stone_medium_1.prefab"),
                LoadPrefab("Assets/Supercyan Free Forest Sample/Prefabs/Mobile/Stone/Mobile_forestpack_stone_large_1.prefab"),
                LoadPrefab("Assets/Supercyan Free Forest Sample/Prefabs/Mobile/Foliage/Grass/Mobile_forestpack_foliage_grassPatch_small_1.prefab"),
                LoadPrefab("Assets/Supercyan Free Forest Sample/Prefabs/Mobile/Foliage/Grass/Mobile_forestpack_foliage_grassPatch_small_2.prefab"),
                LoadPrefab("Assets/Supercyan Free Forest Sample/Prefabs/Mobile/Foliage/Mushroom/Mobile_forestpack_foliage_mushroom_blue_big.prefab"),
                LoadPrefab("Assets/Supercyan Free Forest Sample/Prefabs/Mobile/Foliage/Mushroom/Mobile_forestpack_foliage_mushroom_red_small.prefab"),
                LoadPrefab("Assets/Supercyan Free Forest Sample/Prefabs/Mobile/Sign/Mobile_forestpack_roadSign_westEast_1.prefab")
            };
            bootstrap.SetImportedAssets(vehiclePrefabs, environmentPrefabs);
            EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.SaveScene(scene, MainScenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainScenePath, true)
            };

            PlayerSettings.companyName = "Music Road";
            PlayerSettings.productName = "Music Road";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.memorySize = 512;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.WebGL, "com.musicroad.prototype");

            EditorUtility.SetDirty(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            AssetDatabase.SaveAssets();
            Debug.Log($"Music Road scene created at {MainScenePath}");
        }

        [MenuItem("Music Road/Build WebGL")]
        public static void BuildWebGL()
        {
            CreateSceneAndConfigure();
            Directory.CreateDirectory("Build");

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { MainScenePath },
                locationPathName = "Build",
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"WebGL build failed with {report.summary.totalErrors} errors.");
            }

            Debug.Log($"WebGL build complete: {Path.GetFullPath(options.locationPathName)}");
        }

        private static GameObject LoadPrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                throw new BuildFailedException($"Required imported prefab could not be loaded: {path}");
            }

            return prefab;
        }
    }
}
