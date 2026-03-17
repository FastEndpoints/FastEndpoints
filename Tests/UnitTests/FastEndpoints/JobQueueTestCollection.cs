using Xunit;

namespace JobQueue;

[CollectionDefinition(nameof(JobQueueTestCollection), DisableParallelization = true)]
public sealed class JobQueueTestCollection;
