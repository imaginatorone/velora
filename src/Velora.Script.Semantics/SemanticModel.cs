using System.Collections.ObjectModel;
using Velora.Script.Syntax;
using Velora.Script.Syntax.Diagnostics;
using Velora.Script.Syntax.Text;

namespace Velora.Script.Semantics;

/// <summary>
/// The immutable symbols, bindings, references, and diagnostics for one syntax tree.
/// </summary>
public sealed class SemanticModel
{
    private readonly ReadOnlyCollection<Symbol> _symbols;
    private readonly ReadOnlyCollection<SemanticReference> _references;
    private readonly ReadOnlyCollection<Diagnostic> _semanticDiagnostics;
    private readonly ReadOnlyCollection<Diagnostic> _diagnostics;
    private readonly IReadOnlyDictionary<SyntaxNode, Symbol> _declaredSymbols;
    private readonly IReadOnlyDictionary<SymbolKey, Symbol> _symbolsByKey;

    private SemanticModel(
        SyntaxTree syntaxTree,
        Symbol[] symbols,
        SemanticReference[] references,
        Diagnostic[] semanticDiagnostics,
        Diagnostic[] diagnostics,
        IReadOnlyDictionary<SyntaxNode, Symbol> declaredSymbols,
        IReadOnlyDictionary<SymbolKey, Symbol> symbolsByKey)
    {
        SyntaxTree = syntaxTree;
        _symbols = Array.AsReadOnly(symbols);
        _references = Array.AsReadOnly(references);
        _semanticDiagnostics = Array.AsReadOnly(semanticDiagnostics);
        _diagnostics = Array.AsReadOnly(diagnostics);
        _declaredSymbols = declaredSymbols;
        _symbolsByKey = symbolsByKey;
    }

    public SyntaxTree SyntaxTree { get; }

    /// <summary>
    /// Gets all symbols ordered by kind and then by ordinal name.
    /// </summary>
    public IReadOnlyList<Symbol> Symbols => _symbols;

    /// <summary>
    /// Gets resolved and unresolved source references in source order.
    /// </summary>
    public IReadOnlyList<SemanticReference> References => _references;

    public IReadOnlyList<Diagnostic> SemanticDiagnostics => _semanticDiagnostics;

    /// <summary>
    /// Gets syntax and semantic diagnostics together in deterministic source order.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public static SemanticModel Create(SyntaxTree syntaxTree)
    {
        ArgumentNullException.ThrowIfNull(syntaxTree);
        return new Binder(syntaxTree).Bind();
    }

    public Symbol? GetDeclaredSymbol(SyntaxNode syntax)
    {
        ArgumentNullException.ThrowIfNull(syntax);
        return _declaredSymbols.GetValueOrDefault(syntax);
    }

    public Symbol? LookupSymbol(SymbolKind kind, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _symbolsByKey.GetValueOrDefault(new SymbolKey(kind, name));
    }

    public IReadOnlyList<Symbol> LookupSymbols(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Array.AsReadOnly(_symbols.Where(symbol => symbol.Name == name).ToArray());
    }

    public IReadOnlyList<Symbol> GetSymbols(SymbolKind kind) =>
        Array.AsReadOnly(_symbols.Where(symbol => symbol.Kind == kind).ToArray());

    public IReadOnlyList<SemanticReference> GetReferences(SyntaxNode syntax)
    {
        ArgumentNullException.ThrowIfNull(syntax);
        return Array.AsReadOnly(
            _references.Where(reference => ReferenceEquals(reference.Syntax, syntax)).ToArray());
    }

    public IReadOnlyList<SemanticReference> FindReferences(Symbol symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        return Array.AsReadOnly(
            _references.Where(reference => ReferenceEquals(reference.Symbol, symbol)).ToArray());
    }

