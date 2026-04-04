using CompanyBrain.Admin.Server.Domain.Enums;

namespace CompanyBrain.Admin.Server.Domain;

public static class LicensePlanDefaults
{
    public static (int MaxApiKeys, int MaxDocuments, long MaxStorageBytes) GetLimits(LicenseTier tier) => tier switch
    {
        LicenseTier.Free => (3, 100, 1L * 1024 * 1024 * 1024),
        LicenseTier.Starter => (10, 500, 5L * 1024 * 1024 * 1024),
        LicenseTier.Professional => (25, 2_000, 25L * 1024 * 1024 * 1024),
        LicenseTier.Enterprise => (100, 50_000, 100L * 1024 * 1024 * 1024),
        _ => (3, 100, 1L * 1024 * 1024 * 1024)
    };

    public static string GetPlanName(LicenseTier tier) => tier switch
    {
        LicenseTier.Free => "Free",
        LicenseTier.Starter => "Starter",
        LicenseTier.Professional => "Professional",
        LicenseTier.Enterprise => "Enterprise",
        _ => "Unknown"
    };

    public static DateTime? GetExpiryDate(LicenseTier tier) => tier switch
    {
        LicenseTier.Free => null,
        _ => DateTime.UtcNow.AddYears(1)
    };
}