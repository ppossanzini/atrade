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

## ADR-0016 - Own protocol client on the vendored protobuf schema

- Date: 2026-09-19
- Status: Accepted
- Context: ADR-0006 chose the official cTrader C# SDK. On inspection the published package (`cTrader.OpenAPI.Net` 1.4.4) dates to 2022, ships only `lib/net6.0` (out of support on .NET 10), and pins `Google.Protobuf` 3.20.1, `System.Reactive` 5.0.0 and `Websocket.Client` 4.4.43. Taking it would mean carrying an unmaintained runtime and three dated transitive dependencies into the tier that talks to the broker, and the SDK would still hide the message flow the risk and reconciliation work depends on.
- Decision:
  - The official **protobuf schema** is vendored from `spotware/openapi-proto-messages` at a pinned commit, with its MIT licence, under `Handlers/Broker/Proto/` and a `PROVENANCE.md` recording source, commit, retrieval date and the one additive line per file (a `csharp_namespace` option, needed because the upstream files declare no `package`).
  - The protocol client is ours: application authentication, account authentication, account list, request/response correlation by `clientMsgId`, heartbeat and error mapping, built directly on `Google.Protobuf` and `ClientWebSocket`.
  - Only protobuf runtime packages are taken (`Google.Protobuf`, `Grpc.Tools` at build time), so the dependency surface stays small and maintained.
  - Consequence accepted: framing, correlation, reconnection and error mapping are our responsibility and must be tested as such. The vendored schema is never edited; if upstream changes, the schema is re-vendored, not patched.
- Consequences: The broker tier owns its transport, which is where the reconciliation dedup and the reconnect rules have to live anyway. Serialization and correlation are covered by tests of the protocol client, and the upstream protocol stays the single source of truth for message shapes. ADR-0006's SDK sentence is superseded by this record; the stack profile states the same.

## ADR-0017 - In Automatic the operator does not decide

- Date: 2026-09-19
- Status: Accepted
- Context: The approved routing matrix says that in Automatic a proposal with a `Review` gate is "forbidden until it is redefined as `Pass`". Read literally, a review in Automatic can never be approved by hand; read loosely, the mode would only describe automatic forwarding and the operator could always decide. The difference matters, because it decides whether the mode is a rule or a label.
- Decision:
  - The mode governs **who decides**, and Automatic means the operator has delegated the decisions. A proposal in `NeedsReview` is not decidable in Automatic, and the operator must switch to Supervised or Manual on purpose, with a journalled change, before acting on it.
  - Everything else about routing is unchanged: `Block` is terminal in every mode, `Allow` is forwarded automatically in Supervised and Automatic, and a review always waits for a person.
  - The server computes decidability per read from the current mode instead of storing it, so a proposal never becomes decidable because it was once stored that way. The queue reports it, and the interface disables the actions accordingly.
  - A refusal is journalled with its reason (`mode_requires_operator`, `gate_regressed`, a missing reason or an expired window), so the operator can always tell why an action was not accepted.
- Consequences: The mode is a real constraint with an observable cost — one deliberate, audited mode change before a manual decision. In exchange, no delegation is silently revoked and no review becomes a permission by accident. The rule is pinned by table tests over the whole matrix and by a handler test that proves an approval is refused in Automatic.

## ADR-0018 - Order execution behind the same transparent seam

- Date: 2026-09-19
- Status: Accepted
- Context: The Execution Engine has to be built and verified before the cTrader application is approved, otherwise it stays unverifiable for weeks. The alternative, waiting, leaves the most dangerous code in the project (the one that sends orders) written only on paper. ADR-0015 already established how this is solved for market data: one seam, two implementations, and no part of the application knowing which one is in force.
- Decision:
  - `IExecutionGateway` exposes the two operations the engine actually needs: send one leg with its `clientOrderId`, and read the current state of an order for reconciliation.
  - Two implementations live behind it: the simulated gateway and the cTrader gateway. The simulated one is deterministic and profile driven — it accepts, rejects, partially fills, never answers (to exercise the timeout) and repeats an event (to exercise dedup). The cTrader implementation remains unverified against a real account until the approved demo credentials are configured.
  - The same boundary as ADR-0015 holds: no contract, no decision, no journal payload and no screen carries a marker of which implementation is active. Where the outcomes come from is an operational fact, answered by configuration.
  - The gateway never writes transactional state: persistence belongs to the handler and happens before the send.
  - What the simulation does **not** replace: reconciliation against a real account. Every rule that needs the real broker (dedup of true broker identities, reconnect without duplicates, deal reconstruction) stays unverified until the approved demo credentials are exercised, and no claim is made about it.
