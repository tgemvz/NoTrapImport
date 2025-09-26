using AspireApp.ApiService.Services;
using FluentAssertions;

namespace AspireApp.Tests;

[TestClass]
public class WebContentFetcherTests
{
    private const string TestUrl =
        "https://www.temu.com/ch/translated-text-1-stuck-erwachsenen-scherzstift-pen-robuste-kunststoff-button-batterie-betriebene-schreib-und-zeichenwerkzeug-mit-bunter-streifenmuster-lustige-neuentdeckungsgeschenk-fur-weihnachten-halloween-erntedankfest-partys-und-erwachsenenfeiern-scherzgeschenk-spielerischer-design-ergonomisches-griff-g-601101181489357.html?_oak_name_id=8592420318916749874&_oak_mp_inf=EM2hlrOs1ogBGhZnb29kc180ZHJlYWhfcmVjb21tZW5kILnFpeaCMw%3D%3D&top_gallery_url=https%3A%2F%2Fimg.kwcdn.com%2Fproduct%2Ffancy%2F180a2167-a577-4ac7-8aa1-b140a7f15f13.jpg&spec_gallery_id=10432101592&refer_page_sn=10032&refer_source=10016&freesia_scene=11&_oak_freesia_scene=11&_oak_rec_ext_1=MTQ3&_oak_gallery_order=1763782615%2C1855054196%2C692515201%2C685435107%2C265967552&refer_page_el_sn=200444&_x_sessn_id=wlyx8zxb21&refer_page_name=goods&refer_page_id=10032_1758880119901_f0w8dtxx8o";
    
    [TestMethod]
    public async Task TestMyFluff()
    { 
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        
        var webContentFetcher = new WebContentFetcher();
        var htmlOutput = await webContentFetcher.GetHtmlContentAsync(TestUrl, cancellationToken);
        
        htmlOutput.Should().NotBeNullOrWhiteSpace();
    }
}