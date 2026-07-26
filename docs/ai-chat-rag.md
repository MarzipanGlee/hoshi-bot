# AI-Chat: RAG-Implementierung (Stand 2026-07-23)

*Status: beschreibt den aktuellen Ist-Zustand des Retrieval-Teils von AiChat (Volltext- +
Vektorsuche, Embedding-/Chat-Modelle) sowie die praktischen Erfahrungen mit der lokalen
Ollama-Instanz auf dem Testserver. Kein Konzeptpapier wie
[ai-chat-member-lore.md](ai-chat-member-lore.md) — hier geht es um das, was tatsächlich läuft.*

## Überblick

AiChat kombiniert zwei Retrieval-Pfade zu einer **Hybrid-Suche**:

1. **Volltextsuche (FTS)** über Postgres' eingebaute `tsvector`/`tsquery`-Funktionen.
2. **Vektorsuche** über `pgvector`, mit Embeddings von Ollama oder Gemini.

Beide Kandidatenlisten werden per **Reciprocal Rank Fusion (RRF)** zusammengeführt, zusätzlich
gewichtet nach einer Kanal-"Wissens-Tier"-Einstufung. Es gibt **kein Chunking** — die
Retrieval-Einheit ist immer eine einzelne Discord-Nachricht.

## Volltextsuche (FTS)

Die FTS berechnet Postgres' `tsvector`/`tsquery` **zur Abfragezeit**, nicht über eine
generierte/gespeicherte Spalte oder einen GIN-Index:

```sql
to_tsvector(language, content) @@ websearch_to_tsquery(language, search)
```

Grund: Die Suchsprache ist konfigurierbar und lässt sich daher nicht in eine generierte
Spalte fest einbacken. Freie Nutzerfragen werden zusätzlich in OR-verknüpfte Suchbegriffe
(≥ 3 Zeichen) zerlegt, weil ein ganzer Satz per AND kaum je matcht.

Die indexierten Nachrichten liegen in einer eigenen Tabelle mit Standard-Indizes auf
Gilden-ID und Discord-Message-ID, aber (noch) keinem spezialisierten FTS-Index.

## Vektorsuche (Embeddings)

Erweiterung **pgvector**. Feste Embedding-Dimension: **768**. Zwei Tabellen tragen
`vector(768)`-Spalten:

- die indexierten Wissenskanal-Nachrichten (Embedding + verwendetes Embedding-Modell)
- das Gilden-Gedächtnis (episodisches/Member-Gedächtnis)

Es gibt **noch keinen ANN-Index** (HNSW/IVFFlat) — bewusst ein sequenzieller
Cosine-Distance-Scan als v1-Kompromiss.

### Hybrid-Suche (RRF)

Die Suche holt zwei Kandidatenlisten (FTS + Vektor, je gedeckelt auf 40 Treffer) und
fusioniert sie per **Reciprocal Rank Fusion**:

```
score += 1.0 / (RrfK + rank + 1)   // RrfK = 60 (Standardwert)
```

Danach eine Multiplikation nach Kanal-Tier: Faktor 1,5 für bevorzugte Wissenskanäle, Faktor
0,25 für Kanäle, die nur als letzter Ausweg zählen sollen. Fehlen Embeddings (z. B. Backend
deaktiviert), degradiert die Suche sauber auf FTS-only.

Für episodische/Member-Memories kommt **reine Vektorsuche** ohne FTS-Anteil zum Einsatz (Pool
24, Re-Ranking nach Salienz + Aktualität, mit Dedup-Schwelle für sehr ähnliche Treffer).

## Embedding-Backends (Ollama vs. Gemini)

Zwei Backends stehen zur Auswahl:

| Backend | Modell | Besonderheit |
|---|---|---|
| **Ollama** (Default) | `embeddinggemma` | läuft lokal, kein API-Key nötig |
| **Gemini** | `gemini-embedding-001` oder `gemini-embedding-2` | Output wird auf 768 Dimensionen gekürzt, um mit Ollama-Embeddings kompatibel zu bleiben |

## Chat-/Completion-Modelle

Getrennt von den Embeddings gibt es zwei Backends für die eigentlichen Antworten:

