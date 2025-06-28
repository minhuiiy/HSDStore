namespace CPTStore.Models
{
    public class ErrorViewModel
    {
        public required string RequestId { get; set; }
        public int? StatusCode { get; set; }
        public required string Message { get; set; }
        public string? Title { get; set; }
        public string? SuggestedAction { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
