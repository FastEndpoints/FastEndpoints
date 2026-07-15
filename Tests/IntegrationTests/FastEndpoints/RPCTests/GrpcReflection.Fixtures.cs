// two types that share a simple name across namespaces. they live in their own file because a file-scoped
// namespace can't be mixed with block-scoped ones.

namespace RemoteProcedureCalls.Billing
{
    public class Address
    {
        public string City { get; set; } = "";
    }
}

namespace RemoteProcedureCalls.Shipping
{
    public class Address
    {
        public string City { get; set; } = "";
    }
}
