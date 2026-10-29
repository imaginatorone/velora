using System.Text;
using Velora.Runtime.Contracts;
using Velora.Script.Compiler;

namespace Velora.Runtime;

public sealed partial class StoryRuntime
{
    private void ExecuteScene(ChangeSceneInstruction scene)
    {
        _backgroundPath = scene.ImagePath;
        Emit(sequence => new BackgroundChanged(sequence, scene.Id, scene.ImagePath, Transition(scene.Transition)));
    }

    private void ExecuteShowSprite(ShowSpriteInstruction show)
    {
        float x = 0.5f;
        float y = 0.5f;
        if (show.Position is { } position)
        {
            x = position.Kind == SpritePositionKind.Preset
                ? position.Preset switch { "left" => 0.25f, "right" => 0.75f, _ => 0.5f }
                : FiniteFloat(position.X);
            y = position.Kind == SpritePositionKind.Preset ? 0.5f : FiniteFloat(position.Y);
        }

        float zoom = 1;
        float rotation = 0;
        int zOrder = 0;
        bool flipped = false;
        foreach (IrModifier modifier in show.Modifiers)
        {
            switch (modifier.Name)
            {
                case "closeup": zoom = 1.5f; break;
                case "medium": zoom = 1; break;
                case "far": zoom = 0.6f; break;
                case "zoom": zoom = FiniteFloat(ModifierNumber(modifier, 1)); break;
                case "rotate": rotation = FiniteFloat(ModifierNumber(modifier, 0)); break;
                case "zorder": zOrder = (int)Math.Clamp(ModifierNumber(modifier, 0), int.MinValue, int.MaxValue); break;
                case "flipped": flipped = true; break;
            }
        }

        string path = show.ImagePath;
        if (show.IsExpressionShorthand)
        {
            path = ResolveSpriteShorthand(show.Target, show.ImagePath);
        }

        var sprite = new SpriteState
        {
            Id = show.Target, ImagePath = path, X = x, Y = y,
            ScaleX = zoom, ScaleY = zoom, Rotation = rotation, ZOrder = zOrder, Flipped = flipped,
        };
        _sprites[show.Target] = sprite;
        Emit(sequence => new SpriteShown(sequence, show.Id, sprite, Transition(show.Transition)));
    }

    private string ResolveSpriteShorthand(string character, string expression)
    {
        foreach (string prefix in new[] { "images/sprites/", "sprites/" })
        {
            foreach (string extension in new[] { ".png", ".webp", ".jpg" })
            {
                string candidate = $"{prefix}{character}/{expression}{extension}";
                if (_assetExists(candidate))
                {
                    return candidate;
                }
            }
        }

        foreach (string prefix in new[] { "images/sprites/", "sprites/" })
        {
            foreach (string extension in new[] { ".png", ".webp", ".jpg" })
            {
                string candidate = $"{prefix}{character}{extension}";
                if (_assetExists(candidate))
                {
                    return candidate;
                }
            }
        }

        return expression;
    }

    private void ExecuteHideSprite(HideSpriteInstruction hide)
    {
        string[] targets = hide.HideAll
            ? _sprites.Keys.Order(StringComparer.Ordinal).ToArray()
            : [hide.Target];
        foreach (string target in targets)
        {
            if (_sprites.Remove(target))
            {
                _activeTweens.RemoveAll(tween => tween.TargetId == target);
                Emit(sequence => new SpriteHidden(sequence, hide.Id, target, Transition(hide.Transition)));
            }
        }
    }

    private void ExecuteSay(SayInstruction say)
    {
        string speaker = say.SpeakerId ?? string.Empty;
        string name = speaker;
        string color = speaker.Length == 0 ? "#B0BEC5" : "#FFFFFF";
        if (_characters.TryGetValue(speaker, out CharacterInfo? character))
        {
            name = character.DisplayName;
            color = character.Color;
        }

        string text = _localizer.Translate(Interpolate(say.Text), "say", speaker);
        if (speaker.Length != 0)
        {
            name = _localizer.Translate(name, "character", speaker);
        }

        // Runtime owns interpolation. Text rendering must not expand substituted
        // strings a second time; unknown variables have the legacy display result "".
        _dialogue = new DialogueState(speaker, name, color, text, 0, isComplete: text.Length == 0);
        _dialogueHistory.Add(new DialogueHistoryEntry(speaker, name, color, text));
        if (_dialogueHistory.Count > MaximumDialogueHistory)
        {
            _dialogueHistory.RemoveAt(0);
        }

        _status = RuntimeExecutionStatus.WaitingForAdvance;
        Emit(sequence => new DialogueStarted(sequence, say.Id, _dialogue));
    }

    private string Interpolate(string text)
    {
        var result = new StringBuilder(text.Length);
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] == '[')
            {
                int end = text.IndexOf(']', index + 1);
                if (end >= 0)
                {
                    if (_variables.TryGetValue(text[(index + 1)..end], out RuntimeValue? value))
                    {
                        result.Append(RuntimeValueSemantics.ToInterpolationText(value));
                    }
                    index = end;
                    continue;
                }
            }

            result.Append(text[index]);
        }

        return result.ToString();
    }

    private void ExecutePlayAudio(PlayAudioInstruction play)
    {
        double volume = 1;
        double fade = 0;
        double crossfade = 0;
        foreach (IrModifier modifier in play.Modifiers)
        {
            switch (modifier.Name)
            {
                case "volume": volume = ModifierNumber(modifier, 1); break;
                case "fadein": fade = ModifierNumber(modifier, 0); break;
                case "crossfade": crossfade = ModifierNumber(modifier, 0); break;
            }
        }

        _audioTracks[play.Channel] = new AudioTrackState
        {
            Channel = play.Channel, FilePath = play.FilePath, Volume = FiniteFloat(volume), Playing = true,
        };
        var request = new AudioRequest(play.Channel, play.FilePath, FiniteFloat(volume),
            play.Channel == "bgm", ToDuration(fade, "audio fade"), ToDuration(crossfade, "audio crossfade"));
        Emit(sequence => new AudioRequested(sequence, play.Id, request));
    }

    private void ExecuteStopAudio(StopAudioInstruction stop)
    {
        _audioTracks.Remove(stop.Channel);
        var request = new AudioStopRequest(stop.Channel,
            ToDuration(stop.FadeOut is null ? 0 : ModifierNumber(stop.FadeOut, 0), "audio fade"));
        Emit(sequence => new AudioStopped(sequence, stop.Id, request));
    }

    private TransitionIntent? Transition(TransitionDefinition? definition) =>
        definition is null ? null : new TransitionIntent(definition.Name, ToDuration(definition.Duration, "transition"));

    private static double ModifierNumber(IrModifier modifier, double fallback, int index = 0)
    {
        bool negative = false;
        foreach (IrArgument argument in modifier.Arguments)
        {
            if (argument.Kind == IrArgumentKind.Punctuation)
            {
                negative |= argument.Value == "-";
                continue;
            }

            if (index-- == 0)
            {
                return RuntimeValueSemantics.TryParseLegacyNumber(argument.Value, out double value)
                    ? negative ? -value : value
                    : fallback;
            }

            negative = false;
        }

        return fallback;
    }

    private static float FiniteFloat(double value) => (float)Math.Clamp(value, -float.MaxValue, float.MaxValue);
}
