using System;

namespace YARG
{
    [Serializable]
    public class TourUnlockCondition
    {
        public enum ConditionType
        {
            SongsCompleted,
            StarsEarned,
            Score
        }

        /// <summary>
        /// What type of requirement this condition checks.
        /// </summary>
        public ConditionType TypeOfCondition;

        /// <summary>
        /// The amount required to satisfy this condition.
        /// For <see cref="ConditionType.SongsCompleted"/>, this is a count of distinct completed songs.
        /// For <see cref="ConditionType.StarsEarned"/>, this is a total star count.
        /// </summary>
        public int RequiredAmount;

        public bool IsConditionMet(TourProgress progress)
        {
            // var db = ScoreContainer.Database;
            // var tourId = SelectedTourData.TourId;

            int actual = TypeOfCondition switch
            {
                TourUnlockCondition.ConditionType.SongsCompleted => progress.TotalSongs,
                TourUnlockCondition.ConditionType.StarsEarned => progress.TotalStars,
                TourUnlockCondition.ConditionType.Score => progress.TotalScore,
                _ => throw new InvalidOperationException(
                    $"Unknown TourUnlockCondition type: {TypeOfCondition}"),
            };

            return actual >= RequiredAmount;
        }
    }
}