- Consequences: The engine, its state machine, idempotency, sequencing, timeout handling and compensation are verifiable with the simulator, while the cTrader adapter supplies the provider path after approval. The project remains explicit that reconciliation is verified only in its logic until a real demo account is exercised, and the promotion gate keeps that distinction visible.

## ADR-0019 - Compensation is an explicit operator action, never a rollback

- Date: 2026-09-19
- Status: Accepted
- Context: A basket that is only partially executed leaves real exposure. The tempting shortcut is to treat compensation as an atomic rollback of the executed legs, but no broker offers atomicity across sequential orders, and a silent closure would be a trading decision taken by the implementation instead of the operator.
- Decision:
  - Compensation is a **new sequence of orders** with its own execution, its own `clientOrderId` values and its own audit trail. The original execution is never rewritten.
  - It is always confirmed by the operator, in every mode including Automatic: it touches real exposure and is not a delegable decision.
  - A violated policy puts the execution in `CompensationRequired` and leaves it visible with its real coverage; the system never closes positions on its own, not even to "fix" a partial fill.
  - If compensation is not confirmed, the residual exposure stays visible as a degraded state instead of disappearing from the operator's view.
- Consequences: The residual exposure of a partial execution is an explicit, auditable state rather than a hidden invariant, and the kill switch keeps its meaning (it blocks new openings and never closes what exists). The cost is an operator action on the critical path, which is the intended trade for the most dangerous operation in the system.

## ADR-0020 - Instrument facts from the provider, position size from the risk model

- Date: 2026-09-19
- Status: Accepted
- Context: The first draft of the execution design asked the operator to configure an allowlist of symbols, a minimum order volume and a default volume per symbol. That put broker facts and risk decisions in the same configuration file: the minimum order and its step are properties of the instrument (lot size, leverage, contract size), while the amount to risk is a decision of the risk model. Keeping them together would have made the application the owner of facts it does not own and the operator the author of numbers the model should derive.
- Decision:
  - **Symbols are what the provider describes.** The application keeps no allowlist of its own: a symbol that the provider does not describe as tradable is not traded, and an instrument the provider does not offer cannot appear in an order.
  - **Minimum volume, step, maximum, lot size and pip size per unit are provider facts**, delivered by the data seam (`IMarketDataSource.DescribeAsync`) and never configured as operating policy. They differ per symbol, which is exactly why they cannot be a constant in the application.
  - **The volume is computed by the risk model**: risk amount = equity × leg risk cap, divided by the value of the stop distance, then rounded **down** to the instrument step. It is never rounded up to reach a minimum, and never invented: a size below the instrument minimum is refused with its reason.
  - The risk model owns the only sizing input that is a decision rather than a fact: the **stop distance in pips**, which is a **property of the leg inside the basket**, decided with the rest of the composition and frozen into the version. It is not a deployment setting: how far a leg may run against us is a decision about that instrument in that basket, it must be visible where the basket is edited, and it must age with the version rather than with the server.
  - A leg may exist without a stop distance while the basket is being composed: the refusal belongs to the sizing, which produces no volume and therefore no order, and it is reported as `stop_distance_not_configured` instead of blocking the draft.
  - Currency conversion is required only when neither the base nor the quote currency is the account currency; without a rate supplied by the provider, no volume is produced and nothing is sent.
