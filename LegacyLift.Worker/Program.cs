using LegacyLift.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddRabbitMQClient("RabbitMq");

builder.Services.AddHostedService<ProductCreatedConsumer>();

builder.Build().Run();
