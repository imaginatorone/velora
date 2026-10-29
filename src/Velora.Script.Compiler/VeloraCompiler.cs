using System.Collections.Immutable;
using System.Globalization;
using Velora.Runtime.Contracts;
using Velora.Script.Semantics;
using Velora.Script.Syntax;
using Velora.Script.Syntax.Diagnostics;

namespace Velora.Script.Compiler;

/// <summary>Compiles one bound VelScript document into deterministic, source-mapped runtime IR.</summary>
public static class VeloraCompiler
{
    /// <summary>The compatibility compiler identity included in every program fingerprint.</summary>
    public const string CompilerVersion = "1.0.0";

    /// <summary>Names the canonical byte contract used for <see cref="ProgramFingerprint"/>.</summary>
    public const string CanonicalFingerprintAlgorithm = "SHA-256/VeloraIR-Binary-1";

    /// <summary>
    /// The canonical fingerprint format. Version 1 begins with the length-prefixed UTF-8 string
    /// <c>VeloraIR</c>, this format number, and <see cref="CompilerVersion"/>. It then writes instructions,
    /// startup IDs, entry data, and sorted globals in declared order. Integers and IEEE-754 double bits
    /// are big-endian; booleans are one byte; strings are an Int32 byte length plus UTF-8; collections
    /// carry an Int32 count; nullable values carry a presence byte. Instruction/expression kind tags are
    /// explicitly assigned by the serializer. SHA-256 hashes the resulting bytes. Source paths/spans are
    /// deliberately excluded, so formatting-only edits do not invalidate saves.
    /// </summary>
    public const int CanonicalFingerprintFormatVersion = 1;

    public static VeloraCompilation Compile(SemanticModel semanticModel)
    {
        ArgumentNullException.ThrowIfNull(semanticModel);
        return new Lowerer(semanticModel).Compile();
    }

    private sealed class Lowerer
    {
        private readonly SemanticModel _semanticModel;
        private readonly string _filePath;
        private readonly List<CompilationDiagnostic> _diagnostics = [];
        private readonly List<InstructionDraft> _drafts = [];
        private readonly Dictionary<SyntaxNode, InstructionDraft> _draftBySyntax =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<InstructionDraft, InstructionId> _ids =
            new(ReferenceEqualityComparer.Instance);

        public Lowerer(SemanticModel semanticModel)
        {
            _semanticModel = semanticModel;
            _filePath = semanticModel.SyntaxTree.Text.FilePath;
            foreach (Diagnostic diagnostic in semanticModel.Diagnostics)
            {
                _diagnostics.Add(new CompilationDiagnostic(
                    diagnostic.Id,
                    ConvertSeverity(diagnostic.Severity),
                    diagnostic.Message,
                    Source(diagnostic.Span.Start, diagnostic.Span.Length)));
            }
        }

        public VeloraCompilation Compile()
        {
            SequenceDraft root = BuildRoot(_semanticModel.SyntaxTree.Root.Statements);
            WireSequence(root, continuation: null);

            for (int index = 0; index < _drafts.Count; index++)
            {
                _ids.Add(_drafts[index], InstructionId.FromOrdinal(index));
            }

            ImmutableArray<VeloraInstruction> instructions =
                _drafts.Select(Materialize).ToImmutableArray();

            InstructionDraft? start = ResolveLabelTarget("start");
            InstructionId? entryInstructionId = start is null
                ? IdOf(Entry(root, fallback: null))
                : IdOf(start.Continuation);
            string? entryLabel = start is null ? null : "start";
            ImmutableArray<InstructionId> initialization = BuildInitialization(root, start);
            ImmutableArray<GlobalDefinition> definitions = BuildDefinitions();

            ProgramFingerprint fingerprint = IrCanonicalizer.ComputeFingerprint(
                CompilerVersion,
                CanonicalFingerprintFormatVersion,
                instructions,
                initialization,
                entryInstructionId,
                entryLabel,
                definitions);

            VeloraProgram program = new(
                instructions,
                initialization,
                entryInstructionId,
                entryLabel,
                definitions,
                fingerprint);

            CompilationDiagnostic[] diagnostics =
            [
                .. _diagnostics
                    .OrderBy(static diagnostic => diagnostic.Source.Start)
                    .ThenBy(static diagnostic => diagnostic.Source.Length)
                    .ThenBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)
                    .ThenBy(static diagnostic => diagnostic.Message, StringComparer.Ordinal),
            ];
            return new VeloraCompilation(program, diagnostics);
        }

