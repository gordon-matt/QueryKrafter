using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace QueryKrafter;

public static class ComponentsServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Optional: the components work without it, using the built-in languages. Call it to add
        /// languages or override strings from <c>{tag}.json</c> files in a folder of your own.
        /// </summary>
        public IServiceCollection AddQueryKrafter(Action<QueryKrafterLocalizationOptions>? configure = null)
        {
            var options = new QueryKrafterLocalizationOptions();
            configure?.Invoke(options);
            services.TryAddSingleton<IQueryKrafterLocalizer>(new QueryKrafterLocaleCatalog(options));
            return services;
        }
    }
}
