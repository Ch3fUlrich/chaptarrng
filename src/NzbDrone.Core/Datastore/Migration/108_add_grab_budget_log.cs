using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(108)]
    public class add_grab_budget_log : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
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
    }
}
