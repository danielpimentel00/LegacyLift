using System.Diagnostics;
using LegacyLift.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace LegacyLift.MigrationService;

// Aplica las migraciones pendientes y termina el proceso. El AppHost arranca la API con
// WaitForCompletion: solo cuando este proceso sale con codigo 0.
public sealed class Migrator(
    IServiceProvider services,
    IHostApplicationLifetime lifetime,
    IHostEnvironment environment,
    ILogger<Migrator> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // ServiceDefaults registra como fuente de trazas el nombre de la aplicacion: este span
        // aparece en el dashboard junto con los comandos SQL que ejecuta la migracion.
        using var activitySource = new ActivitySource(environment.ApplicationName);
        using var activity = activitySource.StartActivity("Migrating database");

        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var pending = (await db.Database.GetPendingMigrationsAsync(stoppingToken)).ToList();
            logger.LogInformation("Migraciones pendientes: {Count} ({Migrations})",
                pending.Count, string.Join(", ", pending));

            await db.Database.MigrateAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            logger.LogError(ex, "La migracion de la base de datos fallo");

            // Codigo de salida distinto de 0: el AppHost marca el recurso como fallido
            // y no arranca la API.
            Environment.ExitCode = 1;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
