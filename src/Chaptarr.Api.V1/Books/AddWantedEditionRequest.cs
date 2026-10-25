namespace Chaptarr.Api.V1.Books
{
    public class AddWantedEditionRequest
    {
        public int EditionId { get; set; }
        public string Language { get; set; }
        public string Isbn { get; set; }
        public string Asin { get; set; }
        public string MediaType { get; set; }
        public bool AsNewVariant { get; set; }

        // Whether to start an automatic search for the newly created wanted edition.
        // Defaults to false; the UI exposes this as an opt-in toggle.
        public bool SearchForNewBook { get; set; } = false;
    }
}
