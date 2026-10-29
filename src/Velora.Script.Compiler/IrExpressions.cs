namespace Velora.Script.Compiler;

/// <summary>Base type for stable, syntax-independent VelScript expression IR.</summary>
public abstract record VeloraExpression(SourceLocation Source);

public sealed record StringExpression(string Value, SourceLocation Source) : VeloraExpression(Source);

public sealed record NumberExpression(double Value, SourceLocation Source) : VeloraExpression(Source);

public sealed record BooleanExpression(bool Value, SourceLocation Source) : VeloraExpression(Source);

public sealed record ColorExpression(string Value, SourceLocation Source) : VeloraExpression(Source);

public sealed record VariableExpression(string Name, SourceLocation Source) : VeloraExpression(Source);

public enum UnaryOperator
{
    Negate,
    Not,
}

public sealed record UnaryExpression(
    UnaryOperator Operator,
    VeloraExpression Operand,
    SourceLocation Source) : VeloraExpression(Source);

public enum BinaryOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Equal,
    NotEqual,
    LessThan,
    GreaterThan,
    LessThanOrEqual,
    GreaterThanOrEqual,
    And,
    Or,
}

public sealed record BinaryExpression(
    VeloraExpression Left,
    BinaryOperator Operator,
    VeloraExpression Right,
    SourceLocation Source) : VeloraExpression(Source);

/// <summary>Recoverable placeholder produced when the source has no usable expression.</summary>
public sealed record ErrorExpression(SourceLocation Source) : VeloraExpression(Source);
