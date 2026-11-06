using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Chaptarr.Api.V1.BookFiles;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser;
using NzbDrone.Core.RootFolders;

namespace Chaptarr.Core.Test.Api
{
    [TestFixture]
    public class IllustratedListingFixture
    {
        [TestCase("manga", null, "manga")]
        [TestCase("comics", null, "comics")]
        [TestCase("magazines", null, "magazines")]
        [TestCase("MaNgA", null, "manga")]
        [TestCase("CoMiCs", null, "comics")]
        [TestCase("MaGaZiNeS", null, "magazines")]
        [TestCase("library", "MaNgA", "manga")]
        [TestCase("library", "CoMiCs", "comics")]
        [TestCase("library", "MaGaZiNeS", "magazines")]
        [TestCase("library", null, null)]
        [TestCase("manga-extra", "comics-extra", null)]
        public void inventory_classifies_from_root_leaf_or_subdirectory_otherwise_filename(string leaf, string subdirectory, string directoryType)
        {
            // An ancestor type must not influence an unrelated registered root.
            var temporary = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var root = Path.Combine(temporary, "manga", leaf);
            var directory = subdirectory == null ? root : Path.Combine(root, subdirectory);
            Directory.CreateDirectory(directory);
            try
            {
                var names = new[] { "Quorven Issue 1.cbr", "Quorven Issue 2.cbz", "Quorven Issue 3.pdf", "Quorven v01-03" };
                foreach (var name in names) File.WriteAllText(Path.Combine(directory, name), "fixture");
                var roots = new Mock<IRootFolderService>();
                roots.Setup(x => x.All()).Returns(new List<RootFolder> { new RootFolder { Path = root } });
                var controller = new BookFileController(null, null, null, null, null, null, null, roots.Object, LogManager.GetCurrentClassLogger());
                foreach (var type in new[] { "comics", "manga", "magazines" })
                {
                    var expected = directoryType == null
                        ? (type == "comics" ? names.Take(2) : type == "magazines" ? names.Skip(2).Take(1) : names.Skip(3))
                        : directoryType == type ? names : Array.Empty<string>();
                    var result = controller.GetIllustrated(root + Path.DirectorySeparatorChar, type);
                    Assert.That(result.Entries.Select(x => x.Name), Is.EquivalentTo(expected), type);
                    Assert.That(result.Entries.All(x => x.Type == type), Is.True);
                }

                if (directoryType == "magazines")
                {
                    var result = controller.GetIllustrated(root, "magazines", expectedIssues: new[] { "Quorven|1", "Quorven|2", "Quorven|3", "Quorven|4" });
                    Assert.That(result.Entries.Select(x => x.Name), Is.EquivalentTo(names));
                    Assert.That(result.MissingIssues.Select(x => x.Issue), Is.EqualTo(new[] { "4" }));
                }
            }
            finally { Directory.Delete(temporary, true); }
        }

        [Test]
        public async System.Threading.Tasks.Task invalid_requests_are_mapped_to_http_400()
        {
            var roots = new Mock<IRootFolderService>();
            roots.Setup(x => x.All()).Returns(new List<RootFolder>());
            var controller = new BookFileController(null, null, null, null, null, null, null, roots.Object, LogManager.GetCurrentClassLogger());
            foreach (Action request in new Action[]
            {
                () => controller.GetBookFiles(1, new List<int>(), new List<int>(), false, rootFolderPath: "/invented/root"),
                () => controller.GetBookFiles(null, new List<int>(), new List<int>(), null),
                () => controller.GetIllustrated("/invented/root", "comics")
            })
            {
                var exception = Assert.Catch(request.Invoke);
                var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
                context.Response.Body = new MemoryStream();
                context.Features.Set<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>(new Microsoft.AspNetCore.Diagnostics.ExceptionHandlerFeature { Error = exception });
                await new Chaptarr.Http.ErrorManagement.ChaptarrErrorPipeline(LogManager.GetCurrentClassLogger()).HandleException(context);
                Assert.That(context.Response.StatusCode, Is.EqualTo(400));
            }
        }

