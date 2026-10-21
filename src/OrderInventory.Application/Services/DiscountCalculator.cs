namespace OrderInventory.Application.Services;

public interface IDiscountCalculator
{
    (decimal SubTotal, decimal Discount, decimal Total) CalculateOrderTotals(decimal subTotal, int customerOrderCountSoFar);
}

public class DiscountCalculator : IDiscountCalculator
{
    public (decimal SubTotal, decimal Discount, decimal Total) CalculateOrderTotals(decimal subTotal, int customerOrderCountSoFar)
    {
        // customerOrderCountSoFar is count of previous orders. Current order number is count + 1.
        int currentOrderNumber = customerOrderCountSoFar + 1;
        bool isFifthOrder = (currentOrderNumber % 5 == 0);

        decimal highValueDiscount = subTotal > 10000m ? Math.Round(subTotal * 0.05m, 2, MidpointRounding.AwayFromZero) : 0m;
        decimal fifthOrderDiscount = isFifthOrder ? Math.Round(subTotal * 0.10m, 2, MidpointRounding.AwayFromZero) : 0m;

        decimal totalDiscount = Math.Round(highValueDiscount + fifthOrderDiscount, 2, MidpointRounding.AwayFromZero);
        decimal total = Math.Max(0m, Math.Round(subTotal - totalDiscount, 2, MidpointRounding.AwayFromZero));

        return (subTotal, totalDiscount, total);
    }
}
