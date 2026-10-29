using Velora.Localization;
using Velora.Platform;
using Velora.Runtime.Contracts;
using Velora.Script.Compiler;

namespace Velora.Runtime;

/// <summary>
/// Executes one immutable <see cref="VeloraProgram"/> without presentation,
/// device, filesystem, clock, or environment dependencies.
/// </summary>
public sealed partial class StoryRuntime
{
    public const int MaximumDialogueHistory = 500;

    private const int MaximumInstructionsPerCommand = 100_000;

    private readonly VeloraProgram _program;
    private readonly IPlatformInfo _platformInfo;
    private readonly ITextLocalizer _localizer;
    private readonly Dictionary<string, RuntimeValue> _variables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CharacterInfo> _characters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SpriteState> _sprites = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AudioTrackState> _audioTracks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ScreenDefinition> _screens = new(StringComparer.Ordinal);
    private readonly HashSet<string> _activeScreens = new(StringComparer.Ordinal);
    private readonly List<DialogueHistoryEntry> _dialogueHistory = [];
    private readonly List<TweenState> _activeTweens = [];
    private readonly List<RuntimeEvent> _commandEvents = [];
    private readonly ulong _initialRandomSeed;
    private readonly Func<string, bool> _assetExists;
    private readonly Dictionary<string, InstructionId> _definitionInstructions = new(StringComparer.Ordinal);
    private readonly List<ParticleEmitterState> _particles = [];

    private RuntimeExecutionStatus _status = RuntimeExecutionStatus.NotStarted;
    private InstructionId _currentInstruction;
    private InstructionId? _nextInstruction;
    private string _currentLabel = string.Empty;
    private string _backgroundPath = string.Empty;
    private WaitState? _wait;
    private ChoiceState? _choice;
    private TextInputState? _textInput;
    private DialogueState? _dialogue;
    private string _title = string.Empty;
    private bool _isSaveLocked;
    private ulong _randomSeed;
    private ulong _randomState;
    private ulong _nextEventSequence = 1;

    public StoryRuntime(
        VeloraProgram program,
        IPlatformInfo platformInfo,
        ITextLocalizer localizer,
        ulong randomSeed = 0,
        Func<string, bool>? assetExists = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(platformInfo);
        ArgumentNullException.ThrowIfNull(localizer);

        _program = program;
        _platformInfo = platformInfo;
        _localizer = localizer;
        _initialRandomSeed = randomSeed;
        _randomSeed = randomSeed;
        _randomState = randomSeed;
        _assetExists = assetExists ?? (static _ => false);
    }

    public VeloraProgram Program => _program;

    /// <summary>Gets the detached events produced by the most recent command.</summary>
    public IReadOnlyList<RuntimeEvent> LastEvents { get; private set; } =
        Array.AsReadOnly(Array.Empty<RuntimeEvent>());

    /// <summary>Resets and runs the story through its first suspension point.</summary>
    public IReadOnlyList<RuntimeEvent> Start()
    {
        BeginCommand();
        ResetMutableState();
        _status = RuntimeExecutionStatus.Running;

        foreach (InstructionId instructionId in _program.InitializationInstructionIds)
        {
            ExecuteInstruction(instructionId);
            _status = RuntimeExecutionStatus.Running;
            _wait = null;
            _choice = null;
            _textInput = null;
        }

        _currentLabel = _program.EntryLabel ?? string.Empty;
        SetVariable(
            "sys.os_username",
            new RuntimeValue.StringValue(_platformInfo.UserName ?? string.Empty),
            _currentInstruction);

        _nextInstruction = _program.EntryInstructionId;
        if (_nextInstruction is null)
        {
            _status = RuntimeExecutionStatus.Finished;
        }
        else
        {
            RunUntilSuspended();
        }

        return CompleteCommand();
    }

    public IReadOnlyList<RuntimeEvent> Advance()
    {
        BeginCommand();
        if (_status == RuntimeExecutionStatus.WaitingForAdvance)
        {
            _status = RuntimeExecutionStatus.Running;
            RunUntilSuspended();
        }

        return CompleteCommand();
    }

