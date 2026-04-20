namespace CompanyBrain.Dashboard.Helpers;

public readonly record struct PiiDetectionResult
{
    public int EmailCount { get; init; }
    public int ApiKeyCount { get; init; }
    public int IpAddressCount { get; init; }
    public int GitHubTokenCount { get; init; }
    public int SlackTokenCount { get; init; }
    public int AwsKeyCount { get; init; }
    
    public int TotalCount => EmailCount + ApiKeyCount + IpAddressCount 
        + GitHubTokenCount + SlackTokenCount + AwsKeyCount;
    
    public bool HasPii => TotalCount > 0;
}
