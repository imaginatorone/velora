# Using Velora inside Godot .NET

Velora C# is a **headless narrative runtime**, not a custom build of Godot,
a Godot plugin, or a replacement for Godot's physics and rendering.

A working example host exists locally in the **DreadfulDream** Godot
project. Keep the C# Velora checkout accessible to the .NET project; add
\`ProjectReference\` entries for the parser, semantic binder, compiler,
runtime, and dependencies (or distribute the libraries as NuGet packages later).

Host flow:

1. Godot loads a \`.vel\` script as UTF-8 text.
2. Call \`SyntaxTree.ParseText(scriptText, path)\`.
3. Call \`SemanticModel.Create(tree)\`.
4. Call \`VeloraCompiler.Compile(model)\` and check \`Succeeded\`.
5. Create \`StoryRuntime(compilation.Program, platformInfo, textLocalizer)\`.
6. Call \`Start\` on interaction, \`Advance\` after dialogue, and
   \`SelectChoice(index)\` when the player selects a response.
7. Use the returned \`DialogueStarted\` / \`ChoiceRequested\` events,
   or \`CaptureState().Dialogue\` / \`CaptureState().Execution.Choice\`
   to populate Godot's UI. State is headless and deterministic.
8. In the host, animate portraits and typewriter text; route character audio
   via the host's own sound players. These features do not alter Velora IR.

\`\`\`csharp
var syntax = SyntaxTree.ParseText(source, "res://Stories/first_meeting.vel");
var compilation = VeloraCompiler.Compile(SemanticModel.Create(syntax));
if (!compilation.Succeeded) throw new InvalidOperationException("Bad VelScript");
var story = new StoryRuntime(compilation.Program, platformInfo, textLocalizer);
var events = story.Start();
// Render events or story.CaptureState().Dialogue in the game's UI.
\`\`\`

The required interfaces \`IPlatformInfo\` and \`ITextLocalizer\` are
implemented by the host. This allows the same compiled narrative to run in
Godot, in Velora's CLI, or in the future standalone Velora Player.

**Limitations:** the C# Player/Editor parity is unfinished; the current
integration does not promise Godot export support to mobile or consoles.
Do not copy Velora sources into a game to avoid divergent forks.
