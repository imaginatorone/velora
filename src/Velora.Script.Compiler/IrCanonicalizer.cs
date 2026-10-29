using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Velora.Runtime.Contracts;

namespace Velora.Script.Compiler;

internal static class IrCanonicalizer
{
    public static ProgramFingerprint ComputeFingerprint(
        string compilerVersion,
        int formatVersion,
        ImmutableArray<VeloraInstruction> instructions,
        ImmutableArray<InstructionId> initialization,
        InstructionId? entryInstructionId,
        string? entryLabel,
        ImmutableArray<GlobalDefinition> definitions)
    {
        CanonicalSink sink = new();
        sink.WriteString("VeloraIR");
        sink.WriteInt32(formatVersion);
        sink.WriteString(compilerVersion);

        sink.WriteInt32(instructions.Length);
        foreach (VeloraInstruction instruction in instructions)
        {
            WriteInstruction(sink, instruction);
        }

        sink.WriteInt32(initialization.Length);
        foreach (InstructionId instructionId in initialization)
        {
            sink.WriteInt32(instructionId.Ordinal);
        }

        sink.WriteNullableInstructionId(entryInstructionId);
        sink.WriteNullableString(entryLabel);

        sink.WriteInt32(definitions.Length);
        foreach (GlobalDefinition definition in definitions)
        {
            sink.WriteInt32(GetGlobalDefinitionKind(definition.Kind));
            sink.WriteString(definition.Name);
            sink.WriteNullableInstructionId(definition.EffectiveInstructionId);
            sink.WriteBoolean(definition.IsImplicit);
        }

        string hash = Convert.ToHexStringLower(SHA256.HashData(sink.WrittenSpan));
        return new ProgramFingerprint(compilerVersion, hash);
    }

