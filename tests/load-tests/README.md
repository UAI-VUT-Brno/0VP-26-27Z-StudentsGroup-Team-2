# Zátěžové testy (Load Testing)

Zátěžový test API v nástroji [k6](https://k6.io). Skript
[`api-load-test.js`](api-load-test.js) simuluje souběžné uživatele, kteří
opakovaně volají `GET /api/todos` a `GET /api/info`.

## Průběh zátěže

| Fáze | Délka | Souběžní uživatelé (VU) |
|---|---|---|
| Náběh | 10 s | 0 → 20 |
| Náběh | 20 s | 20 → 50 |
| Výdrž | 20 s | 50 |
| Doběh | 10 s | 50 → 0 |

Každý virtuální uživatel v jedné iteraci zavolá oba endpointy a pak 1 s čeká (think time).

## Podmínky (thresholds)

| Metrika | Podmínka | Význam |
|---|---|---|
| `http_req_duration` | `p(95)<500` | **95 % všech požadavků odpoví do 500 ms** |
| `http_req_duration{endpoint:todos}` | `p(95)<500` | totéž jen pro `/api/todos` |
| `http_req_duration{endpoint:info}` | `p(95)<500` | totéž jen pro `/api/info` |
| `http_req_failed` | `rate<0.01` | méně než 1 % chybových odpovědí |

Pokud některá podmínka neprojde, k6 skončí nenulovým návratovým kódem, takže test
lze použít i v CI.

## Spuštění

1. Nainstalovat k6:

   ```bash
   brew install k6            # macOS
   winget install k6          # Windows
   # Linux / Docker: https://grafana.com/docs/k6/latest/set-up/install-k6/
   ```

2. Z kořene repozitáře spustit API (Release, ať měření není zkreslené debug buildem):

   ```bash
   dotnet run -c Release --project src/UAI.ZeroVP.API --launch-profile http
   ```

   API naslouchá na `http://localhost:5258`.

3. V druhém terminálu spustit test:

   ```bash
   k6 run tests/load-tests/api-load-test.js
   ```

   Jinou adresu API (např. kontejner) lze předat proměnnou `BASE_URL`:

   ```bash
   k6 run -e BASE_URL=http://localhost:8080 tests/load-tests/api-load-test.js
   ```

## Výsledek testu

Reálný běh: 12. 10. 2026, k6 v2.3.0, macOS (Apple Silicon), API v Release
na `localhost` se čtyřmi ukázkovými úkoly. Všechny podmínky prošly.

```text
     execution: local
        script: tests/load-tests/api-load-test.js
        output: -

     scenarios: (100.00%) 1 scenario, 50 max VUs, 1m30s max duration (incl. graceful stop):
              * default: Up to 50 looping VUs for 1m0s over 4 stages (gracefulRampDown: 30s, gracefulStop: 30s)


  █ THRESHOLDS 

    http_req_duration
    ✓ 'p(95)<500' p(95)=943.44µs

      {endpoint:info}
      ✓ 'p(95)<500' p(95)=695.24µs

      {endpoint:todos}
      ✓ 'p(95)<500' p(95)=1.02ms

    http_req_failed
    ✓ 'rate<0.01' rate=0.00%


  █ TOTAL RESULTS 

    checks_total.......: 6198    102.148409/s
    checks_succeeded...: 100.00% 6198 out of 6198
    checks_failed......: 0.00%   0 out of 6198

    ✓ todos: status 200
    ✓ todos: vrací pole
    ✓ info: status 200

    HTTP
    http_req_duration..............: avg=516.63µs min=104µs med=444µs   max=17.92ms p(90)=804.9µs p(95)=943.44µs
      { endpoint:info }............: avg=405µs    min=105µs med=345µs   max=13.11ms p(90)=555µs   p(95)=695.24µs
      { endpoint:todos }...........: avg=628.25µs min=104µs med=582.5µs max=17.92ms p(90)=894µs   p(95)=1.02ms  
      { expected_response:true }...: avg=516.63µs min=104µs med=444µs   max=17.92ms p(90)=804.9µs p(95)=943.44µs
    http_req_failed................: 0.00%  0 out of 4132
    http_reqs......................: 4132   68.09894/s

    EXECUTION
    iteration_duration.............: avg=1s       min=1s    med=1s      max=1.02s   p(90)=1s      p(95)=1s      
    iterations.....................: 2066   34.04947/s
    vus............................: 3      min=2         max=50
    vus_max........................: 50     min=50        max=50

    NETWORK
    data_received..................: 3.1 MB 51 kB/s
    data_sent......................: 324 kB 5.3 kB/s
```

**Shrnutí:** 4 132 požadavků, 0 % chyb, 95. percentil odezvy 0,94 ms –
hluboko pod limitem 500 ms. Data jsou v paměti procesu, proto je odezva takto nízká;
při nasazení s databází nebo za reverzní proxy je vhodné test zopakovat.
