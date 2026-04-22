# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
# RETTET HER: Vi åbner port 5151 i stedet for 8080
EXPOSE 5151
EXPOSE 5152


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["StockPriceSheetPrintService/StockPriceSheetPrintService.csproj", "StockPriceSheetPrintService/"]
RUN dotnet restore "./StockPriceSheetPrintService/StockPriceSheetPrintService.csproj"
COPY . .
WORKDIR "/src/StockPriceSheetPrintService"
RUN dotnet build "./StockPriceSheetPrintService.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./StockPriceSheetPrintService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "StockPriceSheetPrintService.dll"]