- **Ollama**: Chatmodell `llama3.1:8b`, Kontextfenster 4096 Tokens.
- **Gemini** (Default): Chatmodell `gemini-3.5-flash`, separates, günstigeres Gate-Modell
  `gemini-3.1-flash-lite` für den Passiv-Zuhör-Filter (Ja/Nein-Vorabcheck, ob eine Nachricht
  überhaupt eine volle Antwort verdient), plus ein optionales Router-Modell (Routing
  einfach/komplex) und ein Member-Lore-Modell (fällt standardmäßig auf das Gate-Modell zurück).

Für Ollama existiert derselbe Gate-Mechanismus, standardmäßig aber **deaktiviert** (siehe
Erfahrungsbericht unten — das dafür getestete Leichtgewichtsmodell hat nicht funktioniert).

## Chunking/Indexierung

Es gibt **keine Text-Chunking-Pipeline mit Overlap** — die Retrieval-Einheit ist immer genau
eine Discord-Nachricht (Inhalt + Embed-Text, gekappt bei 4000 Zeichen), aufgenommen/
aktualisiert pro Discord-Message-ID. Bearbeitete Nachrichten invalidieren ihr Embedding, das
dann in Batches (50 pro Batch, gedeckelt auf 1000 pro Lauf) nachberechnet wird. MemberLore
und Memory chunken ebenfalls nicht — sie speichern das Ergebnis eines einzelnen
LLM-Extraktionsaufrufs (kurze Sätze/strukturierte Fakten) und embedden diese als Ganzes.

## Relevante Konfiguration

Globale Ollama-Einstellungen:

```
BaseUrl            = http://ollama:11434
DefaultModel        = llama3.1:8b
TimeoutSeconds       = 300
EmbeddingModel       = embeddinggemma
EmbedBatchSize       = 50
MaxEmbedPerRun       = 1000
```

Gemini hat keine vergleichbare globale Konfiguration — dafür ist ein API-Key nötig.

---

## Erfahrungen mit der lokalen Ollama-Instanz (Testserver)

Beobachtungen aus den Bot-Logs des Testservers (`~/hoshi-testing/logs/bot/`, 12.–23.07.2026)
und `docker compose`/`ollama ps` auf dem Server selbst.

### Hardware & Setup

Der Testserver läuft **CPU-only**, keine GPU:

- 8 vCPUs, Intel Xeon Silver 4210 @ 2.20 GHz
- 22 GB RAM
- `ollama/ollama:latest` im eigenen Container, `OLLAMA_MAX_LOADED_MODELS=3`,
  `OLLAMA_NUM_PARALLEL=1`, `OLLAMA_KEEP_ALIVE=-1` (Modelle bleiben dauerhaft im RAM, damit
  Antworten nicht durch Kaltstarts noch langsamer werden — ein `OllamaWarmupService` lädt sie
  beim Bot-Start vor)

Aktuell resident: `llama3.1:8b` (~5,6 GB) und `embeddinggemma` (~680 MB), beide dauerhaft
geladen, beide auf `100% CPU`-Processor laut `ollama ps` (also kein GPU-Offloading aktiv).

### Antwortzeiten (real gemessen)

Wichtig, um die Logs richtig zu lesen: Pro Nachricht ruft Ollama-AiChat potenziell **zwei**
`/api/chat`-Requests auf — erst den kurzen Gate-Check (Ja/Nein, ein Token Output), danach —
nur falls der Gate mit "yes" antwortet bzw. der Nutzer den Bot direkt adressiert hat — die
eigentliche volle Antwortgenerierung. Die beiden haben eine sehr unterschiedliche Laufzeit:

| Aufruf | Beobachtete Dauer |
|---|---|
| Gate-Check (`passive gate=…`, kurzer Ja/Nein-Prompt) | ~1,7 – 4,6 s |
| **Volle Chat-Antwort** (`provider="Ollama" … → answer=…`, llama3.1:8b) | **n=25, min ≈ 18,7 s, p25 ≈ 55 s, Median ≈ 97 s, p75 ≈ 130 s, max ≈ 195 s** |
| `/api/embed` (embeddinggemma, einzelne Nachricht) | meist < 1 s, p90 ≈ 2,8 s, vereinzelt bis 7 s |

Die eigentliche Antwortgenerierung lag in den Logs des Testservers also so gut wie nie unter
einer Minute, meist im Bereich von 1–2,5 Minuten. Das gilt für **jede** vom Nutzer
tatsächlich wahrgenommene AiChat-Antwort, nicht nur für Ausreißer unter Last — der
Gate-Check ist schnell, die eigentliche Antwort auf einer CPU-only-Instanz nicht.
`OLLAMA_NUM_PARALLEL=1` verschärft das zusätzlich, wenn mehrere Anfragen zeitlich
zusammenfallen (Gate-Check, Antwort, paralleler Embedding-Nachlauf serialisieren sich alle
auf demselben Prozess) — dann wurden auch Ausreißer bis ~195 s beobachtet.

