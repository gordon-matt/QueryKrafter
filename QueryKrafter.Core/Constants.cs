namespace QueryKrafter;

/// <summary>
/// Provides constants.
/// </summary>
public static class Constants
{
    /// <summary>For <see cref="QueryBuilderConfig.MaxGroupDepth"/>: groups may nest without limit.</summary>
    public const int UnlimitedDepth = -1;

    /// <summary>
    /// Provides conditions.
    /// </summary>
    public static class Conditions
    {
        /// <summary>
        /// The and constant.
        /// </summary>
        public const string And = "AND";
        /// <summary>
        /// The or constant.
        /// </summary>
        public const string Or = "OR";
    }

    /// <summary>
    /// Provides themes.
    /// </summary>
    public static class Themes
    {
        /// <summary>
        /// The light constant.
        /// </summary>
        public const string Light = "light";
        /// <summary>
        /// The dark constant.
        /// </summary>
        public const string Dark = "dark";
        /// <summary>
        /// The auto constant.
        /// </summary>
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
