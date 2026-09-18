# Roadmap di implementazione

Status: proposta per Gate 3  
Strategia: vertical slice demo-first

## Principi

- `prototipe/` resta congelato e viene usato solo per confronto UX.
- Ogni slice termina con server, client, test e criterio osservabile integrati.
- I contratti server sono definiti prima del client e sono canonici.
- Nessuna migration EF viene generata o applicata dall'agente.
- Nessun ordine viene inviato finche autenticazione, persistenza, idempotenza e riconciliazione non sono verificate.
- Il conto live resta fuori scope fino al gate di promozione.

## Slice 0 - Fondazioni

Output:

- nuovo `server/` con i quattro progetti, test project e solution;
- nuovo `client/` Vue 3 TypeScript senza codice del prototipo;
- configurazione, logging, health check, error pipeline e CI locale;
- SQLite configurato WAL senza migration generata;
- shell autenticata minimale e runtime settings Axios.

Exit criteria: build e test di entrambi i progetti; health server; pagina client autenticata; nessun riferimento sorgente a `prototipe/`.

## Slice 1 - Sessione e stato operativo

Output:

- bootstrap sicuro dell'operatore locale;
- login, cookie session, logout, lockout e antiforgery;
- stato operativo e kill switch persistito;
- route guards e schermata di stato client;
- audit login/logout/kill switch.

Copre: AC-01, parte di AC-13.

## Slice 2 - Basket lifecycle

Output:

- create, list, detail, rename, clone;
- update separati per composizione e policy;
- publish immutabile, activation transaction e archive;
- Basket Builder di produzione integrato ai contratti reali;
- handler/unit/integration test per invarianti e ownership.

Copre: AC-02, AC-03, AC-04.

Gate dati umano: approvare chiavi, lunghezze, nullability e soglie della policy prima di creare le entity definitive.

## Slice 3 - cTrader connectivity e reconciliation read-only

Output:

- OAuth callback server-side e storage cifrato token;
- application/account auth sul solo demo approvato;
- symbol/account snapshot e stato connessione;
- reconnect, refresh token, reconcile di posizioni e pending order;
- journal e UI degli stati degradati;
- nessun invio ordine.

Copre: parte di AC-08, AC-12, AC-15.

Exit criteria: restart e reconnect ricostruiscono lo stesso snapshot demo senza duplicati.

## Slice 4 - Risk Engine deterministico

Output:

- snapshot input versionato;
- gate result strutturati e auditabili;
- test a tabella sui boundary;
- visualizzazione gate e motivazioni nel client;
- fail-closed per dati mancanti o stale.

Copre: AC-08, AC-13, AC-17.

Gate funzionale umano: approvare formule, soglie, timezone e sizing elencati in `FunctionalAnalysis.md`.

## Slice 5 - Execution Engine demo

Output:

- proposta deterministica di test senza LLM;
- persist-first execution e clientOrderId idempotente;
- invio sequenziale di ordini demo;
- gestione execution/error event, timeout e reconcile-before-retry;
- fill parziali, policy e compensazione esplicita;
- UI execution e test end-to-end sul demo.

Copre: AC-09, AC-10, AC-11, AC-12.

Gate sicurezza: ordine minimo e simbolo consentito devono essere approvati prima del primo test broker con effetto reale sul demo.

## Slice 6 - Market Manager

Output:

- start/stop analisi e modalita manual/supervised/automatic;
- proposal state machine, TTL e decisioni operatore;
- automatic dispatch solo da `Pass` e mai da `Review`/`Block`;
- code, dettaglio e stati degradati nel client;
- audit completo.

Copre: AC-05, AC-06, AC-07.

## Slice 7 - JigenDB e Ollama

Prerequisito: spike JigenDB superato con API, persistenza, backup e retrieval verificati.

Output:

- evidence store isolato;
- retrieval versionato e tracciato;
- Ollama structured output con schema e allowlist;
- proposta scartata/sospesa su errore senza fallback permissivo;
- nessun riferimento dell'adapter LLM al gateway ordini.

Copre: AC-06, AC-16.

## Slice 8 - Storico e readiness demo

Output:

- episode builder da deal riconciliati;
- Win/Loss History, metriche server-side e Decision Journal;
- backup/restore test, soak test, recovery drill e controllo performance SQLite;
- verifica completa AC e rapporto di readiness demo.

Copre: AC-14, AC-15 e regressione AC-01..17.

## Gate di promozione live separato

La promozione non fa parte dell'MVP e richiede almeno:

- periodo demo e criteri quantitativi approvati;
- zero divergenze di riconciliazione irrisolte;
- recovery drill e restore test superati;
- limiti live, simboli, volumi e modalita operativa approvati;
- revisione sicurezza e gestione segreti;
- procedura di rollback a demo e kill switch provata;
- ADR dedicato e approvazione esplicita.

## Traceability sintetica

| Slice | Criteri principali | Evidenza |
| --- | --- | --- |
| 0-1 | AC-01, AC-13 | Build, API auth test, browser route guard |
| 2 | AC-02..04 | Handler/integration test + workflow browser |
| 3-4 | AC-08, AC-12, AC-17 | Demo reconcile test + risk boundary tests |
| 5 | AC-09..11 | Demo broker E2E + duplicate/timeout tests |
| 6 | AC-05..07 | State-machine tests + browser workflow |
| 7 | AC-16 | Adapter failure tests + dependency check |
| 8 | AC-14..15 | Reconciled episode test + audit trace |
