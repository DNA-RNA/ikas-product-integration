using MultiSiteIkas.Core.Interfaces;

namespace MultiSiteIkas.Core.Services;

public sealed class PricingService : IPricingService
{
    public decimal CalculateSitePrice(decimal basePrice, decimal marginPercentage, decimal additionalPrice)
    {
        var withMargin = basePrice * (1 + marginPercentage / 100m);
        var final = withMargin + additionalPrice;
        return Math.Round(final, 2);
    }
}
