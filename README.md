# Insurance sales portal — Graftcode port

This repository is a fork of the [AltkomSoftware .NET microservices sample](https://github.com/asc-lab/dotnetcore-microservices-poc). It exists to show Graftcode in use: product, price, offer, policy, search, account, dashboard, and agent operations are hosted as grafts on Graftcode Gateway and checked with `gc-insurance-graft-e2e`.

The graft stack does not start RabbitMQ, ElasticSearch, Eureka, the Ocelot gateway, Hangfire, or SignalR. Chat, the Vue portal, the Blazor client, and the gateway remain in the tree and are outside this demo. Creating a policy writes the policy, the premium account, the search row, and the sales fact in one SQL transaction. CSV payment import stays outside the demo.

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

Chat (the SignalR notification path is already a no-op), the Vue app under `Web`, `BlazorWasmClient`, `AgentPortalApiGateway`, `eureka-server`, and `scripts/app.yml` stay on disk. They are not in `DotNetMicroservicesPoc.sln` and `docker-compose.graft.yml` does not start them. PDF certificates were already absent from this fork.

## What the port removed

The business operations above are the same ones the REST controllers exposed. The graft host loads class libraries. Removed from the exposed path: ASP.NET hosts, controllers, Swagger, MediatR, Eureka, RestEase, RabbitMQ, the outbox, Hangfire, SignalR, ElasticSearch, Marten, NHibernate, and EF Core. Product, tariff, offer, and policy rules stay in the repositories. Search, account balance, and dashboard aggregates read the rows `CreatePolicy` writes.

## Same functions

The graft still covers the operations the REST controllers exposed: the product catalog, price, offer, policy, search, account balance, sales figures, and agent login.

In the original sample those operations were the domain rules, the command and query handlers, and the repositories those handlers called. That is **2826** non-empty `*.cs` lines: **1933** of rules and handlers, and **893** of persistence (EF Core, NHibernate, Marten, ElasticSearch, and the in-memory agents).

The same operations are **1527** lines in 15 files now, including the SQL and the `|` wire format.

| | Lines |
| --- | ---: |
| Original rules, handlers, and their repositories | 2826 |
| This port, SQL and wire format included | 1527 |
| Fewer lines | 1299 |

That is a **46%** reduction of the code that performs these operations.

The **4580** lines in **144** files on disk before this rewrite are the whole seven projects, including the host, controllers, MediatR, and RabbitMQ. That count is not a measure of these functions.

Bank-statement CSV import is outside both numbers. The shared `Graftcode/GraftAsync.cs` bridge is 11 lines, linked into each library, and is the only `GetResult` call site. It goes away when Gateway accepts `Task`.

## Troubleshooting

- `dotnet add package` takes its source from the container log. There is no fixed feed URL. Without a project key, each start prints a new `https://grft.dev/<guid>__free`. If restore fails, restart the host and use the URL from that log.
- If port `8092` is occupied, change the published ports in `docker-compose.graft.yml` and point the client host at the new WebSocket URL.
- Restore under this directory uses the repo-local `NuGet.Config` (nuget.org only).
