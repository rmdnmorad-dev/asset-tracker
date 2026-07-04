#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace VampFrost
{
    public static class VampFrostEditorTools
    {
        [MenuItem("VampFrost/Fix Sprite Import Settings")]
        static void FixSprites()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Sprites" });
            int n = 0;
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Sprite;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.spritePixelsPerUnit = path.Contains("Player") ? 128 : 32;
                imp.SaveAndReimport();
                n++;
            }
            Debug.Log($"[VampFrost] Fixed import settings on {n} textures " +
                      "(Point filter, uncompressed, PPU 32 / Player 128).");
        }

        [MenuItem("VampFrost/Open Save Folder")]
        static void OpenSave() => EditorUtility.RevealInFinder(Application.persistentDataPath);

        [MenuItem("VampFrost/Delete Save File")]
        static void DeleteSave()
        {
            string p = Application.persistentDataPath + "/vampfrost_save.json";
            if (System.IO.File.Exists(p)) { System.IO.File.Delete(p); Debug.Log("[VampFrost] Save deleted."); }
            else Debug.Log("[VampFrost] No save file found.");
        }
    }
}
#endif
