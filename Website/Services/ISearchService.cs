using System.Linq;

namespace NuGetGallery
{
    public interface ISearchService
    {
        SearchResults Search(IQueryable<Package> packages, string searchTerm, string sortExpression, int take);
    }

    public class SearchResults
    {
        public IQueryable<Package> Packages { get; set; }

        public int Count { get; set; }
    }

}