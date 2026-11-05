using System.Text.Json.Nodes;
using Dapper;
using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(124)]
    public class add_illustrated_ebook_qualities : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Execute.WithConnection((connection, transaction) =>
            {
                foreach (var definition in NzbDrone.Core.Qualities.Quality.DefaultQualityDefinitions)
                {
                    if (definition.Quality.Id < 5 || definition.Quality.Id > 7)
                    {
                        continue;
                    }

                    connection.Execute(@"INSERT INTO ""QualityDefinitions"" (""Quality"", ""Title"", ""Weight"", ""MinSize"", ""MaxSize"")
                        SELECT @Quality, @Title, @Weight, 0, NULL WHERE NOT EXISTS
                        (SELECT 1 FROM ""QualityDefinitions"" WHERE ""Quality"" = @Quality)",
                        new { Quality = definition.Quality.Id, Title = definition.Quality.Name, definition.Weight }, transaction);
                }

                foreach (var row in connection.Query<ProfileRow>(@"SELECT ""Id"", ""Items"" FROM ""QualityProfiles"" WHERE ""ProfileType"" = 2", transaction: transaction))
                {
                    var items = JsonNode.Parse(row.Items).AsArray();
                    for (var id = 5; id <= 7; id++)
                    {
                        if (!ContainsQuality(items, id))
                        {
                            items.Add(new JsonObject { ["quality"] = id, ["allowed"] = true });
                        }
                    }

                    connection.Execute(@"UPDATE ""QualityProfiles"" SET ""Items"" = @Items WHERE ""Id"" = @Id", new { row.Id, Items = items.ToJsonString() }, transaction);
                }
            });
        }

        private static bool ContainsQuality(JsonArray items, int id)
        {
            foreach (var item in items)
            {
                if (item?["quality"]?.GetValue<int>() == id || (item?["items"] is JsonArray children && ContainsQuality(children, id)))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class ProfileRow
        {
            public int Id { get; set; }
            public string Items { get; set; }
        }
    }
}
