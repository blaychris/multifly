using Godot;

public partial class GameOver : Control
{
    private Button? retryButton;
    private Button? mainMenuButton;
        // Removed orphaned GameState reference

    public override void _Ready()
    {
        // Lower BGM volume to 50% for game over
        var audioManager = GetNodeOrNull<AudioManager>("/root/AudioManager");
        audioManager?.SetBackgroundMusicVolume(0.25f);
            // Removed orphaned GameState reference
        retryButton = GetNode<Button>("RetryButton");
        mainMenuButton = GetNode<Button>("MainMenuButton");

        retryButton.Pressed += OnRetryPressed;
        mainMenuButton.Pressed += OnExitPressed;
    }

    private void OnRetryPressed()
    {
            var audioManager = GetNodeOrNull<AudioManager>("/root/AudioManager");
            audioManager?.SetBackgroundMusicVolume(1.0f);
            audioManager?.StopBackgroundMusic();
            GetTree().ChangeSceneToFile("res://scenes/Gameplay.tscn");
    }

    private void OnExitPressed()
    {
        var audioManager = GetNodeOrNull<AudioManager>("/root/AudioManager");
        audioManager?.SetBackgroundMusicVolume(1.0f);
        audioManager?.StopBackgroundMusic();
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }
}