using TMPro;
using UnityEngine;
using YARG.Menu;
using YARG.Menu.ListMenu;
using YARG.Menu.MusicLibrary;

namespace YARG
{
    public class TourView : ViewObject<TourListViewType>
    {
        public void OpenTour()
        {
            MusicLibraryMenu.SetReload(MusicLibraryReloadState.Full);
            GlobalVariables.State.CurrentTour = ViewType?.TourData;
            MenuManager.Instance.PushMenu(MenuManager.Menu.Tour, true);
            //Go to menu for the tour
        }
    }
}