using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Integracion con ASP.NET Core: los HTTP triggers reciben HttpRequest y devuelven IActionResult.
builder.ConfigureFunctionsWebApplication();

builder.Build().Run();
