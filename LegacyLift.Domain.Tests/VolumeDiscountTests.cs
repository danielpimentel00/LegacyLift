namespace LegacyLift.Domain.Tests;

public class VolumeDiscountTests
{
    [Theory]
    [InlineData(1, "0")]
    [InlineData(9, "0")]        // justo por debajo del primer tramo
    [InlineData(10, "0.05")]    // limite inferior inclusivo
    [InlineData(99, "0.05")]
    [InlineData(100, "0.10")]   // limite inferior inclusivo
    [InlineData(5000, "0.10")]
    public void RateFor_ReturnsRateOfTheTier(int quantity, string expected)
    {
        Assert.Equal(decimal.Parse(expected), VolumeDiscount.RateFor(quantity));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RateFor_NonPositiveQuantity_IsRejected(int quantity)
    {
        DomainAssert.Violates(() => VolumeDiscount.RateFor(quantity));
    }
}
