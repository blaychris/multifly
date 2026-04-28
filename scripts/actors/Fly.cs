using Godot;
#nullable enable

public partial class Fly : Area2D
{
    // Velocity for bounce/collision response
    public Vector2 Velocity = Vector2.Zero;
    /// <summary>
    /// Returns the collision shape's radius and offset if available.
    /// Supports CapsuleShape2D and RectangleShape2D.
    /// </summary>
    public (float Radius, Vector2 Offset)? GetCollisionShape()
    {
        if (collisionShape?.Shape is CapsuleShape2D capsuleShape)
        {
            // Capsule: use half the height as the effective radius for overlap
            float radius = capsuleShape.Height * 0.5f;
            return (radius, collisionShape.Position);
        }
        if (collisionShape?.Shape is RectangleShape2D rectangleShape)
        {
            // Use half the diagonal as an approximate radius
            float radius = rectangleShape.Size.Length() * 0.5f;
            return (radius, collisionShape.Position);
        }
        if (collisionShape?.Shape is CircleShape2D circleShape)
        {
            return (circleShape.Radius, collisionShape.Position);
        }
        return null;
    }
    private static readonly RandomNumberGenerator Rng = new();
    private enum FlyMovementMode
    {
        ChaseTarget,
        BonusEscape
    }

    private const float ExplosionDurationSeconds = 0.35f;
    private const float AnswerDissolveDurationSeconds = 0.5f;
    private const float AnswerDissolveEndScale = 8.0f;
    private const float BonusHorizontalSpeedMultiplier = 2.0f;
    private const float BonusHorizontalTargetThreshold = 10.0f;
    private const int ExplosionBlobCount = 12;
    private const float SpawnDurationSeconds = 0.5f;
    private const int SpawnBlobCount = 10;

    [Signal]
    public delegate void ReachedPlayerEventHandler(Fly fly);

    [Signal]
    public delegate void FlyDestroyedEventHandler(Fly fly);

    [Signal]
    public delegate void FlySelectedEventHandler(Fly fly);

    [Export]
    public int MultiplierLeft { get; set; }

    [Export]
    public int MultiplierRight { get; set; }

    [Export]
    public float MoveSpeed { get; set; } = 10.0f;

    [Export]
    public float ArrivalDistance { get; set; } = 24.0f;

    [Export]
    public float ReachBoundaryY { get; set; } = float.MaxValue;

    [Export]
    public int MinimumMultiplier { get; set; } = 1;

    [Export]
    public int MaximumMultiplier { get; set; } = 9;

    [Export]
    public float WrongAnswerAdvanceSpeed { get; set; } = 140.0f;

    private bool isSelected = false;
    private bool isMoving;
    private bool isExploding;
    private bool isSpawning;
    private float remainingPenaltyDistance;
    private const string OutlineEnabledParameter = "enabled";
    private AnimationPlayer? animationPlayer;
    private CollisionShape2D? collisionShape;
    private AnimatedSprite2D? sprite;
    private ShaderMaterial? outlineMaterial;
    private Label? leftEyeLabel;
    private Label? rightEyeLabel;
    private Label? answerLabel;
    private Node2D? playerTarget;
    private Vector2? explicitTargetPosition;
    private Rect2? movementBounds;
    private Vector2 answerLabelCenterPosition;
    private Vector2 baseScale = Vector2.One;
    private FlyMovementMode movementMode = FlyMovementMode.ChaseTarget;
    private float bonusTargetX;
    private const float TiltMaxRadians = 0.48f; // ~10 degrees
    private const float TiltLerpSpeed = 12.0f;

    public int LeftEye => MultiplierLeft;
    public int RightEye => MultiplierRight;

