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
| *2026-10-12* | @pmikes1 | Tvorba `ResponseTimeMiddleware.cs` a registrace v pipeline | `feature/api-profiling` | PR #... | @Castulik | *Čeká na review* | 
| *2026-10-12* | @stepanch284 | Příprava zátěžových testů v `tests/load-tests/` a `README.md` | `feature/load-testing` | PR #... | @Castulik | *Čeká na review* | 
| *2026-10-12* | @luxat26 | Doplnění anotací `[ProducesResponseType]` a XML komentářů | `feature/openapi-docs` | PR #... | @Castulik | *Čeká na review* | 

## 3. Poznámky k průběhu a řešení problémů (Retrospektiva)

*Tato sekce slouží pro záznam případných komplikací, které během řešení nastaly (např. překročení thresholdů latence, git konflikty, nejasnosti v modelech).*

* **Issue 1 (Latence & Testy):**

  * *Záznam:* [Zde po dokončení uveďte shrnutí naměřených hodnot odezvy v zátěži.]

* **Issue 2 (OpenAPI):**

  * *Záznam:* [Zde uveďte potvrzení shody s OpenAPI 3.x schématem a ověření ve Swagger UI.]
