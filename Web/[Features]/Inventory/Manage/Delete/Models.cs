using System.ComponentModel;

namespace Inventory.Manage.Delete;

public class Request
{
    [DefaultValue("test default val")]
    public string ItemID { get; set; }
}