- Consequences: The boundary between what the broker knows and what we decide stays visible in the code, and the simulated provider carries demo instrument specifications that the real provider will simply replace, exactly as with market data (ADR-0015) and order execution (ADR-0018). The cost is one more provider responsibility to model, and one more fail-closed input (the stop distance) that the operator must decide before a single order can be prepared.

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
- Status: Superseded by ADR-0030
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
- Status: Partially superseded by ADR-0030
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
- Status: Superseded by ADR-0021
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

## ADR-0015 - Simulated market and account data behind a seam

- Date: 2026-09-19
- Status: Accepted (2026-09-19)
- Context: The Risk Engine is complete but starved. `RiskQueryHandler` builds its input from the database and reports no market snapshot, so every decision is `Block` on `SnapshotMissing` and no leg, coverage, basket-risk or daily-loss gate can ever be judged. The real feed depends on the cTrader application being approved, which is outside our control and can take days. Development cannot be suspended on an external approval, but producing market values as if they were real would silently turn a simulation into a trading decision.
- Decision (proposed):
  - Introduce the seam `IMarketDataSource` in the Handlers tier, producing one coherent capture: capture timestamp, per-symbol spread, volatility and executability, plus the account values needed to compute basket risk and daily loss. `RiskQueryHandler` and the analysis cycle consume that interface and nothing else.
  - Two implementations behind the seam: `SimulatedMarketDataSource` for development and demo, and a broker implementation that stays unavailable until the application is approved. Which one is used is decided by `Trading:MarketData:Source` (`None` | `Simulated` | `Broker`), defaulting to `None`.
  - `None` is the default and means no data: the gates block exactly as they do today. The simulated source is inert unless it is explicitly configured, so a deployment cannot start with invented values by accident.
  - The simulation is transparent to the application. No contract, no decision, no journal payload and no screen carries a "simulated" marker, because a marking that changes behaviour or wording would falsify the very behaviour being developed and tested. Where a source is in force is an operational fact, answered by the configuration and by one startup log line, not by the domain.
  - The simulation is deterministic: a configured seed and configured symbol profiles decide the sequence, so a demo or a test can be replayed and a gate path can be forced deliberately (spread near the limit, symbol unavailable, volatility spike).
  - One hard boundary, checked where the application cannot miss it: with a source other than the broker no Live account may exist, and startup fails if one does. The check belongs to the host, not to the domain, and it is the only place that looks at the selected provider.
  - The simulation does not implement the cTrader protocol or its transport: it is an in-process data source, not a fake server. Verifying the protocol client stays the job of the broker integration and is not claimed by this ADR.
- Consequences: The whole risk and analysis surface becomes exercisable and demoable before the external approval. When the application is approved, the simulated source is deleted and only the implementation behind the seam changes: no contract, no handler and no screen has to be touched, because nothing above the seam ever knew the difference. In exchange, the swap must be remembered as the only step of the transition, and the `None` default plus the Live-account guard are what keep an unconfigured or misconfigured deployment from reporting a market nobody observed.


## ADR-0021 - Leg risk limits belong to the leg