    public IReadOnlyList<RuntimeEvent> SelectChoice(int index)
    {
        BeginCommand();
        if (_status != RuntimeExecutionStatus.WaitingForChoice ||
            _choice is null ||
            index < 0 ||
            index >= _choice.Options.Count ||
            !_choice.Options[index].IsEnabled)
        {
            return CompleteCommand();
        }

        VeloraInstruction instruction = _program.GetInstruction(_currentInstruction);
        switch (instruction)
        {
            case RequestInputInstruction input when input.Options.Length != 0:
                CommitTextInput(input.TargetVariable, input.Options[index]);
                _nextInstruction = input.ContinuationInstructionId;
                break;

            case PresentChoiceInstruction menu when index < menu.Choices.Length:
                _nextInstruction = menu.Choices[index].TargetInstructionId;
                break;

            default:
                Warn(
                    RuntimeWarningCodes.InvalidArgument,
                    "The pending choice does not match the current instruction.");
                return CompleteCommand();
        }

        _choice = null;
        _status = RuntimeExecutionStatus.Running;
        RunUntilSuspended();
        return CompleteCommand();
    }

    public IReadOnlyList<RuntimeEvent> SubmitTextInput(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        BeginCommand();
        if (_status == RuntimeExecutionStatus.WaitingForTextInput && _textInput is not null)
        {
            CommitTextInput(_textInput.VariableName, text);
            _textInput = null;
            _status = RuntimeExecutionStatus.Running;
            RunUntilSuspended();
        }

        return CompleteCommand();
    }

