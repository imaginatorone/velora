namespace Velora.Script.Semantics;

/// <summary>
/// Describes how a symbol is introduced or written in source.
/// </summary>
public enum SymbolDefinitionKind
{
    Label,
    Character,
    Screen,
    VariableAssignment,
    InputTarget,
    Configuration,
}
