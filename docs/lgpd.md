# LGPD (SEG-01)

O sistema trata CPF, dados de familiares, documentos de óbito e, nas preferências de funeral, possivelmente crença
religiosa, que é dado sensível (LGPD, art. 5º, II). Este documento registra o que já está no código e a política de
retenção proposta, para o grupo revisar.

## O que já está no código

| Cuidado | Como |
|---|---|
| Dados pessoais cifrados em repouso | CPF de clientes e dependentes gravado cifrado (`ICriptografiaDados`, Data Protection do ASP.NET, AES-256). Busca e unicidade usam o `cpf_hash` (HMAC-SHA256). CPFs gravados antes disso são cifrados na subida da API |
| Chaves fora do código | Chaves do Data Protection na tabela `identidade.chaves_protecao_dados`; chave do hash em `Seguranca:ChaveHash` (secret). Em produção, proteger as chaves com certificado ou cofre |
| Arquivos cifrados | `Armazenamento:S3:CifrarNoServidor` grava os arquivos com SSE-S3 (AES-256) na AWS |
| Controle de acesso | Policies por perfil (AUT-04) e isolamento por funerária (TEN-01) |
| Auditoria | Operações sensíveis registradas com usuário, IP e valores antes e depois (AUT-09) |
| Aceite de termos | Versão e data do aceite dos termos de uso e da política de privacidade, com IP. Versão nova publicada (`Lgpd:VersaoTermosDeUso`) deixa o aceite pendente |
| CPF mascarado | Listagens mostram `***.456.789-**`; o detalhe mostra completo para quem atende |
| Logs sem dados pessoais | O enricher `MascaraDadosSensiveis` (INF-08) mascara CPF, e-mail, telefone, senha e token nos logs |
| Exportação pelo titular | `GET /api/v1/clientes/me/exportacao` devolve cadastro, dependentes, contratos e aceites (art. 18) |
| Terceiros veem o mínimo | O resumo das atribuições tem um tipo por serviço, sem CPF, contrato ou valores (TER-02) |

## Política de retenção proposta ⚠ validar com o grupo

| Dado | Por quanto tempo | Depois |
|---|---|---|
| Contratos, faturas e pagamentos | 5 anos após o fim do contrato (prazo fiscal e prescricional) | Anonimizar o titular e os dependentes |
| Dados do falecido, certidão e documentos do óbito | 5 anos após o funeral | Excluir os arquivos; manter só o registro anonimizado do atendimento |
| Cadastro de cliente sem contrato | 2 anos sem acesso | Excluir a conta e o cadastro |
| Preferências de funeral em vida | Enquanto houver contrato ativo | Excluir junto com o contrato |
| Auditoria | 5 anos | Excluir |
| Logs da aplicação | 14 dias (configurado no Serilog) | Rotação automática |

Anonimizar é trocar nome, CPF, e-mail, telefone e endereço por valores sem relação com a pessoa, mantendo os números
para relatórios.

## Ainda falta

- **Job de retenção e anonimização** que aplique a política acima, depois que o grupo validar os prazos.
- **CPF de funcionários e documento de terceiros** cifrados como o de clientes. Os mesmos `ICriptografiaDados` e
  `cpf_hash` servem.
- **Documentos do falecido** com acesso restrito e link temporário, quando o CER-01 existir.
- **Revisar com o grupo** quais dados são realmente necessários em cada cadastro.
- O aceite de termos no cadastro do cliente é opcional na API (`aceite_termos`) para não quebrar o frontend atual.
  O frontend deve mandar o aceite, e pode passar a ser obrigatório.