        private SequenceDraft BuildRoot(IReadOnlyList<StatementSyntax> statements)
        {
            SequenceDraft root = new();
            string? containingLabel = null;

            foreach (StatementSyntax statement in statements)
            {
                if (statement is LabelStatementSyntax label && IsUsable(label.Identifier))
                {
                    containingLabel = label.Identifier.ValueText;
                }

                InstructionDraft? draft = BuildStatement(statement, containingLabel);
                if (draft is null)
                {
                    continue;
                }

                root.Statements.Add(draft);
            }

            return root;
        }

        private SequenceDraft BuildNested(
            IReadOnlyList<StatementSyntax> statements,
            string? containingLabel)
        {
            SequenceDraft sequence = new();
            foreach (StatementSyntax statement in statements)
            {
                InstructionDraft? draft = BuildStatement(statement, containingLabel);
                if (draft is not null)
                {
                    sequence.Statements.Add(draft);
                }
            }

            return sequence;
        }

        private InstructionDraft? BuildStatement(StatementSyntax statement, string? containingLabel)
        {
            if (statement is BadStatementSyntax)
            {
                _diagnostics.Add(new CompilationDiagnostic(
                    CompilerDiagnosticIds.SkippedInvalidStatement,
                    CompilationDiagnosticSeverity.Error,
                    "The invalid statement was skipped while producing runtime IR.",
                    Source(statement)));
                return null;
            }

            if (statement is MenuStatementSyntax { Choices.Count: 0 })
            {
                _diagnostics.Add(new CompilationDiagnostic(
                    CompilerDiagnosticIds.EmptyMenu,
                    CompilationDiagnosticSeverity.Error,
                    "An empty menu cannot suspend the runtime and was skipped while producing runtime IR.",
                    Source(statement)));
                return null;
            }

            InstructionDraft draft = statement switch
            {
                IfStatementSyntax conditional => BuildConditional(conditional, containingLabel),
                MenuStatementSyntax menu => BuildMenu(menu, containingLabel),
                _ => new InstructionDraft(statement, containingLabel),
            };

            _drafts.Add(draft);
            _draftBySyntax[statement] = draft;

            // Structured children follow their controlling instruction in deterministic source preorder.
            if (draft is BranchDraft branch)
            {
                branch.Then = BuildNested(((IfStatementSyntax)statement).ThenStatements, containingLabel);
                foreach (ElifClauseSyntax clause in ((IfStatementSyntax)statement).ElifClauses)
                {
                    branch.Elifs.Add((clause.Condition, BuildNested(clause.Statements, containingLabel)));
                }

                ElseClauseSyntax? elseClause = ((IfStatementSyntax)statement).ElseClause;
                branch.Else = elseClause is null
                    ? null
                    : BuildNested(elseClause.Statements, containingLabel);
            }
            else if (draft is MenuDraft choice)
            {
                foreach (ChoiceClauseSyntax clause in ((MenuStatementSyntax)statement).Choices)
                {
                    choice.Choices.Add((clause.TextToken.ValueText, BuildNested(clause.Statements, containingLabel)));
                }
            }

            return draft;
        }

        private static BranchDraft BuildConditional(
            IfStatementSyntax conditional,
            string? containingLabel) => new(conditional, containingLabel);

        private static MenuDraft BuildMenu(MenuStatementSyntax menu, string? containingLabel) =>
            new(menu, containingLabel);

