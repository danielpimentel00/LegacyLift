using LegacyLift.Infrastructure;
using LegacyLift.MigrationService;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Solo el DbContext: este proceso no necesita Redis ni RabbitMQ.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddHostedService<Migrator>();

builder.Build().Run();
