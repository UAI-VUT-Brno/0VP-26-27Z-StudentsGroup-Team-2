# Team Worklog – Evidence práce a workflow

Tento dokument slouží k evidenci aktivity týmu, sledování procesu vývoje (`branch` → `PR` → `review` → `merge`) a přehledu o tom, kdo na čem pracoval a kdo prováděl code review.

## 1. Přehled členů týmu a rolí

| Člen týmu (GitHub handle) | Role v týmu | Přidělená oblast / Issue | 
| ----- | ----- | ----- | 
| **@Castulik** | Team Leader | Koordinace, Code Reviews, správa `main`, dokumentace | 
| **@pmikes1** | Vývojář (Dev 1) | Issue 16: CI pipeline / Issue 17: Datová vrstva | 
| **@stepanch284** | Vývojář (Dev 2) | Issue 16: CI dokumentace & badge / Issue 17: DTO & mapping | 
| **@luxat26** | Vývojář (Dev 3) | Issue 17: API endpoint & kontroler testy | 

## 2. Záznam aktivit a revizí (Changelog & Reviews)

| Datum | Člen týmu | Aktivita / Typ zásahu | Větev / PR | Commit / Hash | Reviewer | Výsledek / Stav | 
| ----- | ----- | ----- | ----- | ----- | ----- | ----- | 
| *2026-10-12* | @Castulik | Založení struktury issues, zadání a inicializace worklogu | `main` | `init` | – | Dokončeno | 
| *2026-10-12* | @pmikes1 | Tvorba `ResponseTimeMiddleware.cs`, hlavička `X-Response-Time-Ms`, varování nad 500 ms | `feature/api-profiling` | PR | @Castulik | V revizi (doplnění unit testů) | 
| *2026-10-12* | @stepanch284 | Příprava zátěžových testů v k6 (`tests/load-tests/`), dokumentace a spouštěcí návod | `feature/load-testing` | PR | @Castulik | Schváleno / Merged | 
| *2026-10-12* | @luxat26 | Doplnění `[ProducesResponseType]`, RFC 9457 ProblemDetails schémat a XML ukázek | `feature/openapi-docs` | PR | @Castulik | Schváleno / Merged | 
| *2026-10-21* | @pmikes1 | Issue #16: Nastavení CI pipeline v GitHub Actions (`.github/workflows/ci.yml`) | `feature/ci-workflow` | PR #19 | @Castulik | Schváleno / Merged | 
| *2026-10-21* | @stepanch284 | Issue #16: CI status badge a nová sekce o CI v `README.md` | `feature/ci-docs` | PR #22 | @Castulik | Schváleno / Merged | 
| *2026-10-21* | @pmikes1 | Issue #17: Datový model (`DueAtUtc`), `ITodoRepository.AddRangeAsync` a unit testy | `feature/repeat-data-layer` | PR #20 | @Castulik | Schváleno / Merged | 
| *2026-10-21* | @stepanch284 | Issue #17: `RepeatTodoItemRequest`, `TodoItemDto.DueAtUtc`, `TodoMapper.ToEntities` a unit testy | `feature/repeat-dto-mapping` | PR #21 | @Castulik | Schváleno / Merged | 
| *2026-10-21* | @luxat26 | Issue #17: Endpoint `POST /api/todos/repeat` v `TodosController.cs` a testy s Moq | `feature/repeat-endpoint` | PR #23 | @Castulik | Schváleno / Merged | 
| *2026-10-21* | @Castulik | Issue #17: Doplnění dokumentace endpointu do `README.md` a testovacích dotazů do `.http` | `feature/repeat-docs` | PR | @pmikes1 | V revizi (čeká na review) | 

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