    /// <summary>
    /// Gets all definition and resolved-reference spans that a rename operation must consider.
    /// </summary>
    public IReadOnlyList<TextSpan> GetRenameSpans(Symbol symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        TextSpan[] spans =
        [
            .. symbol.Definitions.Select(static definition => definition.Span),
            .. _references
                .Where(reference => ReferenceEquals(reference.Symbol, symbol))
                .Select(static reference => reference.Span),
        ];

        Array.Sort(spans, static (left, right) =>
        {
            int result = left.Start.CompareTo(right.Start);
            return result != 0 ? result : left.Length.CompareTo(right.Length);
        });
        return Array.AsReadOnly(spans);
    }

    public Symbol? GetSymbolAtPosition(int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);

        foreach (Symbol symbol in _symbols)
        {
            foreach (SymbolDefinition definition in symbol.Definitions)
            {
                if (definition.Span.Contains(position))
                {
                    return symbol;
                }
            }
        }

        return _references.FirstOrDefault(reference => reference.Span.Contains(position))?.Symbol;
    }

    private readonly record struct SymbolKey(SymbolKind Kind, string Name);

    private sealed class Binder
    {
        private static readonly string[] ImplicitVariableNames =
        [
            "os_locale",
            "os_time",
            "os_username",
            "sys.name_gibberish",
            "sys.os_username",
        ];

        private readonly SyntaxTree _syntaxTree;
        private readonly Dictionary<SymbolKey, SymbolBuilder> _builders = [];
        private readonly List<Diagnostic> _diagnostics = [];
        private readonly List<SemanticReference> _references = [];

        public Binder(SyntaxTree syntaxTree)
        {
            _syntaxTree = syntaxTree;
            foreach (string name in ImplicitVariableNames)
            {
                SymbolKey key = new(SymbolKind.Variable, name);
                _builders.Add(key, new SymbolBuilder(name, SymbolKind.Variable, isImplicit: true));
            }
        }

        public SemanticModel Bind()
        {
            foreach (StatementSyntax statement in _syntaxTree.Root.Statements)
            {
                CollectDefinitions(statement, isTopLevel: true);
            }

            Dictionary<SymbolKey, Symbol> symbolsByKey = BuildSymbols();
            Dictionary<SyntaxNode, Symbol> declaredSymbols = BuildDeclaredSymbolMap(symbolsByKey);

            foreach (StatementSyntax statement in _syntaxTree.Root.Statements)
            {
                BindReferences(statement, symbolsByKey);
            }

            Symbol[] symbols =
            [
                .. symbolsByKey.Values
                    .OrderBy(static symbol => symbol.Kind)
                    .ThenBy(static symbol => symbol.Name, StringComparer.Ordinal),
            ];
            SemanticReference[] references =
            [
                .. _references
                    .OrderBy(static reference => reference.Span.Start)
                    .ThenBy(static reference => reference.Span.Length)
                    .ThenBy(static reference => reference.Kind),
            ];
            Diagnostic[] semanticDiagnostics = SortDiagnostics(_diagnostics);
            Diagnostic[] diagnostics = SortDiagnostics(_syntaxTree.Diagnostics.Concat(semanticDiagnostics));

            return new SemanticModel(
                _syntaxTree,
                symbols,
                references,
                semanticDiagnostics,
                diagnostics,
                new ReadOnlyDictionary<SyntaxNode, Symbol>(declaredSymbols),
                new ReadOnlyDictionary<SymbolKey, Symbol>(symbolsByKey));
        }

        private void CollectDefinitions(StatementSyntax statement, bool isTopLevel)
        {
            switch (statement)
            {
                case CharacterDefinitionStatementSyntax character:
                    AddNamedDefinition(
                        SymbolKind.Character,
                        character.Identifier,
                        character,
                        SymbolDefinitionKind.Character,
                        SemanticDiagnosticIds.DuplicateCharacter,
                        "Character");
                    break;

                case LabelStatementSyntax label when isTopLevel:
                    AddNamedDefinition(
                        SymbolKind.Label,
                        label.Identifier,
                        label,
                        SymbolDefinitionKind.Label,
                        SemanticDiagnosticIds.DuplicateLabel,
                        "Label");
                    break;

                case LabelStatementSyntax label:
                    if (IsUsable(label.Identifier))
                    {
                        _diagnostics.Add(new Diagnostic(
                            SemanticDiagnosticIds.NestedLabel,
                            DiagnosticSeverity.Warning,
                            $"Label '{label.Identifier.ValueText}' is nested and cannot be a jump target in the legacy runtime.",
                            label.Identifier.Span));
                    }

                    break;

                case ScreenDefinitionStatementSyntax screen:
                    AddNamedDefinition(
                        SymbolKind.Screen,
                        screen.NameToken,
                        screen,
                        SymbolDefinitionKind.Screen,
                        SemanticDiagnosticIds.DuplicateScreen,
                        "Screen");
                    break;

                case SetStatementSyntax set:
                    AddVariableDefinition(set.Name, set, SymbolDefinitionKind.VariableAssignment, prefix: null);
                    break;

                case InputStatementSyntax input:
                    AddVariableDefinition(input.Target, input, SymbolDefinitionKind.InputTarget, prefix: null);
                    break;

                case ConfigStatementSyntax config:
                    AddVariableDefinition(config.Key, config, SymbolDefinitionKind.Configuration, prefix: "__config.");
                    break;
            }

            switch (statement)
            {
                case MenuStatementSyntax menu:
                    foreach (ChoiceClauseSyntax choice in menu.Choices)
                    {
                        foreach (StatementSyntax nested in choice.Statements)
                        {
                            CollectDefinitions(nested, isTopLevel: false);
                        }
                    }

                    break;

                case IfStatementSyntax conditional:
                    foreach (StatementSyntax nested in conditional.ThenStatements)
                    {
                        CollectDefinitions(nested, isTopLevel: false);
                    }

                    foreach (ElifClauseSyntax clause in conditional.ElifClauses)
                    {
                        foreach (StatementSyntax nested in clause.Statements)
                        {
                            CollectDefinitions(nested, isTopLevel: false);
                        }
                    }

                    if (conditional.ElseClause is not null)
                    {
                        foreach (StatementSyntax nested in conditional.ElseClause.Statements)
                        {
                            CollectDefinitions(nested, isTopLevel: false);
                        }
                    }

                    break;
            }
        }

        private void AddNamedDefinition(
            SymbolKind symbolKind,
            SyntaxToken nameToken,
            SyntaxNode syntax,
            SymbolDefinitionKind definitionKind,
            string duplicateDiagnosticId,
            string displayKind)
        {
            if (!IsUsable(nameToken))
            {
                return;
            }

            SymbolBuilder builder = GetOrAddBuilder(symbolKind, nameToken.ValueText);
            if (builder.Definitions.Count != 0)
            {
                _diagnostics.Add(new Diagnostic(
                    duplicateDiagnosticId,
                    DiagnosticSeverity.Warning,
                    $"{displayKind} '{nameToken.ValueText}' is declared more than once; the last declaration is used.",
                    nameToken.Span));
            }

            builder.Definitions.Add(new SymbolDefinition(syntax, nameToken.Span, definitionKind));
        }

        private void AddVariableDefinition(
            QualifiedNameSyntax name,
            SyntaxNode syntax,
            SymbolDefinitionKind definitionKind,
            string? prefix)
        {
            if (!IsUsable(name))
            {
                return;
            }

            string symbolName = string.Concat(prefix, name.ValueText);
            SymbolBuilder builder = GetOrAddBuilder(SymbolKind.Variable, symbolName);
            builder.Definitions.Add(new SymbolDefinition(syntax, name.Span, definitionKind));
        }

        private SymbolBuilder GetOrAddBuilder(SymbolKind kind, string name)
        {
            SymbolKey key = new(kind, name);
            if (!_builders.TryGetValue(key, out SymbolBuilder? builder))
            {
                builder = new SymbolBuilder(name, kind, isImplicit: false);
                _builders.Add(key, builder);
            }

            return builder;
        }

        private Dictionary<SymbolKey, Symbol> BuildSymbols()
        {
            Dictionary<SymbolKey, Symbol> symbols = [];
            foreach ((SymbolKey key, SymbolBuilder builder) in _builders)
            {
                symbols.Add(
                    key,
                    new Symbol(builder.Name, builder.Kind, [.. builder.Definitions], builder.IsImplicit));
            }

            return symbols;
        }

        private Dictionary<SyntaxNode, Symbol> BuildDeclaredSymbolMap(
            IReadOnlyDictionary<SymbolKey, Symbol> symbolsByKey)
        {
            Dictionary<SyntaxNode, Symbol> declared = new(ReferenceEqualityComparer.Instance);
            foreach ((SymbolKey key, SymbolBuilder builder) in _builders)
            {
                Symbol symbol = symbolsByKey[key];
                foreach (SymbolDefinition definition in builder.Definitions)
                {
                    declared[definition.Syntax] = symbol;
                }
            }

            return declared;
        }

        private void BindReferences(
            StatementSyntax statement,
            IReadOnlyDictionary<SymbolKey, Symbol> symbolsByKey)
        {
            switch (statement)
            {
                case DialogueStatementSyntax dialogue:
                    AddReference(
                        dialogue.SpeakerToken.ValueText,
                        SymbolKind.Character,
                        SemanticReferenceKind.DialogueSpeaker,
                        dialogue,
                        dialogue.SpeakerToken,
                        symbolsByKey);
                    BindInterpolations(dialogue.TextToken, dialogue, symbolsByKey);
                    break;

                case NarrationStatementSyntax narration:
                    BindInterpolations(narration.TextToken, narration, symbolsByKey);
                    break;

                case JumpStatementSyntax jump:
                    AddReference(
                        jump.TargetToken.ValueText,
                        SymbolKind.Label,
                        SemanticReferenceKind.Jump,
                        jump,
                        jump.TargetToken,
                        symbolsByKey);
                    break;

                case SetStatementSyntax set:
                    BindExpression(set.Expression, symbolsByKey);
                    break;

                case ConfigStatementSyntax config:
                    BindExpression(config.Expression, symbolsByKey);
                    break;

                case TweenStatementSyntax tween:
                    BindExpression(tween.Value, symbolsByKey);
                    break;

                case EmotionStatementSyntax emotion:
                    AddReference(
                        emotion.CharacterToken.ValueText,
                        SymbolKind.Character,
                        SemanticReferenceKind.EmotionCharacter,
                        emotion,
                        emotion.CharacterToken,
                        symbolsByKey);
                    break;

                case ShowScreenStatementSyntax showScreen:
                    AddReference(
                        showScreen.ScreenNameToken.ValueText,
                        SymbolKind.Screen,
                        SemanticReferenceKind.ShowScreen,
                        showScreen,
                        showScreen.ScreenNameToken,
                        symbolsByKey);
                    break;

                case HideScreenStatementSyntax hideScreen:
                    AddReference(
                        hideScreen.ScreenNameToken.ValueText,
                        SymbolKind.Screen,
                        SemanticReferenceKind.HideScreen,
                        hideScreen,
                        hideScreen.ScreenNameToken,
                        symbolsByKey);
                    break;

                case ScreenDefinitionStatementSyntax screen:
                    foreach (ScreenElementSyntax element in screen.Elements)
                    {
                        BindScreenElement(element, symbolsByKey);
                    }

                    break;

                case MenuStatementSyntax menu:
                    foreach (ChoiceClauseSyntax choice in menu.Choices)
                    {
                        foreach (StatementSyntax nested in choice.Statements)
                        {
                            BindReferences(nested, symbolsByKey);
                        }
                    }

                    break;

                case IfStatementSyntax conditional:
                    BindExpression(conditional.Condition, symbolsByKey);
                    foreach (StatementSyntax nested in conditional.ThenStatements)
                    {
                        BindReferences(nested, symbolsByKey);
                    }

                    foreach (ElifClauseSyntax clause in conditional.ElifClauses)
                    {
                        BindExpression(clause.Condition, symbolsByKey);
                        foreach (StatementSyntax nested in clause.Statements)
                        {
                            BindReferences(nested, symbolsByKey);
                        }
                    }

                    if (conditional.ElseClause is not null)
                    {
                        foreach (StatementSyntax nested in conditional.ElseClause.Statements)
                        {
                            BindReferences(nested, symbolsByKey);
                        }
                    }

                    break;
            }
        }

        private void BindExpression(
            ExpressionSyntax expression,
            IReadOnlyDictionary<SymbolKey, Symbol> symbolsByKey)
        {
            switch (expression)
            {
                case NameExpressionSyntax name:
                    if (IsUsable(name.Name))
                    {
                        AddReference(
                            name.Name.ValueText,
                            SymbolKind.Variable,
                            SemanticReferenceKind.VariableRead,
                            name,
                            name.Name.Span,
                            symbolsByKey);
                    }

                    break;

                case UnaryExpressionSyntax unary:
                    BindExpression(unary.Operand, symbolsByKey);
                    break;

                case BinaryExpressionSyntax binary:
                    BindExpression(binary.Left, symbolsByKey);
                    BindExpression(binary.Right, symbolsByKey);
                    break;

                case ParenthesizedExpressionSyntax parenthesized:
                    BindExpression(parenthesized.Expression, symbolsByKey);
                    break;
            }
        }

        private void BindScreenElement(
            ScreenElementSyntax element,
            IReadOnlyDictionary<SymbolKey, Symbol> symbolsByKey)
        {
            foreach (ScreenPropertySyntax property in element.Properties)
            {
                if (property.NameToken.ValueText == "action" &&
                    property.ValueTokens.Count == 2 &&
                    property.ValueTokens[0].Kind == SyntaxKind.JumpKeyword)
                {
                    SyntaxToken target = property.ValueTokens[1];
                    AddReference(
                        target.ValueText,
                        SymbolKind.Label,
                        SemanticReferenceKind.ScreenActionJump,
                        property,
                        target,
                        symbolsByKey);
                }
            }

            if (element is PanelScreenElementSyntax panel)
            {
                foreach (ScreenElementSyntax child in panel.Children)
                {
                    BindScreenElement(child, symbolsByKey);
                }
            }
        }

        private void BindInterpolations(
            SyntaxToken textToken,
            SyntaxNode syntax,
            IReadOnlyDictionary<SymbolKey, Symbol> symbolsByKey)
        {
            foreach (InterpolationReference interpolation in EnumerateInterpolations(textToken))
            {
                AddReference(
                    interpolation.Name,
                    SymbolKind.Variable,
                    SemanticReferenceKind.VariableInterpolation,
                    syntax,
                    interpolation.Span,
                    symbolsByKey);
            }
        }

        private void AddReference(
            string name,
            SymbolKind targetKind,
            SemanticReferenceKind referenceKind,
            SyntaxNode syntax,
            SyntaxToken token,
            IReadOnlyDictionary<SymbolKey, Symbol> symbolsByKey)
        {
            if (!IsUsable(token))
            {
                return;
            }

            AddReference(name, targetKind, referenceKind, syntax, token.Span, symbolsByKey);
        }

        private void AddReference(
            string name,
            SymbolKind targetKind,
            SemanticReferenceKind referenceKind,
            SyntaxNode syntax,
            TextSpan span,
            IReadOnlyDictionary<SymbolKey, Symbol> symbolsByKey)
        {
            Symbol? symbol = symbolsByKey.GetValueOrDefault(new SymbolKey(targetKind, name));
            _references.Add(new SemanticReference(name, targetKind, referenceKind, syntax, span, symbol));

            if (symbol is null)
            {
                _diagnostics.Add(CreateUndefinedDiagnostic(name, targetKind, span));
            }
        }

        private static Diagnostic CreateUndefinedDiagnostic(string name, SymbolKind kind, TextSpan span)
        {
            (string id, string displayKind) = kind switch
            {
                SymbolKind.Label => (SemanticDiagnosticIds.UndefinedLabel, "Label"),
                SymbolKind.Character => (SemanticDiagnosticIds.UndefinedCharacter, "Character"),
                SymbolKind.Screen => (SemanticDiagnosticIds.UndefinedScreen, "Screen"),
                SymbolKind.Variable => (SemanticDiagnosticIds.UndefinedVariable, "Variable"),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };

            return new Diagnostic(
                id,
                DiagnosticSeverity.Warning,
                $"{displayKind} '{name}' is not defined.",
                span);
        }

        private static IEnumerable<InterpolationReference> EnumerateInterpolations(SyntaxToken token)
        {
            if (token.Kind != SyntaxKind.StringLiteralToken || token.Text.Length == 0)
            {
                yield break;
            }

            List<DecodedCharacter> characters = DecodeStringToken(token.Text);
            for (int index = 0; index < characters.Count; index++)
            {
                if (characters[index].Value != '[')
                {
                    continue;
                }

                int close = index + 1;
                while (close < characters.Count && characters[close].Value != ']')
                {
                    close++;
                }

                if (close == characters.Count)
                {
                    continue;
                }

                int nameLength = close - index - 1;
                if (nameLength == 0)
                {
                    continue;
                }

                string name = string.Concat(
                    characters.Skip(index + 1).Take(nameLength).Select(static character => character.Value));
                int rawStart = characters[index + 1].RawStart;
                int rawEnd = characters[close - 1].RawEnd;
                yield return new InterpolationReference(
                    name,
                    TextSpan.FromBounds(token.Span.Start + rawStart, token.Span.Start + rawEnd));
                index = close;
            }
        }

        private static List<DecodedCharacter> DecodeStringToken(string rawText)
        {
            List<DecodedCharacter> result = new(rawText.Length);
            int index = rawText[0] == '"' ? 1 : 0;

            while (index < rawText.Length)
            {
                if (rawText[index] == '"')
                {
                    break;
                }

                int rawStart = index;
                char value = rawText[index];
                if (value == '\\' && index + 1 < rawText.Length)
                {
                    char escaped = rawText[index + 1];
                    value = escaped switch
                    {
                        'n' => '\n',
                        't' => '\t',
                        '\\' => '\\',
                        '"' => '"',
                        _ => escaped,
                    };
                    index += 2;
                }
                else
                {
                    index++;
                }

                result.Add(new DecodedCharacter(value, rawStart, index));
            }

            return result;
        }

        private static bool IsUsable(SyntaxToken token) =>
            !token.IsMissing && token.ValueText.Length != 0;

        private static bool IsUsable(QualifiedNameSyntax name) =>
            name.Identifiers.Count != 0 && name.Identifiers.All(IsUsable);

        private static Diagnostic[] SortDiagnostics(IEnumerable<Diagnostic> diagnostics) =>
        [
            .. diagnostics
                .OrderBy(static diagnostic => diagnostic.Span.Start)
                .ThenBy(static diagnostic => diagnostic.Span.Length)
                .ThenBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal),
        ];

        private sealed class SymbolBuilder(string name, SymbolKind kind, bool isImplicit)
        {
            public string Name { get; } = name;

            public SymbolKind Kind { get; } = kind;

            public bool IsImplicit { get; } = isImplicit;

            public List<SymbolDefinition> Definitions { get; } = [];
        }

        private readonly record struct DecodedCharacter(char Value, int RawStart, int RawEnd);

        private readonly record struct InterpolationReference(string Name, TextSpan Span);
    }
}
