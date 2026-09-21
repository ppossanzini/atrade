# Roadmap di implementazione

Status: Slice 0-2 consegnate; Slice 3 parzialmente consegnata e bloccata dall'approvazione cTrader; Slice 4 consegnata con gate umano sulle soglie ancora aperto; Slice 5 e 6 consegnate; Slice 9 (Strategia) consegnata
Strategia: vertical slice demo-first

## Stato di consegna

| Slice | Stato | Evidenza |
| --- | --- | --- |
| 0 - Fondazioni | Consegnata | build e test verdi su entrambi i progetti; health `200`; pagina autenticata |
| 1 - Sessione e stato operativo | Consegnata | login `200` / logout `204` / `401` dopo logout; kill switch persistito con operatore e motivo; 35 test |
| 2 - Basket lifecycle | Consegnata | 143 test; flusso completo verificato via HTTP e in browser; snapshot immutabili verificati a DB; `InitialCreate` applicata |
| 3 - Broker demo | Parziale, bloccata da fuori | codice completo e test verde; reachability TCP/wss e errore provider reali (`OA client is not in active state`); autenticazione, snapshot, riconciliazione e rinnovo token non verificabili finche l'app non e approvata |
| 4 - Risk Engine | Consegnata (gate umano aperto) | 269 test; 12 codici gate con codice, valore osservato, soglia e timestamp; limiti di gamba sulla gamba (ADR-0021, sostituisce ADR-0013); pannelli Risk gate e Soglie di verifica in browser, incluso il ciclo kill switch ingaggiato/rilasciato riflesso nei gate |
| 5 - Execution Engine | Consegnata (senza conto broker reale) | 385 test; persist-first con `clientOrderId` idempotente, invio sequenziale, dedup degli eventi broker, fill parziali, compensazione esplicita e journal di ogni avvio rifiutato; gateway simulato dietro `IExecutionGateway` (ADR-0018/0019); vista Esecuzione verificata in browser (coda, dettaglio gambe/eventi, compensazione con motivo obbligatorio). La verifica su conto demo autorizzato e la riconciliazione reale restano bloccate dall'approvazione cTrader |
| 6 - Market Manager | Consegnata | 340 test; ciclo di analisi reale con proposte, snapshot persistito e 10 valutazioni di gate per proposta; matrice di instradamento a tabella; decisioni con rivalutazione del gate e rispetto della modalita (ADR-0017); vista operatore verificata in browser (coda, dettaglio, rifiuto con motivazione obbligatoria) |

Le slice successive restano da consegnare.

### Cambio di scope controllato (2026-09-19)

Slice 3 e bloccata da un'approvazione esterna (app cTrader non attiva), che a cascata rende non giudicabili i gate di mercato dello Slice 4 e non verificabili i percorsi verdi dello Slice 6. Per non sospendere lo sviluppo su un evento fuori dal nostro controllo si introduce una sorgente dati simulata dietro la seam `IMarketDataSource` (ADR-0015, accettata).

Effetti attesi:

- i gate su snapshot, spread, volatilita, copertura, rischio per paniere e perdita giornaliera diventano giudicabili e visibili in esercizio simulato;
- il percorso `Allow`/`Review` diventa raggiungibile senza inventare dati reali, quindi non serve piu l'iniezione di sviluppo inizialmente prevista per lo Slice 6;
- nessun ordine e nessun conto live possono essere raggiunti da una cattura simulata, e la simulazione e inerte finche `Trading:MarketData:Source` non la abilita esplicitamente.

Resta invariato: la verifica del protocollo cTrader e della riconciliazione non e coperta dalla simulazione e attende l'approvazione dell'app.

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

**Esito dello spike (2026-09-19, `Jigen.Store` 1.2.3): superato.** Su 1.2.3, con artefatti pacchettizzati localmente dal tag `v1.2.3`:

- **API**: `Store` con WAL, `AppendContent`, `Search` con similarità coseno e `VectorCollection<T>` tipizzata; su tre vettori noti il ranking è quello atteso (`1.0000`, `0.9701`, `0.0000`).
- **Persistenza**: `SaveChangesAsync` + `Close` + riapertura + `ReconcileIndexAsync` restituiscono gli stessi hit.
- **Backup**: non esiste una API di backup; una copia consistente della directory a database chiuso (4 file: content, vectors, index, wal) si riapre e risponde come l'originale. La procedura va quindi definita esplicitamente.
- **Vincolo**: un database è apribile da un solo `Store` per path; il secondo tentativo fallisce con `IOException`. Va previsto nel ciclo di vita dell'host.

