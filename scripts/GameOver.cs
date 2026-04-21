
using Godot;
using System;

public partial class GameOver : Control
{

    private Button? retryButton;
    private Button? mainMenuButton;
    private Label? totalScoreLabel;

    public override void _Ready()
    {
        // Lower BGM volume to 50% for game over
        var audioManager = GetNodeOrNull<AudioManager>("/root/AudioManager");
        audioManager?.SetBackgroundMusicVolume(0.25f);

        retryButton = GetNode<Button>("RetryButton");
        mainMenuButton = GetNode<Button>("MainMenuButton");
        totalScoreLabel = GetNodeOrNull<Label>("TotalScoreLabel");

        // Show total score only in campaign mode, using accumulated campaign score
        var gameState = GetNodeOrNull<GameState>("/root/GameState");
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

        retryButton.Pressed += OnRetryPressed;
        mainMenuButton.Pressed += OnExitPressed;
    }

    private void OnRetryPressed()
    {
            var audioManager = GetNodeOrNull<AudioManager>("/root/AudioManager");
            audioManager?.SetBackgroundMusicVolume(1.0f);
            //audioManager?.StopBackgroundMusic();
            GetTree().ChangeSceneToFile("res://scenes/Gameplay.tscn");
    }

    private void OnExitPressed()
    {
        var audioManager = GetNodeOrNull<AudioManager>("/root/AudioManager");
        audioManager?.SetBackgroundMusicVolume(1.0f);
        audioManager?.StopBackgroundMusic();
        // If in campaign, submit score to Google Play
        var gameState = GetNodeOrNull<GameState>("/root/GameState");
        if (gameState != null && gameState.IsCampaignActive)
        {
            int totalScore = gameState.GetCurrentCampaignScore();
            gameState.SubmitScoreToGooglePlay(totalScore);
        }
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }
}