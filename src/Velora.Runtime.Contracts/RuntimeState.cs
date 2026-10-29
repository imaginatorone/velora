using System.Collections.ObjectModel;

namespace Velora.Runtime.Contracts;

/// <summary>Serializable deterministic runtime bookkeeping.</summary>
public readonly record struct DeterministicState(
    ulong RandomSeed,
    ulong RandomState,
    ulong NextEventSequence);

/// <summary>
/// Complete presentation-neutral state required to capture, restore, and replay a story runtime.
/// </summary>
public sealed record RuntimeState
{
    /// <summary>Creates a detached immutable runtime state.</summary>
    public RuntimeState(
        ProgramFingerprint programFingerprint,
        ExecutionState execution,
        IReadOnlyDictionary<string, RuntimeValue> variables,
        SceneState scene,
        DeterministicState deterministic,
        DialogueState? dialogue = null,
        IEnumerable<DialogueHistoryEntry>? dialogueHistory = null,
        IEnumerable<TweenState>? activeTweens = null,
        IEnumerable<AudioTrackState>? audioTracks = null,
        string title = "",
        bool isSaveLocked = false,
        IEnumerable<InstructionId>? definitionInstructions = null,
        IEnumerable<string>? activeScreens = null,
        IEnumerable<ParticleEmitterState>? particles = null)
    {
        ArgumentNullException.ThrowIfNull(programFingerprint);
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(variables);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(title);

        ProgramFingerprint = programFingerprint;
        Execution = execution;
        Variables = ContractCollections.CopyOrdinalDictionary(
            variables,
            nameof(variables),
            ValidateVariable);
        Scene = scene;
        Deterministic = deterministic;
        Dialogue = dialogue;
        DialogueHistory = ContractCollections.CopyList(
            dialogueHistory ?? [],
            nameof(dialogueHistory),
            entry => ArgumentNullException.ThrowIfNull(entry));
        ActiveTweens = CopyTweens(activeTweens ?? []);
        AudioTracks = CopyAudioTracks(audioTracks ?? []);
        Title = title;
        IsSaveLocked = isSaveLocked;
        DefinitionInstructions = ContractCollections.CopyList(definitionInstructions ?? [], nameof(definitionInstructions));
        ActiveScreens = ContractCollections.CopyList(activeScreens ?? [], nameof(activeScreens),
            static name => ArgumentException.ThrowIfNullOrWhiteSpace(name));
        Particles = ContractCollections.CopyList(particles ?? [], nameof(particles), static emitter =>
        {
            ArgumentNullException.ThrowIfNull(emitter);
            ArgumentException.ThrowIfNullOrWhiteSpace(emitter.Name);
            ArgumentException.ThrowIfNullOrWhiteSpace(emitter.Preset);
            ContractCollections.RequireFinite(emitter.Intensity, nameof(particles));
            ContractCollections.RequireFinite(emitter.Wind, nameof(particles));
            ContractCollections.RequireFinite(emitter.X, nameof(particles));
            ContractCollections.RequireFinite(emitter.Y, nameof(particles));
            ContractCollections.RequireFinite(emitter.Radius, nameof(particles));
        });
    }

    public ProgramFingerprint ProgramFingerprint { get; }

    public ExecutionState Execution { get; }

    public IReadOnlyDictionary<string, RuntimeValue> Variables { get; }

    public SceneState Scene { get; }

    public DeterministicState Deterministic { get; }

    public DialogueState? Dialogue { get; }

    public IReadOnlyList<DialogueHistoryEntry> DialogueHistory { get; }

    public IReadOnlyList<TweenState> ActiveTweens { get; }

    public IReadOnlyList<AudioTrackState> AudioTracks { get; }

    public string Title { get; }

    public bool IsSaveLocked { get; }

    public IReadOnlyList<InstructionId> DefinitionInstructions { get; }

    public IReadOnlyList<string> ActiveScreens { get; }

    public IReadOnlyList<ParticleEmitterState> Particles { get; }

    private static ReadOnlyCollection<TweenState> CopyTweens(IEnumerable<TweenState> tweens)
    {
        ArgumentNullException.ThrowIfNull(tweens);
        TweenState[] copy = tweens
            .Select(tween => tween ?? throw new ArgumentException("A tween must not be null.", nameof(tweens)))
            .OrderBy(tween => tween.Id)
            .ToArray();

        for (int index = 1; index < copy.Length; index++)
        {
            if (copy[index - 1].Id == copy[index].Id)
            {
                throw new ArgumentException($"The tween identifier {copy[index].Id} occurs more than once.", nameof(tweens));
            }
        }

        return Array.AsReadOnly(copy);
    }

    private static ReadOnlyCollection<AudioTrackState> CopyAudioTracks(IEnumerable<AudioTrackState> tracks)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        AudioTrackState[] copy = tracks
            .Select(track => ValidateAudioTrack(track))
            .OrderBy(track => track.Channel, StringComparer.Ordinal)
            .ThenBy(track => track.FilePath, StringComparer.Ordinal)
            .ToArray();

        for (int index = 1; index < copy.Length; index++)
        {
            if (StringComparer.Ordinal.Equals(copy[index - 1].Channel, copy[index].Channel))
            {
                throw new ArgumentException(
                    $"The audio channel '{copy[index].Channel}' occurs more than once.",
                    nameof(tracks));
            }
        }

        return Array.AsReadOnly(copy);
    }

    private static AudioTrackState ValidateAudioTrack(AudioTrackState? track)
    {
        return ContractCollections.ValidateAudioTrack(track, nameof(track));
    }

    private static void ValidateVariable(string name, RuntimeValue value)
    {
        ContractCollections.RequireNonEmpty(name, nameof(name));
        ContractCollections.ValidateRuntimeValue(value, nameof(value));
    }
}
