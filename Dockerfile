# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/StoreOps.Api/StoreOps.Api.csproj src/StoreOps.Api/
RUN dotnet restore src/StoreOps.Api/StoreOps.Api.csproj

COPY src/ src/
RUN dotnet publish src/StoreOps.Api/StoreOps.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "StoreOps.Api.dll"]