- Date: 2026-09-19
- Status: Accepted
- Supersedes: ADR-0013
- Context: The maximum spread and maximum volatility were configuration keys per market (`Trading:Risk:Markets:{Market}`). Two problems surfaced at verification time. First, the same engine kept half of its policy in the basket version (risk per basket, daily loss, minimum coverage, failure policy) and half in a deployment file, so changing a limit left no trace in the version history and none in the journal, while the other half was versioned and journalled. Second, the deployment file declared the market taxonomy, duplicating a classification the provider catalogue already owns. The rule confirmed by the operator: a value that shapes a decision about a leg belongs to the leg; configuration may only hold defaults or a simulator scenario.
- Decision:
  - `MaxSpreadPips` and `MaxVolatilityPercent` live on the leg and follow the stop-distance chain: `BasketCompositionLegDto` → `BasketDraftLeg` → `BasketVersionLeg` → `ProposalLeg`/`ProposalLegDto`.
  - Zero means "not decided". The composition accepts it because the operator is still composing; `RiskInputFactory` turns it into an absent limit and `RiskEngine` blocks that leg with `RISK_THRESHOLD_NOT_CONFIGURED`. A leg never borrows a limit from another leg or from its market.
  - `RiskCandidateLeg` and `RiskEvaluationLeg` carry the limits; `RiskEngine` no longer resolves any threshold from a market.
  - `RiskThresholds` keeps only `SnapshotMaxAgeSeconds` and `IsConfigured`. The window describes the freshness of the feed, not a decision about a basket, so it stays a deployment property; if snapshots ever become per market, it follows them.
  - `Trading:Risk:Markets` and the related `RiskConfigurationKeys` entries are removed. `appsettings.json` and `appsettings.Local.json` keep only `SnapshotMaxAgeSeconds`.
  - `MarketRiskLimitsDto` is deleted; `RiskLimitsDto` becomes `SnapshotMaxAgeSeconds` + `IsConfigured`, which narrows the ADR-0014 bullet that described one entry per market.
  - Accepted range: spread `0..100000` pips, volatility `0..100` %. Outside it the composition is rejected as a value that cannot mean anything.
  - The `LegRiskLimits` migration adds the columns and carries the approved values onto the existing drafts, versions and proposals by market (Fx 1.5 / 0.35, Metal 40 / 0.8, Index 5 / 0.6), so no leg is judged differently before and after the deploy.
- Consequences: changing a limit now requires publishing a version, which makes it versioned, journalled and visible in the proposal history like every other decision about a leg. Two legs of the same market can be judged by different limits, which is the point. The market taxonomy disappears from configuration. The `Soglie di verifica` panel shows only the snapshot window and states where the leg limits are decided, and the basket leg table carries the two new columns.

## ADR-0022 - The strategy is the version policy, not a second entity

- Date: 2026-09-19
- Status: Accepted
- Context: The frozen prototype has a "Strategy Lab" holding an entry mode, a risk-per-basket limit, a daily-loss limit, an LLM guardrail and a promotion timeline. The first two limits already exist and already govern the risk gate as the basket version policy. A separate strategy entity would have given the screen and the gate two copies of the same limit, which is precisely the failure this codebase keeps avoiding; and the prototype's promotion timeline (valid spec, walk-forward, shadow trading, demo account) proposes steps no slice delivers, while the roadmap already defines the promotion gate as seven explicit requirements.
- Decision:
  - The strategy **is** the version policy. `BasketPolicyDto`, `BasketDraftPolicy` and `BasketVersionPolicy` gain `EntryMode` (`RegimeMomentum` default, `Momentum`, `MeanReversion`) and validation refuses a value the vocabulary does not know.
  - `Proposal` freezes the declared entry rule of the version it came from, and the queue and the detail report it through one shared builder: a field added to the contract can now be added in exactly one place, because building the row twice is what let the two disagree.
  - `BasketDetailDto.ActivePolicy` carries the frozen policy of the active version next to the draft. The Strategy screen shows both, so a rule change reads as preparing the next version instead of as taking effect immediately.
  - The entry mode is a **declared, versioned, audited parameter**, not an engine. The directional rule it names needs a price series, which the deterministic source does not have; it is exercised by the evidence source of the RAG slice. The screen states this instead of implying that the selector changes proposals.
  - The promotion path is the seven requirements recorded in the roadmap, read-only through `GET /api/operations/promotion`, each with a measured state: `Satisfied`, `NotSatisfied` or `NotVerifiable`. A requirement needing an approval, a drill or a review is never reported as met merely because nothing contradicts it, and an empty execution history does not count as a clean period.
  - The prototype's "DSL v0.3" is not implemented: no rule language exists, and inventing one to match a prototype label would be scope nobody asked for. The screen shows the version the rules belong to instead.
- Consequences: one limit, one home, and a limit change now requires publishing a version, so it is versioned and journalled like every other rule. The screen can no longer promise behaviour the engine does not have. The cost is a visible asymmetry: the entry mode is stored and reported but changes no verdict yet, and it must be reported as such until the evidence source exists.

