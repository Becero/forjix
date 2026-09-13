# Homologação no Render com Azure SQL

Este documento prepara a V1 para um único Render Web Service. A imagem Docker compila o Angular e o publica no `wwwroot` da API ASP.NET Core. Frontend, API e cookie de renovação permanecem no mesmo domínio. O deploy não executa migrations automaticamente.

## Arquitetura

- Render Web Service executando uma imagem Docker Linux;
- ASP.NET Core em `Production`, ouvindo `0.0.0.0:$PORT`;
- Angular servido como arquivos estáticos pela API;
- SQL Server externo, preferencialmente Azure SQL;
- `ForjixMaster` e um banco separado por tenant continuam usando o provider SQL Server.

## 1. Criar e preparar o Azure SQL

1. Crie um Azure SQL logical server e um banco vazio chamado `ForjixMaster`.
2. Crie também o banco do tenant, por exemplo `Forjix_EmpresaDemo`.
3. Configure o firewall do Azure SQL para aceitar o IP público usado na execução controlada do migrator. Antes de liberar o Render, consulte os intervalos de saída da região escolhida e autorize apenas os necessários.
4. Use um usuário com acesso aos dois bancos durante a homologação. Em uma etapa posterior, separe a identidade de migration da identidade de runtime e aplique privilégio mínimo.
5. Use strings com criptografia e validação de certificado:

```text
Server=tcp:<servidor>.database.windows.net,1433;Initial Catalog=ForjixMaster;User ID=<usuario>;Password=<senha>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

Para o tenant, altere somente `Initial Catalog` para `Forjix_EmpresaDemo`. Não salve essas strings em arquivos versionados ou logs.

## 2. Variáveis do Web Service

Obrigatórias:

| Variável | Uso |
| --- | --- |
| `ConnectionStrings__ForjixMaster` | conexão com o catálogo master |
| `TenantDatabases__empresa-demo` | conexão do tenant cujo `SecretReference` é `TenantDatabases:empresa-demo` |
| `Jwt__SigningKey` | segredo aleatório com pelo menos 32 bytes; mantenha estável entre deploys |
| `Jwt__Issuer` | emissor esperado, por exemplo `Forjix.Api.Homologation` |
| `Jwt__Audience` | audiência esperada, por exemplo `Forjix.Web.Homologation` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `DOTNET_ENVIRONMENT` | `Production` |

Opcionais:

| Variável | Padrão | Uso |
| --- | --- | --- |
| `Jwt__AccessTokenMinutes` | `15` | duração do access token, entre 1 e 60 minutos |
| `Jwt__ClockSkewSeconds` | `30` | tolerância de relógio, entre 0 e 300 segundos |
| `RateLimiting__AuthenticationPermitLimit` | `10` | tentativas de autenticação por IP/minuto |
| `Cors__AllowedOrigins__0` | vazio | somente se outro domínio consumir a API; não é necessário no modo same-origin |
| `PORT` | fornecida pelo Render | porta HTTP; a aplicação faz bind em todas as interfaces |

`RENDER=true` é fornecida pela plataforma e ativa o processamento limitado de `X-Forwarded-For` e `X-Forwarded-Proto`. Não defina manualmente essa variável fora do Render.

## 3. Executar o migrator

O migrator lê variáveis de ambiente em `Production`, não carrega User Secrets e não é iniciado pela API. Execute-o em uma estação segura com .NET 10 e acesso liberado no firewall.

### Primeira criação controlada da Empresa Demo (PowerShell)

```powershell
$env:DOTNET_ENVIRONMENT = 'Production'
$env:ConnectionStrings__ForjixMaster = '<connection-string-ForjixMaster>'
Set-Item -Path 'Env:TenantDatabases__empresa-demo' -Value '<connection-string-Forjix_EmpresaDemo>'
$env:Forjix__HomologationProvisioning__Enabled = 'true'
$env:Forjix__HomologationProvisioning__Confirmation = 'PROVISION_FORJIX_HOMOLOGATION'
$env:Forjix__HomologationProvisioning__TenantName = 'Empresa Demo'
$env:Forjix__HomologationProvisioning__TenantSlug = 'empresa-demo'
$env:Forjix__HomologationProvisioning__DatabaseName = 'Forjix_EmpresaDemo'
$env:Forjix__HomologationProvisioning__TenantSecretReference = 'TenantDatabases:empresa-demo'
$env:Forjix__HomologationProvisioning__AdminEmail = '<email-administrador>'
$env:Forjix__HomologationProvisioning__AdminName = '<nome-administrador>'
$env:Forjix__HomologationProvisioning__AdminPassword = '<senha-forte-temporaria>'
$env:Forjix__HomologationProvisioning__SeedCommercialDemo = 'true'
dotnet run --project tools/Forjix.DatabaseMigrator/Forjix.DatabaseMigrator.csproj --configuration Release
Remove-Item Env:Forjix__HomologationProvisioning__Enabled
Remove-Item Env:Forjix__HomologationProvisioning__Confirmation
Remove-Item Env:Forjix__HomologationProvisioning__AdminPassword
```

Em variáveis de ambiente, `TenantDatabases:empresa-demo` é representada por `TenantDatabases__empresa-demo`; os dois sublinhados substituem os dois-pontos. O processo é idempotente, mas redefine a senha administrativa enquanto estiver habilitado. Remova as variáveis de provisionamento imediatamente após a execução. O seed comercial só roda quando `SeedCommercialDemo=true` e a confirmação exata está presente; `DevelopmentSeed` continua bloqueado em `Production`.

### Migrations posteriores, sem provisionamento

```powershell
$env:DOTNET_ENVIRONMENT = 'Production'
$env:ConnectionStrings__ForjixMaster = '<connection-string-ForjixMaster>'
Set-Item -Path 'Env:TenantDatabases__empresa-demo' -Value '<connection-string-Forjix_EmpresaDemo>'
dotnet run --project tools/Forjix.DatabaseMigrator/Forjix.DatabaseMigrator.csproj --configuration Release
```

O migrator atualiza primeiro `ForjixMaster` e depois todos os tenants ativos. Toda `SecretReference` registrada no master precisa ter sua variável correspondente disponível.

## 4. Validar a imagem localmente

Com bancos já migrados e acessíveis:

```powershell
docker build --tag forjix:homolog .
docker run --rm --name forjix-homolog-local -p 10000:10000 `
  -e PORT=10000 `
  -e ASPNETCORE_ENVIRONMENT=Production `
  -e DOTNET_ENVIRONMENT=Production `
  -e ConnectionStrings__ForjixMaster='<connection-string-ForjixMaster>' `
  -e 'TenantDatabases__empresa-demo=<connection-string-Forjix_EmpresaDemo>' `
  -e Jwt__SigningKey='<segredo-aleatorio-com-32-ou-mais-bytes>' `
  -e Jwt__Issuer='Forjix.Api.Homologation' `
  -e Jwt__Audience='Forjix.Web.Homologation' `
  forjix:homolog
```

