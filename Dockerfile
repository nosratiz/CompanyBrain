FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/CompanyBrain/CompanyBrain.csproj src/CompanyBrain/
RUN dotnet restore src/CompanyBrain/CompanyBrain.csproj

COPY src/CompanyBrain/ src/CompanyBrain/
RUN dotnet publish src/CompanyBrain/CompanyBrain.csproj \
    -c Release \
    -o /app/publish \
    -p:PublishAot=false \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "CompanyBrain.dll"]