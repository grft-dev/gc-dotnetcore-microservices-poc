# Insurance sales portal — Graftcode port

This repository is a fork of the [AltkomSoftware .NET microservices sample](https://github.com/asc-lab/dotnetcore-microservices-poc). It exists to show Graftcode in use: product, price, offer, policy, search, account, dashboard, and agent operations are hosted as grafts on Graftcode Gateway and checked with `gc-insurance-graft-e2e`.

The graft stack does not start RabbitMQ, ElasticSearch, Eureka, the Ocelot gateway, Hangfire, or SignalR. Legacy chat/UI/gateway projects have been removed from this port so the repository tracks only the GRAFT-hosted business path. Creating a policy writes the policy, the premium account, the search row, and the sales fact in one SQL transaction. CSV payment import stays outside the demo.

## Testing note

We intentionally rolled back the recent attempt to restore legacy in-repo unit tests for this port. In this GRAFT shape, public entry points are static methods, and the original .NET test architecture does not isolate or mock static boundaries well without intrusive refactoring.

That restoration produced brittle tests with high maintenance cost and low confidence gain. For this port we prioritize integration and E2E verification of the exposed GRAFT surface, where business behavior is validated end-to-end.

## Migration impact (auditable slice KPI)

Operations currently exposed in this port include product catalog, pricing, offer creation, policy create/details/terminate, policy search, account balance, sales dashboard views, and agent authentication/profile lookup.

Known behavior and architecture differences versus the base implementation:

- **Auth:** the previous flow produced signed JWT tokens with expiry and claims; this port returns a text payload (`login|roles|avatar|products`) and profile lookup now takes login as an explicit argument.
- **Policy search:** Elasticsearch MultiMatch (`BestFields`, fuzziness `AUTO`) was replaced by SQL `LIKE '%text%'`, `TOP 10`, sorted by policy number.
- **New policy balance:** the previous `PolicyCreatedHandler` opened accounts without posting expected premium; this rewrite writes an `Expected` entry during policy creation, so starting balance differs.
- **Consistency model:** RabbitMQ/outbox with separate projections was replaced by writing policy, account, search, and sales rows in one SQL transaction. This is an architecture simplification in this rewrite and is not attributable to Graftcode alone.

This repository also removes product surface that existed in the original sample: UI (`Web`, `BlazorWasmClient`), chat (`ChatService`), API gateway (`AgentPortalApiGateway`), and legacy in-repo tests. Because of that, whole-repository before/after percentages are not a fair comparison of the same product surface.

### Reproducible numbers (pinned refs)

Measured refs:

- base: `6500daaf` (`origin/master` baseline used in review)
- head: `83db31210fec5cda5f6e6685148ad6606950836b` (current `gc-port` head at measurement time)

Count rule (applied on both refs):

- non-empty physical lines in UTF-8/UTF-8-SIG text,
- comments/usings/braces included,
- SQL embedded inside C# included,
- formula equivalent to `sum(bool(line.strip()) for line in text.splitlines())`.

Measured KPI totals:

| Metric | Value |
| --- | ---: |
| Base slice (95 files) | 3019 |
| Head slice (15 files) | 1527 |
| Fewer lines | 1492 |
| Reduction | 49.42% |

KPI interpretation:

- This KPI is a **mixed implementation slice** (SQL repositories + service wrappers + tariff calculator), not pure domain logic.
- In the head slice, **1168 / 1527** lines are in `Repositories`, and **359 / 1527** lines are in service wrappers + `TariffCalculator`.
- The `|` / `;` / `~` wire format replaced typed DTO responses; server-side formatting code is counted, client-side parsing cost is not.
- Generated Graft NuGet clients/gateway are outside this count, just as third-party library source code was outside the previous stack's count.

## Exposed public surface

Modules:

- `ProductService.dll`
- `PricingService.dll`
- `PolicyService.dll`
- `PolicySearchService.dll`
- `PaymentService.dll`
- `DashboardService.dll`
- `AuthService.dll`

Types and methods (names have no `Async` suffix):

- `ProductService.Services.ProductCatalogService`
  - `FindAllProducts`, `FindProductByCode`, `CreateProductDraft`, `ActivateProduct`, `DiscontinueProduct`
- `PricingService.Services.PriceCalculationService`
  - `CalculatePrice`
- `PolicyService.Services.PolicySalesService`
  - `CreateOffer`, `CreatePolicy`, `GetPolicyDetails`, `TerminatePolicy`
- `PolicySearchService.Services.PolicySearch`
  - `SearchPolicies`
- `PaymentService.Services.PaymentAccountService`
  - `GetAccountBalance`
- `DashboardService.Services.SalesDashboardService`
  - `GetAgentsSales`, `GetTotalSales`, `GetSalesTrends`
- `AuthService.Services.AgentAuthService`
  - `Authenticate`, `GetAgentByLogin`

`CreateOffer` calls the pricing library in-process. It does not use HTTP.

A composite record joins fields with `|`. Child lists use `;`, and child fields use `~`. Dates are `yyyy-MM-dd`. Money uses invariant `0.00`. A missing product is an empty string. An empty list is an empty array. A missing environment variable or a broken business rule throws `InvalidOperationException` with a stable message.

Examples:

- Product list: `TRI|Safe Traveller|Travel insurance|/static/travel.jpg|10|plane|Active`
- Product detail appends covers and questions: `C2~Illness~~false~5000.00;C3~Assistance~~true~` and `DESTINATION~choice~1~Destination~EUR=Europe,WORLD=World,PL=Poland`
- Price: `36.00|C2=26.00;C3=10.00`
- Offer: `{offerNumber}|36.00|C2=26.00;C3=10.00`
- Policy details: `{number}|TRI|2026-06-01|2027-06-01|Ada Lovelace|36.00|C2;C3`
- Termination: `{number}|{moneyToReturn}`
- Account: `{policyNumber}|{accountNumber}|{balance}` (a new policy's expected premium makes the balance negative)
- Search: `{policyNumber}|{holder}|{product}|{status}`
- Agent sales: `jimmy.solid|2|200.00`
- Total sales starts with `total|{count}|{premium}` and then one line per product
- Trends: `{periodStart}|{count}|{premium}` with unit `Day`, `Week`, `Month`, or `Year`
- Authenticate: `admin|SALESMAN;USER|static/avatars/admin.png|TRI;HSI;FAI;CAR`
- Agent card: `admin|static/avatars/admin.png|TRI;HSI;FAI;CAR`

`CreateProductDraft` takes `code`, `name`, `image`, `description`, `maxNumberOfInsured`, `icon`, encoded covers, and encoded questions. `CalculatePrice` and `CreateOffer` take `productCode`, `policyFrom`, `policyTo`, covers such as `C2;C3`, and answers such as `DESTINATION=EUR;NUM_OF_ADULTS=1;NUM_OF_CHILDREN=0`.

The runtime image is `mcr.microsoft.com/dotnet/runtime:10.0`. The host replaces the platform stub of `Microsoft.Data.SqlClient` with the unix implementation, because GG loads the flat DLL.

This demo starts the stack without `GG_PROJECT_KEY`.

## 1) Start Docker stack with GG

From `ports/gc-dotnetcore-microservices-poc`:

```powershell
docker compose -f docker-compose.graft.yml up --build -d
```

Ports:

- `8092` Gateway (`/nuget`, WebSocket)
- `8093` Graft Vision
- `1435` SQL Server (seeded products, tariffs, agents, accounts, search rows, and sales facts)

WebSocket endpoint for clients:

- `ws://localhost:8092/ws`

Graft Vision:

- `http://localhost:8093`

Demo logins live in the seed: `admin` / `admin`, `jimmy.solid` / `secret`, `danny.solid` / `secret`.

## 2) Install generated clients

After the container logs `dotnet add package`, open `http://localhost:8092/nuget` and use the package ids printed there.

The ids below are an example from one generation (`zp5n7a`). Without a `GG_PROJECT_KEY` linked to your Graftcode account, every generation gets a new suffix. With that key the id stays stable.

- `graft.nuget.productservice_zp5n7a` 1.0.0
- `graft.nuget.pricingservice_zp5n7a` 1.0.0
- `graft.nuget.policyservice_zp5n7a` 1.0.0
- `graft.nuget.policysearchservice_zp5n7a` 1.0.0
- `graft.nuget.paymentservice_zp5n7a` 1.0.0
- `graft.nuget.dashboardservice_zp5n7a` 1.0.0
- `graft.nuget.authservice_zp5n7a` 1.0.0

NuGet will not replace an existing `1.0.0` when the package id stays the same. When the hash changes, delete the old package folder and restore from the feed printed in the container log. When the hash stays and the host surface changed, delete the cached `1.0.0` and restore from that same feed.

Runtime config (replace the suffix with the one from the log):

```powershell
$env:GRAFTCODE_CONFIG_NUGET_PRODUCTSERVICE = "name=graft.nuget.ProductService; runtime=netcore; modules=ProductService.dll; host=ws://localhost:8092/ws; stateless=true"
$env:GRAFTCODE_CONFIG_NUGET_PRICINGSERVICE = "name=graft.nuget.PricingService; runtime=netcore; modules=PricingService.dll; host=ws://localhost:8092/ws; stateless=true"
$env:GRAFTCODE_CONFIG_NUGET_POLICYSERVICE = "name=graft.nuget.PolicyService; runtime=netcore; modules=PolicyService.dll; host=ws://localhost:8092/ws; stateless=true"
$env:GRAFTCODE_CONFIG_NUGET_POLICYSEARCHSERVICE = "name=graft.nuget.PolicySearchService; runtime=netcore; modules=PolicySearchService.dll; host=ws://localhost:8092/ws; stateless=true"
$env:GRAFTCODE_CONFIG_NUGET_PAYMENTSERVICE = "name=graft.nuget.PaymentService; runtime=netcore; modules=PaymentService.dll; host=ws://localhost:8092/ws; stateless=true"
$env:GRAFTCODE_CONFIG_NUGET_DASHBOARDSERVICE = "name=graft.nuget.DashboardService; runtime=netcore; modules=DashboardService.dll; host=ws://localhost:8092/ws; stateless=true"
$env:GRAFTCODE_CONFIG_NUGET_AUTHSERVICE = "name=graft.nuget.AuthService; runtime=netcore; modules=AuthService.dll; host=ws://localhost:8092/ws; stateless=true"
```

## 3) Run the e2e verifier

```powershell
dotnet run --project C:\Coding\Marketing\gc-insurance-graft-e2e\gc-insurance-graft-e2e.csproj -c Release -- `
  --host "ws://localhost:8092/ws" `
  --stateless true `
  --report "C:\Coding\Marketing\gc-insurance-graft-e2e\reports\insurance-graft-e2e.json"
```

The runner uses the PackageReferences checked into its project file. Those ids are one generation; replace them when `/nuget` prints a new suffix.

Exit codes: `0` all checks passed, `1` an assertion failed, `2` the run was blocked before assertions.

## 4) Stop

```powershell
docker compose -f docker-compose.graft.yml down
```

## Outside this demo

Bank-statement CSV import remains outside this demo and is not part of the exposed graft surface.

## What the port removed

This rewrite runs business calls through class libraries loaded by the graft host and removes previously exposed product surface components: HTTP hosts/controllers, Swagger, MediatR request pipeline, Eureka/RestEase integrations, RabbitMQ/outbox, Hangfire, SignalR, ElasticSearch, Marten, NHibernate/EF runtime usage on the active path, plus UI/chat/gateway projects.

## Slice manifest and reproducibility details

Use this command to reproduce the KPI totals with pinned SHAs and print full manifests:

```powershell
pwsh .\scripts\line-count-slice.ps1 -BaseSha 6500daaf -HeadSha 83db31210fec5cda5f6e6685148ad6606950836b -ShowManifest
```

Head manifest (`83db31210fec5cda5f6e6685148ad6606950836b`, exact files counted):

- `ProductService/Repositories/SqlProductRepository.cs`
- `ProductService/Services/ProductCatalogService.cs`
- `PricingService/Repositories/SqlTariffRepository.cs`
- `PricingService/Services/PriceCalculationService.cs`
- `PricingService/Domain/TariffCalculator.cs`
- `PolicyService/Repositories/SqlPolicyRepository.cs`
- `PolicyService/Services/PolicySalesService.cs`
- `PolicySearchService/Repositories/SqlPolicySearchRepository.cs`
- `PolicySearchService/Services/PolicySearch.cs`
- `PaymentService/Repositories/SqlPaymentRepository.cs`
- `PaymentService/Services/PaymentAccountService.cs`
- `DashboardService/Repositories/SqlDashboardRepository.cs`
- `DashboardService/Services/SalesDashboardService.cs`
- `AuthService/Repositories/SqlAgentRepository.cs`
- `AuthService/Services/AgentAuthService.cs`

Base manifest (`6500daaf`, exact selection rule):

- include all `*.cs` under these services:
  - `AuthService`, `DashboardService`, `PaymentService`, `PolicySearchService`, `PolicyService`, `PricingService`, `ProductService`
- include only subtrees:
  - `Domain/**`, `Commands/**`, `Queries/**`, `DataAccess/**`
- exclude files:
  - `PaymentService/Domain/BankStatementFile.cs`
  - `PaymentService/Domain/InPaymentRegistrationService.cs`

Excluded from KPI on both sides:

- controllers and API/DTO contract projects (`*.Api`)
- `Init/**`, `Messaging/**`, `Listeners/**`, `RestClients/**`
- host/bootstrap/runtime wiring (`Program`, `Startup`, compose/runtime shell)
- in-repo tests
- CSV import behavior and related files
- `Graftcode/GraftAsync.cs` (11 lines; excluded unless added symmetrically on both sides)
- `db/seed.sql` (about 308 non-empty lines; SQL seed artifact, outside C# KPI)
- NHibernate XML mappings (`*.hbm.xml`) on the base side (outside `*.cs` counting rule)

Context-only whole-base number (not KPI): in `6500daaf`, the same seven services contain **5302** non-empty `*.cs` lines across **164** files when counted without the KPI slice filter.

## Troubleshooting

- `dotnet add package` takes its source from the container log. There is no fixed feed URL. Without a project key, each start prints a new `https://grft.dev/<guid>__free`. If restore fails, restart the host and use the URL from that log.
- If port `8092` is occupied, change the published ports in `docker-compose.graft.yml` and point the client host at the new WebSocket URL.
- Restore under this directory uses the repo-local `NuGet.Config` (nuget.org only).