    private static void WriteInstruction(CanonicalSink sink, VeloraInstruction instruction)
    {
        sink.WriteInt32(GetInstructionKind(instruction));
        sink.WriteInt32(instruction.Id.Ordinal);
        sink.WriteNullableString(instruction.ContainingLabel);
        sink.WriteNullableInstructionId(instruction.ContinuationInstructionId);

        switch (instruction)
        {
            case DefineCharacterInstruction character:
                sink.WriteString(character.CharacterId);
                sink.WriteString(character.DisplayName);
                sink.WriteString(character.Color);
                break;

            case LabelInstruction label:
                sink.WriteString(label.Name);
                sink.WriteBoolean(label.IsJumpTarget);
                break;

            case ChangeSceneInstruction scene:
                sink.WriteString(scene.ImagePath);
                WriteTransition(sink, scene.Transition);
                break;

            case ShowSpriteInstruction show:
                sink.WriteString(show.Target);
                sink.WriteString(show.ImagePath);
                sink.WriteBoolean(show.IsExpressionShorthand);
                WritePosition(sink, show.Position);
                WriteTransition(sink, show.Transition);
                WriteModifiers(sink, show.Modifiers);
                break;

            case HideSpriteInstruction hide:
                sink.WriteString(hide.Target);
                sink.WriteBoolean(hide.HideAll);
                WriteTransition(sink, hide.Transition);
                break;

            case SayInstruction say:
                sink.WriteNullableString(say.SpeakerId);
                sink.WriteString(say.Text);
                break;

            case PlayAudioInstruction play:
                sink.WriteString(play.Channel);
                sink.WriteString(play.FilePath);
                WriteModifiers(sink, play.Modifiers);
                break;

            case StopAudioInstruction stop:
                sink.WriteString(stop.Channel);
                WriteModifier(sink, stop.FadeOut);
                break;

            case WaitInstruction wait:
                sink.WriteDouble(wait.DurationSeconds);
                break;

            case SetVariableInstruction set:
                sink.WriteString(set.Name);
                WriteExpression(sink, set.Value);
                break;

            case JumpInstruction jump:
                sink.WriteString(jump.LabelName);
                sink.WriteNullableInstructionId(jump.TargetInstructionId);
                break;

            case PresentChoiceInstruction choice:
                sink.WriteInt32(choice.Choices.Length);
                foreach (ChoiceTarget target in choice.Choices)
                {
                    sink.WriteString(target.Text);
                    sink.WriteNullableInstructionId(target.TargetInstructionId);
                }

                break;

            case BranchInstruction branch:
                sink.WriteInt32(branch.Branches.Length);
                foreach (ConditionalTarget target in branch.Branches)
                {
                    WriteExpression(sink, target.Condition);
                    sink.WriteNullableInstructionId(target.TargetInstructionId);
                }

                sink.WriteNullableInstructionId(branch.ElseTargetInstructionId);
                break;

            case SetConfigInstruction config:
                sink.WriteString(config.Key);
                WriteExpression(sink, config.Value);
                break;

            case StartTweenInstruction tween:
                sink.WriteString(tween.Target);
                sink.WriteString(tween.Property);
                WriteExpression(sink, tween.Value);
                sink.WriteDouble(tween.DurationSeconds);
                sink.WriteString(tween.Easing);
                break;

            case TriggerEmotionInstruction emotion:
                sink.WriteString(emotion.CharacterId);
                sink.WriteString(emotion.Emotion);
                break;

            case RequestInputInstruction input:
                sink.WriteString(input.Prompt);
                sink.WriteString(input.TargetVariable);
                sink.WriteInt32(input.Options.Length);
                foreach (string option in input.Options)
                {
                    sink.WriteString(option);
                }

                break;

            case ExecuteMetaInstruction meta:
                sink.WriteString(meta.Action);
                WriteArguments(sink, meta.Arguments);
                break;

            case ParticleInstruction particle:
                sink.WriteString(particle.Preset);
                sink.WriteNullableString(particle.Name);
                sink.WriteBoolean(particle.StopAll);
                WriteModifiers(sink, particle.Parameters);
                break;

            case RegisterScreenInstruction screen:
                WriteScreen(sink, screen.Screen);
                break;

            case ShowScreenInstruction showScreen:
                sink.WriteString(showScreen.ScreenName);
                break;

            case HideScreenInstruction hideScreen:
                sink.WriteString(hideScreen.ScreenName);
                break;

            default:
                throw new InvalidOperationException(
                    $"Canonical serialization does not support {instruction.GetType().Name}.");
        }
    }

    private static int GetInstructionKind(VeloraInstruction instruction) => instruction switch
    {
        DefineCharacterInstruction => 1,
        LabelInstruction => 2,
        ChangeSceneInstruction => 3,
        ShowSpriteInstruction => 4,
        HideSpriteInstruction => 5,
        SayInstruction => 6,
        PlayAudioInstruction => 7,
        StopAudioInstruction => 8,
        WaitInstruction => 9,
        SetVariableInstruction => 10,
        JumpInstruction => 11,
        PresentChoiceInstruction => 12,
        BranchInstruction => 13,
        SetConfigInstruction => 14,
        StartTweenInstruction => 15,
        TriggerEmotionInstruction => 16,
        RequestInputInstruction => 17,
        ExecuteMetaInstruction => 18,
        ParticleInstruction => 19,
        RegisterScreenInstruction => 20,
        ShowScreenInstruction => 21,
        HideScreenInstruction => 22,
        _ => throw new InvalidOperationException($"Unknown instruction {instruction.GetType().Name}."),
    };

