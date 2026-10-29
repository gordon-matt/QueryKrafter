using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace QueryKrafter;

/// <summary>
/// Makes filters from a class's public properties, for a quick start. Labels, descriptions,
/// headings and order come from <see cref="DisplayAttribute"/> when present; properties with
/// <c>[Display(AutoGenerateFilter = false)]</c> are left out.
/// </summary>
public static class QueryFilters
{
    private const string ReflectionMessage = "Reads the type's properties and enumeration members by reflection.";

    [RequiresUnreferencedCode(ReflectionMessage)]
    public static IReadOnlyList<QueryFilter> FromType<T>() => FromType(typeof(T));

    [RequiresUnreferencedCode(ReflectionMessage)]
    public static IReadOnlyList<QueryFilter> FromType(Type type) =>
    [
        .. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .Select(p => (Property: p, Display: p.GetCustomAttribute<DisplayAttribute>()))
            .Where(p => p.Display?.GetAutoGenerateFilter() != false)
            .Select(p => (p.Property, p.Display, Filter: ToFilter(p.Property, p.Display)))
            .Where(p => p.Filter is not null)
            .OrderBy(p => p.Display?.GetOrder() ?? 10000)
            .Select(p => p.Filter!),
    ];

    [RequiresUnreferencedCode(ReflectionMessage)]
    private static QueryFilter? ToFilter(PropertyInfo property, DisplayAttribute? display)
    {
        var underlying = Nullable.GetUnderlyingType(property.PropertyType);
        var core = underlying ?? property.PropertyType;
        if (ValueTypeOf(core) is not { } valueType)
        {
            return null;
        }

        bool canBeNull = underlying is not null || !property.PropertyType.IsValueType;
        IReadOnlyList<QueryFilterOption> options = core.IsEnum ? EnumOptions(core) : [];

        IReadOnlyList<string>? operators = core.IsEnum
            ? QueryOperators.PickList
            : canBeNull
                ? null
                : [.. QueryOperators.Defaults
                    .Where(o => o.AppliesTo.HasFlag(valueType.Kind) && o.Type is not (QueryOperators.IsNull or QueryOperators.IsNotNull))
                    .Select(o => o.Type)];

        if (core.IsEnum && !canBeNull)
        {
            operators = [.. operators!.Where(o => o is not (QueryOperators.IsNull or QueryOperators.IsNotNull))];
        }

        return new QueryFilter
        {
            Id = property.Name,
            Label = display?.GetName() ?? property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? Humanize(property.Name),
            Description = display?.GetDescription(),
            Group = display?.GetGroupName(),
            Placeholder = display?.GetPrompt(),
            Type = valueType,
            Values = options,
            Operators = operators,
        };
    }

    private static QueryValueType? ValueTypeOf(Type type)
    {
        if (type.IsEnum)
        {
            return QueryValueType.Integer;
        }

        return Type.GetTypeCode(type) switch
        {
            TypeCode.String or TypeCode.Char => QueryValueType.String,
            TypeCode.Boolean => QueryValueType.Boolean,
            TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 => QueryValueType.Integer,
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal => QueryValueType.Double,
            TypeCode.DateTime => QueryValueType.DateTime,
            _ when type == typeof(DateTimeOffset) => QueryValueType.DateTime,
            _ when type == typeof(DateOnly) => QueryValueType.Date,
            _ when type == typeof(TimeOnly) => QueryValueType.Time,
            _ when type == typeof(Guid) => QueryValueType.String,
            _ => null,
        };
    }

    [RequiresUnreferencedCode(ReflectionMessage)]
    private static IReadOnlyList<QueryFilterOption> EnumOptions(Type type) =>
    [
        .. type.GetFields(BindingFlags.Public | BindingFlags.Static).Select(field => new QueryFilterOption(
            Convert.ToInt64(field.GetRawConstantValue(), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            field.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? Humanize(field.Name))),
    ];

    /// <summary>Splits PascalCase into words: <c>OrderDate</c> becomes "Order date".</summary>
    internal static string Humanize(string name)
    {
        var words = new System.Text.StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            bool startsWord = i > 0 && char.IsUpper(c) && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1])));
            if (startsWord)
            {
                words.Append(' ');
                words.Append(i + 1 < name.Length && char.IsUpper(name[i + 1]) ? c : char.ToLowerInvariant(c));
            }
            else
            {
                words.Append(c);
            }
        }

        return words.ToString();
    }
}
