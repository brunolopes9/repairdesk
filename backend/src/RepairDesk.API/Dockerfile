FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/ ./src/

RUN dotnet restore src/RepairDesk.API/RepairDesk.API.csproj
RUN dotnet publish src/RepairDesk.API/RepairDesk.API.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# QuestPDF/SkiaSharp precisam de fontconfig + fontes em Linux
USER root
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl libfontconfig1 fonts-dejavu-core fonts-liberation \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app ./
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "RepairDesk.API.dll"]
