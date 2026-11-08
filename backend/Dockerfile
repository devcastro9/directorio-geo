FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    TZ=America/La_Paz
USER $APP_UID
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src
COPY backend.csproj ./
RUN dotnet restore backend.csproj

FROM restore AS build
ARG BUILD_CONFIGURATION=Release
COPY . .
RUN dotnet build backend.csproj -c $BUILD_CONFIGURATION --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish backend.csproj -c $BUILD_CONFIGURATION --no-build \
    -o /app/publish /p:UseAppHost=false

FROM base AS final
COPY --from=publish --chown=app:app /app/publish ./
ENTRYPOINT ["dotnet", "backend.dll"]
