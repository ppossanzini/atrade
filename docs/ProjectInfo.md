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
