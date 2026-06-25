namespace FastEndpoints;

/// <summary>
/// job queue options for a planned command dispatched as a job.
/// </summary>
/// <param name="ExecuteAfter">date/time before which the job should not execute.</param>
/// <param name="ExpireOn">date/time after which the job should be considered expired.</param>
public sealed record JobDispatchOptions(DateTime? ExecuteAfter = null, DateTime? ExpireOn = null);
