# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "."]

COPY ["usage-demo/DemoWebApi/DemoWebApi.csproj", "usage-demo/DemoWebApi/"]

COPY ["extensions/Propel.FeatureFlags.PostgreSql/Propel.FeatureFlags.PostgreSql.csproj", "extensions/Propel.FeatureFlags.PostgreSql/"]

COPY ["infrastructure/Propel.FeatureFlags.Infrastructure.PostgresSql/Propel.FeatureFlags.Infrastructure.PostgresSql.csproj", "infrastructure/Propel.FeatureFlags.Infrastructure.PostgresSql/"]
COPY ["infrastructure/Propel.FeatureFlags.Infrastructure.Redis/Propel.FeatureFlags.Infrastructure.Redis.csproj", "infrastructure/Propel.FeatureFlags.Infrastructure.Redis/"]

COPY ["src/Propel.FeatureFlags/Propel.FeatureFlags.csproj", "src/Propel.FeatureFlags/"]
COPY ["src/Propel.FeatureFlags.AspNetCore/Propel.FeatureFlags.AspNetCore.csproj", "src/Propel.FeatureFlags.AspNetCore/"]
COPY ["src/Propel.FeatureFlags.Attributes/Propel.FeatureFlags.Attributes.csproj", "src/Propel.FeatureFlags.Attributes/"]

RUN dotnet restore "./usage-demo/DemoWebApi/DemoWebApi.csproj"
COPY . .
WORKDIR "/src/usage-demo/DemoWebApi"
RUN dotnet build "./DemoWebApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./DemoWebApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DemoWebApi.dll"]