    public IReadOnlyList<RuntimeEvent> Update(TimeSpan delta)
    {
        if (delta < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), delta, "The runtime delta must not be negative.");
        }

        BeginCommand();
        UpdateTweens(delta);

        if (_status == RuntimeExecutionStatus.WaitingForTime && _wait is not null)
        {
            if (delta >= _wait.Remaining)
            {
                _wait = null;
                _status = RuntimeExecutionStatus.Running;
                RunUntilSuspended();
            }
            else
            {
                _wait = new WaitState(_wait.Duration, _wait.Remaining - delta);
            }
        }
        else if (_status == RuntimeExecutionStatus.Running)
        {
            RunUntilSuspended();
        }

        return CompleteCommand();
    }

    /// <summary>Creates a detached, presentation-neutral state.</summary>
    public RuntimeState CaptureState()
    {
        var execution = new ExecutionState(
            _status,
            _currentInstruction,
            _currentLabel,
            wait: _wait,
            choice: _choice,
            textInput: _textInput);
        return new RuntimeState(
            _program.Fingerprint,
            execution,
            _variables,
            new SceneState(_backgroundPath, _sprites.Values),
            new DeterministicState(_randomSeed, _randomState, _nextEventSequence),
            _dialogue,
            _dialogueHistory,
            _activeTweens,
            _audioTracks.Values,
            _title,
            _isSaveLocked,
            _definitionInstructions.Values.Order(),
            _activeScreens.Order(StringComparer.Ordinal),
            _particles);
    }

    /// <summary>
    /// Restores a state for this exact compiled program without replaying events
    /// or external intents.
    /// </summary>
    public void Restore(RuntimeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.ProgramFingerprint != _program.Fingerprint)
        {
            throw new ArgumentException(
                "The runtime state belongs to a different compiled program.",
                nameof(state));
        }

        ValidateInstruction(state.Execution.CurrentInstruction, state.Execution.Status);
        ValidateRestoredState(state);

        _variables.Clear();
        foreach ((string name, RuntimeValue value) in state.Variables)
        {
            _variables.Add(name, value);
        }

        _sprites.Clear();
        foreach (SpriteState sprite in state.Scene.Sprites)
        {
            _sprites.Add(sprite.Id, sprite);
        }

        _audioTracks.Clear();
        foreach (AudioTrackState track in state.AudioTracks)
        {
            _audioTracks.Add(track.Channel, track);
        }

        _dialogueHistory.Clear();
        _dialogueHistory.AddRange(state.DialogueHistory);
        _activeTweens.Clear();
        _activeTweens.AddRange(state.ActiveTweens);

        _status = state.Execution.Status;
        _currentInstruction = state.Execution.CurrentInstruction;
        _currentLabel = state.Execution.CurrentLabel;
        _backgroundPath = state.Scene.BackgroundPath;
        _wait = state.Execution.Wait;
        _choice = state.Execution.Choice;
        _textInput = state.Execution.TextInput;
        _dialogue = state.Dialogue;
        _title = state.Title;
        _isSaveLocked = state.IsSaveLocked;
        _randomSeed = state.Deterministic.RandomSeed;
        _randomState = state.Deterministic.RandomState;
        _nextEventSequence = state.Deterministic.NextEventSequence;
        _nextInstruction = ResolveContinuation(state.Execution);

        RebuildStaticDefinitions(state.DefinitionInstructions);
        _activeScreens.Clear();
        _activeScreens.UnionWith(state.ActiveScreens);
        _particles.Clear();
        foreach (ParticleEmitterState emitter in state.Particles)
        {
            _particles.Add(emitter);
        }
        _commandEvents.Clear();
        LastEvents = Array.AsReadOnly(Array.Empty<RuntimeEvent>());
    }

    private void RunUntilSuspended()
    {
        int executed = 0;
        while (_status == RuntimeExecutionStatus.Running)
        {
            if (_nextInstruction is null)
            {
                _status = RuntimeExecutionStatus.Finished;
                return;
            }

            if (executed++ >= MaximumInstructionsPerCommand)
            {
                Warn(
                    RuntimeWarningCodes.ExecutionLimitReached,
                    $"Execution exceeded {MaximumInstructionsPerCommand} instructions without suspending.");
                _nextInstruction = null;
                _status = RuntimeExecutionStatus.Finished;
                return;
            }

            ExecuteInstruction(_nextInstruction.Value);
        }
    }

    private void MoveExecutionPosition(VeloraInstruction instruction)
    {
        InstructionId previous = _currentInstruction;
        if (instruction is LabelInstruction label)
        {
            _currentLabel = label.Name;
        }

        _currentInstruction = instruction.Id;
        Emit(sequence => new ExecutionPositionChanged(
            sequence,
            previous,
            instruction.Id,
            _currentLabel));
    }

    private void CommitTextInput(string variableName, string text)
    {
        SetVariable(variableName, new RuntimeValue.StringValue(text), _currentInstruction);
        if (text.Length != 0)
        {
            SetVariable(
                "sys.name_gibberish",
                new RuntimeValue.BooleanValue(NameHeuristics.IsGibberish(text)),
                _currentInstruction);
        }
    }

    private void SetVariable(string name, RuntimeValue value, InstructionId instruction)
    {
        _variables.TryGetValue(name, out RuntimeValue? previous);
        _variables[name] = value;
        Emit(sequence => new VariableChanged(sequence, instruction, name, previous, value));
    }

    private void BeginCommand()
    {
        _commandEvents.Clear();
    }

    private IReadOnlyList<RuntimeEvent> CompleteCommand()
    {
        LastEvents = Array.AsReadOnly(_commandEvents.ToArray());
        return LastEvents;
    }

    private void Emit(Func<ulong, RuntimeEvent> factory)
    {
        RuntimeEvent runtimeEvent = factory(_nextEventSequence);
        _nextEventSequence = checked(_nextEventSequence + 1);
        _commandEvents.Add(runtimeEvent);
    }

    private void Warn(string code, string message)
    {
        Emit(sequence => new RuntimeWarning(sequence, _currentInstruction, code, message));
    }

    private void ResetMutableState()
    {
        _variables.Clear();
        _characters.Clear();
        _sprites.Clear();
        _audioTracks.Clear();
        _screens.Clear();
        _activeScreens.Clear();
        _definitionInstructions.Clear();
        _particles.Clear();
        _dialogueHistory.Clear();
        _activeTweens.Clear();
        _status = RuntimeExecutionStatus.NotStarted;
        _currentInstruction = default;
        _nextInstruction = null;
        _currentLabel = string.Empty;
        _backgroundPath = string.Empty;
        _wait = null;
        _choice = null;
        _textInput = null;
        _dialogue = null;
        _title = string.Empty;
        _isSaveLocked = false;
        _randomSeed = _initialRandomSeed;
        _randomState = _initialRandomSeed;
        _nextEventSequence = 1;
    }

    private void ValidateInstruction(InstructionId instruction, RuntimeExecutionStatus status)
    {
        if (status is RuntimeExecutionStatus.NotStarted or RuntimeExecutionStatus.Finished &&
            !instruction.IsValid)
        {
            return;
        }

        if (!instruction.IsValid || instruction.Ordinal >= _program.Instructions.Length)
        {
            throw new ArgumentException("The runtime state contains an instruction outside this program.");
        }
    }

    private InstructionId? ResolveContinuation(ExecutionState execution)
    {
        if (execution.Status is RuntimeExecutionStatus.NotStarted or RuntimeExecutionStatus.Finished ||
            !execution.CurrentInstruction.IsValid)
        {
            return null;
        }

        return _program.GetInstruction(execution.CurrentInstruction).ContinuationInstructionId;
    }

    private void RebuildStaticDefinitions(IEnumerable<InstructionId> definitions)
    {
        _characters.Clear();
        _screens.Clear();
        _definitionInstructions.Clear();
        foreach (InstructionId id in definitions)
        {
            VeloraInstruction instruction = _program.GetInstruction(id);
            switch (instruction)
            {
                case DefineCharacterInstruction character:
                    _definitionInstructions[$"character:{character.CharacterId}"] = id;
                    _characters[character.CharacterId] = new CharacterInfo(
                        character.DisplayName,
                        character.Color);
                    break;
                case RegisterScreenInstruction screen:
                    _definitionInstructions[$"screen:{screen.Screen.Name}"] = id;
                    _screens[screen.Screen.Name] = screen.Screen;
                    break;
            }
        }
    }

    private void ValidateRestoredState(RuntimeState state)
    {
        foreach (InstructionId id in state.DefinitionInstructions)
        {
            ValidateInstruction(id, RuntimeExecutionStatus.Running);
            if (_program.GetInstruction(id) is not (DefineCharacterInstruction or RegisterScreenInstruction))
            {
                throw new ArgumentException("A restored definition must identify a character or screen.", nameof(state));
            }
        }

        if (state.Execution.Status is RuntimeExecutionStatus.NotStarted or RuntimeExecutionStatus.Finished)
        {
            return;
        }

        VeloraInstruction current = _program.GetInstruction(state.Execution.CurrentInstruction);
        bool valid = state.Execution.Status switch
        {
            RuntimeExecutionStatus.WaitingForAdvance => current is SayInstruction,
            RuntimeExecutionStatus.WaitingForTime => current is WaitInstruction,
            RuntimeExecutionStatus.WaitingForChoice => current switch
            {
                PresentChoiceInstruction menu => state.Execution.Choice!.Options.Count == menu.Choices.Length,
                RequestInputInstruction input => state.Execution.Choice!.Options.Count == input.Options.Length,
                _ => false,
            },
            RuntimeExecutionStatus.WaitingForTextInput => current is RequestInputInstruction input &&
                input.Options.IsEmpty && input.TargetVariable == state.Execution.TextInput!.VariableName,
            _ => false,
        };
        if (!valid)
        {
            throw new ArgumentException("The restored suspension does not match its instruction.", nameof(state));
        }
    }

    private sealed record CharacterInfo(string DisplayName, string Color);
}