        [Test]
        public void inventory_orders_before_limiting_and_does_not_infer_gaps_from_partial_walks()
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllText(Path.Combine(root, "Zorvik Issue 2.pdf"), "fixture");
                File.WriteAllText(Path.Combine(root, "Aelvik Issue 1.pdf"), "fixture");
                var roots = new Mock<IRootFolderService>();
                roots.Setup(x => x.All()).Returns(new List<RootFolder> { new RootFolder { Path = "bad\0root" }, new RootFolder { Path = root } });
                var controller = new BookFileController(null, null, null, null, null, null, null, roots.Object, LogManager.GetCurrentClassLogger());
                var result = controller.GetIllustrated(root, "magazines", 1, expectedIssues: new[] { "Zorvik|2" });
                Assert.That(result.Entries.Single().Name, Is.EqualTo("Aelvik Issue 1.pdf"));
                Assert.That(result.MissingIssues, Is.Empty);
                for (var i = 0; i < 10010; i++) File.WriteAllText(Path.Combine(root, $"noise{i}.txt"), "fixture");
                Assert.That(controller.GetIllustrated(root, "magazines", expectedIssues: new[] { "Absentvik|3" }).MissingIssues, Is.Empty);
            }
            finally { Directory.Delete(root, true); }
        }

        [TestCase("Series.v01-05")]
        [TestCase("Zelvik.Quorvex.vol.01-20")]
        public void dotted_volume_ranges_are_bundles(string name)
        {
            Assert.That(IllustratedLibraryListing.Classify(name), Is.EqualTo("manga"));
        }

        [TestCase("README")]
        [TestCase("metadata")]
        public void dotless_non_media_is_not_a_bundle(string name)
        {
            Assert.That(IllustratedLibraryListing.Classify(name), Is.Not.EqualTo("manga"));
            Assert.That(IllustratedLibraryListing.List(name, "comics").Limitation, Does.StartWith("Unrecognized file:"));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void blank_root_is_no_filter(string root)
        {
            var media = new Mock<IMediaFileService>();
            media.Setup(x => x.Get(It.IsAny<List<int>>())).Returns(new List<BookFile>());
            var controller = new BookFileController(null, media.Object, null, null, null, null, null, null, LogManager.GetCurrentClassLogger());
            Assert.That(controller.GetBookFiles(null, new List<int>(), new List<int>(), false, rootFolderPath: root), Is.Empty);
        }
        [Test]
        public void illustrated_and_other_bookfile_actions_use_host_fallback_authentication()
        {
            // Startup requires authenticated API users unless endpoint metadata opts out.
            var controller = typeof(BookFileController);
            Assert.That(controller.GetCustomAttributes(true).OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>(), Is.Empty);
            foreach (var name in new[] { "GetIllustrated", "GetBookFiles" })
            {
                var action = controller.GetMethod(name);
                Assert.That(action.GetCustomAttributes(true).OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>(), Is.Empty);
                Assert.That(action.GetCustomAttributes(true).OfType<Microsoft.AspNetCore.Mvc.NonActionAttribute>(), Is.Empty);
            }
        }

        [Test]
        public void unreadable_directory_leaves_a_partial_listing()
        {
            if (OperatingSystem.IsWindows()) Assert.Ignore("Unix permission regression");
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var denied = Path.Combine(root, "denied");
            Directory.CreateDirectory(denied);
            File.WriteAllText(Path.Combine(root, "Visible.cbz"), "fixture");
            File.WriteAllText(Path.Combine(denied, "Hidden.cbz"), "fixture");
            try
            {
                File.SetUnixFileMode(denied, UnixFileMode.None);
                try
                {
                    Directory.GetFileSystemEntries(denied);
                    Assert.Ignore("Process can enumerate permission-denied directories (root or privileged)");
                }
                catch (UnauthorizedAccessException) { }
                var roots = new Mock<IRootFolderService>();
                roots.Setup(x => x.All()).Returns(new List<RootFolder> { new RootFolder { Path = root } });
                var controller = new BookFileController(null, null, null, null, null, null, null, roots.Object, LogManager.GetCurrentClassLogger());
                Assert.That(controller.GetIllustrated(root, "comics").Entries.Select(x => x.Name), Is.EqualTo(new[] { "Visible.cbz" }));
            }
            finally
            {
                File.SetUnixFileMode(denied, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                Directory.Delete(root, true);
            }
        }

        [TestCase(null)]
        [TestCase(false)]
        public void root_filter_requires_unmapped_surface(bool? unmapped)
        {
            var controller = new BookFileController(null, null, null, null, null, null, null, null, LogManager.GetCurrentClassLogger());
            Assert.Throws<Chaptarr.Http.REST.BadRequestException>(() => controller.GetBookFiles(1, new List<int>(), new List<int>(), unmapped, rootFolderPath: "/library/comics"));
        }

        [Test]
        public void root_filter_excludes_siblings_and_prefix_collisions()
        {
            var media = new Mock<IMediaFileService>();
            media.Setup(x => x.GetUnmappedFiles()).Returns(new List<BookFile>
            {
                new BookFile { Id = 1, Path = "/library/comics/a.cbz" },
                new BookFile { Id = 2, Path = "/library/comics-extra/b.cbz" },
                new BookFile { Id = 3, Path = "/library/manga/c.cbz" }
            });
            var roots = new Mock<IRootFolderService>();
            roots.Setup(x => x.All()).Returns(new List<RootFolder> { new RootFolder { Path = "/library/comics" } });
            var controller = new BookFileController(null, media.Object, null, null, null, null, null, roots.Object, LogManager.GetCurrentClassLogger());
            Assert.That(controller.GetBookFiles(null, new List<int>(), new List<int>(), true, rootFolderPath: "/library/comics").Select(x => x.Id), Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void inventory_includes_unparsed_archive_and_extensionless_bundles_and_production_parity()
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(root);
            try
            {
                var names = new[] { "Zindle Lantern Archive 001 [1993] Jasper Finch - The Brightest Kid On Mars.cbz", "Other.Group_Starcatcher Petal Clear Moon Arc v01-03", "Mooncat v01-05", "Chronicle Atlas - Issue 163.PDF", "Chronicle_Atlas_-_Issue_164.pdf", "Orbital Survey-Issue.33.2014.pdf", "Orbital.Survey-Issue.98.2019.pdf" };
                foreach (var name in names) File.WriteAllText(Path.Combine(root, name), "fixture");
                var roots = new Mock<IRootFolderService>();
                roots.Setup(x => x.All()).Returns(new List<RootFolder> { new RootFolder { Path = root } });
                var controller = new BookFileController(null, null, null, null, null, null, null, roots.Object, LogManager.GetCurrentClassLogger());
                var response = controller.GetIllustrated(root, "magazines", expectedIssues: new[] { "Chronicle Atlas|163", "Chronicle Atlas|164", "Orbital Survey|33", "Orbital Survey|98" });
                var entries = response.Entries;
                Assert.That(response.MissingIssues, Is.Empty);
                File.Delete(Path.Combine(root, names[4]));
                File.Delete(Path.Combine(root, names[6]));
                Assert.That(controller.GetIllustrated(root, "magazines", expectedIssues: new[] { "Chronicle Atlas|164", "Orbital Survey|98" }).MissingIssues.Select(x => x.Series), Is.EquivalentTo(new[] { "Chronicle Atlas", "Orbital Survey" }));
                File.WriteAllText(Path.Combine(root, names[4]), "fixture");
                File.WriteAllText(Path.Combine(root, names[6]), "fixture");
                Assert.That(entries.Select(x => x.Name), Is.EquivalentTo(names.Skip(3)));
                Assert.That(entries.Single(x => x.Name == names[3]).Series, Is.EqualTo("Chronicle Atlas"));
                foreach (var series in new[] { "Chronicle Atlas", "Orbital Survey" })
                {
                    var expected = series == "Chronicle Atlas" ? new[] { (series, "163"), (series, "164") } : new[] { (series, "33"), (series, "98") };
                    var owned = entries.Where(x => x.Series == series).Select(x => (x.Series, x.Issue)).ToArray();
                    Assert.That(IllustratedLibraryListing.MissingIssues(expected, owned), Is.Empty);
                    Assert.That(IllustratedLibraryListing.MissingIssues(expected, owned.Take(1)), Has.Count.EqualTo(1));
                }
                var bundles = controller.GetIllustrated(root, "manga").Entries;
                Assert.That(bundles.Select(x => x.Name), Is.EquivalentTo(names.Skip(1).Take(2)));
                Assert.That(bundles.All(x => x.Limitation.StartsWith("Extensionless bundle:")), Is.True);
                foreach (var name in new[] { "Zelvik.Quorvex.vol.01-20", "Series.v01-05", "README", "metadata" }) File.WriteAllText(Path.Combine(root, name), "fixture");
                Assert.That(controller.GetIllustrated(root, "manga").Entries, Has.Count.EqualTo(4));
                Assert.That(controller.GetIllustrated(root, "comics").Entries.Select(x => x.Name), Does.Not.Contain("README"));
                Assert.That(controller.GetIllustrated(root, "comics").Entries.Single().Limitation, Is.Null);
                Assert.Throws<Chaptarr.Http.REST.BadRequestException>(() => controller.GetIllustrated(root + "-other", "manga"));
                Assert.Throws<Chaptarr.Http.REST.BadRequestException>(() => controller.GetIllustrated(Path.Combine(root, ".."), "manga"));
                Assert.Throws<Chaptarr.Http.REST.BadRequestException>(() => controller.GetIllustrated(root, "books"));
                Assert.Throws<Chaptarr.Http.REST.BadRequestException>(() => controller.GetIllustrated("bad\0path", "manga"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void walk_is_nested_bounded_and_skips_hidden_links_and_non_text()
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var outside = root + "-outside";
            var alias = root + "-alias";
            Directory.CreateDirectory(Path.Combine(root, "nested"));
            Directory.CreateDirectory(Path.Combine(root, ".hidden"));
            Directory.CreateDirectory(outside);
            try
            {
                File.WriteAllText(Path.Combine(root, "nested", "Visible.cbz"), "fixture");
                File.WriteAllText(Path.Combine(root, "Visible2.cbz"), "fixture");
                File.WriteAllText(Path.Combine(root, ".hidden", "Hidden.cbz"), "fixture");
                File.WriteAllText(Path.Combine(root, "audio.mp3"), "fixture");
                File.WriteAllText(Path.Combine(outside, "Outside.cbz"), "fixture");
                try
                {
                    Directory.CreateSymbolicLink(Path.Combine(root, "linked"), outside);
                    Directory.CreateSymbolicLink(alias, root);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException || ex is PlatformNotSupportedException)
                {
                    Assert.Ignore("Symlink creation is not permitted: " + ex.GetType().Name);
                }
                var roots = new Mock<IRootFolderService>();
                roots.Setup(x => x.All()).Returns(new List<RootFolder> { new RootFolder { Path = root } });
                var controller = new BookFileController(null, null, null, null, null, null, null, roots.Object, LogManager.GetCurrentClassLogger());
                Assert.That(controller.GetIllustrated(root + Path.DirectorySeparatorChar, "comics").Entries.Select(x => x.Name), Is.EquivalentTo(new[] { "Visible.cbz", "Visible2.cbz" }));
                Assert.That(controller.GetIllustrated(alias, "comics").Entries, Has.Count.EqualTo(2));
                Assert.That(controller.GetIllustrated(root, "comics", 1).Entries, Has.Count.EqualTo(1));
                Directory.CreateSymbolicLink(Path.Combine(root, "loop"), Path.Combine(root, "loop"));
                var media = new Mock<IMediaFileService>();
                media.Setup(x => x.GetUnmappedFiles()).Returns(new List<BookFile>
                {
                    new BookFile { Id = 1, Path = Path.Combine(root, "loop", "unsafe.cbz") },
                    new BookFile { Id = 2, Path = Path.Combine(root, "Visible2.cbz") }
                });
                var filtered = new BookFileController(null, media.Object, null, null, null, null, null, roots.Object, LogManager.GetCurrentClassLogger());
                Assert.That(filtered.GetBookFiles(null, new List<int>(), new List<int>(), true, rootFolderPath: root).Select(x => x.Id), Is.EqualTo(new[] { 2 }));
                Assert.Throws<Chaptarr.Http.REST.BadRequestException>(() => controller.GetIllustrated(root, "comics", 10001));
            }
            finally
            {
                if (Directory.Exists(alias)) Directory.Delete(alias);
                Directory.Delete(root, true);
                Directory.Delete(outside, true);
            }
        }
    }
}
