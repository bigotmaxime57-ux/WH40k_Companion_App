# Image de la version web (serveur + client WebAssembly compilé AOT), déployée sur Fly.io (fly.toml).
# Construction en deux temps : compilation avec le SDK, puis exécution avec le seul runtime ASP.NET Core.

# --- Compilation -----------------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Compilation AOT du client : charge de travail wasm-tools, qui utilise Python sous Linux
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 \
    && rm -rf /var/lib/apt/lists/*
RUN dotnet workload install wasm-tools

WORKDIR /src

# Projets d'abord (restauration gardée en cache tant qu'ils ne changent pas), puis les sources
COPY Directory.Build.props ./
COPY src/Wh40kSim.Core/Wh40kSim.Core.csproj src/Wh40kSim.Core/
COPY src/Wh40kSim.Simulation/Wh40kSim.Simulation.csproj src/Wh40kSim.Simulation/
COPY src/Wh40kSim.UI/Wh40kSim.UI.csproj src/Wh40kSim.UI/
COPY src/Wh40kSim.Web.Client/Wh40kSim.Web.Client.csproj src/Wh40kSim.Web.Client/
COPY src/Wh40kSim.Web/Wh40kSim.Web.csproj src/Wh40kSim.Web/
RUN dotnet restore src/Wh40kSim.Web/Wh40kSim.Web.csproj -p:RunAOTCompilation=true

COPY src/ src/
RUN dotnet publish src/Wh40kSim.Web/Wh40kSim.Web.csproj -c Release -p:RunAOTCompilation=true -o /app

# --- Exécution -------------------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app
COPY --from=build /app .

# HTTP sur le port 8080 (HTTPS assuré par l'hébergeur). Le serveur tourne sous l'utilisateur sans privilèges de l'image
# .NET : le script de démarrage, lancé en root, lui confie d'abord le volume /data (journal des lancements), puis
# démarre le serveur sous cet utilisateur
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY docker-entrypoint.sh /app/docker-entrypoint.sh

ENTRYPOINT ["sh", "/app/docker-entrypoint.sh"]
