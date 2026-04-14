# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy solution + central package management + build props first for layer caching
COPY Directory.Build.props Directory.Packages.props Wrapster.slnx ./
COPY src/Wrapster.Api/Wrapster.Api.csproj            src/Wrapster.Api/
COPY src/Wrapster.Application/Wrapster.Application.csproj src/Wrapster.Application/
COPY src/Wrapster.Domain/Wrapster.Domain.csproj      src/Wrapster.Domain/
COPY src/Wrapster.Infrastructure/Wrapster.Infrastructure.csproj src/Wrapster.Infrastructure/
COPY src/Wrapster.Mailing/Wrapster.Mailing.csproj    src/Wrapster.Mailing/
RUN dotnet restore src/Wrapster.Api/Wrapster.Api.csproj

# Copy the rest and publish
COPY src/ src/
RUN dotnet publish src/Wrapster.Api/Wrapster.Api.csproj \
    --configuration ${BUILD_CONFIGURATION} \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Non-root user for security
RUN groupadd --system --gid 1000 app \
    && useradd --system --uid 1000 --gid app --shell /bin/false app
USER app

COPY --from=build --chown=app:app /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_NOLOGO=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=true

EXPOSE 8080

ENTRYPOINT ["dotnet", "Wrapster.Api.dll"]
