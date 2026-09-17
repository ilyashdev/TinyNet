import http from 'k6/http';
import { BASE_URL, countExpected503, countStatus } from './lib.js';

const PROFILE = __ENV.PROFILE || 'io';
const MS = Number(__ENV.MS || 500);
const RATE = Number(__ENV.RATE || 100);

countExpected503();

export const options = {
  discardResponseBodies: true,
  scenarios: {
    compare: {
      executor: 'constant-arrival-rate',
      rate: RATE,
      timeUnit: '1s',
      duration: '30s',
      preAllocatedVUs: RATE,
      maxVUs: RATE * 20,
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
  },
};

export function setup() {
  console.log(`профиль ${PROFILE}, ${RATE} rps, обработчик ${MS} ms`);
}

export default function () {
  countStatus(http.get(`${BASE_URL}/load/${PROFILE}?ms=${MS}`));
}