namespace Lissie.Common;

public sealed record AzureOpenAISettings(Uri Endpoint, string Deployment)
{
    public Uri ResponsesEndpoint => Endpoint.AbsolutePath.TrimEnd('/').EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase)
        ? Endpoint
        : new UriBuilder(Endpoint) { Path = $"{Endpoint.AbsolutePath.TrimEnd('/')}/openai/v1/" }.Uri;
}
