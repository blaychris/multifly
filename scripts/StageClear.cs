using Godot;
#nullable enable

public partial class StageClear : Node2D
{
    // Reference to the autoloaded GameState singleton
    private GameState? gameState;
    private Label? titleLabel;
    private Label? clearTimeLabel;
    private Label? bestTimeLabel;
    private Label? scoreLabel;
    private Label? bestScoreLabel;
    private Label? breakdownLabel;
    private HeartDisplay? heartDisplay;
    private Button? replayButton;
    private Button? nextStageButton;
    private Button? exitButton;

    // Stage result data should be set by the previous scene before showing this screen

    public override void _Ready()
    {
        // Lower BGM volume to 50% for stage clear
        var audioManager = GetNodeOrNull<AudioManager>("/root/AudioManager");
        audioManager?.SetBackgroundMusicVolume(0.25f);
        gameState = GetNodeOrNull<GameState>("/root/GameState");
        titleLabel = GetNodeOrNull<Label>("TitleLabel");
        clearTimeLabel = GetNodeOrNull<Label>("ClearTimeLabel");
        bestTimeLabel = GetNodeOrNull<Label>("BestTimeLabel");
        scoreLabel = GetNodeOrNull<Label>("ScoreLabel");
        bestScoreLabel = GetNodeOrNull<Label>("BestScoreLabel");
        breakdownLabel = GetNodeOrNull<Label>("BreakdownLabel");
        heartDisplay = GetNodeOrNull<HeartDisplay>("HeartDisplay");
        replayButton = GetNode<Button>("ReplayButton");
        nextStageButton = GetNode<Button>("NextStageButton");
        exitButton = GetNode<Button>("ExitButton");

        replayButton.Pressed += OnReplayPressed;
        nextStageButton.Pressed += OnNextStagePressed;
        exitButton.Pressed += OnExitPressed;

        if (gameState == null || !gameState.IsCampaignActive)
        {
            nextStageButton.Visible = false;
            nextStageButton.Disabled = true;
        }

        if (breakdownLabel != null)
        {
            breakdownLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        }

        // Display the last stage result if available
        if (gameState != null && gameState.LastStageResult != null)
        {
            var result = gameState.LastStageResult;
            if (titleLabel != null)
                titleLabel.Text = $"Stage {result.Level} Clear!";
            if (clearTimeLabel != null)
                clearTimeLabel.Text = $"Clear time: {FormatMilliseconds(result.TargetClearTimeMs)}";
            if (bestTimeLabel != null)
                bestTimeLabel.Text = GetBestTimeText(result.BestTargetClearTimeMs, result.IsNewBestTargetClearTime);
            if (scoreLabel != null)
                scoreLabel.Text = $"Score: {result.StageScore:N0}";
            if (bestScoreLabel != null)
                bestScoreLabel.Text = $"Best score: {result.BestStageScore:N0}{(result.IsNewBestScore ? "  New best!" : string.Empty)}";
            if (breakdownLabel != null)
                breakdownLabel.Text = BuildBreakdownText(new StageResult {
                    Level = result.Level,
                    TargetClearTimeMs = result.TargetClearTimeMs,
                    BestTargetClearTimeMs = result.BestTargetClearTimeMs,
                    StageScore = result.StageScore,
                    BestStageScore = result.BestStageScore,
                    StageClearBonus = result.StageClearBonus,
                    TargetFlyScore = result.TargetFlyScore,
                    ExtraCleanupBonus = result.ExtraCleanupBonus,
                    FlawlessAnswersBonus = result.FlawlessAnswersBonus,
                    NoBackspaceBonus = result.NoBackspaceBonus,
                    TargetFlyCount = result.TargetFlyCount,
                    ExtraCleanupKills = result.ExtraCleanupKills,
                    WrongAnswers = result.WrongAnswers,
                    BackspaceUses = result.BackspaceUses,
                    CampaignStageScoreTotal = result.CampaignStageScoreTotal,
                    IsNewBestScore = result.IsNewBestScore,
                    IsNewBestTargetClearTime = result.IsNewBestTargetClearTime
                });
            if (heartDisplay != null)
                heartDisplay.SetHeartCount(gameState.Hearts);
        }
            var totalScoreLabel = GetNodeOrNull<Label>("TotalScoreLabel");
            if (gameState != null && totalScoreLabel != null)
            {
                if (gameState.IsCampaignActive)
                {
                    int totalScore = gameState.GetCurrentCampaignScore();
                    totalScoreLabel.Text = $"Total Score: {totalScore:N0}";
                    totalScoreLabel.Visible = true;
                }
                else
                {
                    totalScoreLabel.Visible = false;
                }
            }
    }



