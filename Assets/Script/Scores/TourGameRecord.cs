using System;
using SQLite;

namespace YARG.Scores
{
    [Table("TourGameRecords")]
    public class TourGameRecord
    {
        // DO NOT change any of these field names
        // without changing the SQL queries!

        [Indexed]
        public Guid TourId { get; set; }

        [Indexed]
        public int GameRecordId { get; set; }
    }
}