Valide `http://localhost:10000/`, `/api/health/live`, login, `/api/me`, renovação do cookie, `/api/products` e uma navegação direta para `/app/products`. Uma rota inexistente sob `/api` deve retornar problema HTTP 404, nunca `index.html`.

## 5. Criar o serviço no Render

1. Faça push do commit validado para a branch `main` somente quando quiser disponibilizá-lo.
2. No Render, crie um Blueprint a partir do repositório GitHub `Becero/forjix`; o `render.yaml` contém apenas um Web Service Docker.
3. Confirme a branch `main`, o `Dockerfile` na raiz e `/api/health/live` como health check.
4. Preencha os três valores marcados como secretos no fluxo inicial do Blueprint: as duas connection strings e `Jwt__SigningKey`.
5. Mantenha auto-deploy desativado até concluir a homologação. O Blueprint não cria PostgreSQL e não executa o migrator.
6. Após o deploy, repita os testes da seção anterior usando o domínio `onrender.com`.

## 6. Rollback

1. No painel do serviço, selecione o último deploy estável e use a opção de redeploy/rollback disponível.
2. Não reverta o banco automaticamente: migrations aplicadas podem não ser compatíveis com binários antigos.
3. Se a versão anterior não aceitar o schema atual, coloque o serviço em manutenção, restaure backups/PITR do `ForjixMaster` e dos bancos de tenant em novos bancos e troque as variáveis somente após validar a restauração.
4. Preserve o `Jwt__SigningKey` em um rollback comum; alterá-lo encerra todas as sessões.

## Limitações conhecidas da homologação

- Logos são gravados em `/app/App_Data/tenant-assets`, no filesystem efêmero do Render. Um deploy ou reinício pode remover o arquivo. A ausência da logo não impede autenticação nem operação; a configuração pode indicar que havia uma logo até que outra seja enviada. Object storage fica para uma etapa futura.
- As chaves de Data Protection usadas no cookie de refresh também não são compartilhadas entre instâncias e podem mudar após a substituição do container. Nesta fase use uma única instância e aceite novo login após deploy/restart. Antes de escalar, persista as chaves em um serviço externo protegido.
- A API não executa migrations no startup. Isso evita concorrência e mudanças de schema não controladas, mas exige a etapa operacional explícita antes de cada versão que altere o banco.
- O `render.yaml` deixa auto-deploy desligado e não define plano/região; escolha esses itens de acordo com custo, latência e IPs liberados no Azure SQL.