    private static string FormatMilliseconds(long milliseconds)
    {
        double seconds = Mathf.Max(milliseconds, 0) / 1000.0;
        return $"{seconds:0.00}s";
    }

    private static string GetBestTimeText(long bestTimeMs, bool isNewBest = false)
    {
        if (bestTimeMs < 0)
        {
            return "Best target clear: no record yet";
        }
        string recordSuffix = isNewBest ? "  New best!" : string.Empty;
        return $"Best target clear: {FormatMilliseconds(bestTimeMs)}{recordSuffix}";
    }

    private static string BuildBreakdownText(StageResult stageResult)
    {
        string flawlessLine = stageResult.WrongAnswers == 0
            ? $"Flawless answers bonus: +{stageResult.FlawlessAnswersBonus:N0}"
            : $"Flawless answers bonus: +0 ({stageResult.WrongAnswers} wrong {(stageResult.WrongAnswers == 1 ? "answer" : "answers")})";
        string cleanInputLine = stageResult.BackspaceUses == 0
            ? $"Clean input bonus: +{stageResult.NoBackspaceBonus:N0}"
            : $"Clean input bonus: +0 ({stageResult.BackspaceUses} backspace {(stageResult.BackspaceUses == 1 ? "use" : "uses")})";
        return string.Join("\n", new[]
        {
            $"Score criteria:",
            $"Stage clear bonus: +{stageResult.StageClearBonus:N0}",
            $"Target fly takedowns: {stageResult.TargetFlyCount} x 100 = +{stageResult.TargetFlyScore:N0}",
            $"Bonus-window takedowns: {stageResult.ExtraCleanupKills} x 150 = +{stageResult.ExtraCleanupBonus:N0}",
            flawlessLine,
            cleanInputLine,
            $"Total Score Stage 1 to {stageResult.Level}: {stageResult.CampaignStageScoreTotal:N0}"
        });
    }

    private void OnReplayPressed()
    {
        var audioManager = GetNodeOrNull<AudioManager>("/root/AudioManager");
        audioManager?.SetBackgroundMusicVolume(1.0f);
        //audioManager?.StopBackgroundMusic();
        GetTree().ChangeSceneToFile("res://scenes/Gameplay.tscn");
    }

    private void OnNextStagePressed()
    {
        var audioManager = GetNodeOrNull<AudioManager>("/root/AudioManager");
        audioManager?.SetBackgroundMusicVolume(1.0f);
        // Use GameState singleton for level management
        if (gameState == null)
        {
            GD.PushError("GameState singleton not found!");
            return;
        }
        if (gameState.CurrentLevel >= GameState.MaxLevelCount)
        {
            // If in campaign, submit score to Google Play
            if (gameState.IsCampaignActive)
            {
                int totalScore = gameState.GetCurrentCampaignScore();
                gameState.SubmitScoreToGooglePlay(totalScore);
            }
            GetTree().ChangeSceneToFile("res://scenes/LevelSelect.tscn");
            return;
        }
        // Unlock the next level if not already unlocked
        gameState.UnlockNextLevel();
        gameState.CurrentLevel += 1;
        GetTree().ChangeSceneToFile("res://scenes/Gameplay.tscn");
    }

    private void OnExitPressed()
    {
        var audioManager = GetNodeOrNull<AudioManager>("/root/AudioManager");
        audioManager?.SetBackgroundMusicVolume(1.0f);
        audioManager?.StopBackgroundMusic();

        if (gameState != null && gameState.IsCampaignActive)
        {
            int totalScore = gameState.GetCurrentCampaignScore();
            gameState.SubmitScoreToGooglePlay(totalScore);
        }

        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }

    // Minimal StageResult definition for display
    public class StageResult
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
        public int CampaignStageScoreTotal { get; set; }
        public bool IsNewBestScore { get; set; }
        public bool IsNewBestTargetClearTime { get; set; }
    }
}