**Blocco lato pubblicazione (esterno ad AutoTrade).** `Jigen.Store` e `Jigen.Indexer.HNSW` dichiarano `Jigen.Primitives` come dipendenza, ma quel pacchetto **non è pubblicato su nuget.org a nessuna versione** (verificato per 1.2.3 e 1.3.0) né è presente sul feed `wisetown2022`: `dotnet add package Jigen.Store` non si risolve (`NU1101`). Il progetto si pacchettizza correttamente, quindi è un buco di pubblicazione, non di codice. Nella pipeline `.github/workflows/build-packages-and-container.yml` lo step aggiunto per `Jigen.Primitives` punta a `src/Client/Jigen.Primitives/...`, che non esiste: il progetto è in `src/Jigen/Jigen.Primitives/...` e `dotnet pack` esce con `MSB1009: Project file does not exist`. Poiché la pubblicazione avviene solo su push di tag, correggere il percorso non basta: la dipendenza **1.2.3** va ripubblicata rieseguendo il workflow sul ref del tag `v1.2.3` (con `--skip-duplicate` pubblica solo il pacchetto mancante), altrimenti AutoTrade deve passare a una versione la cui `Primitives` sia stata pubblicata.

Altri rilievi su 1.2.3, da tenere presenti nell'adapter: `DataBasePath` deve esistere (lo store non crea la directory) e una directory mancante viene riportata come "già aperto in un'altra istanza", che indica la causa sbagliata.

**Esito dello spike (2026-09-19, `Jigen.Store` 1.3.1): superato su nuget.org.** Chiuso il blocco, lo spike è stato ripetuto risolvendo **solo** da nuget.org: `Jigen.Primitives` esiste **unicamente alla versione 1.3.1**, quindi le versioni precedenti restano non ripristinabili e non referenziabili. Su 1.3.1 i cinque controlli passano: ranking su vettori noti, persistenza attraverso riapertura, backup per copia della directory che risponde in modo identico, rifiuto del secondo writer e collezione tipizzata. La dipendenza è stata quindi promossa da "contratto" a pacchetto reale (ADR-0023).

Output:

- evidence store isolato;
- retrieval versionato e tracciato;
- Ollama structured output con schema e allowlist;
- proposta scartata/sospesa su errore senza fallback permissivo;
- nessun riferimento dell'adapter LLM al gateway ordini.

**Stato dei primi due output (2026-09-20).** Lo store è isolato e reale (`Jigen.Store` 1.3.1, ADR-0023), con disponibilità riportata nello stato operativo. Lo structured output è implementato e verificato contro il modello reale (`qwen2.5:3b`, ADR-0024): schema inviato al motore, poi validazione campo per campo, esito a due sole forme (opinione validata oppure nessuna opinione con motivo), bound di byte e di tempo, fail-closed provato fermando il motore. Restano da consegnare: il retrieval versionato e tracciato (manca il modello di embedding, che non è lo stesso modello di analisi), il collegamento dell'opinione al percorso della proposta e lo stato degradato esplicito nel ciclo di analisi.

**Catena di embedding misurata (2026-09-20).** Il modello `nomic-embed-text` (768 dimensioni) è installato sul motore locale ed è stato misurato contro lo store reale: 768 dimensioni accettate e restituite integre, ranking coerente con il significato (non solo con la geometria) su episodi del dominio, metadati che sopravvivono al round trip, risultato identico dopo riapertura. L'unit test dello store usava vettori a 3 dimensioni, quindi questa parte era assunta e non verificata: adesso è verificata, in modo opt-in (`AUTOTRADE_OLLAMA_LIVE=1`, tag `Live`) e quindi esclusa da qualunque rivendicazione di copertura.

**Cambio di rotta sull'embedding, e blocco conseguente (2026-09-20).** Il proprietario del sistema ha chiesto che la struttura funzioni in modo atomico: embedding **in-process** con il runtime ONNX di Jigen, nessun motore esterno (ADR-0025). L'enum degli engine non ha quindi più alcun valore per un motore esterno, così la decisione è garantita dal tipo e non da una convenzione, e l'engine entra nella chiave della collezione insieme al modello e alla versione del testo. Il test live costruito su Ollama è stato rimosso perché dichiarava un motore che non è più quello di produzione: la misura della taglia reale (768 dimensioni) è rimasta come test dello store senza motore esterno, mentre la verifica del ranking semantico va rifatta sull'adattatore ONNX.

