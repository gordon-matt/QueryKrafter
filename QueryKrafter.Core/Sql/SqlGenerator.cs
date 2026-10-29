using System.Text;
using QueryKrafter.Serialization;
using QueryKrafter.Validation;
using QueryKrafter.Values;

namespace QueryKrafter.Sql;

/// <summary>
/// Turns rules into a parameterized WHERE clause. Each rule is matched to a filter in the
/// configuration by its <c>id</c>, and the filter's SQL is written, never the <c>field</c> the
/// rule arrived with. Values are parameters unless <see cref="SqlParameterStyle.Inline"/> is chosen.
/// Rules are validated first; invalid rules give an invalid result.
/// </summary>
public static class SqlGenerator
{
    public static Result<SqlQuery> Generate(QueryGroup root, QueryBuilderConfig config, SqlGeneratorOptions? options = null)
    {
        var check = QueryValidator.ToResult(QueryValidator.Validate(root, config), config);
        if (!check.IsSuccess)
        {
            return Result<SqlQuery>.Invalid([.. check.ValidationErrors]);
        }

        var writer = new Writer(config, options ?? SqlGeneratorOptions.Default);
        string sql = writer.Group(root, level: 0, isRoot: true);

        if (writer.Errors.Count > 0)
        {
            return Result<SqlQuery>.Error(new ErrorList(writer.Errors));
        }

        return sql.Length == 0 ? SqlQuery.Empty : new SqlQuery(sql, writer.Parameters);
    }

    /// <summary>Parses rules JSON in the jQuery QueryBuilder format, then generates SQL from it.</summary>
    public static Result<SqlQuery> Generate(string? rulesJson, QueryBuilderConfig config, SqlGeneratorOptions? options = null)
    {
        var parsed = QueryJson.TryParse(rulesJson);
        return parsed.IsSuccess
            ? Generate(parsed.Value, config, options)
            : Result<SqlQuery>.Invalid([.. parsed.ValidationErrors]);
    }

    private sealed class Writer(QueryBuilderConfig config, SqlGeneratorOptions options)
    {
        private const string Indent = "    ";

        private SqlDialect Dialect => options.Dialect;

        public List<SqlQueryParameter> Parameters { get; } = [];

        public List<string> Errors { get; } = [];

        /// <param name="level">How far the lines this group is written on are indented, when indenting.</param>
        public string Group(QueryGroup group, int level, bool isRoot)
        {
            int count = group.Rules.Count(node => node is QueryRule || (node is QueryGroup child && child.AllRules().Any()));
            if (count == 0)
            {
                return string.Empty;
            }

            bool needsParentheses = group.Not || (!isRoot && count > 1);
            int innerLevel = needsParentheses ? level + 1 : level;

            var parts = new List<string>(count);
            foreach (var node in group.Rules)
            {
                string part = node switch
                {
                    QueryGroup child => Group(child, innerLevel, isRoot: false),
                    QueryRule rule => Rule(rule),
                    _ => string.Empty,
                };

                if (part.Length > 0)
                {
                    parts.Add(part);
                }
            }

            // The configuration's spelling is written, not the rule's, since the comparison ignores case.
            string condition = config.Conditions.First(c => string.Equals(c, group.Condition, StringComparison.OrdinalIgnoreCase));
            string not = group.Not ? "NOT " : string.Empty;

            if (!options.Indented)
            {
                string body = string.Join($" {condition} ", parts);
                return needsParentheses ? $"{not}({body})" : body;
            }

            string padding = Padding(innerLevel);
            string lines = string.Join($"\n{padding}{condition} ", parts);
            return needsParentheses ? $"{not}(\n{padding}{lines}\n{Padding(level)})" : lines;
        }

        private static string Padding(int level) => string.Concat(Enumerable.Repeat(Indent, level));

        private string Rule(QueryRule rule)
        {
            if (config.FindFilter(rule.Id) is not { } filter || config.FindOperator(rule.Operator) is not { } op)
            {
                return string.Empty;
            }

            var sql = options.Operators?.GetValueOrDefault(op.Type) ?? SqlOperators.Defaults.GetValueOrDefault(op.Type);
            if (sql is null)
            {
                Errors.Add($"There is no SQL translation for the operator \"{op.Type}\".");
                return string.Empty;
            }

            string field = options.FieldResolver?.Invoke(filter, Dialect)
                ?? (string.IsNullOrWhiteSpace(filter.Field) ? Dialect.QuoteIdentifier(filter.Id) : filter.Field);

            var values = RuleValues.Typed(rule, filter, op);
            if (!op.Multiple && op.InputCount == 1 && values.Count > 1)
            {
                values = [values[0]];
            }

            return Fill(sql, field, op, values);
        }

        private string Fill(SqlOperator sql, string field, QueryOperator op, IReadOnlyList<object?> values)
        {
            var output = new StringBuilder(sql.Template.Length + field.Length + 16);
            string template = sql.Template;
            int next = 0;

            for (int i = 0; i < template.Length; i++)
            {
                if (template[i] == '{')
                {
                    if (Placeholder(template, i, "{field}"))
                    {
                        output.Append(field);
                        i += "{field}".Length - 1;
                        continue;
                    }

                    if (Placeholder(template, i, "{escape}"))
                    {
                        if (sql.EscapeLike)
                        {
                            output.Append(Dialect.LikeEscapeClause);
                        }

                        i += "{escape}".Length - 1;
                        continue;
                    }

                    if (Placeholder(template, i, "{value}"))
                    {
                        if (op.Multiple)
                        {
                            output.AppendJoin(", ", values.Select(v => Value(sql, v)));
                            next = values.Count;
                        }
                        else
                        {
                            output.Append(Value(sql, next < values.Count ? values[next] : null));
                            next++;
                        }

                        i += "{value}".Length - 1;
                        continue;
                    }
                }

                output.Append(template[i]);
            }

            return output.ToString();
        }

        private string Value(SqlOperator sql, object? value)
        {
            if (value is string text && sql.EscapeLike)
            {
                value = string.Format(CultureInfo.InvariantCulture, sql.ValueFormat, Dialect.EscapeLike(text));
            }
            else if (value is not null && sql.ValueFormat != "{0}")
            {
                value = string.Format(CultureInfo.InvariantCulture, sql.ValueFormat, value);
            }

            if (options.ParameterStyle == SqlParameterStyle.Inline)
            {
                return Dialect.FormatLiteral(value);
            }

            int position = Parameters.Count + 1;
            string name = options.ParameterPrefix + position.ToString(CultureInfo.InvariantCulture);
            Parameters.Add(new SqlQueryParameter(name, ToParameterValue(value)));
            return Dialect.Parameter(name, position, options.ParameterStyle);
        }

        /// <summary>Most ADO.NET providers still expect <see cref="DateTime"/> and <see cref="TimeSpan"/> rather than the date-only and time-only types.</summary>
        private static object? ToParameterValue(object? value) => value switch
        {
            DateOnly date => date.ToDateTime(TimeOnly.MinValue),
            TimeOnly time => time.ToTimeSpan(),
            _ => value,
        };

        private static bool Placeholder(string template, int index, string placeholder) =>
            string.CompareOrdinal(template, index, placeholder, 0, placeholder.Length) == 0;
    }
}
