using System;

namespace YARG
{
    [Serializable]
    public class TourData
    {
        /// <summary>
        /// Unique identifier for this tour. Used to locate the corresponding save database.
        /// </summary>
        public Guid TourId;

        /// <summary>
        /// Display name of the tour.
        /// </summary>
        public string TourName = "New Tour";

        /// <summary>
        /// Author/creator of the tour.
        /// </summary>
        public string Author = "Unknown";

        public bool ShowAllLockedShows = false;

        /// <summary>
        /// The ordered list of shows in this tour.
        /// Shows are referenced by their 0-based index within this array.
        /// </summary>
        public TourShowData[] Shows;

        public TourProgress GetTourProgress()
        {
            return TourManager.GetTourProgress(TourId);
        }
    }
}