global using FastEndpoints;
global using FastEndpoints.Security;
global using System.Text;
global using System.Text.Json;
global using System.Text.Json.Nodes;
global using System.Text.Json.Serialization;
global using Microsoft.AspNetCore.Http;
global using Web.Auth;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Int.FastEndpoints")]
[assembly: InternalsVisibleTo("Int.Swagger")]
[assembly: InternalsVisibleTo("Unit.FastEndpoints")]

namespace Web;

public class Program;