using System.Collections.ObjectModel;

namespace Velora.Script.Syntax;

public abstract class StatementSyntax : SyntaxNode
{
    internal StatementSyntax(SyntaxKind kind, SyntaxNodeContext context)
        : base(kind, context)
    {
    }
}

public sealed class CharacterDefinitionStatementSyntax : StatementSyntax
{
    internal CharacterDefinitionStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken characterKeyword,
        SyntaxToken identifier,
        SyntaxToken equalsToken,
        SyntaxToken displayNameToken,
        ColorClauseSyntax? colorClause)
        : base(SyntaxKind.CharacterDefinitionStatement, context)
    {
        CharacterKeyword = characterKeyword;
        Identifier = identifier;
        EqualsToken = equalsToken;
        DisplayNameToken = displayNameToken;
        ColorClause = colorClause;
    }

    public SyntaxToken CharacterKeyword { get; }

    public SyntaxToken Identifier { get; }

    public SyntaxToken EqualsToken { get; }

    public SyntaxToken DisplayNameToken { get; }

    public ColorClauseSyntax? ColorClause { get; }

    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        if (ColorClause is not null)
        {
            yield return ColorClause;
        }
    }
}

public sealed class LabelStatementSyntax : StatementSyntax
{
    internal LabelStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken labelKeyword,
        SyntaxToken identifier,
        SyntaxToken colonToken)
        : base(SyntaxKind.LabelStatement, context)
    {
        LabelKeyword = labelKeyword;
        Identifier = identifier;
        ColonToken = colonToken;
    }

    public SyntaxToken LabelKeyword { get; }

    public SyntaxToken Identifier { get; }

    public SyntaxToken ColonToken { get; }
}

public sealed class SceneStatementSyntax : StatementSyntax
{
    internal SceneStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken sceneKeyword,
        SyntaxToken imageToken,
        TransitionClauseSyntax? transition)
        : base(SyntaxKind.SceneStatement, context)
    {
        SceneKeyword = sceneKeyword;
        ImageToken = imageToken;
        Transition = transition;
    }

    public SyntaxToken SceneKeyword { get; }

    public SyntaxToken ImageToken { get; }

    public TransitionClauseSyntax? Transition { get; }

    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        if (Transition is not null)
        {
            yield return Transition;
        }
    }
}

public sealed class ShowStatementSyntax : StatementSyntax
{
    private readonly ReadOnlyCollection<SyntaxToken> _imageTokens;
    private readonly ReadOnlyCollection<ModifierSyntax> _modifiers;

    internal ShowStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken showKeyword,
        SyntaxToken targetToken,
        SyntaxToken[] imageTokens,
        PositionClauseSyntax? position,
        TransitionClauseSyntax? transition,
        ModifierSyntax[] modifiers)
        : base(SyntaxKind.ShowStatement, context)
    {
        ShowKeyword = showKeyword;
        TargetToken = targetToken;
        _imageTokens = Array.AsReadOnly(imageTokens);
        Position = position;
        Transition = transition;
        _modifiers = Array.AsReadOnly(modifiers);
    }

    public SyntaxToken ShowKeyword { get; }

    public SyntaxToken TargetToken { get; }

    /// <summary>
    /// Gets one quoted asset token or the legacy unquoted sprite-expression identifiers.
    /// </summary>
    public IReadOnlyList<SyntaxToken> ImageTokens => _imageTokens;

    public string ImageValueText => _imageTokens.Count == 1 && _imageTokens[0].Kind == SyntaxKind.StringLiteralToken
        ? _imageTokens[0].ValueText
        : string.Join('_', _imageTokens.Select(static token => token.ValueText));

    public PositionClauseSyntax? Position { get; }

    public TransitionClauseSyntax? Transition { get; }

    public IReadOnlyList<ModifierSyntax> Modifiers => _modifiers;

    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        if (Position is not null)
        {
            yield return Position;
        }

        if (Transition is not null)
        {
            yield return Transition;
        }

        foreach (ModifierSyntax modifier in _modifiers)
        {
            yield return modifier;
        }
    }
}

public sealed class HideStatementSyntax : StatementSyntax
{
    internal HideStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken hideKeyword,
        SyntaxToken targetToken,
        TransitionClauseSyntax? transition)
        : base(SyntaxKind.HideStatement, context)
    {
        HideKeyword = hideKeyword;
        TargetToken = targetToken;
        Transition = transition;
    }

    public SyntaxToken HideKeyword { get; }

    public SyntaxToken TargetToken { get; }

    public bool IsAll => TargetToken.ValueText == "all";

    public TransitionClauseSyntax? Transition { get; }

    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        if (Transition is not null)
        {
            yield return Transition;
        }
    }
}