## ADR-0023 - The semantic memory is an evidence source behind an availability-carrying seam

- Date: 2026-09-20
- Status: Accepted
- Context: Slice 7 needs a retrieval source for episodes and a local model for structured rationale. The prototype assumed JigenDB and Ollama; neither was a proven dependency in this repository. An early spike found that `Jigen.Store` and `Jigen.Indexer.HNSW` declared `Jigen.Primitives` as a dependency while it was published nowhere, so restore failed with `NU1101` on both 1.2.3 and 1.3.0; the cause was a pack step in the library's own pipeline pointing at a path that does not exist. The publisher resolved it, and `Jigen.Primitives` now exists at 1.3.1 only, so the versions before 1.3.1 remain unrestorable and are not referenceable. A spike on 1.3.1 resolved from nuget.org alone passed ranking on known vectors, persistence across reopen, a backup by directory copy, single-writer refusal and typed collection.
- Decision:
  - `Jigen.Store` 1.3.1 is a real dependency of `AutoTrade.Trading.Handlers`, with `MessagePack` 3.1.7 added explicitly because `[MessagePackObject]` is used directly rather than only transitively.
  - The seam is `IJigenEvidenceStore` (upsert of evidence, top-k retrieval with metadata). Availability travels **in the result** (`EvidenceSearchResult.IsAvailable`), so "the store is not configured" can never be read as "no episode matches". `UnavailableEvidenceStore` is registered when no provider is configured and every call returns unavailable.
  - `EvidenceModule.EnsureProviderIsUsable` runs at startup and the store is also resolved eagerly, so a configured store that cannot be opened stops the host instead of surfacing as a failed retrieval hours later, when a degraded memory could quietly change an outcome. `GET /api/operations/status` reports provider and availability, and the client shows the state.
  - Two engine behaviours are owned by the adapter, not leaked to callers: the store does not create its database directory, and when the directory is missing it reports *"already open in another Store instance or process"*, which is the wrong cause; and one path is openable by exactly one `Store`, which is why the registration is a singleton.
  - No backup API exists, so backup is a consistent copy of the **closed** directory (content, vectors, index, wal, lock). This is documented rather than wrapped in an API that does not exist.
  - Semantic memory holds no transactional state, decides no verdict and never reaches the order gateway. A retrieval result is evidence input to analysis; it cannot authorise an order, and the fail-closed requirement on a stopped store or model is enforced at the consumer, not by the store.
- Consequences: the semantic memory is observable and its absence is explicit rather than silent, which is the prerequisite for the fail-closed rule of the analysis slice. The cost is a pinned minor version (1.3.1) with no fallback to 1.2.3, so the dependency cannot be downgraded, and the store's lifecycle quirks remain the adapter's responsibility to keep encoded and covered by tests.

## ADR-0024 - The local model produces a validated opinion, never a value

- Date: 2026-09-20
- Status: Accepted
- Context: The prototype assumed a local model producing a rationale alongside the measured facts. A model is a probabilistic component: it can ignore a schema, name an instrument that was never under analysis, or answer with a value outside the vocabulary. It also has to be installed, which is a deployment decision rather than an application one. Meanwhile the platform must keep the rule that no verdict, size or limit comes from anything but the engine, and that a missing capability fails closed instead of quietly weakening an outcome.
- Decision:
  - The engine is Ollama over loopback and the model is a configuration value (`Trading:Ollama:Model`). Nothing is downloaded at run time: the model must already be present, and `EnsureModelIsPresentAsync` checks it at startup. A model that is configured but absent, or an engine that is not running, aborts startup rather than degrading silently; the second case was verified live by stopping the engine and observing the host refuse with an explicit message.
  - The answer is requested against a JSON schema and then validated field by field: the schema raises the chance of a usable answer, it is not what makes the answer trustworthy. The symbol must match the one under analysis and be inside the allowlist, the bias must be a defined member of the vocabulary (a numeric string such as "7" parses successfully and is therefore rejected explicitly), the confidence must be inside 0..1, and the rationale must be non-empty and inside the configured length. The answer body is bounded while it is read, so a model that never stops cannot be buffered in full before being refused.
  - The outcome carries availability and has exactly two shapes: an opinion that passed every check, or no opinion with a reason. A rejected answer keeps `IsAvailable = true` with a null opinion, because "the model is there and said nothing usable" and "there is no model" are different facts that must not be collapsed.
  - The input is versioned (`PromptVersion`). Changing the wording of the instruction is changing the version, so an opinion produced under an old instruction stays distinguishable from a new one.
  - The model never sets a size, a limit or a verdict, never authorises an order, and never reaches the order gateway. Its output is advisory input to analysis, in the same position as a retrieval result.
  - The live check against the real model is an opt-in test (`AUTOTRADE_OLLAMA_LIVE=1`) tagged `Live`. It is excluded from any coverage claim, because when it returns early it asserts nothing: a deployment decision must not become a requirement of the default suite, and a test that passes by doing nothing must not be reported as evidence it did not gather.
