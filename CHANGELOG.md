# Changelog da API

Mudanças no contrato da API (`openapi.json`) que o frontend precisa saber (INF-21). Formato baseado no
[Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/); versões em [SemVer](https://semver.org/lang/pt-BR/).

Seções de cada versão: **Adicionado**, **Alterado**, **Obsoleto** (com o que usar no lugar), **Removido**
(só em versão maior) e **Corrigido**. Quebra de contrato vem marcada com **[QUEBRA]**.

## [Não publicado]

Primeira versão do contrato. Será publicada como `v0.1.0`.

### Adicionado

- Autenticação e conta: login, refresh token com rotação, logout, convite, definição/recuperação/troca de senha,
  confirmação de e-mail, minhas funerárias, aceite dos termos (LGPD) e auditoria das ações.
- Funerárias: auto-cadastro, cadastro pelo ADMIN, aprovação/suspensão e configurações (identidade visual,
  cobrança, lembretes e prazo de aceite dos terceiros).
- Equipe da funerária: funcionários e consultores.
- Clientes: cadastro, meus dados, dependentes, exportação dos dados (LGPD) e busca pela equipe
  (nome, CPF e status do contrato).
- Planos e catálogo: gestão, consulta pública, coberturas, variações, imagens e fornecedor do item.
- Contratos: contratação pelo cliente e pela equipe, consulta e elegibilidade (carência).
- Funeral: preferências de funeral em vida.
- Terceiros: cadastro de parceiros, portal do terceiro com a lista de atribuições, aceite e recusa.
- Notificações: central (`/api/v1/notificacoes`) e tempo real pelo SignalR em `/hubs/notificacoes`.
