using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class AspireAppAIWrapper
{

    private readonly string _apiKey;
    private readonly string _endpoint;
    public AspireAppAIWrapper()
    {
        var apiKey = Environment.GetEnvironmentVariable("SWISS_AI_PLATFORM_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("SWISS_AI_PLATFORM_API_KEY environment variable is not set.");
        }
        _apiKey = apiKey;
        _endpoint = "https://api.swisscom.com/layer/swiss-ai-weeks/apertus-70b/v1/chat/completions";
    }

    public async Task<string> GetChatMessage(string userMessage)
    {
        using var http = new HttpClient();

        var payload = new
        {
            model = "swiss-ai/Apertus-70B",
            messages = new[]
            {
                new { role = "user", content = userMessage }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        using var req = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        // Common auth header patterns: Bearer or Api-Key. Adjust if your platform requires a different header.
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

        resp.EnsureSuccessStatusCode();

        var respJson = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(respJson);

        // Try common shapes: choices[0].message.content (OpenAI chat-completions) or choices[0].text / content[0].text
        string result = null;

        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (first.TryGetProperty("message", out var messageEl) && messageEl.TryGetProperty("content", out var contentEl))
            {
                result = contentEl.GetString();
            }
            else if (first.TryGetProperty("text", out var textEl))
            {
                result = textEl.GetString();
            }
        }

        return result ?? respJson;
    }

}
