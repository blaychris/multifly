using Godot;

public partial class GameManager : Node
{
    private int currentStage;
    private int totalFliesToDestroy;
    private int fliesDestroyed;
    private int playerHearts;
    private bool isGameOver;

    public override void _Ready()
    {
        currentStage = 0;
        playerHearts = 1; // Player starts with 1 heart
        isGameOver = false;
        LoadStage(currentStage);
    }

    private void LoadStage(int stageIndex)
    {
        // Load stage data and initialize flies
        // This is where you would read from the stage JSON file
        // For now, let's assume we have a fixed number of flies
        totalFliesToDestroy = 5; // Example value
        fliesDestroyed = 0;
    }

    public void SelectFly(int flyMultiplier1, int flyMultiplier2)
    {
        // Logic to select a fly and activate the numpad
        // Highlight the selected fly
    }

    public void InputNumber(int number)
    {
        // Check if the input number is correct
        // If correct, destroy the fly and spawn new ones
        // If incorrect, handle player heart deduction
    }

    private void OnFlyReachedPlayer()
    {
        playerHearts--;
        if (playerHearts <= 0)
        {
            GameOver();
        }
    }

    private void GameOver()
    {
        isGameOver = true;
        // Transition to Game Over scene
    }

    public void FlyDestroyed()
    {
        fliesDestroyed++;
        if (fliesDestroyed >= totalFliesToDestroy)
        {
            StageClear();
        }
    }

    private void StageClear()
    {
        // Transition to Stage Clear scene
    }
}