# 🧠 Contexto Geral do Projeto - TechOS (AssistenciaTech)

> **Data de Atualização:** 05/11/2026  
> **Status:** Ativo  
> **Propósito:** Segundo Cérebro e Memória Contínua para Agentes de IA e Desenvolvedor no Obsidian.

---

## 🎯 Sobre o Sistema

O **TechOS** é uma plataforma de gestão e bancada para lojas de informática e assistência técnica de aparelhos eletrônicos:
- **Framework:** C# ASP.NET Core MVC no .NET 8
- **Banco de Dados:** PostgreSQL 16 com Entity Framework Core (`Npgsql`)
- **Cache:** Redis distribuído com fallback local e Circuit Breaker (`ResilientCacheService`)
- **Laudos e Comprovantes:** QuestPDF (desenho vetorial A4) + QRCoder com link dinâmico
- **Comunicação:** Bot do Telegram via Webhook com Deep Linking (`/start {clienteId}`)
- **Fiscal & Financeiro:** Pagamento Pix com Webhook HMAC, cálculo automático de ISS/ICMS e emissão de NFS-e (padrão nacional ABRASF)
- **Mobile:** API REST JWT com suporte offline-first (SQLite / WatermelonDB)

---

## 🏛️ Decisões Arquiteturais Recentes

1. **Estrutura de Repositório Limpa:**
   - Remoção de todos os scripts temporários e sobras de patches.
   - Linguagens detectadas naturalmente no GitHub: C#, HTML/Razor, CSS e Dockerfile.
   - Documentação técnica consolidada na pasta `docs/`.

2. **Skills de Engenharia Instaladas (`.agents/skills/`):**
   - Integrado o pacote de skills de engenharia do Matt Pocock.
   - Configurado `AGENTS.md` e `docs/agents/` com suporte a GitHub Issues e vocabulário canônico de triagem (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`).

3. **Integração com Obsidian:**
   - O repositório opera como Vault local.
   - Qualquer arquivo `.md` editado no Obsidian é consumido pelos agentes, e vice-versa.

---

## 📌 Próximos Passos & Backlog

- [ ] Continuar evolução da camada DDD em `src/AssistenciaTech.Domain/`.
- [ ] Conectar triagem automática de issues via `/triage`.
- [ ] Manter documentações de decisões em `docs/adr/`.
