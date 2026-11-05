# ADR 001: Unificação do State Pattern de Domínio, Fluent API e EF Core Migrations

- **Status:** Aceito
- **Data:** 2026-11-05

## Contexto

O projeto possuía dois modelos desconectados para `OrdemServico`:
1. `src/AssistenciaTech.Domain/Entities/OrdemServico.cs` implementando o padrão de projeto **State** (`IOrdemServicoState`), mas sem referência no projeto web principal (`AssistenciaTech.csproj`).
2. `Models/OrdemServico.cs` utilizado pelo Entity Framework Core (`AppDbContext`) e pelos controllers MVC, com regras de transição de status acopladas diretamente em métodos privados do `AdminController`.

Além disso, o `Program.cs` executava `Database.EnsureCreated()` seguido de dezenas de comandos `ALTER TABLE` e `CREATE TABLE IF NOT EXISTS` inline na inicialização, dificultando o versionamento formal do esquema do banco de dados.

## Decisão

1. **Unificação Pragmática do Domínio:** Referenciar `AssistenciaTech.Domain` em `AssistenciaTech.csproj` e introduzir `OrdemServicoStateMachine` em `AssistenciaTech.Domain.States`, conectando todos os estágios operacionais (`Recebido`, `Em Análise`, `Aguardando Aprovação do Orçamento`, `Aguardando Peças`, `Em Reparo`, `Concluído`, `Entregue ao Cliente`, `Cancelado`) ao contrato `IOrdemServicoState`.
2. **Mapeamento via Fluent API:** Extrair as configurações de tabela, precisão decimal (`decimal(18,2)`), índices de busca e chaves estrangeiras para classes `IEntityTypeConfiguration<T>` em `Data/Configurations/EntityConfigurations.cs`.
3. **EF Core Migrations e Inicializador Dedicado:** Substituir o bloco de SQL manual do `Program.cs` pela migration formal `InitialCreate` em `Migrations/` e pelo serviço `DbInitializer`, que aplica `MigrateAsync()` com detecção automática de bancos legados e realiza o seed idempotente dos usuários administrativo e demo.
4. **Observabilidade e CI:** Adicionar Health Checks nativos (`/healthz` e `/ready`) validando PostgreSQL e Cache Resiliente, suporte a `ProblemDetails` (RFC 7807) e pipeline de integração contínua no GitHub Actions (`.github/workflows/ci.yml`).

## Consequências

- **Positivas:** Código do `Program.cs` enxuto e testável; regras de transição e baixa/estorno de estoque testadas isoladamente na camada de domínio; evolução segura do esquema de banco via `dotnet ef migrations`; validação automatizada de build e testes a cada push.
- **Cuidados:** Bancos já criados anteriormente via `EnsureCreated()` têm a tabela `__EFMigrationsHistory` sincronizada automaticamente pelo `DbInitializer` na primeira execução.
