using Godot;
using System;
using System.Collections.Generic;
#nullable enable

public partial class Customize : Node
{
    private Button? bgmToggleButton;
    private Button? autopickToggleButton;
    private Button? externalKeyboardToggleButton;
    private Button? exitButton;

    private LineEdit? playerNameText;
    private LineEdit? multiplesOfText;
    private Label? unrankedLabel;
    private AudioManager? audioManager;
    private GameState? gameState;
    private bool isMusicOn = true;
    private bool isAutopickOn = false;
    private bool isExternalKeyboardOn = false;

    public override void _Ready()
    {
        bgmToggleButton = GetNodeOrNull<Button>("BgmToggleButton");
        exitButton = GetNodeOrNull<Button>("ExitButton");
        playerNameText = GetNodeOrNull<LineEdit>("PlayerNameText");
        multiplesOfText = GetNodeOrNull<LineEdit>("MultiplesOf");
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

        if (playerNameText != null)
        {
            playerNameText.Text = gameState?.PlayerName ?? string.Empty;
            playerNameText.TextChanged += OnPlayerNameTextChanged;
        }

        unrankedLabel = GetNodeOrNull<Label>("MultiplesOf/Label2");
        if (multiplesOfText != null)
        {
            multiplesOfText.Text = gameState?.FocusNumbers ?? string.Empty;
            UpdateUnrankedLabelVisibility(multiplesOfText.Text);
            multiplesOfText.TextChanged += OnMultiplesOfTextChanged;
        }
    }

    private void OnPlayerNameTextChanged(string newText)
    {
        string updatedName = newText.Trim();
        if (gameState != null)
        {
            gameState.SetPlayerName(updatedName);
        }

        var firebaseService = GetNodeOrNull<FirebaseService>("/root/FirebaseService");
        if (firebaseService == null)
        {
            return;
        }

        string playerId = OS.GetUniqueId();
        firebaseService.UpdatePlayerName(playerId, string.IsNullOrEmpty(updatedName) ? "Player" : updatedName);
    }

    private void OnMultiplesOfTextChanged(string newText)
    {
        if (multiplesOfText == null)
        {
            return;
        }

        string filtered = FilterFocusedNumbersText(newText);
        if (filtered != newText)
        {
            multiplesOfText.Text = filtered;
        }

        UpdateUnrankedLabelVisibility(filtered);

        if (gameState != null)
        {
            gameState.SetFocusNumbers(filtered);
        }
    }

    private void UpdateUnrankedLabelVisibility(string text)
    {
        if (unrankedLabel == null)
        {
            return;
        }

        unrankedLabel.Visible = !string.IsNullOrEmpty(text.Trim());
    }

    private static string FilterFocusedNumbersText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var seenDigits = new HashSet<char>();
        var filtered = new List<char>();
        bool lastWasDigit = false;

        foreach (char ch in text)
        {
            if (ch == ' ')
            {
                continue;
            }

            if (ch >= '1' && ch <= '9')
            {
                if (!seenDigits.Contains(ch))
                {
                    filtered.Add(ch);
                    seenDigits.Add(ch);
                    lastWasDigit = true;
                }
                else
                {
                    lastWasDigit = true;
                }

                continue;
            }

            if (ch == ',' && lastWasDigit)
            {
                if (filtered.Count > 0 && filtered[^1] != ',')
                {
                    filtered.Add(',');
                }

                lastWasDigit = false;
            }
        }

        while (filtered.Count > 0 && filtered[^1] == ',')
        {
            filtered.RemoveAt(filtered.Count - 1);
        }

        return new string(filtered.ToArray());
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
