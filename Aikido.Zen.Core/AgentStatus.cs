namespace Aikido.Zen.Core
{
    public class AgentStatus
    {
        public AgentStatus(ReportingStatusResult heartbeatStatus)
        {
            HeartbeatStatus = heartbeatStatus;
        }

        /// <summary>
        /// Gets the current heartbeat reporting status of the agent.
        /// This indicates whether the agent is successfully communicating with the Zen API
        /// through heartbeat events and other monitoring reports.
        /// </summary>
        /// <value>
        /// A <see cref="ReportingStatusResult"/> value indicating the current reporting status:
        /// - <see cref="ReportingStatusResult.NotReported"/>: No reporting attempts have been made
        /// - <see cref="ReportingStatusResult.Ok"/>: Agent is successfully reporting
        /// - <see cref="ReportingStatusResult.Expired"/>: Heartbeat reporting has expired
        /// - <see cref="ReportingStatusResult.Failure"/>: The most recent reporting attempt failed
        /// </value>
        public ReportingStatusResult HeartbeatStatus { get; }
    }

    public enum ReportingStatusResult
    {
        NotReported,
        Ok,
        Expired,
        Failure,
    }
}
