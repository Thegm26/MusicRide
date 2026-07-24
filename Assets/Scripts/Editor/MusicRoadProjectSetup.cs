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
            if (!File.Exists(MainScenePath))
            {
                CreateSceneAndConfigure();
            }
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
    }
}
