# Roadmap técnico observado

Este arquivo não cria requisitos de negócio. Ele reúne lacunas, riscos e limites demonstráveis no repositório. Não foram encontrados marcadores `TODO` ou `FIXME` no código de produção; as prioridades abaixo são recomendações técnicas derivadas da implementação atual.

## Financeiro V1 implementado

Categorias, contas a pagar/receber, parcelas, baixas/estornos, integração venda/compra/caixa e dashboard estão implementados. O fluxo não reprocessa documentos históricos e não inclui conciliação bancária ou juros automáticos; consulte [Financeiro](12-financeiro.md). A validação utilizou SQL Server LocalDB com isolamento físico entre dois tenants.

## Cobertura funcional e integrações

1. **Integrações ainda sem adapters reais.** `Forjix.Integrations` contém apenas o marcador de assembly. O próprio `AGENTS.md` mantém fiscal, adquirência/TEF, aplicativo móvel e multi-loja fora do escopo.
2. **Administração da plataforma Master não exposta.** Há entidades/tabelas para tenants, planos, assinaturas, features, configurações e bancos, mas não controllers nem telas para gerenciá-las; o provisionamento passa pelo migrador.
3. **Feature flags sem aplicação funcional observada.** Features habilitadas são incluídas em `SessionContext`, mas as rotas/serviços atuais são governados por permissions; não foi encontrado guard/handler que bloqueie uma operação por feature.
4. **Limite de usuários não aplicado.** `Plan.UserLimit` é persistido, porém a criação de usuários não consulta esse limite.

## Operação e confiabilidade

1. **Health check de dependências.** Adicionar checks reais exigiria uma decisão futura; hoje `/api/health` não testa SQL.
2. **Persistência de chaves e assets.** O deploy documentado não evidencia armazenamento compartilhado para Data Protection e o logotipo usa filesystem local. Isso merece validação antes de múltiplas instâncias ou deploys efêmeros.
3. **Observabilidade.** Há logs estruturados, correlation ID e auditoria, mas não foram encontrados tracing distribuído, métricas da aplicação ou alertas versionados.
4. **Manutenção de tokens.** Não há job para remover refresh tokens expirados/revogados.
5. **Concorrência do caixa.** A aplicação usa transação serializável, sem constraint física para uma única sessão aberta.

## Segurança

1. **Bloqueio por tentativas.** Completar ou remover o modelo parcial de lockout: os campos existem e `LockedUntil` é consultado, mas falhas não são contabilizadas pelo login.
2. **Rate limiting.** A proteção atual cobre somente autenticação. A necessidade de limites nos endpoints de negócio deve ser avaliada conforme carga e ameaça reais.
3. **Referências históricas sem FK.** Documentar a política de retenção ou decidir se alguns IDs históricos devem ganhar integridade referencial.
4. **Gestão de segredos.** Manter validação operacional de que Master, tenants, JWT e senhas vêm exclusivamente do secret store/ambiente em cada destino.

## API e frontend

1. **Contrato de API.** OpenAPI só é servido em Development; não há especificação versionada nem cliente Angular gerado, aumentando risco de divergência manual.
2. **Cobertura do Angular.** Há 18 casos Jasmine: app, máscaras brasileiras, serviço HTTP financeiro e dashboard financeiro. Guards, interceptors e demais features ainda requerem ampliação de cobertura.
3. **Exportação de relatórios.** O “Excel” atual é SpreadsheetML 2003 com extensão `.xls`, e o PDF é um resumo textual simples. Evoluir formato/layout seria trabalho futuro, sem assumir novo conteúdo comercial.
4. **Responsividade móvel.** O frontend é web Angular e há estilos responsivos, mas não existe projeto híbrido/mobile no repositório.

## Banco e testes

1. **Testes SQL dependentes de ambiente.** Vinte e nove casos descobertos usam `SqlFact`/`SqlTheory` e são ignorados sem `FORJIX_RUN_SQL_TESTS=true` e bancos CI provisionados.
2. **Constraints adicionais.** Regras como caixa único aberto são garantidas pelo serviço e pela transação, não por constraint. Qualquer reforço físico deve ser compatível com SQL Server e com a regra vigente.
3. **Teste de migrations em deploy.** O migrador registra cada execução, mas a pipeline deve continuar garantindo que ele rode antes da API; a API deliberadamente não migra schemas.

## Ordem técnica sugerida

Sem atribuir prazo ou escopo comercial, a ordem de redução de risco seria: saúde/persistência em produção; lockout e lifecycle de tokens; testes Angular e SQL automatizados; contrato OpenAPI; depois adapters e módulos explicitamente aprovados. Cada item requer decisão própria antes de alterar produção.

## Orçamentos V1 — atualização de 26/09/2026

O módulo comercial de orçamentos está implementado. A migration tenant `20260926144839_AddQuotesV1` acrescenta `Quotes`, `QuoteItems` e `QuoteSequences`, sete permissões `quotes.*` e os endpoints `/api/quotes`. Criação não movimenta estoque nem financeiro; conversão aprovada reutiliza a venda em transação única. Consulte [Orçamentos V1](13-orcamentos.md) para esquema, contratos, status, testes e limitações.

## Estoque Avançado V1 — atualização de 26/09/2026

Inventário físico implementado como `Stocktake`, preservando `Inventories` como saldo único. Migration `20260926150829_AddAdvancedInventoryV1` adiciona `Stocktakes`, `StocktakeItems`, `StocktakeSequences`, campos opcionais de motivo/observação em movimentos e sete permissões. Finalização incremental e transacional, com bloqueio de contagens desatualizadas. Alertas, histórico integrado, ajustes e relatórios disponíveis. Consulte [Estoque Avançado / Inventário V1](14-estoque-avancado.md) para contratos, regras, testes e limitações.