    private static void WriteExpression(CanonicalSink sink, VeloraExpression expression)
    {
        switch (expression)
        {
            case StringExpression value:
                sink.WriteInt32(1);
                sink.WriteString(value.Value);
                break;
            case NumberExpression value:
                sink.WriteInt32(2);
                sink.WriteDouble(value.Value);
                break;
            case BooleanExpression value:
                sink.WriteInt32(3);
                sink.WriteBoolean(value.Value);
                break;
            case ColorExpression value:
                sink.WriteInt32(4);
                sink.WriteString(value.Value);
                break;
            case VariableExpression value:
                sink.WriteInt32(5);
                sink.WriteString(value.Name);
                break;
            case UnaryExpression unary:
                sink.WriteInt32(6);
                sink.WriteInt32(GetUnaryOperatorKind(unary.Operator));
                WriteExpression(sink, unary.Operand);
                break;
            case BinaryExpression binary:
                sink.WriteInt32(7);
                WriteExpression(sink, binary.Left);
                sink.WriteInt32(GetBinaryOperatorKind(binary.Operator));
                WriteExpression(sink, binary.Right);
                break;
            case ErrorExpression:
                sink.WriteInt32(8);
                break;
            default:
                throw new InvalidOperationException($"Unknown expression {expression.GetType().Name}.");
        }
    }

    private static void WriteTransition(CanonicalSink sink, TransitionDefinition? transition)
    {
        sink.WriteBoolean(transition is not null);
        if (transition is not null)
        {
            sink.WriteString(transition.Name);
            sink.WriteDouble(transition.Duration);
        }
    }

    private static void WritePosition(CanonicalSink sink, SpritePositionDefinition? position)
    {
        sink.WriteBoolean(position is not null);
        if (position is not null)
        {
            sink.WriteInt32(GetSpritePositionKind(position.Kind));
            sink.WriteNullableString(position.Preset);
            sink.WriteDouble(position.X);
            sink.WriteDouble(position.Y);
        }
    }

    private static void WriteModifiers(CanonicalSink sink, ImmutableArray<IrModifier> modifiers)
    {
        sink.WriteInt32(modifiers.Length);
        foreach (IrModifier modifier in modifiers)
        {
            WriteModifier(sink, modifier);
        }
    }

    private static void WriteModifier(CanonicalSink sink, IrModifier? modifier)
    {
        sink.WriteBoolean(modifier is not null);
        if (modifier is not null)
        {
            sink.WriteString(modifier.Name);
            WriteArguments(sink, modifier.Arguments);
        }
    }

    private static void WriteArguments(CanonicalSink sink, ImmutableArray<IrArgument> arguments)
    {
        sink.WriteInt32(arguments.Length);
        foreach (IrArgument argument in arguments)
        {
            sink.WriteInt32(GetArgumentKind(argument.Kind));
            sink.WriteString(argument.Value);
        }
    }

    private static void WriteScreen(CanonicalSink sink, ScreenDefinition screen)
    {
        sink.WriteString(screen.Name);
        sink.WriteInt32(screen.Elements.Length);
        foreach (ScreenElementDefinition element in screen.Elements)
        {
            WriteScreenElement(sink, element);
        }
    }

    private static void WriteScreenElement(CanonicalSink sink, ScreenElementDefinition element)
    {
        sink.WriteInt32(GetScreenElementKind(element.Kind));
        sink.WriteNullableString(element.Text);
        sink.WriteNullableString(element.ImagePath);
        sink.WriteInt32(element.Properties.Length);
        foreach (ScreenPropertyDefinition property in element.Properties)
        {
            sink.WriteString(property.Name);
            WriteArguments(sink, property.Values);
        }

        sink.WriteInt32(element.Children.Length);
        foreach (ScreenElementDefinition child in element.Children)
        {
            WriteScreenElement(sink, child);
        }

        sink.WriteBoolean(element.Action is not null);
        if (element.Action is not null)
        {
            sink.WriteInt32(GetScreenActionKind(element.Action.Kind));
            sink.WriteString(element.Action.Value);
            sink.WriteNullableInstructionId(element.Action.TargetInstructionId);
        }
    }

    // Canonical tags are deliberately independent of public enum ordinals. Adding or
    // reordering API enum members must not silently rewrite persisted fingerprints.
    private static int GetGlobalDefinitionKind(GlobalDefinitionKind kind) => kind switch
    {
        GlobalDefinitionKind.Label => 0,
        GlobalDefinitionKind.Character => 1,
        GlobalDefinitionKind.Screen => 2,
        GlobalDefinitionKind.Variable => 3,
        _ => throw new InvalidOperationException($"Unknown global definition kind {kind}."),
    };

