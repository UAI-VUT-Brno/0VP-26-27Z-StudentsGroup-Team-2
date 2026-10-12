// Zátěžový test API UAI.ZeroVP v nástroji k6 (https://k6.io).
// Simuluje souběžné uživatele, kteří střídavě čtou seznam úkolů a informace o instanci.
// Spuštění: k6 run tests/load-tests/api-load-test.js  (podrobnosti v README.md)

import http from 'k6/http';
import { check, group, sleep } from 'k6';

// Adresu API lze přepsat: k6 run -e BASE_URL=http://localhost:8080 ...
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5258';

export const options = {
    // Průběh zátěže: náběh na 50 souběžných virtuálních uživatelů, výdrž, doběh.
    stages: [
        { duration: '10s', target: 20 },
        { duration: '20s', target: 50 },
        { duration: '20s', target: 50 },
        { duration: '10s', target: 0 },
    ],
    thresholds: {
        // Hlavní podmínka: 95 % všech požadavků musí odpovědět do 500 ms.
        http_req_duration: ['p(95)<500'],
        // Stejná podmínka zvlášť pro každý endpoint, aby pomalý endpoint nezakryl rychlý.
        'http_req_duration{endpoint:todos}': ['p(95)<500'],
        'http_req_duration{endpoint:info}': ['p(95)<500'],
        // Chybových odpovědí smí být méně než 1 %.
        http_req_failed: ['rate<0.01'],
    },
};

export default function () {
    group('GET /api/todos', () => {
        const res = http.get(`${BASE_URL}/api/todos`, { tags: { endpoint: 'todos' } });
        check(res, {
            'todos: status 200': (r) => r.status === 200,
            'todos: vrací pole': (r) => Array.isArray(r.json()),
        });
    });

    group('GET /api/info', () => {
        const res = http.get(`${BASE_URL}/api/info`, { tags: { endpoint: 'info' } });
        check(res, {
            'info: status 200': (r) => r.status === 200,
        });
    });

    // Pauza mezi akcemi uživatele (think time), ať test odpovídá reálnému chování.
    sleep(1);
}
