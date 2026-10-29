using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using QueryKrafter.Validation;
using QueryKrafter.Values;

namespace QueryKrafter.Linq;

/// <summary>
/// Turns rules into a predicate for LINQ, so the same rules filter an in-memory list or an Entity
/// Framework query. Values are captured as closures rather than constants, so EF sends them as
/// parameters. Rules are validated first; invalid rules give an invalid result.
/// </summary>
public static class QueryExpressionBuilder
{
    public const string ReflectionMessage = "Properties are looked up by name and generic methods are made at run time.";

    private static readonly MethodInfo StartsWithMethod = typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;
    private static readonly MethodInfo EndsWithMethod = typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!;
    private static readonly MethodInfo ContainsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
    private static readonly MethodInfo ToLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
    private static readonly MethodInfo CompareMethod = typeof(string).GetMethod(nameof(string.Compare), [typeof(string), typeof(string)])!;
    private static readonly MethodInfo EnumerableContainsMethod = typeof(Enumerable).GetMethods()
        .Single(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2);

    [RequiresUnreferencedCode(ReflectionMessage)]
    [RequiresDynamicCode(ReflectionMessage)]
    public static Result<Expression<Func<T, bool>>> Build<T>(QueryGroup root, QueryBuilderConfig config, QueryExpressionOptions? options = null)
    {
        var check = QueryValidator.ToResult(QueryValidator.Validate(root, config), config);
        if (!check.IsSuccess)
        {
            return Result<Expression<Func<T, bool>>>.Invalid([.. check.ValidationErrors]);
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        var builder = new Builder(config, options ?? QueryExpressionOptions.Default);
        var body = builder.Group(root, parameter);

        if (builder.Errors.Count > 0)
        {
            return Result<Expression<Func<T, bool>>>.Error(new ErrorList(builder.Errors));
        }

        return Expression.Lambda<Func<T, bool>>(body ?? Expression.Constant(true), parameter);
    }

    [RequiresUnreferencedCode(ReflectionMessage)]
    [RequiresDynamicCode(ReflectionMessage)]
    private sealed class Builder(QueryBuilderConfig config, QueryExpressionOptions options)
    {
        public List<string> Errors { get; } = [];

        public Expression? Group(QueryGroup group, ParameterExpression parameter)
        {
            bool isOr = string.Equals(group.Condition, Constants.Conditions.Or, StringComparison.OrdinalIgnoreCase);
            if (!isOr && !string.Equals(group.Condition, Constants.Conditions.And, StringComparison.OrdinalIgnoreCase))
            {
                Errors.Add($"LINQ supports AND and OR groups only, not \"{group.Condition}\".");
                return null;
            }

            Expression? body = null;
            foreach (var node in group.Rules)
            {
                var part = node switch
                {
                    QueryGroup child => Group(child, parameter),
                    QueryRule rule => Rule(rule, parameter),
                    _ => null,
                };

                if (part is null)
                {
                    continue;
                }

                body = body is null ? part : isOr ? Expression.OrElse(body, part) : Expression.AndAlso(body, part);
            }

            return body is not null && group.Not ? Expression.Not(body) : body;
        }

        private Expression? Rule(QueryRule rule, ParameterExpression parameter)
        {
            if (config.FindFilter(rule.Id) is not { } filter || config.FindOperator(rule.Operator) is not { } op)
            {
                return null;
            }

            string path = string.IsNullOrWhiteSpace(filter.Property) ? filter.Id : filter.Property;
            var guards = new List<Expression>();
            if (Member(parameter, path, guards) is not { } member)
            {
                Errors.Add($"{parameter.Type.Name} has no property \"{path}\".");
                return null;
            }

            Expression? comparison;
            try
            {
                comparison = op.InputCount <= 0
                    ? Check(op, member)
                    : Compare(rule, filter, op, member, path, guards);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                Errors.Add($"\"{op.Type}\" can't be used on \"{path}\" ({member.Type.Name}): {ex.Message}");
                return null;
            }

            if (comparison is null)
            {
                return null;
            }

            for (int i = guards.Count - 1; i >= 0; i--)
            {
                comparison = Expression.AndAlso(guards[i], comparison);
            }

            return comparison;
        }

        /// <summary>Operators without values look at the property itself.</summary>
        private Expression? Check(QueryOperator op, Expression member)
        {
            if (options.Operators?.GetValueOrDefault(op.Type) is { } custom)
            {
                return custom(member, []);
            }

            bool canBeNull = CanBeNull(member.Type);
            return op.Type switch
            {
                QueryOperators.IsNull => canBeNull ? Expression.Equal(member, Expression.Constant(null, member.Type)) : Expression.Constant(false),
                QueryOperators.IsNotNull => canBeNull ? Expression.NotEqual(member, Expression.Constant(null, member.Type)) : Expression.Constant(true),
                QueryOperators.IsEmpty => Expression.Equal(Text(member), Expression.Constant(string.Empty)),
                QueryOperators.IsNotEmpty => Expression.AndAlso(
                    Expression.NotEqual(Text(member), Expression.Constant(null, typeof(string))),
                    Expression.NotEqual(member, Expression.Constant(string.Empty))),
                _ => Unsupported(op),
            };
        }

        private Expression? Compare(QueryRule rule, QueryFilter filter, QueryOperator op, Expression member, string path, List<Expression> guards)
        {
            var operand = Operand(member, filter, guards, out var valueType);

            var typed = RuleValues.Typed(rule, filter, op);
            if (!op.Multiple && op.InputCount == 1 && typed.Count > 1)
            {
                typed = [typed[0]];
            }

            var values = new List<object?>(typed.Count);
            foreach (object? value in typed)
            {
                if (!TryConvert(value, valueType, out object? converted))
                {
                    Errors.Add($"The value \"{value}\" doesn't suit \"{path}\" ({valueType.Name}).");
                    return null;
                }

                if (valueType.IsEnum)
                {
                    converted = Convert.ChangeType(converted, Enum.GetUnderlyingType(valueType), CultureInfo.InvariantCulture);
                }

                values.Add(options.CaseInsensitive && converted is string text ? text.ToLowerInvariant() : converted);
            }

            if (operand.Type == typeof(string) && options.CaseInsensitive)
            {
                guards.Add(Expression.NotEqual(operand, Expression.Constant(null, typeof(string))));
                operand = Expression.Call(operand, ToLowerMethod);
            }

            if (options.Operators?.GetValueOrDefault(op.Type) is { } custom)
            {
                return custom(operand, [.. values.Select(v => Value(v, operand.Type))]);
            }

            if (op.Multiple)
            {
                var array = Array.CreateInstance(operand.Type, values.Count);
                for (int i = 0; i < values.Count; i++)
                {
                    array.SetValue(values[i], i);
                }

                var contains = Expression.Call(EnumerableContainsMethod.MakeGenericMethod(operand.Type), Value(array, array.GetType()), operand);
                return op.Type switch
                {
                    QueryOperators.In => contains,
                    QueryOperators.NotIn => NotNullAnd(operand, Expression.Not(contains)),
                    _ => Unsupported(op),
                };
            }

            var first = Value(values.Count > 0 ? values[0] : null, operand.Type);
            return op.Type switch
            {
                QueryOperators.Equal => Expression.Equal(operand, first),
                QueryOperators.NotEqual => Expression.NotEqual(operand, first),
                QueryOperators.Less => Order(ExpressionType.LessThan, operand, first),
                QueryOperators.LessOrEqual => Order(ExpressionType.LessThanOrEqual, operand, first),
                QueryOperators.Greater => Order(ExpressionType.GreaterThan, operand, first),
                QueryOperators.GreaterOrEqual => Order(ExpressionType.GreaterThanOrEqual, operand, first),
                QueryOperators.Between => Expression.AndAlso(
                    Order(ExpressionType.GreaterThanOrEqual, operand, first),
                    Order(ExpressionType.LessThanOrEqual, operand, Second(values, operand.Type))),
                QueryOperators.NotBetween => Expression.OrElse(
                    Order(ExpressionType.LessThan, operand, first),
                    Order(ExpressionType.GreaterThan, operand, Second(values, operand.Type))),
                QueryOperators.BeginsWith => NotNullAnd(operand, Expression.Call(Text(operand), StartsWithMethod, first)),
                QueryOperators.NotBeginsWith => NotNullAnd(operand, Expression.Not(Expression.Call(Text(operand), StartsWithMethod, first))),
                QueryOperators.Contains => NotNullAnd(operand, Expression.Call(Text(operand), ContainsMethod, first)),
                QueryOperators.NotContains => NotNullAnd(operand, Expression.Not(Expression.Call(Text(operand), ContainsMethod, first))),
                QueryOperators.EndsWith => NotNullAnd(operand, Expression.Call(Text(operand), EndsWithMethod, first)),
                QueryOperators.NotEndsWith => NotNullAnd(operand, Expression.Not(Expression.Call(Text(operand), EndsWithMethod, first))),
                _ => Unsupported(op),
            };
        }

        private Expression? Unsupported(QueryOperator op)
        {
            Errors.Add($"There is no LINQ translation for the operator \"{op.Type}\".");
            return null;
        }

        /// <summary>
        /// The expression values are compared with: the property, its date for date filters on
        /// date-time properties, or its number for enumerations, which have no comparison operators.
        /// </summary>
        private static Expression Operand(Expression member, QueryFilter filter, List<Expression> guards, out Type valueType)
        {
            var underlying = Nullable.GetUnderlyingType(member.Type);
            var core = underlying ?? member.Type;

            if (filter.Type == QueryValueType.Date && (core == typeof(DateTime) || core == typeof(DateTimeOffset)))
            {
                var value = member;
                if (underlying is not null)
                {
                    guards.Add(Expression.Property(member, nameof(Nullable<int>.HasValue)));
                    value = Expression.Property(member, nameof(Nullable<int>.Value));
                }

                valueType = typeof(DateTime);
                return Expression.Property(value, nameof(DateTime.Date));
            }

            valueType = core;
            if (core.IsEnum)
            {
                var number = Enum.GetUnderlyingType(core);
                return Expression.Convert(member, underlying is null ? number : typeof(Nullable<>).MakeGenericType(number));
            }

            return member;
        }

        /// <summary>Follows a dotted path, guarding each step that could be null.</summary>
        private Expression? Member(Expression target, string path, List<Expression> guards)
        {
            var current = target;
            string[] parts = path.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0 && options.NullSafe && CanBeNull(current.Type))
                {
                    guards.Add(Expression.NotEqual(current, Expression.Constant(null, current.Type)));
                }

                if (i > 0 && Nullable.GetUnderlyingType(current.Type) is not null)
                {
                    current = Expression.Property(current, nameof(Nullable<int>.Value));
                }

                if (FindMember(current.Type, parts[i]) is not { } member)
                {
                    return null;
                }

                current = Expression.MakeMemberAccess(current, member);
            }

            return parts.Length == 0 ? null : current;
        }

