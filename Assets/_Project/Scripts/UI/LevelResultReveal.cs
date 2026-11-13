using DG.Tweening;
using SmashFest.Progress;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SmashFest.UI
{
    public class LevelResultReveal
    {
        private const string RewardFormat = "+{0:0}";
        private const string DetailFormat = "Clear +{0:0}   Balls +{1:0}";
        private const float Duration = 1.7f;
        private const float FirstStarDelay = 0.25f;
        private const float StarInterval = 0.22f;
        private const float StarPopDuration = 0.3f;
        private const float StarStartScale = 0.3f;
        private const float CountStart = 0.95f;
        private const float CountDuration = 0.65f;

        private readonly Image[] stars;
        private readonly TMP_Text rewardLabel;
        private readonly TMP_Text detailLabel;
        private readonly Color earnedColor;
        private readonly Color emptyColor;
        private readonly ProgressTween timeline;
        private int earnedStars;
        private int totalReward;
        private int shownReward = -1;

        public LevelResultReveal(Image[] stars, TMP_Text rewardLabel, TMP_Text detailLabel, Color emptyColor, GameObject owner)
        {
            this.stars = stars;
            this.rewardLabel = rewardLabel;
            this.detailLabel = detailLabel;
            this.emptyColor = emptyColor;
            earnedColor = stars.Length > 0 ? stars[0].color : Color.white;
            timeline = new ProgressTween(Duration, Apply, owner, true, false);
        }

        public void Play(LevelResult result)
        {
            earnedStars = result.Stars;
            totalReward = result.TotalReward;
            shownReward = -1;
            if (detailLabel != null)
                detailLabel.SetText(DetailFormat, result.ClearReward, result.BallReward);

            timeline.Restart();
        }

        private void Apply(float progress)
        {
            float time = progress * Duration;
            for (int i = 0; i < stars.Length; i++)
                ApplyStar(stars[i], i < earnedStars ? (time - FirstStarDelay - i * StarInterval) / StarPopDuration : -1f);

            float count = Mathf.Clamp01((time - CountStart) / CountDuration);
            int reward = Mathf.RoundToInt(totalReward * DOVirtual.EasedValue(0f, 1f, count, Ease.OutQuad));
            if (reward == shownReward || rewardLabel == null)
                return;

            shownReward = reward;
            rewardLabel.SetText(RewardFormat, reward);
        }

        private void ApplyStar(Image star, float reveal)
        {
            Transform starTransform = star.transform;
            if (reveal <= 0f)
            {
                star.color = emptyColor;
                starTransform.localScale = Vector3.one;
                return;
            }

            float scale = DOVirtual.EasedValue(StarStartScale, 1f, Mathf.Clamp01(reveal), Ease.OutBack);
            star.color = earnedColor;
            starTransform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
