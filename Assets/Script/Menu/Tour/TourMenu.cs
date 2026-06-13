using System;
using System.Collections.Generic;
using System.Linq;
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
                    db.QueryTourCompletedSongCount(tourId, condition.ShowIndex),
                TourUnlockCondition.ConditionType.StarsEarned =>
                    db.QueryTourTotalStars(tourId, condition.ShowIndex),
                TourUnlockCondition.ConditionType.Score =>
                    db.QueryTourTotalScore(tourId, condition.ShowIndex),
                _ => throw new InvalidOperationException(
                    $"Unknown TourUnlockCondition type: {condition.TypeOfCondition}"),
            };

            return actual >= condition.RequiredAmount;
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
                string scope = condition.ShowIndex == -1
                    ? "Total "
                    : " ";//$"\"Show {SelectedTourData.Shows[condition.ShowIndex].ShowName}\" ";

                string conditionText = condition.TypeOfCondition switch
                {
                    TourUnlockCondition.ConditionType.SongsCompleted =>
                        $"{scope}Songs Completed: {condition.RequiredAmount}",
                    TourUnlockCondition.ConditionType.StarsEarned =>
                        $"{scope}Stars: {condition.RequiredAmount}",
                    TourUnlockCondition.ConditionType.Score =>
                        $"{scope}Score: {condition.RequiredAmount}",
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
                    var unlockText = BuildUnlockText(show);
                    if (!string.IsNullOrEmpty(unlockText))
                    {
                        showName += $" {unlockText}";
                    }
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