# CompanyBrain API + MCP Server Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for restore
COPY src/CompanyBrain.Core/CompanyBrain.Core.csproj src/CompanyBrain.Core/
COPY src/CompanyBrain/CompanyBrain.csproj src/CompanyBrain/

# Restore dependencies
RUN dotnet restore src/CompanyBrain/CompanyBrain.csproj

# Copy source code
COPY src/CompanyBrain.Core/ src/CompanyBrain.Core/
COPY src/CompanyBrain/ src/CompanyBrain/

# Build and publish
RUN dotnet publish src/CompanyBrain/CompanyBrain.csproj \
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

ENTRYPOINT ["dotnet", "CompanyBrain.dll"]
