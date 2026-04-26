using Godot;
#nullable enable

public partial class FloatingNumpad : Control
{
    [Signal]
    public delegate void InputChangedEventHandler(string currentInput);

    [Signal]
    public delegate void LayoutChangedEventHandler(float numpadTopY);

    [Signal]
    public delegate void BackspaceUsedEventHandler();

    [Signal]
    public delegate void AutopickToggledEventHandler(bool enabled);

    private string currentInput = string.Empty;
    private bool isFlySelected = false;
    private Control? numpadPanel;
    private Control? centerTargetButton;
    private Viewport? viewport;
    private Button? autopickButton;
    private bool isAutopickRed = false;
    private GameState? gameState;

    public bool IsAutopickEnabled => isAutopickRed;

    public override void _Ready()
    {
        numpadPanel = GetNode<Control>("Numpad");
        centerTargetButton = GetNode<Control>("Numpad/Button5");
        viewport = GetViewport();
        gameState = GetNodeOrNull<GameState>("/root/GameState");
        if (viewport != null)
        {
            viewport.SizeChanged += OnViewportSizeChanged;
        }

        SetNumpadVisible(true);
        UpdateViewportLayout();
        for (int i = 0; i <= 9; i++)
        {
            int number = i;
            Button numpadButton = GetNode<Button>("Numpad/Button" + number);
            numpadButton.Pressed += () => OnNumpadButtonPressed(number);
        }

        autopickButton = GetNode<Button>("Numpad/AutopickButton");
        if (autopickButton != null)
        {
            autopickButton.Pressed += ToggleAutopickButtonColor;
            isAutopickRed = gameState?.IsAutopickEnabled ?? false;
            autopickButton.Modulate = isAutopickRed ? Colors.Red : Colors.White;
        }

        Button backButton = GetNode<Button>("Numpad/BackButton");
        backButton.Pressed += OnBackButtonPressed;
        CallDeferred(nameof(UpdateViewportLayout));
    }

    public override void _ExitTree()
    {
        if (viewport != null)
        {
            viewport.SizeChanged -= OnViewportSizeChanged;
        }
    }

    public float GetNumpadTopY()
    {
        return numpadPanel?.GetGlobalRect().Position.Y ?? GetGlobalRect().Position.Y;
    }

    public Vector2 GetFlyTargetPosition()
    {
        if (centerTargetButton != null)
        {
            Rect2 targetRect = centerTargetButton.GetGlobalRect();
            return targetRect.Position + (targetRect.Size * 0.5f);
        }

        if (numpadPanel != null)
        {
            Rect2 numpadRect = numpadPanel.GetGlobalRect();
            return numpadRect.Position + (numpadRect.Size * 0.5f);
        }

        Rect2 rootRect = GetGlobalRect();
        return rootRect.Position + (rootRect.Size * 0.5f);
    }

    public void BeginInput()
    {
        isFlySelected = true;
        ClearInput();
    }

    public void EndInput()
    {
        isFlySelected = false;
        ClearInput();
    }

    public void ResetCurrentInput()
    {
        if (!isFlySelected)
        {
            return;
        }

        ClearInput();
    }

    private void OnNumpadButtonPressed(int number)
    {
        if (!isFlySelected)
        {
            return;
        }

        currentInput += number.ToString();
        EmitSignal(SignalName.InputChanged, currentInput);
    }

    private void ClearInput()
    {
        currentInput = string.Empty;
        EmitSignal(SignalName.InputChanged, currentInput);
    }

    private void OnBackButtonPressed()
    {
        if (!isFlySelected)
        {
            return;
        }

        if (currentInput.Length == 0)
        {
            return;
        }

        currentInput = currentInput[..^1];
        EmitSignal(SignalName.BackspaceUsed);
        EmitSignal(SignalName.InputChanged, currentInput);
    }

    private void ToggleAutopickButtonColor()
    {
        if (autopickButton == null)
        {
            return;
        }

        isAutopickRed = !isAutopickRed;
        if (gameState != null)
        {
            gameState.IsAutopickEnabled = isAutopickRed;
        }

        autopickButton.Modulate = isAutopickRed ? Colors.Red : Colors.White;
        EmitSignal(SignalName.AutopickToggled, isAutopickRed);
    }

    private void EmitLayoutChangedSignal()
    {
        EmitSignal(SignalName.LayoutChanged, GetNumpadTopY());
    }

    private void OnViewportSizeChanged()
    {
        UpdateViewportLayout();
    }

    private void UpdateViewportLayout()
    {
        Vector2 viewportSize = GetViewportRect().Size;
        Position = Vector2.Zero;
        Size = viewportSize;
        EmitLayoutChangedSignal();
    }

    private void SetNumpadVisible(bool isVisible)
    {
        if (numpadPanel != null)
        {
            numpadPanel.Visible = isVisible;
        }
    }
}