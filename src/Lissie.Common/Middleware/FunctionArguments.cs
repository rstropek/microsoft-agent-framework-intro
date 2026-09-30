using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Lissie.Common.Middleware;

internal static class FunctionArguments
{
    extension(AIFunctionArguments arguments)
    {
        // Arguments arrive as JsonElement (from the model) or as CLR values (from code); normalize both
        public bool TryGet<T>(string name, [NotNullWhen(true)] out T? value)
        {
            value = default;
            if (!arguments.TryGetValue(name, out var raw) || raw is null)
            {
                return false;
            }

            if (raw is T typed)
            {
                value = typed;
                return true;
            }

            try
            {
                var json = raw as JsonElement? ?? JsonSerializer.SerializeToElement(raw, AIJsonUtilities.DefaultOptions);
                value = json.Deserialize<T>(AIJsonUtilities.DefaultOptions);
                return value is not null;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
