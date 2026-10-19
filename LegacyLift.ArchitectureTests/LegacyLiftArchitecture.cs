using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using RabbitMQ.Client;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace LegacyLift.ArchitectureTests;

// Arquitectura analizada y capas con nombre, compartidas por todos los tests.
// Cargar los ensamblados es lo caro (se leen con Mono.Cecil): se hace una sola vez.
internal static class LegacyLiftArchitecture
{
    public static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(LegacyLift.Domain.Product).Assembly, // LegacyLift.Domain
            typeof(Program).Assembly,                   // LegacyLift: Api, Application, Infrastructure
            // Frameworks: se cargan para poder prohibir dependencias hacia sus tipos.
            typeof(DbContext).Assembly,
            typeof(HttpContext).Assembly,
            typeof(Results).Assembly,
            typeof(ConnectionFactory).Assembly,
            typeof(RedisCache).Assembly)
        .Build();

    // ---- Capas de LegacyLift ----------------------------------------------------------
    // ResideInNamespace compara el nombre exacto; con regex se incluyen los sub-namespaces.

    public static readonly IObjectProvider<IType> DomainLayer =
        Types().That().ResideInAssembly(typeof(LegacyLift.Domain.Product).Assembly).As("Domain");

    public static readonly IObjectProvider<IType> ApplicationLayer =
        Types().That().ResideInNamespaceMatching(@"^LegacyLift\.Application(\..+)?$").As("Application");

    public static readonly IObjectProvider<IType> InfrastructureLayer =
        Types().That().ResideInNamespaceMatching(@"^LegacyLift\.Infrastructure(\..+)?$").As("Infrastructure");

    public static readonly IObjectProvider<IType> ApiLayer =
        Types().That().ResideInNamespaceMatching(@"^LegacyLift\.Api(\..+)?$").As("Api");

    // ---- Frameworks --------------------------------------------------------------------

    public static readonly IObjectProvider<IType> EntityFrameworkCore =
        Types().That().ResideInAssembly(typeof(DbContext).Assembly).As("Entity Framework Core");

    public static readonly IObjectProvider<IType> AspNetCore =
        Types().That().ResideInAssembly(typeof(HttpContext).Assembly, typeof(Results).Assembly).As("ASP.NET Core");

    public static readonly IObjectProvider<IType> RabbitMq =
        Types().That().ResideInAssembly(typeof(ConnectionFactory).Assembly).As("RabbitMQ.Client");

    public static readonly IObjectProvider<IType> Redis =
        Types().That().ResideInAssembly(typeof(RedisCache).Assembly).As("Redis (StackExchangeRedis)");
}
