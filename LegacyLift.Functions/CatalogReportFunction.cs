using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace LegacyLift.Functions;

public sealed class CatalogReportFunction(ILogger<CatalogReportFunction> logger)
{
    // NCRONTAB de 6 campos (incluye segundos): todos los dias a las 02:00 UTC.
    [Function(nameof(NightlyCatalogReport))]
    public void NightlyCatalogReport([TimerTrigger("0 0 2 * * *")] TimerInfo timer)
    {
        logger.LogInformation("Reporte de catalogo ejecutado. Atrasado: {IsPastDue}. Siguiente: {Next}",
            timer.IsPastDue, timer.ScheduleStatus?.Next);
    }
}
