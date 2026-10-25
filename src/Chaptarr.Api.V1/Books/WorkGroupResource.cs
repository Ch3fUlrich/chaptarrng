using System.Collections.Generic;

namespace Chaptarr.Api.V1.Books
{
    public class WorkVersionResource
    {
        public int BookId { get; set; }
        public string Title { get; set; }
        public string MediaType { get; set; }
        public string Language { get; set; }
        public bool IsVariant { get; set; }
        public bool Monitored { get; set; }
        public bool HasFiles { get; set; }
    }

    public class WorkGroupResource
    {
        public string WorkKey { get; set; }
        public string Title { get; set; }
        public List<WorkVersionResource> Versions { get; set; }
    }
}
