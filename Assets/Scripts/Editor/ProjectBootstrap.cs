using System.IO;
using AR.Core;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace AR.Editor
{
    /// <summary>
    /// One-click (and first-load automatic) project setup:
    ///   1. TMP Essential Resources   2. Korean TMP font asset (Resources/Fonts/MainFont)
    ///   3. Reimport art with TextureImportRules + register Addressables   4. Main.unity with GameFlow in Build Settings
    /// Every step is idempotent. Runs automatically once while Assets/Scenes/Main.unity does not exist.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectBootstrap
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string FontSource = "Assets/Fonts/NanumGothic-Regular.ttf";
        private const string FontAssetPath = "Assets/Resources/Fonts/MainFont.asset";

        static ProjectBootstrap()
        {
            if (Application.isBatchMode) return;
            if (!File.Exists(ScenePath)) EditorApplication.delayCall += Run;
        }

        [MenuItem("AR/Setup Project (font, addressables, Main scene)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;

            if (!EnsureTmpEssentials()) return; // import is async; Run() is called again when it finishes
            EnsureFontAsset();
            ReimportArt();
            AddressablesSync.Sync();
            EnsureMainScene();
            Debug.Log("[AR] Project setup complete. Open Assets/Scenes/Main.unity and press Play.");
        }

        /// <summary>Batchmode entry: Unity.exe -batchmode -quit -executeMethod AR.Editor.ProjectBootstrap.RunBatch</summary>
        public static void RunBatch()
        {
            EnsureFontAsset();
            ReimportArt();
            AddressablesSync.Sync();
            EnsureMainScene();
        }

        private static bool EnsureTmpEssentials()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null) return true;
            AssetDatabase.importPackageCompleted += OnTmpImported;
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            return false;
        }

        private static void OnTmpImported(string packageName)
        {
            AssetDatabase.importPackageCompleted -= OnTmpImported;
            EditorApplication.delayCall += Run;
        }

        private static void EnsureFontAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath) != null) return;
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontSource);
            if (font == null)
            {
                Debug.LogWarning($"[AR] {FontSource} not found; Korean text will not render.");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(FontAssetPath)!);
            var fa = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 2048, 2048,
                AtlasPopulationMode.Dynamic, true);
            fa.name = "MainFont";
            AssetDatabase.CreateAsset(fa, FontAssetPath);
            if (fa.material != null)
            {
                fa.material.name = "MainFont Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            }
            if (fa.atlasTextures != null && fa.atlasTextures.Length > 0 && fa.atlasTextures[0] != null)
            {
                fa.atlasTextures[0].name = "MainFont Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            }
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();

            // Make it the TMP default so any new text uses Korean-capable glyphs.
            var tmpSettings = Resources.Load<TMP_Settings>("TMP Settings");
            if (tmpSettings != null)
            {
                var so = new SerializedObject(tmpSettings);
                var prop = so.FindProperty("m_defaultFontAsset");
                if (prop != null)
                {
                    prop.objectReferenceValue = fa;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static void ReimportArt()
        {
            foreach (var folder in new[] { "Assets/VFX", "Assets/Sprites" })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            }
        }

        private static void EnsureMainScene()
        {
            if (!File.Exists(ScenePath))
            {
                // Unity refuses additive scenes next to an unsaved "Untitled" scene: replace it instead.
                var current = SceneManager.GetActiveScene();
                bool untitled = string.IsNullOrEmpty(current.path);
                if (untitled && current.isDirty && !Application.isBatchMode &&
                    !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                var mode = untitled ? NewSceneMode.Single : NewSceneMode.Additive;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
                var cam = camGo.GetComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 8;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
                camGo.transform.position = new Vector3(0, 0, -10);
                SceneManager.MoveGameObjectToScene(camGo, scene);
                SceneManager.MoveGameObjectToScene(new GameObject("GameFlow", typeof(GameFlow)), scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                if (mode == NewSceneMode.Additive) EditorSceneManager.CloseScene(scene, true);
            }

            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0 || scenes[0].path != ScenePath)
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            var active = SceneManager.GetActiveScene();
            if (!Application.isBatchMode && active.path != ScenePath && !active.isDirty)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }
}
