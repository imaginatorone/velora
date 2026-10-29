using Velora.Script.Syntax.Text;

namespace Velora.Script.Syntax.Diagnostics;

/// <summary>
/// A stable, source-located language diagnostic.
/// </summary>
public sealed record Diagnostic(
    string Id,
    DiagnosticSeverity Severity,
    string Message,
    TextSpan Span);
