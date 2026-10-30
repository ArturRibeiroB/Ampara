# Imagem da API (INF-02, INF-15). Build: docker build -t funeraria-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ src/
# Versão do release (X.Y.Z), passada pelo CI na tag vX.Y.Z (INF-21). Vazia = versão de desenvolvimento.
ARG VERSAO=
RUN dotnet publish src/Funeraria.Api/Funeraria.Api.csproj -c Release -o /app/publish /p:UseAppHost=false ${VERSAO:+/p:Version=$VERSAO}

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Funeraria.Api.dll"]
