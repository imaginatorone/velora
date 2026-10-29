namespace Velora.Runtime.Contracts;

/// <summary>Base type for ordered, one-way notifications emitted by the headless runtime.</summary>
public abstract record RuntimeEvent
{
    private protected RuntimeEvent(ulong sequence, InstructionId instruction)
    {
        Sequence = sequence;
        Instruction = instruction;
    }

    /// <summary>Gets the deterministic, monotonically increasing event sequence.</summary>
    public ulong Sequence { get; }

    /// <summary>Gets the instruction responsible for the event, if one exists.</summary>
    public InstructionId Instruction { get; }
}

public sealed record DialogueStarted : RuntimeEvent
{
    public DialogueStarted(ulong sequence, InstructionId instruction, DialogueState dialogue)
        : base(sequence, instruction)
    {
        ArgumentNullException.ThrowIfNull(dialogue);
        Dialogue = dialogue;
    }

    public DialogueState Dialogue { get; }
}

public sealed record BackgroundChanged : RuntimeEvent
{
    public BackgroundChanged(
        ulong sequence,
        InstructionId instruction,
        string backgroundPath,
        TransitionIntent? transition = null)
        : base(sequence, instruction)
    {
        ArgumentNullException.ThrowIfNull(backgroundPath);
        BackgroundPath = backgroundPath;
        Transition = transition;
    }

    public string BackgroundPath { get; }

    public TransitionIntent? Transition { get; }
}

public sealed record SpriteShown : RuntimeEvent
{
    public SpriteShown(
        ulong sequence,
        InstructionId instruction,
        SpriteState sprite,
        TransitionIntent? transition = null)
        : base(sequence, instruction)
    {
        ValidateSprite(sprite);
        Sprite = sprite;
        Transition = transition;
    }

    public SpriteState Sprite { get; }

    public TransitionIntent? Transition { get; }

    private static void ValidateSprite(SpriteState? sprite)
    {
        ContractCollections.ValidateSprite(sprite, nameof(sprite));
    }
}

public sealed record SpriteHidden : RuntimeEvent
{
    public SpriteHidden(
        ulong sequence,
        InstructionId instruction,
        string spriteId,
        TransitionIntent? transition = null)
        : base(sequence, instruction)
    {
        ContractCollections.RequireNonEmpty(spriteId, nameof(spriteId));
        SpriteId = spriteId;
        Transition = transition;
    }

    public string SpriteId { get; }

    public TransitionIntent? Transition { get; }
}

public sealed record SpritePropertyChanged : RuntimeEvent
{
    public SpritePropertyChanged(
        ulong sequence,
        InstructionId instruction,
        string spriteId,
        string property,
        double value)
        : base(sequence, instruction)
    {
        ContractCollections.RequireNonEmpty(spriteId, nameof(spriteId));
        ContractCollections.RequireNonEmpty(property, nameof(property));
        ContractCollections.RequireFinite(value, nameof(value));
        SpriteId = spriteId;
        Property = property;
        Value = value;
    }

    public string SpriteId { get; }

    public string Property { get; }

    public double Value { get; }
}

public sealed record ChoiceRequested : RuntimeEvent
{
    public ChoiceRequested(ulong sequence, InstructionId instruction, ChoiceState choice)
        : base(sequence, instruction)
    {
        ArgumentNullException.ThrowIfNull(choice);
        Choice = choice;
    }

    public ChoiceState Choice { get; }
}

public sealed record TextInputRequested : RuntimeEvent
{
    public TextInputRequested(ulong sequence, InstructionId instruction, TextInputState input)
        : base(sequence, instruction)
    {
        ArgumentNullException.ThrowIfNull(input);
        Input = input;
    }

    public TextInputState Input { get; }
}

/// <summary>Presentation-neutral request to start or replace an audio track.</summary>
public sealed record AudioRequest
{
    public AudioRequest(
        string channel,
        string filePath,
        float volume,
        bool loop,
        TimeSpan fadeDuration,
        TimeSpan crossfadeDuration = default)
    {
        ContractCollections.RequireNonEmpty(channel, nameof(channel));
        ContractCollections.RequireNonEmpty(filePath, nameof(filePath));
        ContractCollections.RequireFinite(volume, nameof(volume));
        ContractCollections.RequireNonNegative(fadeDuration, nameof(fadeDuration));
        ContractCollections.RequireNonNegative(crossfadeDuration, nameof(crossfadeDuration));
        Channel = channel;
        FilePath = filePath;
        Volume = volume;
        Loop = loop;
        FadeDuration = fadeDuration;
        CrossfadeDuration = crossfadeDuration;
    }

    public string Channel { get; }

    public string FilePath { get; }

    public float Volume { get; }

    public bool Loop { get; }

    public TimeSpan FadeDuration { get; }

    public TimeSpan CrossfadeDuration { get; }
}

public sealed record AudioRequested : RuntimeEvent
{
    public AudioRequested(ulong sequence, InstructionId instruction, AudioRequest request)
        : base(sequence, instruction)
    {
        ArgumentNullException.ThrowIfNull(request);
        Request = request;
    }

    public AudioRequest Request { get; }
}

/// <summary>Presentation-neutral request to stop an audio channel.</summary>
public sealed record AudioStopRequest
{
    public AudioStopRequest(string channel, TimeSpan fadeDuration)
    {
        ContractCollections.RequireNonEmpty(channel, nameof(channel));
        ContractCollections.RequireNonNegative(fadeDuration, nameof(fadeDuration));
        Channel = channel;
        FadeDuration = fadeDuration;
    }

