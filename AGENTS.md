# TechOS Agent Guidelines

## Ambiente e Execução
- **Terminal Windows**: PowerShell 5.1 não suporta `&&`. Use ponto e vírgula (`;`) ou execute comandos em passos separados.
- **Testes rápidos**: `powershell -File scripts/test-fast.ps1` (ou adicione `-All` para a suíte completa).
- **Sincronização**: `powershell -File scripts/sync-main.ps1 -CommitMessage "<mensagem>"` (executa suíte de testes, commit, push para `origin/main` e sincronização com OneDrive).

## Ponteiros de Contexto

- **Padrões de Código e UI** (`docs/CODING_STANDARDS.md`): Consulte ao escrever código C#, criar/alterar views Razor e CSS (tokens Workbench Slate, sem cores dispersas, sem emojis), configurar cache resiliente (`IResilientCacheService`) ou transições de estado da Ordem de Serviço.
- **Domínio e Vocabulário** (`docs/agents/domain.md`): Consulte antes de criar ou renomear entidades, métodos, tabelas e testes. Alinhe os termos com `GLOSSARY.md` e consulte decisões arquiteturais em `docs/adr/`.
- **Deploy e Observabilidade** (`docs/agents/deploy-observability.md`): Consulte ao diagnosticar erros 404/500, inspecionar contêineres de produção ou verificar hash de commit ativo e integridade via `/health`.
- **Rastreador de Tarefas** (`docs/agents/issue-tracker.md`): Consulte ao buscar, criar, comentar ou mapear dependências bloqueantes de issues via `gh` CLI.
- **Labels de Triagem** (`docs/agents/triage-labels.md`): Consulte ao classificar demandas nos 5 papéis canônicos (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`).
