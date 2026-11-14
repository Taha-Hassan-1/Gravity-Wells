using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using GravityWells.Core;
using GravityWells.Game;

namespace GravityWells.UI
{
    // Two lists on two cards: how the game sounds and feels, then the store and privacy rows. Toggles are
    // switches, a choice shows its current value, and a row that opens something has a chevron, so each row
    // says what tapping it will do.
    public sealed class SettingsScreen : UIScreen
    {
        private const float RowHeight = 124f;

        private readonly AppFlow _flow;
        private readonly UIStyle _style;
        private readonly UISwitch _soundSwitch;
        private readonly UISwitch _musicSwitch;
        private readonly TextMeshProUGUI _vibrationsValue;
        private readonly Button _removeAdsRow;
        private readonly TextMeshProUGUI _removeAdsLabel;
        private readonly TextMeshProUGUI _removeAdsValue;
        private readonly GameObject _privacyOptionsRow;
        private readonly GameObject _privacyDivider;
        private readonly RectTransform _storeCard;

        public SettingsScreen(AppFlow flow, UIRoot ui, UIStyle style)
        {
            _flow = flow;
            _style = style;
            BuildRoot(ui.CanvasTransform, "Settings", ui, style, opaque: true);
            AddHeader(Strings.Settings.Title, style, () => _flow.ShowMainMenu());

            float width = UITokens.ContentWidth;
            const float firstCardY = -340f;

            // ---- sound and feel ----
            RectTransform feel = AddCard("FeelCard", firstCardY, 3);
            AddRow(feel, 0, 3, "SoundRow", Strings.Settings.Sound, "sound", ToggleSound, out RectTransform soundSide, out _);
            _soundSwitch = new UISwitch(soundSide, style);
            AddRow(feel, 1, 3, "MusicRow", Strings.Settings.Music, "music", ToggleMusic, out RectTransform musicSide, out _);
            _musicSwitch = new UISwitch(musicSide, style);
            AddRow(feel, 2, 3, "VibrationsRow", Strings.Settings.Vibrations, "haptics", ToggleVibrations, out RectTransform vibrationsSide, out _);
            _vibrationsValue = UIFactory.CreateRowValue(vibrationsSide, string.Empty, style);

            // ---- store and privacy ----
            float storeY = firstCardY - 3 * RowHeight - UITokens.SpaceXl;
            _storeCard = AddCard("StoreCard", storeY, 4);
            _removeAdsRow = AddRow(
                _storeCard, 0, 4, "RemoveAdsRow", Strings.Settings.RemoveAds, "shop", () => _flow.PurchaseRemoveAds(),
                out RectTransform adsSide, out _removeAdsLabel);
            _removeAdsValue = UIFactory.CreateRowValue(adsSide, string.Empty, style);
            AddRow(_storeCard, 1, 4, "RestoreRow", Strings.Settings.RestorePurchase, "restart", () => _flow.RestorePurchases(), out _, out _);
            Button privacy = AddRow(
                _storeCard, 2, 4, "PrivacyRow", Strings.Settings.PrivacyPolicy, "lock", () => _flow.OpenPrivacyPolicy(),
                out RectTransform privacySide, out _);
            _privacyDivider = privacy.transform.Find("Divider").gameObject;
            UIFactory.CreateRowChevron(privacySide, style);

            // Only shown where the consent rules require the user to be able to reopen it.
            Button options = AddRow(
                _storeCard, 3, 4, "PrivacyOptionsRow", Strings.Settings.PrivacyOptions, "settings", () => _flow.ShowPrivacyOptions(),
                out RectTransform optionsSide, out _);
            UIFactory.CreateRowChevron(optionsSide, style);
            _privacyOptionsRow = options.gameObject;

            TextMeshProUGUI version = UIFactory.CreateText(
                Content, "Version", Strings.Settings.Version(Application.version), UITokens.TextMeta, TextAlignmentOptions.Center, style, dim: true);
            UIFactory.Place(version.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(width, 50f));
        }

        private RectTransform AddCard(string name, float y, int rows)
        {
            Image card = UIFactory.CreateSurface(Content, name, _style);
            UIFactory.Place(
                card.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y),
                new Vector2(UITokens.ContentWidth, rows * RowHeight));
            return card.rectTransform;
        }

        private Button AddRow(
            RectTransform card, int index, int rows, string name, string label, string icon, UnityAction onClick,
            out RectTransform accessory, out TextMeshProUGUI labelText)
        {
            var size = new Vector2(UITokens.ContentWidth, RowHeight);
            Button row = UIFactory.CreateRow(
                card, name, label, size, _style, onClick, UIIcons.Get(icon), divider: index < rows - 1,
                out accessory, out labelText);
            UIFactory.Place((RectTransform)row.transform, new Vector2(0.5f, 1f), new Vector2(0f, -index * RowHeight), size);
            return row;
        }

        protected override void OnShow() => Refresh();

        public void Refresh()
        {
            _soundSwitch.Set(_flow.Save.Data.SoundOn);
            _musicSwitch.Set(_flow.Save.Data.MusicOn);
            _vibrationsValue.text = Strings.Settings.VibrationsValue(_flow.Save.Data.HapticsOn, _flow.Save.Data.VibrationStrong);

            bool removed = _flow.AdsRemoved;
            _removeAdsRow.interactable = !removed;
            _removeAdsLabel.text = removed ? Strings.Settings.AdsRemoved : Strings.Settings.RemoveAds;
            _removeAdsValue.text = removed ? string.Empty : _flow.RemoveAdsPrice;

            // The optional last row leaves no empty band behind when it is hidden.
            bool options = _flow.PrivacyOptionsRequired;
            _privacyOptionsRow.SetActive(options);
            _privacyDivider.SetActive(options);
            _storeCard.sizeDelta = new Vector2(UITokens.ContentWidth, (options ? 4 : 3) * RowHeight);
        }

        private void ToggleSound()
        {
            _flow.SetSoundOn(!_flow.Save.Data.SoundOn);
            Refresh();
        }

        private void ToggleMusic()
        {
            _flow.SetMusicOn(!_flow.Save.Data.MusicOn);
            Refresh();
        }

        private void ToggleVibrations()
        {
            _flow.CycleVibrations();
            Refresh();
        }

        public override void OnBack() => _flow.ShowMainMenu();
    }
}
