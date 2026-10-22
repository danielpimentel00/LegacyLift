var builder = DistributedApplication.CreateBuilder(args);

// ---- Infraestructura (contenedores) ------------------------------------------------

// Persistent: el contenedor sobrevive entre ejecuciones del AppHost (SQL Server tarda en arrancar).
// WithDataVolume: los datos sobreviven aunque el contenedor se recree.
var sql = builder.AddSqlServer("sql")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// AddDatabase crea la base "legacylift" cuando el servidor esta listo; las tablas no.
var db = sql.AddDatabase("legacylift");

var redis = builder.AddRedis("redis");

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

// ---- Servicios ------------------------------------------------------------------------

// Aplica el esquema (migraciones de EF Core) y termina.
var migrations = builder.AddProject<Projects.LegacyLift_MigrationService>("migrations")
    .WithReference(db, "Default")
    .WaitFor(db);

// El segundo argumento de WithReference es el nombre de la cadena de conexion que se inyecta
// (ConnectionStrings__Default, ...): coincide con lo que ya leen la API y el worker,
// asi su codigo no cambia.
builder.AddProject<Projects.LegacyLift>("api")
    .WithReference(db, "Default")
    .WithReference(redis, "Redis")
    .WithReference(rabbitmq, "RabbitMq")
    .WaitForCompletion(migrations) // implica que db ya estaba lista
    .WaitFor(redis)
    .WaitFor(rabbitmq);

builder.AddProject<Projects.LegacyLift_Worker>("worker")
    .WithReference(rabbitmq, "RabbitMq")
    .WaitFor(rabbitmq);

builder.Build().Run();
