namespace LegacyLift.Application.Products;

public sealed class ProductCacheOptions
{
    public const string Section = "Products:Cache";
    public int TtlSeconds { get; set; } = 60;
}