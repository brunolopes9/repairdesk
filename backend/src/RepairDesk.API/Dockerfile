FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/ ./src/

RUN dotnet restore src/RepairDesk.API/RepairDesk.API.csproj
RUN dotnet publish src/RepairDesk.API/RepairDesk.API.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# QuestPDF/SkiaSharp precisam de fontconfig + fontes em Linux.
# Pré-criar /data/photos e /backups com ownership 'app' para que
# named volumes montados por cima herdem permissões correctas.
USER root
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl libfontconfig1 fonts-dejavu-core fonts-liberation \
 && rm -rf /var/lib/apt/lists/* \
 && mkdir -p /data/photos /data/dp-keys /backups \
 && chown -R app:app /data /backups

COPY --from=build /app ./
COPY src/RepairDesk.API/docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh
RUN chmod +x /usr/local/bin/docker-entrypoint.sh && sed -i 's/\r$//' /usr/local/bin/docker-entrypoint.sh
# USER root: entrypoint corre chmod e DROP privileges com runuser antes de dotnet arrancar.
USER root
EXPOSE 8080
ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]
