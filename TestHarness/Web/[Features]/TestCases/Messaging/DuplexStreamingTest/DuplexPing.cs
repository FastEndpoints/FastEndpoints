namespace TestCases.DuplexStreamingTest;

public class DuplexPing : IDuplexStreamCommand<DuplexPong>
{
    public int Number { get; set; }

    /// <summary>
    /// the handler ends the call after replying to this item
    /// </summary>
    public bool Stop { get; set; }

    /// <summary>
    /// the handler throws when it receives this item
    /// </summary>
    public bool Fail { get; set; }

    /// <summary>
    /// the client fails to serialize this item, which is a write failure that doesn't end the call by itself
    /// </summary>
    public bool FailSerialization { get; set; }

    /// <summary>
    /// optional filler for making an item exceed the client's max send message size
    /// </summary>
    public string Payload { get; set; }

    public int SerializationGuard
    {
        get => FailSerialization ? throw new InvalidOperationException("serialization failure") : 0;
        set { }
    }
}

public class DuplexPong
{
    public int Number { get; set; }
}
