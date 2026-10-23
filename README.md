# UAI.ZeroVP – ukázkové API pro výuku Dockeru

[![CI](https://github.com/UAI-VUT-Brno/0VP-26-27Z-StudentsGroup-Team-2/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/UAI-VUT-Brno/0VP-26-27Z-StudentsGroup-Team-2/actions/workflows/ci.yml?query=branch%3Amain)

Jednoduché REST API pro správu úkolů (ToDo), rozdělené do tří projektů.
Slouží jako výchozí bod pro navazující lekce o kontejnerizaci.

## Struktura repozitáře

```text
UAI.ZeroVP.sln                 solution – zůstává v kořeni
src/                           zdrojový kód aplikace
├─ UAI.ZeroVP.DTO/             kontrakt API
├─ UAI.ZeroVP.Data/            entita a paměťové úložiště
└─ UAI.ZeroVP.API/             webové API, kontrolery, mapování
case-study-a-dockerfile/       výuka – vrstvení Dockerfile
case-study-b-tooling/          výuka – nástroje a workflow
case-study-c-configuration/    výuka – konfigurace aplikace
```

Aplikace i výukové materiály sdílejí jeden kontext, proto se **sestavuje vždy
z kořene repozitáře** – jak `dotnet build`, tak `docker build`. Podrobnosti
v sekci [Výukové materiály k Dockeru](#výukové-materiály-k-dockeru).

| Projekt | Typ | Obsah | Závislosti |
|---|---|---|---|
| `UAI.ZeroVP.DTO` | Class library | Kontrakt API – `TodoItemDto`, `CreateTodoItemRequest`, `RepeatTodoItemRequest`, `UpdateTodoItemRequest`, `InstanceInfoDto`, výčet `TodoPriority` | žádné |
| `UAI.ZeroVP.Data` | Class library | Entita `TodoItem`, rozhraní `ITodoRepository`, paměťová implementace `InMemoryTodoRepository`, registrace do DI | `DTO` |
| `UAI.ZeroVP.API` | ASP.NET Core Web API | Kontrolery, mapování entita ↔ DTO, konfigurace pipeline | `DTO`, `Data` |

Závislosti tvoří jednosměrný řetěz **DTO → Data → API**. Kontroler zná jen rozhraní
`ITodoRepository`, takže výměna paměťového úložiště za databázi se ho nedotkne.

## Úložiště dat

Data žijí **pouze v operační paměti procesu** (`ConcurrentDictionary`).
Zastavením aplikace nebo kontejneru se nenávratně ztratí a každá replika má vlastní,
na ostatních nezávislou sadu dat. To je záměr a navazuje se na něj:
[case study A](case-study-a-dockerfile/) to ukazuje v příkladech 4 a 5 (restart
kontejneru, replikace za nginx), [case study B](case-study-b-tooling/05-compose/)
pak protiklad – pojmenovaný svazek, který restart přežije.

Při startu se vkládají čtyři ukázkové úkoly. Vypnout je lze nastavením:

```jsonc
// appsettings.json
"Todo": { "SeedSampleData": false }
```

nebo proměnnou prostředí `Todo__SeedSampleData=false` (dvojité podtržítko = oddělovač sekcí).

## Spuštění

```bash
dotnet run --project src/UAI.ZeroVP.API
```

API naslouchá na `http://localhost:5258`. Kořen `/` přesměruje na Swagger UI
(nebo na seznam úkolů, pokud je Swagger vypnutý).

## Endpointy

| Metoda | Cesta | Popis |
|---|---|---|
| `GET` | `/api/todos` | Seznam úkolů; volitelný filtr `?isCompleted=true\|false` |
| `GET` | `/api/todos/{id}` | Detail úkolu |
| `POST` | `/api/todos` | Založení úkolu → `201 Created` + hlavička `Location` |
| `POST` | `/api/todos/repeat` | Založení týdenní řady úkolů → `201 Created` + hlavička `Location` na `/api/todos` |
| `PUT` | `/api/todos/{id}` | Úplná aktualizace úkolu |
| `POST` | `/api/todos/{id}/complete` | Označení úkolu za dokončený |
| `DELETE` | `/api/todos/{id}` | Smazání úkolu → `204 No Content` |
| `GET` | `/api/info` | Hostname instance, prostředí, doba běhu, počet úkolů v paměti |
| `GET` | `/health` | Kontrola zdraví – použije ji `HEALTHCHECK` v Dockerfile |
| `GET` | `/swagger` | Interaktivní dokumentace Swagger UI |
| `GET` | `/openapi/v1.json` | Popis API ve formátu OpenAPI |

Chybové odpovědi mají jednotný tvar `ProblemDetails` (RFC 9457).
Připravené požadavky k odzkoušení jsou v souboru `src/UAI.ZeroVP.API/UAI.ZeroVP.API.http`.

### Týdenní opakování úkolů (`POST /api/todos/repeat`)

  Endpoint slouží k hromadnému vytvoření počátečního úkolu a zadaného počtu dalších týdenních opakování v jedné atomické operaci.

  **Struktura těla požadavku (`RepeatTodoItemRequest`):**
  - `title` (string, povinný, 1–200 znaků): Název úkolu.
  - `description` (string, volitelný, max 1000 znaků): Popis úkolu.
  - `priority` (string/enum, výchozí `Normal`): Priorita úkolu (`Low`, `Normal`, `High`).
  - `firstDueAtUtc` (DateTimeOffset, povinný): Datum a čas prvního výskytu v UTC.
  - `repeatCount` (int, povinný, rozsah 1–52): Počet dalších týdenních opakování po prvním termínu.

  **Příklad kalkulace termínů:**  
  Zadání například `repeatCount: 3` vytvoří celkem **4 samostatné úkoly**:
  1. První výskyt s termínem `firstDueAtUtc`.
  2. Druhý výskyt s termínem `firstDueAtUtc + 7 dní`.
  3. Třetí výskyt s termínem `firstDueAtUtc + 14 dní`.
  4. Čtvrtý výskyt s termínem `firstDueAtUtc + 21 dní`.

  Při úspěchu vrátí odpověď `201 Created` se seznamem vytvořených úkolů `TodoItemDto`, kde každý obsahuje vlastní identifikátor `id` a termín `dueAtUtc`. Běžně zakládané úkoly přes `POST /api/todos` zůstávají zpětně kompatibilní a mají `dueAtUtc: null`. Při neplatném vstupu (neplatná pole, chybějící termín, `repeatCount` mimo rozsah 1–52) vrátí API standardní `400 Bad Request` a nevytvoří žádný úkol.

  > **Poznámka k rozsahu (Scope note):** Vytvořené výskyty jsou samostatné, navzájem nezávislé běžné úkoly (nejde o trvalé pravidlo opakování). Dokončení nebo smazání jednoho výskytu neovlivňuje ostatní úkoly v sérii. Úkoly jsou uloženy pouze v operační paměti a restart aplikace nepřežijí.

## Swagger

Dokument OpenAPI generuje vestavěná knihovna .NET 10 (`Microsoft.AspNetCore.OpenApi`)
a vystavuje ho na `/openapi/v1.json`. Balíček `Swashbuckle.AspNetCore.SwaggerUI`
nad ním přidává jen interaktivní uživatelské rozhraní na `/swagger` – druhý generátor
dokumentu se tedy nepoužívá.

Popisy operací a vlastností ve Swaggeru pocházejí z XML komentářů v kódu;
proto mají všechny tři projekty zapnuté `<GenerateDocumentationFile>`.

Swagger je zapnutý v prostředí Development a vypnutý v Production.
Přepnout ho lze nastavením nebo proměnnou prostředí:

```jsonc
// appsettings.json
"Swagger": { "Enabled": true }
```

```bash
# například při spuštění kontejneru
ASPNETCORE_ENVIRONMENT=Production Swagger__Enabled=true dotnet UAI.ZeroVP.API.dll
```

### Rychlý test

```bash
# Seznam úkolů
curl http://localhost:5258/api/todos

# Založení jednoho úkolu
curl -X POST http://localhost:5258/api/todos \
    -H "Content-Type: application/json" \
    -d '{"title":"Napsat Dockerfile","priority":"High"}'

# Založení týdenní řady úkolů (první výskyt + 3 opakování = 4 úkoly)
curl -X POST http://localhost:5258/api/todos/repeat \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Týdenní status",
    "description": "Pravidelný report pokroku",
    "priority": "High",
    "firstDueAtUtc": "2026-10-26T09:00:00Z",
    "repeatCount": 3
  }'

# Informace o instanci
curl http://localhost:5258/api/info
```

## Continuous Integration

Workflow `CI` (`.github/workflows/ci.yml`) běží v GitHub Actions automaticky:

- při každém pushi do `main`, `feature/**` a `fix/**`,
- u každého pull requestu do `main`.

Na `ubuntu-latest` s .NET `10.0.x` provede `dotnet restore`, sestavení solution
v konfiguraci Release a `dotnet test` (projekt `tests/UAI.ZeroVP.UnitTests`).
Chyba kompilace nebo neprošlý test shodí celý běh. Novější push do stejné větve
zruší starší, ještě běžící běh.

Stejnou kontrolu lze spustit lokálně z kořene repozitáře:

```bash
dotnet restore UAI.ZeroVP.sln
dotnet build UAI.ZeroVP.sln --no-restore -c Release
dotnet test UAI.ZeroVP.sln --no-build -c Release
```

**Pull request smí být sloučen jen tehdy, když je `CI` zelený.**
Když běh selže:

1. V PR klikněte u kontroly `CI` na *Details*, případně otevřete záložku **Actions**
   a vyberte červený běh.
2. V jobu `build-and-test` rozbalte krok označený ❌ – `Build` znamená chybu
   kompilace, `Test` neprošlý test (název testu a hláška `Assert` jsou ve výpisu).
3. Chybu opravte lokálně, ověřte příkazy výše a pushněte do stejné větve –
   CI se spustí znovu.

Odznak nahoře ukazuje stav posledního běhu na `main`.

## Poznámka ke kódování

Zdrojové soubory jsou uloženy jako **UTF-8 s BOM**. Bez BOM dekóduje kompilátor C#
na Windows zdrojový kód systémovou ANSI kódovou stránkou a diakritika v řetězcích
se rozbije. Pravidlo hlídá `.editorconfig` (`charset = utf-8-bom`).

## Výukové materiály k Dockeru

Nad touto aplikací jsou postavené tři nezávislé case studies, každá o pěti příkladech.
Každý příklad má zadání pro studenta a komentované řešení. Náročnost stoupá
od case study A k case study C.

| Case study | Úroveň | Téma |
|---|---|---|
| [A – vrstvení Dockerfile](case-study-a-dockerfile/) | začátečníci | jeden Dockerfile, který se postupně zlepšuje: od 1,34 GB k zpevněnému image za 352 MB |
| [B – nástroje a workflow](case-study-b-tooling/) | mírně pokročilí | životní cyklus, diagnostika rozbitých kontejnerů, vrstvy, registry |
| [C – konfigurace aplikace](case-study-c-configuration/) | pokročilí | jeden image, mnoho prostředí: proměnné, soubory, priorita zdrojů, tajemství |

Materiály case studies jsou psané **anglicky**; samotná aplikace a tento soubor zůstávají v češtině.

Každá case study končí příkladem s Docker Compose. Všechny tři jsou samostatné –
lze je probírat v libovolném pořadí nebo jen některý.

### Společné pro všechny case studies

Sestavuje se vždy **z kořene projektu**, Dockerfile se uvádí přepínačem `-f`:

```bash
docker build -f case-study-a-dockerfile/02-multi-stage-build/Dockerfile -t zerovp:a02 .
```

Ignorovací soubory jsou uložené vedle jednotlivých Dockerfile jako
`<jméno Dockerfile>.dockerignore`, protože všechny příklady sdílejí jeden kontext.
V běžném projektu patří `.dockerignore` do kořene repozitáře.
