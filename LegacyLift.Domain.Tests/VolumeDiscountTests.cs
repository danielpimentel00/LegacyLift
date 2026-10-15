namespace LegacyLift.Domain.Tests;

public class VolumeDiscountTests
{
    [Fact]
    public void RateFor_SmallOrder_HasNoDiscount()
    {
        Assert.Equal(0m, VolumeDiscount.RateFor(1));
    }
}
