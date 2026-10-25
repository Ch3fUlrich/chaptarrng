using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // Databases coming from the pre-NG fork recorded its own 108/109 in VersionInfo, so FluentMigrator treats NG's
    // 109_add_query_performance_indexes as already applied and skips it. Re-applying its (idempotent) index set here
    // repairs those databases and is a no-op on a clean NG database.
    [Migration(121)]
    public class add_grab_budget_log : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            EnsureQueryPerformanceIndexes();

            if (!Schema.Table("GrabBudgetLog").Exists())
            {
                Create.Table("GrabBudgetLog")
                    .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                    .WithColumn("GrabbedAt").AsDateTime().NotNullable();
            }

            if (!Schema.Table("GrabBudgetLog").Index("IX_GrabBudgetLog_GrabbedAt").Exists())
            {
                Create.Index("IX_GrabBudgetLog_GrabbedAt")
                    .OnTable("GrabBudgetLog")
                    .OnColumn("GrabbedAt").Ascending();
            }
        }

        private void EnsureQueryPerformanceIndexes()
        {
            if (Schema.Table("BookFiles").Exists())
            {
                if (!Schema.Table("BookFiles").Index("IX_BookFiles_EditionId_Size").Exists())
                {
                    Create.Index("IX_BookFiles_EditionId_Size")
                        .OnTable("BookFiles")
                        .OnColumn("EditionId").Ascending()
                        .OnColumn("Size").Ascending();
                }

                if (Schema.Table("BookFiles").Index("IX_BookFiles_EditionId").Exists())
                {
                    Delete.Index("IX_BookFiles_EditionId").OnTable("BookFiles");
                }
            }

            if (Schema.Table("Books").Exists())
            {
                if (!Schema.Table("Books").Index("IX_Books_AuthorId_ReleaseDate_Id").Exists())
                {
                    Create.Index("IX_Books_AuthorId_ReleaseDate_Id")
                        .OnTable("Books")
                        .OnColumn("AuthorId").Ascending()
                        .OnColumn("ReleaseDate").Ascending()
                        .OnColumn("Id").Ascending();
                }

                Execute.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Books_MediaType_TitleSort""
                    ON ""Books"" (""MediaType"", (LOWER(COALESCE(""CleanTitle"", ""Title"", ''))), ""Id"", ""CleanTitle"", ""Title"");");
            }

            if (Schema.Table("Editions").Exists())
            {
                if (!Schema.Table("Editions").Index("IX_Editions_Monitored_BookId").Exists())
                {
                    Create.Index("IX_Editions_Monitored_BookId")
                        .OnTable("Editions")
                        .OnColumn("Monitored").Ascending()
                        .OnColumn("BookId").Ascending();
                }

                if (Schema.Table("Editions").Index("IX_Editions_Monitored").Exists())
                {
                    Delete.Index("IX_Editions_Monitored").OnTable("Editions");
                }
            }
        }
    }
}
