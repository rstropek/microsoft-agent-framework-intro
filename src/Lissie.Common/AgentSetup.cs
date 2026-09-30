using System.ClientModel.Primitives;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;

namespace Lissie.Common;

public static class AgentSetup
{
    private static IConfiguration Configuration => field ??= new ConfigurationBuilder()
        .AddUserSecrets(typeof(AgentSetup).Assembly, optional: true)
        .AddEnvironmentVariables()
        .Build();

    public static AzureOpenAISettings Settings => field ??= AzureOpenAISettings.From(Configuration);

    public static IChatClient CreateChatClient() => Configuration.CreateChatClient();

    extension(IConfiguration configuration)
    {
        // Same agent code, different model provider: only configuration changes
        public IChatClient CreateChatClient() => (configuration["LLM_PROVIDER"] switch
        {
            null or "" or "azure" => CreateAzureOpenAIClient(AzureOpenAISettings.From(configuration)),
            "openai-compatible" => CreateOpenAICompatibleClient(OpenAICompatibleSettings.From(configuration)),
            var provider => throw new InvalidOperationException($"Unknown LLM_PROVIDER '{provider}'. Use 'azure' (default) or 'openai-compatible'."),
        })
            .AsBuilder()
            .ConfigureOptions(options => options.Reasoning ??= new() { Effort = ReasoningEffort.Low })
            .Build();
    }

    extension(IHostApplicationBuilder builder)
    {
        public IHostApplicationBuilder AddLissieChatClient()
        {
            builder.Services.AddSingleton(_ => builder.Configuration.CreateChatClient());
            return builder;
        }
    }

    // Azure OpenAI, Responses API (history stored by the service), Entra ID via az login
    private static IChatClient CreateAzureOpenAIClient(AzureOpenAISettings settings) =>
        new OpenAIClient(
                new BearerTokenPolicy(new AzureCliCredential(), "https://cognitiveservices.azure.com/.default"),
                new OpenAIClientOptions { Endpoint = settings.ResponsesEndpoint })
            .GetResponsesClient()
            .AsIChatClient(settings.Deployment);

    // OpenRouter, Ollama, ...: Chat Completions (stateless, the agent keeps the history), API key
    private static IChatClient CreateOpenAICompatibleClient(OpenAICompatibleSettings settings) =>
        new OpenAIClient(settings.ApiKey, new OpenAIClientOptions { Endpoint = settings.Endpoint })
            .GetChatClient(settings.Model)
            .AsIChatClient();
}
