using TMPro;
using UnityEngine;
using YARG.Menu;
using YARG.Menu.ListMenu;
using YARG.Menu.MusicLibrary;

namespace YARG
{
    public class TourView : ViewObject<TourListViewType>
    {
        public void OpenTour() => ViewType.OpenTour();
    }
}