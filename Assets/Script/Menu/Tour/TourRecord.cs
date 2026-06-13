using System;
using SQLite;

namespace YARG.Scores
{
    [Table("TourRecords")]
    public class TourRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// The ID of the tour this record belongs to.
        /// </summary>
        [Indexed]
        public Guid TourId { get; set; }

        public string SongName { get; set; }
        public string Artist   { get; set; }

        /// <summary>
        /// 0-based index of the show within the tour's show list.
        /// </summary>
        public int ShowIndex { get; set; }

        /// <summary>
        /// Best band stars achieved for this song in this tour. Stored independently of MaxScore.
        /// </summary>
        public int MaxStars { get; set; }

        /// <summary>
        /// Best band score achieved for this song in this tour. Stored independently of MaxStars.
        /// </summary>
        public int MaxScore { get; set; }

        public bool Completed { get; set; }
    }
}