- Consequences: the analysis seam is real and verified end to end against the model this deployment will run, and it cannot leak an unvalidated value into the decision path. The cost is a hard startup dependency: with the provider set to Ollama the host will not start without a reachable engine, so an operator who does not want that must leave the provider at None, which is the supported way to run without analysis.

## ADR-0025 - Embedding runs in process, and the engine is part of the identity of a space

- Date: 2026-09-20
- Status: Accepted
- Context: Semantic memory needs vectors, and a generative model does not produce them: `qwen2.5:3b` answers questions, it does not embed. The first path explored was Ollama's `/api/embed` with `nomic-embed-text`, which was installed and measured against the real store (768 dimensions, semantic ranking, survives reopen). That path works, but it puts a second process and a loopback service in the middle of a capability that is structural: retrieval would depend on an engine being up, and an engine being started with the right flags, for numbers that the store itself could have produced. The owner of the system asked for the structure to work atomically instead: one process, no external engine.
- Decision:
  - Embedding runs **in process** with Jigen's own ONNX runtime (`Jigen.SemanticTools`, `OnnxEmbeddingGenerator`), the same library that ranks the vectors. The checkpoint is a file on disk; nothing is downloaded at run time and no service is contacted.
  - `EmbeddingEngineKind` has exactly two members, `Unset` and `JigenOnnx`. There is deliberately **no member for an externally hosted engine**: the decision that embedding is in process is expressed by the type, so it cannot be reversed by a configuration value someone copies from an old file.
  - The engine, the model and the text version are part of a collection's identity: `{name}@{engine}@{model}@{textVersion}`. The same model run by two engines is not guaranteed to produce the same numbers, because quantisation and preprocessing differ, and because the task prefix matters: `search_document:` and `search_query:` are different vectors for the same text. An incomplete collection cannot even be named, so two incompatible spaces cannot merge by accident.
  - The two roles are exposed separately by the seam (`EmbedDocumentAsync`, `EmbedQueryAsync`) instead of one method with a flag, because mixing them up degrades retrieval silently rather than failing.
- Consequences: retrieval cannot depend on a running service, and the vectors compared are the ones produced by the code that owns them. Two costs are accepted. First, the ONNX path needs the checkpoint files (`model.onnx`, tokenizer) which are not in the Jigen repository and must be provisioned like any other model artefact: they are supplied by the deployment and are **still outstanding**, so the seam exists and refuses to embed until they are configured. Second, it needs `Jigen.SemanticTools` as a package, which was **not published** on nuget.org or on the private feed when this decision was taken, the same class of blocker that `Jigen.Primitives` was the day before; the publisher resolved it and **1.3.2 is published** together with the rest of the family, so `Jigen.Store` and `Jigen.SemanticTools` are both on 1.3.2 and a `ProjectReference` to a working copy was never needed.
  - The live embedding test that used Ollama was removed rather than adapted: it declared an engine that is no longer the production one, and keeping it would have required labelling Ollama-produced vectors with an ONNX identity. The size guarantee it carried is not lost: a store test now writes and ranks 768-dimensional vectors with no external engine, and the semantic-ranking measurement will be repeated on the real ONNX adapter once the checkpoint is in place.

