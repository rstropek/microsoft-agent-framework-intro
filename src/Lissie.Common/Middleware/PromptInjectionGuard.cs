using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Lissie.Common.Middleware;

/// <summary>IChatClient middleware (security): obvious prompt injections are answered without calling the model.</summary>
public sealed partial class PromptInjectionGuard(ILogger logger)
{
    public const string Refusal =
        "Your Majesty, my instructions are not negotiable, and the treat cabinet remains closed. This attempt has been logged for the Primary Human.";

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options, IChatClient innerClient, CancellationToken cancellationToken) =>
        Inspect(messages, options) ?? await innerClient.GetResponseAsync(messages, options, cancellationToken);

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options, IChatClient innerClient,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (Inspect(messages, options) is { } refusal)
        {
            foreach (var update in refusal.ToChatResponseUpdates())
            {
                yield return update;
            }

            yield break;
        }

        await foreach (var update in innerClient.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            yield return update;
        }
    }

    private ChatResponse? Inspect(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        if (messages.LastOrDefault(m => m.Role == ChatRole.User) is not { Text: { Length: > 0 } text }
            || Injection().Match(text) is not { Success: true } match)
        {
            return null;
        }

        LogBlocked(logger, match.Value);

        // Keep the service-side conversation id: the session continues as if the attempt never reached the model
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, Refusal))
        {
            ConversationId = options?.ConversationId,
            FinishReason = ChatFinishReason.ContentFilter,
        };
    }

    [GeneratedRegex(@"\b(ignore|disregard|forget)\b.{0,30}\binstructions\b|\bsystem\s+prompt\b|\byou\s+are\s+now\b|\bdeveloper\s+mode\b", RegexOptions.IgnoreCase)]
    private static partial Regex Injection();

    [LoggerMessage(EventId = 21, Level = LogLevel.Warning, Message = "Security: prompt injection blocked before reaching the model (matched '{Match}')")]
    private static partial void LogBlocked(ILogger logger, string match);
}
