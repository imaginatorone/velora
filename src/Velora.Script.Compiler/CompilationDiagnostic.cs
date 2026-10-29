namespace Velora.Script.Compiler;

public enum CompilationDiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>A source-located diagnostic independent of parser object identity.</summary>
public sealed record CompilationDiagnostic(
    string Id,
    CompilationDiagnosticSeverity Severity,
    string Message,
    SourceLocation Source);

/// <summary>Stable diagnostic identifiers emitted while lowering syntax to IR.</summary>
public static class CompilerDiagnosticIds
{
    public const string SkippedInvalidStatement = "VEL3001";
    public const string InvalidNumericValue = "VEL3002";
    public const string InvalidExpression = "VEL3003";
    public const string InvalidScreenElement = "VEL3004";
    public const string EmptyMenu = "VEL3005";
}
