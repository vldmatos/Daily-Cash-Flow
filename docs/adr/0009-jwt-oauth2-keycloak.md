# ADR-0009: Autenticacao JWT/OAuth2 via Keycloak

- **Status**: Accepted
- **Data**: 2026-04-26
- **Decisores**: Arquiteto de Solucoes

## Contexto

Considerando **"criterios de seguranca para consumo (integracao) de servicos"** como diferencial. Precisamos de um esquema de autenticacao/autorizacao que:

1. Seja **padrao da industria** (nao reinventar roda)
2. Suporte **clients** de diferentes tipos (SPA, mobile, server-to-server)
3. Permita **autorizacao** por merchant (um merchant nao pode ler saldo de outro)
4. Seja **trocavel** (vendor-neutral)

Opcoes consideradas:

| Opcao | Pro | Contra |
|---|---|---|
| **Keycloak (OSS)** | Completo, OIDC/OAuth2, federation, livre | Precisa operar |
| IdentityServer | .NET nativo, rico | Licenca comercial apos Upgrade |
| Azure AD B2C | Managed, SaaS | Lock-in, custo por Entra ID |
| Auth0 | SaaS robusto | Caro em escala |
| JWT custom | Simples | Reinvencao; sem refresh token, revogacao, federation |

## Decisao

Usar **Keycloak 23** como Identity Provider local, emitindo **JWT RS256** via **OAuth2/OIDC**. Os APIs validam o JWT usando JWKS baixado periodicamente.

Em producao, a mesma abordagem funciona com:

- Keycloak em HA
- **Microsoft Entra ID** (trocando apenas issuer + audience)
- Outro IdP OIDC-compliant

### Realm e Clients

- Realm: `cashflow`
- Client `cashflow-api` (confidential, server-side):
  - Flow: Client Credentials (para integracoes server-to-server)
- Client `cashflow-frontend` (public, PKCE):
  - Flow: Authorization Code + PKCE (para SPA/mobile)

### Claims

| Claim | Conteudo | Uso |
|---|---|---|
| `iss` | `https://idp/realms/cashflow` | validacao |
| `aud` | `cashflow-api` | validacao |
| `sub` | userId/serviceAccountId | logging |
| `exp`/`iat`/`nbf` | - | validacao |
| `merchantId` (custom) | GUID do merchant | **autorizacao** |
| `realm_access.roles` | `merchant`, `admin` | **autorizacao** |

Claim `merchantId` e **mapeado** no Keycloak como "User Attribute" e incluido no token via mapper.

### Autorizacao nos APIs

- `[Authorize]` obrigatorio em todas as rotas (excecao: `/health`, `/metrics`)
- `[Authorize(Roles = "merchant")]` onde aplica
- Policy `MerchantOwnerPolicy`: claim `merchantId` == path/body `merchantId`. Implementada em um requirement handler.

### Seguranca de Transporte

- TLS 1.2+ obrigatorio em producao (HSTS habilitado)
- Refresh token em cookie HttpOnly + Secure (para SPA)
- Token TTL curto (15 min access; 24h refresh)
- Revogacao de refresh token pelo Keycloak

### Rate Limiting

No gateway (YARP + `AddRateLimiter`):

- Global: 100 req/s por token
- Burst: 200 req/s com token bucket
- Excedido: `429 Too Many Requests` com header `Retry-After`

### Gestao de Segredos

- Client secret do `cashflow-api` nao e exposto a SPAs; e usado apenas server-to-server
- Secrets em `user-secrets` local; Key Vault/env vars em producao
- Rotacao manual documentada no runbook; automatica (V1.1) via Key Vault rotation policy

### Auditoria

- Todo request logado com `userId`, `merchantId`, `endpoint`, `result`, `traceId`
- Logs retidos 90 dias (Seq), mais em frio em S3/Blob (V1.1)

## Consequencias

### Positivas

- Padrao OIDC/OAuth2: qualquer SDK/cliente sabe consumir
- Keycloak e gratis, robusto, com UI de administracao rica
- Trocavel sem reescrever codigo (Microsoft Identity.Web tem JWT bearer nativo)
- Federation com Google/GitHub/AD ready

### Negativas

- Operar Keycloak em producao tem custo (ha DB, ha sessions)
- Cold start inicial de JWKS (resolvido com cache)
- JWT e stateless -> revogacao imediata requer lista de tokens revogados ou TTL curto. Optamos por TTL curto.

## Checklist Seguranca (OWASP top 10 2021)

| # | Categoria | Mitigacao |
|---|---|---|
| A01 | Broken Access Control | `[Authorize]` + `MerchantOwnerPolicy` em todos os endpoints |
| A02 | Cryptographic Failures | TLS 1.2+, secrets em vault |
| A03 | Injection | EF Core parametrizado; FluentValidation |
| A04 | Insecure Design | DDD + threat modeling documentado (V1.1) |
| A05 | Security Misconfiguration | `dotnet list package --vulnerable` no CI |
| A06 | Vulnerable Components | Trivy scan no pipeline |
| A07 | Auth/ID Failures | Keycloak com MFA (V1.1) |
| A08 | Integrity Failures | Container image signing (Cosign) — V1.1 |
| A09 | Logging/Monitoring Failures | OTel + Seq + alerting |
| A10 | SSRF | Sem outbound para URLs controladas por usuario |

## Referencias

- [Keycloak docs](https://www.keycloak.org/documentation)
- [RFC 7519 — JWT](https://www.rfc-editor.org/rfc/rfc7519)
- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
