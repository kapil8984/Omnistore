using OrderInventory.Application.Services;
using Xunit;

namespace OrderInventory.UnitTests;

public class DiscountCalculatorTests
{
    private readonly DiscountCalculator _calculator = new();

    [Fact]
    public void Rule5_NoDiscount_WhenSubTotalBelow10000_AndNotFifthOrder()
    {
        // Arrange
        decimal subTotal = 5000.00m;
        int previousOrders = 1; // 2nd order

        // Act
        var result = _calculator.CalculateOrderTotals(subTotal, previousOrders);

        // Assert
        Assert.Equal(5000.00m, result.SubTotal);
        Assert.Equal(0.00m, result.Discount);
        Assert.Equal(5000.00m, result.Total);
    }

    [Fact]
    public void Rule5_FivePercentDiscount_WhenSubTotalAbove10000()
    {
        // Arrange
        decimal subTotal = 20000.00m;
        int previousOrders = 0; // 1st order

        // Act
        var result = _calculator.CalculateOrderTotals(subTotal, previousOrders);

        // Assert: 5% of 20,000 is 1,000
        Assert.Equal(20000.00m, result.SubTotal);
        Assert.Equal(1000.00m, result.Discount);
        Assert.Equal(19000.00m, result.Total);
    }

    [Fact]
    public void Rule5_TenPercentDiscount_OnEveryFifthOrder()
    {
        // Arrange: previous 4 orders means this is the 5th order
        decimal subTotal = 8000.00m;
        int previousOrders = 4;

        // Act
        var result = _calculator.CalculateOrderTotals(subTotal, previousOrders);

        // Assert: 10% of 8,000 is 800
        Assert.Equal(8000.00m, result.SubTotal);
        Assert.Equal(800.00m, result.Discount);
        Assert.Equal(7200.00m, result.Total);
    }

    [Fact]
    public void Rule5_CombinedDiscounts_WhenSubTotalAbove10000_AndFifthOrder()
    {
        // Arrange: 5th order with SubTotal = 20,000
        decimal subTotal = 20000.00m;
        int previousOrders = 4; // 5th order

        // Act
        var result = _calculator.CalculateOrderTotals(subTotal, previousOrders);

        // Assert: 5% (1,000) + 10% (2,000) = 3,000 discount. Total = 17,000
        Assert.Equal(20000.00m, result.SubTotal);
        Assert.Equal(3000.00m, result.Discount);
        Assert.Equal(17000.00m, result.Total);
    }

    [Fact]
    public void Rule5_TenthOrder_AlsoQualifiesForFifthOrderDiscount()
    {
        // Arrange: previous 9 orders means this is 10th order (multiple of 5)
        decimal subTotal = 6000.00m;
        int previousOrders = 9;

        // Act
        var result = _calculator.CalculateOrderTotals(subTotal, previousOrders);

        // Assert: 10% discount
        Assert.Equal(600.00m, result.Discount);
        Assert.Equal(5400.00m, result.Total);
    }
}
