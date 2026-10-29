namespace Velora.Script.Compiler.Tests;

public sealed class ExpressionLoweringTests
{
    [Fact]
    public void LowersEveryExpressionOperatorToStableEnum()
    {
        const string source = """
            set add = 1 + 2
            set subtract = 2 - 1
            set multiply = 2 * 3
            set divide = 6 / 2
            set equal = 1 == 1
            set not_equal = 1 != 2
            set less = 1 < 2
            set greater = 2 > 1
            set less_equal = 1 <= 2
            set greater_equal = 2 >= 1
            set and_value = true and false
            set or_value = true or false
            """;
        VeloraProgram program = CompilerTestHelper.Compile(source).Program;

        Assert.Equal(
            Enum.GetValues<BinaryOperator>(),
            program.Instructions
                .Cast<SetVariableInstruction>()
                .Select(static instruction => Assert.IsType<BinaryExpression>(instruction.Value).Operator));
    }

    [Fact]
    public void PreservesPrecedenceAndParenthesizedShape()
    {
        SetVariableInstruction set = Assert.Single(
            CompilerTestHelper.Compile("set result = (1 + 2) * 3 >= 9 and not false").Program.Instructions
                .OfType<SetVariableInstruction>());
        BinaryExpression and = Assert.IsType<BinaryExpression>(set.Value);
        Assert.Equal(BinaryOperator.And, and.Operator);
        BinaryExpression comparison = Assert.IsType<BinaryExpression>(and.Left);
        Assert.Equal(BinaryOperator.GreaterThanOrEqual, comparison.Operator);
        BinaryExpression multiply = Assert.IsType<BinaryExpression>(comparison.Left);
        Assert.Equal(BinaryOperator.Multiply, multiply.Operator);
        Assert.Equal(BinaryOperator.Add, Assert.IsType<BinaryExpression>(multiply.Left).Operator);
        Assert.Equal(UnaryOperator.Not, Assert.IsType<UnaryExpression>(and.Right).Operator);
    }

    [Fact]
    public void LowersLiteralAndVariableKinds()
    {
        const string source = """
            set message = "value"
            set number = 1.5
            set yes = true
            set no = false
            set color = #ABCDEF
            set copy = player.name
            """;
        SetVariableInstruction[] instructions =
        [
            .. CompilerTestHelper.Compile(source).Program.Instructions.OfType<SetVariableInstruction>(),
        ];

        Assert.IsType<StringExpression>(instructions[0].Value);
        Assert.IsType<NumberExpression>(instructions[1].Value);
        Assert.IsType<BooleanExpression>(instructions[2].Value);
        Assert.IsType<BooleanExpression>(instructions[3].Value);
        Assert.IsType<ColorExpression>(instructions[4].Value);
        Assert.Equal("player.name", Assert.IsType<VariableExpression>(instructions[5].Value).Name);
    }

    [Fact]
    public void LowersUnaryNegationAndNumberSuffix()
    {
        VeloraProgram program = CompilerTestHelper.Compile("set value = -1.25\nwait 2s").Program;
        UnaryExpression unary = Assert.IsType<UnaryExpression>(
            Assert.Single(program.Instructions.OfType<SetVariableInstruction>()).Value);

        Assert.Equal(UnaryOperator.Negate, unary.Operator);
        Assert.Equal(1.25, Assert.IsType<NumberExpression>(unary.Operand).Value);
        Assert.Equal(2, Assert.Single(program.Instructions.OfType<WaitInstruction>()).DurationSeconds);
    }

    [Fact]
    public void ExpressionNodesCarrySyntaxIndependentUtf16Locations()
    {
        const string source = "set greeting = emoji + \"🌙\"";
        BinaryExpression expression = Assert.IsType<BinaryExpression>(
            Assert.Single(CompilerTestHelper.Compile(source, "unicode.vel").Program.Instructions
                .OfType<SetVariableInstruction>()).Value);

        Assert.Equal("unicode.vel", expression.Source.FilePath);
        Assert.Equal(source.IndexOf("emoji", StringComparison.Ordinal), expression.Source.Start);
        Assert.Equal("emoji + \"🌙\"".Length, expression.Source.Length);
    }
}
