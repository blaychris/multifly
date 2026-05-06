using Godot;
#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;

public partial class GameState : Node
{

    // Placeholder for Google Play score submission
    public async void SubmitScoreToGooglePlay(int score)
    {
        if (!string.IsNullOrEmpty(FocusNumbers))
        {
            GD.Print("[Google Play] Skipping score submit because FocusNumbers is set.");
            return;
        }

        if (!IsCampaignActive)
        {
            GD.Print("[Google Play] Skipping score submit because not in campaign mode.");
            return;
        }

        // Attempt to route leaderboard submissions through a Firebase REST service if available.
        var firebaseService = GetNodeOrNull<FirebaseService>("/root/FirebaseService");
        if (firebaseService != null)
        {
            string playerId = OS.GetUniqueId(); // Unique device ID
            string playerName = string.IsNullOrWhiteSpace(PlayerName) ? "Player" : PlayerName;
            int stage = CurrentLevel;
            bool isAutopickOn = IsAutopickEnabled;
            bool isKeyboardSupportOn = IsExternalKeyboardEnabled;

            GD.Print($"FirebaseService: Checking existing record for playerId={playerId} before submit...");
            var existingEntry = await firebaseService.FetchLeaderboardEntryAsync(playerId);
            if (existingEntry == null)
            {
                GD.Print("FirebaseService: No existing leaderboard record found for this device.");
            }
            else
            {
                GD.Print($"FirebaseService: Existing record found: score={existingEntry.Score}, stage={existingEntry.Stage}, timestamp={existingEntry.Timestamp}.");
            }

            if (existingEntry != null && existingEntry.Score >= score)
            {
                GD.Print($"FirebaseService: Current score {score} is not higher than existing record {existingEntry.Score}. Submission skipped.");
                return;
            }

            GD.Print($"FirebaseService: Submitting new score {score} for playerId={playerId}...");
            firebaseService.SubmitScore(playerId, playerName, score, stage, isAutopickOn, isKeyboardSupportOn);
            return;
        }

        GD.Print($"[Google Play] Submitting score: {score}");
        // TODO: Integrate with Google Play Games Services
        GD.Print($"[Google Play] Score submitted successfully: {score}");
    }

    public const int MaxLevelCount = 5;
    private const string PersistenceFilePath = "user://game_state.json";
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
    public bool IsBackgroundMusicEnabled { get; set; } = true;
    public bool IsAutopickEnabled { get; set; } = false;

    public string PlayerName { get; set; } = "";
    public string FocusNumbers { get; set; } = string.Empty;
    public bool IsExternalKeyboardEnabled { get; set; } = false;
    public long CurrentCampaignLeaderboardScore { get; private set; }
    public int CurrentCampaignStageScoreTotal { get; private set; }
    public int CurrentCampaignStartLevel { get; private set; } = 1;
    public int CurrentCampaignStagesCompleted { get; private set; }
    public bool IsCampaignActive { get; set; }
    public StageResult? LastStageResult { get; private set; }

    private readonly Dictionary<int, long> bestFlyCountZeroTimesMs = new();
    private readonly Dictionary<int, int> bestStageScores = new();
    private readonly Dictionary<int, long> bestPackedStageScores = new();

    public override void _Ready()
    {
        LoadPersistentData();
    }

    private sealed class PersistentSaveData
    {
        public int HighestUnlockedLevel { get; set; }
        public Dictionary<int, long> BestFlyTimes { get; set; } = new();
        public Dictionary<int, int> BestStageScores { get; set; } = new();
        public Dictionary<int, long> BestPackedStageScores { get; set; } = new();
        public bool IsAutopickEnabled { get; set; }
        public bool IsExternalKeyboardEnabled { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string FocusNumbers { get; set; } = string.Empty;
    }

    private void LoadPersistentData()
    {
        if (!FileAccess.FileExists(PersistenceFilePath))
        {
            return;
        }

        using var file = FileAccess.Open(PersistenceFilePath, FileAccess.ModeFlags.Read);
        var content = file.GetAsText();
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        try
        {
            var data = JsonSerializer.Deserialize<PersistentSaveData>(content);
            if (data == null)
            {
                return;
            }

            HighestUnlockedLevel = Math.Clamp(data.HighestUnlockedLevel, 1, MaxLevelCount);
            bestFlyCountZeroTimesMs.Clear();
            bestStageScores.Clear();
            bestPackedStageScores.Clear();

            foreach (var kvp in data.BestFlyTimes)
            {
                int level = Math.Clamp(kvp.Key, 1, MaxLevelCount);
                bestFlyCountZeroTimesMs[level] = Math.Max(0L, kvp.Value);
            }

            foreach (var kvp in data.BestStageScores)
            {
                int level = Math.Clamp(kvp.Key, 1, MaxLevelCount);
                bestStageScores[level] = Math.Max(0, kvp.Value);
            }

            foreach (var kvp in data.BestPackedStageScores)
            {
                int level = Math.Clamp(kvp.Key, 1, MaxLevelCount);
                bestPackedStageScores[level] = Math.Max(0L, kvp.Value);
            }

            IsAutopickEnabled = data.IsAutopickEnabled;
            IsExternalKeyboardEnabled = data.IsExternalKeyboardEnabled;
            PlayerName = data.PlayerName ?? string.Empty;
            FocusNumbers = data.FocusNumbers ?? string.Empty;
        }
        catch (Exception e)
        {
            GD.PrintErr($"Failed to load persisted game state: {e.Message}");
        }
    }