        private static void WireSequence(SequenceDraft sequence, InstructionDraft? continuation)
        {
            for (int index = 0; index < sequence.Statements.Count; index++)
            {
                InstructionDraft statement = sequence.Statements[index];
                InstructionDraft? next = index + 1 < sequence.Statements.Count
                    ? sequence.Statements[index + 1]
                    : continuation;
                statement.Continuation = next;

                if (statement is BranchDraft branch)
                {
                    WireSequence(branch.Then, next);
                    foreach ((_, SequenceDraft body) in branch.Elifs)
                    {
                        WireSequence(body, next);
                    }

                    if (branch.Else is not null)
                    {
                        WireSequence(branch.Else, next);
                    }
                }
                else if (statement is MenuDraft menu)
                {
                    foreach ((_, SequenceDraft body) in menu.Choices)
                    {
                        WireSequence(body, next);
                    }
                }
            }
        }

        private VeloraInstruction Materialize(InstructionDraft draft)
        {
            InstructionId id = _ids[draft];
            InstructionId? continuation = IdOf(draft.Continuation);
            SourceLocation source = Source(draft.Syntax);
            string? label = draft.ContainingLabel;

            return draft.Syntax switch
            {
                CharacterDefinitionStatementSyntax character => new DefineCharacterInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    character.Identifier.ValueText,
                    character.DisplayNameToken.ValueText,
                    character.ColorClause?.ColorToken.ValueText ?? "#FFFFFF"),

                LabelStatementSyntax labelSyntax => new LabelInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    labelSyntax.Identifier.ValueText,
                    ReferenceEquals(ResolveLabelTarget(labelSyntax.Identifier.ValueText), draft)),

