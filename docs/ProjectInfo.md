# Project Information and Decisions

## ADR-0001 - Prototype technology and scope

- Date: 2026-09-18
- Status: Accepted
- Context: The greenfield project needs a fast visual prototype to validate basket construction, strategy configuration, execution cases, and risk interactions before backend development.
- Decision:
  - Build a separate Vue 3 JavaScript frontend in `prototipe`.
  - Use Element Plus as the UI component library.
  - Use local mock data only; do not connect cTrader or JigenDB.
  - Represent Dashboard, Basket Builder, Strategy Lab, Execution/Risk, and Decision Journal.
  - Keep the future backend boundary in C# and the future embedded RAG boundary in JigenDB.
- Consequences: The prototype validates workflows and visual states but cannot validate broker behavior, persistence, model quality, or trading profitability.

## ADR-0002 - Basket ownership and execution policy

- Date: 2026-09-18
- Status: Accepted
- Context: Each basket leg must be analyzed before the user decides whether to include it.
- Decision:
  - Make inclusion, direction, weight, timeframe, risk cap, and failure policy configurable per leg.
  - Version basket configurations and keep execution scenarios tied to a specific version.
  - Expose partial fills, rejected legs, residual exposure, and risk-gate outcomes in the prototype.
  - Prevent strategy analysis from visually bypassing the deterministic risk decision.
- Consequences: User control and operational failure states are first-class parts of the UX.

## ADR-0003 - Performance history and decision journal separation

- Date: 2026-09-18
- Status: Accepted
- Context: Operators need to inspect realized wins and losses in addition to individual execution states and decision traces.
- Decision:
  - Provide a dedicated Win/Loss History surface with aggregate metrics and episode filtering.
  - Link each episode to entry rationale, intermediate management, exit reason, MAE, MFE, and retrieved evidence count.
  - Keep the Decision Journal focused on chronological audit events rather than performance aggregation.
  - Use mock episodes only until the C# persistence and JigenDB boundaries are implemented.
- Consequences: Performance evaluation and causal audit remain connected but have distinct user workflows and future read models.

## ADR-0004 - Named basket registry and lifecycle

- Date: 2026-09-18
- Status: Accepted
- Context: Win/Loss episodes reference multiple historical baskets, while the first Basket Builder model exposed only one mutable basket and did not identify which configuration was active.
- Decision:
  - Manage multiple named, versioned baskets through the application UI.
  - Distinguish the basket selected for editing from the single basket active for future strategy execution.
  - Require an impact summary and explicit confirmation before activation.
  - Allow non-destructive archival only; preserve basket identity and versions referenced by orders, performance episodes, journal events, and JigenDB evidence.
  - Expose create, rename, clone, archive, activation, and version history operations without requiring direct database access.
- Consequences: Basket lifecycle becomes an application-owned domain workflow. The future C# backend must enforce the same invariants and provide auditable commands for every transition.

## ADR-0005 - Continuous analysis and supervised Market Manager

- Date: 2026-09-18
- Status: Accepted
- Context: After approving a basket, the operator needs market analysis to continue in the background and compliant order proposals to advance without approving every signal manually.
- Decision:
  - Add a Market Manager surface with manual, supervised, and automatic operating modes; supervised is the prototype default.
  - Keep continuous market analysis independent from basket composition approval.
  - Allow only proposals that pass deterministic freshness, exposure, liquidity, and risk gates to advance automatically.
  - Route exceptions to the operator for approval, rejection, or suspension; permanently block proposals that fail hard limits.
  - Treat suggested basket composition changes as new drafts requiring explicit activation, never as automatic mutations of the active basket.
- Consequences: The future C# backend must run analysis as a background service while deterministic policy and risk components retain exclusive authority over order dispatch.

## ADR-0006 - Production runtime and integration stack