Zum Vergleich: Gemini-Antworten (`gemini-3.5-flash`) liegen laut denselben Logs im Bereich
von wenigen Sekunden, unabhängig von Serverlast — da Google die Inferenz übernimmt.

**Qualität**: Über die reine Latenz hinaus war in der praktischen Nutzung auch die
**inhaltliche Qualität der Gemini-Antworten spürbar besser** als die von llama3.1:8b — ein
weiterer, unabhängig von der Geschwindigkeit stehender Grund, warum die Testgilde
mittlerweile auf Gemini als Chat-Provider steht (siehe "Aktueller Stand" unten).

### Das Gate-Modell-Experiment (gescheitert)

Für den "passiven Zuhör"-Filter (ein Ja/Nein-Vorabcheck, ob eine Nachricht überhaupt eine
volle Antwort verdient, um nicht bei jeder Chat-Nachricht eine teure volle Antwort zu
generieren) wurde zunächst ein sehr kleines dediziertes Modell (`gemma3:1b`) getestet, um
Ressourcen zu sparen. Ergebnis laut Log und Compose-Kommentar: **verworfen** — das kleine
Modell hat auf echte deutsche Fragen mit "NO" geantwortet und bei trivialen
Prompt-Änderungen inkonsistent hin- und hergewechselt. Beispiel aus den Logs (18.07., mehrfach):

```
AiChat guild ...: passive gate=gemma3:1b → no → silent
```

— während echte Fragen im selben Kanal unbeantwortet blieben. Die Lösung: das bereits
resident laufende `llama3.1:8b` auch als Gate-Modell wiederverwenden (kein zusätzliches
Modell im RAM nötig, klassifiziert zuverlässig), auf Kosten einer etwas höheren Gate-Latenz
(einige Sekunden statt praktisch sofort) gegenüber der vollen Antwortgenerierung
(~eine Minute in Lastspitzen).

### Weitere Beobachtung: Modell nicht vorab gepullt

Beim ersten Hochfahren des Containers (18.07., 11:43 Uhr) schlug ein Chat-Request fehl:

```
[WRN] Ollama request failed (model llama3.1:8b, status 404): {"error":"model 'llama3.1:8b' not found"}
```

— Ollama pullt Modelle nicht automatisch beim ersten `/api/chat`-Aufruf; sie müssen einmalig
per `docker compose exec ollama ollama pull <modell>` gezogen werden. Führte zu einer
sichtbaren Fehlantwort (`answer=null`) beim allerersten Versuch, seither nicht mehr
aufgetreten.

### Aktueller Stand (22.–23.07.)

In den letzten Log-Tagen zeigt sich: Die Testgilde nutzt für **Chat-Antworten** inzwischen
ausschließlich `provider="Gemini"` (41 Treffer in den letzten fünf Logdateien, 0 für
`provider="Ollama"`) — Ollama läuft aber weiterhin durchgehend für das
**Embedding** (`embeddinggemma`), da die semantische Suche unabhängig vom gewählten
Chat-Provider für alle Gilden läuft. Die Umstellung auf Gemini als Chat-Provider für die
Testgilde deckt sich mit den oben beobachteten Antwortzeiten — die volle Antwortgenerierung
mit llama3.1:8b auf reiner CPU-Hardware brauchte praktisch durchgehend über eine Minute, oft
1,5–2 Minuten, während Gemini in wenigen Sekunden antwortet — **und das bei zusätzlich
spürbar besserer inhaltlicher Qualität der Antworten.**

### Fazit

- Für **Embeddings** (kleines Modell, kurze Texte) ist die lokale CPU-Instanz gut genug und
  spart laufende Kosten/API-Keys pro Gilde.
- Für **volle Chat-Antworten** ist ein 8B-Modell auf reiner CPU **grundsätzlich zu langsam**
  für ein Discord-Chat-Erlebnis — nicht nur unter Lastspitzen, sondern im Normalfall: real
  gemessen praktisch immer 60+ Sekunden, im Median ~97 s, bis zu ~195 s. Das ist der
  eigentliche Hauptgrund (zusätzlich zur schwächeren Antwortqualität gegenüber Gemini),
  warum die Testgilde inzwischen auf Gemini als Chat-Provider umgestellt ist — Ollama bliebe
  hierfür nur mit GPU-Beschleunigung praktikabel (der `deploy`-Block in `compose.yaml` für
  NVIDIA-Container ist vorbereitet, aber auf dem aktuellen Testserver nicht aktiv).
