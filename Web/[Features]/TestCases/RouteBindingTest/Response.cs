#pragma warning disable CS8618
namespace TestCases.RouteBindingTest
{
    public class Response
    {
        public string String { get; set; }
        public bool Bool { get; set; }
        public int Int { get; set; }
        public long Long { get; set; }
        public double Double { get; set; }
        public decimal Decimal { get; set; }

        public string FromBody { get; set; }
    }
}