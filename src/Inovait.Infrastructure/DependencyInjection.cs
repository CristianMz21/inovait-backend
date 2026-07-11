using Inovait.Core.Domain.Common;
using Inovait.Infrastructure.Persistence;
using Inovait.Infrastructure.Persistence.Interceptors;
using Inovait.Infrastructure.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Inovait.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInovaitInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ITextNormalizer, TextNormalizer>();
        services.AddSingleton<TextNormalizationInterceptor>();
        services.AddSingleton<AuditSaveChangesInterceptor>();
        services.AddScoped<AcademicConfigurationStartupCheck>();
        services.AddDbContext<InovaitDbContext>((provider, options) =>
            options.UseSqlServer(connectionString)
                .AddInterceptors(
                    provider.GetRequiredService<TextNormalizationInterceptor>(),
                    provider.GetRequiredService<AuditSaveChangesInterceptor>()));

        return services;
    }
}
