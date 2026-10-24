# Contexto de construcción: carpeta backend/
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ src/
RUN dotnet publish src/Workers/Selvana.AI.Worker/Selvana.AI.Worker.csproj -c Release -o /app

# El worker es un servicio web mínimo: recibe eventos por HTTP (ADR 0008)
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 8080
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "Selvana.AI.Worker.dll"]
