using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Frontend.Components.Pages
{
    // Ensure the HttpClient is registered in your Program.cs or equivalent startup file
    public class ProductClassificationPageModel(IHttpClientFactory httpClientFactory) : PageModel
    {
        private const string ApiBaseUrl = "http://localhost:5531"; // *** CHANGE THIS TO YOUR ACTUAL API URL ***

        [BindProperty]
        public string InputUrl { get; set; } = string.Empty;

        public ProductModel? ResultModel { get; set; }

        public string? ErrorMessage { get; set; }

        public string ClassificationColor { get; set; } = "black";

        public async Task OnPostAsync()
        {
            ErrorMessage = null;
            ResultModel = null;
            ClassificationColor = "black";

            if (string.IsNullOrWhiteSpace(InputUrl))
            {
                ErrorMessage = "Please enter a URL.";
                return;
            }

            try
            {
                var client = httpClientFactory.CreateClient();
                // Construct the full API URL. The 'url' path parameter expects the full input URL.
                var apiUrl = $"{ApiBaseUrl}/api/Product/classification/{Uri.EscapeDataString(InputUrl)}";

                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    
                    // The API returns a JSON string model *inside* an ActionResult<string>.
                    // We need to deserialize the resulting JSON string into our model.
                    var resultJson = JsonSerializer.Deserialize<string>(jsonString);
                    if (resultJson != null)
                    {
                        ResultModel = JsonSerializer.Deserialize<ProductModel>(resultJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        CalculateClassificationColor();
                    }
                    else
                    {
                        ErrorMessage = "API returned an empty or invalid string result.";
                    }
                }
                else
                {
                    ErrorMessage = $"Error calling API: {response.StatusCode}. Content: {await response.Content.ReadAsStringAsync()}";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"An unexpected error occurred: {ex.Message}";
            }
        }

        private void CalculateClassificationColor()
        {
            if (ResultModel != null)
            {
                double value = ResultModel.ProductClassification;
                // Clamp the value to the 1-100 range
                double clampedValue = Math.Clamp(value, 1, 100);

                // HSL Hue calculation (0-120 degrees): 
                // We want 1 (best/green) to be 120 and 100 (worst/red) to be 0.
                // Scale 1-100 to 0-1 (normalized)
                double normalized = (clampedValue - 1) / 99.0; 
                // Invert the scale (1 is 0, 100 is 1) and map to 0-120 range
                int hue = (int)((1.0 - normalized) * 120.0); 

                // Set the color string for CSS
                ClassificationColor = $"hsl({hue}, 70%, 50%)"; // Using 70% saturation and 50% lightness for a vibrant color
            }
        }
    }

    // Model to match the expected API return structure
    public class ProductModel
    {
        public string ProductName { get; set; } = string.Empty;
        public string ProductDescription { get; set; } = string.Empty;
        public double ProductClassification { get; set; } // Value from 1 to 100
    }
}