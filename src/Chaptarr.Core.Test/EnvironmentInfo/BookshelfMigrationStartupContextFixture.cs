using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;

namespace Chaptarr.Core.Test.EnvironmentInfo
{
    [TestFixture]
    public class BookshelfMigrationStartupContextFixture
    {
        [TestCase("-migrate-bookshelf", true, false, false, false, false)]
        [TestCase("--preview-bookshelf-migration", false, true, false, false, false)]
        [TestCase("--restore-bookshelf", false, false, true, false, false)]
        [TestCase("--preview-bookshelf-restore", false, false, false, true, false)]
        [TestCase("--confirm-bookshelf-service-stopped", false, false, false, false, true)]
        public void should_parse_a_single_bookshelf_maintenance_flag(
            string argument,
            bool migrate,
            bool previewMigration,
            bool restore,
            bool previewRestore,
            bool confirmServiceStopped)
        {
            var context = new StartupContext(argument);

            Assert.That(context.MigrateBookshelf, Is.EqualTo(migrate));
            Assert.That(context.PreviewBookshelfMigration, Is.EqualTo(previewMigration));
            Assert.That(context.RestoreBookshelf, Is.EqualTo(restore));
            Assert.That(context.PreviewBookshelfRestore, Is.EqualTo(previewRestore));
            Assert.That(context.ConfirmBookshelfServiceStopped, Is.EqualTo(confirmServiceStopped));
        }
    }
}
