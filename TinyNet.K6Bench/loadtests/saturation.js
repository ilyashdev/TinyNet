import http from 'k6/http';
import {sleep} from 'k6';
import exec from 'k6/execution';
import {BASE_URL, countExpected503, countStatus} from './lib.js';

const RATE = Number(__ENV.RATE || 20000);
const VUS = Number(__ENV.VUS || 256);
const DURATION = __ENV.DURATION || '20s';
const TARGET = __ENV.TARGET || '/load/io?ms=0';
const JITTER = Number(__ENV.JITTER ?? 1);

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
    if (JITTER > 0 && exec.vu.iterationInScenario === 0)
        sleep(Math.random() * JITTER);
    countStatus(http.get(`${BASE_URL}${TARGET}`));
}
