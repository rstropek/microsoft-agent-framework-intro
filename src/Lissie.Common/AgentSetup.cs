using System.ClientModel.Primitives;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;
using OpenAI.Responses;

namespace Lissie.Common;

public static class AgentSetup
{
    public const string EndpointKey = "AZURE_OPENAI_ENDPOINT";
    public const string DeploymentKey = "AZURE_OPENAI_DEPLOYMENT_NAME";
    public const string TokenScope = "https://cognitiveservices.azure.com/.default";

    public static ReasoningEffort DefaultReasoningEffort => ReasoningEffort.Low;

    public static AzureCliCredential Credential { get; } = new();

    public static IConfiguration Configuration => field ??= new ConfigurationBuilder()
        .AddUserSecrets(typeof(AgentSetup).Assembly, optional: true)
        .AddEnvironmentVariables()
        .Build();

    public static AzureOpenAISettings Settings => field ??= Configuration.GetAzureOpenAISettings();

    public static ResponsesClient CreateResponsesClient() => Configuration.CreateResponsesClient();

    public static IChatClient CreateChatClient() => Configuration.CreateChatClient();

    extension(IConfiguration configuration)
    {
        public AzureOpenAISettings GetAzureOpenAISettings()
        {
            var endpoint = configuration[EndpointKey];
            var deployment = configuration[DeploymentKey];
            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment))
            {
                throw new InvalidOperationException($"""
                    Azure OpenAI is not configured ({EndpointKey}: {(string.IsNullOrWhiteSpace(endpoint) ? "missing" : "ok")}, {DeploymentKey}: {(string.IsNullOrWhiteSpace(deployment) ? "missing" : "ok")}).
                    Set both once for all projects of this repository (shared UserSecretsId):
                      dotnet user-secrets set {EndpointKey} "https://<resource>.openai.azure.com/" --project src/Lissie.Common
                      dotnet user-secrets set {DeploymentKey} "<deployment>" --project src/Lissie.Common
                    or export them as environment variables. Authentication uses the Azure CLI login (az login).
                    """);
            }

            return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
                ? new(uri, deployment)
                : throw new InvalidOperationException($"{EndpointKey} is not an absolute URI: '{endpoint}'.");
        }

        public ResponsesClient CreateResponsesClient()
        {
            var settings = configuration.GetAzureOpenAISettings();
            return new OpenAIClient(
                    new BearerTokenPolicy(Credential, TokenScope),
                    new OpenAIClientOptions { Endpoint = settings.ResponsesEndpoint })
                .GetResponsesClient();
        }

        public IChatClient CreateChatClient() =>
            configuration.CreateResponsesClient()
                .AsIChatClient(configuration.GetAzureOpenAISettings().Deployment)
                .AsBuilder()
                .ConfigureOptions(options => options.Reasoning ??= new() { Effort = DefaultReasoningEffort })
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
}
