using System;
using System.Collections.Generic;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Metadata;
using NzbDrone.Core.Qualities;

namespace Chaptarr.Core.Test.DecisionEngine
{
    [TestFixture]
    public class ReleaseLanguageSpecificationVariantFixture
    {
        private static bool AllowUnknown = true;

        [SetUp]
        public void ResetAllowUnknown()
        {
            AllowUnknown = true;
        }

        private static ReleaseLanguageSpecification CreateSubject(params MetadataProfile[] profiles)
        {
            return new ReleaseLanguageSpecification(new StubMetadataProfileService(profiles), LanguageGateConfig.Create(AllowUnknown), LogManager.GetCurrentClassLogger());
        }

        private static MetadataProfile BuildBothLanguageProfile()
        {
            return new MetadataProfile
            {
                Id = 2,
                Name = "Variant Ebook Default",
                AllowedLanguages = "eng, deu"
            };
        }

        private static Book BuildVariantRow(string titleSlug)
        {
            return new Book
            {
                Id = 101,
                Title = "Dune",
                TitleSlug = titleSlug,
                MediaType = BookMediaType.Ebook,
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
        }

        private static Book BuildLegacyRow(string titleSlug)
        {
            return new Book
            {
                Id = 101,
                Title = "Dune",
                TitleSlug = titleSlug,
                MediaType = BookMediaType.Ebook,
                BaseBookId = "hc:1",
                HardcoverBookId = "hc:1",
                Editions = new List<Edition>
                {
                    new Edition
                    {
                        Id = 1,
                        Title = "Dune",
                        Monitored = true,
                        ReadingFormatId = 3
                    }
                }
            };
        }

        [Test]
        public void ReleaseLanguageSpecificationVariant_RejectsLanguageMismatch()
        {
            var profile = BuildBothLanguageProfile();

            var subject = CreateSubject(profile);
            var remoteBook = BuildRemoteBook(
                "Frank.Herbert.Dune.2021.ENGLiSH.RETAiL.ePub.eBOOK-GRP",
                profileId: 2,
                bookTitle: "Dune",
                authorName: "Frank Herbert",
                row: BuildVariantRow("dune_wanted_ebook_deu_101"));

            var decision = subject.IsSatisfiedBy(remoteBook, new BookSearchCriteria());

            Assert.That(decision.Accepted, Is.False);
        }

        [Test]
        public void ReleaseLanguageSpecificationVariant_UnknownReleaseRejectedForExplicitRowLanguage()
        {
            AllowUnknown = false;
            var profile = BuildBothLanguageProfile();

            var subject = CreateSubject(profile);
            var remoteBook = BuildRemoteBook(
                "Frank.Herbert.Dune.2021.RETAiL.ePub.eBOOK-GRP",
                profileId: 2,
                bookTitle: "Dune",
                authorName: "Frank Herbert",
                row: BuildVariantRow("dune_wanted_ebook_deu_101"));

            var decision = subject.IsSatisfiedBy(remoteBook, new BookSearchCriteria());

            Assert.That(decision.Accepted, Is.False);
        }

        [Test]
        public void ReleaseLanguageSpecificationVariant_UnknownReleaseAcceptedByDefaultForExplicitRowLanguage()
        {
            var subject = CreateSubject(BuildBothLanguageProfile());
            var remoteBook = BuildRemoteBook(
                "Frank.Herbert.Dune.2021.RETAiL.ePub.eBOOK-GRP",
                profileId: 2,
                bookTitle: "Dune",
                authorName: "Frank Herbert",
                row: BuildVariantRow("dune_wanted_ebook_deu_101"));

            var decision = subject.IsSatisfiedBy(remoteBook, new BookSearchCriteria());

            Assert.That(decision.Accepted, Is.True);
        }

        [Test]
        public void ReleaseLanguageSpecificationVariant_KnownMismatchStillRejectedWhenUnknownAllowed()
        {
            var subject = CreateSubject(BuildBothLanguageProfile());
            var remoteBook = BuildRemoteBook(
                "Frank.Herbert.Dune.2021.ENGLiSH.RETAiL.ePub.eBOOK-GRP",
                profileId: 2,
                bookTitle: "Dune",
                authorName: "Frank Herbert",
                row: BuildVariantRow("dune_wanted_ebook_deu_101"));

            var decision = subject.IsSatisfiedBy(remoteBook, new BookSearchCriteria());

            Assert.That(decision.Accepted, Is.False);
        }

        [Test]
        public void ReleaseLanguageSpecificationVariant_CriteriaLanguageUnknownReleaseAcceptedByDefault()
        {
            var subject = CreateSubject(BuildBothLanguageProfile());
            var remoteBook = BuildRemoteBook(
                "Frank.Herbert.Dune.2021.RETAiL.ePub.eBOOK-GRP",
                profileId: 2,
                bookTitle: "Dune",
                authorName: "Frank Herbert",
                row: BuildLegacyRow("dune"));

            var decision = subject.IsSatisfiedBy(remoteBook, new BookSearchCriteria { Language = "deu" });

            Assert.That(decision.Accepted, Is.True);

            AllowUnknown = false;
            subject = CreateSubject(BuildBothLanguageProfile());
            decision = subject.IsSatisfiedBy(remoteBook, new BookSearchCriteria { Language = "deu" });

            Assert.That(decision.Accepted, Is.False);
        }

        [Test]
        public void ReleaseLanguageSpecificationVariant_LegacyRowUnknownReleasePasses()
        {
            var profile = BuildBothLanguageProfile();

            var subject = CreateSubject(profile);
            var remoteBook = BuildRemoteBook(
                "Frank.Herbert.Dune.2021.RETAiL.ePub.eBOOK-GRP",
                profileId: 2,
                bookTitle: "Dune",
                authorName: "Frank Herbert",
                row: BuildLegacyRow("dune"));

            var decision = subject.IsSatisfiedBy(remoteBook, new BookSearchCriteria());

            Assert.That(decision.Accepted, Is.True);
        }

        [Test]
        public void ReleaseLanguageSpecificationVariant_MatchingLanguagePasses()
        {
            var profile = BuildBothLanguageProfile();

            var subject = CreateSubject(profile);
            var remoteBook = BuildRemoteBook(
                "Frank.Herbert.Dune.2021.GERMAN.RETAiL.ePub.eBOOK-GRP",
                profileId: 2,
                bookTitle: "Dune",
                authorName: "Frank Herbert",
                row: BuildVariantRow("dune_wanted_ebook_deu_101"));

            var decision = subject.IsSatisfiedBy(remoteBook, new BookSearchCriteria());

            Assert.That(decision.Accepted, Is.True);
        }

        private static RemoteBook BuildRemoteBook(string title, int profileId, string bookTitle = "1984", string authorName = "George Orwell", List<Language> languages = null, Quality quality = null, BookMediaType mediaType = BookMediaType.Ebook, Book row = null)
        {
            var book = row ?? new Book
            {
                Title = bookTitle,
                MediaType = mediaType,
                Editions = new List<Edition>
                {
                    new Edition { Title = bookTitle }
                }
            };

            return new RemoteBook
            {
                Author = new Author
                {
                    Name = authorName,
                    EbookMetadataProfileId = profileId
                },
                Books = new List<Book> { book },
                ParsedBookInfo = new ParsedBookInfo
                {
                    AuthorName = authorName,
                    BookTitle = bookTitle,
                    Quality = new QualityModel(quality ?? Quality.EPUB)
                },
                SearchCriteriaMatch = new TitleMatchResult
                {
                    PrimaryTitle = bookTitle,
                    MatchedVariant = bookTitle,
                    Book = book,
                    IsMatch = true
                },
                Release = new ReleaseInfo
                {
                    Title = title,
                    Author = authorName,
                    Languages = languages ?? new List<Language>()
                }
            };
        }

        private sealed class StubMetadataProfileService : IMetadataProfileService
        {
            private readonly Dictionary<int, MetadataProfile> _profiles;

            public StubMetadataProfileService(params MetadataProfile[] profiles)
            {
                _profiles = new Dictionary<int, MetadataProfile>();

                foreach (var profile in profiles ?? Array.Empty<MetadataProfile>())
                {
                    _profiles[profile.Id] = profile;
                }
            }

            public MetadataProfile Add(MetadataProfile profile) => throw new NotImplementedException();
            public void Update(MetadataProfile profile) => throw new NotImplementedException();
            public void Delete(int id) => throw new NotImplementedException();
            public List<MetadataProfile> All() => new List<MetadataProfile>(_profiles.Values);
            public MetadataProfile Get(int id) => _profiles.TryGetValue(id, out var profile) ? profile : null;
            public bool Exists(int id) => _profiles.ContainsKey(id);
            public List<Book> FilterBooks(Author input, int profileId) => throw new NotImplementedException();
        }
    }
}
