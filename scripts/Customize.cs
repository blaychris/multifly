using Godot;
#nullable enable

public partial class Customize : Node
{
    private Button? bgmToggleButton;
    private Button? autopickToggleButton;
    private Button? externalKeyboardToggleButton;
    private Button? exitButton;
    private AudioManager? audioManager;
    private GameState? gameState;
    private bool isMusicOn = true;
    private bool isAutopickOn = false;
    private bool isExternalKeyboardOn = false;

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

        autopickToggleButton = GetNodeOrNull<Button>("AutopickToggleButton");
        if (autopickToggleButton != null)
        {
            autopickToggleButton.ToggleMode = true;
            autopickToggleButton.Pressed += OnAutopickTogglePressed;
            isAutopickOn = gameState?.IsAutopickEnabled ?? false;
            UpdateAutopickToggleText();
            autopickToggleButton.ButtonPressed = isAutopickOn;
        }

        externalKeyboardToggleButton = GetNodeOrNull<Button>("ExternalKeyboardToggleButton");
        if (externalKeyboardToggleButton != null)
        {
            externalKeyboardToggleButton.ToggleMode = true;
            externalKeyboardToggleButton.Pressed += OnExternalKeyboardTogglePressed;
            isExternalKeyboardOn = gameState?.IsExternalKeyboardEnabled ?? false;
            UpdateExternalKeyboardToggleText();
            externalKeyboardToggleButton.ButtonPressed = isExternalKeyboardOn;
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

    private void UpdateAutopickToggleText()
    {
        if (autopickToggleButton == null)
        {
            return;
        }

        autopickToggleButton.Text = isAutopickOn ? "Autopick: On" : "Autopick: Off";
    }

    private void UpdateExternalKeyboardToggleText()
    {
        if (externalKeyboardToggleButton == null)
        {
            return;
        }

        externalKeyboardToggleButton.Text = isExternalKeyboardOn ? "External Keyboard: On" : "External Keyboard: Off";
    }

    private void OnAutopickTogglePressed()
    {
        isAutopickOn = !isAutopickOn;
        if (gameState != null)
        {
            gameState.IsAutopickEnabled = isAutopickOn;
        }

        UpdateAutopickToggleText();
        if (autopickToggleButton != null)
        {
            autopickToggleButton.ButtonPressed = isAutopickOn;
        }
    }

    private void OnExternalKeyboardTogglePressed()
    {
        isExternalKeyboardOn = !isExternalKeyboardOn;
        if (gameState != null)
        {
            gameState.IsExternalKeyboardEnabled = isExternalKeyboardOn;
        }

        UpdateExternalKeyboardToggleText();
        if (externalKeyboardToggleButton != null)
        {
            externalKeyboardToggleButton.ButtonPressed = isExternalKeyboardOn;
        }
    }

    private void OnExitPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }
}
