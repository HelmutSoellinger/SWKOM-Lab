namespace DMSystem.Contracts
{
    public class SearchResult
    {
        public string DocumentId { get; set; } = string.Empty;
        public int MatchCount { get; set; } = 0; // Number of matches for the search term
    }
}
