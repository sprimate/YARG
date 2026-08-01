using UnityEngine;
using YARG.Menu;
using YARG.Menu.ListMenu;
using YARG.Menu.MusicLibrary;

namespace YARG
{
    public class TourListViewType : BaseViewType
    {
        public override BackgroundType Background => BackgroundType.Normal;
        public TourData TourData { get; private set; }
        public TourListViewType(TourData tourData)
        {
            TourData = tourData;
        }

        public override string GetPrimaryText(bool selected)
        {
            return FormatAs(TourData.TourName, TextType.Primary, selected);
        }

        public override string GetSecondaryText(bool selected)
        {
            return string.Empty;
        }

        public void OpenTour()
        {
            MusicLibraryMenu.SetReload(MusicLibraryReloadState.Full);
            GlobalVariables.State.CurrentTour = TourData;
            Debug.LogWarning($"Setting current tour to {TourData.TourName} with id {TourData.TourId}");

            MenuManager.Instance.PushMenu(MenuManager.Menu.Tour, true);
        }
    }
}