- Kleinere Modelle als Ressourcen-Sparmaßnahme (siehe `gemma3:1b`-Versuch) sind bei
  deutschsprachigen Alltagsfragen unzuverlässig genug, dass sich die Wiederverwendung des
  großen, ohnehin geladenen Modells mehr lohnt als ein zweites kleines Modell im RAM.

---

## Troubleshooting: „Hoshi hat einen Fakt erfunden/verpasst/ausgelassen"

Playbook aus einem realen Vorfall (23.–24.07.2026): Hoshi behauptete fälschlich,
Simulacrum-Umbauten kämen aus Remote-Campus-Events (tatsächlich Shop-only), und zitierte
am Folgetag einen veralteten Promo-Code, obwohl der aktuelle kurz zuvor in drei Kanälen
gepostet worden war. Hinter derselben Symptomfamilie steckten **vier verschiedene
Ursachenklassen** — bei einem neuen Vorfall dieser Art in dieser Reihenfolge prüfen und
erst danach am Prompt schrauben:

1. **Selbstzitat-Schleife (Index-Hygiene).** Prüfen, ob eine frühere (falsche) Antwort
   des Bots selbst im RAG-Index gelandet ist und als „Quelle" zurückgeliefert wird:
   `AiChatIndexedMessages` nach `AuthorName = 'Hoshi Sato'` abfragen — muss seit dem Fix
   immer null Zeilen ergeben. Hintergrund: der Live-Indexpfad
   (`AiChatIndexService.IndexMessageAsync`) überspringt Bot-eigene Nachrichten, der
   periodische Backfill (`UpsertMessagesAsync`) tat das ursprünglich nicht — 177
   kontaminierte Zeilen, der Bot „bestätigte" seine eigene Falschantwort bei jeder
   Wiederholungsfrage. Wichtig: keine Ausnahme einführen, die Hoshis eigene (übersetzte)
   Reposts als Quelle zulässt — das öffnet genau dieses Loch wieder.
2. **Embedding-Abdeckung/Quota.** `Embedding IS NULL`-Anzahl für die Gilde prüfen und in
   den Bot-Logs nach `Gemini embed failed`/Quota-Fehlern suchen. Das Gemini-Embedding
   im Free-Tier hat ein Tageslimit (1000 Embed-Requests/Tag) — ist es erschöpft,
   degradiert die Suche auf FTS-only (sauber, aber lexikalisch schwach: „Refits" in der
   Korrektur matcht „Überholung" in der Frage nicht). Das ist die dokumentierte
   Graceful Degradation, kein Bug — ggf. einfach den Quota-Reset abwarten.
3. **Kanal-Tier-Konfiguration.** Speziell bei „sie hat einen gerade erst geposteten Fakt
   verpasst": prüfen, ob der echte Quellkanal als **Bevorzugt** (Preferred) eingestuft
   ist (`GuildFeatureChannels`, Feature 19). Der Live-Block
   (`BuildLatestAnnouncementsBlockAsync` — „die letzten Nachrichten immer frisch holen,
   am Ranking vorbei") zieht **nur** aus Preferred-Kanälen; Normal-Kanäle hängen
   komplett an der gerankten FTS/Vektor-Suche und verlieren dort gegen ältere, lexikalisch
   ähnlichere Posts. Fix ist reine Konfiguration (Web-Admin-Kanalauswahl), kein Deploy —
   Kanallisten sind nicht gecacht, wirkt sofort.
4. **Erst jetzt: Prompt.** Eine Grounding-Regel im System-Prompt („Wissensquellen"-Block
   in `BuildSystemInstructionAsync`, `AiChatService.cs`: Quellzeilen als eigenständig
   behandeln, lieber Unsicherheit zugeben als kombinieren/raten) existiert bereits —
   sie hat den ursprünglichen Vorfall allein *nicht* behoben. Prompt-Tweaks sind der
   letzte Schritt, nicht der erste.

Merksatz aus dem Vorfall: nicht bei der ersten plausiblen Ursache stehen bleiben —
erst der Live-Redeploy + Retest zeigte, dass Schicht 1 allein nicht reichte.
