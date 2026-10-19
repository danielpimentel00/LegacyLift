using ArchUnitNET.Domain;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static LegacyLift.ArchitectureTests.LegacyLiftArchitecture;

namespace LegacyLift.ArchitectureTests;

public class ConventionTests
{
    private static readonly IObjectProvider<ArchUnitNET.Domain.Class> Handlers =
        Classes().That()
            .ResideInNamespaceMatching(@"^LegacyLift\.Application(\..+)?$")
            .And().HaveNameEndingWith("Handler")
            .As("Handlers");

    // Guarda contra reglas "vacías": si alguien renombra o mueve los handlers, la regla de abajo
    // pasaría sin comprobar nada. Este test lo detecta.
    [Fact]
    public void Handlers_AreFound()
    {
        Assert.NotEmpty(Handlers.GetObjects(LegacyLiftArchitecture.Architecture));
    }

    [Fact]
    public void Handlers_AreInternalAndSealed() =>
        Classes().That().Are(Handlers)
            .Should().BeInternal()
            .AndShould().BeSealed()
            .Because("un handler es un detalle de implementación de Application: internal evita que " +
                     "otros ensamblados lo usen directamente y sealed, que se extienda por herencia")
            .Check(LegacyLiftArchitecture.Architecture);

    [Fact]
    public void DomainClasses_AreSealed() =>
        Classes().That().ResideInAssembly(typeof(LegacyLift.Domain.Product).Assembly)
            .Should().BeSealed()
            .Because("una subclase podría saltarse las invariantes de la entidad " +
                     "(las clases static también cuentan como sealed)")
            .Check(LegacyLiftArchitecture.Architecture);
}
