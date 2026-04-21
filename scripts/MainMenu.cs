using Godot;

public partial class MainMenu : Control
{
    private Button? startButton;
    private Button? levelSelectButton;
    private Button? highScoresButton;
    private Button? customizeButton;
    private Button? exitButton;

    public override void _Ready()
    {
        startButton = GetNode<Button>("Control/StartButton");
        levelSelectButton = GetNode<Button>("Control/LevelSelectButton");
        highScoresButton = GetNode<Button>("Control/HighScoresButton");
        customizeButton = GetNode<Button>("Control/CustomizeButton");
        exitButton = GetNode<Button>("Control/ExitButton");

        startButton.Pressed += OnCampaignButtonPressed;
        levelSelectButton.Pressed += OnLevelSelectButtonPressed;
        highScoresButton.Pressed += OnHighScoresButtonPressed;
        customizeButton.Pressed += OnCustomizeButtonPressed;
        exitButton.Pressed += OnExitButtonPressed;
    }

    private void OnCampaignButtonPressed()
    {
        // Start campaign: always at level 1, reset campaign scores, enable campaign mode
        var gameState = GetNodeOrNull<GameState>("/root/GameState");
        if (gameState != null)
        {
            gameState.CurrentLevel = 1;
            gameState.ResetCampaignScores();
            gameState.StartCampaign(1);
        }
        GetTree().ChangeSceneToFile("res://scenes/Gameplay.tscn");
    }

    private void OnLevelSelectButtonPressed()
    {
        // Level select disables campaign mode
        var gameState = GetNodeOrNull<GameState>("/root/GameState");
        if (gameState != null)
        {
            gameState.IsCampaignActive = false;
            gameState.ResetCampaignScores();
        }
        GetTree().ChangeSceneToFile("res://scenes/LevelSelect.tscn");
    }

    private void OnHighScoresButtonPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/HighScores.tscn");
    }

    private void OnCustomizeButtonPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/Customize.tscn");
    }

    private void OnExitButtonPressed()
    {
        GetTree().Quit();
    }
}