public sealed class DialogueStatementSyntax : StatementSyntax
{
    internal DialogueStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken speakerToken,
        SyntaxToken textToken)
        : base(SyntaxKind.DialogueStatement, context)
    {
        SpeakerToken = speakerToken;
        TextToken = textToken;
    }

    public SyntaxToken SpeakerToken { get; }

    public SyntaxToken TextToken { get; }
}

public sealed class NarrationStatementSyntax : StatementSyntax
{
    internal NarrationStatementSyntax(SyntaxNodeContext context, SyntaxToken textToken)
        : base(SyntaxKind.NarrationStatement, context)
    {
        TextToken = textToken;
    }

    public SyntaxToken TextToken { get; }
}

public sealed class PlayStatementSyntax : StatementSyntax
{
    private readonly ReadOnlyCollection<ModifierSyntax> _modifiers;

    internal PlayStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken playKeyword,
        SyntaxToken? channelToken,
        SyntaxToken fileToken,
        ModifierSyntax[] modifiers)
        : base(SyntaxKind.PlayStatement, context)
    {
        PlayKeyword = playKeyword;
        ChannelToken = channelToken;
        FileToken = fileToken;
        _modifiers = Array.AsReadOnly(modifiers);
    }

    public SyntaxToken PlayKeyword { get; }

    public SyntaxToken? ChannelToken { get; }

    public SyntaxToken FileToken { get; }

    public IReadOnlyList<ModifierSyntax> Modifiers => _modifiers;

    public override IEnumerable<SyntaxNode> ChildNodes() => _modifiers;
}

public sealed class StopStatementSyntax : StatementSyntax
{
    internal StopStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken stopKeyword,
        SyntaxToken? channelToken,
        ModifierSyntax? fadeOutModifier)
        : base(SyntaxKind.StopStatement, context)
    {
        StopKeyword = stopKeyword;
        ChannelToken = channelToken;
        FadeOutModifier = fadeOutModifier;
    }

    public SyntaxToken StopKeyword { get; }

    public SyntaxToken? ChannelToken { get; }

    public ModifierSyntax? FadeOutModifier { get; }

    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        if (FadeOutModifier is not null)
        {
            yield return FadeOutModifier;
        }
    }
}

public sealed class WaitStatementSyntax : StatementSyntax
{
    internal WaitStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken waitKeyword,
        SyntaxToken durationToken)
        : base(SyntaxKind.WaitStatement, context)
    {
        WaitKeyword = waitKeyword;
        DurationToken = durationToken;
    }

    public SyntaxToken WaitKeyword { get; }

    public SyntaxToken DurationToken { get; }
}

public sealed class SetStatementSyntax : StatementSyntax
{
    internal SetStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken setKeyword,
        QualifiedNameSyntax name,
        SyntaxToken equalsToken,
        ExpressionSyntax expression)
        : base(SyntaxKind.SetStatement, context)
    {
        SetKeyword = setKeyword;
        Name = name;
        EqualsToken = equalsToken;
        Expression = expression;
    }

    public SyntaxToken SetKeyword { get; }

    public QualifiedNameSyntax Name { get; }

    public SyntaxToken EqualsToken { get; }

    public ExpressionSyntax Expression { get; }

    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        yield return Name;
        yield return Expression;
    }
}

public sealed class JumpStatementSyntax : StatementSyntax
{
    internal JumpStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken jumpKeyword,
        SyntaxToken targetToken)
        : base(SyntaxKind.JumpStatement, context)
    {
        JumpKeyword = jumpKeyword;
        TargetToken = targetToken;
    }

    public SyntaxToken JumpKeyword { get; }

    public SyntaxToken TargetToken { get; }
}

public sealed class ConfigStatementSyntax : StatementSyntax
{
    internal ConfigStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken configKeyword,
        QualifiedNameSyntax key,
        SyntaxToken equalsToken,
        ExpressionSyntax expression)
        : base(SyntaxKind.ConfigStatement, context)
    {
        ConfigKeyword = configKeyword;
        Key = key;
        EqualsToken = equalsToken;
        Expression = expression;
    }

    public SyntaxToken ConfigKeyword { get; }

    public QualifiedNameSyntax Key { get; }

    public SyntaxToken EqualsToken { get; }

    public ExpressionSyntax Expression { get; }

    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        yield return Key;
        yield return Expression;
    }
}

