import http from "k6/http";
import { sleep } from "k6";
import { check } from "k6";

/**
 * Options for the k6 test
 * @type {Object}
 */
export let options = {
    vus: 1, // 1 user looping for 10 iterations
    iterations: 10, // 10 iterations
};

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
    const avgResponseTime = data.metrics.http_req_duration.avg;
    console.log(`Average Response Time: ${avgResponseTime} ms`);
    return {
        stdout: avgResponseTime, // Output the data to stdout
    };
}
