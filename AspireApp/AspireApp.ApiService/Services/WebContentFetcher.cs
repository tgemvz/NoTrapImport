using Microsoft.Playwright;

namespace AspireApp.ApiService.Services;

public class WebContentFetcher
{
    public async Task<string> GetHtmlContentAsync(string url, CancellationToken cancellationToken)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true // false for debugging
        });

        var page = await browser.NewPageAsync();
        var response = await page.GotoAsync(url, new PageGotoOptions { });
        
        await page.WaitForTimeoutAsync(1000);
        var finalHtmlContent = await page.ContentAsync();

        return response is { Ok: false } ? throw new Exception("shit went wrong") : finalHtmlContent;
    }
}