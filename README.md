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
| Painel de jobs (Hangfire) | http://localhost:5080/hangfire?access_token=`<token do ADMIN>`. A API troca o token por um cookie e abre o painel |
| pgAdmin (opcional) | `docker compose --profile tools up -d pgadmin` → http://localhost:5050 (sem login; o servidor "Funerária (local)" já vem cadastrado) |

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
| Consultor da demo | `consultor@demo.localhost` + a mesma senha da demo, X-Tenant `demo` |
| Cliente da demo (CLIENTE) | `cliente@demo.localhost` + a mesma senha da demo, X-Tenant `demo` |
| Terceiros da demo (TERCEIRO) | `frota@demo.localhost`, `despachante@demo.localhost` e `fornecedor@demo.localhost` + a mesma senha, X-Tenant `demo` |

A demo já começa com os planos e o catálogo modelo **ativos** (as funerárias novas recebem tudo inativo) e com as
regras de cobrança padrão, então o cliente da demo já consegue contratar. Banco criado antes desta versão do seed
não recebe os planos e o catálogo: recrie o volume do Postgres (`docker compose down -v`) para gerar a demo de novo.

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

### Confirmação de e-mail e senha esquecida (AUT-05)

- O cliente que se cadastra recebe o link `.../confirmar-email?token=...`; o frontend chama
  `POST /api/v1/auth/confirmar-email` com `{ "token" }`. Quem já tinha o e-mail confirmado recebe só as boas-vindas.
- `POST /api/v1/auth/esqueci-senha` com `{ "email" }` responde 204 sempre. O link vai para quem tem vínculo ativo
  com a funerária do X-Tenant; sem X-Tenant, só para o ADMIN (link no subdomínio `admin`).
- `POST /api/v1/auth/redefinir-senha` com `{ "token", "senha" }`. O link vale 30 minutos e uma vez; a troca
  encerra as sessões abertas. Validades e links em `Identidade` no appsettings.

## Testes

```bash
dotnet test --solution Funeraria.slnx
```

- **Unitários** (`tests/Modules/*.UnitTests`): domínio e validadores.
- **Integração** (`tests/Modules/*.IntegrationTests`): a API inteira contra um PostgreSQL real em container
  (Testcontainers). Precisam do Docker rodando. Os testes do Tenants sobem também um SeaweedFS, para o logo.
- **Arquitetura** (`tests/Funeraria.ArchitectureTests`): garantem as fronteiras entre módulos e camadas.

O runner é o Microsoft.Testing.Platform (configurado no `global.json`).

O `MigrationsTests` falha quando algum módulo muda o modelo sem criar a migration.

## CI (INF-15)

`.github/workflows/ci.yml` roda em todo push e pull request:

1. Build em Release, gerando o `artifacts/openapi/openapi.json` sem subir a API (publicado como artefato do run).
2. Todos os testes (o runner do GitHub tem Docker para os Testcontainers).
3. Push na `main` ou na `developer`: imagem da API em `ghcr.io/<dono>/<repositorio>-api`, com a tag da branch e do commit.
4. Tag `vX.Y.Z`: imagem com a versão e release no GitHub com o `openapi.json` da mesma versão.
5. Contrato (oasdiff): compara o `openapi.json` novo com o da última release e escreve as mudanças no resumo do run.
   Quebra de contrato só gera aviso no dia a dia; na tag, falha se a versão maior não subiu (ver abaixo).

Para **bloquear o merge com testes falhando**: Settings → Branches → regra para `main` e `developer` →
*Require status checks to pass* → marque **Build e testes**.

Gerar o openapi.json localmente:

```bash
dotnet build src/Funeraria.Api -p:OpenApiGenerateDocumentsOnBuild=true
```

## Contrato da API e versões (INF-21)

O `openapi.json` é o contrato com o frontend. Cada release publica o arquivo e a imagem Docker com a mesma versão,
e o `info.version` do documento é essa versão (fora de release, `X.Y.Z-dev`).

- **Versionamento semântico:** quebra de contrato (remover ou renomear campo/endpoint, mudar tipo, tornar campo
  obrigatório) = versão maior; endpoint ou campo novo = versão menor; correção sem mudar o contrato = patch.
  Enquanto estivermos em `0.x`, quebra de contrato sobe a versão menor.
- **Obsoleto antes de remover:** endpoint com `.MarcarObsoleto("GET /api/v1/novo")`; campo de request/response com
  `[property: Obsolete("Use novo_campo.")]`. Os dois saem como `deprecated` no OpenAPI. A remoção vem numa versão maior.
