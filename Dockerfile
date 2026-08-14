# Estágio 1: Build (Usa o SDK completo para restaurar pacotes e compilar)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia o arquivo de projeto e restaura dependências (Camada de cache do Docker)
COPY ["AssistenciaTech.csproj", "./"]
RUN dotnet restore "AssistenciaTech.csproj"

# Copia o restante do código fonte e faz o build em modo Release
COPY . .
RUN rm -rf tests
RUN dotnet publish "AssistenciaTech.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Estágio 2: Runtime (Usa a imagem enxuta apenas para rodar a aplicação)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Configura a porta padrão da aplicação para 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_hostBuilder__reloadConfigOnChange=false
EXPOSE 8080

# Cria o diretório para mapear o banco de dados via Volume e ajusta as permissões para o usuário app (não-root)
RUN mkdir -p /app/data && chown -R app:app /app/data

# Copia os artefatos gerados no estágio de Build para o estágio final definindo a propriedade do usuário app
COPY --from=build --chown=app:app /app/publish .

# Define o usuário não-root para a execução da aplicação
USER app

# Define o comando de inicialização
ENTRYPOINT ["dotnet", "AssistenciaTech.dll"]
