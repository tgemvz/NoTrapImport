using Microsoft.Playwright;

namespace AspireApp.ApiService.Services;

public interface IWebContentFetcher
{
    Task<string> GetHtmlContentAsync(string url, CancellationToken cancellationToken);
}

public class WebContentFetcher : IWebContentFetcher
{
    public async Task<string> GetHtmlContentAsync(string url, CancellationToken cancellationToken)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false // Set to false to see the browser window for debugging
        });

        var page = await browser.NewPageAsync();

        // 1. Navigate to the URL and wait until the network is idle, 
        //    meaning JavaScript execution is likely complete.
        var response = await page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            // You can use the CancellationToken here if needed:
            Timeout = cancellationToken.IsCancellationRequested ? 1 : 30000 // 30 seconds default
        });

        // Optional: Wait an extra second to ensure all JS renders
        await page.WaitForTimeoutAsync(1000);

        // 2. Retrieve the *final* rendered HTML content, including all JS changes
        var finalHtmlContent = await page.ContentAsync();

        return response is { Ok: false } ? throw new Exception("shit went wrong") : finalHtmlContent;
    }
}