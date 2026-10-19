using LegacyLift.Application.Products;
using Microsoft.Extensions.DependencyInjection;

namespace LegacyLift.Application;

public static class DependencyInjection
{
    // Los handlers son internal: solo este ensamblado puede registrarlos y usarlos.
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<GetProductHandler>();
        services.AddScoped<GetProductQuoteHandler>();
        services.AddScoped<CreateProductHandler>();
        return services;
    }
}
