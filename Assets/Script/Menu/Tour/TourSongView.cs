using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Menu.MusicLibrary;

namespace YARG
{
    public class TourSongView : SongView
    {
        public Image lockedIcon;
        public Button favoriteButton;
        public TextMeshProUGUI songOrCategoryName;
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

            Debug.Log("View Type for {vieweType.Name}: " + viewType.GetType().Name + " setting " + songOrCategoryName + " to " + songOrCategoryName.overflowMode, songOrCategoryName);

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