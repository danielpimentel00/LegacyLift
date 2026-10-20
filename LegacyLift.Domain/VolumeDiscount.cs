namespace LegacyLift.Domain;

public static class VolumeDiscount
{
    public const int SmallBulkThreshold = 10;
    public const int LargeBulkThreshold = 100;
    public const decimal SmallBulkRate = 0.05m;
    public const decimal LargeBulkRate = 0.10m;

    public static decimal RateFor(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("La cantidad debe ser mayor que cero.");

        if (quantity >= LargeBulkThreshold) return LargeBulkRate;
        if (quantity >= SmallBulkThreshold) return SmallBulkRate;
        return 0m;
    }
}
