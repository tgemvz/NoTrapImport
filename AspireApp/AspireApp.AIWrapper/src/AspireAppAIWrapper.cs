using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;

public class AspireAppAIWrapper
{
    private readonly string _apiKey;
    private readonly string _endpoint;
    private readonly string _ragEndpoint;

    ILogger _logger;

    public AspireAppAIWrapper(ILogger logger)
    {
        _logger = logger;
        var apiKey = Environment.GetEnvironmentVariable("SWISS_AI_PLATFORM_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("SWISS_AI_PLATFORM_API_KEY environment variable is not set.");
        }
        _apiKey = apiKey;
        _endpoint = "https://api.swisscom.com/layer/swiss-ai-weeks/apertus-70b/v1/chat/completions";
        _ragEndpoint = "http://localhost:8001/search/semantic_query";
    }


    public static string GetPromptForJsonSchema(string schemaString)
    {
        var prompt =
$$"""
Respond with JSON only that exactly matches the schema:
{{schemaString}}
Do not include any text outside of the JSON object.
Do not make up additional JSON properties 
""";
        return prompt;
    }
    public static string GetPromptForClassification(Tuple<string, string>[] legalContextInfo)
    {
        var prompt =
$$"""
To determine if a product may be legally imported into Switerzland use following legal context(s):
{{string.Join("--------", legalContextInfo.Select(c => "Source: " + c.Item1 + " Text: " + c.Item2))}}
--------
Do not make up any legal context.
If the legal context does not provide sufficient information to determine the legality of the product,
respond with a value within the range [0, 1} and explain that in LegalExplanation.
Give the urls of the relevant context in the field LinkToLegalDocuments
When ProductLegality is above or equal 0.5 the product is considered legal, otherwise illegal
""";
        return prompt;
    }

    public async Task<ProductIdentificationResponse> GetProductIdentificationAsync(ProductClassificationRequest request, CancellationToken cancellationToken)
    {
        var schemaString = GetJsonSchema<ProductIdentificationResponse>();
        var systemMessage = "You are a Productidentifier that responds in JSON format only" +
                            GetPromptForJsonSchema(schemaString)
                           ;

        var result = await GetChatMessageSemiStructuredOutput<ProductIdentificationResponse>(request.HtmlContent, systemMessage, cancellationToken);
        result.ProductUrl = request.ProductUrl;
        result.Id = request.Id;
        result.RequestDate = request.RequestDate;
        _logger.LogDebug($"Identification result: {JsonSerializer.Serialize(result)}");
        return result;
    }

    public async Task<ProductClassificationResponse> GetProductClassificationAsync(ProductIdentificationRequest request, CancellationToken cancellationToken)
    {
        var requestDesc = request.ProductDescription ?? "";

        var relevantLegalContext = await GetRagResult<RagResult[]>(requestDesc); // use product description as query for RAG

        var legalContextInfo = relevantLegalContext.Select(c => new Tuple<string, string>(c.Url, c.Text)).ToArray();

        var schemaString = GetJsonSchema<ProductClassificationResponse>();
        var systemMessage = "You are a Productclassifier that responds in JSON format only" +
                            GetPromptForClassification(legalContextInfo) +
                            GetPromptForJsonSchema(schemaString)
                           ;

        var result = await GetChatMessageSemiStructuredOutput<ProductClassificationResponse>(requestDesc, systemMessage, cancellationToken);
        result.Id = request.Id;
        result.ProductUrl = request.ProductUrl;

        _logger.LogDebug($"Classification result: {JsonSerializer.Serialize(result)}");

        return result;
    }

