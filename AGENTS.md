# Guidelines para Agentes de IA

Este arquivo define as diretrizes, padrões operacionais e configurações de skills para agentes de IA neste repositório.

## Coding standards e UI

Padrões arquiteturais, tokens de interface (Workbench Slate), proibições visuais e regras do terminal Windows em `docs/CODING_STANDARDS.md`.

## Observabilidade e Deploy

Monitoramento em tempo de execução via `/health`, rastreabilidade de commits de build e verificação de prontidão de contêineres em `docs/agents/deploy-observability.md`.

## Scripts operacionais

Scripts em PowerShell para testes rápidos (`scripts/test-fast.ps1`) e sincronização segura com push e atualização local (`scripts/sync-main.ps1`).

## Agent skills

### Issue tracker

GitHub Issues é o rastreador de issues e tarefas deste repositório (via web ou `gh` CLI). Veja `docs/agents/issue-tracker.md`.

### Triage labels

Vocabulário padrão de triagem de 5 papéis canônicos (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). Veja `docs/agents/triage-labels.md`.

### Domain docs

Layout single-context com `GLOSSARY.md` e decisões arquiteturais em `docs/adr/`. Veja `docs/agents/domain.md`.
