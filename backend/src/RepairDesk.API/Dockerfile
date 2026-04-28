FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/global.json ./
COPY backend/Directory.Build.props ./
COPY backend/RepairDesk.sln ./
COPY backend/src/ ./src/
COPY backend/tests/ ./tests/

RUN dotnet restore RepairDesk.sln
RUN dotnet publish src/RepairDesk.API/RepairDesk.API.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN groupadd --system --gid 1000 app && useradd --system --uid 1000 --gid app app
COPY --from=build /app ./
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "RepairDesk.API.dll"]