        private static MemberInfo? FindMember(Type type, string name)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;
            return (MemberInfo?)type.GetProperty(name, Flags)
                ?? (MemberInfo?)type.GetField(name, Flags)
                ?? type.GetProperties(Flags).FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private Expression NotNullAnd(Expression operand, Expression test) =>
            options.NullSafe && CanBeNull(operand.Type)
                ? Expression.AndAlso(Expression.NotEqual(operand, Expression.Constant(null, operand.Type)), test)
                : test;

        private static Expression Text(Expression operand) =>
            operand.Type == typeof(string)
                ? operand
                : throw new InvalidOperationException("The operator needs a text property.");

        private static Expression Order(ExpressionType comparison, Expression left, Expression right)
        {
            if (left.Type != typeof(string))
            {
                return Expression.MakeBinary(comparison, left, right);
            }

            return Expression.MakeBinary(comparison, Expression.Call(CompareMethod, left, right), Expression.Constant(0));
        }

        private static Expression Second(List<object?> values, Type type) => Value(values.Count > 1 ? values[1] : null, type);

        /// <summary>Reads the value through a closure so Entity Framework sends it as a parameter.</summary>
        private static Expression Value(object? value, Type type)
        {
            var holder = Activator.CreateInstance(typeof(ValueHolder<>).MakeGenericType(type), [value])!;
            return Expression.Property(Expression.Constant(holder), nameof(ValueHolder<object>.Value));
        }

