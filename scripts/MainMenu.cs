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

        startButton.Pressed += OnStartButtonPressed;
        levelSelectButton.Pressed += OnLevelSelectButtonPressed;
        highScoresButton.Pressed += OnHighScoresButtonPressed;
        customizeButton.Pressed += OnCustomizeButtonPressed;
        exitButton.Pressed += OnExitButtonPressed;
    }

    private void OnStartButtonPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/LevelSelect.tscn");
    }

    private void OnLevelSelectButtonPressed()
    {
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