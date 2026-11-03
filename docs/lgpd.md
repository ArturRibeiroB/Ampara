# LGPD (SEG-01)

O sistema trata CPF, dados de familiares, documentos de óbito e, nas preferências de funeral, possivelmente crença
religiosa, que é dado sensível (LGPD, art. 5º, II). Este documento registra o que já está no código e a política de
retenção proposta, para o grupo revisar.

## O que já está no código

| Cuidado | Como |
|---|---|
| Dados pessoais cifrados em repouso | CPF de clientes, dependentes e funcionários e CPF/CNPJ de terceiros gravados cifrados (`ICriptografiaDados`, Data Protection do ASP.NET, AES-256). Busca e unicidade usam o hash (`cpf_hash`, `cpf_cnpj_hash`, HMAC-SHA256). Valores gravados antes disso são cifrados na subida da API (`CifrarDadosLegados`) |
| Chaves fora do código | Chaves do Data Protection na tabela `identidade.chaves_protecao_dados`; chave do hash em `Seguranca:ChaveHash` (secret). Em produção, proteger as chaves com certificado ou cofre |
| Arquivos cifrados | `Armazenamento:S3:CifrarNoServidor` grava os arquivos com SSE-S3 (AES-256) na AWS |
| Documentos do falecido | Bucket privado; download só por link temporário, para a equipe interna e o titular (CER-01) |
| Controle de acesso | Policies por perfil (AUT-04) e isolamento por funerária (TEN-01) |
| Auditoria | Operações sensíveis registradas com usuário, IP e valores antes e depois (AUT-09) |
| Aceite de termos | Versão e data do aceite dos termos de uso e da política de privacidade, com IP. Versão nova publicada (`Lgpd:VersaoTermosDeUso`) deixa o aceite pendente. `Lgpd:AceiteTermosObrigatorio` exige o aceite no auto-cadastro do cliente |
| CPF mascarado | Listagens de clientes mostram `***.456.789-**`; o detalhe mostra completo para quem atende |
| Logs sem dados pessoais | O enricher `MascaraDadosSensiveis` (INF-08) mascara CPF, e-mail, telefone, senha e token nos logs |
| Exportação pelo titular | `GET /api/v1/clientes/me/exportacao` devolve cadastro, dependentes, contratos e aceites (art. 18) |
| Terceiros veem o mínimo | O resumo das atribuições tem um tipo por serviço, sem CPF, contrato ou valores (TER-02) |
| Retenção e anonimização | Job `lgpd-retencao`, **desligado por padrão** e, ligado, **em simulação** até alguém desligar a simulação (abaixo) |

## Política de retenção proposta ⚠ validar com o grupo

| Dado | Por quanto tempo | Depois |
|---|---|---|
| Contratos, faturas e pagamentos | 5 anos após o fim do contrato (prazo fiscal e prescricional) | Anonimizar o titular e os dependentes |
| Dados do falecido, certidão e documentos do óbito | 5 anos após o funeral | Excluir os arquivos; manter só o registro anonimizado do atendimento |
| Cadastro de cliente sem contrato | 2 anos sem acesso | Anonimizar o cadastro e a conta (ver abaixo por que não excluir) |
| Preferências de funeral em vida | Enquanto houver contrato ativo | Excluir 30 dias depois do fim do último contrato |
| Auditoria | 5 anos | Excluir |
| Logs da aplicação | 14 dias (configurado no Serilog) | Rotação automática |

Anonimizar é trocar nome, CPF, e-mail, telefone e endereço por valores sem relação com a pessoa, mantendo os números
para relatórios.

## Como o job de retenção funciona

Roda todo dia às 04:00 UTC (fila `manutencao` do Hangfire). Cada módulo aplica a parte dos dados dele
(`IPoliticaRetencao`), e a falha de uma regra não impede as outras. O log só traz contagens, nunca dados pessoais.

### Ligando com cuidado

Tudo fica na seção `Lgpd:Retencao` (prazos acima são os padrões):

```json
"Lgpd": {
  "Retencao": {
    "Habilitada": false,
    "Simulacao": true,
    "AnosAposFimDoContrato": 5,
    "AnosAposFuneral": 5,
    "AnosClienteSemContrato": 2,
    "AnosAuditoria": 5,
    "DiasPreferenciasAposFimDoContrato": 30,
    "LimitePorExecucao": 500
  }
}
```

