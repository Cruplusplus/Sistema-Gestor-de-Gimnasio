# ========================================================
# Dockerfile Multi-stage para Gimnasio.Web (.NET 10)
# ========================================================

# Etapa 1: Compilación y publicación
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar csproj y restaurar paquetes NuGet
COPY ["src/Gimnasio.Web/Gimnasio.Web.csproj", "src/Gimnasio.Web/"]
RUN dotnet restore "src/Gimnasio.Web/Gimnasio.Web.csproj"

# Copiar todo el código fuente y publicar
COPY src/Gimnasio.Web/ src/Gimnasio.Web/
WORKDIR "/src/src/Gimnasio.Web"
RUN dotnet publish "Gimnasio.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Etapa 2: Imagen ligera de ejecución (Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5150
EXPOSE 5150

ENTRYPOINT ["dotnet", "Gimnasio.Web.dll"]
