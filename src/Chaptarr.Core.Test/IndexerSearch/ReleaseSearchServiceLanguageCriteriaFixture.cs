using System;
using System.Collections.Generic;
using System.Reflection;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;

namespace Chaptarr.Core.Test.IndexerSearch
{
    [TestFixture]
    public class ReleaseSearchServiceLanguageCriteriaFixture
    {
        [Test]
        public void ReleaseSearchServiceLanguageCriteria_CriteriaCarriesLanguage()
        {
            var author = new Author
            {
                Id = 10,
                Name = "Frank Herbert",
                EbookMonitored = true,
                EbookQualityProfileId = 2
            };
            var book = new Book
            {
                Id = 101,
                Title = "Dune",
                TitleSlug = "dune_wanted_ebook_deu_101",
                Author = author,
                AuthorId = author.Id,
                MediaType = BookMediaType.Ebook,
                EbookMonitored = true,
                AddOptions = new AddBookOptions { AddType = BookAddType.Manual },
                BaseBookId = "hc:1",
                HardcoverBookId = "hc:1",
                LanguageCode = "deu",
                Editions = new List<Edition>
                {
                    new Edition
                    {
                        Id = 1,
                        Title = "Dune",
                        Monitored = true,
                        Language = "deu",
                        ReadingFormatId = 3
                    }
                }
            };
            var authorCatalog = new List<Book> { book };

            var decisionMaker = DispatchProxy.Create<IMakeDownloadDecision, DecisionMakerProxy>();
            var subject = BuildSubject(decisionMaker);

            subject.BookSearch(book, authorCatalog, false, true, false).GetAwaiter().GetResult();

            var criteria = ((DecisionMakerProxy)(object)decisionMaker).LastSearchCriteria;

            Assert.That(criteria, Is.Not.Null, "The book search criteria should have been produced");
            Assert.That(criteria.Language, Is.EqualTo("deu"));
        }

        private static ReleaseSearchService BuildSubject(IMakeDownloadDecision decisionMaker)
        {
            return new ReleaseSearchService(
                DispatchProxy.Create<IIndexerFactory, IndexerFactoryProxy>(),
                DispatchProxy.Create<IBookService, BookServiceProxy>(),
                DispatchProxy.Create<IAuthorService, AuthorServiceProxy>(),
                null,
                decisionMaker,
                LogManager.GetLogger(nameof(ReleaseSearchServiceLanguageCriteriaFixture)));
        }

        private class BookServiceProxy : DispatchProxy
        {
            public int CatalogLoads { get; private set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod.Name == nameof(IBookService.GetBooksByAuthor))
                {
                    CatalogLoads++;
                    return new List<Book>();
                }

                throw new NotImplementedException(targetMethod.Name);
            }
        }

        private class AuthorServiceProxy : DispatchProxy
        {
            public int AuthorLoads { get; private set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod.Name == nameof(IAuthorService.GetAuthor))
                {
                    AuthorLoads++;
                    throw new AssertionException("The attached author should have been reused");
                }

                throw new NotImplementedException(targetMethod.Name);
            }
        }

        private class IndexerFactoryProxy : DispatchProxy
        {
            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod.Name == nameof(IIndexerFactory.AutomaticSearchEnabled) ||
                    targetMethod.Name == nameof(IIndexerFactory.InteractiveSearchEnabled))
                {
                    return new List<IIndexer>();
                }

                throw new NotImplementedException(targetMethod.Name);
            }
        }

        private class DecisionMakerProxy : DispatchProxy
        {
            public int AuthorCatalogCount { get; private set; }
            public List<int> SearchBookIds { get; set; } = new();
            public bool MonitoredBooksOnly { get; private set; } = true;
            public BookSearchCriteria LastSearchCriteria { get; private set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod.Name == nameof(IMakeDownloadDecision.GetSearchDecision))
                {
                    var criteria = (SearchCriteriaBase)args[1];
                    AuthorCatalogCount = criteria.AuthorCatalog?.Count ?? 0;
                    SearchBookIds = criteria.Books?.ConvertAll(book => book.Id) ?? new List<int>();
                    MonitoredBooksOnly = criteria.MonitoredBooksOnly;
                    LastSearchCriteria = criteria as BookSearchCriteria;
                    return new List<DownloadDecision>();
                }

                throw new NotImplementedException(targetMethod.Name);
            }
        }
    }
}