## ADR-0026 - An episode is a concluded operational event, not a closed trade

- Date: 2026-09-21
- Status: Accepted
- Context: The documents used "episode" for two different things. `TechnicalAnalysis` defines it as a closed trade derived from reconciled deals (`Id, VersionId, ExecutionId, outcome, PnL, R, MAE/MFE`), which is what the Win/Loss History of slice 8 needs. But semantic memory is consulted **before** a decision, while a closed trade is only known **after**: the two have opposite lifecycles. Two further constraints were measured on the real database rather than assumed. First, the economic episode cannot be built today (no reconciled deals, and deriving one from simulated executions would put experiences that never happened into memory, where retrieval would treat them as real). Second, 5228 of 5250 proposals expire without producing anything memorable, so memorising every analysis cycle would bury the seven cases that matter under a day's worth of noise.
- Decision:
  - An episode is **`OperationalEpisode`**: a concluded operational event whose outcome is known at the moment of writing, described together with the **measurable context** it happened in. The term `Episode` is left to the closed trade of the Win/Loss History, so the two concepts cannot be confused again.
  - Relevance: **anomalies plus successful executions**, meaning a blocking gate, an execution closed without nominal coverage, a compensation, a refusal, a kill switch change, and an execution completed nominally. Cycles that produce nothing are not episodes: the absence of an event is not an experience.
  - Written **when the outcome is known**, because the memory is append-only and a vector is never rewritten: an episode opened at the start of an execution could never learn how it ended.
  - The context is not decoration: a retrieval compares situations, so the numbers that were measured travel in the text (coverage, snapshot age, daily loss, and spread and volatility per leg against their limits). The `Detail` the engine itself produced is quoted verbatim; the memory never paraphrases a rule.
  - The wording is versioned (`OperationalEpisodeRenderer.TextVersion`) and rendered under the **invariant culture**. The version is part of the collection name, and a comma-decimal locale would otherwise change the text, and therefore the vector, for a reason invisible in the code.
  - Writing to memory is **auxiliary**: a memory that is unavailable or failing never blocks or fails the transactional operation that produced the episode.
- Consequences: the memory can be populated with facts the system already records, and a retrieval can contrast a new situation with situations that were actually measured. The cost is that the two meanings of the word stay distinct in the codebase, and that the economic outcome will arrive later as a separate concept, linked by reference rather than by rewriting the episode that was already stored.

## ADR-0027 - cTrader read-only snapshot stays behind the market-data seam

- Date: 2026-09-21
- Status: Accepted
- Context: The cTrader application is now approved, so Slice 3 can be completed in code. The provider owns account balances, instrument rules, prices, positions and pending orders; the application must not reproduce those facts or send orders from the connectivity slice.
- Decision:
  - The vendored protobuf schema is driven by our own WebSocket client. A snapshot reader owns app/account authentication, token rotation, trader/assets/symbols, reconcile, daily deals, unrealized PnL and requested spot events.
  - `CtraderMarketDataSource` implements the existing `IMarketDataSource` seam. It maps provider scales once, returns unavailable on incomplete data, and leaves volatility absent because these messages do not publish a volatility percentage.
  - Refresh is preventive and rotates both tokens, encrypting and persisting the pair before the new access token is used. A missing or ambiguous account is fail-closed; no account is guessed.
  - `GET /api/broker/snapshot` exposes a secret-free projection of account, symbols, positions and pending orders. Reconciliation is read-only and no execution gateway is enabled by this slice.
- Consequences: the risk path can select cTrader through configuration without changing contracts, and the remaining live check is operational (credentials, OAuth grant, demo account and reconnect duplicate check). Token/account metadata is persisted by the credential boundary as part of refresh/selection; this is lifecycle bookkeeping and never changes a risk verdict.

## ADR-0028 - cTrader execution is a guarded, idempotent market-order adapter

