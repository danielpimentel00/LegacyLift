using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Respawn;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using LegacyLift.Domain;
using LegacyLift.Infrastructure;

namespace LegacyLift.IntegrationTests;

public class ContainersWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7.0").Build();
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:3.11").Build();

    // Una sola conexion compartida por la app y los tests. La estrategia de rollback
    // la necesita: la transaccion del test vive en esta conexion y todos los DbContext
    // (los de las peticiones HTTP incluidos) tienen que usarla.
    private SqlConnection _connection = null!;
    private DbTransaction? _testTransaction;
    private Respawner _respawner = null!;

    public string RabbitMqConnectionString => _rabbit.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_sql.StartAsync(), _redis.StartAsync(), _rabbit.StartAsync());

        var sql = new SqlConnectionStringBuilder(_sql.GetConnectionString())
        {
            InitialCatalog = "LegacyLiftTests"
        };

        // La base se crea con un DbContext propio y la cadena de conexion completa.
        // A traves de la conexion compartida fallaria: en cuanto una SqlConnection se abre
        // (o lo intenta), su ConnectionString deja de incluir la contrasena, y EF la necesita
        // para conectarse a master y crear la base.
        var createOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(sql.ConnectionString)
            .Options;
        await using (var db = new AppDbContext(createOptions))
        {
            await db.Database.EnsureCreatedAsync();
        }

        // Abierta desde aqui hasta el final: EF no cierra una conexion que no abrio el.
        _connection = new SqlConnection(sql.ConnectionString);
        await _connection.OpenAsync();

        // Acceder a Services construye el host; los contenedores ya tienen puerto asignado.
        _ = Services;

        // Respawn analiza el esquema una sola vez y genera los DELETE en orden de claves foraneas.
        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            WithReseed = true
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
        builder.UseSetting("ConnectionStrings:RabbitMq", _rabbit.GetConnectionString());

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(_connection));

            // Cada DbContext se une a la transaccion del test, si hay una abierta.
            services.RemoveAll<AppDbContext>();
            services.AddScoped(sp =>
            {
                var db = new AppDbContext(sp.GetRequiredService<DbContextOptions<AppDbContext>>());
                if (_testTransaction is not null)
                    db.Database.UseTransaction(_testTransaction);
                return db;
            });
        });
    }

    // ---- Estrategia A: Respawn ----------------------------------------------------

    public async Task ResetDatabaseWithRespawnAsync()
    {
        // Borra todas las tablas y reinicia las identidades: el seed vuelve a recibir Ids 1 y 2.
        await _respawner.ResetAsync(_connection);
        await SeedAsync();
    }

    // ---- Estrategia B: transaccion con rollback -----------------------------------

    public async Task BeginTestTransactionAsync()
    {
        _testTransaction = await _connection.BeginTransactionAsync();
    }

    public async Task RollbackTestTransactionAsync()
    {
        if (_testTransaction is null) return;

        await _testTransaction.RollbackAsync();
        await _testTransaction.DisposeAsync();
        _testTransaction = null;
    }

    // ---- Referencia: el reset manual que usabamos hasta ahora ---------------------

    public async Task ResetDatabaseManuallyAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Products.ExecuteDeleteAsync();
        await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT Products ON");

        // Id tiene setter privado: el Id explicito se asigna a traves del change tracker.
        var teclado = Product.Create("Teclado", 49.99m);
        var monitor = Product.Create("Monitor", 199.00m);
        db.Products.AddRange(teclado, monitor);
        db.Entry(teclado).Property(p => p.Id).CurrentValue = 1;
        db.Entry(monitor).Property(p => p.Id).CurrentValue = 2;
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT Products OFF");
    }

    // ---- Comun ---------------------------------------------------------------------

    public async Task ResetCacheAndQueueAsync()
    {
        // Ni Respawn ni el rollback tocan Redis o RabbitMQ.
        await _redis.ExecAsync(["redis-cli", "FLUSHALL"]);
        await _rabbit.ExecAsync(["rabbitmqctl", "purge_queue", RabbitMqProductEvents.Queue]);
    }

    private async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Uno a uno, para que el orden de los Ids generados sea determinista.
        db.Products.Add(Product.Create("Teclado", 49.99m));
        await db.SaveChangesAsync();
        db.Products.Add(Product.Create("Monitor", 199.00m));
        await db.SaveChangesAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _connection.DisposeAsync();
        await _sql.DisposeAsync();
        await _redis.DisposeAsync();
        await _rabbit.DisposeAsync();
    }
}
