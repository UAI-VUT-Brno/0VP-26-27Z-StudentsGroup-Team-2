# ============================================================================
#  UAI.ZeroVP – Multi-stage Dockerfile
#
#  Stage 1 (build):   mcr.microsoft.com/dotnet/sdk:10.0
#                     obnovení balíčků, sestavení a publikace v konfiguraci Release
#  Stage 2 (runtime): mcr.microsoft.com/dotnet/aspnet:10.0
#                     běhové prostředí s HEALTHCHECK na /health a neprivilegovaným uživatelem
# ============================================================================

# ---------------------------------------------------------------------------
#  STAGE 1 - build
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Nejprve projektové soubory kvůli efektivnímu cachování vrstvy dotnet restore.
COPY UAI.ZeroVP.sln .
COPY src/UAI.ZeroVP.DTO/UAI.ZeroVP.DTO.csproj   src/UAI.ZeroVP.DTO/
COPY src/UAI.ZeroVP.Data/UAI.ZeroVP.Data.csproj src/UAI.ZeroVP.Data/
COPY src/UAI.ZeroVP.API/UAI.ZeroVP.API.csproj   src/UAI.ZeroVP.API/
COPY tests/UAI.ZeroVP.UnitTests/UAI.ZeroVP.UnitTests.csproj tests/UAI.ZeroVP.UnitTests/
RUN dotnet restore UAI.ZeroVP.sln

# Zbytek zdrojového kódu a publikace v konfiguraci Release.
COPY . .
RUN dotnet publish src/UAI.ZeroVP.API/UAI.ZeroVP.API.csproj \
        --configuration Release \
        --no-restore \
        --output /app

# ---------------------------------------------------------------------------
#  STAGE 2 - runtime
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

LABEL org.opencontainers.image.title="UAI.ZeroVP API" \
      org.opencontainers.image.description="Sample ToDo API for teaching Docker" \
      org.opencontainers.image.vendor="UAI" \
      org.opencontainers.image.licenses="MIT"

RUN apt-get update \
 && apt-get install --yes --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app .

# Příprava adresáře pro sdílené JSON úložiště (/app/data) a oprávnění pro $APP_UID.
RUN mkdir -p /app/data \
 && chown -R $APP_UID:$APP_UID /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=1

EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=3s --start-period=20s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/health || exit 1

USER $APP_UID

ENTRYPOINT ["dotnet", "UAI.ZeroVP.API.dll"]
