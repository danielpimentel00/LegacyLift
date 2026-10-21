using LegacyLift.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHostedService<ProductCreatedConsumer>();

builder.Build().Run();
