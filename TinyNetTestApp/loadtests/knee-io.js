import http from 'k6/http';
import { BASE_URL, countExpected503, countStatus, predict } from './lib.js';


const N = Number(__ENV.N || 256);
const QUEUE = Number(__ENV.QUEUE || 1024);
const MS = Number(__ENV.MS || 1000);

const p = predict(N, QUEUE, MS);


const FILL = 8;
const overload = Math.round(p.throughput + QUEUE / FILL);


const vusNeeded = Math.ceil(overload * p.maxLatency * 1.3);

countExpected503();

export const options = {
  discardResponseBodies: true,
  scenarios: {
    knee: {
      executor: 'ramping-arrival-rate',
      startRate: Math.round(p.throughput * 0.4),
      timeUnit: '1s',
      preAllocatedVUs: Math.ceil(p.throughput * (MS / 1000) * 1.5),
      maxVUs: vusNeeded,
      stages: [
        { duration: '10s', target: Math.round(p.throughput * 0.6) },
        { duration: '15s', target: Math.round(p.throughput * 0.6) },
        { duration: '15s', target: overload },
        { duration: '10s', target: overload },
      ],
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
  },
};

export function setup() {
  console.log(`N=${N} queue=${QUEUE} ms=${MS}`);
  console.log(`предсказание: полка ${p.throughput} rps, `
    + `ожидание в полной очереди ${p.queueWait}s, максимум ${p.maxLatency}s`);
  console.log(`перегруз ${overload} rps, VU до ${vusNeeded}`);
}

export default function () {
  countStatus(http.get(`${BASE_URL}/load/io?ms=${MS}`));
}