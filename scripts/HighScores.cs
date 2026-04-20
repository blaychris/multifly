using Godot;
using System.Collections.Generic;

public partial class HighScores : Node
{
    private List<int> highScores = new();

    public override void _Ready()
    {
        highScores = new List<int>();
        LoadHighScores();
    }

    public void AddScore(int score)
    {
        highScores.Add(score);
        highScores.Sort((a, b) => b.CompareTo(a)); // Sort in descending order
        if (highScores.Count > 10) // Keep only top 10 scores
        {
            highScores.RemoveAt(highScores.Count - 1);
        }
        SaveHighScores();
    }

    public List<int> GetHighScores()
    {
        return highScores;
    }

    private void LoadHighScores()
    {
        // Load high scores from a file or database
        // This is a placeholder for actual loading logic
    }

    private void SaveHighScores()
    {
        // Save high scores to a file or database
        // This is a placeholder for actual saving logic
    }
}