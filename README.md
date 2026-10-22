# Funerária Online — Backend

API do SaaS de funerárias. **Monolito modular** em .NET 10 com Clean Architecture dentro de cada módulo,
PostgreSQL, multi-tenancy por `id_tenant` e autenticação própria (ASP.NET Core Identity + JWT).
O frontend fica em outro repositório e conversa com esta API pelo contrato OpenAPI (INF-21).

## Rodando localmente

Pré-requisito: Docker. O .NET SDK 10 só é necessário para rodar a API fora do Docker e para os testes.

### Tudo no Docker (mais simples)

```bash
cp .env.example .env
docker compose up -d --build
```

| O quê | Endereço |
|---|---|
| Swagger | http://localhost:5080/swagger |
| Scalar | http://localhost:5080/scalar |
| OpenAPI | http://localhost:5080/openapi/v1.json |
| Saúde da API / do banco | http://localhost:5080/health · http://localhost:5080/health/ready |
| PostgreSQL | `localhost:5433` (usuário, senha e banco `funeraria`) |
| S3 (SeaweedFS) | http://localhost:8333, access key `funeraria`, secret `funeraria123`, bucket `funeraria-documentos` |
| E-mails (Mailpit) | http://localhost:8025 |
| pgAdmin (opcional) | `docker compose --profile tools up -d pgadmin` → http://localhost:5050 |

Depois de mudar o código da API, rode `docker compose up -d --build api`.

**Porta ocupada?** Se o compose disser que uma porta não está disponível, troque a porta no `.env`
(`POSTGRES_PORTA`, `API_PORTA` etc.). O PostgreSQL usa a 5433 por padrão para não conflitar com um
PostgreSQL instalado no Windows. No Windows, o Hyper-V também reserva faixas de portas; para ver quais:
`netsh interface ipv4 show excludedportrange protocol=tcp`.

> O armazenamento de arquivos usa **SeaweedFS** em vez da MinIO: a MinIO deixou de publicar imagens públicas
> e as tags antigas não existem mais. A API vai falar S3, então em produção vale S3 ou Azure Blob (INF-18).

### API com `dotnet run` (para debugar)

Suba só a infraestrutura e configure os segredos uma vez (ficam no user-secrets, fora do repositório):

```bash
docker compose up -d postgres seaweedfs seaweedfs-init redis mailpit
cd src/Funeraria.Api
dotnet user-secrets set "Jwt:ChaveAssinatura" "<uma chave aleatória com pelo menos 32 caracteres>"
dotnet user-secrets set "Seed:AdminSenha" "<senha do ADMIN da plataforma>"
dotnet user-secrets set "Seed:SenhaDemo" "<senha dos usuários de demonstração>"
cd ../..
dotnet run --project src/Funeraria.Api --launch-profile http
```

Se a API do Docker estiver rodando, pare-a antes (`docker compose stop api`): as duas usam a porta 5080.
Se mudou `POSTGRES_PORTA`, ajuste a connection string em `appsettings.Development.json`.

### Usuários e funerária de demonstração

Em Development a API aplica as migrations de todos os módulos e roda o seed ao iniciar:

| Quem | Login |
|---|---|
| ADMIN da plataforma | `admin@funeraria.localhost` + `SEED_ADMIN_SENHA` (ou `Seed:AdminSenha`), **sem** X-Tenant |
| Gestor da demo (FUNERARIA) | `gestor@demo.localhost` + `SEED_SENHA_DEMO` (ou `Seed:SenhaDemo`), X-Tenant `demo` |
| Funcionário da demo | `funcionario@demo.localhost` + a mesma senha da demo, X-Tenant `demo` |
| Cliente da demo (CLIENTE) | `cliente@demo.localhost` + a mesma senha da demo, X-Tenant `demo` |

**Testando pelo Swagger:** preencha o campo `X-Tenant` com `demo`, chame `POST /api/v1/auth/login`, copie o
`access_token`, clique em **Authorize** e cole o token. No navegador, a funerária também pode vir do
subdomínio: http://demo.localhost:5080.

