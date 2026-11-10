using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace AR.Editor
{
    /// <summary>
    /// Keeps Addressables addresses equal to the paths used in story/stage JSON:
    ///   Assets/Sprites/Characters/aria_smile.png → "Characters/aria_smile"
    ///   Assets/Audio/SFX/crash.wav               → "SFX/crash"
    ///   Assets/VFX/impacts/bullet-impact.png     → "impacts/bullet-impact" (+ ".json" for the sheet data)
    /// Runs automatically whenever files under those folders are imported, so tools that drop files there
    /// (fetch_art.py, gen_sfx.py, pixelfx_export.sh) need no manual step.
    /// </summary>
    public sealed class AddressablesSync : AssetPostprocessor
    {
        private static readonly string[] Roots = { "Assets/Sprites", "Assets/Audio", "Assets/VFX" };
        private static bool _scheduled;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (_scheduled) return;
            foreach (var p in imported)
            {
                if (!IsTracked(p)) continue;
                _scheduled = true;
                EditorApplication.delayCall += () =>
                {
                    _scheduled = false;
                    Sync();
                };
                return;
            }
        }

        private static bool IsTracked(string path)
        {
            foreach (var r in Roots)
                if (path.StartsWith(r + "/")) return AddressFor(path, r) != null;
            return false;
        }

        private static string AddressFor(string path, string root)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            string rel = path.Substring(root.Length + 1);
            switch (ext)
            {
                case ".png":
                case ".jpg":
                case ".wav":
                case ".ogg":
                case ".mp3":
                    return rel.Substring(0, rel.Length - ext.Length);
                case ".json":
                    return rel; // keep the extension so it never collides with the PNG of the same name
                default:
                    return null;
            }
        }

        [MenuItem("AR/Sync Addressables")]
        public static void Sync()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null) return;
            var group = settings.DefaultGroup;
            int count = 0;
            foreach (var root in Roots)
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                foreach (var guid in AssetDatabase.FindAssets("", new[] { root }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetDatabase.IsValidFolder(path)) continue;
                    string address = AddressFor(path, root);
                    if (address == null) continue;
                    var entry = settings.FindAssetEntry(guid);
                    if (entry != null && entry.address == address) continue;
                    entry = settings.CreateOrMoveEntry(guid, group, false, false);
                    entry.address = address;
                    count++;
                }
            }
            if (count == 0) return;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log($"[AR] Addressables: registered/updated {count} entries");
        }
    }
}
