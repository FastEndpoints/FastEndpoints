using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class OperationParameterFinalizer
{
    internal static void Finalize(OpenApiOperation operation)
    {
        if (GlobalConfig.IsUsingAspVersioning)
            RemoveDuplicates(operation);

        Sort(operation);
    }

    static void RemoveDuplicates(OpenApiOperation operation)
    {
        if (operation.Parameters is not { Count: > 1 })
            return;

        var seen = new HashSet<(string Name, ParameterLocation Location)>();

        for (var i = operation.Parameters.Count - 1; i >= 0; i--)
        {
            var p = operation.Parameters[i];

            if (p.Name is null || p.In is not { } loc)
                continue;

            if (!seen.Add((p.Name, loc)))
                operation.Parameters.RemoveAt(i);
        }
    }

    static void Sort(OpenApiOperation operation)
    {
        if (operation.Parameters is not { Count: > 1 })
            return;

        var sorted = operation.Parameters
                              .OrderBy(
                                  p => p.In switch
                                  {
                                      ParameterLocation.Path => 0,
                                      ParameterLocation.Query => 1,
                                      ParameterLocation.Header => 2,
                                      ParameterLocation.Cookie => 3,
                                      _ => 4
                                  })
                              .ToList();

        operation.Parameters.Clear();

        foreach (var p in sorted)
            operation.Parameters.Add(p);
    }
}
