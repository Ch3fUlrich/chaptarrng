using System;

namespace NzbDrone.Core.Books
{
    public class WantedEditionSelection
    {
        public int? EditionId { get; set; }
        public string Language { get; set; }
        public string Isbn { get; set; }
        public string Asin { get; set; }
        public BookMediaType? MediaType { get; set; }
        public bool AsNewVariant { get; set; }
    }

    public class WantedEditionResult
    {
        public Book Book { get; set; }
        public bool Created { get; set; }
    }
}
