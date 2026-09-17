import http from 'k6/http';
import { check } from 'k6';
import { BASE_URL } from './lib.js';

export const options = {
  discardResponseBodies: true,
  scenarios: {
    baseline: {
      executor: 'ramping-arrival-rate',
      startRate: 100,
      timeUnit: '1s',
      preAllocatedVUs: 100,
      maxVUs: 1500,
      stages: [
        { duration: '10s', target: 100 },
        { duration: '15s', target: 300 },
        { duration: '15s', target: 500 },
      ],
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
  },
};

export default function () {
  const res = http.get(`${BASE_URL}/`);
  check(res, { 'status 200': (r) => r.status === 200 });
}