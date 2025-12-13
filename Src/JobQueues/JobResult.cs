using System.Diagnostics.CodeAnalysis;

namespace FastEndpoints;

/// <summary>
/// a wrapper for holding a job execution result as well as any progress details.
/// </summary>
/// <typeparam name="TResult">the type of the end result</typeparam>
/// <param name="totalSteps">the total number of steps the job is expected to have</param>
public class JobResult<TResult>(int totalSteps) : IJobResult where TResult : notnull
{
    /// <summary>
    /// the number of steps currently completed
    /// </summary>
    public int CurrentStep { get; set; }

    /// <summary>
    /// the number of total steps to be completed
    /// </summary>
    public int TotalSteps { get; set; } = totalSteps;

    /// <summary>
    /// the percentage of steps currently completed
    /// </summary>
    public int ProgressPercentage
        => TotalSteps == 0
               ? throw new NotSupportedException($"Reading '{nameof(ProgressPercentage)}' is not supported when '{TotalSteps}' hasn't been specified!")
               : CurrentStep * 100 / TotalSteps;

    /// <summary>
    /// the current status
    /// </summary>
    public string? CurrentStatus { get; set; }

    /// <summary>
    /// the end result
    /// </summary>
    public TResult? Result { get; set; }

    /// <summary>
    /// returns true if either the result is ready or if all the steps are complete.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Result))]
    public bool IsComplete => (TotalSteps > 0 && CurrentStep >= TotalSteps) || Result is not null;
}