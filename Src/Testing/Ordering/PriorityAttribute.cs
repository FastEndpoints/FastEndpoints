namespace FastEndpoints.Testing;

/// <summary>
/// attribute used to order tests within a test collection
/// </summary>
/// <param name="priority">the priority of the test method</param>
[AttributeUsage(AttributeTargets.Method)]
public class PriorityAttribute(int priority) : Attribute
{
    public int Priority { get; private set; } = priority;
}