namespace LegacyLift.Domain.Tests;

public class ProductTests
{
    [Fact]
    public void Create_WithValidData_ReturnsProduct()
    {
        var product = Product.Create("Teclado", 49.99m);

        Assert.NotNull(product);
    }

    [Fact]
    public void Create_WithEmptyName_Throws()
    {
        Assert.ThrowsAny<Exception>(() => Product.Create("", 10m));
    }

    [Fact]
    public void ChangePrice_UpdatesPrice()
    {
        var product = Product.Create("Teclado", 100m);

        product.ChangePrice(120m);

        Assert.Equal(120m, product.Price);
    }

    [Fact]
    public void QuoteTotal_ReturnsPositiveAmount()
    {
        var product = Product.Create("Teclado", 10m);

        Assert.True(product.QuoteTotal(5) > 0);
    }
}
