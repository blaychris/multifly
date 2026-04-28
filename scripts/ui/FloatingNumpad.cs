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
    private Button? keyboardSupportButton;
    private TextureRect? autopickIconRect;
    private TextureRect? keyboardIconRect;
    private Texture2D? autopickIcon;
    private Texture2D? keyboardIcon;
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

        autopickIcon = GD.Load<Texture2D>("res://scenes/ui/autoicon.png");
        keyboardIcon = GD.Load<Texture2D>("res://scenes/ui/keyboardicon.png");

        autopickButton = GetNode<Button>("Numpad/AutopickButton");
        if (autopickButton != null)
        {
            // Keep the visual autopick state, but prevent it from being clicked in the floating numpad.
            autopickButton.Disabled = true;
            autopickButton.FocusMode = FocusModeEnum.None;
            autopickButton.Modulate = Colors.White;
            isAutopickRed = gameState?.IsAutopickEnabled ?? false;
            autopickIconRect = CreateOutlinedIconRect(autopickButton, autopickIcon);
            UpdateAutopickButtonIcon();
        }

        keyboardSupportButton = GetNode<Button>("Numpad/KeyboardSupportButton");
        if (keyboardSupportButton != null)
        {
            keyboardSupportButton.Disabled = true;
            keyboardSupportButton.FocusMode = FocusModeEnum.None;
            keyboardSupportButton.Modulate = Colors.White;
            keyboardIconRect = CreateOutlinedIconRect(keyboardSupportButton, keyboardIcon);
            UpdateKeyboardIconVisibility();
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

    public void SimulateDigitInput(int number)
    {
        if (!isFlySelected)
        {
            return;
        }

        if (number < 0 || number > 9)
        {
            return;
        }

        currentInput += number.ToString();
        EmitSignal(SignalName.InputChanged, currentInput);
    }

    public void SimulateBackspaceInput()
    {
        if (!isFlySelected || currentInput.Length == 0)
        {
            return;
        }

        currentInput = currentInput[..^1];
        EmitSignal(SignalName.BackspaceUsed);
        EmitSignal(SignalName.InputChanged, currentInput);
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

        autopickButton.Modulate = Colors.White;
        UpdateAutopickButtonIcon();
        EmitSignal(SignalName.AutopickToggled, isAutopickRed);
    }

    private TextureRect CreateOutlinedIconRect(Button parentButton, Texture2D? texture)
    {
        TextureRect iconRect = new TextureRect
        {
            StretchMode = TextureRect.StretchModeEnum.Scale,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = MouseFilterEnum.Ignore,
            Texture = texture
        };

        Vector2 iconSize = new Vector2(30, 30);
        iconRect.AnchorLeft = 0;
        iconRect.AnchorTop = 0;
        iconRect.AnchorRight = 0;
        iconRect.AnchorBottom = 0;
        iconRect.SizeFlagsHorizontal = 0;
        iconRect.SizeFlagsVertical = 0;
        iconRect.CustomMinimumSize = iconSize;
        iconRect.Size = iconSize;
        iconRect.Position = (parentButton.Size - iconSize) * 0.5f;

        var shaderCode = @"shader_type canvas_item;
uniform float outline_thickness = 3.0;
uniform vec4 outline_color : hint_color = vec4(1.0);
uniform float alpha_threshold = 0.1;

void fragment() {
    vec2 uv = UV;
    vec4 tex = texture(TEXTURE, uv);
    if (tex.a > alpha_threshold) {
        COLOR = tex;
        return;
    }
    vec2 pixel = outline_thickness * TEXTURE_PIXEL_SIZE;
    for (int x = -1; x <= 1; x++) {
        for (int y = -1; y <= 1; y++) {
            if (x == 0 && y == 0) continue;
            vec2 sampleUV = uv + vec2(x, y) * pixel;
            if (texture(TEXTURE, sampleUV).a > alpha_threshold) {
                COLOR = outline_color;
                return;
            }
        }
    }
    COLOR = vec4(0.0);
}";

        iconRect.Material = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = shaderCode
            }
        };

        parentButton.AddChild(iconRect);
        return iconRect;
    }

    private void UpdateOutlinedIconRectLayout(Button? parentButton, TextureRect? iconRect)
    {
        if (parentButton == null || iconRect == null)
        {
            return;
        }

        Vector2 iconSize = new Vector2(30, 30);
        iconRect.Size = iconSize;
        iconRect.CustomMinimumSize = iconSize;
        iconRect.Position = (parentButton.Size - iconSize) * 0.5f;
    }

    private void UpdateKeyboardIconVisibility()
    {
        if (keyboardIconRect == null)
        {
            return;
        }

        bool externalKeyboardEnabled = gameState?.IsExternalKeyboardEnabled ?? false;
        keyboardIconRect.Visible = externalKeyboardEnabled;
    }

    private void UpdateAutopickButtonIcon()
    {
        if (autopickIconRect == null)
        {
            return;
        }

        autopickIconRect.Texture = isAutopickRed ? autopickIcon : null;
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
        UpdateOutlinedIconRectLayout(autopickButton, autopickIconRect);
        UpdateOutlinedIconRectLayout(keyboardSupportButton, keyboardIconRect);
        UpdateKeyboardIconVisibility();
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