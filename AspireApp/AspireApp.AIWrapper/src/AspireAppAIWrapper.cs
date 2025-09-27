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
You are a strict JSON-only responder. Produce exactly one top-level JSON value that VALIDATES against the provided schema and nothing else.

Requirements:
1) Output only valid JSON. Do not include any text, explanation, metadata, markdown, or code fences before or after the JSON.
2) Do not add, remove, or rename properties. Only include properties defined in the schema.
3) Respect JSON types exactly:
   - string -> JSON string (use quotes)
   - number/integer -> numeric literal (no quotes)
   - boolean -> true or false (no quotes)
   - array -> JSON array (use [] if empty)
   - object -> JSON object (include nested properties or set them to null)
4) If a property value cannot be determined, set it to null. Do not create extra explanatory fields.
5) Arrays must be present as arrays (even if empty). Objects must be present as objects (or null if unknown and allowed).
6) No comments, no trailing commas, and ensure the JSON parses with a strict JSON parser (e.g. JSON.parse).
7) Keep the output minimal and syntactically correct so it can be deserialized directly into the target type.

Schema:
{{schemaString}}

Now return only the JSON that conforms to the schema.
""";
        return prompt;
    }
    public static string GetPromptForClassification(Tuple<string, string>[] legalContextInfo)
    {
        var prompt =
$$"""
You are an expert assistant for Swiss import law. Use ONLY the legal context provided below and nothing else to evaluate whether the product may be legally imported into Switzerland.

Strict output rules (will be combined with a JSON schema prompt — output MUST be valid JSON that matches that schema and nothing else):
1) Output only a single JSON object. Do NOT include any prose, commentary, headings, or markdown outside the JSON.
2) Do NOT add, remove, or rename properties. Follow property names and types exactly.
3) productLegality: return a number between 0.0 and 1.0 (inclusive) representing the likelihood the product is legal to import.
   - 1.0 = clearly legal under the provided context
   - 0.0 = clearly illegal under the provided context
   - If uncertain, choose an appropriate probability and justify briefly in legalExplanation.
4) isLegal: MUST be true if productLegality >= 0.5, otherwise false. Ensure boolean is consistent with productLegality.
5) legalExplanation: give a concise explanation (1–3 short sentences) that cites ONLY information from the provided legal context. If the context is insufficient, state "Insufficient information" and list the specific missing facts needed to decide.
6) linkToLegalDocuments: include only URLs present in the provided legal context or RAG result. Do NOT fabricate or modify URLs. If none are available, return an empty array [].
7) If a property value cannot be determined, set it to null (except linkToLegalDocuments which should be [] if none).
8) No extra properties, no comments, no trailing commas, and ensure strict JSON parseability.

Legal context (use exactly as provided; do NOT invent facts):
{{legalContextInfo}}

Decision rule: productLegality >= 0.5 => considered legal; productLegality < 0.5 => considered illegal.
""";
        return prompt;
    }

    public async Task<ProductIdentificationResponse> GetProductIdentificationAsync(ProductClassificationRequest request, CancellationToken cancellationToken)
    {
        var schemaString = GetJsonSchema<ProductIdentificationResponse>();
        var systemMessage = "You are a Productidentifier that identifies products." +
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
        var requestDesc = request.ProductDescription ?? "no description available";

        var relevantLegalContext = await GetRagResult<RagResult[]>(requestDesc); // use product description as query for RAG

        var legalContextInfo = relevantLegalContext.Select(c => new Tuple<string, string>(c.Url, c.Text)).ToArray();

        var schemaString = GetJsonSchema<ProductClassificationResponse>();
        var systemMessage = "You are a Productclassifier that determines if a product is allowed to be imported." +
                            GetPromptForClassification(legalContextInfo) +
                            GetPromptForJsonSchema(schemaString)
                           ;

        var result = await GetChatMessageSemiStructuredOutput<ProductClassificationResponse>(requestDesc, systemMessage, cancellationToken);
        result.Id = request.Id;
        result.ProductUrl = request.ProductUrl;

        _logger.LogDebug($"Classification result (Deserialized): {JsonSerializer.Serialize(result)}");

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
                _logger.LogDebug($"RAG result: {ragDoc}");
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
        // Attempt to deserialize the payload into the requested class T.
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // 1) Try direct deserialization.
        try
        {
            var direct = JsonSerializer.Deserialize<T>(payloadStr, options);
            if (direct != null)
            {
                _logger.LogDebug($"LLM direct deserialization successful in GetChatMessageSemiStructuredOutput: {payloadStr}");
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
                    _logger.LogDebug($"LLM extracted deserialization successful in GetChatMessageSemiStructuredOutput: {jsonOnly}");
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