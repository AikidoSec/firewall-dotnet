import http from "k6/http";
import { sleep, check } from "k6";
import { Trend } from "k6/metrics";

/**
 * Options for the k6 test
 * @type {Object}
 */
export let options = {
    vus: 1, // 1 user looping for 10 iterations
    iterations: 10, // 10 iterations
};

// Create a Trend metric to track response times
let GET_TREND = new Trend("GET_TREND");

/**
 * Main function for the k6 test
 */
export default function () {
    // Check if APP_URL is defined
    if (!__ENV.APP_URL) {
        console.error("::error::APP_URL environment variable is not set.");
        throw new Error("APP_URL environment variable is not set.");
    }
    if (!__ENV.AIKIDO_URL || !__ENV.AIKIDO_REALTIME_URL) {
        console.error(
            "::error::AIKIDO_URL or AIKIDO_REALTIME_URL environment variable is not set."
        );
        throw new Error(
            "AIKIDO_URL or AIKIDO_REALTIME_URL environment variable is not set."
        );
    }
    // log the env variables that start with AIKIDO
    console.log(
        "AIKIDO_URL:",
        __ENV.AIKIDO_URL,
        "AIKIDO_REALTIME_URL:",
        __ENV.AIKIDO_REALTIME_URL,
        "AIKIDO_TOKEN:",
        __ENV.AIKIDO_TOKEN
    );

    // Make a GET request to the /health endpoint
    let res = http.get(`${__ENV.APP_URL}/health`);
    GET_TREND.add(res.timings.duration);

    // Check if the response status is 200
    check(res, {
        "is status 200": (r) => r.status === 200,
    });

    // Sleep for 200ms between requests
    sleep(0.2);
}
