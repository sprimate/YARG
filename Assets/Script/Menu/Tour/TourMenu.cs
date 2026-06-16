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
        private TourData setupTourData;
        int totalStars;
        int totalScore;
        int totalSongs;
        public static bool IsLocked(ViewType viewType)
        {
            return viewType is TourSongViewType songViewType && songViewType.isLocked;
        }

        protected override void OnEnable()
        {
            if (setupTourData == CurrentTourData)
            {
                SetReload(MusicLibraryReloadState.None);
            }
            else
            {
                SetReload(MusicLibraryReloadState.Full);
            }
            base.OnEnable();
            UpdateScores();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            CurrentTourData = null;
            SetReload(MusicLibraryReloadState.Full);
        }

        protected override void Refresh()
        {
            base.Refresh();
            UpdateScores();
        }

        void UpdateScores()
        {
            Debug.Log("Updating Scores!");
            totalSongsText.text = $"Songs: {totalSongs}";
            totalStarsText.text = $"Stars: {totalStars}";
            totalScoreText.text = $"Score: {totalScore}";
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
            if (CurrentTourData == null && setupTourData != null)
            {
                CurrentTourData = setupTourData;
                setupTourData = null;
            }

            bool showedFirstLocked = false;
            totalStars = totalScore = totalSongs = 0;

            foreach (var show in CurrentTourData.Shows)
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

                bool isLocked = show.UnlockConditions != null && show.UnlockConditions.Length > 0 && show.UnlockConditions.Any(condition => !IsConditionMet(condition));

                var showName = show.ShowName;
                if (isLocked)
                {
                    if (showedFirstLocked && !CurrentTourData.ShowAllLockedShows)
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
                    var songType = new TourSongViewType(this, song, isLocked);
                    var record = GetPlayerScoreRecord(song);
                    if (record != null && !isLocked)
                    {
                        totalStars += record.Stars.GetStarCount();
                        totalScore += record.Score;
                        totalSongs++;
                    }

                    list.Add(songType);
                }
            }

            UpdateScores();
            setupTourData = CurrentTourData;
            return list;
        }

        PlayerScoreRecord GetPlayerScoreRecord(SongEntry song)
        {
            var db = ScoreContainer.Database;
            var player = PlayerContainer.Players.First(e => !e.Profile.IsBot);
            bool useAllResults = SettingsManager.Settings.UseAllResultsInTour.Value;
            PlayerScoreRecord bestRecord = null;
            foreach (var instrument in Enum.GetValues(typeof(Core.Instrument)).Cast<Core.Instrument>())
            {
                PlayerScoreRecord potentialRecord;
                if (useAllResults)
                {
                    potentialRecord = db.QueryPlayerSongHighScore(song.Hash, player.Profile.Id, instrument, false);
                }
                else
                {
                    potentialRecord = db.QueryTourSongHighScore(CurrentTourData.TourId, song.Hash, player.Profile.Id, instrument, false);
                }

                if (potentialRecord != null && (bestRecord == null || potentialRecord.Score > bestRecord.Score))
                {
                    bestRecord = potentialRecord;
                }
            }

            Debug.Log("Use ALl Results!!! " + useAllResults + " -> " + bestRecord);
            return bestRecord;
        }

        bool IsConditionMet(TourUnlockCondition condition)
        {
            // var db = ScoreContainer.Database;
            // var tourId = SelectedTourData.TourId;

            int actual = condition.TypeOfCondition switch
            {
                TourUnlockCondition.ConditionType.SongsCompleted => totalSongs,
                TourUnlockCondition.ConditionType.StarsEarned => totalStars,
                TourUnlockCondition.ConditionType.Score => totalScore,
                _ => throw new InvalidOperationException(
                    $"Unknown TourUnlockCondition type: {condition.TypeOfCondition}"),
            };

            return actual >= condition.RequiredAmount;
        }
    }
}