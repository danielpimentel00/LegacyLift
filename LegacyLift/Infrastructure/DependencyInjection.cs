using LegacyLift.Application.Products;
using Microsoft.EntityFrameworkCore;

namespace LegacyLift.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Las lambdas leen la configuración al construir las opciones, no ahora: así los tests de
        // integración pueden sobrescribir las cadenas de conexión con UseSetting.
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));

        services.AddStackExchangeRedisCache(options =>
            options.Configuration = configuration.GetConnectionString("Redis"));

        services.AddScoped<IProductRepository, EfProductRepository>();
        services.AddSingleton<IProductEvents, RabbitMqProductEvents>();

        return services;
    }
}
