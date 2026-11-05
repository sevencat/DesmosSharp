using DesmosSharp.Core.Parsing;

namespace DesmosSharp.Core.Tests;

public class ParserTests
{
    [Fact]
    public void Implicit_multiplication_is_parsed()
    {
        var node = Parser.Parse("2x");

        var binary = Assert.IsType<BinaryNode>(node);
        Assert.Equal(BinaryOperator.Multiply, binary.Operator);
        Assert.IsType<NumberNode>(binary.Left);
        Assert.Equal("x", Assert.IsType<IdentifierNode>(binary.Right).Name);
    }

    [Fact]
    public void Parenthesized_pair_is_a_point()
    {
        var node = Parser.Parse("(1, 2)");

        var point = Assert.IsType<PointNode>(node);
        Assert.Equal(1, Assert.IsType<NumberNode>(point.X).Value);
        Assert.Equal(2, Assert.IsType<NumberNode>(point.Y).Value);
    }

    [Fact]
    public void Absolute_value_becomes_abs_call()
    {
        var node = Parser.Parse("|x|");

        var call = Assert.IsType<CallNode>(node);
        Assert.Equal("abs", call.Name);
        Assert.Single(call.Arguments);
    }

    [Fact]
    public void Absolute_values_can_be_combined()
    {
        var node = Parser.Parse("|x|+|y|");

        var binary = Assert.IsType<BinaryNode>(node);
        Assert.Equal(BinaryOperator.Add, binary.Operator);
        Assert.Equal("abs", Assert.IsType<CallNode>(binary.Left).Name);
        Assert.Equal("abs", Assert.IsType<CallNode>(binary.Right).Name);
    }

    [Fact]
    public void Scalar_times_absolute_value_is_a_product()
    {
        var node = Parser.Parse("2|x|");

        var binary = Assert.IsType<BinaryNode>(node);
        Assert.Equal(BinaryOperator.Multiply, binary.Operator);
        Assert.IsType<CallNode>(binary.Right);
    }

    [Fact]
    public void Restriction_follows_absolute_value()
    {
        var node = Parser.Parse("y=|x|{x<0}");

        var compare = Assert.IsType<CompareNode>(node);
        var restricted = Assert.IsType<RestrictedNode>(compare.Right);
        Assert.IsType<CallNode>(restricted.Value);
    }

    [Fact]
    public void Restriction_is_attached_to_the_value()
    {
        var node = Parser.Parse("x^2{y>0}");

        var restricted = Assert.IsType<RestrictedNode>(node);
        Assert.IsType<BinaryNode>(restricted.Value);
        Assert.IsType<CompareNode>(restricted.Condition);
    }

    [Fact]
    public void Function_definition_parses_as_comparison()
    {
        var node = Parser.Parse("f(x) = x^2");

        var compare = Assert.IsType<CompareNode>(node);
        Assert.Equal(CompareOperator.Equal, compare.Operator);
        var signature = Assert.IsType<CallNode>(compare.Left);
        Assert.Equal("f", signature.Name);
        Assert.Equal("x", Assert.IsType<IdentifierNode>(Assert.Single(signature.Arguments)).Name);
    }

    [Fact]
    public void Subscripts_are_part_of_the_identifier()
    {
        var node = Parser.Parse("x_1 + log_2(8)");

        var binary = Assert.IsType<BinaryNode>(node);
        Assert.Equal("x_1", Assert.IsType<IdentifierNode>(binary.Left).Name);
        Assert.Equal("log_2", Assert.IsType<CallNode>(binary.Right).Name);
    }

    [Fact]
    public void Unicode_symbols_are_normalized()
    {
        var node = Parser.Parse("π + θ");

        var binary = Assert.IsType<BinaryNode>(node);
        Assert.Equal("pi", Assert.IsType<IdentifierNode>(binary.Left).Name);
        Assert.Equal("theta", Assert.IsType<IdentifierNode>(binary.Right).Name);
    }

    [Fact]
    public void Top_level_commas_produce_a_list()
    {
        var node = Parser.Parse("(1,1), (2,2)");

        var list = Assert.IsType<ListNode>(node);
        Assert.Equal(2, list.Items.Count);
        Assert.All(list.Items, item => Assert.IsType<PointNode>(item));
    }

    [Fact]
    public void Incomplete_expression_throws_with_position()
    {
        var error = Assert.Throws<ParseException>(() => Parser.Parse("1+"));

        Assert.True(error.Position >= 0);
    }

    [Fact]
    public void Empty_expression_throws()
    {
        Assert.Throws<ParseException>(() => Parser.Parse("   "));
    }

    [Fact]
    public void Empty_list_throws()
    {
        Assert.Throws<ParseException>(() => Parser.Parse("[]"));
    }
}
