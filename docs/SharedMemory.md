# Shared Structural Memory

- Keep the immutable visual prototype in `prototipe/` and implement production code only in new `client/` and `server/` projects.
- Never copy mock stores, simulated execution logic, or prototype-only contracts into production code.
- Use a dense trading-workbench layout with independent sidebar and content scrolling.
- Make Basket Builder the primary decision surface.
- Use Element Plus controls for all interactive Vue widgets.
- Keep user-facing text in Vue I18n dictionaries.
- Use mock contracts that describe the future domain without implementing integrations.
- Keep Win/Loss performance history separate from the chronological Decision Journal.
- Manage multiple named and versioned baskets through UI-owned lifecycle commands.
- Keep exactly one basket active for future execution, independently from the basket selected for editing.
- Archive baskets non-destructively so historical orders, episodes, journal events, and JigenDB evidence retain valid references.
- Keep continuous market analysis separate from basket approval and composition changes.
- Route order proposals through a deterministic Market Manager with manual, supervised, and automatic modes; supervised is the default.
