using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(123)]
    public class add_missing_search_attempt : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (!Schema.Table("MissingSearchAttempt").Exists())
            {
                Create.Table("MissingSearchAttempt")
                    .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                    .WithColumn("BookId").AsInt32().NotNullable()
                    .WithColumn("Attempts").AsInt32().NotNullable()
                    .WithColumn("LastAttemptAt").AsDateTime().NotNullable();
            }

            if (!Schema.Table("MissingSearchAttempt").Index("IX_MissingSearchAttempt_BookId").Exists())
            {
                Create.Index("IX_MissingSearchAttempt_BookId")
                    .OnTable("MissingSearchAttempt")
                    .OnColumn("BookId").Ascending()
                    .WithOptions().Unique();
            }
        }
    }
}
