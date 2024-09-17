namespace DMSystem
{
    public class Document
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateOnly LastModified { get; set; }
        public string Author { get; set; }
        public string? Description { get; set; }
        public string Content { get; set; }
    }
}