    private static int GetUnaryOperatorKind(UnaryOperator kind) => kind switch
    {
        UnaryOperator.Negate => 0,
        UnaryOperator.Not => 1,
        _ => throw new InvalidOperationException($"Unknown unary operator {kind}."),
    };

    private static int GetBinaryOperatorKind(BinaryOperator kind) => kind switch
    {
        BinaryOperator.Add => 0,
        BinaryOperator.Subtract => 1,
        BinaryOperator.Multiply => 2,
        BinaryOperator.Divide => 3,
        BinaryOperator.Equal => 4,
        BinaryOperator.NotEqual => 5,
        BinaryOperator.LessThan => 6,
        BinaryOperator.GreaterThan => 7,
        BinaryOperator.LessThanOrEqual => 8,
        BinaryOperator.GreaterThanOrEqual => 9,
        BinaryOperator.And => 10,
        BinaryOperator.Or => 11,
        _ => throw new InvalidOperationException($"Unknown binary operator {kind}."),
    };

    private static int GetSpritePositionKind(SpritePositionKind kind) => kind switch
    {
        SpritePositionKind.Preset => 0,
        SpritePositionKind.Coordinates => 1,
        _ => throw new InvalidOperationException($"Unknown sprite position kind {kind}."),
    };

    private static int GetArgumentKind(IrArgumentKind kind) => kind switch
    {
        IrArgumentKind.Identifier => 0,
        IrArgumentKind.Text => 1,
        IrArgumentKind.Number => 2,
        IrArgumentKind.Color => 3,
        IrArgumentKind.Punctuation => 4,
        _ => throw new InvalidOperationException($"Unknown IR argument kind {kind}."),
    };

    private static int GetScreenElementKind(ScreenElementKind kind) => kind switch
    {
        ScreenElementKind.Text => 0,
        ScreenElementKind.ImageButton => 1,
        ScreenElementKind.Panel => 2,
        _ => throw new InvalidOperationException($"Unknown screen element kind {kind}."),
    };

    private static int GetScreenActionKind(ScreenActionKind kind) => kind switch
    {
        ScreenActionKind.Command => 0,
        ScreenActionKind.Jump => 1,
        _ => throw new InvalidOperationException($"Unknown screen action kind {kind}."),
    };

    private sealed class CanonicalSink
    {
        private readonly ArrayBufferWriter<byte> _buffer = new();

        public ReadOnlySpan<byte> WrittenSpan => _buffer.WrittenSpan;

        public void WriteBoolean(bool value) => WriteByte(value ? (byte)1 : (byte)0);

        public void WriteInt32(int value)
        {
            Span<byte> destination = _buffer.GetSpan(sizeof(int));
            BinaryPrimitives.WriteInt32BigEndian(destination, value);
            _buffer.Advance(sizeof(int));
        }

        public void WriteDouble(double value)
        {
            Span<byte> destination = _buffer.GetSpan(sizeof(long));
            BinaryPrimitives.WriteInt64BigEndian(destination, BitConverter.DoubleToInt64Bits(value));
            _buffer.Advance(sizeof(long));
        }

        public void WriteString(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            int byteCount = Encoding.UTF8.GetByteCount(value);
            WriteInt32(byteCount);
            Span<byte> destination = _buffer.GetSpan(byteCount);
            int written = Encoding.UTF8.GetBytes(value, destination);
            _buffer.Advance(written);
        }

        public void WriteNullableString(string? value)
        {
            WriteBoolean(value is not null);
            if (value is not null)
            {
                WriteString(value);
            }
        }

        public void WriteNullableInstructionId(InstructionId? instructionId)
        {
            WriteBoolean(instructionId.HasValue);
            if (instructionId.HasValue)
            {
                WriteInt32(instructionId.Value.Ordinal);
            }
        }

        private void WriteByte(byte value)
        {
            Span<byte> destination = _buffer.GetSpan(1);
            destination[0] = value;
            _buffer.Advance(1);
        }
    }
}