    public override void _Ready()
    {
        baseScale = Scale;
        InputPickable = true;
        animationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
        collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (sprite != null)
        {
            sprite.Animation = "default";
            sprite.Play("default");
            leftEyeLabel = sprite.GetNodeOrNull<Label>("l_eye");
            rightEyeLabel = sprite.GetNodeOrNull<Label>("r_eye");
        }
        outlineMaterial = sprite?.Material as ShaderMaterial;
        answerLabel = GetNodeOrNull<Label>("AnswerLabel");
        if (answerLabel != null)
        {
            answerLabelCenterPosition = answerLabel.Position + (answerLabel.Size * 0.5f);
            UpdateAnswerLabelLayout();
        }

        playerTarget = GetParent()?.GetNodeOrNull<Node2D>("Player");
        AreaEntered += OnAreaEntered;
        ResetFly();
    }

    private void OnAreaEntered(Area2D otherArea)
    {
        if (otherArea == this)
        {
            return;
        }

        if (otherArea is Fly otherFly)
        {
            GD.Print($"Fly collision: {Name} hit {otherFly.Name}");
        }
    }

    public override void _InputEvent(Viewport viewport, InputEvent @event, int shapeIdx)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } ||
            @event is InputEventScreenTouch { Pressed: true })
        {
            EmitSignal(SignalName.FlySelected, this);
            viewport.SetInputAsHandled();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!isMoving)
        {
            return;
        }

        // Apply velocity for bounce/collision
        if (Velocity.LengthSquared() > 0.0001f)
        {
            float frameDelta = (float)delta;
            Vector2 nextPosition = GlobalPosition + Velocity * frameDelta;
            Vector2 clampedPosition = ClampGlobalPositionToMovementBounds(nextPosition);
            GlobalPosition = clampedPosition;

            if (clampedPosition != nextPosition)
            {
                // Bounce off the boundary instead of stopping.
                UpdateSpriteTilt(frameDelta, Velocity.Normalized());

                if (!Mathf.IsEqualApprox(clampedPosition.X, nextPosition.X))
                {
                    Velocity = new Vector2(-Velocity.X * 0.6f, Velocity.Y);
                }
                if (!Mathf.IsEqualApprox(clampedPosition.Y, nextPosition.Y))
                {
                    Velocity = new Vector2(Velocity.X, -Velocity.Y * 0.6f);
                }

                // If the remaining velocity is small, stop it to avoid jitter.
                if (Velocity.Length() < 2.0f)
                    Velocity = Vector2.Zero;
            }
            else
            {
                UpdateSpriteTilt(frameDelta, Velocity.Normalized());
                // Dampen velocity (simulate friction)
                Velocity *= 0.90f;
                if (Velocity.Length() < 2.0f)
                    Velocity = Vector2.Zero;
            }
        }
        else
        {
            UpdateSpriteTilt((float)delta, Vector2.Zero);
        }

        if (movementMode == FlyMovementMode.BonusEscape)
        {
            ProcessBonusEscape(delta);
            return;
        }

        // Only move toward target if not bouncing
        if (Velocity == Vector2.Zero)
        {
            Vector2 targetPosition;
            if (explicitTargetPosition.HasValue)
            {
                targetPosition = explicitTargetPosition.Value;
            }
            else
            {
                playerTarget ??= GetParent()?.GetNodeOrNull<Node2D>("Player");
                if (playerTarget == null)
                {
                    return;
                }

                targetPosition = playerTarget.GlobalPosition;
            }

            if (HasReachedPlayer(targetPosition))
            {
                NotifyReachedPlayer();
                return;
            }

            Vector2 toTarget = targetPosition - GlobalPosition;
            float distance = toTarget.Length();
            if (distance <= ArrivalDistance)
            {
                NotifyReachedPlayer();
                return;
            }

            float frameDelta = (float)delta;
            Vector2 moveDirection = toTarget.Normalized();
            float penaltyStep = 0.0f;
            if (remainingPenaltyDistance > 0.0f)
            {
                penaltyStep = Mathf.Min(WrongAnswerAdvanceSpeed * frameDelta, remainingPenaltyDistance);
                remainingPenaltyDistance -= penaltyStep;
            }

            float totalStep = (MoveSpeed * frameDelta) + penaltyStep;
            GlobalPosition = ClampGlobalPositionToMovementBounds(GlobalPosition + (moveDirection * totalStep));
            UpdateSpriteTilt(frameDelta, moveDirection);

            if (HasReachedPlayer(targetPosition))
            {
                NotifyReachedPlayer();
            }
        }
    }

    public void ResetFly()
    {
        GD.Print("Fly reset");
        isSelected = false;
        isMoving = false;
        isExploding = false;
        isSpawning = false;
        movementMode = FlyMovementMode.ChaseTarget;
        remainingPenaltyDistance = 0.0f;
        GenerateMultipliers();
        SetTypedInput(string.Empty);
        Visible = true;
        InputPickable = true;
        SetVisualAlpha(1.0f);
        Scale = baseScale;
        if (sprite != null)
        {
            sprite.Rotation = 0.0f;
        }
        collisionShape?.SetDeferred("disabled", false);
        UpdateFlyAppearance();
    }

    private void GenerateMultipliers()
    {
        int minValue = Mathf.Min(MinimumMultiplier, MaximumMultiplier);
        int maxValue = Mathf.Max(MinimumMultiplier, MaximumMultiplier);

        MultiplierLeft = Rng.RandiRange(minValue, maxValue);
        MultiplierRight = Rng.RandiRange(minValue, maxValue);
        UpdateMultiplierDisplay();
    }

    private void UpdateMultiplierDisplay()
    {
        if (leftEyeLabel != null)
        {
            leftEyeLabel.Text = MultiplierLeft.ToString();
        }

        if (rightEyeLabel != null)
        {
            rightEyeLabel.Text = MultiplierRight.ToString();
        }
    }

    public void SetMultipliers(int leftMultiplier, int rightMultiplier)
    {
        MultiplierLeft = leftMultiplier;
        MultiplierRight = rightMultiplier;
        UpdateMultiplierDisplay();
    }

    public void SetReachBoundaryY(float reachBoundaryY)
    {
        ReachBoundaryY = reachBoundaryY;
    }

    public void SetTargetPosition(Vector2 targetPosition)
    {
        explicitTargetPosition = targetPosition;
    }

    public void StartBonusEscape()
    {
        movementMode = FlyMovementMode.BonusEscape;
        isMoving = true;
        remainingPenaltyDistance = 0.0f;
        ChooseNextBonusTargetX();
    }

    public void SetMovementBounds(Rect2 bounds)
    {
        movementBounds = bounds;
        ClampInsideMovementBounds();
    }

    public void ClampInsideMovementBounds()
    {
        GlobalPosition = ClampGlobalPositionToMovementBounds(GlobalPosition);
    }

    private float GetCollisionBottomY()
    {
        if (collisionShape?.Shape is RectangleShape2D rectangleShape)
        {
            return GlobalPosition.Y + collisionShape.Position.Y + (rectangleShape.Size.Y * 0.5f);
        }

        return GlobalPosition.Y;
    }

    private bool HasReachedPlayer(Vector2 targetPosition)
    {
        return GetCollisionBottomY() >= ReachBoundaryY || GlobalPosition.DistanceTo(targetPosition) <= ArrivalDistance;
    }

    private void NotifyReachedPlayer()
    {
        isMoving = false;
        EmitSignal(SignalName.ReachedPlayer, this);
    }

    private void ProcessBonusEscape(double delta)
    {
        float frameDelta = (float)delta;
        float horizontalStep = MoveSpeed * BonusHorizontalSpeedMultiplier * frameDelta;

        if (Mathf.Abs(GlobalPosition.X - bonusTargetX) <= BonusHorizontalTargetThreshold)
        {
            ChooseNextBonusTargetX();
        }

        float nextX = Mathf.MoveToward(GlobalPosition.X, bonusTargetX, horizontalStep);
        float nextY = GlobalPosition.Y;
        GlobalPosition = ClampBonusGlobalPosition(new Vector2(nextX, nextY));

        if (Mathf.Abs(GlobalPosition.X - bonusTargetX) <= BonusHorizontalTargetThreshold)
        {
            ChooseNextBonusTargetX();
        }
    }

    private void ChooseNextBonusTargetX()
    {
        if (!movementBounds.HasValue)
        {
            bonusTargetX = GlobalPosition.X;
            return;
        }

        Rect2 bounds = movementBounds.Value;
        Rect2 localCollisionRect = GetLocalCollisionRect();
        float minX = bounds.Position.X - localCollisionRect.Position.X;
        float maxX = bounds.End.X - (localCollisionRect.Position.X + localCollisionRect.Size.X);
        if (maxX < minX)
        {
            bonusTargetX = bounds.Position.X + (bounds.Size.X * 0.5f);
            return;
        }

        bonusTargetX = Rng.RandfRange(minX, maxX);
    }

    private Vector2 ClampBonusGlobalPosition(Vector2 globalPosition)
    {
        if (!movementBounds.HasValue)
        {
            return globalPosition;
        }

        Rect2 bounds = movementBounds.Value;
        Rect2 localCollisionRect = GetLocalCollisionRect();
        float minX = bounds.Position.X - localCollisionRect.Position.X;
        float maxX = bounds.End.X - (localCollisionRect.Position.X + localCollisionRect.Size.X);
        float minY = (bounds.Position.Y - localCollisionRect.Position.Y) - localCollisionRect.Size.Y;
        float maxY = bounds.End.Y - (localCollisionRect.Position.Y + localCollisionRect.Size.Y);

        float clampedX = ClampAxis(globalPosition.X, minX, maxX, bounds.Position.X + (bounds.Size.X * 0.5f), localCollisionRect.Position.X, localCollisionRect.Size.X);
        float clampedY = ClampAxis(globalPosition.Y, minY, maxY, bounds.Position.Y, localCollisionRect.Position.Y, localCollisionRect.Size.Y);
        return new Vector2(clampedX, clampedY);
    }

    private Vector2 ClampGlobalPositionToMovementBounds(Vector2 globalPosition)
    {
        if (!movementBounds.HasValue)
        {
            return globalPosition;
        }

        Rect2 bounds = movementBounds.Value;
        Rect2 localCollisionRect = GetLocalCollisionRect();

        float minX = bounds.Position.X - localCollisionRect.Position.X;
        float maxX = bounds.End.X - (localCollisionRect.Position.X + localCollisionRect.Size.X);
        float minY = bounds.Position.Y - localCollisionRect.Position.Y;
        float maxY = bounds.End.Y - (localCollisionRect.Position.Y + localCollisionRect.Size.Y);

        float clampedX = ClampAxis(globalPosition.X, minX, maxX, bounds.Position.X + (bounds.Size.X * 0.5f), localCollisionRect.Position.X, localCollisionRect.Size.X);
        float clampedY = ClampAxis(globalPosition.Y, minY, maxY, bounds.Position.Y + (bounds.Size.Y * 0.5f), localCollisionRect.Position.Y, localCollisionRect.Size.Y);
        return new Vector2(clampedX, clampedY);
    }

    private Rect2 GetLocalCollisionRect()
    {
        if (collisionShape?.Shape is RectangleShape2D rectangleShape)
        {
            Vector2 size = rectangleShape.Size;
            return new Rect2(collisionShape.Position - (size * 0.5f), size);
        }

        return new Rect2(Vector2.Zero, Vector2.Zero);
    }

    private void UpdateSpriteTilt(float delta, Vector2 direction)
    {
        if (sprite == null)
        {
            return;
        }

        float targetRotation = 0.0f;
        if (direction.LengthSquared() > 0.0001f)
        {
            targetRotation = Mathf.Clamp(direction.X, -1.0f, 1.0f) * TiltMaxRadians;
        }

        sprite.Rotation = Mathf.Lerp(sprite.Rotation, targetRotation, Mathf.Clamp(delta * TiltLerpSpeed, 0.0f, 1.0f));
    }

    private static float ClampAxis(float value, float minValue, float maxValue, float boundsCenter, float localOffset, float localSize)
    {
        if (maxValue < minValue)
        {
            return boundsCenter - localOffset - (localSize * 0.5f);
        }

        return Mathf.Clamp(value, minValue, maxValue);
    }

    public async void StartFly()
    {
        if (isExploding || isSpawning)
        {
            return;
        }

        isSpawning = true;
        isMoving = true;
        InputPickable = false;
        collisionShape?.SetDeferred("disabled", true);
        Visible = true;
        SetVisualAlpha(0.0f);
        Scale = baseScale * 0.82f;

        Node2D? spawnEffect = SpawnArrivalEffect();
        Tween flyTween = CreateTween();
        flyTween.SetParallel(true);
        flyTween.TweenMethod(Callable.From<float>(SetVisualAlpha), 0.0f, 1.0f, SpawnDurationSeconds)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        flyTween.TweenProperty(this, "scale", baseScale, SpawnDurationSeconds)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);

        await ToSignal(GetTree().CreateTimer(SpawnDurationSeconds), SceneTreeTimer.SignalName.Timeout);

        if (IsInstanceValid(spawnEffect))
        {
            spawnEffect.QueueFree();
        }

        SetVisualAlpha(1.0f);
        Scale = baseScale;
        collisionShape?.SetDeferred("disabled", false);
        InputPickable = true;
        isSpawning = false;
    }

    public void Select()
    {
        isSelected = true;
        UpdateFlyAppearance();
    }

    public void Deselect()
    {
        isSelected = false;
        SetTypedInput(string.Empty);
        UpdateFlyAppearance();
    }

    public void SelectFly()
    {
        Select();
    }

    public void DeselectFly()
    {
        Deselect();
    }

    private void UpdateFlyAppearance()
    {
        if (outlineMaterial != null)
        {
            outlineMaterial.SetShaderParameter(OutlineEnabledParameter, 1.0f); // Always show outline
            outlineMaterial.SetShaderParameter("line_color", isSelected ? Colors.Red : Colors.White);
        }

        if (animationPlayer == null)
        {
            return;
        }

        if (isSelected && animationPlayer.HasAnimation("Glow"))
        {
            animationPlayer.Play("Glow");
        }
        else if (animationPlayer.HasAnimation("Normal"))
        {
            animationPlayer.Play("Normal");
        }
    }

    public int GetMultiplierProduct()
    {
        return MultiplierLeft * MultiplierRight;
    }

    public bool CheckAnswer(int playerInput)
    {
        return playerInput == GetMultiplierProduct();
    }

    public int GetExpectedAnswerDigits()
    {
        return GetMultiplierProduct().ToString().Length;
    }

    public void SetTypedInput(string currentInput)
    {
        if (answerLabel == null)
        {
            return;
        }

        SetAnswerVisualState(1.0f, Vector2.One);
        answerLabel.Text = currentInput;
        UpdateAnswerLabelLayout();
        answerLabel.Visible = !string.IsNullOrEmpty(currentInput);
    }

    public void AdvancePenalty(float yOffset)
    {
        remainingPenaltyDistance += (Mathf.Max(yOffset, 0.0f) + 10.0f);
    }

    public async void Explode()
    {
        if (isExploding)
        {
            return;
        }


        isExploding = true;
        isSpawning = false;
        isMoving = false;
        isSelected = false;
        InputPickable = false;
        collisionShape?.SetDeferred("disabled", true);

        // Play splat sound effect
        var audioManager = GetNodeOrNull<Node>("/root/AudioManager") as AudioManager;
        if (audioManager != null)
        {
            var splatStream = GD.Load<AudioStream>("res://scenes/bgm/splat.ogg");
            if (splatStream != null)
                audioManager.PlaySoundEffect(splatStream);
        }

        Node2D? explosionEffect = SpawnExplosionEffect();

        if (animationPlayer != null && animationPlayer.HasAnimation("Explode"))
        {
            animationPlayer.Play("Explode");
        }

        bool shouldDissolveAnswer = answerLabel is { Visible: true } && !string.IsNullOrEmpty(answerLabel.Text);
        if (shouldDissolveAnswer)
        {
            Tween answerTween = CreateTween();
            answerTween.TweenMethod(Callable.From<float>(SetAnswerDissolveProgress), 0.0f, 1.0f, AnswerDissolveDurationSeconds)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);
        }

        HideFlyBody();
        EmitSignal(SignalName.FlyDestroyed, this);
        float destructionDurationSeconds = Mathf.Max(ExplosionDurationSeconds, shouldDissolveAnswer ? AnswerDissolveDurationSeconds : 0.0f);
        await ToSignal(GetTree().CreateTimer(destructionDurationSeconds), SceneTreeTimer.SignalName.Timeout);

        if (IsInstanceValid(explosionEffect))
        {
            explosionEffect.QueueFree();
        }

        QueueFree();
    }

    private Node2D? SpawnExplosionEffect()
    {
        Node? parent = GetParent();
        if (parent == null)
        {
            return null;
        }

        Node2D explosionRoot = new();
        parent.AddChild(explosionRoot);
        explosionRoot.GlobalPosition = GetVisualCenterGlobalPosition();

        for (int index = 0; index < ExplosionBlobCount; index++)
        {
            Color blobColor = CreateExplosionColor();
            Polygon2D blob = new()
            {
                Color = blobColor,
                Polygon = CreateExplosionBlobPolygon(Rng.RandfRange(4.0f, 8.0f))
            };

            float startScale = Rng.RandfRange(0.7f, 1.3f);
            Vector2 startOffset = Vector2.FromAngle(Rng.RandfRange(0.0f, Mathf.Tau)) * Rng.RandfRange(0.0f, 5.0f);
            Vector2 endOffset = Vector2.FromAngle(Rng.RandfRange(0.0f, Mathf.Tau)) * Rng.RandfRange(18.0f, 42.0f);

            blob.Position = startOffset;
            blob.Scale = Vector2.One * startScale;
            blob.Rotation = Rng.RandfRange(0.0f, Mathf.Tau);
            explosionRoot.AddChild(blob);

            Tween tween = explosionRoot.CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(blob, "position", endOffset, ExplosionDurationSeconds)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(blob, "scale", Vector2.One * 0.1f, ExplosionDurationSeconds)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.In);
            tween.TweenProperty(blob, "modulate", new Color(blobColor.R, blobColor.G, blobColor.B, 0.0f), ExplosionDurationSeconds)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);
        }

        return explosionRoot;
    }

    private Node2D? SpawnArrivalEffect()
    {
        Node2D spawnRoot = new();
        AddChild(spawnRoot);
        spawnRoot.Position = GetVisualCenterLocalPosition();

        for (int index = 0; index < SpawnBlobCount; index++)
        {
            Color blobColor = CreateExplosionColor();
            Polygon2D blob = new()
            {
                Color = blobColor,
                Polygon = CreateSpawnBlobPolygon(Rng.RandfRange(8.0f, 16.0f))
            };

            Vector2 startOffset = Vector2.FromAngle(Rng.RandfRange(0.0f, Mathf.Tau)) * Rng.RandfRange(28.0f, 56.0f);
            Vector2 endOffset = Vector2.FromAngle(Rng.RandfRange(0.0f, Mathf.Tau)) * Rng.RandfRange(0.0f, 5.0f);

            blob.Position = startOffset;
            blob.Scale = Vector2.One * Rng.RandfRange(0.45f, 0.95f);
            blob.Rotation = Rng.RandfRange(0.0f, Mathf.Tau);
            spawnRoot.AddChild(blob);

            Tween tween = spawnRoot.CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(blob, "position", endOffset, SpawnDurationSeconds)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(blob, "scale", Vector2.One * Rng.RandfRange(1.2f, 1.9f), SpawnDurationSeconds)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(blob, "rotation", blob.Rotation + Rng.RandfRange(-1.6f, 1.6f), SpawnDurationSeconds)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);
            tween.TweenProperty(blob, "modulate", new Color(blobColor.R, blobColor.G, blobColor.B, 0.0f), SpawnDurationSeconds)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);
        }

        return spawnRoot;
    }

    private Vector2 GetVisualCenterGlobalPosition()
    {
        return sprite?.GlobalPosition ?? GlobalPosition;
    }

    private Vector2 GetVisualCenterLocalPosition()
    {
        return sprite?.Position ?? Vector2.Zero;
    }

    private void SetVisualAlpha(float alpha)
    {
        Color spriteModulate = new(1.0f, 1.0f, 1.0f, alpha);
        Color labelModulate = new(1.0f, 1.0f, 1.0f, alpha);

        if (sprite != null)
        {
            sprite.Modulate = spriteModulate;
        }

        if (leftEyeLabel != null)
        {
            leftEyeLabel.Modulate = labelModulate;
        }

        if (rightEyeLabel != null)
        {
            rightEyeLabel.Modulate = labelModulate;
        }

        if (answerLabel != null)
        {
            answerLabel.Modulate = labelModulate;
        }
    }

    private void HideFlyBody()
    {
        if (sprite != null)
        {
            Color color = sprite.Modulate;
            sprite.Modulate = new Color(color.R, color.G, color.B, 0.0f);
        }

        if (leftEyeLabel != null)
        {
            Color color = leftEyeLabel.Modulate;
            leftEyeLabel.Modulate = new Color(color.R, color.G, color.B, 0.0f);
        }

        if (rightEyeLabel != null)
        {
            Color color = rightEyeLabel.Modulate;
            rightEyeLabel.Modulate = new Color(color.R, color.G, color.B, 0.0f);
        }

        if (outlineMaterial != null)
        {
            outlineMaterial.SetShaderParameter(OutlineEnabledParameter, 0.0f);
        }
    }

    private void SetAnswerDissolveProgress(float progress)
    {
        if (answerLabel == null)
        {
            return;
        }

        float alpha = 1.0f - progress;
        float scaleValue = Mathf.Lerp(1.0f, AnswerDissolveEndScale, progress);
        SetAnswerVisualState(alpha, Vector2.One * scaleValue);
    }

    private void SetAnswerVisualState(float alpha, Vector2 scale)
    {
        if (answerLabel == null)
        {
            return;
        }

        Color color = answerLabel.Modulate;
        answerLabel.Modulate = new Color(color.R, color.G, color.B, alpha);
        answerLabel.Scale = scale;
    }

    private void UpdateAnswerLabelLayout()
    {
        if (answerLabel == null)
        {
            return;
        }

        Vector2 minimumSize = answerLabel.GetMinimumSize();
        Vector2 desiredSize = new(
            Mathf.Max(minimumSize.X, 1.0f),
            Mathf.Max(minimumSize.Y, 1.0f));

        answerLabel.Size = desiredSize;
        answerLabel.Position = answerLabelCenterPosition - (desiredSize * 0.5f);
        answerLabel.PivotOffset = desiredSize * 0.5f;
    }

    private static Vector2[] CreateExplosionBlobPolygon(float radius)
    {
        const int pointCount = 9;
        Vector2[] points = new Vector2[pointCount];

        for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
        {
            float angle = (Mathf.Tau * pointIndex) / pointCount;
            float variedRadius = radius * Rng.RandfRange(0.75f, 1.2f);
            points[pointIndex] = Vector2.FromAngle(angle) * variedRadius;
        }

        return points;
    }

    private static Vector2[] CreateSpawnBlobPolygon(float radius)
    {
        const int pointCount = 6;
        Vector2[] points = new Vector2[pointCount];

        for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
        {
            float angle = (Mathf.Tau * pointIndex) / pointCount;
            float pointStretch = (pointIndex % 2 == 0) ? 1.35f : 0.58f;
            float variedRadius = radius * pointStretch * Rng.RandfRange(0.82f, 1.12f);
            points[pointIndex] = Vector2.FromAngle(angle) * variedRadius;
        }

        return points;
    }

    private static Color CreateExplosionColor()
    {
        return new Color(0.0f, 0.0f, 0.0f, 0.95f);
    }

    public void DestroyFly()
    {
        Explode();
    }
}