using System;

namespace YARG
{
    /// <summary>
    /// Identifies a song within a tour by its human-readable name and artist.
    /// At runtime this is matched against the loaded <see cref="YARG.Core.Song.SongEntry"/> catalogue.
    /// </summary>
    [Serializable]
    public class TourSongEntry
    {
        public string SongName;
        public string Artist;
    }
}
