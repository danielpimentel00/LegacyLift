namespace LegacyLift.Domain;

public sealed class Product
{
    public const int NameMaxLength = 100;
    public const decimal MinPrice = 0.01m;
    public const decimal MaxPrice = 100_000m;

    // Un cambio de precio no puede mover el precio mas de un 50% en una sola operacion.
    public const decimal MaxPriceChangeRatio = 0.5m;

    // Para EF Core: materializa la entidad desde la base sin pasar por las validaciones.
    private Product() { }

    public int Id { get; private set; }
    public string Name { get; private set; } = "";
    public decimal Price { get; private set; }

    public static Product Create(string name, decimal price)
    {
        var product = new Product();
        product.Rename(name);
        product.Price = EnsureValidPrice(price);
        return product;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del producto es obligatorio.");

        var trimmed = name.Trim();
        if (trimmed.Length > NameMaxLength)
            throw new DomainException($"El nombre no puede superar {NameMaxLength} caracteres.");

        Name = trimmed;
    }

    public void ChangePrice(decimal newPrice)
    {
        EnsureValidPrice(newPrice);

        var maxChange = Price * MaxPriceChangeRatio;
        if (Math.Abs(newPrice - Price) > maxChange)
            throw new DomainException(
                $"Un cambio de precio no puede superar el {MaxPriceChangeRatio * 100}% del precio actual.");

        Price = newPrice;
    }

    // Total a pagar por 'quantity' unidades, con el descuento por volumen ya aplicado.
    public decimal QuoteTotal(int quantity)
    {
        var rate = VolumeDiscount.RateFor(quantity);
        var gross = Price * quantity;
        var discount = Math.Round(gross * rate, 2, MidpointRounding.AwayFromZero);
        return gross - discount;
    }

    private static decimal EnsureValidPrice(decimal price)
    {
        if (price < MinPrice || price > MaxPrice)
            throw new DomainException($"El precio debe estar entre {MinPrice} y {MaxPrice}.");

        if (decimal.Round(price, 2) != price)
            throw new DomainException("El precio admite como maximo 2 decimales.");

        return price;
    }
}
