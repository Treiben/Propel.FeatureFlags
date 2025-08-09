# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ui/FeatureRabbit.Management.Api/FeatureRabbit.Management.Api.csproj", "ui/FeatureRabbit.Management.Api/"]
COPY ["src/FeatureRabbit.Flags.Cache.Redis/FeatureRabbit.Flags.Cache.Redis.csproj", "src/FeatureRabbit.Flags.Cache.Redis/"]
COPY ["src/FeatureRabbit.Flags/FeatureRabbit.Flags.csproj", "src/FeatureRabbit.Flags/"]
COPY ["src/FeatureRabbit.Flags.Persistence.PostgresSQL/FeatureRabbit.Flags.Persistence.PostgresSQL.csproj", "src/FeatureRabbit.Flags.Persistence.PostgresSQL/"]

RUN dotnet restore "./ui/FeatureRabbit.Management.Api/FeatureRabbit.Management.Api.csproj"

COPY . .
WORKDIR "/src/ui/FeatureRabbit.Management.Api"
RUN dotnet build "./FeatureRabbit.Management.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./FeatureRabbit.Management.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FeatureRabbit.Management.Api.dll"]