- **CHANGELOG:** toda mudança que o frontend precisa saber entra em [`CHANGELOG.md`](CHANGELOG.md), na seção
  "Não publicado". No release, a seção vira a versão.
- **Release:** suba a seção do CHANGELOG, crie a tag (`git tag v0.2.0 && git push origin v0.2.0`) e depois suba o
  `VersionPrefix` do `Directory.Build.props` para a próxima versão planejada.

Ver a comparação localmente (com Docker):

```bash
docker run --rm -v "$PWD:/specs" tufin/oasdiff:v1.34.1 changelog /specs/base.json /specs/artifacts/openapi/openapi.json
```

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
| Identidade | `identidade` | Login, refresh token com rotação, logout, minhas funerárias, convite, definição, recuperação e troca de senha, confirmação de e-mail, equipe da funerária com funcionários e consultores (AUT-01 a AUT-08) |
| Tenants | `tenants` | Auto-cadastro e cadastro pelo ADMIN, aprovação/suspensão, configurações da funerária (TEN-01 a TEN-03) |
| Clientes | `clientes` | Cadastro do cliente (conta + vínculo CLIENTE), meus dados, dependentes e busca pela equipe (Criar Cliente, Criar Dependente, CLI-01 a CLI-03) |
| Planos | `planos` | Gestão de planos, configurações em jsonb, coberturas com itens do catálogo e planos modelo copiados para cada funerária nova (PLA-01, PLA-02, PLA-03) |
| Notificacoes | `notificacoes` | Envio de e-mail por SMTP (Mailpit em desenvolvimento; provider de produção pendente, NOT-01); central de notificações e entrega em tempo real pelo SignalR em `/hubs/notificacoes` (NOT-02) |
| Contratos | `contratos` | Contratação pelo cliente (`POST /contratos`) e pela equipe (`POST /gestao/contratos`), com pessoas cobertas, valor e periodicidade da contratação; consulta pelo cliente e busca pela equipe; dependentes depois da contratação e cancelamento com motivo (Criar Contrato, CON-01 a CON-04) |
| Catalogo | `catalogo` | Categorias, itens, variações e imagens; catálogo modelo copiado para cada funerária nova; `ICatalogoConsultas` para os outros módulos (FUN-01) |
| Terceiros | `terceiros` | Cadastro de parceiros (frota, despachante/cartório, fornecedor) com convite e acesso pela Identidade; portal do terceiro com a lista única de atribuições, aceite e recusa (TER-01, TER-02) |
| Funeral | `funeral` | Preferências de funeral em vida (FUN-02), com crença religiosa cifrada; beneficiário (falecido, com elegibilidade do CON-04), funeral com etapas e histórico, aviso à família, acompanhamento e equipe responsável (Gerar Beneficiário, Registrar Funeral, FUN-06 a FUN-08) |
| Financeiro, Documentos, Translado, Relatorios | um por módulo | Projetos criados, ainda sem casos de uso (Documentos e Translado já têm as tabelas de status do ARQ-01) |

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

## Decisões técnicas

Os cuidados com dados pessoais (LGPD) estão em [docs/lgpd.md](docs/lgpd.md).

As decisões de negócio do grupo (certidão, Stripe Connect, translado, consultor, multa e juros) estão em
[docs/decisoes.md](docs/decisoes.md).

| Card | Decisão | Por quê |
|---|---|---|
| INF-05 | **Mediator** (source generator) para commands e queries | Registro gerado no build, sem reflexão em tempo de execução; behaviors de validação e log em um lugar só |
| INF-10 | **Mapeamento manual**, sem Mapster | Os DTOs são `record` com um método `De(entidade)`. Fica explícito o que sai na API (nunca vaza senha ou campo interno), o compilador acusa campo esquecido e não há configuração de mapeamento para testar |
| INF-04 | EF Core para escrita, **Dapper** para listagens e buscas | Consultas Dapper filtram `id_tenant` à mão e só leem o schema do próprio módulo |
| INF-08 | Serilog: console e arquivo JSON (`logs/`), Seq opcional em desenvolvimento | Correlation id por requisição (`X-Correlation-Id`); senha, tokens e CPF mascarados nos logs |
| INF-12 | Chamadas externas por `AddClienteExterno` (Microsoft.Extensions.Http.Resilience): retry exponencial com jitter, circuit breaker e timeout, com tentativas e aberturas de circuito no log | Uma API fora do ar não derruba a nossa nem prende threads; os valores ficam na seção `Resiliencia` |
| ARQ-01 | Máquina de estados própria (`MaquinaDeEstados`, `AgregadoComStatus`), sem a biblioteca Stateless | Uma tabela de transições basta; histórico e evento a cada mudança, concorrência otimista contra transições simultâneas |
| INF-18 | Armazenamento compatível com S3: **SeaweedFS** em desenvolvimento | A MinIO deixou de publicar imagens públicas |

