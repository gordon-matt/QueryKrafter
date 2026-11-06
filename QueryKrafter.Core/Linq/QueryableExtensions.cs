using System.Diagnostics.CodeAnalysis;

namespace QueryKrafter.Linq;

/// <summary>
/// Provides extension methods for queryables.
/// </summary>
public static class QueryableExtensions
{
    extension<T>(IQueryable<T> source)
    {
        /// <summary>Filters by the rules, translated by the query provider (e.g. into SQL by Entity Framework).</summary>
        [RequiresUnreferencedCode(QueryExpressionBuilder.ReflectionMessage)]
        [RequiresDynamicCode(QueryExpressionBuilder.ReflectionMessage)]
        public Result<IQueryable<T>> ApplyQuery(QueryGroup root, QueryBuilderConfig config, QueryExpressionOptions? options = null) =>
            QueryExpressionBuilder.Build<T>(root, config, options).Map(predicate => source.Where(predicate));
    }

    extension<T>(IEnumerable<T> source)
    {
        /// <summary>
        /// Applies the query.
        /// </summary>
        /// <param name="root">The root.</param>
        /// <param name="config">The config.</param>
        /// <param name="options">The options.</param>
        /// <returns>The outcome for the ts.</returns>
        [RequiresUnreferencedCode(QueryExpressionBuilder.ReflectionMessage)]
        [RequiresDynamicCode(QueryExpressionBuilder.ReflectionMessage)]
        public Result<IEnumerable<T>> ApplyQuery(QueryGroup root, QueryBuilderConfig config, QueryExpressionOptions? options = null) =>
            QueryExpressionBuilder.Build<T>(root, config, options).Map(predicate => source.Where(predicate.Compile()));
    }
}
