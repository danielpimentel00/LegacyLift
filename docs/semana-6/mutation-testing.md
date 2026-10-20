# Semana 6 — Mutation testing con Stryker.NET

**Objetivo:** ejecutar mutation testing sobre la capa de dominio, subir el mutation score a ≥70 %
y dejar documentadas las mutaciones que se decidió no matar.

| | Tests | Mutantes | Muertos | Supervivientes | Sin cobertura | Score |
|---|---:|---:|---:|---:|---:|---:|
| Línea base | 5 | 47 | 22 | 14 | 11 | **46.81 %** |
| Tests reforzados | 37 | 47 | 45 | 2 | 0 | **95.74 %** |
| Con supervivientes anotados | 37 | 45 (+2 ignorados) | 45 | 0 | 0 | **100 %** |

Herramienta: `dotnet-stryker` 5.0.0 (herramienta local, `dotnet-tools.json`). .NET SDK 10.0.301.

---

## 1. Punto de partida: no había capa de dominio

`Product` era un POCO con setters públicos y toda la lógica vivía en los endpoints de
`Program.cs`. Mutar eso no habría enseñado nada, así que la semana empezó extrayendo un dominio:

- **`LegacyLift.Domain`** (sin dependencias de EF ni ASP.NET):
  - `Product`: entidad encapsulada (setters privados, constructor privado para EF) con invariantes:
    - nombre obligatorio, recortado, máx. 100 caracteres;
    - precio entre 0.01 y 100 000, con máx. 2 decimales;
    - `ChangePrice`: un cambio no puede mover el precio más de ±50 % (límite inclusivo);
    - `QuoteTotal(quantity)`: total con descuento por volumen, redondeo `AwayFromZero` a 2 decimales.
  - `VolumeDiscount`: 0 % por debajo de 10 uds., 5 % desde 10, 10 % desde 100; cantidad > 0.
  - `DomainException` para violaciones de reglas.
- **API:** expone DTOs (`ProductResponse`, `QuoteResponse`), publica el evento `ProductCreated`
  y traduce `DomainException` a **400 ProblemDetails** (`DomainExceptionHandler`). Nuevo endpoint
  `GET /api/products/{id}/quote?quantity=N`. La configuración de EF (precisión, longitud) pasa a
  `AppDbContext.OnModelCreating`.
- Los tests de integración de la Semana 5 se adaptaron a los DTOs y a `Product.Create(...)`.

> Separar entidad y contratos públicos también prepara el terreno para el *contract testing* de
> esta semana: `ProductCreated` y `ProductResponse` son ahora el contrato, no la entidad.

## 2. Configuración

`LegacyLift.Domain.Tests/stryker-config.json`:

```json
{
  "stryker-config": {
    "project": "LegacyLift.Domain.csproj",
    "reporters": [ "html", "json", "progress", "cleartext" ],
    "thresholds": { "high": 85, "low": 70, "break": 70 }
  }
}
```

- `project` limita la mutación a la capa de dominio (el test project solo referencia ese proyecto).
- `break: 70` hace que `dotnet stryker` termine con código de salida ≠ 0 si el score baja del 70 %:
  sirve como *quality gate* en CI.

Cómo ejecutarlo:

```powershell
dotnet tool restore
cd LegacyLift.Domain.Tests
dotnet stryker
# informe: StrykerOutput/<fecha>/reports/mutation-report.html (ignorado por git)
```

## 3. Línea base: 5 tests verdes, 46.81 %

Los tests iniciales se escribieron a propósito en el estilo "típico" que pasa revisión sin problemas:

```csharp
Assert.NotNull(Product.Create("Teclado", 49.99m));                 // no comprueba Name ni Price
Assert.ThrowsAny<Exception>(() => Product.Create("", 10m));        // cualquier excepción vale
product.ChangePrice(120m); Assert.Equal(120m, product.Price);      // un único valor, lejos de los límites
Assert.True(product.QuoteTotal(5) > 0);                            // "positivo" no es "correcto"
Assert.Equal(0m, VolumeDiscount.RateFor(1));                       // solo el tramo sin descuento
```

Todos pasan y recorren la mayoría de los métodos, pero Stryker mostró lo que **no** detectan:

| Mutante superviviente | Por qué sobrevivió |
|---|---|
| `trimmed.Length > 100` → `>=` | ningún test con nombre de 100 caracteres exactos |
| `EnsureValidPrice(newPrice);` → eliminado | `ChangePrice` nunca se probó con un precio inválido |
| `Price * 0.5m` → `Price / 0.5m` | ningún test intentaba un cambio > 50 % |
| `Abs(...) > maxChange` → `>=` | ningún test en el límite exacto del 50 % |
| `Price * quantity` → `Price / quantity` | `> 0` también se cumple con la división |
| `gross - discount` → `gross + discount` | cantidad 5 → sin descuento, el signo no importa |
| `price < Min \|\| price > Max` → `&&`, `<=`, `>=` | ningún precio en los límites ni fuera de rango |
| `quantity <= 0` → `< 0` | no se probó la cantidad 0 |
| `quantity >= 100` → `>`, `quantity >= 10` → `>` | solo se probó la cantidad 1 |
| mensajes de excepción → `""` | `ThrowsAny<Exception>` no mira el mensaje |

Además, **11 mutantes sin cobertura**: los `throw` de nombre largo, precio fuera de rango,
decimales, cambio excesivo y cantidad ≤ 0 no se ejecutaban nunca.

