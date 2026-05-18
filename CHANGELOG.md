# Changelog

Todas as mudancas relevantes do RepairDesk ficam registadas aqui.

Formato inspirado em [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).  
O produto usa versionamento SemVer pragmatico: `MAJOR.MINOR.PATCH`.

## [Unreleased]

### Added

- Stock de peças: entidades `Part`/`PartMovimento`, CRUD API, stock baixo, movimentos assinados, import CSV, UI `/stock` e integração com reparações.

- Campos personalizados de equipamento por tenant: templates configuraveis, values por reparacao, API admin, UI em definicoes/reparacoes, portal publico e PDF de orcamento.

### Changed

### Fixed

### Security

## [0.1.0] - 2026-05-17

### Added

- Backend .NET 10 com arquitetura em camadas, EF Core e SQL Server.
- Frontend React 19 + Vite + Tailwind.
- Docker Compose local com API, frontend, SQL Server e Redis.
- Autenticacao, clientes, reparacoes, trabalhos, despesas, dashboard e portal publico.
- Testes xUnit para areas criticas do backend.

### Changed

- Projeto preparado para CI/CD com GitHub Actions, GHCR e deploy por Docker Compose.

### Security

- Baseline de isolamento multi-tenant no backend.
- Configuracao inicial para secret scanning, CodeQL e atualizacoes Dependabot.
