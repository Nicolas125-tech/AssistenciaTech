# TechOS - Sistema de Gestão e Bancada Operacional para Assistência Técnica

![C#](https://img.shields.io/badge/C%23-.NET%208-239120)
![ASP.NET Core MVC](https://img.shields.io/badge/ASP.NET_Core_MVC-8.0-512BD4)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16%20%2B%20EF%20Core-316192)
![Redis](https://img.shields.io/badge/Redis-Cache%20Distribu%C3%ADdo-DD0031)
![QuestPDF](https://img.shields.io/badge/QuestPDF-Laudos%20%26%20Recibos-brightgreen)
![Docker](https://img.shields.io/badge/Docker-Compose-2CA5E0)
![Testes](https://img.shields.io/badge/Testes-xUnit%20%2B%20Moq-blueviolet)

- **Aplicação em produção (Demonstração):** https://assistenciatech.onrender.com
- **Usuário de demonstração:** `demo@assistenciatech.com`
- **Senha:** `Demo@1234` *(navegação livre com bloqueio de mutações no banco via filtro de segurança)*

O **TechOS** é uma plataforma desenvolvida para resolver os gargalos reais do fluxo de bancada em assistências técnicas e oficinas de manutenção eletrônica. O sistema elimina o controle informal em papel e planilhas, unificando o ciclo de vida completo da Ordem de Serviço (OS): desde o check-in do equipamento, controle de peças e estoque, empréstimo de aparelhos reservas, comunicação automática com o cliente via Telegram sem exigência de senhas complexas, até o laudo técnico impresso em PDF com QR Code dinâmico, faturamento com conciliação Pix e cálculo automatizado de tributos com emissão fiscal NFS-e.

Abaixo você encontra a arquitetura do software detalhada por baixo dos panos, o funcionamento de cada módulo, o modelo de concorrência e resiliência, instruções para execução local via Docker e o roteiro de validação prática.

---

## Sumário

1. [Linha de raciocínio: do balcão da assistência à arquitetura do software](#1-linha-de-raciocínio-do-balcão-da-assistência-à-arquitetura-do-software)
2. [Arquitetura do sistema e modelo duplo (MVC e Domínio DDD)](#2-arquitetura-do-sistema-e-modelo-duplo-mvc-e-domínio-ddd)
3. [Módulos e funcionalidades detalhadas](#3-módulos-e-funcionalidades-detalhadas)
4. [Engenharia de banco de dados e auditoria em JSON Diff](#4-engenharia-de-banco-de-dados-e-auditoria-em-json-diff)
5. [Integrações externas e resiliência operacional](#5-integrações-externas-e-resiliência-operacional)
6. [Segurança da aplicação e modo demonstração](#6-segurança-da-aplicação-e-modo-demonstração)
7. [Como rodar o projeto localmente (Docker Compose e Nativo)](#7-como-rodar-o-projeto-localmente-docker-compose-e-nativo)
8. [Roteiro de testes de bancada de ponta a ponta](#8-roteiro-de-testes-de-bancada-de-ponta-a-ponta)
9. [Suíte de testes automatizados](#9-suíte-de-testes-automatizados)
10. [Contato do desenvolvedor](#10-contato-do-desenvolvedor)

---

## 1. Linha de raciocínio: do balcão da assistência à arquitetura do software

Ao projetar o TechOS, o foco inicial foi mapear as cinco falhas operacionais mais comuns que geram prejuízo financeiro ou perda de confiança com clientes em lojas de manutenção:

1. **Inconsistência de status e etapas puladas na bancada:** Em oficinas, é comum uma OS passar de "Recebido" direto para "Entregue" sem diagnóstico registrado, ou ser cobrada sem aprovação prévia do orçamento. No TechOS, o fluxo é regido por uma máquina de estados explícita (State Pattern) com transições permitidas apenas entre etapas válidas: `Recebido` -> `Em Análise` -> `Aguardando Aprovação / Aguardando Peças` -> `Em Manutenção` -> `Pronto` -> `Entregue` ou `Cancelado`.
2. **Furo de inventário no uso de peças caras:** Quando um técnico usa uma tela ou memória em um conserto, a baixa não pode depender de lembrete manual no final do dia. O serviço `EstoqueService` vincula a reserva e o consumo da peça à OS em transação atômica. Se a OS for cancelada ou a peça removida do escopo, o estoque é estornado automaticamente.
3. **Fricção na comunicação com o cliente:** Pedir para clientes criarem login e senha para acompanhar o reparo gera abandono da consulta e sobrecarga de mensagens no balcão. A solução foi adotar Deep Linking com o bot oficial do Telegram (`/start {clienteId}`). O cliente clica em um link único enviado no check-in, o sistema associa o `TelegramChatId` dele em segundo plano e cada mudança de status na bancada dispara avisos instantâneos de forma passiva.
4. **Comprovantes físicos ilegíveis e desconectados do digital:** Conversores de HTML para PDF em servidores Linux costumam ser lentos, quebrar fontes e falhar com CSS complexo. O TechOS usa a biblioteca C# fluente QuestPDF, compilando os recibos A4 diretamente em desenho vetorial SkiaSharp, injetando um QR Code dinâmico apontando direto para o portal público de consulta do cliente.
5. **Cálculo tributário caótico na entrega:** Empresas de reparo prestam serviços (ISS) e revendem componentes (ICMS), cada qual com alíquotas e bases de cálculo distintas. O módulo financeiro separa automaticamente a parcela de mão de obra e peças, calcula o valor tributável e monta o XML da Nota Fiscal de Serviço (NFS-e) no padrão nacional ABRASF.

---

## 2. Arquitetura do sistema e modelo duplo (MVC e Domínio DDD)

O projeto está estruturado em uma arquitetura limpa em transição controlada para Domain-Driven Design (DDD):

```
AssistenciaTech/
├── Controllers/            # Endpoints MVC e APIs REST para Mobile
├── DTOs/                   # Contratos de entrada protegidos contra overposting
├── Data/                   # AppDbContext, interceptors EF Core e mapeamentos
├── Models/                 # Modelos de produção e persistência relacional
├── Services/               # Regras de negócio, fachadas e orquestradores
├── ViewModels/             # Projeções fortemente tipadas para as telas Razor
├── Views/                  # Telas em Razor Views e Bootstrap 5
├── docs/                   # Documentação detalhada de performance e arquitetura
├── scripts/                # Scripts utilitários de infraestrutura e setup
└── src/
    ├── AssistenciaTech.Domain/          # Entidades puras e State Pattern
    ├── AssistenciaTech.Application/     # Casos de uso e orquestração
    └── AssistenciaTech.Infrastructure/  # Clientes externos e mensageria
```

### O padrão de modelo duplo

Para manter alta produtividade sem abrir mão de isolamento de regras críticas:
- **Camada de Produção MVC/EF (`Models/` e `Controllers/`):** Alimenta as Views do sistema administrativo via Entity Framework Core (`Npgsql`), controlando o fluxo ativo com constantes em `WorkflowStatus.cs`.
- **Camada de Domínio Puro (`src/AssistenciaTech.Domain/`):** Implementa o State Pattern estrito através da interface `IOrdemServicoState` e classes de estado em `OrdemServicoStates.cs`, garantindo que comportamentos de validação possam ser testados de forma unitária sem dependência de banco de dados.

### Proteção por DTOs em todas as entradas

Nenhum Controller aceita entidades de domínio diretamente no corpo de requisições `POST` ou `PUT`. O uso de DTOs como `OrdemServicoCreateDto`, `PecaCreateDto` e `ClienteDto` elimina vetores de ataque por Mass Assignment, impedindo que campos sensíveis (como ID, flags de garantia, desconto administrativo ou status) sejam forçados por requisições maliciosas.

---

## 3. Módulos e funcionalidades detalhadas

### Painel do técnico e Backoffice Administrativo
- **Dashboard com Métricas em Tempo Real:** Consolida ordens abertas, aparelhos concluídos, receita prevista e alertas de estoque baixo. O serviço `AdminFacade` centraliza as operações complexas do controlador.
- **Controle de Evidências Fotográficas:** Upload seguro de imagens com validação de extensão MIME, impedindo execução remota e organizando laudos fotográficos anexados à OS.
- **Exportação de Relatórios em CSV:** Extração com paginação e streaming em memória, sem gerar picos de alocação de memória no servidor.

### Gestão de estoque e peças de reposição
- Catálogo de componentes com preço de custo, preço de venda e margem.
- Definição de `QuantidadeMinima` por peça.
- Alerta visual no painel quando o estoque atinge o nível de reposição, com cache no Redis para evitar contagem de registros repetitiva no banco.

### Equipamentos de backup (Aparelhos reservas)
- Cadastro de smartphones, notebooks e periféricos destinados a empréstimo.
- O serviço `EquipamentoBackupService` monitora a OS: ao vincular um aparelho reserva, seu estado muda para "Emprestado"; na finalização ou cancelamento da OS, o sistema desvincula e retorna o status para "Disponível".

### Faturamento, conciliação Pix e NFS-e
- **Controle de Pagamentos:** Status granular (`Pendente`, `Pago Parcial`, `Pago Total`).
- **Webhook Pix com HMAC:** Endpoint preparado para conciliação bancária automática, validando a assinatura enviada no header com tempo constante para evitar ataques de temporização.
- **Emissão Fiscal NFS-e:** O serviço `NfseXmlGeneratorService` produz o documento XML no padrão ABRASF, com base nas regras tributárias calculadas pelo `TributacaoService`.

### Portal público do cliente e área autenticada
- **Consulta Pública:** O cliente informa o número da OS e seu CPF no portal para acompanhar status, laudo técnico preliminar e previsão de entrega sem precisar de cadastro prévio.
- **Área "Meus Equipamentos":** Área dedicada para clientes com histórico de manutenções anteriores e termos de garantia vigentes.

### Aplicativo mobile para técnicos externos
- Endpoints REST em `MobileApiController` autenticados via tokens JWT.
- Suporte a arquitetura offline-first com WatermelonDB e SQLite, permitindo que o técnico atenda em campo sem conexão e sincronize os laudos ao retornar à rede.

---

## 4. Engenharia de banco de dados e auditoria em JSON Diff

O banco de dados relacional é o PostgreSQL 16, integrado via Entity Framework Core.

### Auditoria forense com AuditoriaInterceptor

Todas as modificações em ordens de serviço passam pelo interceptor assíncrono `AuditoriaInterceptor` antes do `SaveChangesAsync`. O interceptor realiza:
- Leitura do ChangeTracker comparando valores originais e novos.
- Geração de um diff serializado em JSON com campos alterados no formato `{"Campo": {"De": "X", "Para": "Y"}}`.
- Gravação na tabela `AuditoriaOS` com carimbo de data/hora em UTC estrito e registro do usuário autenticado responsável pela alteração.
- Criação de logs de eventos imutáveis para garantir rastreabilidade completa perante o Código de Defesa do Consumidor.

---

## 5. Integrações externas e resiliência operacional

### Bot do Telegram com Deep Linking
- **Configuração:** O bot oficial recebe mensagens via Webhook HTTPS no `TelegramWebhookController`.
- **Pareamento Automático:** Quando o cliente clica no link gerado pelo sistema (`t.me/seu_bot?start=123`), o bot associa a conta ao cliente na primeira interação.
- **Notificações Ativas:** Mudanças de status na bancada disparam notificações com textos personalizados informando a conclusão de testes, disponibilidade para retirada ou orçamento pronto para aprovação.

### Geração de PDFs com QuestPDF e QRCoder
- Renderização nativa vetorial direta para arquivo PDF em formato A4.
- Inclusão de dados da assistência técnica, termos de garantia, discriminação de peças e mão de obra, assinaturas e QR Code legível por câmera que abre a consulta pública no navegador do cliente.

### Cache distribuído com ResilientCacheService
- Cache de leituras frequentes (como dashboard e contadores) no Redis com tempo de vida (TTL) configurável.
- Implementação de Circuit Breaker: se o servidor Redis cair ou ficar inacessível, a aplicação chaveia automaticamente para cache em memória local sem interromper as operações do usuário.

---

## 6. Segurança da aplicação e modo demonstração

- **Proteção contra Mass Assignment:** Uso obrigatório de DTOs nas operações de mutação.
- **Proteção contra CSRF:** Tokens de antifalsificação (`[ValidateAntiForgeryToken]`) em todos os formulários `POST`.
- **Segurança no Webhook:** Validação de segredo de webhook com comparação em tempo constante (`CryptographicOperations.FixedTimeEquals`), prevenindo vazamento de dados por medição de tempo de resposta.
- **Autenticação Segura:** Autenticação por Cookies para o painel web com proteção contra Open Redirect, e JWT para os endpoints da API Mobile.
- **Fail-Fast no Startup:** O `Program.cs` recusa inicializar caso configurações de segurança críticas (como segredo JWT ou dados de conexão) estejam vazias, impedindo deploys vulneráveis.
- **Filtro de Demonstração (`DemoModeFilter`):** O ambiente online em nuvem roda com a conta de teste `demo@assistenciatech.com`. O filtro intercepta qualquer tentativa de escrita (`POST`, `PUT`, `DELETE`), exibindo uma mensagem amigável e preservando a integridade dos dados compartilhados.

---

## 7. Como rodar o projeto localmente (Docker Compose e Nativo)

### Pré-requisitos
- Docker e Docker Compose instalados **ou** SDK do .NET 8 com PostgreSQL local.

### Opção 1: Rodando completo via Docker Compose (Recomendado)

1. Clone o repositório:
   ```bash
   git clone https://github.com/Nicolas125-tech/AssistenciaTech.git
   cd AssistenciaTech
   ```

2. Crie o arquivo de ambiente a partir do exemplo:
   ```bash
   cp .env.example .env
   ```

3. Suba todos os serviços (Aplicação Web, Banco PostgreSQL e Redis):
   ```bash
   docker-compose up -d --build
   ```

4. Acesse a aplicação no navegador:
   ```
   http://localhost:8080
   ```

### Opção 2: Rodando nativo com .NET SDK

1. Suba apenas as dependências de infraestrutura:
   ```bash
   docker-compose up -d database cache
   ```

2. Gere as credenciais administrativas locais:
   ```bash
   ./scripts/setup_local_admin.sh
   ```

3. Restaure os pacotes e inicie a aplicação:
   ```bash
   dotnet restore
   dotnet run
   ```

---

## 8. Roteiro de testes de bancada de ponta a ponta

Para validar o fluxo do sistema após subir a aplicação:

1. **Acesso Administrativo:** Entre em `/Account/Login` com seu usuário administrativo ou use as credenciais locais geradas.
2. **Cadastro de Cliente e Peça:** Cadastre um cliente com telefone e cadastre uma peça com estoque inicial de 5 unidades.
3. **Abertura de Ordem de Serviço:** Vá em "Nova Ordem de Serviço", selecione o cliente, descreva o defeito relatado e adicione a peça cadastrada.
4. **Verificação de Estoque:** Acesse o módulo de peças e observe que o saldo em estoque foi reduzido automaticamente de 5 para 4.
5. **Transição de Status e Laudo:** Edite a OS avançando para "Em Manutenção" e em seguida para "Pronto". Anexe uma foto de evidência.
6. **Impressão de Comprovante:** Clique no botão de impressão da OS para gerar o laudo A4 com o QR Code. Aponte a câmera do celular para o código e verifique a abertura direta da consulta pública.
7. **Fechamento Financeiro:** No módulo financeiro, acesse o faturamento da OS, gere o XML da NFS-e e valide a divisão tributária de mão de obra e peças.

---

## 9. Suíte de testes automatizados

O projeto conta com mais de 180 testes automatizados cobrindo todas as camadas, implementados com xUnit, Moq e FluentAssertions:

- **AssistenciaTech.Domain.Tests:** Validação rigorosa da máquina de estados, regras de garantia, cálculo de orçamentos e transições ilegais.
- **AssistenciaTech.Application.Tests:** Testes unitários de todos os controladores (`AdminController`, `ConsultaController`, `FaturamentosController`, `PecasController`, `TelegramWebhookController`), serviços de negócio e helpers.
- **AssistenciaTech.Data.Tests:** Validação do `AppDbContext`, modelos e comportamento do `AuditoriaInterceptor` com banco em memória.
- **AssistenciaTech.Infrastructure.Tests:** Testes de resiliência e chaveamento do `ResilientCacheService` e despacho de mensagens via Telegram.

Para rodar todos os testes localmente:
```bash
dotnet test AssistenciaTech.sln
```

---

## 10. Contato do desenvolvedor

Desenvolvido por **Nicolas Mandarino**.

- **WhatsApp:** [(43) 98444-5767](https://wa.me/5543984445767)
- **Instagram:** [@_nicolas_mb](https://instagram.com/_nicolas_mb)
- **GitHub:** [Nicolas125-tech](https://github.com/Nicolas125-tech)
