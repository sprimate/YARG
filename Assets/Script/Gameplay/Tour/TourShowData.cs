using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core.Song;
using YARG.Song;

namespace YARG
{
    public enum ELockedSongVisibility
    {
        Visible,
        HideNames,
        Hidden
    }
    /// <summary>
    /// Defines one show within a <see cref="TourData"/>.
    /// A show is an ordered setlist of songs that becomes accessible once its
    /// <see cref="UnlockConditions"/> are all satisfied.
    /// </summary>
    [Serializable]
    public class TourShowData
    {
        /// <summary>
        /// Display name of the show.
        /// </summary>
        public string ShowName;

        public bool ShowLockedSongs = false;

        /// <summary>
        /// The ordered list of songs in this show, identified by name and artist.
        /// </summary>
        public TourSongEntry[] Songs;

        /// <summary>
        /// All conditions that must be satisfied before this show is accessible.
        /// An empty array means the show is always unlocked.
        /// </summary>
        public TourUnlockCondition[] UnlockConditions;

        public IEnumerable<SongEntry> GetSongEntries()
        {
            foreach (var incomingEntry in Songs)
            {
                var entry = SongContainer.GetFuzzySongEntry(incomingEntry.Artist, incomingEntry.SongName);
                if (entry != null)
                {
                    yield return entry;
                }
            }
        }
    }
}