- Date: 2026-09-21
- Status: Accepted
- Context: The cTrader application is approved and the existing `IExecutionGateway` already defines the only boundary through which the Execution Engine can send a leg or reconcile an uncertain order. The provider requires an OAuth grant with `trading` scope, exposes market-order execution events asynchronously, and identifies orders with a caller-supplied `clientOrderId`; retrying an unknown response would risk duplicate exposure.
- Decision:
  - `CtraderExecutionGateway` uses the existing app/account authentication, encrypted credential storage and protobuf WebSocket client. It sends market orders only, passes through the deterministic idempotency key created by the Execution Engine, and never writes transactional execution state.
  - Provider execution events are mapped only when they carry a matching order and a usable order/deal identity. Missing or unsupported information becomes `NoResponse`, so the handler moves to reconciliation rather than guessing.
  - Reconciliation reads the provider order list for the recent window, rejects truncated or ambiguous matches, and reports `Unknown` when the provider cannot identify exactly one order. It never retries or sends a replacement order.
  - The default environment is Demo. Live execution is a separate promotion guarded by `Trading:Execution:Ctrader:AllowLive=true` and an OAuth scope containing `trading`; no deployment enables it implicitly.
- Consequences: the real gateway now exists behind the same seam as the simulator and can be exercised with approved demo credentials. The transport, event mapping and reconciliation logic are locally tested, but provider behavior, reconnect deduplication and live promotion remain operational checks rather than claims made by the default test suite.

## ADR-0029 - cTrader recovery is read-only and volatility is derived from closed bars

- Date: 2026-09-21
- Status: Accepted
- Context: A broker adapter that only handles the happy-path response is not operational after a disconnect or restart, and cTrader spot messages do not provide the volatility percentage required by the deterministic risk gate.
- Decision:
  - The WebSocket runtime owns serialized writes, correlation, heartbeat and bounded reconnect. After reconnect it re-authenticates and restores subscriptions, but it never retries an order-placement request.
  - Volatility is derived from the latest 96 closed M15 trendbars using close-to-close logarithmic returns, sample standard deviation and annualisation by `sqrt(252 * 96)`. Insufficient, stale, duplicate, future or malformed bars fail closed.
  - `ReconcileExecutions` is the only recovery command. Its worker is disabled by default and configured under `Trading:Execution:Reconciliation`; a pass queries non-terminal legs, deduplicates broker events and atomically updates legs, executions and the account reconciliation watermark.
  - Reconciliation never sends or retries an order. Unknown, ambiguous or incomplete broker state leaves the account and execution in `ReconciliationRequired`.
- Consequences: the real market-data path can satisfy the volatility gate without inventing a provider value, and restart/reconnect recovery has an explicit write owner. Live provider behaviour still requires the approved demo-account drill before the implementation can be declared operational.

## ADR-0030 - Operator authentication uses the persisted session as an opaque Bearer token

- Date: 2026-09-21
- Status: Accepted
- Supersedes: ADR-0010 and the cookie/antiforgery parts of ADR-0011
- Context: Cookie authentication and antiforgery made the development client dependent on browser site rules and caused a successful login response to be followed by unauthorized API calls when client and API hosts differed. The owner explicitly requires Bearer authentication and removal of antiforgery.
- Decision:
  - `LoginOperator` continues to create the audited, expiring server-side session and the login endpoint returns its random 256-bit token once as `accessToken` beside the session projection.
  - Every protected request carries `Authorization: Bearer <token>`; a custom ASP.NET Core authentication handler resolves the token against the persisted session and rejects ended, expired or inactive-operator sessions.
  - The client keeps the token in `sessionStorage`, injects the header centrally through Axios, restores the authoritative session with `GET /api/session`, and clears the token on logout or failed restoration. `localStorage` is not used.
  - Cookie authentication, antiforgery registration, antiforgery endpoints, filters and headers are removed. There is no weaker fallback authentication path.
- Consequences: authentication no longer depends on SameSite or credentialed CORS cookies, while immediate server-side revocation and audit semantics remain unchanged. As with any browser-readable Bearer token, preventing script injection remains a security boundary.