## E-mails

Os módulos pedem o envio por `IEnvioEmail` informando o modelo (`ModelosEmail`) e os dados. O módulo Notificacoes monta o
e-mail com o nome, o logo e a cor da funerária e entrega pela fila `emails` do Hangfire, com retry.

- Modelos: `src/Modules/Notificacoes/Funeraria.Modules.Notificacoes.Application/Email/Modelos/` (`.html` e `.txt`).
  `{{campo}}` é escapado; campo sem valor faz a renderização falhar (e o teste do renderizador pega).
- Desenvolvimento: tudo cai no Mailpit (http://localhost:8025).
- Produção: configure `Email:Smtp` com o SMTP do provider. Resend: `smtp.resend.com`, porta 587, usuário `resend`,
  senha = API key. SendGrid: `smtp.sendgrid.net`, porta 587, usuário `apikey`, senha = API key. A senha vai em secret.

## Notificações dentro do sistema

Os módulos chamam `INotificacoes.NotificarAsync` (ou reagem a um evento de integração no próprio módulo Notificacoes).
Cada destinatário ganha uma linha em `notificacoes.notificacoes` e, se estiver conectado, recebe na hora.

- Central: `GET /api/v1/notificacoes?nao_lidas=true`, `PATCH /api/v1/notificacoes/{id}/lida`, `PATCH /api/v1/notificacoes/lidas`.
- Tempo real: conecte em `/hubs/notificacoes?access_token=<jwt>` e escute o evento `notificacao` (mesmo JSON da central).
- Já avisam: terceiro ao receber um serviço; gestores da funerária no aceite e na recusa (TER-02).
- Com mais de uma instância da API é preciso o backplane Redis do SignalR (INF-19).

## Arquivos

Os módulos gravam arquivos por `IArmazenamentoArquivos` (INF-18), sempre na pasta da funerária
(`tenants/{id_tenant}/...`). O tipo é conferido pelos primeiros bytes do arquivo, não pela extensão, e cada uso tem a sua
política em `PoliticaArquivo` (logo: PNG/JPEG/WEBP até 2 MB; documento: PDF/PNG/JPEG até 10 MB). SVG não é aceito.

- O bucket é privado. O navegador baixa por link temporário (`GerarLinkTemporarioAsync`) ou pela API.
- O logo da funerária é a exceção pública: `POST /api/v1/gestao/configuracoes/logo` (campo `arquivo`) grava e devolve
  o endereço `GET /api/v1/funerarias/{slug}/logo?v=...`, que aparece nas telas e nos e-mails. Configure `Api:UrlPublica`.
- Desenvolvimento: SeaweedFS do docker-compose. Produção: `Armazenamento:S3` com o bucket da AWS (deixe `ServiceUrl`
  vazio e use a credencial da instância ou `AccessKey`/`SecretKey` em secret) ou outro serviço compatível com S3.
  `UrlPublica` só é necessária quando o navegador não alcança o `ServiceUrl` (caso do Docker local).

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

Troque `Planos` pelo módulo. Contextos existentes: `IdentidadeDbContext`, `TenantsDbContext`, `PlanosDbContext`, `ClientesDbContext`, `ContratosDbContext`, `CatalogoDbContext`, `TerceirosDbContext`, `FuneralDbContext`.

## Criando um módulo novo ou um caso de uso

1. Caso de uso: pasta em `Application/Commands/<Nome>` ou `Application/Queries/<Nome>` com o command/query,
   o validator e o handler (`public sealed`, exigido pelo Mediator), e o endpoint no projeto Endpoints.
2. Erros esperados ficam numa classe `<Algo>Erros` no Domain, com as mensagens dos cards.
3. Módulo com banco: DbContext herdando `ModuleDbContext`, `AddModuleDbContext<T>()` no `AddXModule()` e a
   migration inicial.
4. Atualize o mapa de módulos acima e, se for um módulo novo, a lista em `tests/Funeraria.ArchitectureTests/Solucao.cs`.
