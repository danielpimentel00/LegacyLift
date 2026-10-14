using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace LegacyLift.IntegrationTests;

// Medicion orientativa con Stopwatch, no un benchmark riguroso: corre dentro del test runner,
// contra Docker, con ruido de la maquina. Sirve para comparar ordenes de magnitud.
[Collection(ApiCollection.Name)]
[Trait("Category", "Measurement")]
public class ResetCostTests(ContainersWebApplicationFactory factory, ITestOutputHelper output)
{
    private const int WarmupIterations = 5;
    private const int Iterations = 50;

    [Fact]
    public async Task CompareResetStrategies()
    {
        // Cada ciclo = dejar la base limpia + el "trabajo" de un test (un INSERT).
        var manual = await MeasureAsync(async () =>
        {
            await factory.ResetDatabaseManuallyAsync();
            await InsertProductAsync();
        });

        var respawn = await MeasureAsync(async () =>
        {
            await factory.ResetDatabaseWithRespawnAsync();
            await InsertProductAsync();
        });

        await factory.ResetDatabaseWithRespawnAsync(); // estado inicial confirmado para el rollback
        var rollback = await MeasureAsync(async () =>
        {
            await factory.BeginTestTransactionAsync();
            try { await InsertProductAsync(); }
            finally { await factory.RollbackTestTransactionAsync(); }
        });

        // Coste que pagan ambas estrategias en cada test, aparte de la base de datos.
        var cacheAndQueue = await MeasureAsync(factory.ResetCacheAndQueueAsync, iterations: 10);

        output.WriteLine($"{"Estrategia",-28}{"Media (ms)",12}{"Mediana (ms)",14}");
        Print("Manual (DELETE + IDENTITY)", manual);
        Print("Respawn", respawn);
        Print("Transaccion + rollback", rollback);
        Print("Redis + RabbitMQ (exec)", cacheAndQueue);
    }

    private async Task InsertProductAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(new Product { Name = "Temporal", Price = 1m });
        await db.SaveChangesAsync();
    }

    private static async Task<(double Mean, double Median)> MeasureAsync(
        Func<Task> cycle, int iterations = Iterations)
    {
        for (var i = 0; i < WarmupIterations; i++)
            await cycle();

        var samples = new double[iterations];
        for (var i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await cycle();
            samples[i] = stopwatch.Elapsed.TotalMilliseconds;
        }

        Array.Sort(samples);
        return (samples.Average(), samples[iterations / 2]);
    }

    private void Print(string name, (double Mean, double Median) result) =>
        output.WriteLine($"{name,-28}{result.Mean,12:F2}{result.Median,14:F2}");
}
