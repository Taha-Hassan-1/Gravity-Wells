using UnityEditor;
using UnityEngine;

namespace GravityWells.Editor
{
    // Icons under Resources/UI/Icons are white glyphs that the UI tints per theme, so they must
    // import as plain sprites with no mipmaps. Applied on import so a re-import never drifts.
    public sealed class UIIconImporter : AssetPostprocessor
    {
        private const string IconFolder = "Assets/_Project/Resources/UI/Icons/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(IconFolder))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 128;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
