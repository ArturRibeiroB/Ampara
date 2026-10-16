# Funerária Online — Backend

API do SaaS de funerárias. **Monolito modular** em .NET 10 com Clean Architecture dentro de cada módulo,
PostgreSQL, multi-tenancy por `id_tenant` e autenticação própria (ASP.NET Core Identity + JWT).
O frontend fica em outro repositório e conversa com esta API pelo contrato OpenAPI (INF-21).

## Rodando localmente

Pré-requisitos: .NET SDK 10 e Docker.

```bash
docker compose up -d
```

Sobe PostgreSQL (5432), MinIO (9000, console em 9001), Redis (6379) e Mailpit (8025).
As credenciais do compose são só para uso local.

Configure os segredos uma vez (ficam fora do repositório, no user-secrets):

```bash
cd src/Funeraria.Api
dotnet user-secrets set "Jwt:ChaveAssinatura" "<uma chave aleatória com pelo menos 32 caracteres>"
dotnet user-secrets set "Seed:AdminSenha" "<senha do ADMIN da plataforma>"
dotnet user-secrets set "Seed:SenhaDemo" "<senha dos usuários de demonstração>"
```

Rode a API:

```bash
dotnet run --project src/Funeraria.Api --launch-profile http
```

Em Development a API aplica as migrations de todos os módulos e roda o seed ao iniciar:

| O quê | Acesso |
|---|---|
| Documentação (Scalar) | http://localhost:5080/scalar |
| OpenAPI | http://localhost:5080/openapi/v1.json |
| ADMIN da plataforma | `admin@funeraria.localhost` + `Seed:AdminSenha`, sem funerária |
| Funerária de demonstração | slug `demo`: http://demo.localhost:5080 ou cabeçalho `X-Tenant: demo` |
| Gestor da demo (FUNERARIA) | `gestor@demo.localhost` + `Seed:SenhaDemo` |
| Funcionário da demo | `funcionario@demo.localhost` + `Seed:SenhaDemo` |

Sem a chave JWT a API não sobe. Sem `Seed:AdminSenha` o ADMIN não é criado (aparece um aviso no log).

## Testes

```bash
dotnet test --solution Funeraria.slnx
```

- **Unitários** (`tests/Modules/*.UnitTests`): domínio e validadores.
- **Integração** (`tests/Modules/*.IntegrationTests`): a API inteira contra um PostgreSQL real em container
  (Testcontainers). Precisam do Docker rodando.
- **Arquitetura** (`tests/Funeraria.ArchitectureTests`): garantem as fronteiras entre módulos e camadas.

O runner é o Microsoft.Testing.Platform (configurado no `global.json`).

## Estrutura

```
src/
├── BuildingBlocks/                 Shared kernel, sem regra de negócio
│   ├── *.Domain                    Entity, AggregateRoot, ValueObject, DomainEvent, Result/Error
│   ├── *.Application               ICommand/IQuery (Mediator), behaviors, abstrações, perfis e policies
│   └── *.Infrastructure            DbContext base, outbox, event bus em memória, Result → ProblemDetails
├── Modules/<Modulo>/
│   ├── *.Domain                    Entidades e regras. Não depende de framework.
│   ├── *.Application               Commands/, Queries/, EventHandlers/
│   ├── *.Infrastructure            DbContext do módulo, configurações EF, repositórios, AddXModule()
│   ├── *.Endpoints                 Minimal APIs do módulo
│   └── *.Contracts                 API pública: interfaces, DTOs e eventos de integração
└── Funeraria.Api                   Host: só compõe os módulos (Modules/ModuleRegistration.cs)
tests/
├── Funeraria.Testing               Factory da API com Testcontainers e JWT de teste
├── Modules/                        Unitários e integração por módulo
└── Funeraria.ArchitectureTests
```

### Regras do monolito modular

