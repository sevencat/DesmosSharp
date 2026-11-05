namespace DesmosSharp.Core.Parsing;

public enum UnaryOperator
{
    Negate,
    Plus,
}

public enum BinaryOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Power,
}

public enum CompareOperator
{
    Equal,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
}

/// <summary>表达式抽象语法树节点。</summary>
public abstract record ExpressionNode;

public sealed record NumberNode(double Value) : ExpressionNode;

/// <summary>变量或常量引用。下标形如 <c>x_1</c> 会整体作为名字保存。</summary>
public sealed record IdentifierNode(string Name) : ExpressionNode;

public sealed record UnaryNode(UnaryOperator Operator, ExpressionNode Operand) : ExpressionNode;

public sealed record BinaryNode(BinaryOperator Operator, ExpressionNode Left, ExpressionNode Right) : ExpressionNode;

public sealed record CallNode(string Name, IReadOnlyList<ExpressionNode> Arguments) : ExpressionNode;

public sealed record FactorialNode(ExpressionNode Operand) : ExpressionNode;

public sealed record ListNode(IReadOnlyList<ExpressionNode> Items) : ExpressionNode;

/// <summary>点 <c>(x, y)</c>。</summary>
public sealed record PointNode(ExpressionNode X, ExpressionNode Y) : ExpressionNode;

public sealed record CompareNode(CompareOperator Operator, ExpressionNode Left, ExpressionNode Right) : ExpressionNode;

/// <summary>带定义域限制的表达式，例如 <c>|x|{x &lt; 0}</c>。</summary>
public sealed record RestrictedNode(ExpressionNode Value, ExpressionNode Condition) : ExpressionNode;

public sealed record PiecewiseBranch(ExpressionNode Condition, ExpressionNode Value);

/// <summary>分段表达式，例如 <c>{x &lt; 0: -x, x &gt;= 0: x}</c>。</summary>
public sealed record PiecewiseNode(IReadOnlyList<PiecewiseBranch> Branches) : ExpressionNode;
