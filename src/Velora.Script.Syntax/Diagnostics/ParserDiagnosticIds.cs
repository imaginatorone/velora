namespace Velora.Script.Syntax.Diagnostics;

/// <summary>
/// Stable identifiers produced by the recoverable VelScript parser.
/// </summary>
public static class ParserDiagnosticIds
{
    public const string UnexpectedToken = "PAR001";
    public const string ExpectedToken = "PAR002";
    public const string ExpectedExpression = "PAR003";
    public const string UnterminatedBlock = "PAR004";
    public const string UnexpectedScreenElement = "PAR005";
}
