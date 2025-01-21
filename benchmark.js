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
        return;
    }

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

/**
 * Function to calculate and print the average response time
 * @param {Object} data - The data object containing metrics
 * @returns {Object} - The summary output
 */
export function handleSummary(data) {
    // Extract the average response time from the GET_TREND metric
    const avgResponseTime = data.metrics.GET_TREND
        ? data.metrics.GET_TREND.values.avg
        : 0;

    // Print the average response time to the console
    console.log(`Average Response Time: ${avgResponseTime}ms`);

    return {};
}
