# Contexto de construcción: carpeta backend/
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/ src/
RUN dotnet publish src/Host/Selvana.Api/Selvana.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Carpeta de las llaves de Data Protection (DataProtection:RutaLlaves); en producción se monta un volumen encima
RUN mkdir -p /llaves && chown $APP_UID /llaves
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Selvana.Api.dll"]
