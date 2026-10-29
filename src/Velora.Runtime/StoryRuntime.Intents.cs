using Velora.Runtime.Contracts;
using Velora.Script.Compiler;

namespace Velora.Runtime;

public sealed partial class StoryRuntime
{
    private void ExecuteShowScreen(ShowScreenInstruction instruction)
    {
        if (!_screens.ContainsKey(instruction.ScreenName))
        {
            Warn(RuntimeWarningCodes.MissingScreen, $"Screen '{instruction.ScreenName}' is not registered.");
            return;
        }

        _activeScreens.Add(instruction.ScreenName);
        Emit(sequence => new ScreenVisibilityChanged(sequence, instruction.Id, instruction.ScreenName, true));
    }

    private void ExecuteHideScreen(HideScreenInstruction instruction)
    {
        _activeScreens.Remove(instruction.ScreenName);
        Emit(sequence => new ScreenVisibilityChanged(sequence, instruction.Id, instruction.ScreenName, false));
    }

    private void ExecuteParticle(ParticleInstruction instruction)
    {
        if (instruction.Preset == "stop")
        {
            string name = instruction.StopAll ? string.Empty : instruction.Name ?? string.Empty;
            if (instruction.StopAll)
            {
                _particles.Clear();
            }
            else
            {
                _particles.RemoveAll(emitter => emitter.Name == name);
            }

            Emit(sequence => new ParticleChanged(sequence, instruction.Id, name, null));
            return;
        }

        if (instruction.Preset is not ("rain" or "snow" or "fire" or "sparkle" or "dust" or "petals" or "custom"))
        {
            Warn(RuntimeWarningCodes.UnknownParticlePreset, $"Unknown particle preset '{instruction.Preset}'.");
            return;
        }

        double intensity = 1, wind = 0, x = 0.5, y = 0.8, radius = 0.1;
        foreach (IrModifier modifier in instruction.Parameters)
        {
            switch (modifier.Name)
            {
                case "intensity": intensity = ModifierNumber(modifier, intensity); break;
                case "wind": wind = ModifierNumber(modifier, wind); break;
                case "radius": radius = ModifierNumber(modifier, radius); break;
                case "at":
                    x = ModifierNumber(modifier, x);
                    y = ModifierNumber(modifier, y, 1);
                    break;
            }
        }

        string emitterName = string.IsNullOrEmpty(instruction.Name) ? instruction.Preset : instruction.Name;
        var emitter = new ParticleEmitterState(emitterName, instruction.Preset, intensity, wind, x, y, radius);
        _particles.Add(emitter);
        Emit(sequence => new ParticleChanged(sequence, instruction.Id, emitterName, emitter));
    }

    private void ExecuteMeta(ExecuteMetaInstruction instruction)
    {
        string[] args = instruction.Arguments.Select(static argument => argument.Value).ToArray();
        switch (instruction.Action)
        {
            case "set_title" when args.Length > 0:
                _title = args[0];
                Emit(sequence => new TitleChanged(sequence, instruction.Id, _title));
                return;
            case "set_title":
                return;
            case "lock_save":
            case "unlock_save":
                _isSaveLocked = instruction.Action == "lock_save";
                Emit(sequence => new SaveLockChanged(sequence, instruction.Id, _isSaveLocked));
                return;
            case "quit":
            case "exit":
                Emit(sequence => new QuitRequested(sequence, instruction.Id));
                return;
            case "main_menu":
                Emit(sequence => new MainMenuRequested(sequence, instruction.Id));
                return;
            case "effect":
                ExecuteEffect(instruction, args);
                return;
        }

        HostAction? action = instruction.Action switch
        {
            "file_write" => HostAction.WriteScriptData,
            "file_delete" => HostAction.DeleteScriptData,
            "delete_save" => HostAction.DeleteSave,
            "fullscreen" => HostAction.Fullscreen,
            "windowed" => HostAction.Windowed,
            "hide_quickbar" => HostAction.HideQuickbar,
            "show_quickbar" => HostAction.ShowQuickbar,
            "glitch_text" => HostAction.GlitchText,
            "glitch_ui" => HostAction.GlitchUi,
            "shake_window" => HostAction.ShakeWindow,
            _ => null,
        };
        if (action is { } hostAction)
        {
            Emit(sequence => new HostActionRequested(sequence, instruction.Id, hostAction, args));
        }
        else
        {
            Warn(RuntimeWarningCodes.UnknownMetaCommand, $"Unknown meta command '{instruction.Action}'.");
        }
    }

    private void ExecuteEffect(ExecuteMetaInstruction instruction, string[] args)
    {
        if (args.Length == 0)
        {
            return;
        }

        if (args[0] == "clear")
        {
            Emit(sequence => new EffectRequested(sequence, instruction.Id,
                new EffectRequest(EffectOperation.ClearAll, string.Empty, 0, TimeSpan.Zero)));
            return;
        }

        if (args[0] is not ("blur" or "vignette" or "crt" or "glitch" or "fade" or "color_grade"))
        {
            Warn(RuntimeWarningCodes.UnsupportedIntent, $"Unknown effect '{args[0]}'.");
            return;
        }

        double Number(int index, double fallback) => index < args.Length &&
            RuntimeValueSemantics.TryParseLegacyNumber(args[index], out double value) ? value : fallback;
        var parameters = new Dictionary<string, double>(StringComparer.Ordinal);
        if (args.Length >= 6 && args[0] is "fade" or "color_grade")
        {
            parameters["r"] = Math.Clamp(Number(3, 0), 0, 1);
            parameters["g"] = Math.Clamp(Number(4, 0), 0, 1);
            parameters["b"] = Math.Clamp(Number(5, 0), 0, 1);
        }

        var request = new EffectRequest(EffectOperation.Apply, args[0], Math.Clamp(Number(1, 1), 0, 1),
            ToDuration(Math.Max(0, Number(2, 0)), "effect duration"), parameters);
        Emit(sequence => new EffectRequested(sequence, instruction.Id, request));
    }
}