    public string Channel { get; }

    public TimeSpan FadeDuration { get; }
}

public sealed record AudioStopped : RuntimeEvent
{
    public AudioStopped(ulong sequence, InstructionId instruction, AudioStopRequest request)
        : base(sequence, instruction)
    {
        ArgumentNullException.ThrowIfNull(request);
        Request = request;
    }

    public AudioStopRequest Request { get; }
}

public enum EffectOperation
{
    Unknown = 0,
    Apply,
    Clear,
    ClearAll,
}

/// <summary>Backend-neutral effect operation with deterministic numeric parameters.</summary>
public sealed record EffectRequest
{
    public EffectRequest(
        EffectOperation operation,
        string effectName,
        double intensity,
        TimeSpan duration,
        IReadOnlyDictionary<string, double>? parameters = null)
    {
        if (operation is EffectOperation.Unknown || !Enum.IsDefined(operation))
        {
            throw new ArgumentOutOfRangeException(nameof(operation), operation, "The effect operation is invalid.");
        }

        ArgumentNullException.ThrowIfNull(effectName);
        if (operation is not EffectOperation.ClearAll)
        {
            ContractCollections.RequireNonEmpty(effectName, nameof(effectName));
        }

        ContractCollections.RequireFinite(intensity, nameof(intensity));
        if (intensity is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(intensity), intensity, "Effect intensity must range from 0 through 1.");
        }

        ContractCollections.RequireNonNegative(duration, nameof(duration));
        Operation = operation;
        EffectName = effectName;
        Intensity = intensity;
        Duration = duration;
        Parameters = ContractCollections.CopyOrdinalDictionary(
            parameters ?? new Dictionary<string, double>(StringComparer.Ordinal),
            nameof(parameters),
            ValidateParameter);
    }

    public EffectOperation Operation { get; }

    public string EffectName { get; }

    public double Intensity { get; }

    public TimeSpan Duration { get; }

    public IReadOnlyDictionary<string, double> Parameters { get; }

    private static void ValidateParameter(string name, double value)
    {
        ContractCollections.RequireNonEmpty(name, nameof(name));
        ContractCollections.RequireFinite(value, nameof(value));
    }
}

public sealed record EffectRequested : RuntimeEvent
{
    public EffectRequested(ulong sequence, InstructionId instruction, EffectRequest request)
        : base(sequence, instruction)
    {
        ArgumentNullException.ThrowIfNull(request);
        Request = request;
    }

    public EffectRequest Request { get; }
}

public sealed record VariableChanged : RuntimeEvent
{
    public VariableChanged(
        ulong sequence,
        InstructionId instruction,
        string name,
        RuntimeValue? previousValue,
        RuntimeValue value)
        : base(sequence, instruction)
    {
        ContractCollections.RequireNonEmpty(name, nameof(name));
        if (previousValue is not null)
        {
            ContractCollections.ValidateRuntimeValue(previousValue, nameof(previousValue));
        }

        ContractCollections.ValidateRuntimeValue(value, nameof(value));
        Name = name;
        PreviousValue = previousValue;
        Value = value;
    }

    public string Name { get; }

    public RuntimeValue? PreviousValue { get; }

    public RuntimeValue Value { get; }
}

public sealed record ExecutionPositionChanged : RuntimeEvent
{
    public ExecutionPositionChanged(
        ulong sequence,
        InstructionId previousInstruction,
        InstructionId currentInstruction,
        string currentLabel)
        : base(sequence, currentInstruction)
    {
        if (!currentInstruction.IsValid)
        {
            throw new ArgumentException("The current instruction must be valid.", nameof(currentInstruction));
        }

        ArgumentNullException.ThrowIfNull(currentLabel);
        PreviousInstruction = previousInstruction;
        CurrentInstruction = currentInstruction;
        CurrentLabel = currentLabel;
    }

    public InstructionId PreviousInstruction { get; }

    public InstructionId CurrentInstruction { get; }

    public string CurrentLabel { get; }
}

public sealed record TitleChanged : RuntimeEvent
{
    public TitleChanged(ulong sequence, InstructionId instruction, string title)
        : base(sequence, instruction)
    {
        ArgumentNullException.ThrowIfNull(title);
        Title = title;
    }

    public string Title { get; }
}

public sealed record SaveLockChanged : RuntimeEvent
{
    public SaveLockChanged(ulong sequence, InstructionId instruction, bool isLocked)
        : base(sequence, instruction)
    {
        IsLocked = isLocked;
    }

    public bool IsLocked { get; }
}

public sealed record QuitRequested : RuntimeEvent
{
    public QuitRequested(ulong sequence, InstructionId instruction)
        : base(sequence, instruction)
    {
    }
}

public sealed record MainMenuRequested : RuntimeEvent
{
    public MainMenuRequested(ulong sequence, InstructionId instruction)
        : base(sequence, instruction)
    {
    }
}

public sealed record RuntimeWarning : RuntimeEvent
{
    public RuntimeWarning(
        ulong sequence,
        InstructionId instruction,
        string code,
        string message)
        : base(sequence, instruction)
    {
        ContractCollections.RequireNonEmpty(code, nameof(code));
        ContractCollections.RequireNonEmpty(message, nameof(message));
        Code = code;
        Message = message;
    }

    public string Code { get; }

    public string Message { get; }
}
