# UAI.ZeroVP – ukázkové API pro výuku Dockeru

[![CI](https://github.com/UAI-VUT-Brno/0VP-26-27Z-StudentsGroup-Team-2/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/UAI-VUT-Brno/0VP-26-27Z-StudentsGroup-Team-2/actions/workflows/ci.yml?query=branch%3Amain)

Jednoduché REST API pro správu úkolů (ToDo), rozdělené do tří projektů.
Slouží jako výchozí bod pro navazující lekce o kontejnerizaci.

## Struktura repozitáře

```text
UAI.ZeroVP.sln                 solution – zůstává v kořeni
Dockerfile                     vícefázový produkční build kontejneru
compose.yaml                   Docker Compose pro běh multi-instance API
src/                           zdrojový kód aplikace
├─ UAI.ZeroVP.DTO/             kontrakt API
├─ UAI.ZeroVP.Data/            entita a paměťové/souborové úložiště
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
| `UAI.ZeroVP.Data` | Class library | Entita `TodoItem`, rozhraní `ITodoRepository`, implementace `InMemoryTodoRepository`, perzistence `TodoStorageHostedService`, registrace do DI | `DTO` |
| `UAI.ZeroVP.API` | ASP.NET Core Web API | Kontrolery, mapování entita ↔ DTO, konfigurace pipeline | `DTO`, `Data` |

Závislosti tvoří jednosměrný řetěz **DTO → Data → API**. Kontroler zná jen rozhraní
`ITodoRepository`, takže výměna paměťového úložiště za databázi se ho nedotkne.

## Úložiště dat a perzistence

Úložiště `InMemoryTodoRepository` kombinuje rychlý přístup v operační paměti (`ConcurrentDictionary`)
s volitelným **trvalým ukládáním do JSON souboru**, aby úkoly přežily restart aplikace nebo kontejneru.

* **Konfigurace cesty k souboru:**
  V `appsettings.json` je výchozí cesta nastavena na:
  ```jsonc
  "Todo": {
    "SeedSampleData": true,
    "StorageFilePath": "data/todos.json"
  }
  ```
  V produkci nebo v kontejneru lze cestu přepsat proměnnou prostředí:
  `Todo__StorageFilePath=/app/data/todos.json`.
  Pokud je `StorageFilePath` nastaveno na `null` (např. při jednotkových testech), repozitář funguje čistě v paměti procesu.

* **Životní cyklus a graceful shutdown:**
  Služba `TodoStorageHostedService` je napojena na životní cyklus aplikace (`IHostedLifecycleService`).
  Při korektním vypnutí serveru (fáze `StoppedAsync`, kdy webový server již nepřijímá nové požadavky) atomicky uloží všechny úkoly do souboru. Zápis probíhá přes unikátní dočasný soubor (`.tmp`), který je následně přejmenován – tím je zaručeno, že soubor nebude nikdy poškozen nedokončeným zápisem.

* **Inicializace při startu:**
  Při startu se repozitář pokusí načíst existující úkoly ze zadaného souboru.
  * Pokud soubor existuje a je platný, načtou se z něj uložené úkoly (ukázková data se nepřidávají).
  * Pokud soubor neexistuje, začíná se načisto a vloží se ukázková data (pokud je `SeedSampleData: true`).
  * Pokud je soubor poškozený (neplatný JSON), aplikace nespadne, zapíše chybu do logu a bezpečně odstartuje s výchozími daty.

## Spuštění

### Jedna instance

```bash
dotnet run --project src/UAI.ZeroVP.API
```

API naslouchá na `http://localhost:5258`. Kořen `/` přesměruje na Swagger UI
(nebo na seznam úkolů, pokud je Swagger vypnutý).

### Běh více instancí na localhostu (souběžný běh)

Pro lokální testování souběžného běhu bez portových kolizí jsou v `launchSettings.json`
připraveny samostatné profily:

```bash
# 1. instance (port 5258, InstanceId = instance-1)
dotnet run --project src/UAI.ZeroVP.API --launch-profile instance-1

# 2. instance (port 5259, InstanceId = instance-2)
dotnet run --project src/UAI.ZeroVP.API --launch-profile instance-2
```

Obě instance běží současně na jednom stroji, ukládají data do sdíleného souboru `data/todos.json`
a v odpovědi na `GET /api/info` se jednoznačně odliší hodnotami `instanceId` a `processId`.

## Kontejnerizace a Docker Compose

V repozitáři je k dispozici vícefázový `Dockerfile` a `compose.yaml` pro spuštění multi-instance prostředí v kontejnerech:

* **`Dockerfile`:**
  * Sestavení probíhá v image `mcr.microsoft.com/dotnet/sdk:10.0` s vrstveným cachováním balíčků (`dotnet restore` nad projektovými soubory před kopírováním zdrojových kódů).
  * Běhové prostředí využívá odlehčený `mcr.microsoft.com/dotnet/aspnet:10.0`.
  * Kontejner neběží pod rootem, ale pod bezpečným neprivilegovaným uživatelem `USER $APP_UID`.
  * Obsahuje automatickou kontrolu zdraví `HEALTHCHECK` volající `curl http://localhost:8080/health`.

* **Spuštění přes Docker Compose:**
  ```bash
  docker compose up --build
  ```
  Tento příkaz spustí dvě repliky API:
  * `api-1`: vystavené na portu hostitele `5258` (`http://localhost:5258/swagger`)
  * `api-2`: vystavené na portu hostitele `5259` (`http://localhost:5259/swagger`)
  * Obě instance sdílejí pojmenovaný Docker svazek (volume) `todo-data:/app/data`, kde je uložen společný perzistentní soubor `todos.json`.

## Endpointy

| Metoda | Cesta | Popis |
|---|---|---|
| `GET` | `/api/todos` | Seznam úkolů; volitelný filtr `?isCompleted=true\|false` a filtr uživatele `?userId=...` |
| `GET` | `/api/todos/{id}` | Detail úkolu |
| `POST` | `/api/todos` | Založení úkolu (volitelně s `userId`) → `201 Created` + hlavička `Location` |
| `POST` | `/api/todos/repeat` | Založení týdenní řady úkolů → `201 Created` + hlavička `Location` na `/api/todos` |
| `PUT` | `/api/todos/{id}` | Úplná aktualizace úkolu (včetně `userId`) |
| `POST` | `/api/todos/{id}/complete` | Označení úkolu za dokončený |
| `DELETE` | `/api/todos/{id}` | Smazání úkolu → `204 No Content` |
| `GET` | `/api/info` | Informace o instanci (`instanceId`, `machineName`, `processId`, prostředí, uptime, počet úkolů) |
| `GET` | `/health` | Kontrola zdraví – použije ji `HEALTHCHECK` v Dockerfile |
| `GET` | `/swagger` | Interaktivní dokumentace Swagger UI |
| `GET` | `/openapi/v1.json` | Popis API ve formátu OpenAPI |

Chybové odpovědi mají jednotný tvar `ProblemDetails` (RFC 9457).
Připravené požadavky k odzkoušení jsou v souboru `src/UAI.ZeroVP.API/UAI.ZeroVP.API.http`.

### Identifikace uživatele a filtr „Moje úkoly“ (`?userId=...`)

Úkoly lze označit volitelným identifikátorem uživatele (`userId` – např. uživatelské jméno nebo e-mail):
* **DTO a validace:** Pole `userId` je dostupné v `TodoItemDto`, `CreateTodoItemRequest`, `RepeatTodoItemRequest` a `UpdateTodoItemRequest` s omezením na maximálně 200 znaků (`[StringLength(200)]`).
* **Normalizace:** Bílé znaky na začátku a na konci jsou automaticky ořezány (`Trim()`). Prázdné řetězce nebo řetězce tvořené pouze mezerami se ukládají jako `null` („bez uživatele“).
* **Filtrování:** Dotaz `GET /api/todos?userId=jan.novak` vrátí pouze úkoly patřící zadanému uživateli. Filtr lze libovolně kombinovat s `?isCompleted=false`. Pokud `userId` není zadán nebo je prázdný, vrátí se úkoly všech uživatelů.

### Týdenní opakování úkolů (`POST /api/todos/repeat`)

Endpoint slouží k hromadnému vytvoření počátečního úkolu a zadaného počtu dalších týdenních opakování v jedné atomické operaci.

**Struktura těla požadavku (`RepeatTodoItemRequest`):**
- `title` (string, povinný, 1–200 znaků): Název úkolu.
- `description` (string, volitelný, max 1000 znaků): Popis úkolu.
- `priority` (string/enum, výchozí `Normal`): Priorita úkolu (`Low`, `Normal`, `High`).
- `firstDueAtUtc` (DateTimeOffset, povinný): Datum a čas prvního výskytu v UTC.
- `repeatCount` (int, povinný, rozsah 1–52): Počet dalších týdenních opakování po prvním termínu.
- `userId` (string, volitelný, max 200 znaků): Identifikátor uživatele; přiřadí se všem vytvořeným výskytům.

**Příklad kalkulace termínů:**  
Zadání například `repeatCount: 3` vytvoří celkem **4 samostatné úkoly**:
1. První výskyt s termínem `firstDueAtUtc`.
2. Druhý výskyt s termínem `firstDueAtUtc + 7 dní`.
3. Třetí výskyt s termínem `firstDueAtUtc + 14 dní`.
4. Čtvrtý výskyt s termínem `firstDueAtUtc + 21 dní`.

Při úspěchu vrátí odpověď `201 Created` se seznamem vytvořených úkolů `TodoItemDto`, kde každý obsahuje vlastní identifikátor `id` a termín `dueAtUtc`. Běžně zakládané úkoly přes `POST /api/todos` zůstávají zpětně kompatibilní a mají `dueAtUtc: null`. Při neplatném vstupu (neplatná pole, chybějící termín, `repeatCount` mimo rozsah 1–52) vrátí API standardní `400 Bad Request` a nevytvoří žádný úkol.

> **Poznámka k rozsahu (Scope note):** Vytvořené výskyty jsou samostatné, navzájem nezávislé běžné úkoly (nejde o trvalé pravidlo opakování). Dokončení nebo smazání jednoho výskytu neovlivňuje ostatní úkoly v sérii. Úkoly jsou perzistovány dle nastavení `Todo:StorageFilePath`.

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
# Seznam úkolů (všech)
curl http://localhost:5258/api/todos

# Seznam úkolů konkrétního uživatele
curl "http://localhost:5258/api/todos?userId=jan.novak&isCompleted=false"

# Založení jednoho úkolu s uživatelem
curl -X POST http://localhost:5258/api/todos \
    -H "Content-Type: application/json" \
    -d '{"title":"Napsat Dockerfile","priority":"High","userId":"jan.novak"}'

# Založení týdenní řady úkolů (první výskyt + 3 opakování = 4 úkoly)
curl -X POST http://localhost:5258/api/todos/repeat \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Týdenní status",
    "description": "Pravidelný report pokroku",
    "priority": "High",
    "firstDueAtUtc": "2026-10-26T09:00:00Z",
    "repeatCount": 3,
    "userId": "jan.novak"
  }'

# Informace o instanci
curl http://localhost:5258/api/info
```

## Continuous Integration & DevSecOps

Workflow `CI` (`.github/workflows/ci.yml`) běží v GitHub Actions automaticky:

- při každém pushi do `main`, `feature/**` a `fix/**`,
- u každého pull requestu do `main`.

Na `ubuntu-latest` s .NET `10.0.x` provádí následující kroky:
1. `dotnet restore` s cachováním NuGet balíčků.
2. **DevSecOps Security Gate:** Automatizovaná bezpečnostní kontrola zranitelností v balíčcích projektu (`dotnet list ... package --vulnerable --include-transitive`). Pokud je detekována jakákoliv zranitelnost, pipeline okamžitě selže.
3. `dotnet build` v konfiguraci Release (`--no-restore`).
4. `dotnet test` v konfiguraci Release (`--no-build`) – spouští všech 153 testů v projektu `tests/UAI.ZeroVP.UnitTests`.

Chyba kompilace, bezpečnostní zranitelnost nebo neprošlý test shodí celý běh. Novější push do stejné větve
zruší starší, ještě běžící běh.

### Lokální reprodukce kontrol (včetně bezpečnostního auditu)

Stejnou kontrolu lze spustit lokálně z kořene repozitáře:

```bash
# 1. Obnova balíčků
dotnet restore UAI.ZeroVP.sln

# 2. Bezpečnostní kontrola zranitelných balíčků (DevSecOps scan)
dotnet list UAI.ZeroVP.sln package --vulnerable --include-transitive

# 3. Sestavení solution
dotnet build UAI.ZeroVP.sln --no-restore -c Release

# 4. Spuštění unit testů
dotnet test UAI.ZeroVP.sln --no-build -c Release
```

**Pull request smí být sloučen jen tehdy, když je `CI` zelený.**
Když běh selže:

1. V PR klikněte u kontroly `CI` na *Details*, případně otevřete záložku **Actions**
   a vyberte červený běh.
2. V jobu `build-and-test` rozbalte krok označený ❌ – `Security scan` značí nalezené zranitelnosti v závislostech, `Build` chybu kompilace a `Test` neprošlý jednotkový test.
3. Chybu opravte lokálně, ověřte příkazy výše a pushněte do stejné větve –
   CI se spustí znovu.

Odznak nahoře ukazuje stav posledního běhu na `main`.

## Poznámka ke kódování

Zdrojové soubory jsou uloženy jako **UTF-8 s BOM**. Bez BOM dekóduje kompilátor C#
na Windows zdrojový kód systémovou ANSI kódovou stránkou a diakritika v řetězcích
se rozbije. Pravidlo hlídá `.editorconfig` (`charset = utf-8-bom`). Soubory Dockeru
a konfigurace používají čisté UTF-8 s řádkovými konci LF.

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
