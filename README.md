# 💻 TechOS - Sistema de Gestão e Bancada para Assistência Técnica

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![.NET 8](https://img.shields.io/badge/.NET_8-5C2D91?style=for-the-badge&logo=.net&logoColor=white)
![ASP.NET Core MVC](https://img.shields.io/badge/ASP.NET_MVC-512BD4?style=for-the-badge&logo=asp.net&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-316192?style=for-the-badge&logo=postgresql&logoColor=white)
![Bootstrap 5](https://img.shields.io/badge/Bootstrap_5-7952B3?style=for-the-badge&logo=bootstrap&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2CA5E0?style=for-the-badge&logo=docker&logoColor=white)
![Redis](https://img.shields.io/badge/redis-%23DD0031.svg?style=for-the-badge&logo=redis&logoColor=white)
![React Native](https://img.shields.io/badge/React_Native-20232A?style=for-the-badge&logo=react&logoColor=61DAFB)
![TypeScript](https://img.shields.io/badge/TypeScript-007ACC?style=for-the-badge&logo=typescript&logoColor=white)
![SQLite](https://img.shields.io/badge/SQLite-07405E?style=for-the-badge&logo=sqlite&logoColor=white)

O **TechOS** é um sistema (MVP) para gerenciar lojas de informática e assistência técnica. Ele tem uma área de consulta para clientes e um painel administrativo para os técnicos.

## Funcionalidades

### Área Pública (Cliente)
- **Landing Page**: Exibe os serviços.
- **Consulta de OS**: O cliente consulta o status do equipamento usando o Nº da Ordem de Serviço e o CPF.
- **Contato**: Links diretos para WhatsApp.

### Backoffice (Painel do Técnico)
- **Painel Dark Mode**: Interface escura para reduzir o cansaço visual.
- **Dashboard**: Mostra ordens de serviço (Abertas, Prontas) e previsão de faturamento.
- **Gestão de Peças**: Controla o estoque e avisa quando itens chegam ao limite mínimo.
- **Notificações no Telegram**: Envia mensagens automáticas com o status da OS para o cliente.
- **Controle de OS**: Gerencia as Ordens de Serviço usando o State Pattern.
- **Impressão de Recibos**: Gera PDFs tamanho A4 com os dados do serviço e diagnóstico (QuestPDF).
- **Tributação e NFS-e**: Isola regras de negócio com DDD.
  - Calcula automaticamente o ISS (5% sobre mão de obra) e ICMS (18% sobre peças) ao fechar a OS.
  - Gera o arquivo XML padrão ABRASF para emissão de Nota Fiscal de Serviço eletrônica (NFS-e).
- **Cache (Redis)**: Otimiza as consultas principais no banco de dados.
  - Os dados do dashboard ficam em cache por 5 minutos.
  - Alertas de estoque ficam armazenados por 1 hora e são atualizados automaticamente a cada movimentação.

### Aplicativo Mobile
- **App (React Native)**: Permite que os técnicos registrem serviços fora da loja, mesmo offline.
- **Banco de Dados Local**: Salva os dados no aparelho usando WatermelonDB (SQLite).
- **Sincronização**: Envia as informações para o servidor automaticamente quando a conexão é restabelecida.

## Tecnologias Utilizadas

- **Linguagem**: C# (.NET 8), TypeScript (Mobile)
- **Arquitetura**: Clean Architecture, State Pattern, Offline-First
- **Banco de Dados**: PostgreSQL, SQLite / WatermelonDB (Mobile)
- **Cache**: Redis
- **Frontend Web**: Razor Views, HTML5, CSS3, Bootstrap 5
- **Frontend Mobile**: React Native
- **PDF**: QuestPDF
- **Infraestrutura**: Docker, Cloudflare Tunnel

## Testar Online (Modo Demo)

Acesse a demonstração na nuvem:
[https://assistenciatech.onrender.com](https://assistenciatech.onrender.com)

**Credenciais (Administrador):**
- **Usuário:** `demo@assistenciatech.com`
- **Senha:** `Demo@1234`

*O sistema está em Modo Demonstração. O usuário demo pode navegar e visualizar os dados, mas o filtro `DemoModeFilter` impede alterações no banco.*

## Como Executar o Projeto

Você pode rodar o projeto localmente com o Docker, replicando o ambiente de produção.

### Via Docker Compose
1. Clone o repositório.
2. Na raiz do projeto, execute:
   ```bash
   docker-compose up -d --build
   ```
3. Acesse `http://localhost:8080`.

### Rodando Nativo
Para rodar sem Docker, você precisa do PostgreSQL instalado.
1. Suba o banco (opcional):
   ```bash
   docker-compose up -d database
   ```
2. Inicie a aplicação:
   ```bash
   dotnet restore
   dotnet run
   ```

## Desenvolvedor

Desenvolvido por **Nicolas**.
📱 **WhatsApp**: [(43) 98444-5767](https://wa.me/5543984445767)  
📸 **Instagram**: [@_nicolas_mb](https://instagram.com/_nicolas_mb)

## Acesso Administrativo Local

Para gerar as credenciais locais iniciais, rode:
```bash
./setup_local_admin.sh
```
Isso criará o usuário `admin` com a senha que você definir.