**Blocco risolto, seam di embedding consegnato (2026-09-20).** `Jigen.SemanticTools` **1.3.2 è pubblicato**, insieme al resto della famiglia (`Jigen.Store` e `Jigen.Primitives` portati a 1.3.2). Il seam è quindi scritto: `ITextEmbeddingSource` con i due ruoli separati (`EmbedDocumentAsync` e `EmbedQueryAsync`), `EmbeddingOptions` con profilo, execution provider e dimensione di output, l'adattatore `JigenOnnxTextEmbeddingSource` che possiede le due cose che il runtime non gestisce (esistenza del checkpoint e del tokenizer, e validazione del profilo invece del fallback a un default) e la sorgente esplicitamente indisponibile quando non c'è provider. `EmbeddingOptions.CreateCollection` è l'unico posto che compone la chiave della collezione, così un chiamante non può dimenticare metà dell'identità. Lo stato operativo riporta l'embedding **separatamente** dallo store: store aperto e sorgente assente significa che la memoria non può rispondere, e le due cose devono essere distinguibili.

**Cosa manca per chiudere la catena.** Il checkpoint (`model.onnx` più tokenizer) — fornito dal deployment, non ancora presente. Fino ad allora il seam esiste e **rifiuta di generare embedding**: nessun vettore di zeri, nessun fallback. Restano poi le due questioni di prodotto già annotate: cosa merita di essere ricordato come episodio e in quale punto della catena va scritto.

**Questioni aperte prima di scrivere il retrieval.** Due, e la seconda è stata scoperta leggendo il codice e non i documenti. Primo: cosa merita di essere ricordato come episodio e in quale punto della catena va scritto, e come si rende il testo di un episodio (la resa è versionata, quindi cambiarla apre una nuova collezione). Secondo: la separazione per modello/engine è ora strutturale nel nome della collezione, ma resta da decidere chi costruisce quella collezione e da dove arriva la versione del testo.

Copre: AC-06, AC-16.

## Slice 8 - Storico e readiness demo

Output:

- episode builder da deal riconciliati;
- Win/Loss History, metriche server-side e Decision Journal;
- backup/restore test, soak test, recovery drill e controllo performance SQLite;
- verifica completa AC e rapporto di readiness demo.

Copre: AC-14, AC-15 e regressione AC-01..17.

## Slice 9 - Strategia versionata

Perimetro: le regole che trasformano l'analisi in una proposta. La strategia **e la policy della versione di paniere**, non una entita separata: gli stessi limiti governano gia il gate di rischio, e una seconda copia permetterebbe alla schermata e al gate di dichiarare valori diversi.

Output:

- modalita di ingresso dichiarata nella catena della policy (bozza, versione, proposta) e validata contro il vocabolario;
- regole in vigore e regole in preparazione tenute distinte, con `ActivePolicy` nella lettura del paniere attivo;
- modalita congelata sulla proposta e riportata sia nella coda sia nel dettaglio dallo stesso costruttore;
- vista Strategia nel client: regole deterministiche, guardrail sull'LLM, percorso di promozione;
- lettura read-only del gate di promozione con stato misurato per requisito.

Copre: AC-17 (lettura del gate), AC-18.

Nota di scope: la modalita e una **dichiarazione versionata e auditata**. La regola direzionale che ne deriva (momentum, regime) richiede una serie storica e viene esercitata dalla sorgente di evidenze dello Slice 7; la schermata lo dichiara invece di suggerire che il selettore cambi le proposte.

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
| 6 | AC-05..07 | 340 handler test (matrice di instradamento, TTL, rivalutazione del gate, idempotenza) + flusso operatore verificato in browser |
| 7 | AC-16 | Adapter failure tests + dependency check |
| 8 | AC-14..15 | Reconciled episode test + audit trace |
| 9 | AC-18 | 385 handler test (modalita congelata sulla versione e riportata sulla proposta, coda e dettaglio coerenti, gate di promozione con stato misurato) + vista Strategia verificata in browser |
