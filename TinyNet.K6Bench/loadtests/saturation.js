import http from 'k6/http';
import { BASE_URL, countExpected503, countStatus } from './lib.js';

const RATE = Number(__ENV.RATE || 20000);
const VUS = Number(__ENV.VUS || 256);
const DURATION = __ENV.DURATION || '20s';
const TARGET = __ENV.TARGET || '/load/io?ms=0';

countExpected503();

export const options = {
  discardResponseBodies: true,
  noConnectionReuse: __ENV.NOREUSE === '1',
  scenarios: {
    saturation: {
      executor: 'constant-arrival-rate',
      rate: RATE,
      timeUnit: '1s',
      duration: DURATION,
      preAllocatedVUs: VUS,
      maxVUs: VUS,
    },
  },
};

export default function () {
  countStatus(http.get(`${BASE_URL}${TARGET}`));
}