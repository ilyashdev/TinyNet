import http from 'k6/http';
import { Counter } from 'k6/metrics';

export const BASE_URL = __ENV.BASE_URL || 'http://localhost:5768';

export const status200 = new Counter('status_200');
export const status503 = new Counter('status_503');
export const statusOther = new Counter('status_other');


export function countExpected503() {
  http.setResponseCallback(http.expectedStatuses(200, 503));
}


let reported = 0;

export function countStatus(res) {
  if (res.status === 200) status200.add(1);
  else if (res.status === 503) status503.add(1);
  else {
    statusOther.add(1);
    if (reported < 5) {
      reported++;
      console.log(`status=${res.status} error_code=${res.error_code} error=${res.error}`);
    }
  }
}


export function predict(n, queue, ms) {
  const throughput = n / (ms / 1000);
  const queueWait = queue / n;
  return {
    throughput,
    queueWait,
    maxLatency: queueWait + ms / 1000,
  };
}
