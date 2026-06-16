using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using YARG.Core.Song;
using YARG.Menu.ListMenu;
using YARG.Menu.MusicLibrary;
using YARG.Scores;
using YARG.Song;

namespace YARG
{
    public class TourMenu : MusicLibraryMenu
    {
        public TextMeshProUGUI totalStarsText;
        public TextMeshProUGUI totalScoreText;
        public TextMeshProUGUI totalSongsText;
        public static TourData SelectedTourData { get; set; }
        public static bool IsLocked(ViewType viewType)
        {
            return viewType is TourSongViewType songViewType && songViewType.isLocked;
        }

        public bool IsLocked(TourShowData show)
        {
            foreach (var condition in show.UnlockConditions)
            {
                if (!IsConditionMet(condition))
                {
                    return true;
                }
            }

            return false;
        }

        bool IsConditionMet(TourUnlockCondition condition)
        {
            var db = ScoreContainer.Database;
            var tourId = SelectedTourData.TourId;

            int actual = condition.TypeOfCondition switch
            {
                TourUnlockCondition.ConditionType.SongsCompleted =>
                    db.QueryTourCompletedSongCount(tourId),
                TourUnlockCondition.ConditionType.StarsEarned =>
                    db.QueryTourTotalStars(tourId),
                TourUnlockCondition.ConditionType.Score =>
                    db.QueryTourTotalScore(tourId),
                _ => throw new InvalidOperationException(
                    $"Unknown TourUnlockCondition type: {condition.TypeOfCondition}"),
            };

            return actual >= condition.RequiredAmount;
        }

        protected override void Refresh()
        {
            base.Refresh();
            var db = ScoreContainer.Database;
            var tourId = SelectedTourData.TourId;

            totalSongsText.text = $"Songs: {db.QueryTourCompletedSongCount(tourId)}";
            totalStarsText.text = $"Stars: {db.QueryTourTotalStars(tourId)}";
            totalScoreText.text = $"Score: {db.QueryTourTotalScore(tourId)}";
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
                        $"Songs Completed: {condition.RequiredAmount}",
                    TourUnlockCondition.ConditionType.StarsEarned =>
                        $"Stars: {condition.RequiredAmount}",
                    TourUnlockCondition.ConditionType.Score =>
                        $"Score: {condition.RequiredAmount}",
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
            return SelectedTourData?.TourName;
        }
        protected override List<ViewType> CreateViewList()
        {
            List<ViewType> list = new List<ViewType>();
            bool showedFirstLocked = false;
            foreach (var show in SelectedTourData.Shows)
            {

                List<SongEntry> showSongs = new();
                foreach (var incomingEntry in show.Songs)
                {
                    if (SongContainer.Artists.TryGetValue(new SortString(incomingEntry.Artist), out var artistContainer))
                    {
                        var entry = artistContainer.FirstOrDefault(songEntry => songEntry?.Name == incomingEntry?.SongName);
                        if (entry != null)
                        {
                            showSongs.Add(entry);
                            continue;
                        }
                        else
                        {
                            Debug.LogError("Could not find song " + incomingEntry.SongName + " by " + incomingEntry.Artist);
                            //Probably put up a special ViewType for songs that we couldn't find, telling the user they need to add/download it
                        }
                    }
                }

                bool isLocked = IsLocked(show);

                var showName = show.ShowName;
                if (isLocked)
                {
                    if (showedFirstLocked && !SelectedTourData.ShowAllLockedShows)
                    {
                        continue;
                    }

                    if (!show.ShowLockedSongs)
                    {
                        showSongs.Clear();
                    }

                    var unlockText = BuildUnlockText(show);
                    if (!string.IsNullOrEmpty(unlockText))
                    {
                        showName += $" {unlockText}";
                    }

                    showedFirstLocked = true;
                }


                list.Add(new CategoryViewType(showName, showSongs.Count, showSongs.ToArray()));

                foreach (var song in showSongs)
                {
                    list.Add(new TourSongViewType(this, song, isLocked));
                }
            }

            return list;
        }
    }
}