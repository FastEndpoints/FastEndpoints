using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FastEndpoints")]

namespace FastEndpoints;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
sealed class NotImplementedAttribute : Attribute { }