using Godot;
#nullable enable
using System.Collections.Generic;
using System.Text.Json;

public partial class Gameplay : Node2D
{   
    // Track last N assigned multiplier pairs to avoid repeats
    private readonly Queue<string> recentMultiplierPairs = new();
    private const int RecentMultiplierPairLimit = 5;
    private TextureRect? backgroundTexture;

    // Reference to the autoloaded GameState singleton
    private GameState? gameState;

    private static readonly RandomNumberGenerator SpawnRng = new();
    private const float MinimumFlySpacing = 500.0f;
    private const float FlyHalfSpawnWidth = 42.0f;
    private const int StageClearBaseScore = 1000;
    private const int TargetFlyScorePerKill = 100;
    private const int CleanupKillScorePerFly = 150;
    private const int FlawlessAnswersBonus = 500;
    private const int NoBackspaceBonus = 350;

    private sealed class StageSettings
    {
        public int TargetFlies { get; set; } = 5;
        public int InitialActiveFlies { get; set; } = 1;
        public int ReplacementFlyCount { get; set; } = 2;
        public int Hearts { get; set; } = 1;
        public Dictionary<int, float> LeftFrequency { get; } = new();
        public Dictionary<int, float> RightFrequency { get; } = new();
        public float FlyMoveSpeed { get; set; } = 10.0f;
        public float WrongAnswerAdvanceSpeed { get; set; } = 140.0f;
        public float WrongAnswerPenaltyDistance { get; set; } = 100.0f;
        public float StageClearDelaySeconds { get; set; } = 5.0f;

        public float GetMultiplierWeight(int value, bool isLeft)
        {
            var freq = isLeft ? LeftFrequency : RightFrequency;
            if (freq.TryGetValue(value, out float weight))
                return Mathf.Max(weight, 0.0f);
            return 1.0f;
        }

        public List<int> GetAvailableMultipliers(bool isLeft)
        {
            var freq = isLeft ? LeftFrequency : RightFrequency;
            List<int> availableMultipliers = new();
            foreach ((int value, float weight) in freq)
            {
                if (weight > 0.0f)
                    availableMultipliers.Add(value);
            }
            if (availableMultipliers.Count == 0)
            {
                for (int multiplier = 1; multiplier <= 9; multiplier++)
                    availableMultipliers.Add(multiplier);
            }
            availableMultipliers.Sort();
            return availableMultipliers;
        }
    }

    private int playerHearts;
    private int remainingFliesTarget;
    private Fly? selectedFly;
    private FloatingNumpad? floatingNumpad;
    private HeartDisplay? heartDisplay;
    private Label? remainingFliesLabel;
    private ColorRect? stageClearBarBackground;
    private ColorRect? stageClearBarFill;
    private PackedScene? flyScene;
    private Vector2 flySpawnPosition;
    private Vector2 flyTargetPosition;
    private Rect2 flyMovementBounds = new(Vector2.Zero, Vector2.Zero);
    private bool hasFlySpawnPosition;
    private bool hasFlyTargetPosition;
    private float flyReachBoundaryY = float.MaxValue;
    private bool stageClearPending;
    private bool stageClearTriggered;
    private float stageClearCountdownRemaining;
    private float stageClearBarFullWidth;
    private ulong stageStartTimeMsec;
    private bool flyCountZeroTimeRecorded;
    private long targetClearTimeMs = -1;
    private bool newBestTargetClearTime;
    private int wrongAnswerCount;
    private int backspaceUseCount;
    private int cleanupWindowKillCount;
    private bool spawnLeftHalfNext = true;
    private readonly List<Fly> flies = new();
    private StageSettings currentStageSettings = new();
    private AudioManager? audioManager;

    public override void _Ready()
    {
        // Assign the autoloaded GameState singleton
        gameState = GetNodeOrNull<GameState>("/root/GameState");
        InitializeGame();
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateStageClearCountdown((float)delta);
        UpdateAutopickSelection();
        ResolveFlyOverlaps();
    }

