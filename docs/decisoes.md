# Decisões de negócio (DEC-04, DEC-09 a DEC-12)

Respostas do grupo em 23/10/2026. Onde o grupo deixou a escolha com o desenvolvimento ("deixo em suas mãos"),
a decisão vem com o motivo. Os itens marcados com **⚠ validar** não foram respondidos e entraram como proposta:
o sistema nasce com eles configuráveis para a regra poder mudar sem mexer no código.

---

## [DEC-04] Integração para emissão de certidão de óbito

**Status:** ✅ Decidido em 23/10/2026 (grupo delegou a escolha)

**Decisão**

- **MVP com fluxo assistido**, sem API externa. O sistema reúne os dados e os documentos (Declaração de Óbito,
  documentos do falecido e do declarante), mostra o checklist do que falta e acompanha o processo. Um funcionário
  da funerária ou um despachante/cartório parceiro (TERCEIRO) registra o óbito no **e-Óbito** ou no cartório,
  informa o protocolo e anexa a certidão emitida.
- **Interface `ICertidaoObitoGateway`** (CER-02) com duas implementações: *manual* (padrão) e *simulada*
  (para testes e demonstração). Uma integração real com o e-Óbito entra depois, sem mudar o resto do sistema.
- **Segunda via** fora do MVP. O cidadão usa o Portal dos Cartórios (registrocivil.org.br).

**Motivo:** não há API pública do e-Óbito para sistemas de terceiros, o credenciamento é de cada funerária e o
registro e a primeira via são gratuitos (Lei 9.534/1997). O fluxo assistido entrega o valor (organização,
checklist, acompanhamento) sem depender de um credenciamento que o projeto não controla.

**Fica para depois:** contato com ON-RCPN / Arpen sobre integração com o e-Óbito e proposta de intermediário
para segunda via.

**Cards afetados:** CER-02, CER-03, CER-04.

---

## [DEC-09] Recebimento dos pagamentos e cobrança das funerárias

**Status:** ✅ Decidido em 23/10/2026

**Decisão do grupo**

- O dinheiro dos clientes vai **direto para a conta de cada funerária**.
- A plataforma cobra das funerárias **taxa por pagamento e mensalidade** (os dois).
- No cadastro, a funerária envia **CNPJ, conta bancária e documentos dos sócios**.

**Como fica no sistema** (detalhes delegados ao desenvolvimento)

- **Stripe Connect.** Cada funerária é uma conta conectada. O cadastro (CNPJ, conta bancária e documentos dos
  sócios) é feito na página de onboarding hospedada pelo Stripe, que faz a verificação (KYC). A plataforma não
  guarda documento nem dado bancário.
- **Cobranças diretas na conta da funerária.** Cliente, assinatura e faturas ficam na conta conectada; a
  funerária aparece como vendedora e paga as tarifas do Stripe sobre os próprios recebimentos.
- **Taxa da plataforma:** percentual retido em cada pagamento (`application_fee_percent` da assinatura).
  Valor inicial: **2%**, configurável pelo ADMIN e por funerária.
- **Mensalidade da plataforma:** assinatura do Stripe Billing na conta da própria plataforma, com a funerária
  como cliente. Valor inicial: **R$ 149,00/mês**, configurável pelo ADMIN.
- **Formas de pagamento das mensalidades dos clientes:** cartão e boleto. Pix fica para cobranças avulsas
  (itens adicionais, faturas em atraso) até o Pix recorrente estar disponível nas contas conectadas.

**Requisito:** a conta Stripe da plataforma precisa ser **brasileira (CNPJ da plataforma)**. Segundo a documentação
do Stripe, plataformas sediadas fora do Brasil não podem recolher taxa sobre pagamentos de contas conectadas
brasileiras.

**Valores iniciais (taxa e mensalidade) ⚠ validar:** foram escolhidos para o sistema funcionar e podem ser
trocados na configuração sem deploy.

**Cards afetados:** TEN-02, COB-01, COB-05, COB-06, REL-03.

---

