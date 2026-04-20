using Godot;
#nullable enable
using System;
using System.Collections.Generic;

public partial class GameState : Node
{
    public const int MaxLevelCount = 5;
    private const long LeaderboardStageScoreMultiplier = 1_000_000_000L;
    private const long LeaderboardTimeMultiplier = 1_000L;
    private const long MaxLeaderboardTimeMs = 999_999L;
    private const int MaxLeaderboardCountValue = 999;
    private const int MaxLeaderboardBackspaceValue = 9;

    public sealed class StageResult
    {
        public int Level { get; set; }
        public long TargetClearTimeMs { get; set; }
        public long BestTargetClearTimeMs { get; set; }
        public int StageScore { get; set; }
        public int BestStageScore { get; set; }
        public int StageClearBonus { get; set; }
        public int TargetFlyScore { get; set; }
        public int ExtraCleanupBonus { get; set; }
        public int FlawlessAnswersBonus { get; set; }
        public int NoBackspaceBonus { get; set; }
        public int TargetFlyCount { get; set; }
        public int ExtraCleanupKills { get; set; }
        public int WrongAnswers { get; set; }
        public int BackspaceUses { get; set; }
        public long PackedLeaderboardScore { get; set; }
        public long CampaignLeaderboardScore { get; set; }
        public int CampaignStageScoreTotal { get; set; }
        public int CampaignStartLevel { get; set; }
        public bool IsNewBestScore { get; set; }
        public bool IsNewBestTargetClearTime { get; set; }
    }

    public int CurrentLevel { get; set; }
    public int HighestUnlockedLevel { get; set; }
    public int Hearts { get; set; }
    public int FliesDestroyed { get; set; }
    public long CurrentCampaignLeaderboardScore { get; private set; }
    public int CurrentCampaignStageScoreTotal { get; private set; }
    public int CurrentCampaignStartLevel { get; private set; } = 1;
    public int CurrentCampaignStagesCompleted { get; private set; }
    public bool IsCampaignActive { get; private set; }
    public StageResult? LastStageResult { get; private set; }

    private readonly Dictionary<int, long> bestFlyCountZeroTimesMs = new();
    private readonly Dictionary<int, int> bestStageScores = new();
    private readonly Dictionary<int, long> bestPackedStageScores = new();

    public GameState()
    {
        CurrentLevel = 1;
        HighestUnlockedLevel = 1;
        Hearts = 1;
        FliesDestroyed = 0;
        CurrentCampaignLeaderboardScore = 0;
        CurrentCampaignStageScoreTotal = 0;
        CurrentCampaignStagesCompleted = 0;
        IsCampaignActive = false;
    }

    public void ResetGame()
    {
        CurrentLevel = 1;
        HighestUnlockedLevel = 1;
        Hearts = 1;
        FliesDestroyed = 0;
        LastStageResult = null;
        ResetCampaignProgress();
        bestFlyCountZeroTimesMs.Clear();
        bestStageScores.Clear();
        bestPackedStageScores.Clear();
    }

    public void StartCampaign(int startLevel)
    {
        ResetCampaignProgress();
        CurrentCampaignStartLevel = Math.Clamp(startLevel, 1, MaxLevelCount);
        IsCampaignActive = true;
    }

    public void ResetCampaignProgress()
    {
        CurrentCampaignLeaderboardScore = 0;
        CurrentCampaignStageScoreTotal = 0;
        CurrentCampaignStagesCompleted = 0;
        CurrentCampaignStartLevel = 1;
        IsCampaignActive = false;
        LastStageResult = null;
    }

    public bool RecordFlyCountZeroTime(int level, long elapsedMilliseconds)
    {
        int normalizedLevel = Math.Max(1, level);
        long normalizedMilliseconds = Math.Max(0, elapsedMilliseconds);
        if (bestFlyCountZeroTimesMs.TryGetValue(normalizedLevel, out long existingBestTime) && existingBestTime <= normalizedMilliseconds)
        {
            return false;
        }

        bestFlyCountZeroTimesMs[normalizedLevel] = normalizedMilliseconds;
        return true;
    }

    public long GetBestFlyCountZeroTime(int level)
    {
        int normalizedLevel = Math.Max(1, level);
        return bestFlyCountZeroTimesMs.TryGetValue(normalizedLevel, out long bestTime) ? bestTime : -1;
    }