    private void InitializeGame()
    {
        SpawnRng.Randomize();
        stageClearPending = false;
        stageClearTriggered = false;
        stageClearCountdownRemaining = 0.0f;
        flyCountZeroTimeRecorded = false;
        targetClearTimeMs = -1;
        newBestTargetClearTime = false;
        wrongAnswerCount = 0;
        backspaceUseCount = 0;
        cleanupWindowKillCount = 0;
        stageStartTimeMsec = Time.GetTicksMsec();
        audioManager = GetNodeOrNull<AudioManager>("/root/AudioManager");
        LoadStageData();
        if (gameState?.IsBackgroundMusicEnabled ?? true)
        {
            audioManager?.PlayLevelBackgroundMusic(gameState?.CurrentLevel ?? 1);
        }
        flyScene = GD.Load<PackedScene>("res://scenes/actors/Fly.tscn");
        floatingNumpad = GetNodeOrNull<FloatingNumpad>("FloatingNumpad");
        heartDisplay = GetNodeOrNull<HeartDisplay>("HeartDisplay");
        remainingFliesLabel = GetNodeOrNull<Label>("RemainingFliesLabel");
        stageClearBarBackground = GetNodeOrNull<ColorRect>("StageClearBarBackground");
        stageClearBarFill = GetNodeOrNull<ColorRect>("StageClearBarFill");
        backgroundTexture = GetNodeOrNull<TextureRect>("TextureRect");

        // Debug: Print available multipliers for this stage
        var leftMultipliers = currentStageSettings.GetAvailableMultipliers(true);
        var rightMultipliers = currentStageSettings.GetAvailableMultipliers(false);
        GD.Print($"[DEBUG] Left multipliers for stage {gameState?.CurrentLevel ?? 1}: {string.Join(", ", leftMultipliers)}");
        GD.Print($"[DEBUG] Right multipliers for stage {gameState?.CurrentLevel ?? 1}: {string.Join(", ", rightMultipliers)}");
        // Set background image per stage
        if (backgroundTexture != null)
        {
            int stage = gameState?.CurrentLevel ?? 1;
            string bgPath = stage switch
            {
                1 => "res://scenes/ui/bg1.png",
                2 => "res://scenes/ui/bg2.png",
                3 => "res://scenes/ui/bg3.png",
                4 => "res://scenes/ui/bg4.png",
                5 => "res://scenes/ui/bg5.png",
                _ => "res://scenes/ui/bg1.png"
            };
            Texture2D bgTex = GD.Load<Texture2D>(bgPath);
            backgroundTexture.Texture = bgTex;
            Vector2 viewportSize = GetViewportRect().Size;
            backgroundTexture.Position = Vector2.Zero;
            backgroundTexture.Size = viewportSize * 1.0f;
        }
        if (stageClearBarFill != null)
        {
            stageClearBarFullWidth = stageClearBarFill.Size.X;
        }
        if (floatingNumpad != null)
        {
            floatingNumpad.InputChanged += OnNumpadInputChanged;
            floatingNumpad.BackspaceUsed += OnNumpadBackspaceUsed;
            floatingNumpad.LayoutChanged += OnFloatingNumpadLayoutChanged;
            floatingNumpad.AutopickToggled += OnAutopickToggled;
            OnFloatingNumpadLayoutChanged(floatingNumpad.GetNumpadTopY());
        }
        heartDisplay?.SetHeartCount(playerHearts);
        UpdateRemainingFliesDisplay();
        UpdateStageClearBar();
        RegisterExistingFlies();
        spawnLeftHalfNext = flies.Count % 2 == 0;
        EnsureInitialFlyCount();
    }

    private void UpdateViewportLayout()
    {
        Vector2 viewportSize = GetViewportRect().Size;
        Position = Vector2.Zero;
        if (backgroundTexture != null)
        {
            backgroundTexture.Position = Vector2.Zero;
            backgroundTexture.Size = viewportSize;
        }
        // Removed EmitLayoutChangedSignal(); as it is not defined and not needed for background resizing.
    }

    public override void _ExitTree()
    {
        if (floatingNumpad != null)
        {
            floatingNumpad.InputChanged -= OnNumpadInputChanged;
            floatingNumpad.BackspaceUsed -= OnNumpadBackspaceUsed;
            floatingNumpad.LayoutChanged -= OnFloatingNumpadLayoutChanged;
            floatingNumpad.AutopickToggled -= OnAutopickToggled;
        }

        // Do not stop background music here so it continues on StageClear screen
    }

    private void LoadStageData()
    {
        int currentLevel = Mathf.Clamp(gameState?.CurrentLevel ?? 1, 1, GameState.MaxLevelCount);
        if (gameState != null)
        {
            gameState.CurrentLevel = currentLevel;
        }

        string stagePath = $"res://data/stages/stage_{currentLevel:00}.json";
        currentStageSettings = LoadStageSettings(stagePath);
        remainingFliesTarget = currentStageSettings.TargetFlies;
        playerHearts = currentStageSettings.Hearts;
        if (gameState != null)
        {
            gameState.Hearts = playerHearts;
        }

        UpdateRemainingFliesDisplay();
    }

