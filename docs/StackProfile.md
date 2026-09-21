# Stack Profile

Status: locked
Detected: 2026-09-18 | Evidence: greenfield workspace and confirmed user choices

| Concern | Decision | Evidence | Owning skill |
| --- | --- | --- | --- |
| frontend framework | Vue 3 with TypeScript for `client/`; Vue 3 with JavaScript only in frozen `prototipe/` | Gate 1 scope update | fe-vue-dev |
| component library | Element Plus | Approved component list | fe-vue-element-plus |
| build | Vite | Prototype baseline and production client approval | fe-vue-dev |
| state management | Pinia | Interactive mock workflow | fe-vue-dev |
| routing | Vue Router | Approved multi-view prototype | fe-vue-dev |
| i18n | Vue I18n, Italian default | UI language and frontend baseline | fe-vue-dev |
| styling | LESS and semantic CSS tokens | Element Plus conventions | fe-vue-element-plus |
| data source | Local mock data in prototype; REST API in production | Gate 1 approval | fe-axios-dev |
| backend platform | .NET 10 LTS, ASP.NET Core Web API and Worker Services | Gate 1 approval | be-dotnet-dev |
| market data source | `IMarketDataSource` with a simulated implementation for development; broker implementation behind the same seam once cTrader approves the app | Controlled change 2026-09-19: development must not wait for an external approval | td-backend-dev |
| mediator | Hikyaku CQRS | Gate 1 approval | be-dotnet-hikyaku |
| mapping | MapZilla | Backend organization convention | be-dotnet-dev |
| persistence | EF Core with SQLite WAL on one Linux host | Gate 1 approval | be-dotnet-dev |
| test stack | xUnit with handler and integration tests | Gate 1 approval | be-dotnet-dev |
| broker integration | Official cTrader Open API **protobuf schema** vendored and driven by our own protocol client (`Google.Protobuf` + `Grpc.Tools`), not the official C# SDK | ADR-0016 supersedes the SDK choice of ADR-0006 | td-backend-dev |
| background processing | .NET Worker Services in the deployed backend process | Gate 1 approval | be-dotnet-dev |
| RAG | `Jigen.Store` 1.3.1 (+ `Jigen.Primitives` 1.3.1, MessagePack 3.1.7) embedded, isolated from transactional persistence, behind `IJigenEvidenceStore` | User constraint; version pinned by ADR-0023 (the pre-1.3.1 family is not published and is not referenceable) | td-backend-dev |
| local LLM | Ollama over local-only HTTP, model `qwen2.5:3b`, behind `IOllamaAnalysisClient`; the model is never downloaded at run time and its absence aborts startup | User constraint; model chosen 2026-09-20 with the user's approval | td-backend-dev |
| embeddings | In-process ONNX via `Jigen.SemanticTools` **1.3.2**, the same runtime that ranks the vectors; checkpoint `nomic-embed-text-v1.5` supplied by the deployment. The seam is written and fails closed until the checkpoint is provided | Owner decision 2026-09-20: the structure must work atomically, no external engine (ADR-0025) | td-backend-dev |
| deployment | Single Linux host, continuously running | Gate 1 approval | td-backend-dev |
| account rollout | One demo account, live enabled only through promotion gate | Gate 1 approval | td-backend-dev |
| authentication | Local operator login with audited session | Gate 1 approval | fe-security-layer-constraints |
| maps/GIS | Absent | Out of scope | fe-vue-esri-map -> skipped |
| HTTP client | Axios for production REST integration | Production scope approved | fe-axios-dev |
| auth/security | Single operator role in MVP | Gate 1 approval | fe-security-layer-constraints |
| accessibility QA | Deferred | User declined formal gate | td-accessibility-check -> skipped |
