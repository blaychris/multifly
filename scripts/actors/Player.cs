using Godot;

public partial class Player : Node2D
{
    private int hearts = 1;
    private int score = 0;
    private bool isFlySelected = false;
    private Fly? selectedFly;

    public override void _Ready()
    {
        // Initialize player settings
    }

    public void SelectFly(Fly fly)
    {
        if (selectedFly != null)
        {
            selectedFly.Deselect();
        }

        selectedFly = fly;
        selectedFly.Select();
        isFlySelected = true;
    }

    public void InputNumber(int number)
    {
        if (isFlySelected && selectedFly != null)
        {
            int product = selectedFly.GetMultiplierProduct();
            if (number == product)
            {
                selectedFly.Explode();
                score += 1;
                isFlySelected = false;
                selectedFly = null;
            }
            else
            {
                hearts--;
                if (hearts <= 0)
                {
                    // Handle game over
                }
            }
        }
    }

    public int GetHearts()
    {
        return hearts;
    }

    public int GetScore()
    {
        return score;
    }
}