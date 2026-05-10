# 💻 AssistênciaTech - Sistema de Gestão para Assistência Técnica

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![.NET 8](https://img.shields.io/badge/.NET_8-5C2D91?style=for-the-badge&logo=.net&logoColor=white)
![ASP.NET Core MVC](https://img.shields.io/badge/ASP.NET_MVC-512BD4?style=for-the-badge&logo=asp.net&logoColor=white)
![Bootstrap 5](https://img.shields.io/badge/Bootstrap_5-7952B3?style=for-the-badge&logo=bootstrap&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2CA5E0?style=for-the-badge&logo=docker&logoColor=white)

O **AssistênciaTech** é um sistema completo (MVP) desenvolvido para automatizar e profissionalizar a gestão de lojas de informática e assistência técnica de computadores e notebooks. Ele une uma vitrine moderna para atrair clientes com um painel administrativo poderoso para os técnicos.

## ✨ Funcionalidades

### 🌐 Área Pública (Cliente)
- **Landing Page Moderna**: Design responsivo criado com Bootstrap 5 para exibição de serviços (Formatação, Limpeza, Reparo de Placas).
- **Consulta de OS**: Uma ferramenta self-service onde o cliente consulta o andamento do seu equipamento utilizando o **Nº da Ordem de Serviço + CPF**, sem precisar enviar mensagens ao técnico.
- **Botões de Ação**: Integração direta para contato ágil via WhatsApp.

### 🔒 Backoffice (Painel do Técnico)
- **Autenticação Segura**: Acesso restrito protegido pelo sistema de Cookie Authentication nativo do ASP.NET Core.
- **Dashboard Resumo**: Visualização instantânea de Ordens Abertas, Equipamentos Prontos e Faturamento Previsto.
- **CRUD de Ordens de Serviço (OS)**: Criação, listagem (ordenada pelas mais recentes), edição rápida de *Status* e deleção de equipamentos. Badges de cores dinâmicas indicam o status atual.
- **Impressão de Recibos em PDF**: Geração instantânea de comprovantes em PDF (padrão A4) contendo os dados do serviço, orçamento e área para assinatura do cliente utilizando a biblioteca **QuestPDF**.

## 🛠️ Tecnologias Utilizadas

- **Linguagem**: C#
- **Framework**: .NET 8 (ASP.NET Core MVC)
- **Banco de Dados**: SQLite (com Entity Framework Core / Fluent API)
- **Frontend**: Razor Views, HTML5, CSS3, Bootstrap 5 e Bootstrap Icons
- **Geração de PDF**: QuestPDF (Licença Community)
- **Infraestrutura e Deploy**: Docker (Multi-stage build), Docker Compose, Cloudflare Tunnel (Quick Tunnels para HTTPS público)

## 🚀 Como Executar o Projeto

O projeto foi construído utilizando a arquitetura *Cloud-Native* e possui suporte completo ao Docker, tornando sua execução trivial.

### 🐳 Via Docker (Recomendado)
Certifique-se de ter o Docker Desktop instalado. O banco de dados (SQLite) será criado automaticamente (`EnsureCreated`) no primeiro boot e seus dados serão preservados através de volumes nomeados.

1. Clone o repositório.
2. Abra o terminal na raiz do projeto.
3. Execute o comando:
```bash
docker-compose up -d --build
```
4. Acesse o sistema através do navegador em `http://localhost:8080`.
*(O Cloudflare Tunnel também irá gerar automaticamente uma URL pública HTTPS visível nos logs do contêiner `tunnel`)*.

### 💻 Rodando Nativo (Sem Docker)
1. Certifique-se de ter o SDK do .NET 8 instalado.
2. Abra o terminal na raiz do projeto.
3. Restaure os pacotes e rode a aplicação:
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
