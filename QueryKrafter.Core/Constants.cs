namespace QueryKrafter;

public static class Constants
{
    /// <summary>For <see cref="QueryBuilderConfig.MaxGroupDepth"/>: groups may nest without limit.</summary>
    public const int UnlimitedDepth = -1;

    public static class Conditions
    {
        public const string And = "AND";
        public const string Or = "OR";
    }

    public static class Themes
    {
        public const string Light = "light";
        public const string Dark = "dark";
        public const string Auto = "auto";

        /// <summary>Takes its colours from the host page's Bootstrap 5.3 variables.</summary>
        public const string Bootstrap = "bootstrap";
    }

    /// <summary>DOM events raised on the builder's root element, for hosts that aren't Blazor.</summary>
    public static class DomEvents
    {
        /// <summary>
        /// <c>detail</c> is <c>{ rules, isValid, errors: [{ path, code, message }], sql }</c>.
        /// <c>sql</c> is <c>{ text, parameters }</c> when a SQL dialect is set, otherwise <c>null</c>.
        /// </summary>
        public const string Changed = "qk-changed";
    }
}