1. **Validar os prazos com o grupo** (e com o jurídico, se possível).
2. Fazer **backup do banco**: anonimização e exclusão não têm volta.
3. Ligar `Habilitada` com `Simulacao: true` e conferir no log quantos registros cada regra pegaria
   (`Retenção LGPD, clientes: N registros seriam afetados (simulação)`).
4. Só então desligar `Simulacao`. `LimitePorExecucao` evita uma transação gigante na primeira vez.

### O que cada regra faz

| Regra | Módulo | Quando | O que faz |
|---|---|---|---|
| `clientes` | Clientes | Todos os contratos cancelados e o último terminou há mais de `AnosAposFimDoContrato`; ou nunca teve contrato, cadastrado há mais de `AnosClienteSemContrato` e sem entrar na conta nesse período | Anonimiza titular e dependentes (inclusive removidos). Pelo outbox: a Identidade desativa o vínculo CLIENTE e encerra as sessões na funerária; o Funeral exclui as preferências |
| `preferencias-de-funeral` | Funeral | Todos os contratos do titular cancelados há mais de `DiasPreferenciasAposFimDoContrato` | Exclui as preferências do titular e dos dependentes |
| `obitos` | Funeral | `AnosAposFuneral` depois do último funeral, com todos concluídos ou cancelados (sem funeral: a partir do falecimento) | Anonimiza beneficiário, responsável, funerais e pedidos. Pelo outbox: Documentos exclui os arquivos do óbito (documentos e certidão) e anonimiza a solicitação da certidão; Translado tira nomes, contatos, motorista e a rua dos endereços; Terceiros reescreve os resumos que os parceiros veem no portal |
| `auditoria` | Identidade | Linhas com mais de `AnosAuditoria` | Exclui |

**A conta na plataforma** é única por pessoa (DEC-02). Ela só é anonimizada quando todos os vínculos dela são de
cliente e nenhum está ativo, e nunca a do ADMIN. Quem também é cliente de outra funerária, funcionário ou terceiro
continua com a conta. O aceite dos termos fica como prova do consentimento, sem o IP.

**Por que anonimizar em vez de excluir** o cadastro sem contrato: o id do cliente aparece em outros módulos
(notificações, auditoria, histórico). Excluir deixaria referências soltas; anonimizado, o registro deixa de ser dado
pessoal (LGPD, art. 12) e as referências continuam válidas. Se o grupo preferir exclusão física, dá para trocar só a
regra `clientes`.

**Último acesso:** a conta grava `ultimo_acesso_em` a cada login e renovação de sessão. Contas que nunca entraram
contam a partir da criação.

## Testes

- `tests/Funeraria.BuildingBlocks.UnitTests/RetencaoDadosJobTests.cs`: job desligado não faz nada, ligado roda em
  simulação, regra com erro não para as outras.
- `tests/Modules/Funeraria.Modules.Clientes.UnitTests/DominioTests.cs`: anonimização do titular e do dependente.
- `tests/Modules/Funeraria.Modules.Clientes.IntegrationTests/RetencaoLgpdTests.cs`: simulação não altera; contrato
  encerrado há mais de 5 anos anonimiza e tira o acesso; contrato vigente, encerrado há pouco ou conta usada
  recentemente ficam.
- `tests/Modules/Funeraria.Modules.Funeral.IntegrationTests/RetencaoLgpdTests.cs`: preferências saem só sem contrato;
  falecido anonimizado e arquivo do óbito excluído do armazenamento; funeral recente ou aberto segura o falecido.
- `CpfFuncionarioCifradoTests` (Identidade) e `DocumentoCifradoTests` (Terceiros): documento cifrado no banco, unicidade
  pelo hash e cifragem dos valores antigos.

## Ainda falta

- **Validar com o grupo** os prazos e o que fazer com o cadastro sem contrato (anonimizar ou excluir).
- **Revisar com o grupo** quais dados são realmente necessários em cada cadastro.
- **Ligar `Lgpd:AceiteTermosObrigatorio`** quando o frontend mandar `aceite_termos: true` no cadastro do cliente. Hoje o
  formulário de cadastro não manda o campo; com a opção ligada, o cadastro sem aceite volta 400.
- **Cópias fora do banco:** valores antigos dentro do JSON da auditoria (`valores_anteriores`) e mensagens já enviadas
  (e-mails, notificações) não são reescritos pela anonimização. A auditoria sai pelo prazo dela.
