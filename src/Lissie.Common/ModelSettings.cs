using System.ClientModel;
using Microsoft.Extensions.Configuration;

namespace Lissie.Common;

/// <summary>Default provider: Azure OpenAI, authenticated with the Azure CLI login (az login).</summary>
public sealed record AzureOpenAISettings(Uri Endpoint, string Deployment)
{
    private const string Hint = """
        Set it once for all projects of this repository (shared UserSecretsId) or export it as an environment variable:
          dotnet user-secrets set AZURE_OPENAI_ENDPOINT "https://<resource>.openai.azure.com/" --project src/Lissie.Common
          dotnet user-secrets set AZURE_OPENAI_DEPLOYMENT_NAME "<deployment>" --project src/Lissie.Common
        """;

    public Uri ResponsesEndpoint => Endpoint.AbsolutePath.TrimEnd('/').EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase)
        ? Endpoint
        : new UriBuilder(Endpoint) { Path = $"{Endpoint.AbsolutePath.TrimEnd('/')}/openai/v1/" }.Uri;

    public static AzureOpenAISettings From(IConfiguration configuration) =>
        new(configuration.RequireUri("AZURE_OPENAI_ENDPOINT", Hint), configuration.Require("AZURE_OPENAI_DEPLOYMENT_NAME", Hint));
}

/// <summary>LLM_PROVIDER=openai-compatible: any OpenAI-compatible endpoint (OpenRouter, Ollama, ...) with an API key.</summary>
internal sealed record OpenAICompatibleSettings(Uri Endpoint, string Model, ApiKeyCredential ApiKey)
{
    private const string Hint = """
        Set LLM_ENDPOINT and LLM_MODEL as environment variables, the API key once in user-secrets:
          OpenRouter: LLM_ENDPOINT=https://openrouter.ai/api/v1  LLM_MODEL=z-ai/glm-5.3-flash
          Ollama:     LLM_ENDPOINT=http://localhost:11434/v1  LLM_MODEL=<local model>  LLM_API_KEY=ollama
          dotnet user-secrets set LLM_API_KEY "<key>" --project src/Lissie.Common
        """;

    public static OpenAICompatibleSettings From(IConfiguration configuration) => new(
        configuration.RequireUri("LLM_ENDPOINT", Hint),
        configuration.Require("LLM_MODEL", Hint),
        new(configuration.Require("LLM_API_KEY", Hint)));
}

internal static class RequiredSettings
{
    extension(IConfiguration configuration)
    {
        public string Require(string key, string hint) => configuration[key] is { } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"{key} is not configured.\n{hint}");

        public Uri RequireUri(string key, string hint) => Uri.TryCreate(configuration.Require(key, hint), UriKind.Absolute, out var uri)
            ? uri
            : throw new InvalidOperationException($"{key} is not an absolute URI.\n{hint}");
    }
}
