import http from 'k6/http';
import { BASE_URL, countExpected503, countStatus } from './lib.js';

const MODE = __ENV.MODE || 'clean';
const RATE = Number(__ENV.RATE || 300);

const oversizeHeader = 'a'.repeat(20000);

countExpected503();
http.setResponseCallback(http.expectedStatuses(200, 413, 503));

export const options = {
  discardResponseBodies: true,
  scenarios: {
    errors: {
      executor: 'constant-arrival-rate',
      rate: RATE,
      timeUnit: '1s',
      duration: '20s',
      preAllocatedVUs: 100,
      maxVUs: 2000,
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
  },
};

export function setup() {
  console.log(`режим ${MODE}, ${RATE} rps`);
}

export default function () {
  const params = MODE === 'oversize' ? { headers: { 'X-Big': oversizeHeader } } : {};
  countStatus(http.get(`${BASE_URL}/`, params));
}