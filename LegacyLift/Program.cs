using LegacyLift.Api;
using LegacyLift.Application;
using LegacyLift.Infrastructure;
using Microsoft.FeatureManagement;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var useAppConfig = builder.Configuration.GetConnectionString("config") is not null;
if (useAppConfig)
{
    builder.AddAzureAppConfiguration("config", configureOptions: options =>
        options.ConfigureRefresh(refresh => refresh
            .Register("Sentinel", refreshAll: true)
            .SetRefreshInterval(TimeSpan.FromSeconds(30)))
        .UseFeatureFlags(flags => flags.SetRefreshInterval(TimeSpan.FromSeconds(30))));
    builder.Services.AddAzureAppConfiguration();
}

if (builder.Configuration.GetConnectionString("secrets") is not null)
{
    builder.Configuration.AddAzureKeyVaultSecrets("secrets");
}

builder.Services.AddFeatureManagement();
builder.Services.AddApplication();
builder.AddInfrastructure();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

var app = builder.Build();

if (useAppConfig)
{
    app.UseAzureAppConfiguration();
}

app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.MapProductEndpoints();

app.Run();

public partial class Program { }
