using LegacyLift.Application.Products;
using Microsoft.EntityFrameworkCore;

namespace LegacyLift.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        // Registro propio + Enrich (y no AddSqlServerDbContext): los tests de integracion
        // reemplazan las opciones del DbContext y AddSqlServerDbContext usa pooling.
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
        builder.EnrichSqlServerDbContext<AppDbContext>();

        builder.AddRedisDistributedCache("Redis");
        builder.AddRabbitMQClient("RabbitMq");

        builder.Services.AddScoped<IProductRepository, EfProductRepository>();
        builder.Services.AddSingleton<IProductEvents, RabbitMqProductEvents>();

        return builder;
    }
}