- Um módulo só referencia o **Contracts** de outro módulo. Nunca Domain, Application, Infrastructure ou o DbContext.
- Comunicação entre módulos: interfaces do Contracts (consulta síncrona) ou eventos de integração.
- Cada módulo tem o **próprio DbContext e schema** no mesmo banco. Não existe transação entre módulos:
  o primeiro grava e publica um evento, o outro reage.
- Eventos de domínio são gravados no **outbox** do módulo na mesma transação e despachados em background.
  Handlers de eventos podem rodar mais de uma vez e precisam ser idempotentes.
- Relatorios é somente leitura: consulta views do schema `relatorios` com Dapper.

Os testes de arquitetura quebram o build se alguma regra for violada.

### Mapa de módulos (INF-01)

| Módulo | Schema | Estado |
|---|---|---|
| Identidade | `identidade` | Login, refresh token com rotação, logout, minhas funerárias (AUT-01 a AUT-04) |
| Tenants | `tenants` | Entidade Funerária e resolução por subdomínio. Cadastro/onboarding pendente (TEN-02) |
| Planos | `planos` | Gestão de planos e configurações em jsonb (PLA-01, PLA-03) |
| Clientes, Catalogo, Contratos, Financeiro, Funeral, Documentos, Translado, Terceiros, Notificacoes, Relatorios | um por módulo | Projetos criados, ainda sem casos de uso |

Quem criar ou alterar um módulo atualiza esta tabela no mesmo pull request.

## Convenções

- **Rotas** em `/api/v1`. Prefixos por perfil: `/admin` (ADMIN), `/gestao` (FUNERARIA e FUNCIONARIO),
  `/terceiros`, e o restante para clientes e rotas compartilhadas.
- **JSON** em snake_case (`valor_mensal`, `access_token`).
- **Erros** em ProblemDetails com `code`, `traceId` e `errors` por campo:
  400 validação, 401 credenciais/sessão, 403 sem permissão, 404, 409 conflito, 423 conta bloqueada.
  Handlers devolvem `Result`; exceção só para falha inesperada (500).
- **Tenant**: vem do subdomínio (ou do cabeçalho `X-Tenant`, enviado pelo frontend) e precisa bater com o
  `id_tenant` do JWT. Entidades `ITenantEntity` recebem filtro global no EF Core. Consultas com Dapper
  **precisam** filtrar `id_tenant` à mão.
- **Autenticação**: access token JWT de 15 min no corpo da resposta; refresh token de 7 dias só em cookie
  httpOnly/Secure/SameSite=Strict, com rotação a cada uso. Reusar um refresh token já trocado derruba todas
  as sessões do usuário.
- **Banco**: tabelas e colunas em snake_case, `timestamptz`, concorrência otimista pela coluna `xmin`,
  auditoria (`created_at`, `updated_at`, `created_by`) preenchida por interceptor.

## Migrations

Cada módulo tem as próprias migrations, com histórico no próprio schema:

```bash
dotnet tool restore
dotnet ef migrations add <Nome> \
  --project src/Modules/Planos/Funeraria.Modules.Planos.Infrastructure \
  --startup-project src/Funeraria.Api \
  --context PlanosDbContext \
  --output-dir Database/Migrations
```

Troque `Planos` pelo módulo. Contextos existentes: `IdentidadeDbContext`, `TenantsDbContext`, `PlanosDbContext`.

## Criando um módulo novo ou um caso de uso

1. Caso de uso: pasta em `Application/Commands/<Nome>` ou `Application/Queries/<Nome>` com o command/query,
   o validator e o handler (`public sealed`, exigido pelo Mediator), e o endpoint no projeto Endpoints.
2. Erros esperados ficam numa classe `<Algo>Erros` no Domain, com as mensagens dos cards.
3. Módulo com banco: DbContext herdando `ModuleDbContext`, `AddModuleDbContext<T>()` no `AddXModule()` e a
   migration inicial.
4. Atualize o mapa de módulos acima e, se for um módulo novo, a lista em `tests/Funeraria.ArchitectureTests/Solucao.cs`.
