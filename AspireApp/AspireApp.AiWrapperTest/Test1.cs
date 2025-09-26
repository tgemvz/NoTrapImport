namespace AspireApp.AiWrapperTest;

[TestClass]
public sealed class AIWrapperTest
{

    const string LEGAL_CONTEXT_INFO = "Schusswaffen sind verboten, Messer sind erlaubt. Spielzeugwaffen sind bedingt erlaubt";

    static bool AssertAPIKeyIsSet()
    {
        // Skip integration test if API key is not provided
        var apiKey = Environment.GetEnvironmentVariable("SWISS_AI_PLATFORM_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Assert.Inconclusive("Environment variable 'SWISS_AI_PLATFORM_API_KEY' is not set. Skipping network-dependent test.");
            return false;
        }

        return true;
    }

    [TestMethod]
    public async Task GetMessage_Returns_NonEmptyString()
    {
        if (!AssertAPIKeyIsSet()) { return; }

        // Create instance (class is in global namespace)
        var wrapper = new AspireAppAIWrapper();

        string result;
        try
        {
            result = await wrapper.GetChatMessage("How old ist the universe?");
        }
        catch (Exception ex)
        {
            Assert.Fail($"GetMessage threw an exception: {ex.Message}");
            return;
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(result), "GetMessage returned null or whitespace.");
    }

    [TestMethod]
    public async Task GetMessage_StrucutredOutput()
    {
        if (!AssertAPIKeyIsSet()) { return; }

        // Create instance (class is in global namespace)
        var wrapper = new AspireAppAIWrapper();

        ProductIdentificationResponse? result = null;
        try
        {
            var schemaString = AspireAppAIWrapper.GetJsonSchema<ProductIdentificationResponse>();
            var systemMessage = "You are a Productidentifier." +
                               "Respond with JSON only that exactly matches the schema: " +
                               schemaString +
                               "Do not include any text outside of the JSON object."
                               ;
            result = await wrapper.GetChatMessageSemiStructuredOutput<ProductIdentificationResponse>("", systemMessage, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Assert.Fail($"GetMessage threw an exception: {ex.Message}");
            return;
        }

        Assert.IsTrue(null != result, "GetMessage returned null or whitespace.");
    }

    [TestMethod]
    public async Task GetMessage_StrucutredOutputOfUnsupportedClass()
    {
        if (!AssertAPIKeyIsSet()) { return; }

        // Create instance (class is in global namespace)
        var wrapper = new AspireAppAIWrapper();

        ProductClassificationBase? result = null;
        try
        {
            var schemaString = AspireAppAIWrapper.GetJsonSchema<ProductClassificationBase>();
            var systemMessage = "You are a Productidentifier." +
                               "Respond with JSON only that exactly matches the schema: " +
                               schemaString +
                               "Do not include any text outside of the JSON object."
                               ;
            result = await wrapper.GetChatMessageSemiStructuredOutput<ProductClassificationBase>("", systemMessage, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Assert.IsTrue(ex is NotSupportedException, $"GetMessage threw an unexpected exception type: {ex.GetType().Name}");
            return;
        }

        Assert.IsTrue(null != result, "GetMessage returned null or whitespace.");
    }

    [TestMethod]
    public async Task GetMessage_StrucutredOutput_ClassificationForbidden()
    {
        if (!AssertAPIKeyIsSet()) { return; }

        // Create instance (class is in global namespace)
        var wrapper = new AspireAppAIWrapper();

        ProductClassificationResponse? result = null;
        try
        {
            var userMessage = "Neue Jagdwaffe mit Zielfernrohr und Laserpointer. Kaliber 7.62mm, Magazin 30 Schuss. SKU 445343003";

            var schemaString = AspireAppAIWrapper.GetJsonSchema<ProductClassificationResponse>();
            var systemMessage = "You are a Productclassifier." +
                                LEGAL_CONTEXT_INFO +
                                "Respond with JSON only that exactly matches the schema: " +
                                schemaString +
                                "Do not include any text outside of the JSON object."
                               ;
            ;
            result = await wrapper.GetChatMessageSemiStructuredOutput<ProductClassificationResponse>(userMessage, systemMessage, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Assert.IsTrue(ex is NotSupportedException, $"GetMessage threw an unexpected exception type: {ex.GetType().Name}");
            return;
        }

        Assert.IsTrue(result.ProductLegality.HasValue && 0 == result.ProductLegality, "ProductLegality is not 'forbidden' as expected.");
    }

    [TestMethod]
    public async Task GetMessage_StrucutredOutput_ClassifiactionAllowed()
    {
        if (!AssertAPIKeyIsSet()) { return; }

        // Create instance (class is in global namespace)
        var wrapper = new AspireAppAIWrapper();

        ProductClassificationResponse? result = null;
        try
        {
            var userMessage = "Neues Taschenmesser mit Klinge aus rostfreiem Stahl und ergonomischem Griff. EAN 1234567890123";

            var schemaString = AspireAppAIWrapper.GetJsonSchema<ProductClassificationResponse>();
            var systemMessage = "You are a Productclassifier." +
                                LEGAL_CONTEXT_INFO +
                                "Respond with JSON only that exactly matches the schema: " +
                                schemaString +
                                "Do not include any text outside of the JSON object."
                               ;
            ;
            result = await wrapper.GetChatMessageSemiStructuredOutput<ProductClassificationResponse>(userMessage, systemMessage, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Assert.IsTrue(ex is NotSupportedException, $"GetMessage threw an unexpected exception type: {ex.GetType().Name}");
            return;
        }

        Assert.IsTrue(result.ProductLegality.HasValue && result.ProductLegality > 0, "ProductLegality is not 'allowed' as expected.");
    }

    [TestMethod]
    public async Task GetMessage_StrucutredOutput_ClassifiactionMiddle()
    {
        if (!AssertAPIKeyIsSet()) { return; }

        // Create instance (class is in global namespace)
        var wrapper = new AspireAppAIWrapper();

        ProductClassificationResponse? result = null;
        try
        {
            var userMessage = "Blastmaster 4000 Gunsword mit integrierter Klingenverlängerung und Energieblaster. Achtung darf nicht in die Hände von Kleinkindern gelangen, da die Gefahr besteht sich ein Auge auszuschissen! Ein Spielzeug für Cosplayer. SKU 9988776655";

            var schemaString = AspireAppAIWrapper.GetJsonSchema<ProductClassificationResponse>();
            var systemMessage = "You are a Productclassifier." +
                                LEGAL_CONTEXT_INFO +
                                "Respond with JSON only that exactly matches the schema: " +
                                schemaString +
                                "Do not include any text outside of the JSON object."
                               ;
            ;
            result = await wrapper.GetChatMessageSemiStructuredOutput<ProductClassificationResponse>(userMessage, systemMessage, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Assert.IsTrue(ex is NotSupportedException, $"GetMessage threw an unexpected exception type: {ex.GetType().Name}");
            return;
        }

        Assert.IsTrue(result.ProductLegality.HasValue && result.ProductLegality != 0 && result.ProductLegality != 1,
         "ProductLegality is not 'restricted' as expected.");
    }

}
