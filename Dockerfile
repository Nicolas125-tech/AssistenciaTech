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
EXPOSE 8080

# Cria o diretório para mapear o banco de dados via Volume
RUN mkdir /app/data

# Copia os artefatos gerados no estágio de Build para o estágio final
COPY --from=build /app/publish .

# Define o comando de inicialização
ENTRYPOINT ["dotnet", "AssistenciaTech.dll"]
