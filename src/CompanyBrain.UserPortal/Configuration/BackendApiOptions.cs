namespace CompanyBrain.UserPortal.Configuration;

public sealed class BackendApiOptions
{
    public const string SectionName = "BackendApi";

    public string BaseUrl { get; init; } = "http://localhost:8070";
}