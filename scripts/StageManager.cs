using Godot;
using System.Collections.Generic;

public partial class StageManager : Node
{
    private int currentStage;
    private int fliesToDestroy;
    private int fliesDestroyed;
    private int playerHearts;
    private List<Fly> activeFlies = new();

    public override void _Ready()
    {
        currentStage = 0;
        fliesDestroyed = 0;
        playerHearts = 1; // Player starts with 1 heart
        LoadStage(currentStage);
    }

    private void LoadStage(int stageIndex)
    {
        // Load stage data from JSON or other source
        // Initialize flies based on stage data
        fliesToDestroy = GetFliesCountForStage(stageIndex);
        SpawnFlies(fliesToDestroy);
    }

    private int GetFliesCountForStage(int stageIndex)
    {
        // Logic to retrieve the number of flies for the given stage
        // This could involve reading from a JSON file
        return 5; // Placeholder value
    }

    private void SpawnFlies(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Fly newFly = GD.Load<PackedScene>("res://scenes/actors/Fly.tscn").Instantiate<Fly>();
            newFly.Position = GetFlyStartPosition();
            newFly.ReachedPlayer += OnFlyReachedPlayer;
            newFly.StartFly();
            activeFlies.Add(newFly);
            GetParent()?.AddChild(newFly);
        }
    }

    private Vector2 GetFlyStartPosition()
    {
        // Logic to determine the starting position of the fly
        return new Vector2(0, 0); // Placeholder value
    }

    private void OnFlyReachedPlayer(Fly fly)
    {
        playerHearts--;
        if (playerHearts <= 0)
        {
            GameOver();
        }
    }

    public void SelectFly(Fly selectedFly)
    {
        // Logic to highlight the selected fly and enable numpad input
    }

    public void InputNumber(int number)
    {
        // Logic to check if the input number matches the selected fly's product
        // If correct, destroy the fly and spawn new ones
    }

    private void GameOver()
    {
        // Logic to handle game over state
    }

    private void ClearStage()
    {
        // Logic to handle stage completion
        if (fliesDestroyed >= fliesToDestroy)
        {
            // Proceed to stage clear
        }
    }
}