using Godot;
#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class HighScores : Control
{
    private VBoxContainer? scoreRows;
    private HBoxContainer? scoreTemplate;
    private ScrollContainer? scoreScroll;
    private Button? backButton;

    public override async void _Ready()
    {
        scoreRows = GetNodeOrNull<VBoxContainer>("HighScoreList/ScoreScroll/ScoreRows");
        scoreScroll = GetNodeOrNull<ScrollContainer>("HighScoreList/ScoreScroll");
        scoreTemplate = GetNodeOrNull<HBoxContainer>("HighScoreList/ScoreEntry");
        backButton = GetNodeOrNull<Button>("HighScoreList/BackButton");

        scoreTemplate?.SetVisible(false);
        if (backButton != null)
        {
            backButton.Pressed += OnBackPressed;
        }

        UpdateScoreScrollSize();
        await LoadHighScoresAsync();
    }

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationResized)
        {
            UpdateScoreScrollSize();
        }
    }

    private void UpdateScoreScrollSize()
    {
        if (scoreScroll == null)
            return;

        var viewportRect = GetViewport().GetVisibleRect();
        float height = viewportRect.Size.Y * 0.6f;
        float minHeight = 220f;
        float maxHeight = viewportRect.Size.Y - 240f;
        scoreScroll.CustomMinimumSize = new Vector2(0, Mathf.Clamp(height, minHeight, maxHeight));
    }

    private async Task LoadHighScoresAsync()
    {
        if (scoreRows == null || scoreTemplate == null)
            return;

        GD.Print("HighScores: Starting leaderboard fetch...");
        ClearScoreRows();

        var firebaseService = GetNodeOrNull<FirebaseService>("/root/FirebaseService");
        if (firebaseService != null)
        {
            GD.Print("HighScores: FirebaseService found. Fetching leaderboard entries...");
            var entries = await firebaseService.FetchLeaderboardAsync();
            GD.Print($"HighScores: FetchLeaderboardAsync returned {entries.Count} entries.");

            if (entries.Count == 0)
            {
                var placeholderText = "No leaderboard entries found.";
                if (firebaseService.LastFetchFailed)
                {
                    placeholderText = firebaseService.LastFetchErrorMessage;
                    if (!string.IsNullOrWhiteSpace(firebaseService.LastFetchDebugInfo))
                    {
                        placeholderText += "\n\n" + firebaseService.LastFetchDebugInfo;
                    }
                }

                GD.Print($"HighScores: {placeholderText}");
                AddPlaceholderRow(placeholderText);
                return;
            }

            var displayEntries = GetDisplayEntries(entries);
            int rank = entries.IndexOf(displayEntries[0]) + 1;
            foreach (var entry in displayEntries)
            {
                AddScoreRow(rank, entry);
                rank++;
            }

            GD.Print($"HighScores: Displayed {displayEntries.Count} leaderboard rows.");
            return;
        }

        GD.Print("HighScores: FirebaseService not available. Cannot fetch leaderboard.");
        AddPlaceholderRow("FirebaseService not available.");
    }

    private void AddScoreRow(int rank, LeaderboardEntry entry)
    {
        if (scoreRows == null)
            return;

        GD.Print($"HighScores: Adding row #{rank} for {entry.PlayerName} with score {entry.Score}.");

        bool isCurrentPlayer = entry.PlayerId == OS.GetUniqueId();

        var rowContent = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.Fill,
            SizeFlagsVertical = Control.SizeFlags.Fill,
            CustomMinimumSize = new Vector2(0, 40)
        };

        Control row = rowContent;
        if (isCurrentPlayer)
        {
            var backgroundRow = new PanelContainer
            {
                Name = $"ScoreRow{rank}",
                SizeFlagsHorizontal = Control.SizeFlags.Fill,
                SizeFlagsVertical = Control.SizeFlags.Fill,
                CustomMinimumSize = new Vector2(0, 40)
            };

            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(1f, 0.4f, 0.4f, 0.7f);
            backgroundRow.AddThemeStyleboxOverride("panel", styleBox);

            backgroundRow.AddChild(rowContent);
            row = backgroundRow;
        }
        else
        {
            rowContent.Name = $"ScoreRow{rank}";
        }

        string displayName = isCurrentPlayer ? "You" : entry.PlayerName;

        var nameLabel = new Label
        {
            Text = $"{rank}. {displayName} (Stage {entry.Stage})",
            SizeFlagsHorizontal = Control.SizeFlags.Fill,
            SizeFlagsVertical = Control.SizeFlags.Fill,
            CustomMinimumSize = new Vector2(0, 40),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var scoreLabel = new Label
        {
            Text = $"{entry.Score:N0}",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.Fill,
            CustomMinimumSize = new Vector2(0, 40),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        rowContent.AddChild(nameLabel);
        rowContent.AddChild(scoreLabel);
        scoreRows.AddChild(row);
        AddSeparatorLine();
    }

    private void AddSeparatorLine()
    {
        if (scoreRows == null)
            return;

        var separator = new ColorRect();
        separator.Name = $"Separator{scoreRows.GetChildCount()}";
        separator.Color = new Color(1, 1, 1, 0.2f);
        separator.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        separator.CustomMinimumSize = new Vector2(0, 2);
        scoreRows.AddChild(separator);
    }

    private void AddPlaceholderRow(string text)
    {
        if (scoreRows == null)
            return;

        var row = new HBoxContainer();
        row.Name = "ScoreRowPlaceholder";
        row.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        row.SizeFlagsVertical = Control.SizeFlags.Fill;
        row.CustomMinimumSize = new Vector2(0, 40);

        var label = new Label();
        label.Text = text;
        label.AutowrapMode = TextServer.AutowrapMode.Word;
        label.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        label.SizeFlagsVertical = Control.SizeFlags.Fill;
        label.CustomMinimumSize = new Vector2(0, 40);

        row.AddChild(label);
        scoreRows.AddChild(row);
    }

    private void ClearScoreRows()
    {
        if (scoreRows == null)
            return;

        foreach (var child in new List<Node>(scoreRows.GetChildren()))
        {
            child.QueueFree();
        }
    }

    private List<LeaderboardEntry> GetDisplayEntries(List<LeaderboardEntry> entries)
    {
        const int windowSize = 11;
        const int halfWindow = 5;
        int count = Math.Min(entries.Count, windowSize);
        int userIndex = entries.FindIndex(e => e.PlayerId == OS.GetUniqueId());
        int start = 0;

        if (userIndex >= 0)
        {
            start = Math.Clamp(userIndex - halfWindow, 0, Math.Max(0, entries.Count - count));
        }

        return entries.GetRange(start, count);
    }

    private void OnBackPressed()
    {
        GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
    }
}
