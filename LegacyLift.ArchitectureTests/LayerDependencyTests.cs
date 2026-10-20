using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using static LegacyLift.ArchitectureTests.LegacyLiftArchitecture;

namespace LegacyLift.ArchitectureTests;

//   Api ──────────► Application ──► Domain
//   Infrastructure ─┘
// Las flechas solo apuntan hacia el dominio. Program.cs (composition root, namespace global)
// es el único que conoce todas las capas y queda fuera de estas reglas.
public class LayerDependencyTests
{
    [Fact]
    public void Domain_DoesNotDependOnOuterLayers() =>
        Types().That().Are(DomainLayer)
            .Should().NotDependOnAny(ApplicationLayer)
            .AndShould().NotDependOnAny(InfrastructureLayer)
            .AndShould().NotDependOnAny(ApiLayer)
            .Because("el dominio es el centro: las demás capas dependen de él, nunca al revés")
            .Check(Architecture);

    [Fact]
    public void Domain_IsPersistenceAndFrameworkIgnorant() =>
        Types().That().Are(DomainLayer)
            .Should().NotDependOnAny(EntityFrameworkCore)
            .AndShould().NotDependOnAny(AspNetCore)
            .AndShould().NotDependOnAny(RabbitMq)
            .AndShould().NotDependOnAny(Redis)
            .Because("las reglas de negocio no deben cambiar si cambia la base de datos, la mensajería " +
                     "o el framework web (p. ej. [Precision] de EF va en AppDbContext, no en Product)")
            .Check(Architecture);

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrApi() =>
        Types().That().Are(ApplicationLayer)
            .Should().NotDependOnAny(InfrastructureLayer)
            .AndShould().NotDependOnAny(ApiLayer)
            .Because("Application define puertos (IProductRepository, IProductEvents); " +
                     "Infrastructure los implementa y Api los invoca")
            .Check(Architecture);

    [Fact]
    public void Application_DoesNotDependOnConcreteFrameworks() =>
        Types().That().Are(ApplicationLayer)
            .Should().NotDependOnAny(EntityFrameworkCore)
            .AndShould().NotDependOnAny(AspNetCore)
            .AndShould().NotDependOnAny(RabbitMq)
            .AndShould().NotDependOnAny(Redis)
            .Because("los handlers trabajan contra abstracciones (IDistributedCache sí, RedisCache no)")
            .Check(Architecture);

    [Fact]
    public void Api_DoesNotTalkToInfrastructureDirectly() =>
        Types().That().Are(ApiLayer)
            .Should().NotDependOnAny(InfrastructureLayer)
            .AndShould().NotDependOnAny(EntityFrameworkCore)
            .AndShould().NotDependOnAny(RabbitMq)
            .AndShould().NotDependOnAny(Redis)
            .Because("los endpoints solo traducen HTTP <-> handlers; inyectar AppDbContext en un " +
                     "endpoint es exactamente el código legacy que se refactorizó")
            .Check(Architecture);

    [Fact]
    public void Infrastructure_DoesNotDependOnApi() =>
        Types().That().Are(InfrastructureLayer)
            .Should().NotDependOnAny(ApiLayer)
            .Because("los adaptadores de salida no deben conocer el adaptador de entrada HTTP")
            .Check(Architecture);
}
