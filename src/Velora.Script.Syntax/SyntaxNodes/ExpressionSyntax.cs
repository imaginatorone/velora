namespace Velora.Script.Syntax;

public abstract class ExpressionSyntax : SyntaxNode
{
    internal ExpressionSyntax(SyntaxKind kind, SyntaxNodeContext context)
        : base(kind, context)
    {
    }
}

public sealed class LiteralExpressionSyntax : ExpressionSyntax
{
    internal LiteralExpressionSyntax(SyntaxNodeContext context, SyntaxToken literalToken)
        : base(SyntaxKind.LiteralExpression, context)
    {
        LiteralToken = literalToken;
    }

    public SyntaxToken LiteralToken { get; }
}

public sealed class NameExpressionSyntax : ExpressionSyntax
{
    internal NameExpressionSyntax(SyntaxNodeContext context, QualifiedNameSyntax name)
        : base(SyntaxKind.NameExpression, context)
    {
        Name = name;
    }

    public QualifiedNameSyntax Name { get; }

    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        yield return Name;
    }
}

public sealed class UnaryExpressionSyntax : ExpressionSyntax
{
    internal UnaryExpressionSyntax(
        SyntaxNodeContext context,
        SyntaxToken operatorToken,
        ExpressionSyntax operand)
        : base(SyntaxKind.UnaryExpression, context)
    {
        OperatorToken = operatorToken;
        Operand = operand;
    }

    public SyntaxToken OperatorToken { get; }

    public ExpressionSyntax Operand { get; }

    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        yield return Operand;
    }
}

public sealed class BinaryExpressionSyntax : ExpressionSyntax
{
    internal BinaryExpressionSyntax(
        SyntaxNodeContext context,
        ExpressionSyntax left,
        SyntaxToken operatorToken,
        ExpressionSyntax right)
        : base(SyntaxKind.BinaryExpression, context)
    {
        Left = left;
        OperatorToken = operatorToken;
        Right = right;
    }

    public ExpressionSyntax Left { get; }

    public SyntaxToken OperatorToken { get; }

    public ExpressionSyntax Right { get; }

    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        yield return Left;
        yield return Right;
    }
}

public sealed class ParenthesizedExpressionSyntax : ExpressionSyntax
{
    internal ParenthesizedExpressionSyntax(
        SyntaxNodeContext context,
        SyntaxToken openParenthesisToken,
        ExpressionSyntax expression,
        SyntaxToken closeParenthesisToken)
        : base(SyntaxKind.ParenthesizedExpression, context)
    {
        OpenParenthesisToken = openParenthesisToken;
        Expression = expression;
        CloseParenthesisToken = closeParenthesisToken;
    }

    public SyntaxToken OpenParenthesisToken { get; }

    public ExpressionSyntax Expression { get; }

    public SyntaxToken CloseParenthesisToken { get; }

    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        yield return Expression;
    }
}

public sealed class MissingExpressionSyntax : ExpressionSyntax
{
    internal MissingExpressionSyntax(SyntaxNodeContext context)
        : base(SyntaxKind.MissingExpression, context)
    {
    }
}
