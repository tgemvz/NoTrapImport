namespace AspireApp.RagService.Models
{
    public class DocumentResult
    { 
        public string Id { get; set; }
        public string Text { get; set; }
        public float Score { get; internal set; }
    }
}
