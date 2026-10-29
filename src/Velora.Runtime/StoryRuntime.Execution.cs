using Velora.Runtime.Contracts;
using Velora.Script.Compiler;

namespace Velora.Runtime;

public sealed partial class StoryRuntime
{
    private void ExecuteInstruction(InstructionId instructionId)
    {
        VeloraInstruction instruction = _program.GetInstruction(instructionId);
        MoveExecutionPosition(instruction);
        _nextInstruction = instruction.ContinuationInstructionId;

        switch (instruction)
        {
            case DefineCharacterInstruction character:
                _definitionInstructions[$"character:{character.CharacterId}"] = character.Id;
                _characters[character.CharacterId] = new CharacterInfo(
                    character.DisplayName,
                    character.Color);
                break;

            case LabelInstruction:
                break;

            case ChangeSceneInstruction scene:
                ExecuteScene(scene);
                break;

            case ShowSpriteInstruction show:
                ExecuteShowSprite(show);
                break;

            case HideSpriteInstruction hide:
                ExecuteHideSprite(hide);
                break;

            case SayInstruction say:
                ExecuteSay(say);
                break;

            case PlayAudioInstruction play:
                ExecutePlayAudio(play);
                break;

            case StopAudioInstruction stop:
                ExecuteStopAudio(stop);
                break;

            case WaitInstruction wait:
                ExecuteWait(wait);
                break;

            case SetVariableInstruction set:
                SetVariable(set.Name, Evaluate(set.Value), set.Id);
                break;

            case JumpInstruction jump:
                ExecuteJump(jump);
                break;

            case PresentChoiceInstruction menu:
                ExecuteChoice(menu);
                break;

            case BranchInstruction branch:
                ExecuteBranch(branch);
                break;

            case SetConfigInstruction config:
                SetVariable($"__config.{config.Key}", Evaluate(config.Value), config.Id);
                break;

            case StartTweenInstruction tween:
                ExecuteTween(tween);
                break;

            case TriggerEmotionInstruction emotion:
                ExecuteEmotion(emotion);
                break;

            case RequestInputInstruction input:
                ExecuteInput(input);
                break;

            case ExecuteMetaInstruction meta:
                ExecuteMeta(meta);
                break;

            case ParticleInstruction particle:
                ExecuteParticle(particle);
                break;

            case RegisterScreenInstruction register:
                _definitionInstructions[$"screen:{register.Screen.Name}"] = register.Id;
                _screens[register.Screen.Name] = register.Screen;
                break;

            case ShowScreenInstruction showScreen:
                ExecuteShowScreen(showScreen);
                break;

            case HideScreenInstruction hideScreen:
                ExecuteHideScreen(hideScreen);
                break;

            default:
                Warn(
                    RuntimeWarningCodes.UnsupportedIntent,
                    $"Instruction '{instruction.GetType().Name}' has no runtime implementation; execution continues.");
                break;
        }
    }

    private void ExecuteWait(WaitInstruction wait)
    {
        TimeSpan duration = ToDuration(wait.DurationSeconds, "wait duration");
        _wait = new WaitState(duration, duration);
        _status = RuntimeExecutionStatus.WaitingForTime;
    }

    private void ExecuteJump(JumpInstruction jump)
    {
        if (jump.TargetInstructionId is null)
        {
            Warn(
                RuntimeWarningCodes.MissingJumpTarget,
                $"Label '{jump.LabelName}' was not found; execution continues after the jump.");
            return;
        }

        _currentLabel = jump.LabelName;
        _nextInstruction = jump.TargetInstructionId;
    }

    private void ExecuteChoice(PresentChoiceInstruction menu)
    {
        if (menu.Choices.Length == 0)
        {
            Warn(
                CompilerDiagnosticIds.EmptyMenu,
                "An empty menu was skipped so that execution can continue.");
            return;
        }

        _choice = new ChoiceState(menu.Choices.Select(static (choice, index) =>
            new ChoiceOption(index, choice.Text)));
        _status = RuntimeExecutionStatus.WaitingForChoice;
        Emit(sequence => new ChoiceRequested(sequence, menu.Id, _choice));
    }

    private void ExecuteBranch(BranchInstruction branch)
    {
        foreach (ConditionalTarget target in branch.Branches)
        {
            if (RuntimeValueSemantics.IsTruthy(Evaluate(target.Condition)))
            {
                _nextInstruction = target.TargetInstructionId;
                return;
            }
        }

        _nextInstruction = branch.ElseTargetInstructionId ?? branch.ContinuationInstructionId;
    }

    private void ExecuteInput(RequestInputInstruction input)
    {
        if (input.Options.Length != 0)
        {
            _choice = new ChoiceState(input.Options.Select(static (option, index) =>
                new ChoiceOption(index, option)));
            _status = RuntimeExecutionStatus.WaitingForChoice;
            Emit(sequence => new ChoiceRequested(sequence, input.Id, _choice));
            return;
        }

        _textInput = new TextInputState(input.TargetVariable, input.Prompt);
        _status = RuntimeExecutionStatus.WaitingForTextInput;
        Emit(sequence => new TextInputRequested(sequence, input.Id, _textInput));
    }

    private RuntimeValue Evaluate(VeloraExpression expression) => expression switch
    {
        StringExpression text => new RuntimeValue.StringValue(text.Value),
        NumberExpression number => new RuntimeValue.NumberValue(number.Value),
        BooleanExpression boolean => new RuntimeValue.BooleanValue(boolean.Value),
        ColorExpression color => new RuntimeValue.StringValue(color.Value),
        VariableExpression variable => EvaluateVariable(variable.Name),
        UnaryExpression unary => EvaluateUnary(unary),
        BinaryExpression binary => EvaluateBinary(binary),
        ErrorExpression => EvaluateError(),
        _ => EvaluateError(),
    };

