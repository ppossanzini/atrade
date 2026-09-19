# Roadmap di implementazione

Status: Slice 0-2 consegnate; Slice 3 parzialmente consegnata e bloccata dall'approvazione cTrader; Slice 4 consegnata con gate umano sulle soglie ancora aperto
Strategia: vertical slice demo-first

## Stato di consegna

| Slice | Stato | Evidenza |
| --- | --- | --- |
| 0 - Fondazioni | Consegnata | build e test verdi su entrambi i progetti; health `200`; pagina autenticata |
| 1 - Sessione e stato operativo | Consegnata | login `200` / logout `204` / `401` dopo logout; kill switch persistito con operatore e motivo; 35 test |
| 2 - Basket lifecycle | Consegnata | 143 test; flusso completo verificato via HTTP e in browser; snapshot immutabili verificati a DB; `InitialCreate` applicata |
| 3 - Broker demo | Parziale, bloccata da fuori | codice completo e test verde; reachability TCP/wss e errore provider reali (`OA client is not in active state`); autenticazione, snapshot, riconciliazione e rinnovo token non verificabili finche l'app non e approvata |
| 4 - Risk Engine | Consegnata (gate umano aperto) | 269 test; 12 codici gate con codice, valore osservato, soglia e timestamp; limiti per mercato (ADR-0013); pannelli Risk gate e Soglie di verifica in browser, incluso il ciclo kill switch ingaggiato/rilasciato riflesso nei gate |

Le slice successive restano da consegnare.

## Principi

- `prototipe/` resta congelato e viene usato solo per confronto UX.
- Ogni slice termina con server, client, test e criterio osservabile integrati.
- I contratti server sono definiti prima del client e sono canonici.
- Lo schema appartiene a EF Core migrations, applicate all'avvio con `Database.MigrateAsync()`; `EnsureCreated` non e piu ammesso (ADR-0012).
- Nessun ordine viene inviato finche autenticazione, persistenza, idempotenza e riconciliazione non sono verificate.
- Il conto live resta fuori scope fino al gate di promozione.

## Slice 0 - Fondazioni

Output:

- nuovo `server/` con i quattro progetti, test project e solution;
- nuovo `client/` Vue 3 TypeScript senza codice del prototipo;
- configurazione, logging, health check, error pipeline e CI locale;
- SQLite configurato WAL; lo schema e poi passato a EF Core migrations con `InitialCreate` (ADR-0012);
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

Esito: gate chiuso il 2026-09-18 (nome simbolo come chiave, default di policy del prototipo, set di campi gamba confermato).

Verifica di consegna: build pulita; 143 test verdi; creazione, rinomina, clone, composizione, policy, pubblicazione, attivazione e archiviazione verificati via HTTP con codici `200/400/409`; ordinale delle gambe per risk cap decrescente poi simbolo; una sola versione attiva alla volta; un paniere con versione attiva non e archiviabile (`409`); vista Panieri verificata in browser contro lo stato reale del server.

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

### Blocco verificato: approvazione dell'applicazione (2026-09-19)

Il 19 settembre 2026 il probe di connettività ha contattato `demo.ctraderapi.com:5035` e ha ricevuto:

```
errorCode: CH_CLIENT_AUTH_FAILURE
description: OA client is not in active state
```

Segue che **il Playground non aggira l'approvazione**: i token che emette servono per un'applicazione gia attiva, e senza stato attivo il provider rifiuta persino la `ProtoOAApplicationAuthReq`. Finche l'app non e approvata, nessuna verifica live e possibile su demo, che resta comunque l'ambiente corretto di destinazione.

Conseguenza sulla pianificazione: le parti di Slice 3 che richiedono una connessione reale restano non verificabili. Le attivita che non dipendono dal provider sono:

- contratti, stato macchina e test di riconciliazione read-only;
- rinnovo token, reconnect e stati degradati;
- pannello di connessione e stati degradati nel client;
- Slice 4 (Risk Engine), interamente deterministico e indipendente da cTrader.

### Prerequisiti verificati (2026-09-18)

Fatti raccolti dalla documentazione ufficiale cTrader Open API:

