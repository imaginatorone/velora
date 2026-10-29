namespace Velora.Runtime.Contracts;

/// <summary>A screen visibility change. The host resolves its definition in the compiled program.</summary>
public sealed record ScreenVisibilityChanged : RuntimeEvent
{
    public ScreenVisibilityChanged(ulong sequence, InstructionId instruction, string screenName, bool visible)
        : base(sequence, instruction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(screenName);
        ScreenName = screenName;
        Visible = visible;
    }

    public string ScreenName { get; }

    public bool Visible { get; }
}

public sealed record ParticleEmitterState(
    string Name, string Preset, double Intensity, double Wind, double X, double Y, double Radius);

/// <summary>
/// Adds an emitter, including repeated names. Null stops every emitter with the
/// given name; an empty name stops all emitters. Particle pools belong to the host.
/// </summary>
public sealed record ParticleChanged : RuntimeEvent
{
    public ParticleChanged(ulong sequence, InstructionId instruction, string name, ParticleEmitterState? emitter)
        : base(sequence, instruction)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        Emitter = emitter;
    }

    public string Name { get; }

    public ParticleEmitterState? Emitter { get; }
}

public enum HostAction
{
    WriteScriptData,
    DeleteScriptData,
    DeleteSave,
    Fullscreen,
    Windowed,
    ShowQuickbar,
    HideQuickbar,
    GlitchText,
    GlitchUi,
    ShakeWindow,
}

/// <summary>
/// An explicit request for a host-owned operation. Preview/replay can suppress
/// external side effects; filesystem requests still require contained host storage.
/// </summary>
public sealed record HostActionRequested : RuntimeEvent
{
    public HostActionRequested(ulong sequence, InstructionId instruction, HostAction action, IEnumerable<string> arguments)
        : base(sequence, instruction)
    {
        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }

        Action = action;
        Arguments = ContractCollections.CopyList(arguments, nameof(arguments),
            static argument => ArgumentNullException.ThrowIfNull(argument));
    }

    public HostAction Action { get; }

    public IReadOnlyList<string> Arguments { get; }
}
