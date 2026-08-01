using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Helpers.Extensions;
using YARG.Menu.MusicLibrary;

namespace YARG
{
    public class TourSongView : SongView
    {
        public Image lockedIcon;
        public Button favoriteButton;
        public TextMeshProUGUI songOrCategoryName;
        public TextMeshProUGUI _instrumentsText;
        public override void Show(bool selected, ViewType viewType)
        {
            base.Show(selected, viewType);
            bool isLocked = TourMenu.IsLocked(viewType);
            favoriteButton.gameObject.SetActive(!isLocked);
            lockedIcon.gameObject.SetActive(isLocked);
            if (viewType is CategoryViewType)
            {
                songOrCategoryName.overflowMode = TextOverflowModes.Overflow;
            }
            else
            {
                songOrCategoryName.overflowMode = TextOverflowModes.Ellipsis;
            }

            if (viewType is TourSongViewType tourSongViewType)
            {
                var instrument = tourSongViewType._PlayerPercentRecord?.Instrument;
                var resourceName = instrument.HasValue ? instrument.Value.ToResourceName() : null;
                _instrumentsText.text = resourceName != null
                    ? $"<sprite name=\"{resourceName}\">"
                    : string.Empty;
            }
            else
            {
                _instrumentsText.text = string.Empty;
            }
        }

        override public void PrimaryTextClick()
        {
            if (TourMenu.IsLocked(ViewType))
            {
                return;
            }

            base.PrimaryTextClick();
        }

        override public void SecondaryTextClick()
        {
            if (TourMenu.IsLocked(ViewType))
            {
                return;
            }

            base.SecondaryTextClick();
        }
    }
}