    public int GetBestStageScore(int level)
    {
        int normalizedLevel = Math.Max(1, level);
        return bestStageScores.TryGetValue(normalizedLevel, out int bestScore) ? bestScore : 0;
    }

    public int GetTotalStageScoreToLevel(int level)
    {
        int normalizedLevel = Math.Clamp(level, 1, MaxLevelCount);
        int totalScore = 0;
        for (int stage = 1; stage <= normalizedLevel; stage++)
        {
            totalScore += GetBestStageScore(stage);
        }

        return totalScore;
    }

    public long GetTotalPackedLeaderboardScoreToLevel(int level)
    {
        int normalizedLevel = Math.Clamp(level, 1, MaxLevelCount);
        long totalScore = 0;
        for (int stage = 1; stage <= normalizedLevel; stage++)
        {
            if (bestPackedStageScores.TryGetValue(stage, out long packedStageScore))
            {
                totalScore += packedStageScore;
            }
        }

        return totalScore;
    }

    public void RecordStageResult(StageResult stageResult)
    {
        int normalizedLevel = Math.Max(1, stageResult.Level);
        int normalizedScore = Math.Max(0, stageResult.StageScore);

        bool isNewBestScore = !bestStageScores.TryGetValue(normalizedLevel, out int existingBestScore) || normalizedScore > existingBestScore;
        if (isNewBestScore)
        {
            bestStageScores[normalizedLevel] = normalizedScore;
        }

        stageResult.Level = normalizedLevel;
        stageResult.StageScore = normalizedScore;
        stageResult.PackedLeaderboardScore = BuildPackedLeaderboardScore(stageResult);
        if (!bestPackedStageScores.TryGetValue(normalizedLevel, out long existingPackedStageScore) || stageResult.PackedLeaderboardScore > existingPackedStageScore)
        {
            bestPackedStageScores[normalizedLevel] = stageResult.PackedLeaderboardScore;
        }

        CurrentCampaignStageScoreTotal = GetTotalStageScoreToLevel(normalizedLevel);
        CurrentCampaignLeaderboardScore = GetTotalPackedLeaderboardScoreToLevel(normalizedLevel);
        CurrentCampaignStagesCompleted = normalizedLevel;
        CurrentCampaignStartLevel = 1;
        IsCampaignActive = true;
        stageResult.CampaignStageScoreTotal = CurrentCampaignStageScoreTotal;
        stageResult.CampaignLeaderboardScore = CurrentCampaignLeaderboardScore;
        stageResult.CampaignStartLevel = CurrentCampaignStartLevel;
        stageResult.IsNewBestScore = isNewBestScore;
        stageResult.BestStageScore = GetBestStageScore(normalizedLevel);
        stageResult.BestTargetClearTimeMs = GetBestFlyCountZeroTime(normalizedLevel);

        LastStageResult = stageResult;
    }

    public void UnlockNextLevel()
    {
        HighestUnlockedLevel = Math.Min(MaxLevelCount, Math.Max(HighestUnlockedLevel, CurrentLevel + 1));
    }

    public void UpdateHearts(int change)
    {
        Hearts += change;
        if (Hearts < 0)
        {
            Hearts = 0;
        }
    }

    public void AddFlyDestroyed()
    {
        FliesDestroyed++;
    }

    private static long BuildPackedLeaderboardScore(StageResult stageResult)
    {
        long clampedTimeScore = MaxLeaderboardTimeMs - Math.Clamp(stageResult.TargetClearTimeMs, 0L, MaxLeaderboardTimeMs);
        long wrongAnswerScore = MaxLeaderboardCountValue - Math.Clamp(stageResult.WrongAnswers, 0, MaxLeaderboardCountValue);
        long backspaceScore = MaxLeaderboardBackspaceValue - Math.Clamp(stageResult.BackspaceUses, 0, MaxLeaderboardBackspaceValue);

        return (Math.Max(stageResult.StageScore, 0) * LeaderboardStageScoreMultiplier)
            + (clampedTimeScore * LeaderboardTimeMultiplier)
            + (wrongAnswerScore * 10L)
            + backspaceScore;
    }
}