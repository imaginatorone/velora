using System.Collections.ObjectModel;

namespace Velora.Script.Syntax;

public sealed class ChoiceClauseSyntax : SyntaxNode
{
    private readonly ReadOnlyCollection<StatementSyntax> _statements;

    internal ChoiceClauseSyntax(
        SyntaxNodeContext context,
        SyntaxToken textToken,
        SyntaxToken colonToken,
        StatementSyntax[] statements)
        : base(SyntaxKind.ChoiceClause, context)
    {
        TextToken = textToken;
        ColonToken = colonToken;
        _statements = Array.AsReadOnly(statements);
    }

    public SyntaxToken TextToken { get; }

    public SyntaxToken ColonToken { get; }

    public IReadOnlyList<StatementSyntax> Statements => _statements;

    public override IEnumerable<SyntaxNode> ChildNodes() => _statements;
}

public sealed class MenuStatementSyntax : StatementSyntax
{
    private readonly ReadOnlyCollection<ChoiceClauseSyntax> _choices;

    internal MenuStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken menuKeyword,
        SyntaxToken colonToken,
        ChoiceClauseSyntax[] choices,
        SyntaxToken endKeyword)
        : base(SyntaxKind.MenuStatement, context)
    {
        MenuKeyword = menuKeyword;
        ColonToken = colonToken;
        _choices = Array.AsReadOnly(choices);
        EndKeyword = endKeyword;
    }

    public SyntaxToken MenuKeyword { get; }

    public SyntaxToken ColonToken { get; }

    public IReadOnlyList<ChoiceClauseSyntax> Choices => _choices;

    public SyntaxToken EndKeyword { get; }

    public override IEnumerable<SyntaxNode> ChildNodes() => _choices;
}

public sealed class ElifClauseSyntax : SyntaxNode
{
    private readonly ReadOnlyCollection<StatementSyntax> _statements;

    internal ElifClauseSyntax(
        SyntaxNodeContext context,
        SyntaxToken elifKeyword,
        ExpressionSyntax condition,
        SyntaxToken colonToken,
        StatementSyntax[] statements)
        : base(SyntaxKind.ElifClause, context)
    {
        ElifKeyword = elifKeyword;
        Condition = condition;
        ColonToken = colonToken;
        _statements = Array.AsReadOnly(statements);
    }

    public SyntaxToken ElifKeyword { get; }

    public ExpressionSyntax Condition { get; }

    public SyntaxToken ColonToken { get; }

    public IReadOnlyList<StatementSyntax> Statements => _statements;

    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        yield return Condition;
        foreach (StatementSyntax statement in _statements)
        {
            yield return statement;
        }
    }
}

public sealed class ElseClauseSyntax : SyntaxNode
{
    private readonly ReadOnlyCollection<StatementSyntax> _statements;

    internal ElseClauseSyntax(
        SyntaxNodeContext context,
        SyntaxToken elseKeyword,
        SyntaxToken colonToken,
        StatementSyntax[] statements)
        : base(SyntaxKind.ElseClause, context)
    {
        ElseKeyword = elseKeyword;
        ColonToken = colonToken;
        _statements = Array.AsReadOnly(statements);
    }

    public SyntaxToken ElseKeyword { get; }

    public SyntaxToken ColonToken { get; }

    public IReadOnlyList<StatementSyntax> Statements => _statements;

    public override IEnumerable<SyntaxNode> ChildNodes() => _statements;
}

public sealed class IfStatementSyntax : StatementSyntax
{
    private readonly ReadOnlyCollection<StatementSyntax> _thenStatements;
    private readonly ReadOnlyCollection<ElifClauseSyntax> _elifClauses;

    internal IfStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken ifKeyword,
        ExpressionSyntax condition,
        SyntaxToken colonToken,
        StatementSyntax[] thenStatements,
        ElifClauseSyntax[] elifClauses,
        ElseClauseSyntax? elseClause,
        SyntaxToken endKeyword)
        : base(SyntaxKind.IfStatement, context)
    {
        IfKeyword = ifKeyword;
        Condition = condition;
        ColonToken = colonToken;
        _thenStatements = Array.AsReadOnly(thenStatements);
        _elifClauses = Array.AsReadOnly(elifClauses);
        ElseClause = elseClause;
        EndKeyword = endKeyword;
    }

    public SyntaxToken IfKeyword { get; }

    public ExpressionSyntax Condition { get; }

    public SyntaxToken ColonToken { get; }

    public IReadOnlyList<StatementSyntax> ThenStatements => _thenStatements;

    public IReadOnlyList<ElifClauseSyntax> ElifClauses => _elifClauses;

    public ElseClauseSyntax? ElseClause { get; }

    public SyntaxToken EndKeyword { get; }

    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        yield return Condition;
        foreach (StatementSyntax statement in _thenStatements)
        {
            yield return statement;
        }

        foreach (ElifClauseSyntax clause in _elifClauses)
        {
            yield return clause;
        }

        if (ElseClause is not null)
        {
            yield return ElseClause;
        }
    }
}

public sealed class InputOptionSyntax : SyntaxNode
{
    internal InputOptionSyntax(SyntaxNodeContext context, SyntaxToken textToken)
        : base(SyntaxKind.InputOption, context)
    {
        TextToken = textToken;
    }

    public SyntaxToken TextToken { get; }
}

public sealed class InputStatementSyntax : StatementSyntax
{
    private readonly ReadOnlyCollection<InputOptionSyntax> _options;

    internal InputStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken inputKeyword,
        SyntaxToken promptToken,
        SyntaxToken arrowToken,
        QualifiedNameSyntax target,
        InputOptionSyntax[] options,
        SyntaxToken? endKeyword)
        : base(SyntaxKind.InputStatement, context)
    {
        InputKeyword = inputKeyword;
        PromptToken = promptToken;
        ArrowToken = arrowToken;
        Target = target;
        _options = Array.AsReadOnly(options);
        EndKeyword = endKeyword;
    }

    public SyntaxToken InputKeyword { get; }

    public SyntaxToken PromptToken { get; }

    public SyntaxToken ArrowToken { get; }

    public QualifiedNameSyntax Target { get; }

    public IReadOnlyList<InputOptionSyntax> Options => _options;

    public SyntaxToken? EndKeyword { get; }

    public bool IsChoice => _options.Count != 0;

    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        yield return Target;
        foreach (InputOptionSyntax option in _options)
        {
            yield return option;
        }
    }
}
