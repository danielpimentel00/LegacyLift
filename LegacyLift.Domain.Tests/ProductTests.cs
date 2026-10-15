namespace LegacyLift.Domain.Tests;

public class ProductTests
{
    // ---- Create / Rename: nombre ---------------------------------------------------

    [Fact]
    public void Create_WithValidData_SetsNameAndPrice()
    {
        var product = Product.Create("Teclado", 49.99m);

        Assert.Equal("Teclado", product.Name);
        Assert.Equal(49.99m, product.Price);
    }

    [Fact]
    public void Create_TrimsName()
    {
        var product = Product.Create("  Teclado  ", 10m);

        Assert.Equal("Teclado", product.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithBlankName_IsRejected(string? name)
    {
        DomainAssert.Violates(() => Product.Create(name!, 10m));
    }

    [Fact]
    public void Create_NameAtMaxLength_IsAccepted()
    {
        var name = new string('a', Product.NameMaxLength);

        var product = Product.Create(name, 10m);

        Assert.Equal(name, product.Name);
    }

    [Fact]
    public void Create_NameOverMaxLength_IsRejected()
    {
        DomainAssert.Violates(() => Product.Create(new string('a', Product.NameMaxLength + 1), 10m));
    }

    [Fact]
    public void Create_MaxLengthIsCheckedAfterTrimming()
    {
        var name = " " + new string('a', Product.NameMaxLength) + " ";

        var product = Product.Create(name, 10m);

        Assert.Equal(Product.NameMaxLength, product.Name.Length);
    }

    [Fact]
    public void Rename_WithInvalidName_KeepsPreviousName()
    {
        var product = Product.Create("Teclado", 10m);

        DomainAssert.Violates(() => product.Rename(""));

        Assert.Equal("Teclado", product.Name);
    }

    // ---- Create: precio ------------------------------------------------------------

    [Theory]
    [InlineData("0.01")]      // MinPrice
    [InlineData("100000")]    // MaxPrice
    [InlineData("19.99")]
    public void Create_PriceWithinRange_IsAccepted(string price)
    {
        var product = Product.Create("Teclado", decimal.Parse(price));

        Assert.Equal(decimal.Parse(price), product.Price);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("100000.01")]
    public void Create_PriceOutOfRange_IsRejected(string price)
    {
        DomainAssert.Violates(() => Product.Create("Teclado", decimal.Parse(price)));
    }

    [Fact]
    public void Create_PriceWithMoreThanTwoDecimals_IsRejected()
    {
        DomainAssert.Violates(() => Product.Create("Teclado", 10.001m));
    }

    // ---- ChangePrice ---------------------------------------------------------------

    [Theory]
    [InlineData("150")]   // +50% exacto: el limite es inclusivo
    [InlineData("50")]    // -50% exacto
    [InlineData("120")]
    public void ChangePrice_WithinAllowedChange_UpdatesPrice(string newPrice)
    {
        var product = Product.Create("Teclado", 100m);

        product.ChangePrice(decimal.Parse(newPrice));

        Assert.Equal(decimal.Parse(newPrice), product.Price);
    }

    [Theory]
    [InlineData("150.01")]
    [InlineData("49.99")]
    public void ChangePrice_BeyondAllowedChange_IsRejectedAndKeepsPrice(string newPrice)
    {
        var product = Product.Create("Teclado", 100m);

        DomainAssert.Violates(() => product.ChangePrice(decimal.Parse(newPrice)));

        Assert.Equal(100m, product.Price);
    }

    [Fact]
    public void ChangePrice_ValidatesPriceRules_EvenWithinAllowedChange()
    {
        // 100 -> 100.001 esta dentro del 50%, pero tiene 3 decimales.
        var product = Product.Create("Teclado", 100m);

        DomainAssert.Violates(() => product.ChangePrice(100.001m));
    }

    [Fact]
    public void ChangePrice_AboveMaxPrice_IsRejected_EvenWithinAllowedChange()
    {
        var product = Product.Create("Teclado", Product.MaxPrice);

        DomainAssert.Violates(() => product.ChangePrice(Product.MaxPrice + 0.01m));
    }

    // ---- QuoteTotal ----------------------------------------------------------------

    [Theory]
    [InlineData(1, "10")]
    [InlineData(5, "50")]        // sin descuento: precio * cantidad
    [InlineData(10, "95")]       // 100 - 5%
    [InlineData(100, "900")]     // 1000 - 10%
    public void QuoteTotal_AppliesVolumeDiscount(int quantity, string expected)
    {
        var product = Product.Create("Teclado", 10m);

        Assert.Equal(decimal.Parse(expected), product.QuoteTotal(quantity));
    }

    [Fact]
    public void QuoteTotal_RoundsDiscountAwayFromZero()
    {
        // 0.25 * 10 = 2.50; 5% = 0.125 -> 0.13 (redondeo bancario daria 0.12)
        var product = Product.Create("Teclado", 0.25m);

        Assert.Equal(2.37m, product.QuoteTotal(10));
    }

    [Fact]
    public void QuoteTotal_WithNonPositiveQuantity_IsRejected()
    {
        var product = Product.Create("Teclado", 10m);

        DomainAssert.Violates(() => product.QuoteTotal(0));
    }
}
