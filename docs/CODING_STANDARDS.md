# Padrões de Código e Diretrizes Operacionais (TechOS)

Este documento estabelece as regras de arquitetura, padrões visuais de interface, execução de comandos e observabilidade para o repositório.

---

## 1. Arquitetura e Domínio

- **State Pattern na Ordem de Serviço**: Toda transição de status (`Recebido`, `EmAnalise`, `AguardandoAprovacao`, `AguardandoPecas`, `EmReparo`, `Concluido`, `Entregue`) é governada pela classe `OrdemServico` e suas implementações de estado no projeto `AssistenciaTech.Domain`. Regras de transição proibidas devem disparar exceções de domínio testáveis.
- **Fachada Administrativa (`IAdminFacade`)**: Operações que coordenam múltiplos serviços (atualização de status com baixa de estoque, disparo de notificações e validação de garantia) devem ser orquestradas através do `AdminFacade` e `AdminDashboardService`, mantendo os controllers finos.
- **Cache Resiliente**: Chamadas de cache distribuído devem utilizar a abstração `IResilientCacheService` com circuit breaker para evitar que indisponibilidades no Redis derrubem a aplicação.
- **Banco de Dados e Migrações**: Modificações no esquema relacional devem ser declaradas via EF Core Migrations em conjunto com o `DbInitializer` para garantir idempotência em bancos remotos (como Neon/PostgreSQL).

---

## 2. Padrões de Interface e Tokens de UI (Workbench Slate)

- **Paleta Oficial (Grafite Industrial & Âmbar Técnico)**:
  - Fundo principal: `--bg-dark` (`#0E1116`)
  - Cartões e painéis: `--surface-dark` (`#151921`)
  - Bordas estruturais: `--border-dark` (`#28303F`) e `--border-strong` (`#3B465B`)
  - Acento prioritário: `--primary-accent` (`#F59E0B`)
- **Painéis e Cabeçalhos**:
  - Toda tela de listagem, criação ou edição deve utilizar `.workbench-panel`.
  - Cabeçalhos de seção devem exibir a categoria em caixa alta com `.workbench-eyebrow` acima do título principal.
- **Proibição de Cores Utilitárias Dispersas**:
  - Não utilizar classes de cores avulsas na navegação ou em cartões (`text-success`, `text-info`, `text-warning`, `text-danger`).
  - Itens da navbar devem ser neutros com a classe `.nav-link`, recebendo `.active` apenas na rota correspondente.
- **Padronização de Botões**:
  - Ação afirmativa primária: `.btn-primary` (salvar, confirmar, consultar).
  - Ação neutra ou de retorno: `.btn-outline-secondary` (cancelar, voltar).
  - Ação técnica contextual: `.btn-outline-primary` (gerar Pix, exportar).
  - Proibido o uso de `.btn-success`, `.btn-warning` ou `.btn-info` como botões de formulário padrão.
- **Tipografia de Dados Técnicos**:
  - IDs de OS, números de série, CPFs, códigos de rastreio e valores em Reais devem sempre aplicar a classe `.font-monospace`.
- **Ícones**:
  - Proibido o uso de emojis em botões, cabeçalhos ou tabelas.
  - Usar exclusivamente Bootstrap Icons com tamanho proporcional (`<i class="bi bi-..."></i>`).

---

## 3. Ambiente e Comandos de Terminal no Windows

- **Sintaxe do Terminal PowerShell 5.1**:
  - O PowerShell 5.1 não suporta o operador `&&`. Comandos encadeados com `&&` causam erro imediato de sintaxe.
  - Utilize sempre ponto e vírgula (`;`) ou execute comandos em etapas individuais.
- **Scripts Utilitários Homologados**:
  - Testes rápidos: `powershell -File scripts/test-fast.ps1`
  - Suíte completa: `powershell -File scripts/test-fast.ps1 -All`
  - Sincronização segura com push e atualização do OneDrive: `powershell -File scripts/sync-main.ps1 -CommitMessage "sua mensagem"`

---

## 4. Deploy e Observabilidade em Produção

- **Endpoint `/health`**:
  - Retorna relatório estruturado em JSON contendo o status de conectividade do PostgreSQL, do cache, tempo de resposta e o hash do commit (`RENDER_GIT_COMMIT` ou `GIT_COMMIT`).
- **Rodapé da Aplicação**:
  - O rodapé exibe o commit ativo vinculado ao endpoint `/health`, permitindo verificar imediatamente se o deploy remoto concluiu com sucesso.
