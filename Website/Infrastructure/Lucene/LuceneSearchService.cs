using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;

namespace NuGetGallery
{
    public class LuceneSearchService : ISearchService
    {
        private const int MaximumRecordsToReturn = 1000;
        private static readonly Sort _downloadCountSort = new Sort(new SortField("DownloadCount", SortField.INT, reverse: true));
        private static readonly Sort _publishedSort = new Sort(new SortField("PublishedDate", SortField.LONG, reverse: true));
        private static readonly Sort _idSort = new Sort(new SortField("Id", SortField.STRING));

        public SearchResults Search(IQueryable<Package> packages, string searchTerm, string sortExpression, int take)
        {
            if (String.IsNullOrEmpty(searchTerm))
            {
                throw new ArgumentException("Argument cannot be null or empty string.", "searchTerm");
            }

            int numberOfHits;
            var keys = SearchCore(searchTerm, GetSortField(sortExpression), out numberOfHits).Take(take);
            if (!keys.Any())
            {
                return new SearchResults { Packages = Enumerable.Empty<Package>().AsQueryable(), Count = 0 };
            }

            var results = packages.Where(p => keys.Contains(p.Key)).ToList();
            var lookup = results.ToDictionary(p => p.Key, p => p);

            return new SearchResults
            {
                Count = numberOfHits,
                Packages = keys.Select(key => LookupPackage(lookup, key))
                       .Where(p => p != null)
                       .AsQueryable()
            };
        }

        private static Package LookupPackage(Dictionary<int, Package> dict, int key)
        {
            Package package;
            dict.TryGetValue(key, out package);
            return package;
        }

        private static IList<int> SearchCore(string searchTerm, Sort sort, out int totalResults)
        {
            if (!Directory.Exists(LuceneCommon.IndexDirectory))
            {
                totalResults = 0;
                return new int[0];
            }

            using (var directory = new LuceneFileSystem(LuceneCommon.IndexDirectory))
            {
                var searcher = new IndexSearcher(directory, readOnly: true);
                var query = ParseQuery(searchTerm);
                var results = searcher.Search(query, filter: null, n: 1000, sort: sort);
                totalResults = results.totalHits;
                var keys = results.scoreDocs.Select(c => Int32.Parse(searcher.Doc(c.doc).Get("Key"), CultureInfo.InvariantCulture))
                                            .ToList();
                searcher.Close();
                return keys;
            }
        }

        private static Query ParseQuery(string searchTerm)
        {
            var fields = new Dictionary<string, float> { { "Id", 1.2f }, { "Title", 1.0f }, { "Tags", 1.0f }, { "Description", 0.8f }, { "Author", 0.6f } };
            var analyzer = new StandardAnalyzer(LuceneCommon.LuceneVersion);
            searchTerm = QueryParser.Escape(searchTerm).ToLowerInvariant();

            var queryParser = new MultiFieldQueryParser(LuceneCommon.LuceneVersion, fields.Keys.ToArray(), analyzer, fields);

            var conjuctionQuery = new BooleanQuery();
            conjuctionQuery.SetBoost(1.5f);
            var disjunctionQuery = new BooleanQuery();
            var wildCardQuery = new BooleanQuery();
            wildCardQuery.SetBoost(0.7f);
            var exactIdQuery = new TermQuery(new Term("Id-Exact", searchTerm));
            exactIdQuery.SetBoost(2.5f);
            var wildCardIdQuery = new TermQuery(new Term("Id-Exact", searchTerm + "*"));
            wildCardIdQuery.SetBoost(1.5f);

            foreach (var term in searchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                conjuctionQuery.Add(queryParser.Parse(term), BooleanClause.Occur.MUST);
                disjunctionQuery.Add(queryParser.Parse(term), BooleanClause.Occur.SHOULD);

                foreach (var field in fields)
                {
                    var wildCardTermQuery = new WildcardQuery(new Term(field.Key, term + "*"));
                    wildCardTermQuery.SetBoost(0.7f * field.Value);
                    wildCardQuery.Add(wildCardTermQuery, BooleanClause.Occur.SHOULD);
                }
            }

            return conjuctionQuery.Combine(new Query[] { exactIdQuery, wildCardIdQuery, conjuctionQuery, disjunctionQuery, wildCardQuery });
        }

        private static Sort GetSortField(string sortExpression)
        {
            if (String.IsNullOrEmpty(sortExpression) || sortExpression.Equals(Constants.PopularitySortOrder, StringComparison.OrdinalIgnoreCase))
            {
                return _downloadCountSort;
            }
            else if (sortExpression.Equals(Constants.RelevanceSortOrder, StringComparison.OrdinalIgnoreCase))
            {
                return Sort.RELEVANCE;
            }
            else if (sortExpression.Equals(Constants.RecentSortOrder, StringComparison.OrdinalIgnoreCase))
            {
                return _publishedSort;
            }
            else if (sortExpression.Equals(Constants.AlphabeticSortOrder, StringComparison.OrdinalIgnoreCase))
            {
                return _idSort;
            }
            return _downloadCountSort;
        }
    }

    public static class SearchExtensions
    {
        public static IQueryable<Package> Search(this ISearchService searchSvc, IQueryable<Package> packages, string searchTerm)
        {
            return searchSvc.Search(packages, searchTerm, Constants.RelevanceSortOrder, 1000).Packages;
        }
    }
}