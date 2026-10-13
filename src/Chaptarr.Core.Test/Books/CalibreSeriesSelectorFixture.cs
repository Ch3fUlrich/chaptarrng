using System.Collections.Generic;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Books.Calibre;

namespace Chaptarr.Core.Test.Books
{
    [TestFixture]
    public class CalibreSeriesSelectorFixture
    {
        [Test]
        public void preferred_series_should_override_calibre_fallback_order()
        {
            var book = new Book
            {
                Title = "The Book",
                SeriesLinks = new List<SeriesBookLink>
                {
                    new SeriesBookLink
                    {
                        SeriesId = 1,
                        SeriesPosition = 1,
                        IsPrimary = true,
                        Series = new Series { Id = 1, Title = "Alpha Series", WorkCount = 100 }
                    },
                    new SeriesBookLink
                    {
                        SeriesId = 2,
                        SeriesPosition = 2,
                        IsPrimary = false,
                        Series = new Series { Id = 2, Title = "Série préférée", WorkCount = 1 }
                    }
                }
            };

            var selected = CalibreSeriesSelector.Select(book, "Série préférée");

            Assert.That(selected.Series.Value.Title, Is.EqualTo("Série préférée"));
        }
    }
}
