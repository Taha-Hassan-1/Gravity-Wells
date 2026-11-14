using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace GravityWells.Editor
{
    // Turns the two bundled TTFs into TextMeshPro font assets under Resources/UI/Fonts, which
    // UIFonts loads at runtime. Re-run it (GravityWells > Build > Create UI Fonts) after replacing a
    // font file. The assets are committed, so a normal build never needs this.
    public static class UIFontBuilder
    {
        private const string Folder = "Assets/_Project/Resources/UI/Fonts/";
        private const string LiberationFallback = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        // ASCII plus the punctuation the UI uses; everything else falls back to Liberation Sans.
        private const string Characters =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~ –—…·";

        [MenuItem("GravityWells/Build/Create UI Fonts")]
        public static void CreateAll()
        {
            Create("VarelaRound-Regular");
            Create("LilitaOne-Regular");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Fonts] UI font assets created");
        }

        private static void Create(string name)
        {
            string assetPath = Folder + name + " SDF.asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            var font = AssetDatabase.LoadAssetAtPath<Font>(Folder + name + ".ttf");
            if (font == null)
            {
                Debug.LogError("[Fonts] missing " + Folder + name + ".ttf");
                return;
            }

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = name + " SDF";
            AssetDatabase.CreateAsset(asset, assetPath);

            // Pre-bake the common characters so no glyph is rasterized for the first time mid-game.
            // This also creates the atlas texture, which is why the sub-assets are added afterwards.
            asset.TryAddCharacters(Characters, out string missing);
            if (!string.IsNullOrEmpty(missing))
            {
                Debug.LogWarning("[Fonts] " + name + " has no glyph for: " + missing);
            }

            if (asset.material != null)
            {
                asset.material.name = name + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }
            if (asset.atlasTextures != null)
            {
                for (int i = 0; i < asset.atlasTextures.Length; i++)
                {
                    asset.atlasTextures[i].name = name + " Atlas " + i;
                    AssetDatabase.AddObjectToAsset(asset.atlasTextures[i], asset);
                }
            }

            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationFallback);
            if (fallback != null)
            {
                asset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { fallback };
            }

            EditorUtility.SetDirty(asset);
        }
    }
}
