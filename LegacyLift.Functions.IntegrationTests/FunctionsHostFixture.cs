using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Testcontainers.Azurite;
using Testcontainers.ServiceBus;

namespace LegacyLift.Functions.IntegrationTests;

// No existe un WebApplicationFactory para Functions: el host corre en otro proceso (func) y
// el worker de .NET en otro mas. Se lanza el host real de Core Tools sobre la salida de la
// compilacion, apuntando a Azurite y al emulador de Service Bus en contenedores.
public sealed class FunctionsHostFixture : IAsyncLifetime
{
    public const string ProductImportsQueue = "product-imports";
    public const string ProductImportsContainer = "product-imports";

    // El emulador solo conoce las colas declaradas en su configuracion.
    private const string ServiceBusConfig = """
        {
          "UserConfig": {
            "Namespaces": [
              {
                "Name": "sbemulatorns",
                "Queues": [
                  {
                    "Name": "product-imports",
                    "Properties": {
                      "DeadLetteringOnMessageExpiration": false,
                      "DefaultMessageTimeToLive": "PT1H",
                      "DuplicateDetectionHistoryTimeWindow": "PT20S",
                      "ForwardDeadLetteredMessagesTo": "",
                      "ForwardTo": "",
                      "LockDuration": "PT1M",
                      "MaxDeliveryCount": 10,
                      "RequiresDuplicateDetection": false,
                      "RequiresSession": false
                    }
                  }
                ],
                "Topics": []
              }
            ],
            "Logging": { "Type": "File" }
          }
        }
        """;

    // Cada version del SDK de Storage envia una version de API (x-ms-version) mas nueva y Azurite
    // rechaza las que no conoce. Se usa la imagen mas reciente y, por si el SDK va por delante,
    // se desactiva esa validacion.
    private readonly AzuriteContainer _azurite =
        new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:latest")
            .WithCommand("--skipApiVersionCheck")
            .Build();

    private readonly ServiceBusContainer _serviceBus =
        new ServiceBusBuilder("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
            .WithAcceptLicenseAgreement(true)
            .WithResourceMapping(Encoding.UTF8.GetBytes(ServiceBusConfig),
                "/ServiceBus_Emulator/ConfigFiles/Config.json")
            .Build();

    private readonly ConcurrentQueue<string> _logs = new();
    private Process? _host;

    public HttpClient Client { get; private set; } = null!;
    public BlobContainerClient ImportsContainer { get; private set; } = null!;
    public ServiceBusClient ServiceBus { get; private set; } = null!;

    public string Logs => string.Join(Environment.NewLine, _logs);

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_azurite.StartAsync(), _serviceBus.StartAsync());

        var storage = _azurite.GetConnectionString();
        var serviceBus = _serviceBus.GetConnectionString();

        ImportsContainer = new BlobContainerClient(storage, ProductImportsContainer);
        await ImportsContainer.CreateIfNotExistsAsync();
        ServiceBus = new ServiceBusClient(serviceBus);

        var port = GetFreePort();
        Client = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}") };
        _host = StartHost(port, storage, serviceBus);

        await WaitUntilRunningAsync();
    }

    public async Task WaitForLogAsync(string text, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_logs.Any(line => line.Contains(text, StringComparison.Ordinal)))
                return;

            await Task.Delay(250);
        }

        throw new TimeoutException($"No aparecio en los logs del host: \"{text}\"\n{Logs}");
    }

    // Lee (y completa, para dejar la DLQ limpia) los mensajes muertos hasta encontrar los esperados.
    public async Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveDeadLettersAsync(
        Func<ServiceBusReceivedMessage, bool> belongsToTest, int expected, TimeSpan timeout)
    {
        await using var receiver = ServiceBus.CreateReceiver(ProductImportsQueue,
            new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

        var found = new List<ServiceBusReceivedMessage>();
        var deadline = DateTime.UtcNow + timeout;

        while (found.Count < expected && DateTime.UtcNow < deadline)
        {
            var batch = await receiver.ReceiveMessagesAsync(maxMessages: 10, maxWaitTime: TimeSpan.FromSeconds(2));
            foreach (var message in batch)
            {
                await receiver.CompleteMessageAsync(message);
                if (belongsToTest(message))
                    found.Add(message);
            }
        }

        return found;
    }

    private Process StartHost(int port, string storage, string serviceBus)
    {
        var startInfo = new ProcessStartInfo("func", $"host start --port {port}")
        {
            WorkingDirectory = FunctionsOutputPath(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        // Las variables de entorno ganan sobre local.settings.json. Los nombres "blobs" y
        // "servicebus" son los valores de Connection en los triggers.
        startInfo.Environment["AzureWebJobsStorage"] = storage;
        startInfo.Environment["blobs"] = storage;
        startInfo.Environment["servicebus"] = serviceBus;
        startInfo.Environment["FUNCTIONS_WORKER_RUNTIME"] = "dotnet-isolated";

        var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) _logs.Enqueue(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) _logs.Enqueue(e.Data); };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                "No se encontro 'func'. Instala Azure Functions Core Tools v4 y verifica que este en el PATH.", ex);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private async Task WaitUntilRunningAsync()
    {
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (DateTime.UtcNow < deadline)
        {
            if (_host!.HasExited)
                throw new InvalidOperationException($"El host de Functions termino con codigo {_host.ExitCode}.\n{Logs}");

            try
            {
                var status = await Client.GetFromJsonAsync<HostStatus>("/admin/host/status");
                if (status?.State == "Running")
                    return;
            }
            catch (HttpRequestException)
            {
                // Todavia no escucha en el puerto.
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"El host de Functions no arranco a tiempo.\n{Logs}");
    }

    // bin/<Configuration>/<TFM> de LegacyLift.Functions, en paralelo a la salida de este proyecto.
    private static string FunctionsOutputPath()
    {
        var testOutput = new DirectoryInfo(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
        var targetFramework = testOutput.Name;
        var configuration = testOutput.Parent!.Name;
        var repositoryRoot = testOutput.Parent.Parent!.Parent!.Parent!.FullName;

        var path = Path.Combine(repositoryRoot, "LegacyLift.Functions", "bin", configuration, targetFramework);
        if (!File.Exists(Path.Combine(path, "host.json")))
            throw new DirectoryNotFoundException($"No se encontro la salida compilada de LegacyLift.Functions en {path}");

        return path;
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    public async Task DisposeAsync()
    {
        if (_host is { HasExited: false })
        {
            // El host lanza el worker de .NET como proceso hijo: hay que matar el arbol completo.
            _host.Kill(entireProcessTree: true);
            await _host.WaitForExitAsync();
        }

        _host?.Dispose();
        Client?.Dispose();
        if (ServiceBus is not null)
            await ServiceBus.DisposeAsync();

        await _serviceBus.DisposeAsync();
        await _azurite.DisposeAsync();
    }

    private sealed record HostStatus(string State);
}
