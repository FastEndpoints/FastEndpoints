namespace FastEndpoints;

public interface IJobStorageRecord
{
    string QueueID { get; set; }
    object Command { get; set; }
    DateTime ExecuteAfter { get; set; }
    DateTime ExpireOn { get; set; }
    bool IsComplete { get; set; }
}
