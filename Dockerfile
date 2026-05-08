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

# Install the EF Core tool and emit a self-contained migrations bundle.
# The bundle is invoked at deploy time by the `migrate` compose service to apply
# pending migrations against the target database before the API starts.
RUN dotnet tool install --global dotnet-ef --version 10.*
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet ef migrations bundle \
    --project src/Wrapsfer.Infrastructure \
    --startup-project src/Wrapsfer.Api \
    --configuration ${BUILD_CONFIGURATION} \
    --target-runtime linux-x64 \
    --output /app/efbundle \
    --force

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

USER app

COPY --from=build --chown=app:app /app/publish ./
COPY --from=build --chown=app:app --chmod=0755 /app/efbundle ./efbundle

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_NOLOGO=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=true

EXPOSE 8080

ENTRYPOINT ["dotnet", "Wrapsfer.Api.dll"]