    private StageSettings LoadStageSettings(string stagePath)
    {
        StageSettings settings = new();
        if (!FileAccess.FileExists(stagePath))
        {
            return settings;
        }

        using FileAccess stageFile = FileAccess.Open(stagePath, FileAccess.ModeFlags.Read);
        string jsonText = stageFile.GetAsText();

        try
        {
            using JsonDocument document = JsonDocument.Parse(jsonText);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("difficulty", out JsonElement difficulty))
            {
                GD.PushWarning($"Stage configuration is missing a difficulty object: {stagePath}");
                return settings;
            }
            settings.TargetFlies = Mathf.Max(1, ReadIntSetting(difficulty, "target_flies", settings.TargetFlies));
            settings.InitialActiveFlies = Mathf.Clamp(ReadIntSetting(difficulty, "initial_active_flies", settings.InitialActiveFlies), 1, settings.TargetFlies);
            settings.ReplacementFlyCount = Mathf.Max(0, ReadIntSetting(difficulty, "replacement_fly_count", settings.ReplacementFlyCount));
            settings.Hearts = Mathf.Max(1, ReadIntSetting(difficulty, "hearts", settings.Hearts));
            settings.FlyMoveSpeed = Mathf.Max(1.0f, ReadFloatSetting(difficulty, "fly_move_speed", settings.FlyMoveSpeed));
            settings.WrongAnswerAdvanceSpeed = Mathf.Max(1.0f, ReadFloatSetting(difficulty, "wrong_answer_advance_speed", settings.WrongAnswerAdvanceSpeed));
            settings.WrongAnswerPenaltyDistance = Mathf.Max(0.0f, ReadFloatSetting(difficulty, "wrong_answer_penalty_distance", settings.WrongAnswerPenaltyDistance));
            settings.StageClearDelaySeconds = Mathf.Max(0.0f, ReadFloatSetting(difficulty, "stage_clear_delay_seconds", settings.StageClearDelaySeconds));

            // Load left and right frequencies
            if (difficulty.ValueKind == JsonValueKind.Object)
            {
                if (difficulty.TryGetProperty("l_frequency", out JsonElement lFreqElement))
                    LoadFrequency(settings.LeftFrequency, lFreqElement);
                if (difficulty.TryGetProperty("r_frequency", out JsonElement rFreqElement))
                    LoadFrequency(settings.RightFrequency, rFreqElement);
            }
        }
        catch (JsonException)
        {
            GD.PushWarning($"Unable to parse stage configuration: {stagePath}");
        }

