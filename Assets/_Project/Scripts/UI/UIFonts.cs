using TMPro;
using UnityEngine;

namespace GravityWells.UI
{
    // The game's two typefaces, loaded once from Resources/UI/Fonts (created by
    // Gravity Wells > Build > Create UI Fonts). Body is a soft rounded face for reading; Display is a
    // chunky one for titles, numbers and primary buttons. If an asset is missing the text falls
    // back to TextMeshPro's default font, so a bad import never leaves blank labels.
    public static class UIFonts
    {
        private static TMP_FontAsset _body;
        private static TMP_FontAsset _display;

        public static TMP_FontAsset Body => _body != null ? _body : _body = Load("VarelaRound-Regular SDF");

        public static TMP_FontAsset Display => _display != null ? _display : _display = Load("LilitaOne-Regular SDF");

        private static TMP_FontAsset Load(string name) => Resources.Load<TMP_FontAsset>("UI/Fonts/" + name);
    }
}