- Date: 2026-09-18
- Status: Accepted
- Context: The visual prototype is approved, but production analysis requires concrete choices for runtime, persistence, broker connectivity, local inference, security, and rollout.
- Decision:
  - Deploy one continuously running Linux host with .NET 10 LTS, ASP.NET Core Web API, Worker Services, EF Core, Hikyaku, MapZilla, and xUnit.
  - Persist transactional state in SQLite WAL and semantic evidence in embedded JigenDB; neither store substitutes for the other.
  - Integrate cTrader through the official C# SDK and Protobuf, beginning with one demo account and promoting to live through an explicit gate.
  - Run the local model through Ollama on a local-only endpoint and keep order authority in deterministic application services.
  - Execute basket legs sequentially by risk priority and provide a local operator login with audited actions.
- Consequences: The MVP favors single-host operability and simple recovery. Multi-instance deployment, PostgreSQL, multiple accounts, and reviewer roles remain later controlled extensions.

## ADR-0007 - Freeze the prototype and create production projects from scratch

- Date: 2026-09-18
- Status: Accepted
- Context: The validated Vue application is a behavioral and visual prototype. Evolving it in place would carry mock contracts, simulated execution behavior, and prototype shortcuts into a safety-critical production system.
- Decision:
  - Rename the prototype root to `prototipe/` and keep its application contents unchanged.
  - Treat `prototipe/` as a read-only reference for workflows, terminology, and visual intent.
  - Create the production frontend from scratch under `client/` and the production backend under `server/`.
  - Use Vue 3 with TypeScript in `client/`; do not migrate or adapt the prototype JavaScript store.
  - Make server REST contracts canonical and integrate the client directly against them without mock compatibility adapters.
- Consequences: Prototype and production have independent dependency graphs and build outputs. Behavioral traceability is maintained through acceptance criteria rather than source-code reuse.

## ADR-0008 - Standard ASP.NET Core DI instead of the organization MEF module loader

- Date: 2026-09-18
- Status: Accepted
- Context: The organization reference templates register modules through a MEF loader (`System.Composition`, `[Export(typeof(IModule))]`, `Loader.Current`) whose types come from `Wise.Core` / `Agricolus.Common`. Those packages are not published on nuget.org, the organization profile expects the private `wisetown2022` feed, and no `nuget.config` exists in this repository. The approved project is an autonomous single-operator application.
- Decision:
  - AutoTrade is not a Teamdev/Wisetown module and does not use the private feed or the MEF loader.
  - Each tier exposes a `Module` class with a plain `IServiceCollection` extension: `AddTradingApi` and `AddTradingHandlers`.
  - The composition root registers both tiers explicitly and registers the single mediator with the Handlers assembly.
  - Layer boundaries stay unchanged: `API` never references `Handlers`; the composition root depends on both.
- Consequences: Module composition is explicit and readable, with no need for a private feed. If the project later joins the organization, migration to the loader is a controlled change requiring a new Gate 1 approval.

## ADR-0009 - SQLite persistence deviations

- Date: 2026-09-18
- Status: Accepted
- Context: The platform skill assumes a relational provider with schema support and requires a default schema name, while the approved persistence is EF Core with SQLite WAL, which has no schema concept.
- Decision:
  - Do not set a default schema in `DB.cs`; SQLite treats `schema.table` as an attached database and would reject it.
  - Table names are mapped explicitly with `[Table]`; uniqueness and lookup indexes are configured in `OnModelCreating`.
  - `Database.EnsureCreatedOnStartup` creates the local development schema only, and it defaults to `false`; production schema is owned by human-authored migrations.
  - The agent never generates nor applies EF Core migrations.
  - Relational integrity is enforced in command handlers, not through ORM foreign keys, following the platform convention.
- Consequences: Local development needs no migration step, while production remains under human-controlled schema evolution. Adding a second persistence provider later would require reintroducing a schema strategy.

## ADR-0010 - Local session, CSRF contract, and implementation watch-outs