    public async Task<T> GetRagResult<T>(string request) where T : class
    {
        HttpClient http;
        HttpRequestMessage ragReq;
        HttpResponseMessage ragResp;
        JsonDocument ragDoc;

        http = new HttpClient();
        var ragPayload = new
        {
            query = request,
            k = 3
        };
        var json = JsonSerializer.Serialize(ragPayload);
        ragReq = new HttpRequestMessage(HttpMethod.Post, _ragEndpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        ragResp = await http.SendAsync(ragReq, HttpCompletionOption.ResponseHeadersRead);
        ragResp.EnsureSuccessStatusCode();
        var ragRespJson = await ragResp.Content.ReadAsStringAsync();
        ragDoc = JsonDocument.Parse(ragRespJson);

        if (ragDoc.RootElement.ValueKind == JsonValueKind.Array && ragDoc.RootElement.GetArrayLength() > 0)
        {
            var maybe = JsonSerializer.Deserialize<T>(ragDoc, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (maybe != null)
            {
                _logger.LogDebug($"RAG result: {JsonSerializer.Serialize(maybe)}");
                return maybe;
            }
        }

        throw new InvalidOperationException($"Failed to convert LLM response to {typeof(T).Name}. Response: {ragRespJson}");
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
            top_p = 0.5,                     // nucleus sampling
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
        _logger.LogDebug($"LLM raw response in GetChatMessageSemiStructuredOutput: {respJson}");
        using var doc = JsonDocument.Parse(respJson);

        // Try common shapes: choices[0].message.content (OpenAI chat-completions) or choices[0].text / content[0].text
        string? result = null;

        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (first.TryGetProperty("message", out var messageEl) && messageEl.TryGetProperty("content", out var contentEl))
            {
                result = contentEl.GetString() ?? string.Empty;
            }
            else if (first.TryGetProperty("text", out var textEl))
            {
                result = textEl.GetString() ?? string.Empty;
            }
        }

        var payloadStr = result ?? respJson;
        _logger.LogDebug($"LLM extracted payload in GetChatMessageSemiStructuredOutput: {payloadStr}");
        // Attempt to deserialize the payload into the requested class T.
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // 1) Try direct deserialization.
        try
        {
            var direct = JsonSerializer.Deserialize<T>(payloadStr, options);
            if (direct != null)
            {
                _logger.LogDebug($"LLM direct deserialization successful in GetChatMessageSemiStructuredOutput: {JsonSerializer.Serialize(direct)}");
                return direct;
            }
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
                if (extracted != null)
                {
                    _logger.LogDebug($"LLM extracted deserialization successful in GetChatMessageSemiStructuredOutput: {JsonSerializer.Serialize(extracted)}");
                    return extracted;
                }
            }
            catch (Exception)
            {
                // fall through to final error
            }
        }

        switch (typeof(T).Name)
        {
            case nameof(ProductIdentificationResponse):
                var res1 = Activator.CreateInstance<T>();
                (res1 as ProductIdentificationResponse)!.ProductDescription = payloadStr;
                _logger.LogDebug($"LLM fallback deserialization successful in GetChatMessageSemiStructuredOutput: {JsonSerializer.Serialize(res1)}");
                return res1;

            case nameof(ProductClassificationResponse):
                var res2 = Activator.CreateInstance<T>();
                (res2 as ProductClassificationResponse)!.LegalExplanation = payloadStr;
                _logger.LogDebug($"LLM fallback deserialization successful in GetChatMessageSemiStructuredOutput: {JsonSerializer.Serialize(res2)}");
                return res2;

            default:
                throw new NotSupportedException($"Type {typeof(T).Name} is not supported for semi-structured output.");
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
        string? result = null;

        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (first.TryGetProperty("message", out var messageEl) && messageEl.TryGetProperty("content", out var contentEl))
            {
                result = contentEl.GetString() ?? string.Empty;
            }
            else if (first.TryGetProperty("text", out var textEl))
            {
                result = textEl.GetString() ?? string.Empty;
            }
        }

        return result ?? respJson;
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
                // required = new[] { "productName", "productDescription", "productCategory", "ean" },
                // additionalProperties = false
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
                    linkToLegalDocuments = new { type = "array", items = new { type = "string" } },
                },
                // required = new[] { "productLegality", "isLegal", "legalExplanation", "linkToLegalDocuments" },
                // additionalProperties = false
            }
        }
    };
}