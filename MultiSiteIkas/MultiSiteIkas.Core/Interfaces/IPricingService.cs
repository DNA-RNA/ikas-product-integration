namespace MultiSiteIkas.Core.Interfaces;

public interface IPricingService
{
    decimal CalculateSitePrice(decimal basePrice, decimal marginPercentage, decimal additionalPrice);
}
