using Azure.Messaging.ServiceBus;

namespace LegacyLift.Functions.IntegrationTests;

// Aislamiento por datos unicos: cada test usa su propio nombre de archivo y de productos,
// asi no hace falta limpiar Azurite ni la cola entre tests.
[Collection(FunctionsCollection.Name)]
public class ProductImportTests(FunctionsHostFixture fixture)
{
    // El Blob trigger sondea el contenedor: con Azurite puede tardar varios segundos en detectarlo.
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task UploadedCsv_ImportsValidRows_AndDeadLettersBusinessRuleViolations()
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var blobName = $"import-{id}.csv";
        var csv = $"Teclado-{id},89.99\nMonitor-{id},0\nlinea rota\nMouse-{id},25.50\n";

        await fixture.ImportsContainer.UploadBlobAsync(blobName, BinaryData.FromString(csv));

        await fixture.WaitForLogAsync($"Linea 3 de {blobName} ignorada", Timeout);
        await fixture.WaitForLogAsync($"Producto importado: Teclado-{id}", Timeout);
        await fixture.WaitForLogAsync($"Producto importado: Mouse-{id}", Timeout);

        var deadLetters = await fixture.ReceiveDeadLettersAsync(
            m => m.Body.ToString().Contains(blobName, StringComparison.Ordinal), expected: 1, Timeout);

        var deadLetter = Assert.Single(deadLetters);
        Assert.Equal("ReglaDeNegocio", deadLetter.DeadLetterReason);
        Assert.Contains($"Monitor-{id}", deadLetter.Body.ToString());
    }

    [Fact]
    public async Task CorruptMessage_GoesToDeadLetterWithoutRetries()
    {
        var messageId = Guid.NewGuid().ToString("N");
        await using var sender = fixture.ServiceBus.CreateSender(FunctionsHostFixture.ProductImportsQueue);

        await sender.SendMessageAsync(new ServiceBusMessage("esto no es json") { MessageId = messageId });

        var deadLetters = await fixture.ReceiveDeadLettersAsync(m => m.MessageId == messageId, expected: 1, Timeout);

        // Si la funcion lanzara la excepcion, Service Bus lo reintentaria y el motivo seria
        // MaxDeliveryCountExceeded. "MensajeInvalido" prueba que lo liquido la funcion.
        var deadLetter = Assert.Single(deadLetters);
        Assert.Equal("MensajeInvalido", deadLetter.DeadLetterReason);
    }
}
