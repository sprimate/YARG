using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Core.Input;
using YARG.Menu;
using YARG.Menu.ListMenu;
using YARG.Menu.Navigation;


namespace YARG
{
    public class TourListMenu : ListMenu<TourListViewType, TourView>
    {
        protected override int ExtraListViewPadding => 15;

        private void OnEnable()
        {
            RequestViewListUpdate();
            TourEditorMenu.RequestViewListUpdate = RequestViewListUpdate;
            TourCloudMenu.RequestViewListUpdate = RequestViewListUpdate;

            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Up",
                    ctx => {
                        SetWrapAroundState(!ctx.IsRepeat);
                        SelectedIndex--;
                    }),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Down",
                    ctx => {
                        SetWrapAroundState(!ctx.IsRepeat);
                        SelectedIndex++;
                    }),
                new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm",
                    () => {
                        if (CurrentSelection != null)
                        {
                            CurrentSelection.OpenTour();
                        }
                    }),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back",
                    () => MenuManager.Instance.PopMenu()),
                new NavigationScheme.Entry(MenuAction.Yellow, "Menu.TourList.EditTour",
                    () => {
                        if (CurrentSelection != null)
                        {
                            TourEditorMenu.EditTour(CurrentSelection.TourData);
                        }
                    }),
                new NavigationScheme.Entry(MenuAction.Blue, "Menu.TourList.CreateTour", TourEditorMenu.CreateAndEditTour),
                new NavigationScheme.Entry(MenuAction.Orange, "Menu.TourList.CloudTours", TourCloudMenu.Open),

            }, false));
        }

        private void OnDisable()
        {
            TourEditorMenu.OnDisable();
            TourCloudMenu.OnDisable();
            Navigator.Instance.PopScheme();
        }

        protected override List<TourListViewType> CreateViewList()
        {
            List<TourListViewType> viewList = new List<TourListViewType>();
            foreach (var tourData in TourManager.GetAllTours())
            {
                viewList.Add(new TourListViewType(tourData));
            }

            Debug.Log($"Added {viewList.Count} tours to the tour list menu");
            return viewList.OrderBy(v => v.TourData.TourName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        void OnGUI()
        {
            TourEditorMenu.OnGUI();
        }
    }
}