Sem a chave JWT a API não sobe. Sem a senha do ADMIN ele não é criado (aparece um aviso no log).

### Cadastrando uma funerária (TEN-02)

1. `POST /api/v1/onboarding/funerarias` (público, sem X-Tenant) ou `POST /api/v1/admin/tenants` (ADMIN).
   A funerária nasce **PENDENTE**.
2. O gestor recebe o convite por e-mail. Em desenvolvimento ele aparece no Mailpit (http://localhost:8025).
   O link aponta para o frontend (`Identidade:Links`) e leva o token: `.../definir-senha?token=...`.
3. O frontend chama `POST /api/v1/auth/definir-senha` com `{ "token", "senha" }`. Isso também confirma o e-mail.
   O link vale 3 dias e uma única vez.
4. O gestor entra com `POST /api/v1/auth/login` usando o X-Tenant da funerária.
5. Os planos modelo (Essencial, Familiar, Premium) já estão lá, **inativos**: os valores são só ponto de partida.
6. O ADMIN aprova com `PATCH /api/v1/admin/tenants/{id}/status` e `{ "status": "ATIVA" }`. Suspender bloqueia
   na hora o acesso de todos os usuários da funerária.

Se quem for indicado como gestor já tiver conta na plataforma, recebe só um aviso de acesso e entra com a
senha que já usa (DEC-02).

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
| Identidade | `identidade` | Login, refresh token com rotação, logout, minhas funerárias, convite e definição de senha (AUT-01 a AUT-04) |
| Tenants | `tenants` | Auto-cadastro e cadastro pelo ADMIN, aprovação/suspensão, configurações da funerária (TEN-01 a TEN-03) |
| Clientes | `clientes` | Cadastro do cliente (conta + vínculo CLIENTE), meus dados, dependentes e busca pela equipe (Criar Cliente, Criar Dependente, CLI-01 a CLI-03) |
| Planos | `planos` | Gestão de planos, configurações em jsonb e planos modelo copiados para cada funerária nova (PLA-01, PLA-03) |
| Notificacoes | — | Envio de e-mail por SMTP (Mailpit em desenvolvimento). Templates e provider de produção pendentes (NOT-01) |
| Contratos | — | Só o contrato `IContratosApi`, com implementação provisória (ninguém tem contrato ativo) até o CON-01/02 |
| Catalogo, Financeiro, Funeral, Documentos, Translado, Terceiros, Relatorios | um por módulo | Projetos criados, ainda sem casos de uso |

Quem criar ou alterar um módulo atualiza esta tabela no mesmo pull request.

## Convenções

- **Rotas** em `/api/v1`. Prefixos por perfil: `/admin` (ADMIN), `/gestao` (FUNERARIA e FUNCIONARIO),
  `/terceiros`, e o restante para clientes e rotas compartilhadas.
- **JSON** em snake_case (`valor_mensal`, `access_token`).
- **Erros** em ProblemDetails com `code`, `trace_id` e `errors` por campo:
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

Troque `Planos` pelo módulo. Contextos existentes: `IdentidadeDbContext`, `TenantsDbContext`, `PlanosDbContext`, `ClientesDbContext`.

## Criando um módulo novo ou um caso de uso

1. Caso de uso: pasta em `Application/Commands/<Nome>` ou `Application/Queries/<Nome>` com o command/query,
   o validator e o handler (`public sealed`, exigido pelo Mediator), e o endpoint no projeto Endpoints.
2. Erros esperados ficam numa classe `<Algo>Erros` no Domain, com as mensagens dos cards.
3. Módulo com banco: DbContext herdando `ModuleDbContext`, `AddModuleDbContext<T>()` no `AddXModule()` e a
   migration inicial.
4. Atualize o mapa de módulos acima e, se for um módulo novo, a lista em `tests/Funeraria.ArchitectureTests/Solucao.cs`.
