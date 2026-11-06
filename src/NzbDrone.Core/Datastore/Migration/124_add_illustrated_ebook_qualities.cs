using System;
using System.Text.Json;
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
                // Weight is supplied by the quality model, not stored in QualityDefinitions.
                foreach (var definition in new[] { new { Quality = 5, Title = "CBZ" }, new { Quality = 6, Title = "CBR" }, new { Quality = 7, Title = "CB7" } })
                {
                    if (connection.ExecuteScalar<int>(@"SELECT COUNT(*) FROM ""QualityDefinitions"" WHERE ""Quality"" = @Quality AND (""Title"" IS NULL OR ""Title"" <> @Title)", definition, transaction) != 0)
                    {
                        throw new InvalidOperationException($"Quality id {definition.Quality} conflicts with migration 124.");
                    }

                    connection.Execute(@"INSERT INTO ""QualityDefinitions"" (""Quality"", ""Title"", ""MinSize"", ""MaxSize"")
                        SELECT @Quality, @Title, 0, NULL WHERE NOT EXISTS
                        (SELECT 1 FROM ""QualityDefinitions"" WHERE ""Quality"" = @Quality)",
                        definition, transaction);
                }

                foreach (var row in connection.Query<ProfileRow>(@"SELECT ""Id"", ""Items"" FROM ""QualityProfiles"" WHERE ""ProfileType"" = 2", transaction: transaction))
                {
                    JsonArray items;
                    try
                    {
                        items = row.Items == null ? null : JsonNode.Parse(row.Items) as JsonArray;
                    }
                    catch (JsonException)
                    {
                        continue;
                    }

                    if (items == null)
                    {
                        continue;
                    }

                    var changed = false;
                    for (var id = 5; id <= 7; id++)
                    {
                        if (!ContainsQuality(items, id))
                        {
                            items.Add(new JsonObject { ["quality"] = id, ["allowed"] = false });
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        connection.Execute(@"UPDATE ""QualityProfiles"" SET ""Items"" = @Items WHERE ""Id"" = @Id", new { row.Id, Items = items.ToJsonString() }, transaction);
                    }
                }
            });
        }

        private static bool ContainsQuality(JsonArray items, int id)
        {
            foreach (var item in items)
            {
                if (item is JsonObject obj && ((obj["quality"] is JsonValue value && value.TryGetValue<int>(out var quality) && quality == id) || (obj["items"] is JsonArray children && ContainsQuality(children, id))))
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
