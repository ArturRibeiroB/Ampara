# Changelog da API

Mudanças no contrato da API (`openapi.json`) que o frontend precisa saber (INF-21). Formato baseado no
[Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/); versões em [SemVer](https://semver.org/lang/pt-BR/).

Seções de cada versão: **Adicionado**, **Alterado**, **Obsoleto** (com o que usar no lugar), **Removido**
(só em versão maior) e **Corrigido**. Quebra de contrato vem marcada com **[QUEBRA]**.

## [Não publicado]

### Adicionado

- Translado: registro por funeral com tipo e valor calculados (`/funerais/{id}/translados`), simulação
  (`/translados/simular`), tabela de preços por faixa de km e documentos exigidos por tipo (`/gestao/translados/...`),
  empresa de frota (`/translados/{id}/terceiro`, `/empresas-disponiveis`), status com histórico e portal da empresa
  (`/terceiros/translados`), com motorista e veículo. Status em tempo real pelo SignalR em `/hubs/translados`.
- Orçamento dos itens adicionais do funeral (`/funerais/{id}/orcamento`, `/orcamento/aprovar`), com registro de quem aprovou e
  fatura avulsa, que não suspende o plano nem gera multa e juros.
- Relatórios: painel da funerária (`/gestao/dashboard`), relatórios de faturas, contratos e funerais com exportação em CSV
  (`/gestao/relatorios/...`) e painel da plataforma (`/admin/dashboard`).

## [0.1.0] - 2026-10-30

Primeira versão do contrato.

### Adicionado

- Autenticação e conta: login, refresh token com rotação, logout, convite, definição/recuperação/troca de senha,
  confirmação de e-mail, minhas funerárias, aceite dos termos (LGPD) e auditoria das ações.
- Funerárias: auto-cadastro, cadastro pelo ADMIN, aprovação/suspensão e configurações (identidade visual,
  cobrança, lembretes e prazo de aceite dos terceiros).
- Equipe da funerária: funcionários e consultores.
- Clientes: cadastro, meus dados, dependentes, exportação dos dados (LGPD) e busca pela equipe
  (nome, CPF e status do contrato).
- Planos e catálogo: gestão, consulta pública, coberturas, variações, imagens e fornecedor do item.
- Contratos: contratação pelo cliente e pela equipe, consulta, elegibilidade (carência), inclusão e remoção de
  dependentes depois da contratação e cancelamento com motivo.
- Funeral: preferências de funeral em vida; registro do beneficiário (falecido) e do funeral, mudança de etapa
  com histórico e aviso à família, listagem da equipe, acompanhamento pelo titular e equipe responsável pelo atendimento.
- Locais (salas próprias, cemitérios, crematórios, igrejas), agenda e reserva para o funeral, sem conflito de
  horário nas salas próprias (com intervalo de preparação).
- Itens do funeral: inclusão e remoção, separação entre o que o plano cobre e o adicional (com a diferença das
  variações), baixa e devolução de estoque e importação das preferências registradas em vida.
- Pedidos aos fornecedores: geração por fornecedor a partir dos itens, aceite e recusa pelo portal, reenvio a outro
  fornecedor, preparo e entrega pelo fornecedor (`/terceiros/pedidos`) e cancelamento junto com o funeral.
- Terceiros: cadastro de parceiros, portal do terceiro com a lista de atribuições, aceite e recusa.
- Documentos do falecido: anexar (PDF, JPEG ou PNG), listar, baixar por link temporário e remover.
- Certidão de óbito: checklist com o prazo legal, solicitação (modo MANUAL ou SIMULADO por funerária), responsável
  (funcionário ou despachante), etapas pela equipe ou pelo portal (`/terceiros/certidoes`), registro da certidão emitida.
- Pagamentos: cadastro da funerária no Stripe (`/gestao/pagamentos/conta`), faturas do contrato (`/contratos/{id}/faturas`,
  `/faturas/{id}`) com link de pagamento e estimativa de multa e juros, baixa manual (`/gestao/faturas/{id}/baixa`).
- Notificações: central (`/api/v1/notificacoes`) e tempo real pelo SignalR em `/hubs/notificacoes`.
