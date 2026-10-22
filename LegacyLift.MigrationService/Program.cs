using LegacyLift.Infrastructure;
using LegacyLift.MigrationService;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddSqlServerDbContext<AppDbContext>("Default");

builder.Services.AddHostedService<Migrator>();

builder.Build().Run();
