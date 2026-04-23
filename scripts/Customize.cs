using Godot;
#nullable enable

public partial class Customize : Node
{
    private Button? bgmToggleButton;
    private Button? exitButton;
    private AudioManager? audioManager;
    private GameState? gameState;
    private bool isMusicOn = true;

    public override void _Ready()
    {
        bgmToggleButton = GetNodeOrNull<Button>("BgmToggleButton");
        exitButton = GetNodeOrNull<Button>("ExitButton");
        audioManager = GetNodeOrNull<AudioManager>("/root/AudioManager");
        gameState = GetNodeOrNull<GameState>("/root/GameState");

        if (gameState != null)
        {
            isMusicOn = gameState.IsBackgroundMusicEnabled;
        }
        else if (audioManager != null)
        {
            isMusicOn = audioManager.IsBackgroundMusicPlaying();
        }

        if (bgmToggleButton != null)
        {
            bgmToggleButton.ToggleMode = true;
            bgmToggleButton.Pressed += OnBgmTogglePressed;
            UpdateBgmToggleText();
            bgmToggleButton.ButtonPressed = isMusicOn;
        }

        if (exitButton != null)
        {
            exitButton.Pressed += OnExitPressed;
        }
    }

    private void OnBgmTogglePressed()
    {
        if (audioManager == null)
        {
            return;
        }

        isMusicOn = !isMusicOn;
        if (gameState != null)
        {
            gameState.IsBackgroundMusicEnabled = isMusicOn;
        }

        if (!isMusicOn)
        {
            audioManager.StopBackgroundMusic();
        }

        UpdateBgmToggleText();
        if (bgmToggleButton != null)
        {
            bgmToggleButton.ButtonPressed = isMusicOn;
        }
    }

    private void UpdateBgmToggleText()
    {
        if (bgmToggleButton == null)
        {
            return;
        }

        bgmToggleButton.Text = isMusicOn ? "Background Music: On" : "Background Music: Off";
    }

    private void OnExitPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }
}
