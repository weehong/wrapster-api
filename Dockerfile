# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy solution + central package management + build props first for layer caching
COPY Directory.Build.props Directory.Packages.props Wrapsfer.slnx ./
COPY src/Wrapsfer.Api/Wrapsfer.Api.csproj            src/Wrapsfer.Api/
COPY src/Wrapsfer.Application/Wrapsfer.Application.csproj src/Wrapsfer.Application/
COPY src/Wrapsfer.Domain/Wrapsfer.Domain.csproj      src/Wrapsfer.Domain/
COPY src/Wrapsfer.Infrastructure/Wrapsfer.Infrastructure.csproj src/Wrapsfer.Infrastructure/
COPY src/Wrapsfer.Mailing/Wrapsfer.Mailing.csproj    src/Wrapsfer.Mailing/
RUN dotnet restore src/Wrapsfer.Api/Wrapsfer.Api.csproj

# Copy the rest and publish
COPY src/ src/
RUN dotnet publish src/Wrapsfer.Api/Wrapsfer.Api.csproj \
    --configuration ${BUILD_CONFIGURATION} \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

USER app

COPY --from=build --chown=app:app /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_NOLOGO=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=true

EXPOSE 8080

ENTRYPOINT ["dotnet", "Wrapsfer.Api.dll"]