    private void SavePersistentData()
    {
        var data = new PersistentSaveData
        {
            HighestUnlockedLevel = HighestUnlockedLevel,
            BestFlyTimes = new Dictionary<int, long>(bestFlyCountZeroTimesMs),
            BestStageScores = new Dictionary<int, int>(bestStageScores),
            BestPackedStageScores = new Dictionary<int, long>(bestPackedStageScores),
            IsAutopickEnabled = IsAutopickEnabled,
            IsExternalKeyboardEnabled = IsExternalKeyboardEnabled,
            PlayerName = PlayerName ?? string.Empty,
            FocusNumbers = FocusNumbers ?? string.Empty
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        var content = JsonSerializer.Serialize(data, options);

        using var file = FileAccess.Open(PersistenceFilePath, FileAccess.ModeFlags.Write);
        file.StoreString(content);
        file.Close();
    }

    public GameState()
    {
        CurrentLevel = 1;
        HighestUnlockedLevel = 1;
        Hearts = 1;
        FliesDestroyed = 0;
        IsExternalKeyboardEnabled = false;
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
        IsExternalKeyboardEnabled = false;
        LastStageResult = null;
        ResetCampaignProgress();
        bestFlyCountZeroTimesMs.Clear();
        bestStageScores.Clear();
        bestPackedStageScores.Clear();
    }

    public void SetPlayerName(string playerName)
    {
        PlayerName = playerName;
        SavePersistentData();
    }

    public void SetFocusNumbers(string focusNumbers)
    {
        FocusNumbers = focusNumbers;
        SavePersistentData();
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
        SavePersistentData();
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

    // Track scores for the current campaign run
    private readonly Dictionary<int, int> campaignStageScores = new();

    public void RecordStageResult(StageResult stageResult)
    {
        int normalizedLevel = Math.Max(1, stageResult.Level);
        int normalizedScore = Math.Max(0, stageResult.StageScore);

        // Track campaign run score (not best)
        campaignStageScores[normalizedLevel] = normalizedScore;

        // Track best score for leaderboard
        bool isNewBestScore = false;
        bool isNewBestPackedScore = false;
        if (string.IsNullOrEmpty(FocusNumbers))
        {
            isNewBestScore = !bestStageScores.TryGetValue(normalizedLevel, out int existingBestScore) || normalizedScore > existingBestScore;
            if (isNewBestScore)
            {
                bestStageScores[normalizedLevel] = normalizedScore;
            }

            stageResult.PackedLeaderboardScore = BuildPackedLeaderboardScore(stageResult);
            if (!bestPackedStageScores.TryGetValue(normalizedLevel, out long existingPackedStageScore) || stageResult.PackedLeaderboardScore > existingPackedStageScore)
            {
                bestPackedStageScores[normalizedLevel] = stageResult.PackedLeaderboardScore;
                isNewBestPackedScore = true;
            }
        }
        else
        {
            stageResult.PackedLeaderboardScore = 0; // or some default
        }

        if (IsCampaignActive)
        {
            CurrentCampaignStageScoreTotal = 0;
            foreach (var score in campaignStageScores.Values)
                CurrentCampaignStageScoreTotal += score;
            CurrentCampaignLeaderboardScore = GetTotalPackedLeaderboardScoreToLevel(normalizedLevel);
            CurrentCampaignStagesCompleted = normalizedLevel;
            stageResult.CampaignStageScoreTotal = CurrentCampaignStageScoreTotal;
            stageResult.CampaignLeaderboardScore = CurrentCampaignLeaderboardScore;
            stageResult.CampaignStartLevel = CurrentCampaignStartLevel;
        }
        else
        {
            CurrentCampaignStageScoreTotal = 0;
            CurrentCampaignLeaderboardScore = 0;
            CurrentCampaignStagesCompleted = 0;
            stageResult.CampaignStageScoreTotal = 0;
            stageResult.CampaignLeaderboardScore = 0;
            stageResult.CampaignStartLevel = 0;
        }

        stageResult.IsNewBestScore = isNewBestScore;
        stageResult.BestStageScore = GetBestStageScore(normalizedLevel);
        stageResult.BestTargetClearTimeMs = GetBestFlyCountZeroTime(normalizedLevel);

        LastStageResult = stageResult;

        if (isNewBestScore || isNewBestPackedScore)
        {
            SavePersistentData();
        }
    }

    public int GetCurrentCampaignScore()
    {
        int total = 0;
        foreach (var score in campaignStageScores.Values)
            total += score;
        return total;
    }

    public void ResetCampaignScores()
    {
        campaignStageScores.Clear();
    }

    public void UnlockNextLevel()
    {
        int newUnlocked = Math.Min(MaxLevelCount, Math.Max(HighestUnlockedLevel, CurrentLevel + 1));
        if (newUnlocked != HighestUnlockedLevel)
        {
            HighestUnlockedLevel = newUnlocked;
            SavePersistentData();
        }
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