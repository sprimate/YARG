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
    }
}
