using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using YARG.Core.Game;
using YARG.Core.Song;
using YARG.Menu.ListMenu;
using YARG.Menu.MusicLibrary;
using YARG.Player;
using YARG.Scores;
using YARG.Settings;
using YARG.Song;

namespace YARG
{
    public class TourMenu : MusicLibraryMenu
    {
        protected override bool ForceSimpleNavigationEntries => true;
        public TextMeshProUGUI totalStarsText;
        public TextMeshProUGUI totalScoreText;
        public TextMeshProUGUI totalSongsText;
        private TourData CurrentTourData { get => GlobalVariables.State.CurrentTour; set => GlobalVariables.State.CurrentTour = value; }
        private static TourData lastTourData;
        private static bool? lastUseAllResults;
        public static bool IsLocked(ViewType viewType)
        {
            return viewType is TourSongViewType songViewType && songViewType.isLocked;
        }

        protected override void OnEnable()
        {
            if (lastTourData == CurrentTourData &&
                lastUseAllResults == SettingsManager.Settings.UseAllResultsInTour.Value)
            {
                SetReload(MusicLibraryReloadState.None);
            }
            else
            {
                SetReload(MusicLibraryReloadState.Full);
            }

            base.OnEnable();
            UpdateScoresTexts();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            SetReload(MusicLibraryReloadState.Full);
        }

        protected override void Refresh()
        {
            base.Refresh();
            UpdateScoresTexts();
        }

        void UpdateScoresTexts()
        {
            var tourProgress = TourManager.GetTourProgress(CurrentTourData);
            Debug.Log("Updating Scores!");
            totalSongsText.text = $"Songs: {tourProgress.TotalSongs}";
            totalStarsText.text = $"Stars: {tourProgress.TotalStars}";
            totalScoreText.text = $"Score: {tourProgress.TotalScore}";
        }

        private static string BuildUnlockText(TourShowData show)
        {
            if (show.UnlockConditions == null || show.UnlockConditions.Length == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            foreach (var condition in show.UnlockConditions)
            {
                string conditionText = condition.TypeOfCondition switch
                {
                    TourUnlockCondition.ConditionType.SongsCompleted =>
                        $"Songs Completed: {condition.RequiredAmount:N0}",
                    TourUnlockCondition.ConditionType.StarsEarned =>
                        $"Stars: {condition.RequiredAmount:N0}",
                    TourUnlockCondition.ConditionType.Score =>
                        $"Score: {condition.RequiredAmount:N0}",
                    _ => string.Empty,
                };

                if (!string.IsNullOrEmpty(conditionText))
                {
                    parts.Add(conditionText);
                }
            }

            return parts.Count == 0 ? string.Empty : $"(Unlock @ {string.Join(" | ", parts)})";
        }

        protected override int ExtraListViewPadding => 15;
        protected override string GetSubheaderText()
        {
            return CurrentTourData?.TourName;
        }

        protected override List<ViewType> CreateViewList()
        {
            List<ViewType> list = new List<ViewType>();
            if (CurrentTourData == null)
            {
                CurrentTourData = lastTourData;
            }
            bool showedFirstLocked = false;
            Debug.LogWarning("Current Tour Data: " + CurrentTourData?.TourName);
            var tourProgress = TourManager.GetTourProgress(CurrentTourData);//UpdateTourProgressCache(CurrentTourData);
            foreach (var show in CurrentTourData.Shows)
            {
                var showSongs = show.GetSongEntries();

                bool isLocked = tourProgress.LockedShows.Contains(show.ShowName);

                var showName = show.ShowName;
                if (isLocked)
                {
                    if (showedFirstLocked && !CurrentTourData.ShowAllLockedShows)
                    {
                        continue;
                    }

                    if (!show.ShowLockedSongs)
                    {
                        showSongs = Enumerable.Empty<SongEntry>();
                    }

                    var unlockText = BuildUnlockText(show);
                    if (!string.IsNullOrEmpty(unlockText))
                    {
                        showName += $" {unlockText}";
                    }

                    showedFirstLocked = true;
                }

                list.Add(new CategoryViewType(showName, showSongs.Count(), showSongs.ToArray()));

                foreach (var song in showSongs)
                {
                    var songType = new TourSongViewType(this, song, isLocked);
                    list.Add(songType);
                }
            }

            UpdateScoresTexts();
            lastTourData = CurrentTourData;
            lastUseAllResults = SettingsManager.Settings.UseAllResultsInTour.Value;
            Debug.LogWarning("SET LAST TOUR DATA TO " + lastTourData?.TourName);
            return list;
        }
    }
}