**Lección:** cobertura de líneas alta ≠ tests que detectan fallos. El mutation score mide la
calidad de las *aserciones*, no cuántas líneas se ejecutan.

## 4. Qué se cambió en los tests

1. **Valores exactos en lugar de propiedades débiles:** `QuoteTotal(5) == 50`, `QuoteTotal(10) == 95`,
   `QuoteTotal(100) == 900`; `Create` comprueba `Name` y `Price`.
2. **Análisis de valores límite (BVA)** en cada comparación: nombre de 100/101 caracteres; precio
   0.01, 0, -5, 100 000, 100 000.01; cambio de precio a 150 y 50 (aceptados) frente a 150.01 y 49.99
   (rechazados); cantidades 0, 1, 9, 10, 99, 100.
3. **Cada regla aislada:** `ChangePrice(100.001m)` está dentro del ±50 % pero tiene 3 decimales.
   Solo la validación de precio puede rechazarlo, y así se mata la eliminación de `EnsureValidPrice`.
4. **Tipo de excepción concreto:** `DomainAssert.Violates` exige `DomainException` (no cualquier
   `Exception`) y un mensaje no vacío, porque la API lo devuelve en el ProblemDetails.
   **No** compara el texto exacto para no acoplar los tests a la redacción.
5. **Estado tras el fallo:** un `Rename` o un `ChangePrice` rechazados dejan el valor anterior.
6. **Redondeo documentado:** `0.25 × 10` con 5 % → descuento 0.125 → **0.13** (`AwayFromZero`; el
   redondeo bancario daría 0.12). Stryker no muta el enum `MidpointRounding`, pero el test fija la regla.

Resultado: **37 tests, 45/47 mutantes muertos, 95.74 %.**

## 5. Mutaciones que decidí no matar

### 5.1 `Product.Name` inicializado a `""` → `"Stryker was here!"` (String) — **equivalente**

```csharp
public string Name { get; private set; } = "";
```

El valor inicial nunca es observable desde fuera: el único camino público para crear un producto
es `Product.Create`, que siempre llama a `Rename` antes de devolverlo. EF Core usa el constructor
privado y sobrescribe `Name` con el valor de la base. Ningún test puede distinguir el original del
mutante, así que es un **mutante equivalente**. El inicializador existe solo para cumplir con
*nullable reference types*.

*Alternativa descartada:* `= null!;` haría desaparecer el mutante, pero sería cambiar código de
producción para "engañar" a la herramienta y además perdería el valor seguro por defecto.

### 5.2 `{MaxPriceChangeRatio * 100}` → `{MaxPriceChangeRatio / 100}` en el mensaje (Arithmetic) — **no compensa**

```csharp
throw new DomainException(
    $"Un cambio de precio no puede superar el {MaxPriceChangeRatio * 100}% del precio actual.");
```

La aritmética está **dentro del texto del mensaje**, no en la regla: el mutante cambia "50.0%" por
"0.005%" y la regla de negocio sigue igual. Para matarlo habría que comparar el texto del mensaje
(`Contains("50")`), y eso es justo el acoplamiento a la redacción que se evitó a propósito en
`DomainAssert` (punto 4.4). Coste mayor que beneficio.

*Si en el futuro* los mensajes pasan a ser parte del contrato (por ejemplo, códigos de error
estables para el frontend o localización), lo correcto sería modelar un código de error
(`PriceChangeTooLarge`) y testearlo, no comparar cadenas.

> Nota aparte: el mensaje muestra "50.0%" porque `0.5m * 100` conserva la escala del decimal.
> Es cosmético y queda fuera del alcance de esta tarea.

### Cómo quedaron anotadas

Ambas llevan un comentario `// Stryker disable once <Mutador> : <motivo>` en `Product.cs`. Stryker
las reporta como **Ignored** y ya no hacen ruido en cada ejecución, y el motivo queda junto al código
que afecta. Con las anotaciones el score es 100 % (45/45). La cifra honesta del ejercicio es el
**95.74 %** de la tabla, porque las anotaciones solo excluyen mutantes analizados a mano.

### Mutantes "Ignored: block already covered filter"

Stryker 5 descarta 6 mutaciones de *eliminación de bloque* (vaciar el cuerpo de un método) cuando las
mutaciones de las sentencias de ese bloque ya cubren el mismo fallo. No cuentan para el score y no
requieren acción.

## 6. Trampas y aprendizajes

- **`ThrowsAny<Exception>`** es casi siempre una aserción débil: también pasa con un
  `NullReferenceException` accidental. Usa el tipo concreto.
- **Un único "valor feliz"** (`120` en un rango 50–150) no detecta errores de `>` frente a `>=`.
  Cada comparación necesita sus dos lados del límite.
- **Aserciones de propiedad** (`> 0`, `NotNull`) dejan vivas las mutaciones aritméticas.
- **Validaciones en cadena:** si dos reglas rechazan la misma entrada, quitar una no se nota.
  Diseña entradas que solo viole una regla.
- **No persigas el 100 %:** los mutantes equivalentes existen. Documentarlos (y anotarlos con
  `Stryker disable` y su motivo) es mejor que deformar el código de producción.
- **Coste:** sobre el dominio, una ejecución tarda ~50 s. Para CI en proyectos grandes conviene
  `--since main` (solo los ficheros cambiados) o limitar `mutate` a las carpetas críticas.