## [DEC-10] API de distância para o translado

**Status:** ✅ Decidido em 23/10/2026

**Decisão:** **OpenStreetMap, pela API do OpenRouteService** (rotas e geocodificação).

- O plano gratuito atende o início: 2.000 rotas por dia (40 por minuto).
- A distância calculada fica gravada no translado; a mesma origem e destino não é consultada duas vezes.
- **Plano B embutido:** se o endereço não for encontrado ou a API falhar, o funcionário informa os km.
  O translado nunca fica travado pela API.
- Atrás da interface `IDistanciaGateway`. Se o volume crescer, o OpenRouteService pode ser hospedado pela
  própria plataforma (imagem Docker com o mapa do Brasil), sem mudar o código.

**Cards afetados:** TRA-02.

---

## [DEC-11] Perfil "consultor" e aprovação de funerárias

**Status:** ✅ Decidido em 23/10/2026

**Decisão do grupo:** o consultor é um **vendedor e acolhedor da funerária**. Quando a família não consegue
fazer o serviço sozinha por causa da dor da perda, ele ajuda e guia as escolhas.

**Como fica no sistema**

- **Novo perfil CONSULTOR**, dentro de uma funerária (como FUNERARIA e FUNCIONARIO), cadastrado pelo gestor (AUT-07).
- Pode: cadastrar cliente e dependentes em nome da família, fazer a contratação, montar a personalização do
  funeral e acompanhar funeral, translado e certidão das famílias que atende.
- Não pode: configurações, planos, catálogo, equipe, terceiros, baixa manual de pagamento e relatórios financeiros.
- Contratos e personalizações feitos por ele guardam o consultor responsável (para acompanhamento e comissão).
- O consultor **não cadastra funerárias**. A funerária entra por auto-cadastro ou pelo ADMIN; a origem
  "Consultor" do cadastro de funerárias deixa de existir.

**Aprovação de funerárias** (grupo delegou)

- A funerária nasce **PENDENTE** e passa a **ATIVA sozinha** quando o Stripe libera a conta (verificação de CNPJ
  e sócios concluída) e o gestor aceita os termos da plataforma. O ADMIN continua podendo ativar e suspender.
- **Motivo:** a verificação do Stripe já valida CNPJ e sócios; uma aprovação manual a mais só atrasaria a entrada.

**Cards afetados:** DEC-06 (matriz), AUT-04, AUT-07, AUT-08, TEN-02, Criar Contrato, FUN-03.

---

## [DEC-12] Multa, juros, desconto e regras de suspensão

**Status:** ✅ Valores decididos em 23/10/2026; regras de suspensão ⚠ validar

**Valores** (grupo delegou, com o limite de 2% de multa)

| Parâmetro | Valor padrão | Observação |
|---|---|---|
| Multa por atraso | **2%** | Limite do CDC (art. 52, § 1º); o sistema não aceita mais que isso |
| Juros de mora | **1% ao mês**, proporcional aos dias | Prática usual em mensalidades; confirmar com o jurídico |
| Desconto no plano anual | **10%** | Já é o desconto dos planos modelo (anual = 12 × mensal − 10%) |

São padrões de cada funerária nova; o gestor pode alterar em Configurações (TEN-03).

**Regras de suspensão ⚠ validar** (não respondidas; proposta que protege o cliente que pagou em dia)

- **Tolerância em dias úteis:** 1 dia útil depois do vencimento, sem contar fins de semana e feriados nacionais.
- **Pagamento em processamento** (boleto pago aguardando compensação) **nunca suspende** o plano.
- **Óbito durante a suspensão:** a família pode quitar as pendências (com multa e juros) no atendimento e a
  cobertura volta na hora. Sem quitação, o funeral é atendido como particular.
- **Reativação:** quitando em até **90 dias** de suspensão, a cobertura volta na hora, **sem nova carência**.
  Depois de 90 dias o contrato é cancelado (COB-03) e uma nova contratação cumpre carência.

**Cards afetados:** TEN-03, COB-03, COB-09, CON-04.
