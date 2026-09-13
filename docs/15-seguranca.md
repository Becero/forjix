# Segurança

## Controles implementados

### Autenticação e sessão

- access token JWT assinado com HMAC-SHA256 (`HS256`), validando emissor, audiência, assinatura e expiração;
- chave de assinatura obrigatória externamente e validada com mínimo de 32 bytes;
- duração padrão do access token de 15 minutos e clock skew padrão de 30 segundos;
- refresh token aleatório de 64 bytes; somente SHA-256 (`TokenHash`) é persistido;
- rotação de refresh tokens, associação por família e revogação da família quando é detectada reutilização;
- cookie `HttpOnly`, `SameSite=Strict`, limitado a `/api/auth`; `Secure` fora de Development;
- nome do cookie `forjix.refresh` em Development e `__Secure-forjix-refresh` nos demais ambientes;
- senhas processadas pelo `PasswordHasher<User>` do ASP.NET Core; senha de seed/provisionamento exige pelo menos 12 caracteres.

### Autorização

`RequirePermissionAttribute`, `PermissionPolicyProvider` e `PermissionAuthorizationHandler` implementam policies dinâmicas por código de permissão. `PermissionChecker` lê o banco do tenant e só considera usuário, papel e permissão ativos. A API é a fronteira de segurança; guardas e menus do Angular apenas melhoram a experiência visual.

### Isolamento e banco

- o JWT carrega `tenant_id` e `tenant_slug` e o contexto da requisição resolve a conexão desse tenant;
- cada operação abre o `ForjixTenantDbContext` com a conexão resolvida; tabelas operacionais não usam `TenantId` porque o isolamento é por banco;
- `SecretReference` é armazenado no Master, nunca a conexão do tenant;
- o segredo é resolvido por `ConfigurationSecretProvider` a partir de configuração externa;
- EF Core usa queries parametrizadas; não foi encontrada composição manual de SQL a partir de entrada HTTP;
- `RowVersion` protege agregados sujeitos a concorrência, incluindo produto/inventário, refresh token, venda, compra e sessão de caixa.

### API e transporte

- CORS aceita somente origens presentes em `Cors:AllowedOrigins` e permite credenciais;
- `UseHttpsRedirection` está habilitado;
- endpoints de autenticação têm rate limit fixo por IP: padrão de 10 requisições por minuto, fila zero;
- validação por DataAnnotations e validações de caso de uso;
- exceções são convertidas em `ProblemDetails`, sem retorno intencional de stack trace;
- correlation ID é aceito/gerado e incluído na resposta e nos logs;
- Serilog produz logs estruturados em console;
- ações relevantes são persistidas em `AuditLogs`.

### Arquivos

O upload de logotipo tem limite de 2 MB, aceita somente PNG/JPEG/WebP e usa nome interno derivado do tenant, não o nome enviado. O serviço valida caminho final contra a raiz configurada para impedir escape de diretório.

## Segredos e configuração sensível

As conexões Master/tenant, `Jwt:SigningKey` e senhas de seed não estão definidas com valores no código versionado. Em Development, o migrador pode carregar .NET User Secrets. Em ambientes publicados, esses valores devem ser variáveis/secret store. Exemplos da documentação usam marcadores sanitizados.

## Pontos de atenção constatados

- `User` possui `FailedAccessAttempts` e `LockedUntil`; o login respeita `LockedUntil`, mas o código atual não incrementa falhas nem cria um bloqueio automático.
- O mínimo do `LoginRequest` HTTP é 8 caracteres, enquanto criação/seed exige 12. Isso não expõe a senha, mas é uma divergência de validação.
- O rate limit está aplicado ao controller de autenticação, não globalmente aos demais endpoints.
- Não foi encontrada persistência/compartilhamento explícito das chaves de ASP.NET Data Protection. Em múltiplas instâncias ou após reinício, isso pode afetar cookies protegidos.
- `LocalTenantAssetStore` usa filesystem local; no Render sem disco persistente, arquivos podem desaparecer após deploy/restart.
- `/api/health` registra somente `AddHealthChecks()` sem um check de SQL; logo, não comprova conectividade com Master ou tenants.
- Não há tarefa de limpeza periódica de refresh tokens expirados/revogados.
- Algumas referências históricas são apenas IDs, sem foreign key física: usuário em auditoria, substituição do refresh token, usuários de cancelamento/caixa e venda associada ao movimento de caixa.
- A trava de sessão de caixa aberta é transacional no serviço, mas não há constraint filtrada no banco que imponha uma única sessão aberta.
- Os recursos externos de pagamento, TEF e fiscal estão fora do escopo atual; não há controles específicos para eles.

Esses itens são observações do código atual, não confirmação de vulnerabilidade explorável nem requisitos novos.
