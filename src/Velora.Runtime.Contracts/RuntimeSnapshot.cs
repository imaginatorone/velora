namespace Velora.Runtime.Contracts;

/// <summary>
/// Compatibility-era serializable runtime snapshot. It deliberately contains no renderer,
/// audio-device, filesystem, editor, or platform handles.
/// </summary>
public sealed record RuntimeSnapshot
{
    private IReadOnlyDictionary<string, RuntimeValue> _variables =
        ContractCollections.CopyOrdinalDictionary(
            new Dictionary<string, RuntimeValue>(StringComparer.Ordinal),
            nameof(Variables));
    private IReadOnlyList<SpriteState> _sprites = Array.AsReadOnly(Array.Empty<SpriteState>());
    private IReadOnlyList<AudioTrackState> _audioTracks =
        Array.AsReadOnly(Array.Empty<AudioTrackState>());

    public ulong Id { get; init; }

    /// <summary>Legacy statement ordinal stored as <c>script_line</c> in VSAV v2.</summary>
    public ulong ScriptLine { get; init; }

    public string CurrentLabel { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, RuntimeValue> Variables
    {
        get => _variables;
        init => _variables = ContractCollections.CopyOrdinalDictionary(
            value,
            nameof(Variables),
            static (_, item) => ContractCollections.ValidateRuntimeValue(item, nameof(Variables)));
    }

    public string BackgroundPath { get; init; } = string.Empty;

    public IReadOnlyList<SpriteState> Sprites
    {
        get => _sprites;
        init => _sprites = ContractCollections.CopyList(
            value,
            nameof(Sprites),
            static item => ContractCollections.ValidateSprite(
                item,
                nameof(Sprites),
                requireNonEmptyIdentity: false));
    }

    public IReadOnlyList<AudioTrackState> AudioTracks
    {
        get => _audioTracks;
        init => _audioTracks = ContractCollections.CopyList(
            value,
            nameof(AudioTracks),
            static item => ContractCollections.ValidateAudioTrack(
                item,
                nameof(AudioTracks),
                requireNonEmptyIdentity: false));
    }

    public string CurrentSpeaker { get; init; } = string.Empty;

    public string CurrentText { get; init; } = string.Empty;

    public bool TextComplete { get; init; }

    /// <summary>Unix time in seconds, matching the legacy snapshot field.</summary>
    public long Timestamp { get; init; }
}
