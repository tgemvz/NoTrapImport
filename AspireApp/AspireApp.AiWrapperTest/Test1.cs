namespace AspireApp.AiWrapperTest;

[TestClass]
public sealed class AIWrapperTest
{
    [TestMethod]
    public async Task GetMessage_Returns_NonEmptyString()
    {
        // Skip integration test if API key is not provided
        var apiKey = Environment.GetEnvironmentVariable("SWISS_AI_PLATFORM_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Assert.Inconclusive("Environment variable 'SWISS_AI_PLATFORM_API_KEY' is not set. Skipping network-dependent test.");
            return;
        }

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
}