                SceneStatementSyntax scene => new ChangeSceneInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    scene.ImageToken.ValueText,
                    CompileTransition(scene.Transition)),

                ShowStatementSyntax show => new ShowSpriteInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    show.TargetToken.ValueText,
                    show.ImageValueText,
                    show.ImageTokens.Count != 1 || show.ImageTokens[0].Kind != SyntaxKind.StringLiteralToken,
                    CompilePosition(show.Position),
                    CompileTransition(show.Transition),
                    show.Modifiers.Select(CompileModifier).ToImmutableArray()),

                HideStatementSyntax hide => new HideSpriteInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    hide.TargetToken.ValueText,
                    hide.IsAll,
                    CompileTransition(hide.Transition)),

                DialogueStatementSyntax dialogue => new SayInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    dialogue.SpeakerToken.ValueText,
                    dialogue.TextToken.ValueText),

                NarrationStatementSyntax narration => new SayInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    null,
                    narration.TextToken.ValueText),

                PlayStatementSyntax play => new PlayAudioInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    play.ChannelToken?.ValueText ?? "sfx",
                    play.FileToken.ValueText,
                    play.Modifiers.Select(CompileModifier).ToImmutableArray()),

                StopStatementSyntax stop => new StopAudioInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    stop.ChannelToken?.ValueText ?? "bgm",
                    stop.FadeOutModifier is null ? null : CompileModifier(stop.FadeOutModifier)),

                WaitStatementSyntax wait => new WaitInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    ParseNumber(wait.DurationToken, 0, "wait duration")),

                SetStatementSyntax set => new SetVariableInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    set.Name.ValueText,
                    CompileExpression(set.Expression)),

                JumpStatementSyntax jump => new JumpInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    jump.TargetToken.ValueText,
                    IdOf(ResolveLabelTarget(jump.TargetToken.ValueText))),

                MenuStatementSyntax when draft is MenuDraft menu => new PresentChoiceInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    menu.Choices.Select(choice => new ChoiceTarget(
                        choice.Text,
                        IdOf(Entry(choice.Body, draft.Continuation)))).ToImmutableArray()),

                IfStatementSyntax conditional when draft is BranchDraft branch => new BranchInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    BuildConditionalTargets(conditional, branch, draft.Continuation),
                    IdOf(Entry(branch.Else, draft.Continuation))),

                ConfigStatementSyntax config => new SetConfigInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    config.Key.ValueText,
                    CompileExpression(config.Expression)),

                TweenStatementSyntax tween => new StartTweenInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    tween.TargetToken.ValueText,
                    tween.PropertyToken.ValueText,
                    CompileExpression(tween.Value),
                    ParseModifierNumber(tween.DurationModifier, 1, "tween duration"),
                    tween.EasingToken?.ValueText ?? "ease_out"),

                EmotionStatementSyntax emotion => new TriggerEmotionInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    emotion.CharacterToken.ValueText,
                    emotion.EmotionToken.ValueText),

                InputStatementSyntax input => new RequestInputInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    input.PromptToken.ValueText,
                    input.Target.ValueText,
                    input.Options.Select(static option => option.TextToken.ValueText).ToImmutableArray()),

                MetaStatementSyntax meta => new ExecuteMetaInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    meta.ActionToken.ValueText,
                    meta.Arguments.Select(CompileArgument).ToImmutableArray()),

                ParticleStatementSyntax particle => new ParticleInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    particle.PresetToken.ValueText,
                    particle.NameToken?.ValueText,
                    particle.StopsAll,
                    particle.Parameters.Select(CompileModifier).ToImmutableArray()),

                ScreenDefinitionStatementSyntax screen => new RegisterScreenInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    new ScreenDefinition(
                        screen.NameToken.ValueText,
                        screen.Elements.SelectMany(CompileScreenElement).ToImmutableArray())),

                ShowScreenStatementSyntax showScreen => new ShowScreenInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    showScreen.ScreenNameToken.ValueText),

                HideScreenStatementSyntax hideScreen => new HideScreenInstruction(
                    id,
                    source,
                    label,
                    continuation,
                    hideScreen.ScreenNameToken.ValueText),

                _ => throw new InvalidOperationException(
                    $"Unsupported instruction syntax {draft.Syntax.GetType().Name}."),
            };
        }

        private ImmutableArray<ConditionalTarget> BuildConditionalTargets(
            IfStatementSyntax syntax,
            BranchDraft draft,
            InstructionDraft? continuation)
        {
            ImmutableArray<ConditionalTarget>.Builder targets = ImmutableArray.CreateBuilder<ConditionalTarget>();
            targets.Add(new ConditionalTarget(
                CompileExpression(syntax.Condition),
                IdOf(Entry(draft.Then, continuation))));

            foreach ((ExpressionSyntax condition, SequenceDraft body) in draft.Elifs)
            {
                targets.Add(new ConditionalTarget(
                    CompileExpression(condition),
                    IdOf(Entry(body, continuation))));
            }

            return targets.ToImmutable();
        }

        private IEnumerable<ScreenElementDefinition> CompileScreenElement(ScreenElementSyntax syntax)
        {
            if (syntax is BadScreenElementSyntax)
            {
                _diagnostics.Add(new CompilationDiagnostic(
                    CompilerDiagnosticIds.InvalidScreenElement,
                    CompilationDiagnosticSeverity.Error,
                    "The invalid screen element was skipped while producing runtime IR.",
                    Source(syntax)));
                return [];
            }

            ScreenElementKind kind;
            string? text = null;
            string? imagePath = null;
            IEnumerable<ScreenElementDefinition> children = [];
            switch (syntax)
            {
                case TextScreenElementSyntax textElement:
                    kind = ScreenElementKind.Text;
                    text = textElement.TextToken.ValueText;
                    break;

                case ImageButtonScreenElementSyntax imageButton:
                    kind = ScreenElementKind.ImageButton;
                    imagePath = imageButton.ImageToken.ValueText;
                    break;

                case PanelScreenElementSyntax panel:
                    kind = ScreenElementKind.Panel;
                    children = panel.Children.SelectMany(CompileScreenElement);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported screen element {syntax.GetType().Name}.");
            }

            ImmutableArray<ScreenPropertyDefinition> properties = syntax.Properties
                .Select(property => new ScreenPropertyDefinition(
                    property.NameToken.ValueText,
                    property.ValueTokens.Select(CompileArgument),
                    Source(property)))
                .ToImmutableArray();
            ScreenActionDefinition? action = CompileScreenAction(syntax.Properties);

            return
            [
                new ScreenElementDefinition(
                    kind,
                    text,
                    imagePath,
                    properties,
                    children,
                    action,
                    Source(syntax)),
            ];
        }

        private ScreenActionDefinition? CompileScreenAction(IReadOnlyList<ScreenPropertySyntax> properties)
        {
            ScreenPropertySyntax? property = properties.LastOrDefault(static property =>
                property.NameToken.ValueText == "action");
            if (property is null || property.ValueTokens.Count == 0)
            {
                return null;
            }

            if (property.ValueTokens[0].Kind == SyntaxKind.JumpKeyword)
            {
                string label = property.ValueTokens.Count > 1
                    ? property.ValueTokens[1].ValueText
                    : string.Empty;
                return new ScreenActionDefinition(
                    ScreenActionKind.Jump,
                    label,
                    IdOf(ResolveLabelTarget(label)),
                    Source(property));
            }

            return new ScreenActionDefinition(
                ScreenActionKind.Command,
                property.ValueTokens[0].ValueText,
                null,
                Source(property));
        }

        private VeloraExpression CompileExpression(ExpressionSyntax syntax) => syntax switch
        {
            LiteralExpressionSyntax literal => CompileLiteral(literal),
            NameExpressionSyntax name => new VariableExpression(name.Name.ValueText, Source(name)),
            UnaryExpressionSyntax unary => new UnaryExpression(
                unary.OperatorToken.Kind == SyntaxKind.NotKeyword ? UnaryOperator.Not : UnaryOperator.Negate,
                CompileExpression(unary.Operand),
                Source(unary)),
            BinaryExpressionSyntax binary => new BinaryExpression(
                CompileExpression(binary.Left),
                CompileBinaryOperator(binary.OperatorToken.Kind),
                CompileExpression(binary.Right),
                Source(binary)),
            ParenthesizedExpressionSyntax parenthesized => CompileExpression(parenthesized.Expression),
            MissingExpressionSyntax => InvalidExpression(syntax),
            _ => InvalidExpression(syntax),
        };

        private VeloraExpression CompileLiteral(LiteralExpressionSyntax literal) =>
            literal.LiteralToken.Kind switch
            {
                SyntaxKind.StringLiteralToken => new StringExpression(
                    literal.LiteralToken.ValueText,
                    Source(literal)),
                SyntaxKind.NumberLiteralToken => new NumberExpression(
                    ParseNumber(literal.LiteralToken, 0, "numeric expression"),
                    Source(literal)),
                SyntaxKind.TrueKeyword => new BooleanExpression(true, Source(literal)),
                SyntaxKind.FalseKeyword => new BooleanExpression(false, Source(literal)),
                SyntaxKind.ColorLiteralToken => new ColorExpression(
                    literal.LiteralToken.ValueText,
                    Source(literal)),
                _ => InvalidExpression(literal),
            };

        private ErrorExpression InvalidExpression(SyntaxNode syntax)
        {
            _diagnostics.Add(new CompilationDiagnostic(
                CompilerDiagnosticIds.InvalidExpression,
                CompilationDiagnosticSeverity.Error,
                "A recoverable error expression was emitted because the source expression is incomplete.",
                Source(syntax)));
            return new ErrorExpression(Source(syntax));
        }

        private static BinaryOperator CompileBinaryOperator(SyntaxKind kind) => kind switch
        {
            SyntaxKind.PlusToken => BinaryOperator.Add,
            SyntaxKind.MinusToken => BinaryOperator.Subtract,
            SyntaxKind.StarToken => BinaryOperator.Multiply,
            SyntaxKind.SlashToken => BinaryOperator.Divide,
            SyntaxKind.EqualsEqualsToken => BinaryOperator.Equal,
            SyntaxKind.BangEqualsToken => BinaryOperator.NotEqual,
            SyntaxKind.LessThanToken => BinaryOperator.LessThan,
            SyntaxKind.GreaterThanToken => BinaryOperator.GreaterThan,
            SyntaxKind.LessThanOrEqualsToken => BinaryOperator.LessThanOrEqual,
            SyntaxKind.GreaterThanOrEqualsToken => BinaryOperator.GreaterThanOrEqual,
            SyntaxKind.AndKeyword => BinaryOperator.And,
            SyntaxKind.OrKeyword => BinaryOperator.Or,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown binary operator."),
        };

        private TransitionDefinition? CompileTransition(TransitionClauseSyntax? syntax)
        {
            if (syntax is null)
            {
                return null;
            }

            double duration = syntax.DurationToken is null
                ? 1
                : ParseNumber(syntax.DurationToken, 1, "transition duration");
            return new TransitionDefinition(syntax.NameToken.ValueText, duration);
        }

        private SpritePositionDefinition? CompilePosition(PositionClauseSyntax? syntax)
        {
            if (syntax is null || syntax.ValueTokens.Count == 0)
            {
                return null;
            }

            if (syntax.ValueTokens[0].Kind == SyntaxKind.IdentifierToken)
            {
                return new SpritePositionDefinition(
                    SpritePositionKind.Preset,
                    syntax.ValueTokens[0].ValueText,
                    0.5,
                    0.5);
            }

            SyntaxToken[] numbers = syntax.ValueTokens
                .Where(static token => token.Kind == SyntaxKind.NumberLiteralToken)
                .ToArray();
            double x = numbers.Length > 0 ? ParseNumber(numbers[0], 0.5, "sprite x position") : 0.5;
            double y = numbers.Length > 1 ? ParseNumber(numbers[1], 0.5, "sprite y position") : 0.5;
            return new SpritePositionDefinition(SpritePositionKind.Coordinates, null, x, y);
        }

        private static IrModifier CompileModifier(ModifierSyntax syntax) => new(
            syntax.NameToken.ValueText,
            syntax.ArgumentTokens.Select(CompileArgument));

        private static IrArgument CompileArgument(SyntaxToken token) => new(
            token.Kind switch
            {
                SyntaxKind.StringLiteralToken => IrArgumentKind.Text,
                SyntaxKind.NumberLiteralToken => IrArgumentKind.Number,
                SyntaxKind.ColorLiteralToken => IrArgumentKind.Color,
                SyntaxKind.CommaToken or SyntaxKind.MinusToken => IrArgumentKind.Punctuation,
                _ => IrArgumentKind.Identifier,
            },
            token.ValueText);

        private double ParseModifierNumber(ModifierSyntax? modifier, double fallback, string context)
        {
            if (modifier is null)
            {
                return fallback;
            }

            SyntaxToken? number = modifier.ArgumentTokens.FirstOrDefault(static token =>
                token.Kind == SyntaxKind.NumberLiteralToken);
            if (number is null)
            {
                return fallback;
            }

            double value = ParseNumber(number, fallback, context);
            return modifier.ArgumentTokens.Any(static token => token.Kind == SyntaxKind.MinusToken)
                ? -value
                : value;
        }

        private double ParseNumber(SyntaxToken token, double fallback, string context)
        {
            string text = token.ValueText.EndsWith('s') ? token.ValueText[..^1] : token.ValueText;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) &&
                double.IsFinite(value))
            {
                return value;
            }

            _diagnostics.Add(new CompilationDiagnostic(
                CompilerDiagnosticIds.InvalidNumericValue,
                CompilationDiagnosticSeverity.Error,
                $"Invalid {context}; the compiler used {fallback.ToString(CultureInfo.InvariantCulture)}.",
                Source(token.Span.Start, token.Span.Length)));
            return fallback;
        }

        private ImmutableArray<InstructionId> BuildInitialization(
            SequenceDraft root,
            InstructionDraft? start)
        {
            if (start is null)
            {
                return [];
            }

            ImmutableArray<InstructionId>.Builder result = ImmutableArray.CreateBuilder<InstructionId>();
            foreach (InstructionDraft draft in root.Statements)
            {
                if (ReferenceEquals(draft, start))
                {
                    break;
                }

                if (draft.Syntax is CharacterDefinitionStatementSyntax or SetStatementSyntax or
                    ConfigStatementSyntax or MetaStatementSyntax or ScreenDefinitionStatementSyntax)
                {
                    result.Add(_ids[draft]);
                }
            }

            return result.ToImmutable();
        }

        private ImmutableArray<GlobalDefinition> BuildDefinitions()
        {
            IEnumerable<GlobalDefinition> definitions = _semanticModel.Symbols.Select(symbol =>
            {
                InstructionId? effective = symbol.EffectiveDefinition is null
                    ? null
                    : IdOf(_draftBySyntax.GetValueOrDefault(symbol.EffectiveDefinition.Syntax));
                return new GlobalDefinition(
                    symbol.Kind switch
                    {
                        SymbolKind.Label => GlobalDefinitionKind.Label,
                        SymbolKind.Character => GlobalDefinitionKind.Character,
                        SymbolKind.Screen => GlobalDefinitionKind.Screen,
                        SymbolKind.Variable => GlobalDefinitionKind.Variable,
                        _ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol.Kind, "Unknown symbol kind."),
                    },
                    symbol.Name,
                    effective,
                    symbol.IsImplicit);
            });

            return definitions
                .OrderBy(static definition => GetDefinitionSortOrder(definition.Kind))
                .ThenBy(static definition => definition.Name, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static int GetDefinitionSortOrder(GlobalDefinitionKind kind) => kind switch
        {
            GlobalDefinitionKind.Label => 0,
            GlobalDefinitionKind.Character => 1,
            GlobalDefinitionKind.Screen => 2,
            GlobalDefinitionKind.Variable => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown global definition kind."),
        };

        private InstructionId? IdOf(InstructionDraft? draft) =>
            draft is null ? null : _ids[draft];

        private InstructionDraft? ResolveLabelTarget(string name)
        {
            SymbolDefinition? definition = _semanticModel
                .LookupSymbol(SymbolKind.Label, name)
                ?.EffectiveDefinition;
            return definition is null
                ? null
                : _draftBySyntax.GetValueOrDefault(definition.Syntax);
        }

        private static InstructionDraft? Entry(SequenceDraft? sequence, InstructionDraft? fallback) =>
            sequence is null || sequence.Statements.Count == 0 ? fallback : sequence.Statements[0];

        private SourceLocation Source(SyntaxNode syntax) => Source(syntax.Span.Start, syntax.Span.Length);

        private SourceLocation Source(int start, int length) => new(_filePath, start, length);

        private static bool IsUsable(SyntaxToken token) => !token.IsMissing && token.ValueText.Length != 0;

        private static CompilationDiagnosticSeverity ConvertSeverity(DiagnosticSeverity severity) => severity switch
        {
            DiagnosticSeverity.Info => CompilationDiagnosticSeverity.Info,
            DiagnosticSeverity.Warning => CompilationDiagnosticSeverity.Warning,
            DiagnosticSeverity.Error => CompilationDiagnosticSeverity.Error,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown diagnostic severity."),
        };

        private sealed class SequenceDraft
        {
            public List<InstructionDraft> Statements { get; } = [];
        }

        private class InstructionDraft(StatementSyntax syntax, string? containingLabel)
        {
            public StatementSyntax Syntax { get; } = syntax;

            public string? ContainingLabel { get; } = containingLabel;

            public InstructionDraft? Continuation { get; set; }
        }

        private sealed class BranchDraft(IfStatementSyntax syntax, string? containingLabel)
            : InstructionDraft(syntax, containingLabel)
        {
            public SequenceDraft Then { get; set; } = new();

            public List<(ExpressionSyntax Condition, SequenceDraft Body)> Elifs { get; } = [];

            public SequenceDraft? Else { get; set; }
        }

        private sealed class MenuDraft(MenuStatementSyntax syntax, string? containingLabel)
            : InstructionDraft(syntax, containingLabel)
        {
            public List<(string Text, SequenceDraft Body)> Choices { get; } = [];
        }
    }
}
