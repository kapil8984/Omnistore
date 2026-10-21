# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy solution and project files
COPY OrderInventorySystem.slnx ./
COPY src/OrderInventory.Domain/OrderInventory.Domain.csproj src/OrderInventory.Domain/
COPY src/OrderInventory.Application/OrderInventory.Application.csproj src/OrderInventory.Application/
COPY src/OrderInventory.Infrastructure/OrderInventory.Infrastructure.csproj src/OrderInventory.Infrastructure/
COPY src/OrderInventory.Api/OrderInventory.Api.csproj src/OrderInventory.Api/
COPY tests/OrderInventory.UnitTests/OrderInventory.UnitTests.csproj tests/OrderInventory.UnitTests/
COPY tests/OrderInventory.IntegrationTests/OrderInventory.IntegrationTests.csproj tests/OrderInventory.IntegrationTests/

# Restore dependencies
RUN dotnet restore src/OrderInventory.Api/OrderInventory.Api.csproj

# Copy all source files
COPY src/ src/

# Publish API
RUN dotnet publish src/OrderInventory.Api/OrderInventory.Api.csproj -c Release -o /out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out ./

EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "OrderInventory.Api.dll"]
