using System.Diagnostics.CodeAnalysis;

namespace QueryKrafter.Linq;

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
        [RequiresUnreferencedCode(QueryExpressionBuilder.ReflectionMessage)]
        [RequiresDynamicCode(QueryExpressionBuilder.ReflectionMessage)]
        public Result<IEnumerable<T>> ApplyQuery(QueryGroup root, QueryBuilderConfig config, QueryExpressionOptions? options = null) =>
            QueryExpressionBuilder.Build<T>(root, config, options).Map(predicate => source.Where(predicate.Compile()));
    }
}
