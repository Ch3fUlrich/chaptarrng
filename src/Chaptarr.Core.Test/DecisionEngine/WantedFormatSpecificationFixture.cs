using System;
using System.Collections.Generic;
using System.Reflection;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace Chaptarr.Core.Test.DecisionEngine
{
    [TestFixture]
    public class WantedFormatSpecificationFixture
    {
        public class WantedProxy : DispatchProxy
        {
            public Dictionary<BookMediaType, List<string>> Formats { get; set; } = new();

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod.Name == nameof(IWantedVersionService.GetWantedFormats))
                {
                    return Formats.TryGetValue((BookMediaType)args[0], out var l) ? l : new List<string>();
                }

                throw new NotImplementedException(targetMethod.Name);
            }
        }

        private static bool Accepts(Quality quality, Dictionary<BookMediaType, List<string>> formats)
        {
            var proxy = DispatchProxy.Create<IWantedVersionService, WantedProxy>();
            ((WantedProxy)(object)proxy).Formats = formats;
            var spec = new WantedFormatSpecification(proxy, LogManager.GetCurrentClassLogger());
            var remote = new RemoteBook
            {
                Release = new ReleaseInfo { Title = "x" },
                ParsedBookInfo = new ParsedBookInfo { Quality = new QualityModel(quality) }
            };
            return spec.IsSatisfiedBy(remote, null).Accepted;
        }

        [Test]
        public void Empty_list_leaves_medium_unrestricted()
        {
            Assert.That(Accepts(Quality.MOBI, new()), Is.True);
            Assert.That(Accepts(Quality.MP3, new()), Is.True);
        }

        [Test]
        public void Ebook_list_rejects_other_ebook_formats_only()
        {
            var formats = new Dictionary<BookMediaType, List<string>> { [BookMediaType.Ebook] = new() { "epub" } };
            Assert.That(Accepts(Quality.EPUB, formats), Is.True);
            Assert.That(Accepts(Quality.MOBI, formats), Is.False);
            Assert.That(Accepts(Quality.MP3, formats), Is.True);
        }

        [Test]
        public void Audiobook_list_rejects_other_audio_formats_only()
        {
            var formats = new Dictionary<BookMediaType, List<string>> { [BookMediaType.Audiobook] = new() { "m4b", "mp3" } };
            Assert.That(Accepts(Quality.M4B, formats), Is.True);
            Assert.That(Accepts(Quality.FLAC, formats), Is.False);
            Assert.That(Accepts(Quality.PDF, formats), Is.True);
        }
    }
}
