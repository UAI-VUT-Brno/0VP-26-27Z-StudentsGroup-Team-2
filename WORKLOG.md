# Team Worklog – Evidence práce a workflow

Tento dokument slouží k evidenci aktivity týmu, sledování procesu vývoje (`branch` → `PR` → `review` → `merge`) a přehledu o tom, kdo na čem pracoval a kdo prováděl code review.

## 1. Přehled členů týmu a rolí

| Člen týmu (GitHub handle) | Role v týmu | Přidělená oblast / Issue | 
| ----- | ----- | ----- | 
| **@Castulik** | Team Leader | Koordinace, Code Reviews, správa `main`, dokumentace | 
| **@pmikes1** | Vývojář (Dev 1) | Issue 1: Profilování odezvy a měřicí middleware | 
| **@stepanch284** | Vývojář (Dev 2) | Issue 1: Zátěžové testy (load testing) | 
| **@luxat26** | Vývojář (Dev 3) | Issue 2: OpenAPI 3.x specifikace a Swagger dokumentace | 

## 2. Záznam aktivit a revizí (Changelog & Reviews)

| Datum | Člen týmu | Aktivita / Typ zásahu | Větev / PR | Commit / Hash | Reviewer | Výsledek / Stav | 
| ----- | ----- | ----- | ----- | ----- | ----- | ----- | 
| *2026-10-12* | @Castulik | Založení struktury issues, zadání a inicializace worklogu | `main` | `init` | – | Dokončeno | 
| *2026-10-12* | @pmikes1 | Tvorba `ResponseTimeMiddleware.cs`, hlavička `X-Response-Time-Ms`, varování nad 500 ms | `feature/api-profiling` | PR | @Castulik | V revizi (doplnění unit testů) | 
| *2026-10-12* | @stepanch284 | Příprava zátěžových testů v k6 (`tests/load-tests/`), dokumentace a spouštěcí návod | `feature/load-testing` | PR | @Castulik | Schváleno / Merged | 
| *2026-10-12* | @luxat26 | Doplnění `[ProducesResponseType]`, RFC 9457 ProblemDetails schémat a XML ukázek | `feature/openapi-docs` | PR | @Castulik | Schváleno / Merged | 

## 3. Poznámky k průběhu a řešení problémů (Retrospektiva)

* **Issue 9 (Latence & Zátěžové testy):**
  * **@pmikes1:** `ResponseTimeMiddleware` vrací hlavičku `X-Response-Time-Ms` a při překročení limitu 500 ms zapisuje varování do logu. Běžná odezva serveru je pod 1 ms.
  * **@stepanch284:** Zátěžový scénář v k6 potvrdil 0,00% chybovost a bezpečné splnění limitu 500 ms:
    * *50 uživatelů:* 4 130 požadavků, p(95) = 2,13 ms.

* **Issue 10 (OpenAPI & Swagger):**
  * **@luxat26:** U všech 404 odpovědí doplněny anotace `[ProducesResponseType(typeof(ProblemDetails), 404)]` dle RFC 9457. Doplněny XML `<param>` značky a `<example>` ukázky v DTO modelech.
  * Specifikace na `/openapi/v1.json` plně odpovídá OpenAPI 3.1, Swagger UI na `/swagger` je funkční a všech 55 testů prochází bez chyb.