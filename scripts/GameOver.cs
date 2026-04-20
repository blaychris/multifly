using Godot;

public partial class GameOver : Control
{
    private Button? retryButton;
    private Button? mainMenuButton;
        // Removed orphaned GameState reference

    public override void _Ready()
    {
            // Removed orphaned GameState reference
        retryButton = GetNode<Button>("RetryButton");
        mainMenuButton = GetNode<Button>("MainMenuButton");

        retryButton.Pressed += OnRetryPressed;
        mainMenuButton.Pressed += OnExitPressed;
    }

    private void OnRetryPressed()
    {
                // Stop background music before retrying
                var audioManager = GetNodeOrNull<AudioManager>("/root/AudioManager");
                audioManager?.StopBackgroundMusic();
                GetTree().ChangeSceneToFile("res://scenes/Gameplay.tscn");
    }

    private void OnExitPressed()
    {
        // Stop background music before exiting to main menu
        var audioManager = GetNodeOrNull<AudioManager>("/root/AudioManager");
        audioManager?.StopBackgroundMusic();
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }
}