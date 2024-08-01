namespace FastEndpoints.Testing;

/// <summary>
/// attribute used to order tests within a test collection and also test-collections themselves
/// </summary>
/// <param name="priority">the priority of the test method or the test-collection</param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class PriorityAttribute(int priority) : Attribute
{
    public int Priority { get; private set; } = priority;
}