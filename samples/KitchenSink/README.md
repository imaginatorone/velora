# Velora Kitchen Sink

This project is Velora's versioned integration fixture. It is intentionally
small enough for deterministic CI runs while exercising every compatibility-era
VelScript statement family through one project.

The default route is finite: submit a name, select a difficulty, then choose
the first menu option. Optional audio paths are deliberately absent so Player
tests verify that missing audio remains non-fatal. SVG artwork is source-owned
and deterministic.

## Coverage matrix

| Area | Covered behavior |
| --- | --- |
| Language | definitions, dotted variables, all value kinds, precedence, coercion, labels, jumps, `if`/`elif`/`else`, menus, text and choice input |
| Continuations | dialogue and wait inside `if`, plus dialogue followed by statements inside a selected choice |
| Text | narration, speaker dialogue, interpolation, bold/color, inline speed/wait/shake/SFX |
| Scene | background, quoted and shorthand sprites, presets/coordinates, transforms, z-order, transitions, hide one/all |
| Audio | BGM/SFX/voice, looping intent, volume, fade-in/out, crossfade, missing optional assets |
| Animation | all legacy tween property aliases, easing, and five emotions |
| Particles | rain, snow, fire, sparkle, dust, petals, custom, named stop, stop all |
| Screens | nested panel, text, image button, jump actions, show/hide |
| Meta | save/quickbar/glitch/window/effect controls; sandboxed file, display, delete-save, and quit forms in non-default labels |
| Project | strict v1 `.velproj`, case-sensitive logical SVG assets, no generated binary files |

Future syntax must not be added opportunistically to this fixture. Update the
matrix, parser/compiler/runtime expectations, compatibility contract, and
relevant golden traces together.
