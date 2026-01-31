using FastEndpoints;
using System.Collections.ObjectModel;

namespace NativeAotChecker.Endpoints;

// Request with various collection types
public class CollectionTypesRequest
{
    public int[] IntArray { get; set; } = [];
    public List<string> StringList { get; set; } = [];
    public HashSet<int> IntHashSet { get; set; } = [];
    public Queue<string> StringQueue { get; set; } = new();
    public Stack<int> IntStack { get; set; } = new();
    public LinkedList<string> StringLinkedList { get; set; } = new();
    public ObservableCollection<int> IntObservable { get; set; } = [];
    public SortedSet<string> StringSortedSet { get; set; } = [];
    public ImmutableItem[] ImmutableArray { get; set; } = [];
}

public class ImmutableItem
{
    public required string Name { get; init; }
    public required int Value { get; init; }
}

// Response
public class CollectionTypesResponse
{
    public int ArrayCount { get; set; }
    public int ListCount { get; set; }
    public int HashSetCount { get; set; }
    public int QueueCount { get; set; }
    public int StackCount { get; set; }
    public int LinkedListCount { get; set; }
    public int ObservableCount { get; set; }
    public int SortedSetCount { get; set; }
    public int ImmutableCount { get; set; }
    public bool AllCollectionsBound { get; set; }
    public List<string> CollectionTypes { get; set; } = [];
}

/// <summary>
/// Tests various collection types (HashSet, Queue, Stack, etc.) in AOT mode.
/// AOT ISSUE: Each collection type requires specific JsonConverter.
/// Activator.CreateInstance for collection types uses reflection.
/// Generic collection interfaces need runtime type resolution.
/// </summary>
public class CollectionTypesEndpoint : Endpoint<CollectionTypesRequest, CollectionTypesResponse>
{
    public override void Configure()
    {
        Post("collection-types-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CollectionTypesRequest req, CancellationToken ct)
    {
        var collectionTypes = new List<string>();
        
        if (req.IntArray.Length > 0) collectionTypes.Add("int[]");
        if (req.StringList.Count > 0) collectionTypes.Add("List<string>");
        if (req.IntHashSet.Count > 0) collectionTypes.Add("HashSet<int>");
        if (req.StringQueue.Count > 0) collectionTypes.Add("Queue<string>");
        if (req.IntStack.Count > 0) collectionTypes.Add("Stack<int>");
        if (req.StringLinkedList.Count > 0) collectionTypes.Add("LinkedList<string>");
        if (req.IntObservable.Count > 0) collectionTypes.Add("ObservableCollection<int>");
        if (req.StringSortedSet.Count > 0) collectionTypes.Add("SortedSet<string>");
        if (req.ImmutableArray.Length > 0) collectionTypes.Add("ImmutableItem[]");

        await Send.OkAsync(new CollectionTypesResponse
        {
            ArrayCount = req.IntArray.Length,
            ListCount = req.StringList.Count,
            HashSetCount = req.IntHashSet.Count,
            QueueCount = req.StringQueue.Count,
            StackCount = req.IntStack.Count,
            LinkedListCount = req.StringLinkedList.Count,
            ObservableCount = req.IntObservable.Count,
            SortedSetCount = req.StringSortedSet.Count,
            ImmutableCount = req.ImmutableArray.Length,
            AllCollectionsBound = collectionTypes.Count >= 5,
            CollectionTypes = collectionTypes
        });
    }
}
