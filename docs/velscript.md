<p align="center">
  <img src="assets/velscript.svg" width="96" alt="VelScript logo">
</p>

# VelScript

VelScript is the scenario language built into Velora Engine. A script is a
UTF-8 text file with the `.vel` extension. It describes dialogue, branches,
images, audio, animation, and runtime behavior in the order they should happen.

This guide documents the grammar implemented by the current runtime. The
bundled [`example.vel`](../assets/scripts/example.vel) is kept parseable by the
test suite and is a good starting point for a new project.

## Contents

- [Quick start](#quick-start)
- [How a script runs](#how-a-script-runs)
- [Basic syntax](#basic-syntax)
- [Dialogue and text](#dialogue-and-text)
- [Variables and expressions](#variables-and-expressions)
- [Labels, conditions, and choices](#labels-conditions-and-choices)
- [Player input](#player-input)
- [Images and transitions](#images-and-transitions)
- [Audio](#audio)
- [Animation and emotion](#animation-and-emotion)
- [Particles](#particles)
- [Screens](#screens)
- [Configuration and meta commands](#configuration-and-meta-commands)
- [Command reference](#command-reference)
- [Common mistakes](#common-mistakes)

## Quick start

Create `assets/scripts/story.vel`:

```text
character guide = "Guide" color: #8FB3A2

set player.name = "Reader"

label start:
    guide "Welcome, [player.name]."
    "The lights come on one by one."

    menu:
        "Enter the observatory":
            jump observatory
        "Leave":
            jump finish
    end

label observatory:
    guide "You chose to stay."
    jump finish

label finish:
    "End of example."
```

Run it from the directory that contains the executable and `assets` folder:

```sh
VeloraEngine --script assets/scripts/story.vel
```

Asset paths written inside a script are relative to `assets`, not relative to
the script file.

## How a script runs

Velora reads the whole file, builds a label index, and then executes statements
in order. Dialogue and menus pause execution until the player responds. `jump`
moves execution to another label.

```text
label start
    |
    v
dialogue -> menu -> selected branch -> jump -> next label
```

The engine looks for `label start` when beginning a new game. Keep character
definitions, initial variables, configuration, and reusable screen definitions
above it.

## Basic syntax

### Files and comments

- New scripts use `.vel`.
- Files must be UTF-8.
- A comment starts with `--` and ends at the line break.
- One statement belongs on one line unless it opens a block.

```text
-- This line is ignored.
set visits = 1 -- This comment is ignored too.
```

### Blocks and indentation

`if`, `menu`, `screen`, and `panel` open blocks. Blocks end with `end`.
Indentation is not syntactically significant in the current grammar, but four
spaces are strongly recommended because they make nesting visible.

```text
if visits > 0:
    "Welcome back."
else:
    "Welcome."
end
```

### Names

Identifiers are case-sensitive. Use lowercase `snake_case` for labels,
variables, character IDs, and screen names:

```text
set route_name = "observatory"
label first_visit:
```

## Dialogue and text

Define a speaker once, then use its ID before a quoted line:

```text
character mira = "Mira" color: #ACCC9D

mira "You made it."
"A train passes behind the station."
```

The second line has no speaker and is shown as narration. Character colors use
`#RRGGBB` or `#RRGGBBAA`.

### Variable insertion

Put a variable name in square brackets to insert its current value into text:

```text
set player.name = "Alex"
mira "Good evening, [player.name]."
```

An unknown variable is left unchanged in the displayed text. Prefer defining
all values before the first line that uses them.

### Text styling

```text
mira "This is {bold}important{/bold}."
mira "The signal is {color:#D4B483}amber{/color}."
```

Supported style tags are `{bold}`/`{/bold}` and
`{color:#RRGGBB}`/`{/color}`.

### Inline timing and actions

Inline commands fire when the typewriter reaches them:

| Tag | Effect |
| --- | --- |
| `{wait:0.5}` | Pause text reveal for 0.5 seconds |
| `{speed:60}` | Change the reveal speed to 60 characters per second |
| `{shake_screen:0.3}` | Shake the scene for 0.3 seconds |
| `{play_sfx:audio/door.wav}` | Play a sound effect |
| `{voice:audio/line_001.ogg}` | Play a voice clip |

## Variables and expressions

Use `set` for script state and dotted names for grouping:

```text
set player.name = "Alex"
set player.trust = 2
set route.ready = true
```

VelScript has strings, numbers, booleans, and colors.

| Kind | Examples |
| --- | --- |
| String | `"station"`, `"Alex"` |
| Number | `12`, `0.75`, `-4` |
| Boolean | `true`, `false` |
| Color | `#8FB3A2`, `#8FB3A2FF` |

Expressions support the following operators, from higher to lower precedence:

| Group | Operators |
| --- | --- |
| Unary | `not`, unary `-` |
| Arithmetic | `*`, `/`, then `+`, `-` |
| Comparison | `==`, `!=`, `<`, `>`, `<=`, `>=` |
| Boolean | `and`, then `or` |

```text
set visits = visits + 1
set score = (visits * 2) - 1
set can_enter = visits >= 2 and route.ready == true
```

`+` is numeric; it does not concatenate strings. Division by zero currently
evaluates to `0`.

Three read-only values are supplied by the runtime:

| Name | Value |
| --- | --- |
| `os_username` | Current operating-system user name, or `Player` |
| `os_time` | Local time in `HH:MM` format |
| `os_locale` | Preferred operating-system locale |

## Labels, conditions, and choices

### Labels and jumps

```text
label start:
    jump platform

label platform:
    "You arrive at the platform."
```

Label names are global within the file. A missing jump target is reported at
runtime.

### Conditions

```text
if trust >= 2:
    jump good_end
elif trust == 1:
    "Maybe next time."
else:
    "The door stays closed."
end
```

Zero, an empty string, `false`, and the string `"false"` are falsey. Other
values are truthy.

### Choices

```text
menu:
    "Knock":
        set trust = trust + 1
        jump inside
    "Leave":
        jump finish
end
```

Every choice owns a block of statements. The menu closes after the selected
block has run.

## Player input

Text input stores what the player typed in a variable:

```text
input "Your name" -> player.name
```

A list of quoted lines after the command changes it into a simple selection
input. The chosen text is stored in the target variable:

```text
input "Difficulty" -> difficulty
    "Story"
    "Normal"
    "Hard"
end
```

After text input, `sys.name_gibberish` is set to a basic heuristic result that
can be used to reject obviously malformed names.

## Images and transitions

### Backgrounds

```text
scene "backgrounds/station.png"
scene "backgrounds/platform.png" with dissolve(0.8)
```

### Sprites

```text
show mira "sprites/mira.png" at left with dissolve(0.4)
show mira "sprites/mira.png" at 0.25,0.55 zoom(1.2) zorder(3) rotate(-5) flipped
hide mira with dissolve(0.3)
hide all
```

`show` uses this order:

```text
show <id> "<asset>" [at <position>] [with <transition>] [modifiers]
```

| Part | Values |
| --- | --- |
| Position | `left`, `center`, `right`, or normalized `x,y` coordinates |
| Scale preset | `closeup`, `medium`, `far` |
| Modifiers | `zoom(number)`, `zorder(number)`, `rotate(number)`, `flipped` |
| Transition | an identifier, optionally followed by a duration |

The current renderer presents named transitions as fades. Missing backgrounds
use a procedural fallback; missing sprites are skipped and reported.

## Audio

Audio paths are relative to `assets`.

```text
play bgm "audio/theme.ogg" fadein(1.0) volume(0.7)
play sfx "audio/door.wav" volume(0.9)
play voice "audio/line_001.ogg"

stop bgm fadeout(1.0)
stop sfx
stop voice
```

Channels are `bgm`, `sfx`, and `voice`. `play` accepts `fadein`, `volume`, and
`crossfade` modifiers. Volume values normally range from `0.0` to `1.0`.

## Animation and emotion

Tween a visible sprite property:

```text
tween mira x 0.7 duration(0.5) ease_out
tween mira alpha 0.0 duration(0.8) linear
tween mira rotate 12 duration(0.3) ease_in_out
```

Properties are `x`, `y`, `alpha`, `scale`/`zoom`, and
`rotation`/`rotate`. Supported easing names include `linear`, `ease_in`,
`ease_out`, `ease_in_out`, `bounce`, `elastic`, and `back`.

Short built-in reactions are available through `emotion`:

```text
emotion mira happy
emotion mira surprised
```

Available emotions are `happy`, `sad`, `surprised`, `angry`, and `nervous`.

## Particles

```text
particle rain intensity(0.4) wind(0.1)
particle fire at(0.5,0.8) radius(0.12)
particle dust intensity(0.16)

particle stop "rain"
particle stop all
```

Presets are `rain`, `snow`, `fire`, `sparkle`, `dust`, and `petals`.
`intensity`, `wind`, `at(x,y)`, and `radius` tune the emitter. Position and
radius are most useful for `fire`.

## Screens

Screens draw reusable overlay elements. Coordinates and sizes are normalized
to the window, where `0,0` is the top-left and `1,1` is the bottom-right.

```text
screen notice:
    panel at 0.1,0.1 size(0.8,0.2) alpha(0.9):
        text "Connection restored" at 0.16,0.17 color: #EDF4E9
    end
end

show_screen notice
hide_screen notice
```

Elements are `text`, `panel`, and `imagebutton`. The current screen system
renders overlays; parsed `action` values are reserved for future interactive
screen handling.

## Configuration and meta commands

### Configuration

```text
config text_speed = 34
config ui_accent_color = "#8FB3A2"
config game_locale = "en"
```

These three keys affect the runtime directly. Other keys remain available as
variables under `__config.<key>`.

### Meta commands

Meta commands control engine behavior outside the story flow:

| Command | Purpose |
| --- | --- |
| `meta set_title "My Story"` | Change the window title |
| `meta lock_save` / `meta unlock_save` | Disable or enable saving |
| `meta hide_quickbar` / `meta show_quickbar` | Hide or show quick controls |
| `meta fullscreen` / `meta windowed` | Change display mode |
| `meta shake_window 0.3` | Shake the native window |
| `meta glitch_text 0.5` | Apply a temporary text glitch |
| `meta glitch_ui 0.5` | Apply a temporary interface glitch |
| `meta delete_save 2` | Delete save slot 2 |
| `meta main_menu` | Return to the main menu |
| `meta quit` | Close the engine |

Post-processing uses `meta effect`:

```text
meta effect vignette 0.6 2.0
meta effect glitch 0.4 0.5
meta effect clear
```

Effect names are `blur`, `vignette`, `crt`, `glitch`, `fade`, and
`color_grade`. Arguments are intensity and duration.

`meta file_write "state.txt" "value"` and
`meta file_delete "state.txt"` are confined to Velora's per-user
`script-data` directory. Absolute paths and parent traversal are rejected.

## Command reference

| Task | Form |
| --- | --- |
| Define a character | `character id = "Name" color: #RRGGBB` |
| Speak | `id "Dialogue"` |
| Narrate | `"Narration"` |
| Set a value | `set name = expression` |
| Mark a location | `label name:` |
| Jump | `jump name` |
| Branch | `if expression:` ... `elif` ... `else` ... `end` |
| Present choices | `menu:` ... `end` |
| Read input | `input "Prompt" -> variable` |
| Change background | `scene "path" [with transition]` |
| Show a sprite | `show id "path" [at position] [with transition]` |
| Hide sprites | `hide id` or `hide all` |
| Play audio | `play bgm|sfx|voice "path"` |
| Stop audio | `stop bgm|sfx|voice` |
| Pause execution | `wait seconds` |
| Animate a sprite | `tween id property value duration(seconds) easing` |
| Trigger a reaction | `emotion id name` |
| Start particles | `particle preset [parameters]` |
| Stop particles | `particle stop name|all` |
| Register an overlay | `screen name:` ... `end` |
| Show or hide an overlay | `show_screen name`, `hide_screen name` |
| Configure the runtime | `config key = value` |
| Control the engine | `meta action [arguments]` |

## Common mistakes

### Forgetting `end`

```text
-- Wrong: the block never closes.
if ready:
    jump next

-- Correct.
if ready:
    jump next
end
```

### Putting modifiers in the wrong order

```text
-- Position and transition come before sprite modifiers.
show mira "sprites/mira.png" at left with dissolve(0.4) zoom(1.1) flipped
```

### Treating indentation as grammar

Indentation documents structure for people, but `end` controls structure for
the current parser. Always keep both correct.

### Using a path relative to the script

If a file is stored at `assets/backgrounds/station.png`, write
`"backgrounds/station.png"` even when the script lives in a nested folder.

### Expecting string concatenation

Use `[variable]` interpolation inside dialogue. Arithmetic `+` converts values
to numbers and is not a string concatenation operator.
