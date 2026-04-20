using Godot;
using System.Collections.Generic;

public partial class LevelSelect : Control
{
    private List<LevelData> levels = new();
    private int selectedLevelIndex;
    private readonly List<Button> levelButtons = new();
    private Button backButton;

    // Local state for unlocked level and max level
    public int HighestUnlockedLevel { get; set; } = 1;
    public int MaxLevelCount { get; set; } = 5;
    public int CurrentLevel { get; set; } = 1;

    public override void _Ready()
    {
        levelButtons.Clear();
        levelButtons.Add(GetNode<Button>("LevelContainer/LevelButton1"));
        levelButtons.Add(GetNode<Button>("LevelContainer/LevelButton2"));
        levelButtons.Add(GetNode<Button>("LevelContainer/LevelButton3"));
        levelButtons.Add(GetNode<Button>("LevelContainer/LevelButton4"));
        levelButtons.Add(GetNode<Button>("LevelContainer/LevelButton5"));
        backButton = GetNode<Button>("LevelContainer/BackButton");

        // Always get the latest unlocked level from GameState
        var gameState = GetNodeOrNull<GameState>("/root/GameState");
        if (gameState != null)
        {
            HighestUnlockedLevel = gameState.HighestUnlockedLevel;
            MaxLevelCount = GameState.MaxLevelCount;
            CurrentLevel = gameState.CurrentLevel;
        }
        levels = LoadLevels();
        selectedLevelIndex = Mathf.Max(CurrentLevel - 1, 0);
        UpdateLevelDisplay();

        for (int index = 0; index < levelButtons.Count; index++)
        {
            int capturedIndex = index;
            levelButtons[index].Pressed += () => SelectLevel(capturedIndex);
        }

        backButton.Pressed += OnBackButtonPressed;
    }

    private List<LevelData> LoadLevels()
    {
        int unlockedLevel = Mathf.Clamp(HighestUnlockedLevel, 1, MaxLevelCount);
        List<LevelData> loadedLevels = new();
        // Get GameState singleton
        var gameState = GetNodeOrNull<GameState>("/root/GameState");
        for (int levelNumber = 1; levelNumber <= MaxLevelCount; levelNumber++)
        {
            long bestTime = -1;
            int bestScore = 0;
            if (gameState != null)
            {
                bestTime = gameState.GetBestFlyCountZeroTime(levelNumber);
                bestScore = gameState.GetBestStageScore(levelNumber);
            }
            loadedLevels.Add(new LevelData(
                $"Stage {levelNumber}",
                unlockedLevel < levelNumber,
                bestTime,
                bestScore));
        }
        return loadedLevels;
    }

    private void UpdateLevelDisplay()
    {
        for (int index = 0; index < levelButtons.Count && index < levels.Count; index++)
        {
            LevelData level = levels[index];
            Button button = levelButtons[index];
            string stageName = level.IsLocked ? $"{level.Name} (Locked)" : level.Name;
            string bestTimeText = level.IsLocked
                ? "Best time: Locked"
                : $"Best time: {FormatBestTime(level.BestTimeMs)}";
            string bestScoreText = level.IsLocked
                ? "Best score: Locked"
                : $"Best score: {level.BestScore:N0}";
            button.Text = string.Join("\n", new[]
            {
                stageName,
                bestTimeText,
                bestScoreText
            });
            button.Disabled = level.IsLocked;
        }
    }

    private static string FormatBestTime(long bestTimeMs)
    {
        if (bestTimeMs < 0)
        {
            return "No record";
        }
        double seconds = bestTimeMs / 1000.0;
        return $"{seconds:0.00}s";
    }

    public void SelectLevel(int index)
    {
        if (index < 0 || index >= levels.Count)
            return;
        if (!levels[index].IsLocked)
        {
            selectedLevelIndex = index;
            CurrentLevel = selectedLevelIndex + 1;
            // Set the current level in GameState before starting gameplay
            var gameState = GetNodeOrNull<GameState>("/root/GameState");
            if (gameState != null)
            {
                gameState.CurrentLevel = CurrentLevel;
            }
            GetTree().ChangeSceneToFile("res://scenes/Gameplay.tscn");
        }
        else
        {
            GD.Print("Level is locked!");
        }
    }

    private void OnBackButtonPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }

    public class LevelData
    {
        public string Name { get; }
        public bool IsLocked { get; }
        public long BestTimeMs { get; }
        public int BestScore { get; }
        public LevelData(string name, bool isLocked, long bestTimeMs, int bestScore)
        {
            Name = name;
            IsLocked = isLocked;
            BestTimeMs = bestTimeMs;
            BestScore = bestScore;
        }
    }
}