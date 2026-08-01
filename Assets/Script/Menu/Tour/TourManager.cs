using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Core.Song;
using YARG.Player;
using YARG.Scores;
using YARG.Settings;
using YARG.Core.Game;
using Cysharp.Threading.Tasks;

namespace YARG
{
    public record TourProgress
    {
        public int TotalStars;
        public int TotalSongs;
        public int TotalScore;
        public HashSet<string> LockedShows = new HashSet<string>();
        public Dictionary<SongEntry, PlayerScoreRecord> HighScoreRecords = new Dictionary<SongEntry, PlayerScoreRecord>();
    }
    public static class TourManager
    {
        private static Dictionary<Guid, TourProgress> tourProgressCache = new Dictionary<Guid, TourProgress>();
        private static HashSet<SongEntry> UnlockedSongsCache = new();

        public static async UniTask UpdateTourProgressCache()
        {
            await UniTask.SwitchToThreadPool();
            tourProgressCache.Clear();
            UnlockedSongsCache.Clear();

            foreach (var tourData in GetAllTours())
            {
                UpdateTourProgressCache(tourData);
            }

            await UniTask.SwitchToMainThread();
        }

        public static TourProgress UpdateTourProgressCache(TourData tourData, params SongEntry[] modifiedSongs)
        {
            tourProgressCache[tourData.TourId] = CalculateTourProgress(tourData, modifiedSongs);
            return tourProgressCache[tourData.TourId];
        }

        public static TourProgress GetTourProgress(TourData tourData)
        {
            return GetTourProgress(tourData.TourId);
        }
        public static TourProgress GetTourProgress(Guid tourId)
        {
            if (tourProgressCache.TryGetValue(tourId, out var p))
            {
                return p;
            }
            else
            {
                // If we don't have it cached for some reason, calculate it on the spot
                var tourData = GetAllTours().FirstOrDefault(t => t.TourId == tourId);
                if (tourData == null)
                {
                    throw new ArgumentException($"No tour found with ID {tourId}");
                }

                tourProgressCache[tourId] = CalculateTourProgress(tourData);
                return tourProgressCache[tourId];
            }
        }

        private static TourProgress CalculateTourProgress(TourData tourData, params SongEntry[] modifiedSongs)
        {
            bool fullRefresh = modifiedSongs?.Any() != true;
            TourProgress progress = fullRefresh ? new TourProgress() : GetTourProgress(tourData);
            HashSet<SongEntry> songsToCheck = fullRefresh ? null : new HashSet<SongEntry>(modifiedSongs);
            foreach (var show in tourData.Shows)
            {
                bool isLocked = show.UnlockConditions != null && show.UnlockConditions.Length > 0 && show.UnlockConditions.Any(condition => !condition.IsConditionMet(progress));
                if (isLocked)
                {
                    progress.LockedShows.Add(show.ShowName);
                }

                foreach (var song in show.GetSongEntries())
                {
                    if (!fullRefresh && !songsToCheck.Contains(song))
                    {
                        Debug.Log($"Skipping song {song.Artist} - {song.Name} since it isn't one of the modified songs");
                        // If this song isn't one of the modified songs, then we can skip it since it won't have changed anything
                        continue;
                    }

                    var record = GetPlayerScoreRecord(song, tourData);
                    if (record != null && !isLocked)
                    {
                        progress.TotalStars += record.Stars.GetStarCount();
                        progress.TotalScore += record.Score;
                        progress.TotalSongs++;
                    }

                    if (!isLocked)
                    {
                        UnlockedSongsCache.Add(song);
                    }
                }
            }

            return progress;
        }

        private static PlayerScoreRecord GetPlayerScoreRecord(SongEntry song, TourData tourData)
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
                    potentialRecord = db.QueryTourSongHighScore(tourData.TourId, song.Hash, player.Profile.Id, instrument, false);
                }

                if (potentialRecord != null && (bestRecord == null || potentialRecord.Score > bestRecord.Score))
                {
                    bestRecord = potentialRecord;
                }
            }

            return bestRecord;
        }

        public static bool IsSongLocked(SongEntry song)
        {
            return !UnlockedSongsCache.Contains(song);
        }

        public static void UpdateTourProgress(Guid tourId, TourProgress progress)
        {
            tourProgressCache[tourId] = progress;
        }

        public static IEnumerable<TourData> GetAllTours()
        {
            foreach (var dir in SettingsManager.Settings.SongFolders)
            {
                foreach (var file in System.IO.Directory.GetFiles(dir, "*.tour", System.IO.SearchOption.AllDirectories))
                {
                    var json = System.IO.File.ReadAllText(file);
                    yield return JsonConvert.DeserializeObject<TourData>(json);
                }
            }
        }
    }
}
