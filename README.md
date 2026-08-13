# 💻 TechOS - Sistema de Gestão e Bancada para Assistência Técnica

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![.NET 8](https://img.shields.io/badge/.NET_8-5C2D91?style=for-the-badge&logo=.net&logoColor=white)
![ASP.NET Core MVC](https://img.shields.io/badge/ASP.NET_MVC-512BD4?style=for-the-badge&logo=asp.net&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-316192?style=for-the-badge&logo=postgresql&logoColor=white)
![Bootstrap 5](https://img.shields.io/badge/Bootstrap_5-7952B3?style=for-the-badge&logo=bootstrap&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2CA5E0?style=for-the-badge&logo=docker&logoColor=white)

O **TechOS** é um sistema completo (MVP) desenvolvido para automatizar e profissionalizar a gestão de lojas de informática e a operação de bancada de assistência técnica de computadores, impressoras e notebooks. Ele une uma área de consulta self-service para clientes a um painel administrativo seguro em Dark Mode para os técnicos.

---

## ✨ Funcionalidades

### 🌐 Área Pública (Cliente)
- **Landing Page Moderna**: Design responsivo com identidade visual escura e moderna para exibição de serviços.
- **Consulta de OS**: Uma ferramenta self-service onde o cliente consulta o andamento do seu equipamento utilizando o **Nº da Ordem de Serviço + CPF** (com validação robusta contra formatações no banco).
- **Integração de Contato**: Botões de ação direta para contato ágil via WhatsApp.

### 🔒 Backoffice (Painel do Técnico)
- **Design Premium Dark Mode**: Interface otimizada em Grafite Azulado com alta legibilidade e suavidade visual para longas jornadas de trabalho na bancada.
- **Dashboard de Bancada**: Cards de resumo com barras indicadoras em Neon contendo o status das ordens de serviço (Abertas, Prontas) e Faturamento Previsto.
- **CRUD e Controle de Status**: Controle rígido do ciclo de vida das Ordens de Serviço (OS) com as regras de negócio integradas e protegidas via State Pattern.
- **Impressão de Recibos em PDF**: Geração instantânea de comprovantes em formato A4 contendo dados do serviço, diagnóstico e assinatura com a biblioteca **QuestPDF**.

---

## 🛠️ Tecnologias Utilizadas

- **Linguagem**: C# (.NET 8)
- **Arquitetura**: Evoluindo para Clean Architecture com State Pattern no domínio.
- **Banco de Dados**: PostgreSQL (com Entity Framework Core & Npgsql)
- **Frontend**: Razor Views, HTML5, CSS3 Customizado, Bootstrap 5 e Bootstrap Icons
- **Geração de PDF**: QuestPDF (Licença Community)
- **Infraestrutura e Deploy**: Docker (Multi-stage build), Docker Compose com persistência de volumes, Cloudflare Tunnel (Quick Tunnels para HTTPS público)

---

## 🚀 Como Executar o Projeto

O projeto possui suporte completo a contêineres Docker, tornando sua execução local trivial e idêntica ao ambiente de produção.

### 🐳 Via Docker Compose (Recomendado)
Certifique-se de ter o **Docker Desktop** instalado. O banco de dados PostgreSQL e a aplicação serão iniciados de forma integrada. O banco será inicializado e tabelado automaticamente (`EnsureCreated`) no primeiro boot, e os dados persistirão em volume seguro.

1. Clone o repositório.
2. Abra o terminal na raiz do projeto.
3. Suba o ambiente completo:
   ```bash
   docker-compose up -d --build
   ```
4. Acesse o sistema localmente em: `http://localhost:8080`.
*(O Cloudflare Tunnel irá gerar uma URL pública temporária com HTTPS seguro visível nos logs do container `tunnel`)*.

### 💻 Rodando Nativo (Para Desenvolvimento)
Para rodar a aplicação localmente fora do Docker, você precisará de uma instância do PostgreSQL rodando no seu host (ou rodar apenas o container do banco).

1. Suba apenas o banco de dados via Docker (opcional se você já tiver Postgres local):
   ```bash
   docker-compose up -d database
   ```
2. Certifique-se de que o SDK do .NET 8 esteja instalado.
3. Restaure as dependências e inicie a aplicação:
   ```bash
   dotnet restore
   dotnet run
   ```
4. O terminal exibirá a URL local (geralmente `http://localhost:5000` ou similar).

---

## 👨‍💻 Desenvolvedor

Este projeto foi desenhado e arquitetado por **Nicolas**.

📱 **WhatsApp**: [(43) 98444-5767](https://wa.me/5543984445767)  
📸 **Instagram**: [@_nicolas_mb](https://instagram.com/_nicolas_mb)

*Sinta-se à vontade para entrar em contato para feedbacks ou oportunidades!*

## Acesso Administrativo Local

Para desenvolvimento local, a aplicação requer a configuração de credenciais no arquivo `appsettings.Development.json`.
Você pode gerar este arquivo automaticamente utilizando o script incluso:

```bash
./setup_local_admin.sh
```

Isto configurará o ambiente com as seguintes credenciais padrão:
- **Usuário:** `admin`
- **Senha:** Senha informada durante o setup (ou uma senha aleatória gerada e exibida no terminal, caso deixado em branco)
