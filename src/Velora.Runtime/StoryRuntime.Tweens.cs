using Velora.Runtime.Contracts;
using Velora.Script.Compiler;

namespace Velora.Runtime;

public sealed partial class StoryRuntime
{
    private void ExecuteTween(StartTweenInstruction tween)
    {
        if (!TrySpriteProperty(tween.Target, tween.Property, out double from) ||
            !RuntimeValueSemantics.TryToFiniteNumber(Evaluate(tween.Value), out double to))
        {
            Warn(RuntimeWarningCodes.InvalidTween, $"Invalid tween target, property or value: {tween.Target}.{tween.Property}.");
            return;
        }

        StartTween(tween.Target, tween.Property, from, to, tween.DurationSeconds, tween.Easing);
    }

    private void StartTween(string target, string property, double from, double to, double seconds,
        string easing, double? returnTo = null)
    {
        _activeTweens.RemoveAll(tween => tween.TargetId == target);
        _activeTweens.Add(new TweenState(_nextEventSequence, target, property, from, FiniteFloat(to), from,
            ToDuration(Math.Max(0.001, seconds), "tween duration"), TimeSpan.Zero, easing, returnTo));
    }

    private void ExecuteEmotion(TriggerEmotionInstruction emotion)
    {
        if (emotion.Emotion is "angry" or "nervous")
        {
            var request = new EffectRequest(EffectOperation.Apply, "shake_screen", 1, TimeSpan.FromSeconds(0.25),
                new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    ["amplitude"] = emotion.Emotion == "angry" ? 8 : 3,
                });
            Emit(sequence => new EffectRequested(sequence, emotion.Id, request));
            return;
        }

        (string property, double delta, double seconds) = emotion.Emotion switch
        {
            "happy" => ("y", -0.025, 0.2),
            "sad" => ("alpha", -0.2, 0.6),
            "surprised" => ("scale", 0.08, 0.2),
            _ => (string.Empty, 0, 0),
        };
        if (property.Length == 0)
        {
            Warn(RuntimeWarningCodes.UnknownEmotion, $"Unknown emotion '{emotion.Emotion}'.");
        }
        else if (TrySpriteProperty(emotion.CharacterId, property, out double from))
        {
            StartTween(emotion.CharacterId, property, from, from + delta, seconds, "ease_out", from);
        }
    }

    private void UpdateTweens(TimeSpan delta)
    {
        for (int index = _activeTweens.Count - 1; index >= 0; index--)
        {
            TweenState tween = _activeTweens[index];
            if (!_sprites.ContainsKey(tween.TargetId))
            {
                _activeTweens.RemoveAt(index);
                continue;
            }

            TimeSpan available = tween.Duration - tween.Elapsed;
            TimeSpan elapsed = delta >= available ? tween.Duration : tween.Elapsed + delta;
            double amount = TweenEasing.Evaluate(tween.Easing, elapsed.TotalSeconds / tween.Duration.TotalSeconds);
            double value = tween.From + (tween.To - tween.From) * amount;
            if (elapsed == tween.Duration && tween.ReturnTo is { } returnTo)
            {
                TimeSpan remainder = delta - available;
                elapsed = remainder >= tween.Duration ? tween.Duration : remainder;
                value = tween.To + (returnTo - tween.To) *
                    TweenEasing.Evaluate("ease_in_out", elapsed.TotalSeconds / tween.Duration.TotalSeconds);
                tween = new TweenState(tween.Id, tween.TargetId, tween.Property, tween.To, returnTo,
                    value, tween.Duration, elapsed, "ease_in_out");
            }
            else
            {
                tween = new TweenState(tween.Id, tween.TargetId, tween.Property, tween.From, tween.To,
                    value, tween.Duration, elapsed, tween.Easing, tween.ReturnTo);
            }

            SetSpriteProperty(tween.TargetId, tween.Property, value);
            if (tween.IsComplete)
            {
                _activeTweens.RemoveAt(index);
            }
            else
            {
                _activeTweens[index] = tween;
            }
        }
    }

    private bool TrySpriteProperty(string target, string property, out double value)
    {
        value = 0;
        if (!_sprites.TryGetValue(target, out SpriteState? sprite))
        {
            return false;
        }

        double? found = property switch
        {
            "x" => sprite.X, "y" => sprite.Y, "alpha" => sprite.Alpha,
            "scale" or "zoom" => sprite.ScaleX, "rotation" or "rotate" => sprite.Rotation,
            _ => null,
        };
        value = found ?? 0;
        return found.HasValue;
    }

    private void SetSpriteProperty(string target, string property, double value)
    {
        SpriteState sprite = _sprites[target];
        float number = FiniteFloat(value);
        _sprites[target] = property switch
        {
            "x" => sprite with { X = number },
            "y" => sprite with { Y = number },
            "alpha" => sprite with { Alpha = Math.Clamp(number, 0, 1) },
            "scale" or "zoom" => sprite with { ScaleX = number, ScaleY = number },
            "rotation" or "rotate" => sprite with { Rotation = number },
            _ => sprite,
        };
        Emit(sequence => new SpritePropertyChanged(sequence, _currentInstruction, target, property, number));
    }
}
