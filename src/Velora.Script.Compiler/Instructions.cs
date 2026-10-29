using System.Collections.Immutable;
using Velora.Runtime.Contracts;

namespace Velora.Script.Compiler;

/// <summary>Base type for immutable, syntax-independent runtime instructions.</summary>
public abstract record VeloraInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId);

public sealed record DefineCharacterInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    string CharacterId,
    string DisplayName,
    string Color) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);

public sealed record LabelInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    string Name,
    bool IsJumpTarget) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);

public sealed record ChangeSceneInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    string ImagePath,
    TransitionDefinition? Transition) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);

public sealed record ShowSpriteInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    string Target,
    string ImagePath,
    bool IsExpressionShorthand,
    SpritePositionDefinition? Position,
    TransitionDefinition? Transition,
    ImmutableArray<IrModifier> Modifiers) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);

public sealed record HideSpriteInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    string Target,
    bool HideAll,
    TransitionDefinition? Transition) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);

public sealed record SayInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    string? SpeakerId,
    string Text) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);

public sealed record PlayAudioInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    string Channel,
    string FilePath,
    ImmutableArray<IrModifier> Modifiers) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);

public sealed record StopAudioInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    string Channel,
    IrModifier? FadeOut) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);

public sealed record WaitInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    double DurationSeconds) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);

public sealed record SetVariableInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    string Name,
    VeloraExpression Value) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);

public sealed record JumpInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    string LabelName,
    InstructionId? TargetInstructionId) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);

public sealed record PresentChoiceInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    ImmutableArray<ChoiceTarget> Choices) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);

public sealed record BranchInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    ImmutableArray<ConditionalTarget> Branches,
    InstructionId? ElseTargetInstructionId) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);

public sealed record SetConfigInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    string Key,
    VeloraExpression Value) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);

public sealed record StartTweenInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    string Target,
    string Property,
    VeloraExpression Value,
    double DurationSeconds,
    string Easing) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);

public sealed record TriggerEmotionInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    string CharacterId,
    string Emotion) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);

public sealed record RequestInputInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    string Prompt,
    string TargetVariable,
    ImmutableArray<string> Options) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);

public sealed record ExecuteMetaInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    string Action,
    ImmutableArray<IrArgument> Arguments) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);

public sealed record ParticleInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    string Preset,
    string? Name,
    bool StopAll,
    ImmutableArray<IrModifier> Parameters) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);

public sealed record RegisterScreenInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    ScreenDefinition Screen) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);

public sealed record ShowScreenInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    string ScreenName) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);

public sealed record HideScreenInstruction(
    InstructionId Id,
    SourceLocation Source,
    string? ContainingLabel,
    InstructionId? ContinuationInstructionId,
    string ScreenName) : VeloraInstruction(Id, Source, ContainingLabel, ContinuationInstructionId);
