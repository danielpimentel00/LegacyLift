namespace LegacyLift.Functions.IntegrationTests;

// Un solo host y un solo juego de contenedores para todos los tests: arrancarlos cuesta
// decenas de segundos. Los tests de una coleccion se ejecutan en serie.
[CollectionDefinition(Name)]
public class FunctionsCollection : ICollectionFixture<FunctionsHostFixture>
{
    public const string Name = "Functions";
}