    private RuntimeValue EvaluateVariable(string name)
    {
        return name switch
        {
            "os_username" => new RuntimeValue.StringValue(_platformInfo.UserName ?? string.Empty),
            "os_time" => new RuntimeValue.StringValue(_platformInfo.LocalTimeText ?? string.Empty),
            "os_locale" => new RuntimeValue.StringValue(_platformInfo.PreferredLocale ?? string.Empty),
            _ when _variables.TryGetValue(name, out RuntimeValue? value) => value,
            _ => new RuntimeValue.StringValue(string.Empty),
        };
    }

    private RuntimeValue EvaluateUnary(UnaryExpression unary)
    {
        RuntimeValue operand = Evaluate(unary.Operand);
        return unary.Operator switch
        {
            UnaryOperator.Not => new RuntimeValue.BooleanValue(!RuntimeValueSemantics.IsTruthy(operand)),
            UnaryOperator.Negate => new RuntimeValue.NumberValue(-RuntimeValueSemantics.ToNumber(operand)),
            _ => EvaluateError(),
        };
    }

    private RuntimeValue EvaluateBinary(BinaryExpression binary)
    {
        RuntimeValue left = Evaluate(binary.Left);
        if (binary.Operator == BinaryOperator.And && !RuntimeValueSemantics.IsTruthy(left))
        {
            return new RuntimeValue.BooleanValue(false);
        }

        if (binary.Operator == BinaryOperator.Or && RuntimeValueSemantics.IsTruthy(left))
        {
            return new RuntimeValue.BooleanValue(true);
        }

        RuntimeValue right = Evaluate(binary.Right);
        return binary.Operator switch
        {
            BinaryOperator.Equal => new RuntimeValue.BooleanValue(
                RuntimeValueSemantics.ToEqualityText(left) == RuntimeValueSemantics.ToEqualityText(right)),
            BinaryOperator.NotEqual => new RuntimeValue.BooleanValue(
                RuntimeValueSemantics.ToEqualityText(left) != RuntimeValueSemantics.ToEqualityText(right)),
            BinaryOperator.LessThan => new RuntimeValue.BooleanValue(
                RuntimeValueSemantics.ToNumber(left) < RuntimeValueSemantics.ToNumber(right)),
            BinaryOperator.GreaterThan => new RuntimeValue.BooleanValue(
                RuntimeValueSemantics.ToNumber(left) > RuntimeValueSemantics.ToNumber(right)),
            BinaryOperator.LessThanOrEqual => new RuntimeValue.BooleanValue(
                RuntimeValueSemantics.ToNumber(left) <= RuntimeValueSemantics.ToNumber(right)),
            BinaryOperator.GreaterThanOrEqual => new RuntimeValue.BooleanValue(
                RuntimeValueSemantics.ToNumber(left) >= RuntimeValueSemantics.ToNumber(right)),
            BinaryOperator.And or BinaryOperator.Or => new RuntimeValue.BooleanValue(
                RuntimeValueSemantics.IsTruthy(right)),
            BinaryOperator.Add => FiniteNumber(
                RuntimeValueSemantics.ToNumber(left) + RuntimeValueSemantics.ToNumber(right)),
            BinaryOperator.Subtract => FiniteNumber(
                RuntimeValueSemantics.ToNumber(left) - RuntimeValueSemantics.ToNumber(right)),
            BinaryOperator.Multiply => FiniteNumber(
                RuntimeValueSemantics.ToNumber(left) * RuntimeValueSemantics.ToNumber(right)),
            BinaryOperator.Divide => Divide(left, right),
            _ => EvaluateError(),
        };
    }

    private RuntimeValue.NumberValue Divide(RuntimeValue left, RuntimeValue right)
    {
        double denominator = RuntimeValueSemantics.ToNumber(right);
        return denominator == 0
            ? new RuntimeValue.NumberValue(0)
            : FiniteNumber(RuntimeValueSemantics.ToNumber(left) / denominator);
    }

    private RuntimeValue.NumberValue FiniteNumber(double value)
    {
        if (double.IsFinite(value))
        {
            return new RuntimeValue.NumberValue(value);
        }

        Warn(
            RuntimeWarningCodes.InvalidExpression,
            "A non-finite numeric result was replaced with zero.");
        return new RuntimeValue.NumberValue(0);
    }

    private RuntimeValue.StringValue EvaluateError()
    {
        Warn(
            RuntimeWarningCodes.InvalidExpression,
            "A recoverable error expression evaluated to an empty string.");
        return new RuntimeValue.StringValue(string.Empty);
    }

    private TimeSpan ToDuration(double seconds, string context)
    {
        if (!double.IsFinite(seconds) || seconds < 0)
        {
            Warn(
                RuntimeWarningCodes.InvalidArgument,
                $"Invalid {context}; zero was used.");
            return TimeSpan.Zero;
        }

        if (seconds >= TimeSpan.MaxValue.TotalSeconds)
        {
            Warn(
                RuntimeWarningCodes.InvalidArgument,
                $"The {context} exceeded the supported range and was clamped.");
            return TimeSpan.MaxValue;
        }

        return TimeSpan.FromSeconds(seconds);
    }
}
