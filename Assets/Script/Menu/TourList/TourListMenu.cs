using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Core.Input;
using YARG.Menu;
using YARG.Menu.ListMenu;
using YARG.Menu.Navigation;
using YARG.Settings;

namespace YARG
{
    public class TourListMenu : ListMenu<TourListViewType, TourView>
    {
        protected override int ExtraListViewPadding => 15;

        private void OnEnable()
        {
            RequestViewListUpdate();

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
                            TourMenu.SelectedTourData = CurrentSelection.TourData;
                            MenuManager.Instance.PushMenu(MenuManager.Menu.Tour, true);
                        }
                    }),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back",
                    () => MenuManager.Instance.PopMenu()),
            }, false));
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();
        }

        protected override List<TourListViewType> CreateViewList()
        {
            List<TourListViewType> viewList = new List<TourListViewType>();
            // viewList.Add(new TourListViewType(new TourData()));
            //add all the tours available right now. Just read the json files from a hardcoded path right now... C:\Users\sprim\Documents\Clone Hero\Tours
            foreach (var dir in SettingsManager.Settings.SongFolders)
            {
                Debug.Log($"Looking for tour files in directory: {dir}");
                foreach (var file in System.IO.Directory.GetFiles(dir, "*.tour", System.IO.SearchOption.AllDirectories))
                {
                    var json = System.IO.File.ReadAllText(file);
                    var tourData = JsonConvert.DeserializeObject<TourData>(json);
                    viewList.Add(new TourListViewType(tourData));
                }
            }


            Debug.Log($"Added {viewList.Count} tours to the tour list menu");
            return viewList;
        }
    }
}