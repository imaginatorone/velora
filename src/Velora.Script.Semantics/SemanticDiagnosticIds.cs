namespace Velora.Script.Semantics;

/// <summary>
/// Stable identifiers emitted by VelScript semantic analysis.
/// </summary>
public static class SemanticDiagnosticIds
{
    public const string DuplicateLabel = "VEL2001";
    public const string DuplicateCharacter = "VEL2002";
    public const string DuplicateScreen = "VEL2003";
    public const string UndefinedLabel = "VEL2004";
    public const string UndefinedCharacter = "VEL2005";
    public const string UndefinedScreen = "VEL2006";
    public const string UndefinedVariable = "VEL2007";
    public const string NestedLabel = "VEL2008";
}