- Date: 2026-09-18
- Status: Accepted
- Context: The MVP requires an audited local operator session protecting a JSON API reached same-site from the SPA, and implementation details surfaced during runtime verification are easy to regress.
- Decision:
  - The authentication cookie carries only opaque claims (`operator_id`, `session_token`); the authoritative session lives in SQLite and is re-validated on every request through `CookieAuthenticationEvents.OnValidatePrincipal`, so logout, expiry and operator deactivation take effect immediately.
  - Mutating endpoints require a token-based antiforgery token delivered in the `X-CSRF-TOKEN` header, obtained from `GET /api/session/antiforgery-token`.
  - `AddControllersWithViews` must stay in use: `AddControllers` does not register the antiforgery filter services, so `[ValidateAntiForgeryToken]` fails at request time.
  - Fail-closed defaults: a missing kill-switch row is reported as engaged, and data that cannot be read never yields "safe to trade".
  - Watch-out: the `Operator` entity type shares its name with the `CQRS.Operator` namespace, so handlers for it need a `using OperatorEntity = ...` alias. Renaming the entity to `OperatorAccount` is the recommended follow-up to remove the trap.
- Consequences: Security behaviour is explicit and verified at runtime. Operators must bootstrap credentials through environment variables or a secret store, never through committed configuration.

## ADR-0011 - Production client scaffold and HTTP contract conventions

- Date: 2026-09-18
- Status: Accepted
- Context: The production frontend had to be created from scratch in `client/` (no reuse of the prototype) and integrated with the verified REST API. Runtime verification exposed two contract behaviours that are easy to regress.
- Decision:
  - Scaffold `client/` with Vue 3 + TypeScript, Vite, Router, Pinia, ESLint, Prettier, plus Element Plus, Axios, Vue I18n (Italian default) and LESS, per the locked stack.
  - Component logic modules use `defineComponent`; plain object definitions do not satisfy the component type required by the router.
  - API enums are serialized as names (`JsonStringEnumConverter`), so the HTTP contract stays readable and the client uses string literal unions.
  - The antiforgery token is bound to the caller identity: the client renews it after a successful login and after logout, otherwise authenticated mutations fail with `400`.
  - The axios instance sends cookies (`withCredentials`) and resolves the API base URL from `src/settings.ts` plus the `public/settings.json` runtime override; no dev-proxy prefix is used.
  - Service classes are transport-only: one method per endpoint, no mapping, no normalization, no runtime payload checks. Pinia stores own orchestration.
  - Development ports are fixed: API `http://127.0.0.1:5271`, client `http://127.0.0.1:5180`; the client origin must be listed in `Cors:AllowedOrigins`.
- Consequences: The client renders the operational status and the kill-switch workflow against live server state. Any future port change or CORS change must be applied together, and the antiforgery renewal step must not be removed.

## ADR-0012 - EF Core migrations own the schema (EnsureCreated removed)

- Date: 2026-09-18
- Status: Accepted
- Context: The schema was created implicitly by `Database.EnsureCreatedAsync()` behind the `Database:EnsureCreatedOnStartup` flag. `EnsureCreated` never evolves an existing database: after Slice 2 added the basket tables the local development database kept the Slice 0/1 schema, so the application would start against a database missing tables it now depends on. The schema had to become an explicit, versioned artifact.
- Decision:
  - `TradingDatabaseInitializer.InitializeAsync` calls `Database.MigrateAsync()` unconditionally; `EnsureCreatedAsync` and the `Database:EnsureCreatedOnStartup` setting are removed from code and from both `appsettings` files.
  - Migrations live in `AutoTrade.Trading.Handlers/Model/Migrations`, next to the `DB` context that owns the model.
  - `Microsoft.EntityFrameworkCore.Design` is referenced by `AutoTrade.Trading.Handlers` (migrations project) and by `AutoTrade.Trading` (startup project), both with `PrivateAssets="all"`; the EF Core CLI resolves the design-time services from the startup project.
  - `20260918170431_InitialCreate` is the single initial migration describing the complete current schema: 13 tables and 11 indexes, including the 7 unique indexes. It carries no `schema` qualifier, consistent with ADR-0009.
  - Migrations are generated by the agent with `dotnet ef migrations add`; they are never hand-edited.
  - A database created by `EnsureCreated` has no `__EFMigrationsHistory` and cannot be migrated in place: it must be reset (or baselined with a dedicated migration) before the next start.
