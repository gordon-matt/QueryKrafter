using System.Linq.Expressions;

namespace QueryKrafter.Linq;

/// <summary>Builds a custom operator: the property being filtered and the rule's values, each typed to match the property.</summary>
public delegate Expression QueryOperatorExpression(Expression property, IReadOnlyList<Expression> values);

public sealed record QueryExpressionOptions
{
    public static QueryExpressionOptions Default { get; } = new();

    /// <summary>
    /// Compares text with <see cref="string.ToLower()"/> on both sides. Leave off for databases
    /// whose collation already ignores case, where it would only stop indexes being used.
    /// </summary>
    public bool CaseInsensitive { get; init; }

    /// <summary>Guards each step of a member path against null, as in-memory collections need.</summary>
    public bool NullSafe { get; init; } = true;

    /// <summary>Replaces or adds operator translations, by operator type.</summary>
    public IReadOnlyDictionary<string, QueryOperatorExpression>? Operators { get; init; }
}
