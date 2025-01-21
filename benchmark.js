import http from "k6/http";
import { sleep } from "k6";
import { check, Trend } from "k6";

/**
 * Options for the k6 test
 * @type {Object}
 */
export let options = {
    vus: 1, // 1 user looping for 10 iterations
    iterations: 10, // 10 iterations
};

// Create a Trend metric to track response times
let responseTimeTrend = new Trend("response_time");

/**
 * Main function for the k6 test
 */
export default function () {
    // Make a GET request to the /health endpoint
    let res = http.get(`${__ENV.APP_URL}/health`);

    // Record the response time
    responseTimeTrend.add(res.timings.duration);

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
    const avgResponseTime = data.metrics.response_time.avg;
    console.log(`Average Response Time: ${avgResponseTime} ms`);
    return {
        stdout: avgResponseTime, // Output the data to stdout
    };
}
