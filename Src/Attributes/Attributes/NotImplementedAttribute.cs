using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FastEndpoints")]

namespace FastEndpoints;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
internal sealed class NotImplementedAttribute : Attribute { }