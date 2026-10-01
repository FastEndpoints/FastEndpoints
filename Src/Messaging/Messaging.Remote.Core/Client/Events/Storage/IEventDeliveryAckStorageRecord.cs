namespace FastEndpoints;

/// <summary>
/// persisted inbox metadata required by delivery-ack subscribers.
/// </summary>
public interface IEventDeliveryAckStorageRecord : IEventStorageRecord
{
    /// <summary>
    /// UTC deadline through which the delivery key must remain unique, including completed and expired inbox rows.
    /// the library sets this from the hub replay deadline plus the subscriber clock-skew allowance.
    /// persist this field with the inbox row. null identifies an ordinary, non-ACK inbox row.
    /// </summary>
    DateTime? RetainUntil { get; set; }
}
