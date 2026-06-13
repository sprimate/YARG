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
        /// The index of the show (0-based) to scope this condition to.
        /// Use -1 to check progress across all shows combined.
        /// </summary>
        public int ShowIndex = -1;

        /// <summary>
        /// The amount required to satisfy this condition.
        /// For <see cref="ConditionType.SongsCompleted"/>, this is a count of distinct completed songs.
        /// For <see cref="ConditionType.StarsEarned"/>, this is a total star count.
        /// </summary>
        public int RequiredAmount;
    }
}