        private static bool CanBeNull(Type type) => !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
    }

    /// <summary>Converts a parsed rule value to a property's type.</summary>
    internal static bool TryConvert(object? value, Type type, out object? result)
    {
        result = value;
        if (value is null || type.IsInstanceOfType(value))
        {
            return true;
        }

        try
        {
            switch (value)
            {
                case string text when type.IsEnum:
                    return Enum.TryParse(type, text, ignoreCase: true, out result);
                case string text when type == typeof(Guid):
                    bool isGuid = Guid.TryParse(text, out var guid);
                    result = guid;
                    return isGuid;
                case string text when type == typeof(char):
                    result = text.Length == 1 ? text[0] : null;
                    return text.Length == 1;
                case DateOnly date when type == typeof(DateTime):
                    result = date.ToDateTime(TimeOnly.MinValue);
                    return true;
                case DateOnly date when type == typeof(DateTimeOffset):
                    result = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                    return true;
                case DateTime dateTime when type == typeof(DateTimeOffset):
                    result = dateTime.Kind == DateTimeKind.Unspecified ? new DateTimeOffset(dateTime, TimeSpan.Zero) : new DateTimeOffset(dateTime);
                    return true;
                case DateTime dateTime when type == typeof(DateOnly):
                    result = DateOnly.FromDateTime(dateTime);
                    return true;
                case TimeOnly time when type == typeof(TimeSpan):
                    result = time.ToTimeSpan();
                    return true;
                case IConvertible when type.IsEnum:
                    result = Enum.ToObject(type, Convert.ToInt64(value, CultureInfo.InvariantCulture));
                    return true;
                case IFormattable formattable when type == typeof(string):
                    result = formattable.ToString(null, CultureInfo.InvariantCulture);
                    return true;
                case IConvertible:
                    result = Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
                    return true;
                default:
                    result = null;
                    return false;
            }
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            result = null;
            return false;
        }
    }
}

internal sealed class ValueHolder<T>(T value)
{
    public T Value { get; } = value;
}
