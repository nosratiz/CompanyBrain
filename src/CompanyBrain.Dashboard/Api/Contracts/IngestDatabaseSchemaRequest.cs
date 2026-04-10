namespace CompanyBrain.Dashboard.Api.Contracts;

internal sealed record IngestDatabaseSchemaRequest(string ConnectionString, string Name, string Provider);