        return settings;
    }

    private static void LoadFrequency(Dictionary<int, float> freqDict, JsonElement freqElement)
    {
        freqDict.Clear();
        if (freqElement.ValueKind != JsonValueKind.Object)
            return;
        foreach (JsonProperty freqProperty in freqElement.EnumerateObject())
        {
            if (!int.TryParse(freqProperty.Name, out int value))
                continue;
            if (!freqProperty.Value.TryGetSingle(out float weight))
                continue;
            freqDict[value] = Mathf.Max(weight, 0.0f);
        }
    }

    private static int ReadIntSetting(JsonElement parent, string propertyName, int fallback)
    {
        if (parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(propertyName, out JsonElement property) &&
            property.TryGetInt32(out int value))
        {
            return value;
        }

        return fallback;
    }

    private static float ReadFloatSetting(JsonElement parent, string propertyName, float fallback)
    {
        if (parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(propertyName, out JsonElement property) &&
            property.TryGetSingle(out float value))
        {
            return value;
        }

        return fallback;
    }

    private void RegisterExistingFlies()
    {
        foreach (Node child in GetChildren())
        {
            if (child is not Fly fly)
            {
                continue;
            }

            if (!hasFlySpawnPosition)
            {
                flySpawnPosition = fly.Position;
                hasFlySpawnPosition = true;
            }

            RegisterFly(fly);
        }
    }

    private void RegisterFly(Fly fly)
    {
        fly.FlyDestroyed += OnFlyDestroyed;
        fly.ReachedPlayer += OnFlyReachedPlayer;
        fly.FlySelected += OnFlySelected;
        ApplyStageSettingsToFly(fly);
        fly.SetReachBoundaryY(flyReachBoundaryY);
        fly.SetMovementBounds(flyMovementBounds);
        if (hasFlyTargetPosition)
        {
            fly.SetTargetPosition(flyTargetPosition);
        }

        flies.Add(fly);
        AssignUniqueMultipliers(fly);
        fly.StartFly();
    }

    private void OnFloatingNumpadLayoutChanged(float numpadTopY)
    {
        flyReachBoundaryY = numpadTopY;
        flyMovementBounds = BuildFlyMovementBounds();
        if (floatingNumpad != null)
        {
            flyTargetPosition = floatingNumpad.GetFlyTargetPosition();
            hasFlyTargetPosition = true;
        }

        foreach (Fly fly in flies)
        {
            if (IsInstanceValid(fly))
            {
                fly.SetReachBoundaryY(flyReachBoundaryY);
                fly.SetMovementBounds(flyMovementBounds);
                if (hasFlyTargetPosition)
                {
                    fly.SetTargetPosition(flyTargetPosition);
                }
            }
        }
    }

    private void ApplyStageSettingsToFly(Fly fly)
    {
        fly.MoveSpeed = currentStageSettings.FlyMoveSpeed;
        fly.WrongAnswerAdvanceSpeed = currentStageSettings.WrongAnswerAdvanceSpeed;
    }

    private void EnsureInitialFlyCount()
    {
        if (remainingFliesTarget <= 0)
        {
            return;
        }

        int desiredFlyCount = Mathf.Clamp(currentStageSettings.InitialActiveFlies, 1, remainingFliesTarget);
        int missingFlyCount = desiredFlyCount - flies.Count;
        if (missingFlyCount > 0)
        {
            SpawnReplacementFlies(missingFlyCount);
        }
    }

    private void AssignUniqueMultipliers(Fly fly)
    {
        HashSet<string> usedPairs = new();
        foreach (Fly existingFly in flies)
        {
            if (existingFly == fly || !IsInstanceValid(existingFly))
                continue;
            usedPairs.Add(GetMultiplierPairKey(existingFly.LeftEye, existingFly.RightEye));
        }

        // Add recent pairs to usedPairs to avoid them
        foreach (var recent in recentMultiplierPairs)
            usedPairs.Add(recent);

        var leftMultipliers = currentStageSettings.GetAvailableMultipliers(true);
        var rightMultipliers = currentStageSettings.GetAvailableMultipliers(false);
        List<Vector2I> availablePairs = new();
        foreach (int leftMultiplier in leftMultipliers)
        {
            foreach (int rightMultiplier in rightMultipliers)
            {
                string pairKey = GetMultiplierPairKey(leftMultiplier, rightMultiplier);
                if (!usedPairs.Contains(pairKey))
                    availablePairs.Add(new Vector2I(leftMultiplier, rightMultiplier));
            }
        }

        // If all pairs are used, allow recent pairs (but still avoid current flies)
        if (availablePairs.Count == 0)
        {
            usedPairs.Clear();
            foreach (Fly existingFly in flies)
            {
                if (existingFly == fly || !IsInstanceValid(existingFly))
                    continue;
                usedPairs.Add(GetMultiplierPairKey(existingFly.LeftEye, existingFly.RightEye));
            }
            foreach (int leftMultiplier in leftMultipliers)
            {
                foreach (int rightMultiplier in rightMultipliers)
                {
                    string pairKey = GetMultiplierPairKey(leftMultiplier, rightMultiplier);
                    if (!usedPairs.Contains(pairKey))
                        availablePairs.Add(new Vector2I(leftMultiplier, rightMultiplier));
                }
            }
            if (availablePairs.Count == 0)
                return;
        }

        Vector2I selectedPair = SelectWeightedMultiplierPair(availablePairs);
        fly.SetMultipliers(selectedPair.X, selectedPair.Y);

        // Track this pair as recently used
        string newPairKey = GetMultiplierPairKey(selectedPair.X, selectedPair.Y);
        recentMultiplierPairs.Enqueue(newPairKey);
        while (recentMultiplierPairs.Count > RecentMultiplierPairLimit)
            recentMultiplierPairs.Dequeue();
    }

    private Vector2I SelectWeightedMultiplierPair(List<Vector2I> availablePairs)
    {
        float totalWeight = 0.0f;
        List<float> pairWeights = new(availablePairs.Count);
        foreach (Vector2I pair in availablePairs)
        {
            float pairWeight = currentStageSettings.GetMultiplierWeight(pair.X, true) * currentStageSettings.GetMultiplierWeight(pair.Y, false);
            pairWeights.Add(pairWeight);
            totalWeight += pairWeight;
        }

        if (totalWeight <= 0.0f)
        {
            return availablePairs[SpawnRng.RandiRange(0, availablePairs.Count - 1)];
        }

        float roll = SpawnRng.RandfRange(0.0f, totalWeight);
        float accumulatedWeight = 0.0f;
        for (int index = 0; index < availablePairs.Count; index++)
        {
            accumulatedWeight += pairWeights[index];
            if (roll <= accumulatedWeight)
            {
                return availablePairs[index];
            }
        }

        return availablePairs[^1];
    }

    public void SelectFly(Fly fly)
    {
        if (selectedFly != null)
        {
            selectedFly.Deselect();
        }

        selectedFly = fly;
        selectedFly.Select();
        selectedFly.SetTypedInput(string.Empty);
        floatingNumpad?.BeginInput();
    }

    public void InputNumber(int number)
    {
        if (selectedFly != null)
        {
            int product = selectedFly.LeftEye * selectedFly.RightEye;
            if (number == product)
            {
                selectedFly.DestroyFly();
            }
        }
    }

    private void OnNumpadInputChanged(string currentInput)
    {
        if (selectedFly == null)
        {
            UpdateAutopickSelection();
            if (selectedFly == null)
            {
                return;
            }
        }

        selectedFly.SetTypedInput(currentInput);
        if (string.IsNullOrEmpty(currentInput))
        {
            return;
        }

        int expectedDigits = selectedFly.GetExpectedAnswerDigits();
        if (currentInput.Length < expectedDigits)
        {
            return;
        }

        if (!int.TryParse(currentInput, out int typedValue))
        {
            return;
        }

        if (selectedFly.CheckAnswer(typedValue))
        {
            Fly flyToDestroy = selectedFly;
            selectedFly = null;
            floatingNumpad?.EndInput();
            flyToDestroy.DestroyFly();
            return;
        }

        wrongAnswerCount++;
        selectedFly.AdvancePenalty(currentStageSettings.WrongAnswerPenaltyDistance);
        floatingNumpad?.ResetCurrentInput();
    }

    private void OnNumpadBackspaceUsed()
    {
        if (stageClearTriggered)
        {
            return;
        }

        backspaceUseCount++;
    }

    private void OnFlyDestroyed(Fly fly)
    {
        if (stageClearTriggered)
        {
            return;
        }

        bool isCleanupWindowKill = stageClearPending || remainingFliesTarget <= 0;
        if (isCleanupWindowKill)
        {
            cleanupWindowKillCount++;
        }

        if (selectedFly == fly)
        {
            selectedFly = null;
            floatingNumpad?.EndInput();
        }

        flies.Remove(fly);
        remainingFliesTarget--;
        UpdateRemainingFliesDisplay();

        if (remainingFliesTarget <= 0)
        {
            RecordFlyCountZeroTimeIfNeeded();
            StartStageClearCountdown();
            if (flies.Count == 0)
            {
                TriggerStageClear();
            }
            return;
        }

        SpawnReplacementFlies(currentStageSettings.ReplacementFlyCount);
        UpdateAutopickSelection();
    }

    private void DestroyRemainingFlies()
    {
        List<Fly> remainingFlies = new(flies);
        flies.Clear();

        foreach (Fly remainingFly in remainingFlies)
        {
            if (remainingFly == selectedFly)
            {
                selectedFly = null;
            }

            if (IsInstanceValid(remainingFly))
            {
                remainingFly.QueueFree();
            }
        }

        floatingNumpad?.EndInput();
    }

    private void SpawnReplacementFlies(int count)
    {
        if (flyScene == null || !hasFlySpawnPosition)
        {
            return;
        }

        List<(Vector2, float)> occupiedPositions = new();
        foreach (Fly existingFly in flies)
        {
            float radius = FlyHalfSpawnWidth;
            var shape = existingFly.GetCollisionShape();
            if (shape.HasValue)
                radius = shape.Value.Radius;
            occupiedPositions.Add((existingFly.GlobalPosition, radius));
        }

        for (int index = 0; index < count; index++)
        {
            Fly newFly = flyScene.Instantiate<Fly>();
            bool isFirstSpawnedFly = occupiedPositions.Count == 0;
            if (isFirstSpawnedFly)
            {
                newFly.Position = FindAvailableSpawnPosition(occupiedPositions, index, null);
                spawnLeftHalfNext = DetermineNextSpawnHalf(newFly.Position.X);
            }
            else
            {
                bool preferLeftHalf = spawnLeftHalfNext;
                newFly.Position = FindAvailableSpawnPosition(occupiedPositions, index, preferLeftHalf);
                spawnLeftHalfNext = !spawnLeftHalfNext;
            }

            AddChild(newFly);
            occupiedPositions.Add((newFly.Position, FlyHalfSpawnWidth));
            RegisterFly(newFly);
        }
    }

    private Vector2 FindAvailableSpawnPosition(List<(Vector2, float)> occupiedPositions, int fallbackIndex, bool? preferLeftHalf)
    {
        const float minimumSpacing = 104.0f;
        const int maxAttempts = 32;

        float flyWidth = FlyHalfSpawnWidth * 2.0f;
        float minX = flyMovementBounds.Position.X;
        float maxX = flyMovementBounds.End.X - flyWidth;
        float spawnY = Mathf.Clamp(flySpawnPosition.Y, flyMovementBounds.Position.Y, flyMovementBounds.End.Y);
        float midpointX = minX + ((maxX - minX) * 0.5f);

        if (preferLeftHalf.HasValue)
        {
            float preferredMinX = preferLeftHalf.Value ? minX : midpointX;
            float preferredMaxX = preferLeftHalf.Value ? midpointX : maxX;

            if (TryFindSpawnPositionInRange(occupiedPositions, spawnY, minimumSpacing, preferredMinX, preferredMaxX, maxAttempts / 2, out Vector2 preferredPosition))
            {
                return preferredPosition;
            }
        }

        if (TryFindSpawnPositionInRange(occupiedPositions, spawnY, minimumSpacing, minX, maxX, maxAttempts, out Vector2 fallbackRandomPosition))
        {
            return fallbackRandomPosition;
        }

        float fallbackStartX = minX;
        float fallbackEndX = maxX;
        if (preferLeftHalf.HasValue)
        {
            fallbackStartX = preferLeftHalf.Value ? minX : midpointX;
            fallbackEndX = preferLeftHalf.Value ? midpointX : maxX;
        }

        float fallbackX = Mathf.Clamp(SpawnRng.RandfRange(fallbackStartX, Mathf.Max(fallbackEndX, fallbackStartX)), minX, maxX);
        return new Vector2(fallbackX, spawnY);
    }

    private bool DetermineNextSpawnHalf(float spawnX)
    {
        float minX = flyMovementBounds.Position.X;
        float maxX = flyMovementBounds.End.X;
        float midpointX = minX + ((maxX - minX) * 0.5f);
        return spawnX >= midpointX;
    }

    private Rect2 BuildFlyMovementBounds()
    {
        Vector2 viewportSize = GetViewportRect().Size;
        float width = viewportSize.X > 0.0f ? viewportSize.X : 648.0f;
        float height = viewportSize.Y > 0.0f ? viewportSize.Y : 1152.0f;
        float bottomY = float.IsFinite(flyReachBoundaryY) ? Mathf.Clamp(flyReachBoundaryY, 1.0f, height) : height;
        return new Rect2(Vector2.Zero, new Vector2(width, bottomY));
    }

    private static bool TryFindSpawnPositionInRange(
        List<(Vector2, float)> occupiedPositions,
        float spawnY,
        float minimumSpacing,
        float minX,
        float maxX,
        int maxAttempts,
        out Vector2 spawnPosition)
    {
        if (maxX < minX)
        {
            spawnPosition = default;
            return false;
        }

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float candidateX = SpawnRng.RandfRange(minX, maxX);
            Vector2 candidatePosition = new(candidateX, spawnY);
            if (IsSpawnPositionAvailable(candidatePosition, occupiedPositions, minimumSpacing))
            {
                spawnPosition = candidatePosition;
                return true;
            }
        }

        spawnPosition = default;
        return false;
    }

    private static bool IsSpawnPositionAvailable(Vector2 candidatePosition, List<(Vector2, float)> occupiedPositions, float minimumSpacing)
    {
        foreach (var (occupiedPosition, occupiedRadius) in occupiedPositions)
        {
            // Use the sum of radii for minimum allowed distance
            float minDist = occupiedRadius + minimumSpacing * 0.5f;
            if (candidatePosition.DistanceTo(occupiedPosition) < minDist)
            {
                return false;
            }
        }
        return true;
    }

    private static string GetMultiplierPairKey(int leftMultiplier, int rightMultiplier)
    {
        int minimumMultiplier = Mathf.Min(leftMultiplier, rightMultiplier);
        int maximumMultiplier = Mathf.Max(leftMultiplier, rightMultiplier);
        return $"{minimumMultiplier}:{maximumMultiplier}";
    }

    private void ResolveFlyOverlaps()
    {
        for (int leftIndex = 0; leftIndex < flies.Count; leftIndex++)
        {
            Fly leftFly = flies[leftIndex];
            if (!IsInstanceValid(leftFly))
            {
                continue;
            }

            for (int rightIndex = leftIndex + 1; rightIndex < flies.Count; rightIndex++)
            {
                Fly rightFly = flies[rightIndex];
                if (!IsInstanceValid(rightFly))
                {
                    continue;
                }

                // Use actual collision shape for overlap/collision
                var leftShapeOpt = leftFly.GetCollisionShape();
                var rightShapeOpt = rightFly.GetCollisionShape();
                if (!leftShapeOpt.HasValue || !rightShapeOpt.HasValue)
                    continue;

                var leftShape = leftShapeOpt.Value;
                var rightShape = rightShapeOpt.Value;
                Vector2 leftCenter = leftFly.GlobalPosition + leftShape.Offset;
                Vector2 rightCenter = rightFly.GlobalPosition + rightShape.Offset;
                float minSeparation = leftShape.Radius + rightShape.Radius;
                float actualDistance = leftCenter.DistanceTo(rightCenter);
                if (actualDistance >= minSeparation)
                    continue;

                // Calculate bounce direction (normalized)
                Vector2 collisionNormal = (rightCenter - leftCenter).Normalized();
                if (collisionNormal == Vector2.Zero)
                    collisionNormal = Vector2.Right;

                // Calculate overlap
                float overlap = minSeparation - actualDistance;

                // Separate flies along collision normal
                Vector2 separation = collisionNormal * (overlap * 0.5f);
                leftFly.GlobalPosition -= separation;
                rightFly.GlobalPosition += separation;
                leftFly.ClampInsideMovementBounds();
                rightFly.ClampInsideMovementBounds();

                // Bounce: exchange velocity along collision normal
                float bounceStrength = 120.0f; // Tune as needed
                Vector2 relativeVelocity = rightFly.Velocity - leftFly.Velocity;
                float separatingVelocity = relativeVelocity.Dot(collisionNormal);
                if (separatingVelocity < bounceStrength * 0.5f)
                {
                    // Impulse to push apart
                    Vector2 impulse = collisionNormal * bounceStrength;
                    leftFly.Velocity -= impulse * 0.5f;
                    rightFly.Velocity += impulse * 0.5f;
                }
            }
        }
    }

    private void OnStageClear()
    {
        GetTree().ChangeSceneToFile("res://scenes/StageClear.tscn");
    }

    private void OnFlyReachedPlayer(Fly fly)
    {
        if (stageClearTriggered)
        {
            return;
        }

        bool isBonusWindow = stageClearPending || remainingFliesTarget <= 0;

        if (selectedFly == fly)
        {
            selectedFly = null;
            floatingNumpad?.EndInput();
        }

        flies.Remove(fly);
        if (IsInstanceValid(fly))
        {
            fly.QueueFree();
        }

        if (isBonusWindow)
        {
            if (flies.Count == 0)
            {
                TriggerStageClear();
            }

            return;
        }

        OnPlayerHit();
        if (stageClearTriggered)
        {
            return;
        }

        EnsureInitialFlyCount();
        UpdateAutopickSelection();
    }

    public void OnPlayerHit()
    {
        playerHearts--;
        if (gameState != null)
        {
            gameState.Hearts = Mathf.Max(playerHearts, 0);
        }

        heartDisplay?.SetHeartCount(playerHearts);
        if (playerHearts <= 0)
        {
            OnGameOver();
        }
    }

    private void OnFlySelected(Fly fly)
    {
        SelectFly(fly);
    }

    private void OnAutopickToggled(bool enabled)
    {
        if (enabled)
        {
            UpdateAutopickSelection();
        }
    }

    private void UpdateAutopickSelection()
    {
        if (floatingNumpad == null || !floatingNumpad.IsAutopickEnabled || stageClearTriggered)
        {
            return;
        }

        Fly? closestFly = null;
        float closestDistance = float.MaxValue;
        foreach (Fly fly in flies)
        {
            if (!IsInstanceValid(fly))
            {
                continue;
            }

            float distance = Mathf.Abs(fly.GlobalPosition.Y - flyTargetPosition.Y);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestFly = fly;
            }
        }

        if (closestFly == null)
        {
            return;
        }

        if (selectedFly != closestFly)
        {
            SelectFly(closestFly);
        }
    }

    private void OnGameOver()
    {
        stageClearPending = false;
        stageClearTriggered = true;
        UpdateStageClearBar();
        GetTree().ChangeSceneToFile("res://scenes/GameOver.tscn");
    }

    private void StartStageClearCountdown()
    {
        if (stageClearPending)
        {
            return;
        }

        stageClearPending = true;
        ActivateBonusFlyMotion();
        stageClearCountdownRemaining = currentStageSettings.StageClearDelaySeconds;
        if (stageClearCountdownRemaining <= 0.0f)
        {
            TriggerStageClear();
            return;
        }

        UpdateStageClearBar();
    }

    private void ActivateBonusFlyMotion()
    {
        foreach (Fly fly in flies)
        {
            if (!IsInstanceValid(fly))
            {
                continue;
            }

            fly.StartBonusEscape();
        }
    }

    private void UpdateStageClearCountdown(float delta)
    {
        if (!stageClearPending || stageClearTriggered)
        {
            return;
        }

        if (flies.Count == 0)
        {
            TriggerStageClear();
            return;
        }

        stageClearCountdownRemaining -= delta;
        UpdateStageClearBar();
        if (stageClearCountdownRemaining <= 0.0f)
        {
            TriggerStageClear();
        }
    }

    private void TriggerStageClear()
    {
        if (stageClearTriggered)
        {
            return;
        }

        stageClearPending = false;
        stageClearTriggered = true;
        UpdateStageClearBar();

        // --- Compute and record stage result ---
        if (gameState != null)
        {
            var stageResult = new GameState.StageResult();
            stageResult.Level = gameState.CurrentLevel;
            stageResult.TargetClearTimeMs = targetClearTimeMs;
            stageResult.TargetFlyCount = currentStageSettings.TargetFlies;
            stageResult.TargetFlyScore = currentStageSettings.TargetFlies * TargetFlyScorePerKill;
            stageResult.ExtraCleanupKills = cleanupWindowKillCount;
            stageResult.ExtraCleanupBonus = cleanupWindowKillCount * CleanupKillScorePerFly;
            stageResult.StageClearBonus = StageClearBaseScore;
            stageResult.WrongAnswers = wrongAnswerCount;
            stageResult.BackspaceUses = backspaceUseCount;
            stageResult.FlawlessAnswersBonus = (wrongAnswerCount == 0) ? FlawlessAnswersBonus : 0;
            stageResult.NoBackspaceBonus = (backspaceUseCount == 0) ? NoBackspaceBonus : 0;
            // Apply time penalty: subtract (TimeInSeconds × 10)
            double timeInSeconds = stageResult.TargetClearTimeMs / 1000.0;
            stageResult.StageScore = (int)(stageResult.StageClearBonus + stageResult.TargetFlyScore + stageResult.ExtraCleanupBonus + stageResult.FlawlessAnswersBonus + stageResult.NoBackspaceBonus - (timeInSeconds * 10));
            gameState.RecordStageResult(stageResult);
        }

        OnStageClear();
    }

    private void RecordFlyCountZeroTimeIfNeeded()
    {
        if (flyCountZeroTimeRecorded)
        {
            return;
        }

        flyCountZeroTimeRecorded = true;
        targetClearTimeMs = (long)(Time.GetTicksMsec() - stageStartTimeMsec);
        int currentLevel = gameState?.CurrentLevel ?? 1;
        newBestTargetClearTime = gameState?.RecordFlyCountZeroTime(currentLevel, targetClearTimeMs) ?? false;
        if (newBestTargetClearTime)
        {
            GD.Print($"Stage {currentLevel} new best zero-target time: {targetClearTimeMs} ms.");
            return;
        }

        long bestTime = gameState?.GetBestFlyCountZeroTime(currentLevel) ?? targetClearTimeMs;
        GD.Print($"Stage {currentLevel} zero-target time: {targetClearTimeMs} ms. Best: {bestTime} ms.");
    }

    private void UpdateStageClearBar()
    {
        bool showBar = stageClearPending && !stageClearTriggered;

        if (stageClearBarBackground != null)
        {
            stageClearBarBackground.Visible = showBar;
        }

        if (stageClearBarFill == null)
        {
            return;
        }

        stageClearBarFill.Visible = showBar;
        if (!showBar)
        {
            return;
        }

        float duration = Mathf.Max(currentStageSettings.StageClearDelaySeconds, 0.001f);
        float ratio = Mathf.Clamp(stageClearCountdownRemaining / duration, 0.0f, 1.0f);
        Vector2 size = stageClearBarFill.Size;
        size.X = stageClearBarFullWidth * ratio;
        stageClearBarFill.Size = size;
    }

    private void UpdateRemainingFliesDisplay()
    {
        if (remainingFliesLabel != null)
        {
            remainingFliesLabel.Text = $"Flies : {Mathf.Max(remainingFliesTarget, 0)}";
        }
    }
}