- Consenso: `https://id.ctrader.com/my/settings/openapi/grantingaccess/?client_id=..&redirect_uri=..&scope=..&product=web`; scope ammessi `accounts` (sola lettura) e `trading` (completo).
- L'authorization code scade in **1 minuto** e va scambiato subito.
- Token: `https://openapi.ctrader.com/apps/token` con `grant_type=authorization_code` oppure `refresh_token`, piu `client_id` e `client_secret`.
- Risposta: `accessToken`, `tokenType`, `expiresIn` (default 2.628.000 s, circa 30 giorni), `refreshToken` (senza scadenza).
- Il refresh **invalida automaticamente** access token e refresh token precedenti: la rotazione deve essere atomica e persistita prima dell'uso.
- Dopo i token servono `ProtoOAApplicationAuthReq` (clientId e clientSecret), `ProtoOAGetAccountListByAccessTokenReq` (accessToken) e `ProtoOAAccountAuthReq` (`ctidTraderAccountId`).

Questioni aperte da chiudere prima di implementare:

1. **Parametro `state` non documentato.** La documentazione ufficiale dell'authorization URL elenca solo `client_id`, `redirect_uri`, `scope`, `product`: nessun `state` e nessun PKCE. La protezione anti-CSRF del callback prevista dall'analisi tecnica va quindi verificata empiricamente o sostituita da un correlatore generato da noi e validato a prescindere da cTrader. Va deciso prima di scrivere il callback.
2. **SDK .NET ufficiale non allineato.** `cTrader.OpenAPI.Net` 1.4.4 pubblicato il 2022-05-03, solo `lib/net6.0` (fuori supporto), dipendenze `Google.Protobuf` 3.20.1, `System.Reactive` 5.0.0, `Websocket.Client` 4.4.43. Il repository `spotware/OpenAPI.Net` risulta non archiviato con ultimo push 2024-06-28 ("Updated SDK to cServer 90"). Consumabile da `net10.0`, ma con dipendenze datate. Alternativa: implementare il protocollo direttamente sui messaggi Protobuf ufficiali.
3. **Input esterni necessari**: registrazione dell'app nel portale Open API con `client_id` e `client_secret`, redirect URI esatta, conto demo cTID abilitato e `ctidTraderAccountId`. Nessun valore puo essere inventato o simulato in produzione.

## Slice 4 - Risk Engine deterministico

Output:

- snapshot input versionato;
- gate result strutturati e auditabili;
- test a tabella sui boundary;
- visualizzazione gate e motivazioni nel client;
- fail-closed per dati mancanti o stale.

Copre: AC-08, AC-13, AC-17.

Gate funzionale umano: approvare formule, soglie, timezone e sizing elencati in `FunctionalAnalysis.md`.

Stato al 2026-09-19: consegnata lato server e client.

- Snapshot input versionato, gate strutturati e test a tabella sui boundary sono implementati; `Database.MigrateAsync()` e l'unico proprietario dello schema.
- La visualizzazione e nel client come pannello `Risk gate` (verdetto, versione e snapshot valutati, elenco gate con codice, oggetto, mercato, valore osservato, soglia, unita, timestamp e motivazione) piu il pannello `Soglie di rischio` (finestra snapshot e limiti per mercato con stato configurato/non configurato). Nessuno dei due duplica la logica del motore: la decisione arriva da `GET /api/risk/baskets/{basketId}` e le soglie da `GET /api/risk/limits`.
- Fail-closed confermato in esercizio: senza snapshot di mercato il verdetto e `Block` su `SnapshotMissing`; con il kill switch ingaggiato si aggiunge `Block` su `KillSwitchEngaged`; al rilascio il gate torna `Allow`.
- Resta aperto il gate umano: senza soglie configurate i gate sulle gambe restano bloccanti per progetto (ADR-0013) e nessun valore puo essere inventato in codice.

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
| 2 | AC-02..04 | 143 handler test; flusso HTTP `200/400/409`; snapshot a DB; workflow browser |
| 3-4 | AC-08, AC-12, AC-17 | Demo reconcile test + risk boundary tests; verifica in browser del pannello Risk gate (gate bloccanti su snapshot mancante e kill switch, con ritorno a `Allow` al rilascio) |
| 5 | AC-09..11 | Demo broker E2E + duplicate/timeout tests |
| 6 | AC-05..07 | State-machine tests + browser workflow |
| 7 | AC-16 | Adapter failure tests + dependency check |
| 8 | AC-14..15 | Reconciled episode test + audit trace |
