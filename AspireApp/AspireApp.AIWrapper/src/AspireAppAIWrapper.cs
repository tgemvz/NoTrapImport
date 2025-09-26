using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;

public class AspireAppAIWrapper
{
    private readonly string _apiKey;
    private readonly string _endpoint;
    private readonly string _ragEndpoint;

    public AspireAppAIWrapper()
    {
        var apiKey = Environment.GetEnvironmentVariable("SWISS_AI_PLATFORM_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("SWISS_AI_PLATFORM_API_KEY environment variable is not set.");
        }
        _apiKey = apiKey;
        _endpoint = "https://api.swisscom.com/layer/swiss-ai-weeks/apertus-70b/v1/chat/completions";

        _ragEndpoint = "http://localhost:8001/search/query";
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
        result.ProductUrl = request.ProductUrl;
        return result;
    }

    public async Task<ProductClassificationResponse> GetProductClassificationAsync(ProductIdentificationRequest request, CancellationToken cancellationToken)
    {
        var requestDesc = request.ProductDescription ?? "";

        var relevantLegalContext = await GetRagResult<RagResult>(requestDesc); // use product description as query for RAG

        var legalContextInfo = relevantLegalContext.Text;
        var urltoLegalDocs = relevantLegalContext.Url != null ? relevantLegalContext.Url : "N/A";

        var schemaString = GetJsonSchema<ProductClassificationResponse>();
        var systemMessage = "You are a Productclassifier." +
                            "Use the following legal context to determine the legality of the product that the users provides: " +
                            legalContextInfo +
                            "Next are the url(s) to the legal documents: " +
                            urltoLegalDocs +
                            "If ProductLegality cannot be definitively conclusively determined, it should be assigned a value within the range [0, 1]." +
                            "When ProductLegality is above 0.5 the product is considered legal, otherwise illegal." +
                            "Respond with JSON only that exactly matches the schema: " +
                            schemaString +
                            "Do not include any text outside of the JSON object."
                           ;

        var result = await GetChatMessageSemiStructuredOutput<ProductClassificationResponse>(requestDesc, systemMessage, cancellationToken);
        result.Id = request.Id;
        result.ProductUrl = request.ProductUrl;
        result.LinkToLegalDocuments = [urltoLegalDocs];

        return result;
    }

    public async Task<T> GetRagResult<T>(string request) where T : class
    {
        HttpClient http;
        HttpRequestMessage ragReq;
        HttpResponseMessage ragResp;
        JsonDocument ragDoc;


        //TODO: get Relevant Text Context from DocumentRetrievalService based on "request"
        http = new HttpClient();
        var ragPayload = new
        {
            query = request,
            top_k = 1
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
            var firstDoc = ragDoc.RootElement[0];

            var maybe = JsonSerializer.Deserialize<T>(firstDoc, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (maybe != null) return maybe;
        }

        throw new InvalidOperationException($"Failed to convert LLM response to {typeof(T).Name}. Response: {ragRespJson}");
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
                    productUrl = new { typeW = "string" },
                    productName = new { typeW = "string" },
                    productDescription = new { type = "string" },
                    productCategory = new { type = "string" },
                    ean = new { type = "string" },
                },
                required = new[] { "productName", "productDescription", "productCategory", "ean", "productUrl" },
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
                    productUrl = new { typeW = "string" },
                    productLegality = new { type = "number" },
                    isLegal = new { type = "boolean" },
                    legalExplanation = new { type = "string" },
                    linkToLegalDocuments = new { type = "array", items = new { type = "string" } },
                },
                required = new[] { "productLegality", "isLegal", "legalExplanation", "linkToLegalDocuments", "productUrl" },
                additionalProperties = false
            }
        }
    };
}