- Consequences: Schema changes require a migration, and the model-to-schema drift is caught by the migration diff instead of surfacing as a missing-table error at runtime. Any database file created before this ADR must be reset once; after that, starts are idempotent and the applied migration is recorded in `__EFMigrationsHistory`.

## ADR-0013 - Per-market leg risk limits

- Date: 2026-09-19
- Status: Accepted
- Context: The Risk Engine needed leg thresholds (maximum spread, maximum volatility) and the open question was whether one global value per threshold was enough. It is not: a spread that is normal on an index makes a major FX pair untradeable, and a table of values spanning both either blocks healthy legs or admits unhealthy ones. The market of a leg already exists in the model (`BasketVersionLeg.Market`, `MarketKind`), so the differentiation needs no new concept.
- Decision:
  - Leg thresholds are per market and are configured under `Trading:Risk:Markets:{Market}:LegSpreadMaxPips` and `Trading:Risk:Markets:{Market}:LegVolatilityMaxPercent`, where `{Market}` is the `MarketKind` name (`Fx`, `Metal`, `Index`).
  - `RiskThresholds` holds a `Dictionary<MarketKind, MarketLegLimits>`; the factory materialises one entry per declared market, and `ForMarket` never returns null. A market that was never configured yields an unconfigured set, so its legs block instead of borrowing another market's limit.
  - `RiskEvaluationLeg` carries `Market`; the engine resolves every leg threshold from that market only. `RiskGateResultDto` exposes the market a gate refers to (null for the basket-level gates, which are market agnostic).
  - `IsFullyConfigured` requires the snapshot window plus both thresholds of **every** declared market, including markets the active basket does not use: an unconfigured market must stay visible rather than silently ignored.
  - The snapshot validity window (`Trading:Risk:SnapshotMaxAgeSeconds`) stays global, because a basket version carries a single market snapshot. If snapshots become per market, this threshold becomes per market in the same change.
  - No threshold has a default in code, and no market inherits another market's value. An unconfigured limit produces `RISK_THRESHOLD_NOT_CONFIGURED` with a blocking verdict.
- Consequences: Adding a market to `MarketKind` immediately creates a configuration obligation, and `IsFullyConfigured` stays false until its limits are decided. `GET /api/risk/limits` reports one entry per market with `IsConfigured`, so the operator can see exactly which market is still undecided, and the sample `appsettings.json` ships the key structure with empty values that keep every gate blocking until real numbers are chosen.

## ADR-0014 - Risk decision is read only and rendered as-is

- Date: 2026-09-19
- Status: Accepted
- Context: Slice 4 requires the gates and their reasons to be visible in the client. The Risk Engine already produces the decision server side, so the open question was what the client owns: recomputing or summarising the verdict would create a second source of truth, and caching a decision per basket would let the UI show a verdict the server no longer holds.
- Decision:
  - The client reads two read-only endpoints, `GET /api/risk/limits` (thresholds in force, one entry per market, including the unconfigured ones) and `GET /api/risk/baskets/{basketId}` (aggregate verdict plus every gate). It never evaluates a threshold and never derives a verdict.
  - The gate panel is bound to the basket selected for editing, not to the active one: the gates name the version they evaluated (`versionNumber`, `basketVersionId`), and a basket that does not hold the active version reports it through `ActiveVersionMissing` instead of hiding the state.
  - The decision is reloaded whenever the selected basket or its stored detail changes, so a mutation followed by a registry reload cannot leave a stale verdict on screen.
  - Every gate renders code, subject, market, observed value, threshold, unit and timestamp. A missing measurement is rendered as `n.d.` and an unconfigured threshold as `Non configurata`: neither is replaced by a zero or by a default, because the operator must be able to tell "no data" from "no limit".
  - The `detail` text produced by the engine is displayed verbatim as the audit narrative. It is currently English while the operator UI is Italian; localising explanations by gate code in the client is the pending follow-up, and the engine text stays untouched so journal payloads remain stable.
- Consequences: The UI can never disagree with the recorded decision, and a blocked verdict is always explainable from the screen. Conversely the panel can only be as informative as the server contract: a new gate code needs an i18n label and, until then, renders with the raw code name.

