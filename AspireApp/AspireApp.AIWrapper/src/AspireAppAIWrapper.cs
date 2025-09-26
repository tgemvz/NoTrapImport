using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;

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

    public async Task<ProductIdentificationResponse> GetProductIdentificationAsync(ProductClassificationRequest request, CancellationToken cancellationToken)
    {
        var schemaString = GetJsonSchema<ProductIdentificationResponse>();
        var systemMessage = "You are a Productidentifier." +
                           "Respond with JSON only that exactly matches the schema: " +
                           schemaString +
                           "Do not include any text outside of the JSON object."
                           ;

        var result = await GetChatMessageSemiStructuredOutput<ProductIdentificationResponse>(request.HtmlContent, systemMessage, cancellationToken);
        return result;
    }

    public async Task<ProductClassificationResponse> GetProductClassificationAsync(string request, CancellationToken cancellationToken)
    {
        //TODO: get Relevant Text Context from DocumentRetrievalService based on "request"
        var legalContextInfo = "Schusswaffen sind verboten, Messer sind erlaubt.";

        var schemaString = GetJsonSchema<ProductClassificationResponse>();
        var systemMessage = "You are a Productclassifier." +
                            legalContextInfo +
                            "Respond with JSON only that exactly matches the schema: " +
                            schemaString +
                            "Do not include any text outside of the JSON object."
                           ;

        var result = await GetChatMessageSemiStructuredOutput<ProductClassificationResponse>(request, systemMessage, cancellationToken);
        return result;
    }

    public async Task<string> GetChatMessage(string userMessage, CancellationToken cancellationToken = default)
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
    public async Task<T> GetChatMessageSemiStructuredOutput<T>(string userMessage, string systemMessage, CancellationToken cancellationToken = default)
    where T : class
    {
        var userMessageSanitized = HttpUtility.HtmlEncode(userMessage);

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };

        var payload = new
        {
            model = "swiss-ai/Apertus-70B",
            messages = new[]
               {
                new { role = "system", content = systemMessage },
                new { role = "user", content = userMessageSanitized }
            },
            //max_tokens = 8192,               // increase as allowed by the provider / model
            temperature = 0.0,               // lower=deterministic, higher=randomness
            top_p = 1.0,                     // nucleus sampling
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

        var payloadStr = result ?? respJson;

        // Attempt to deserialize the payload into the requested class T.
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // 1) Try direct deserialization.
        try
        {
            var direct = JsonSerializer.Deserialize<T>(payloadStr, options);
            if (direct != null) return direct;
        }
        catch (JsonException)
        {
            // fall through to try extracting JSON substring
        }

        // 2) If the model returned extra text, attempt to extract the first JSON object substring.
        int start = payloadStr.IndexOf('{');
        int end = payloadStr.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            var jsonOnly = payloadStr.Substring(start, end - start + 1);
            try
            {
                var extracted = JsonSerializer.Deserialize<T>(jsonOnly, options);
                if (extracted != null) return extracted;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to deserialize extracted JSON to {typeof(T).Name}: {ex.Message}\nExtracted JSON: {jsonOnly}\nFull response: {payloadStr}", ex);
            }
        }

        // 3) If still not deserializable, provide helpful error.
        throw new InvalidOperationException($"Failed to convert LLM response to {typeof(T).Name}. Response: {payloadStr}");

    }

    public static string GetJsonSchema<T>() where T : class
    {
        var schemaString = "";
        switch (typeof(T).Name)
        {
            case nameof(ProductIdentificationResponse):
                schemaString = JsonSerializer.Serialize(AspireAppAIWrapperJsonSchemas.JsonSchemaProductIdentification);
                break;
            case nameof(ProductClassificationResponse):
                schemaString = JsonSerializer.Serialize(AspireAppAIWrapperJsonSchemas.JsonSchemaProductClassification);
                break;
            default:
                throw new NotSupportedException($"Type {typeof(T).Name} is not supported for semi-structured output.");
        }

        return schemaString;
    }
}

public class AspireAppAIWrapperJsonSchemas
{
    public static object JsonSchemaProductIdentification = new
    {
        type = "json_schema",
        json_schema = new
        {
            name = "product_identification_response",
            strict = true,
            schema = new
            {
                type = "object",
                properties = new
                {
                    productName = new { typeW = "string" },
                    productDescription = new { type = "string" },
                    productCategory = new { type = "string" },
                    ean = new { type = "string" },
                },
                required = new[] { "productName", "productDescription", "productCategory" },
                additionalProperties = false
            }
        }
    };

    public static object JsonSchemaProductClassification = new
    {
        type = "json_schema",
        json_schema = new
        {
            name = "product_classification_response",
            strict = true,
            schema = new
            {
                type = "object",
                properties = new
                {
                    productLegality = new { type = "number" },
                    isLegal = new { type = "boolean" },
                    legalExplanation = new { type = "string" },
                    linkToLegalDocuments = new { type = "string" },
                },
                required = new[] { "productName", "productDescription", "productCategory" },
                additionalProperties = false
            }
        }
    };
}