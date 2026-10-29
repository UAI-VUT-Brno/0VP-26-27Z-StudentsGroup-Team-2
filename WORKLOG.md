# Team Worklog – Evidence práce a workflow

Tento dokument slouží k evidenci aktivity týmu, sledování procesu vývoje (`branch` → `PR` → `review` → `merge`) a přehledu o tom, kdo na čem pracoval a kdo prováděl code review.

## 1. Přehled členů týmu a rolí

| Člen týmu (GitHub handle) | Role v týmu | Přidělená oblast / Issue |
| ----- | ----- | ----- |
| **@Castulik** | Team Leader | Koordinace týmu, správa `main`, Code Reviews (PR #32, #33, #34), Issue #25 (0VP-T2-08): DevSecOps security scanning v CI, `README.md`, `WORKLOG.md` (Reviewer: @luxat26) |
| **@pmikes1** | Vývojář (Dev 1) | Issue #26 (0VP-T2-09): JSON perzistence úložiště & Issue #30 (0VP-T2-13 část A): perzistenční unit a integrační testy (PR #32) |
| **@stepanch284** | Vývojář (Dev 2) | Issue #28 (0VP-T2-11): Identifikace uživatele (`UserId`) & Issue #29 (0VP-T2-12): Filtr „Moje úkoly“ (`?userId=`) & Issue #30 (0VP-T2-13 část B): testy uživatelských úkolů (PR #34) |
| **@luxat26** | Vývojář (Dev 3) | Issue #27 (0VP-T2-10): Běh více instancí na localhostu (`launchSettings.json`, `/api/info`) & Issue #31 (0VP-T2-14): Vícefázový `Dockerfile`, `.dockerignore`, `compose.yaml` (PR #33) |

## 2. Záznam aktivit a revizí (Changelog & Reviews)

| Datum | Člen týmu | Aktivita / Typ zásahu | Větev / PR | Commit / Hash | Reviewer | Výsledek / Stav |
| ----- | ----- | ----- | ----- | ----- | ----- | ----- |
| *2026-10-12* | @Castulik | Založení struktury issues, zadání a inicializace worklogu | `main` | `init` | – | Dokončeno |
| *2026-10-12* | @pmikes1 | Tvorba `ResponseTimeMiddleware.cs`, hlavička `X-Response-Time-Ms`, varování nad 500 ms | `feature/api-profiling` | PR #12 | @Castulik | Schváleno / Merged |
| *2026-10-12* | @stepanch284 | Příprava zátěžových testů v k6 (`tests/load-tests/`), dokumentace a spouštěcí návod | `feature/load-testing` | PR #13 | @Castulik | Schváleno / Merged |
| *2026-10-12* | @luxat26 | Doplnění `[ProducesResponseType]`, RFC 9457 ProblemDetails schémat a XML ukázek | `feature/openapi-docs` | PR #14 | @Castulik | Schváleno / Merged |
| *2026-10-21* | @pmikes1 | Issue #16: Nastavení CI pipeline v GitHub Actions (`.github/workflows/ci.yml`) | `feature/ci-workflow` | PR #19 | @Castulik | Schváleno / Merged |
| *2026-10-21* | @stepanch284 | Issue #16: CI status badge a nová sekce o CI v `README.md` | `feature/ci-docs` | PR #22 | @Castulik | Schváleno / Merged |
| *2026-10-21* | @pmikes1 | Issue #17: Datový model (`DueAtUtc`), `ITodoRepository.AddRangeAsync` a unit testy | `feature/repeat-data-layer` | PR #20 | @Castulik | Schváleno / Merged |
| *2026-10-21* | @stepanch284 | Issue #17: `RepeatTodoItemRequest`, `TodoItemDto.DueAtUtc`, `TodoMapper.ToEntities` a unit testy | `feature/repeat-dto-mapping` | PR #21 | @Castulik | Schváleno / Merged |
| *2026-10-21* | @luxat26 | Issue #17: Endpoint `POST /api/todos/repeat` v `TodosController.cs` a testy s Moq | `feature/repeat-endpoint` | PR #23 | @Castulik | Schváleno / Merged |
| *2026-10-21* | @Castulik | Issue #17: Doplnění dokumentace endpointu do `README.md` a testovacích dotazů do `.http` | `feature/repeat-docs` | PR #24 | @pmikes1 | Schváleno / Merged (`9484e7e`) |
| *2026-10-27* | @stepanch284 | Issue #28, #29, #30 (část B): `UserId` v entitách a DTO, mapování v `TodoMapper`, filtr `?userId=` v `TodosController.GetAll`, 25 unit testů | `feature/user-tasks-filtering` | PR #34 | @Castulik | Schváleno / Merged (`3e9de15`) |
| *2026-10-27* | @pmikes1 | Issue #26, #30 (část A): JSON perzistence úkolů (`Todo:StorageFilePath`), atomický zápis, graceful shutdown v `TodoStorageHostedService`, ošetření chyb, 12 testů | `feature/json-persistence` | PR #32 | @Castulik | Schváleno / Merged (`ba4caf4`) |
| *2026-10-29* | @luxat26 | Issue #27, #31: Běh více instancí na localhostu (`launchSettings.json`, `/api/info`), vícefázový `Dockerfile`, `.dockerignore`, `compose.yaml` (2 instance, sdílený volume) | `feature/multiinstance-docker` | PR #33 | @Castulik | Schváleno / Merged (`27d316c`) |
| *2026-10-29* | @Castulik | Issue #25: DevSecOps kontrola zranitelností NuGet balíčků v CI (`ci.yml`), dokumentace nových funkcí v `README.md`, celková evidence v `WORKLOG.md` | `feature/security-scanning` | PR | @luxat26 | V revizi (připraveno k merge) |

## 3. Poznámky k průběhu a řešení problémů (Retrospektiva)

* **Issue 9 (Latence & Zátěžové testy):**
  * **@pmikes1:** `ResponseTimeMiddleware` vrací hlavičku `X-Response-Time-Ms` a při překročení limitu 500 ms zapisuje varování do logu. Běžná odezva serveru je pod 1 ms.
  * **@stepanch284:** Zátěžový scénář v k6 potvrdil 0,00% chybovost a bezpečné splnění limitu 500 ms:
    * *50 uživatelů:* 4 130 požadavků, p(95) = 2,13 ms.

* **Issue 10 (OpenAPI & Swagger):**
  * **@luxat26:** U všech 404 odpovědí doplněny anotace `[ProducesResponseType(typeof(ProblemDetails), 404)]` dle RFC 9457. Doplněny XML `<param>` značky a `<example>` ukázky v DTO modelech.
  * Specifikace na `/openapi/v1.json` plně odpovídá OpenAPI 3.1, Swagger UI na `/swagger` je funkční a všech 55 testů prochází bez chyb.

* **Issue 16 (CI/CD Pipeline):**
  * **@pmikes1 & @stepanch284:** Vytvořeno automatizované CI workflow v GitHub Actions. Spouští se pro push do `main`, `feature/**`, `fix/**` a pull requesty do `main`. Skládá se z `restore`, `build` (Release) a `test` s NuGet cachingem a rušením neaktuálních běhů.
  * Do `README.md` byl doplněn stavový odznak (badge) napojený na `main` a sekce s návodem na lokální reprodukci kontrol. Issue kompletně uzavřena.

* **Issue 17 (Týdenní opakování úkolů):**
  * **@pmikes1:** Přidal `DueAtUtc` do `TodoItem` a implementoval metodu `AddRangeAsync` s garancí atomicity (all-or-nothing rollback) a 15 unit testy v `InMemoryTodoRepositoryRepeatTests.cs`.
  * **@stepanch284:** Přidal validační model `RepeatTodoItemRequest`, rozšířil DTO o `DueAtUtc` a vytvořil mapovací metodu `ToEntities` pro týdenní sekvenci úkolů s ověřením hraničních stavů v `TodoMapperRepeatTests.cs`.
  * **@luxat26:** Přidal endpoint `POST /api/todos/repeat` do `TodosController.cs` vracející `201 Created` (`CreatedAtAction`), doplnil OpenAPI anotace a pokryl chování kontroleru sadou unit testů s Moq v `TodosControllerRepeatTests.cs`.
  * **@Castulik:** Zdokumentoval nový endpoint v `README.md`, přidal ukázkové curl příkazy a do `UAI.ZeroVP.API.http` doplnil platný i validačně chybný HTTP požadavek.

* **Issue 25 (0VP-T2-08: DevSecOps Security Scanning & Dokumentace):**
  * **@Castulik:** Do CI workflow (`.github/workflows/ci.yml`) doplněn krok DevSecOps bezpečnostního skenování (`dotnet list UAI.ZeroVP.sln package --vulnerable --include-transitive`), který kontroluje přímé i tranzitivní závislosti proti známým zranitelnostem a při nálezu shodí pipeline. V `README.md` byl podrobně zdokumentován návod na lokální spuštění bezpečnostního auditu a veškeré nově zavedené funkce řešení (JSON perzistence, identifikace uživatele a filtr `?userId=`, multi-instance spuštění na localhostu, multi-stage Dockerfile a multi-instance Docker Compose). Vedení a aktualizace týmového přehledu ve `WORKLOG.md`. Review provádí @luxat26.

* **Issue 26 (0VP-T2-09: Perzistence úkolů v JSON souboru):**
  * **@pmikes1:** Implementována perzistence úkolů přežívající restarty aplikace. Cesta k souboru se konfiguruje přes `Todo:StorageFilePath` (`appsettings.json`, výchozí `data/todos.json`, přepisovatelné proměnnou prostředí `Todo__StorageFilePath`). Ukládání probíhá při korektním vypnutí serveru (`IHostedLifecycleService.StoppedAsync`) zápisem do unikátního dočasného souboru (`.tmp`) a následným atomickým nahrazením cílového souboru (`File.Move(..., overwrite: true)`), což zamezuje poškození souboru při přerušení. Při startu se data načtou ze souboru; absence souboru zahajuje čistý stav a poškozený soubor je zalogován přes `ILogger` bez pádu aplikace.

* **Issue 27 (0VP-T2-10: Běh více instancí na localhostu):**
  * **@luxat26:** V `launchSettings.json` nadefinovány profily pro souběžné spuštění dvou instancí API bez portových kolizí (instance 1 na portu `5258`, instance 2 na portu `5259`). Obě instance přistupují ke stejnému souboru `data/todos.json`. Do `InstanceInfoDto` a `InfoController` byl doplněn identifikátor `InstanceId` (přebíraný z konfigurace/proměnné prostředí s fallbackem na `{MachineName}:{ProcessId}`) a `ProcessId`, díky čemuž lze instance jednoznačně rozlišit v dotazu `GET /api/info`. Do `UAI.ZeroVP.API.http` doplněny ukázkové dotazy pro obě instance.

* **Issue 28 & 29 (0VP-T2-11 & 0VP-T2-12: Identifikace uživatele a filtr „Moje úkoly“):**
  * **@stepanch284:** Entita `TodoItem` a DTO modely (`TodoItemDto`, `CreateTodoItemRequest`, `RepeatTodoItemRequest`, `UpdateTodoItemRequest`) rozšířeny o volitelnou vlastnost `UserId` s validací délky do 200 znaků (`[StringLength(200)]`). V `TodoMapper.cs` byla zavedena normalizace `NormalizeUserId` ořezávající bílé znaky a převádějící prázdné řetězce na `null`. Metoda `InMemoryTodoRepository.GetAllAsync` a endpoint `GET /api/todos?userId=...` byly rozšířeny o volitelný filtr podle uživatele, který lze libovolně kombinovat se stavem dokončení `isCompleted`.

* **Issue 30 (0VP-T2-13: Unit a integrační testy nové funkcionality):**
  * **@pmikes1 & @stepanch284:** Nová funkcionalita byla kompletně pokryta automatizovanými testy v projektu `tests/UAI.ZeroVP.UnitTests`:
    * **Část A (@pmikes1):** Soubor `TodoJsonPersistenceTests.cs` (12 testovacích metod) pokrývá kompletní cyklus uložení a načtení, absenci souboru, 6 scénářů poškozeného JSONu s ověřením chybových logů, souběžný zápis a integrační test životního cyklu hostitele (`IHost`).
    * **Část B (@stepanch284):** Soubor `TodoUserTasksTests.cs` (25 testů) ověřuje mapování a ořezávání `UserId`, validaci mezních délek (200 vs 201 znaků), propagaci `UserId` do opakovaných úkolů v sérii, filtrování v repozitáři i chování kontroleru s využitím knihovny Moq.
    * Celkový počet testů stoupl ze 105 na 153, přičemž všechny testy jsou plně funkční a zelené.

* **Issue 31 (0VP-T2-14: Dockerfile a Docker Compose):**
  * **@luxat26:** Vytvořen produkční vícefázový `Dockerfile` (`sdk:10.0` pro cachovaný restore a Release build, `aspnet:10.0` pro runtime) s neprivilegovaným uživatelem `USER $APP_UID`, oprávněními pro adresář `/app/data` a kontrolou zdraví `HEALTHCHECK` volající `/health`. Připraven soubor `.dockerignore`. V `compose.yaml` nakonfigurován souběžný běh dvou instancí API (`api-1` na portu 5258, `api-2` na portu 5259) se sdíleným pojmenovaným svazkem `todo-data:/app/data` a jednotnou konfigurací `Todo__StorageFilePath=/app/data/todos.json`.
