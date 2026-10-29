using System.Collections.Immutable;
using Velora.Runtime.Contracts;

namespace Velora.Script.Compiler;

public enum IrArgumentKind
{
    Identifier,
    Text,
    Number,
    Color,
    Punctuation,
}

/// <summary>A cooked command argument whose lexical role remains explicit.</summary>
public sealed record IrArgument(IrArgumentKind Kind, string Value);

public sealed record IrModifier(string Name, ImmutableArray<IrArgument> Arguments)
{
    public IrModifier(string name, IEnumerable<IrArgument> arguments)
        : this(name, arguments.ToImmutableArray())
    {
    }
}

public sealed record TransitionDefinition(string Name, double Duration);

public enum SpritePositionKind
{
    Preset,
    Coordinates,
}

public sealed record SpritePositionDefinition(
    SpritePositionKind Kind,
    string? Preset,
    double X,
    double Y);

public enum ScreenElementKind
{
    Text,
    ImageButton,
    Panel,
}

public sealed record ScreenPropertyDefinition(
    string Name,
    ImmutableArray<IrArgument> Values,
    SourceLocation Source)
{
    public ScreenPropertyDefinition(
        string name,
        IEnumerable<IrArgument> values,
        SourceLocation source)
        : this(name, values.ToImmutableArray(), source)
    {
    }
}

public enum ScreenActionKind
{
    Command,
    Jump,
}

/// <summary>
/// A parsed screen action. Jump targets resolve to the effective top-level label when one exists.
/// </summary>
public sealed record ScreenActionDefinition(
    ScreenActionKind Kind,
    string Value,
    InstructionId? TargetInstructionId,
    SourceLocation Source);

public sealed record ScreenElementDefinition(
    ScreenElementKind Kind,
    string? Text,
    string? ImagePath,
    ImmutableArray<ScreenPropertyDefinition> Properties,
    ImmutableArray<ScreenElementDefinition> Children,
    ScreenActionDefinition? Action,
    SourceLocation Source)
{
    public ScreenElementDefinition(
        ScreenElementKind kind,
        string? text,
        string? imagePath,
        IEnumerable<ScreenPropertyDefinition> properties,
        IEnumerable<ScreenElementDefinition> children,
        ScreenActionDefinition? action,
        SourceLocation source)
        : this(
            kind,
            text,
            imagePath,
            properties.ToImmutableArray(),
            children.ToImmutableArray(),
            action,
            source)
    {
    }
}

public sealed record ScreenDefinition(
    string Name,
    ImmutableArray<ScreenElementDefinition> Elements)
{
    public ScreenDefinition(string name, IEnumerable<ScreenElementDefinition> elements)
        : this(name, elements.ToImmutableArray())
    {
    }
}

public sealed record ConditionalTarget(VeloraExpression Condition, InstructionId? TargetInstructionId);

public sealed record ChoiceTarget(string Text, InstructionId? TargetInstructionId);

public enum GlobalDefinitionKind
{
    Label,
    Character,
    Screen,
    Variable,
}

/// <summary>An ordinally sorted global name and its compatibility-era effective definition.</summary>
public sealed record GlobalDefinition(
    GlobalDefinitionKind Kind,
    string Name,
    InstructionId? EffectiveInstructionId,
    bool IsImplicit);
