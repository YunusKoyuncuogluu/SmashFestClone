using UnityEngine;

namespace SmashFest.Progress
{
    public struct LevelResult
    {
        public int Level;
        public int Stars;
        public int ClearReward;
        public int BallsLeft;
        public int BallReward;

        public int TotalReward => ClearReward + BallReward;
    }

    public class LevelRewardRules
    {
        public const int MaxStars = 3;

        private const int BaseReward = 10;
        private const int BonusRewardMultiplier = 3;
        private const int LevelsPerBonusCoin = 10;
        private const int CoinsPerBall = 5;
        private const float ThreeStarShare = 0.4f;
        private const float TwoStarShare = 0.15f;

        public LevelResult Evaluate(int level, int ballsLeft, int maxBalls, bool isBonus)
        {
            if (isBonus)
                return EvaluateBonus(level);

            float share = maxBalls > 0 ? (float)ballsLeft / maxBalls : 0f;
            return new LevelResult
            {
                Level = level,
                Stars = share >= ThreeStarShare ? 3 : share >= TwoStarShare ? 2 : 1,
                ClearReward = ClearRewardFor(level),
                BallsLeft = ballsLeft,
                BallReward = ballsLeft * CoinsPerBall
            };
        }

        private LevelResult EvaluateBonus(int level)
        {
            return new LevelResult
            {
                Level = level,
                Stars = MaxStars,
                ClearReward = ClearRewardFor(level) * BonusRewardMultiplier
            };
        }

        private int ClearRewardFor(int level) => BaseReward + Mathf.Max(0, level - 1) / LevelsPerBonusCoin;
    }
}
