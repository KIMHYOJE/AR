using UnityEditor;
using UnityEngine;

namespace AR.Editor
{
    /// <summary>
    /// Import settings by folder so nobody has to touch the Inspector:
    ///   Assets/VFX, Sprites/{Enemies,Bullets,Player}: pixel art (Point, uncompressed, 16 PPU, big max size)
    ///   Sprites/{Characters,Backgrounds,CG}:          painted art (Bilinear, compressed, 100 PPU)
    /// </summary>
    public sealed class TextureImportRules : AssetPostprocessor
    {
        private static readonly string[] PixelFolders =
            { "Assets/VFX/", "Assets/Sprites/Enemies/", "Assets/Sprites/Bullets/", "Assets/Sprites/Player/" };
        private static readonly string[] PaintedFolders =
            { "Assets/Sprites/Characters/", "Assets/Sprites/Backgrounds/", "Assets/Sprites/CG/" };

        private void OnPreprocessTexture()
        {
            var ti = (TextureImporter)assetImporter;
            if (StartsWithAny(assetPath, PixelFolders))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.spritePixelsPerUnit = 16;
                ti.maxTextureSize = 8192; // pixelfx sheets can be several thousand px wide
            }
            else if (StartsWithAny(assetPath, PaintedFolders))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.filterMode = FilterMode.Bilinear;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.spritePixelsPerUnit = 100;
                ti.maxTextureSize = 2048;
                if (assetPath.StartsWith("Assets/Sprites/Characters/"))
                {
                    var settings = new TextureImporterSettings();
                    ti.ReadTextureSettings(settings);
                    settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                    ti.SetTextureSettings(settings);
                }
            }
        }

        private static bool StartsWithAny(string path, string[] prefixes)
        {
            foreach (var p in prefixes)
                if (path.StartsWith(p)) return true;
            return false;
        }
    }
}
