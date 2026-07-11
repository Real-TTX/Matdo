# ============================================================
#  Matdo – Dockerfile (Multi-Stage)
#  Basis: ASP.NET Core 10.0 · Port 6006
# ============================================================

# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# NuGet-Restore zuerst (bessere Layer-Caches)
COPY src/Matdo.Web/Matdo.Web.csproj src/Matdo.Web/
RUN dotnet restore src/Matdo.Web/Matdo.Web.csproj

# Restlichen Quellcode kopieren und veröffentlichen
COPY . .
RUN dotnet publish src/Matdo.Web/Matdo.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Zeitzone (für Fälligkeiten/Erinnerungen)
ENV TZ=Europe/Berlin
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# Version wird beim Build übergeben und zur Laufzeit angezeigt
ARG MATDO_VERSION=local-dev
ENV MATDO_VERSION=$MATDO_VERSION

# Anwendung lauscht auf Port 6006
ENV ASPNETCORE_URLS=http://+:6006
ENV Matdo__ConfigDir=/data/config
ENV Matdo__KeysDir=/data/keys

# Standardwerte für den einfachen (compose-freien) Betrieb – von der Dev-/Release-Compose
# per environment überschreibbar. So braucht die Public-Compose keine ENV-Konfiguration.
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__Postgres="Host=db;Port=5432;Database=matdo;Username=matdo;Password=matdo"
EXPOSE 6006

# Daten-Verzeichnis (Configs + Schlüssel) wird als Volume gemountet
RUN mkdir -p /data/config /data/keys
VOLUME ["/data"]

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "Matdo.Web.dll"]
