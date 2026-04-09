# Dockerfile - builds CompanyBrain.Dashboard (API + MCP + Blazor UI)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for restore
COPY src/CompanyBrain.Core/CompanyBrain.Core.csproj src/CompanyBrain.Core/
COPY src/CompanyBrain.Dashboard/CompanyBrain.Dashboard.csproj src/CompanyBrain.Dashboard/

# Restore dependencies
RUN dotnet restore src/CompanyBrain.Dashboard/CompanyBrain.Dashboard.csproj

# Copy source code
COPY src/CompanyBrain.Core/ src/CompanyBrain.Core/
COPY src/CompanyBrain.Dashboard/ src/CompanyBrain.Dashboard/

# Build and publish
RUN dotnet publish src/CompanyBrain.Dashboard/CompanyBrain.Dashboard.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

# Create data directory
RUN mkdir -p /app/data

EXPOSE 8080

ENTRYPOINT ["dotnet", "CompanyBrain.Dashboard.dll"]