public sealed class TweenStatementSyntax : StatementSyntax
{
    internal TweenStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken tweenKeyword,
        SyntaxToken targetToken,
        SyntaxToken propertyToken,
        ExpressionSyntax value,
        ModifierSyntax? durationModifier,
        SyntaxToken? easingToken)
        : base(SyntaxKind.TweenStatement, context)
    {
        TweenKeyword = tweenKeyword;
        TargetToken = targetToken;
        PropertyToken = propertyToken;
        Value = value;
        DurationModifier = durationModifier;
        EasingToken = easingToken;
    }

    public SyntaxToken TweenKeyword { get; }

    public SyntaxToken TargetToken { get; }

    public SyntaxToken PropertyToken { get; }

    public ExpressionSyntax Value { get; }

    public ModifierSyntax? DurationModifier { get; }

    public SyntaxToken? EasingToken { get; }

    public override IEnumerable<SyntaxNode> ChildNodes()
    {
        yield return Value;
        if (DurationModifier is not null)
        {
            yield return DurationModifier;
        }
    }
}

public sealed class EmotionStatementSyntax : StatementSyntax
{
    internal EmotionStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken emotionKeyword,
        SyntaxToken characterToken,
        SyntaxToken emotionToken)
        : base(SyntaxKind.EmotionStatement, context)
    {
        EmotionKeyword = emotionKeyword;
        CharacterToken = characterToken;
        EmotionToken = emotionToken;
    }

    public SyntaxToken EmotionKeyword { get; }

    public SyntaxToken CharacterToken { get; }

    public SyntaxToken EmotionToken { get; }
}

public sealed class MetaStatementSyntax : StatementSyntax
{
    private readonly ReadOnlyCollection<SyntaxToken> _arguments;

    internal MetaStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken metaKeyword,
        SyntaxToken actionToken,
        SyntaxToken[] arguments)
        : base(SyntaxKind.MetaStatement, context)
    {
        MetaKeyword = metaKeyword;
        ActionToken = actionToken;
        _arguments = Array.AsReadOnly(arguments);
    }

    public SyntaxToken MetaKeyword { get; }

    public SyntaxToken ActionToken { get; }

    public IReadOnlyList<SyntaxToken> Arguments => _arguments;
}

public sealed class ParticleStatementSyntax : StatementSyntax
{
    private readonly ReadOnlyCollection<ModifierSyntax> _parameters;

    internal ParticleStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken particleKeyword,
        SyntaxToken presetToken,
        SyntaxToken? nameToken,
        ModifierSyntax[] parameters)
        : base(SyntaxKind.ParticleStatement, context)
    {
        ParticleKeyword = particleKeyword;
        PresetToken = presetToken;
        NameToken = nameToken;
        _parameters = Array.AsReadOnly(parameters);
    }

    public SyntaxToken ParticleKeyword { get; }

    public SyntaxToken PresetToken { get; }

    public SyntaxToken? NameToken { get; }

    public bool StopsAll => PresetToken.ValueText == "stop" && NameToken?.ValueText == "all";

    public IReadOnlyList<ModifierSyntax> Parameters => _parameters;

    public override IEnumerable<SyntaxNode> ChildNodes() => _parameters;
}

public sealed class ShowScreenStatementSyntax : StatementSyntax
{
    internal ShowScreenStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken commandToken,
        SyntaxToken screenNameToken)
        : base(SyntaxKind.ShowScreenStatement, context)
    {
        CommandToken = commandToken;
        ScreenNameToken = screenNameToken;
    }

    public SyntaxToken CommandToken { get; }

    public SyntaxToken ScreenNameToken { get; }
}

public sealed class HideScreenStatementSyntax : StatementSyntax
{
    internal HideScreenStatementSyntax(
        SyntaxNodeContext context,
        SyntaxToken commandToken,
        SyntaxToken screenNameToken)
        : base(SyntaxKind.HideScreenStatement, context)
    {
        CommandToken = commandToken;
        ScreenNameToken = screenNameToken;
    }

    public SyntaxToken CommandToken { get; }

    public SyntaxToken ScreenNameToken { get; }
}

public sealed class BadStatementSyntax : StatementSyntax
{
    private readonly ReadOnlyCollection<SyntaxToken> _tokens;

    internal BadStatementSyntax(SyntaxNodeContext context, SyntaxToken[] tokens)
        : base(SyntaxKind.BadStatement, context)
    {
        _tokens = Array.AsReadOnly(tokens);
    }

    public IReadOnlyList<SyntaxToken> Tokens => _tokens;
}
