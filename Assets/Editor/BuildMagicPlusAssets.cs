using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Copy this file into Assets/Editor in the Unity project containing your model.
// Select the finished .prefab in Project, then use the MagicPlus menu.
public static class BuildMagicPlusAssets
{
    // Match the currently tested Valheim player. Update after checking its log.
    private const string RequiredUnityVersion = "6000.0.75f1";

    [MenuItem("MagicPlus/Build selected arrow for Windows")]
    public static void BuildSelectedArrow()
    {
        if (Application.unityVersion != RequiredUnityVersion)
        {
            EditorUtility.DisplayDialog("MagicPlus",
                "This Valheim build requires Unity " + RequiredUnityVersion +
                ". Current editor: " + Application.unityVersion +
                ". Install the matching editor in Unity Hub and build from a compatible project.", "OK");
            return;
        }
        string prefabPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(prefabPath) || !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("MagicPlus", "Select the arrow .prefab in the Project window first.", "OK");
            return;
        }
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
        {
            EditorUtility.DisplayDialog("MagicPlus", "Install Windows Build Support for this editor through Unity Hub, then try again.", "OK");
            return;
        }

        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "MagicPlus", "Windows"));
        Directory.CreateDirectory(output);
        AssetDatabase.SaveAssets();
        var bundle = new AssetBundleBuild
        {
            assetBundleName = "magicplus_visuals",
            assetNames = new[] { prefabPath },
            addressableNames = new[] { "FireArrowVisual" }
        };
        var manifest = BuildPipeline.BuildAssetBundles(output, new[] { bundle },
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode,
            BuildTarget.StandaloneWindows64);
        string result = Path.Combine(output, bundle.assetBundleName);
        if (manifest == null || !File.Exists(result))
            throw new InvalidOperationException("MagicPlus AssetBundle build failed. Check the Unity Console.");

        File.WriteAllText(Path.Combine(output, "build-info.txt"),
            "UnityVersion: " + Application.unityVersion + "\nTarget: StandaloneWindows64\nPrefab: " + prefabPath +
            "\nPrefabAddress: FireArrowVisual\n");
        Debug.Log("MagicPlus bundle ready: " + result + " (prefab address: FireArrowVisual)");
        EditorUtility.RevealInFinder(result);
    }
}
