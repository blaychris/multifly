using Godot;

public partial class HeartDisplay : Control
{
    private int hearts = 1; // Player starts with 1 heart
    private Label? heartLabel;

    public override void _Ready()
    {
        heartLabel = GetNode<Label>("HeartLabel");
        UpdateHeartDisplay();
    }

    public void LoseHeart()
    {
        if (hearts > 0)
        {
            hearts--;
            UpdateHeartDisplay();
        }
    }

    public void GainHeart()
    {
        hearts++;
        UpdateHeartDisplay();
    }

    public void SetHeartCount(int heartCount)
    {
        hearts = Mathf.Max(0, heartCount);
        UpdateHeartDisplay();
    }

    private void UpdateHeartDisplay()
    {
        if (heartLabel != null)
        {
            heartLabel.Text = "Hearts: " + hearts;
        }
    }

    public int GetHeartCount()
    {
        return hearts;
    }
}