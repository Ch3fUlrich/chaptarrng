using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(109)]
    public class add_grab_budget_batch : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            if (!Schema.Table("GrabBudgetBatch").Exists())
            {
                Create.Table("GrabBudgetBatch")
                    .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                    .WithColumn("StartedAt").AsDateTime().NotNullable()
                    .WithColumn("Grabbed").AsInt32().NotNullable()
                    .WithColumn("Skipped").AsInt32().NotNullable()
                    .WithColumn("Failed").AsInt32().NotNullable()
                    .WithColumn("StopReason").AsInt32().NotNullable()
                    .WithColumn("Details").AsString().Nullable();
            }

            if (!Schema.Table("GrabBudgetBatch").Index("IX_GrabBudgetBatch_StartedAt").Exists())
            {
                Create.Index("IX_GrabBudgetBatch_StartedAt")
                    .OnTable("GrabBudgetBatch")
                    .OnColumn("StartedAt").Ascending();
            }
        }
    }
}
