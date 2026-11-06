using System;
using System.IO;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Data.Sqlite;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace Chaptarr.Core.Test.Datastore
{
    [TestFixture]
    public class IllustratedQualityMigrationFixture
    {
        [TestCase(6, "Other", 14)]
        [TestCase(7, "CB7", 99)]
        [TestCase(5, "Other", 13)]
        public void conflicting_reserved_quality_rolls_back(int quality, string title, int weight)
        {
            var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"illustrated_conflict_{Guid.NewGuid():N}.db");
            var cs = $"Data Source={path};Pooling=False";
            try
            {
                using var db = new SqliteConnection(cs);
                db.Open();
                db.Execute(@"CREATE TABLE VersionInfo (Version INTEGER PRIMARY KEY, AppliedOn TEXT, Description TEXT);
                    WITH RECURSIVE v(n) AS (SELECT 1 UNION ALL SELECT n+1 FROM v WHERE n<123)
                    INSERT INTO VersionInfo SELECT n, CURRENT_TIMESTAMP, 'baseline' FROM v;
                    CREATE TABLE Authors (Id INTEGER PRIMARY KEY, AudiobookRootFolderPath TEXT, EbookRootFolderPath TEXT);
                    CREATE TABLE QualityDefinitions (Quality INTEGER, Title TEXT, Weight INTEGER, MinSize REAL, MaxSize REAL);
                    CREATE TABLE QualityProfiles (Id INTEGER PRIMARY KEY, ProfileType INTEGER, Items TEXT, Cutoff INTEGER);
                    INSERT INTO QualityProfiles VALUES (1,2,'[]',3);");
                db.Execute("INSERT INTO QualityDefinitions VALUES (@quality,@title,@weight,12,34)", new { quality, title, weight });
                var error = Assert.Catch<Exception>(() =>
                    new MigrationController(LogManager.GetLogger(nameof(IllustratedQualityMigrationFixture)), null)
                        .Migrate(cs, new MigrationContext(MigrationType.Main, 124), DatabaseType.SQLite));
                Assert.That(error.ToString(), Does.Contain($"Quality id {quality} conflicts"));
                Assert.That(db.QuerySingle<int>("SELECT COUNT(*) FROM QualityDefinitions"), Is.EqualTo(1));
                Assert.That(db.QuerySingle<string>("SELECT Title FROM QualityDefinitions"), Is.EqualTo(title));
                Assert.That(db.QuerySingle<int>("SELECT Weight FROM QualityDefinitions"), Is.EqualTo(weight));
                Assert.That(db.QuerySingle<string>("SELECT Items FROM QualityProfiles"), Is.EqualTo("[]"));
                Assert.That(db.QuerySingle<int>("SELECT COUNT(*) FROM VersionInfo WHERE Version=124"), Is.Zero);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void preserves_grouped_custom_and_malformed_profiles()
        {
            var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"illustrated_{Guid.NewGuid():N}.db");
            var cs = $"Data Source={path};Pooling=False";
            try
            {
                using (var db = new SqliteConnection(cs))
                {
                    db.Open();
                    db.Execute(@"CREATE TABLE VersionInfo (Version INTEGER PRIMARY KEY, AppliedOn TEXT, Description TEXT);
                        WITH RECURSIVE v(n) AS (SELECT 1 UNION ALL SELECT n+1 FROM v WHERE n<123)
                        INSERT INTO VersionInfo SELECT n, CURRENT_TIMESTAMP, 'baseline' FROM v;
                        CREATE TABLE Authors (Id INTEGER PRIMARY KEY, AudiobookRootFolderPath TEXT, EbookRootFolderPath TEXT);
                        CREATE TABLE QualityDefinitions (Quality INTEGER, Title TEXT, Weight INTEGER, MinSize REAL, MaxSize REAL);
                        CREATE TABLE QualityProfiles (Id INTEGER PRIMARY KEY, ProfileType INTEGER, Items TEXT, Cutoff INTEGER);
                        INSERT INTO QualityProfiles VALUES (1,2,'[{""name"":""custom"",""items"":[{""quality"":5,""allowed"":true}]},{""quality"":""bad""},null,3]',3);
                        INSERT INTO QualityProfiles VALUES (2,2,'invalid',4);
                        INSERT INTO QualityProfiles VALUES (3,2,NULL,4);
                        INSERT INTO QualityProfiles VALUES (4,1,'[]',10);
                        INSERT INTO QualityProfiles VALUES (5,2,'[ {""quality"":5}, {""quality"":6}, {""quality"":7} ]',3);");
                }

                new MigrationController(LogManager.GetLogger(nameof(IllustratedQualityMigrationFixture)), null)
                    .Migrate(cs, new MigrationContext(MigrationType.Main, 124), DatabaseType.SQLite);
                using var result = new SqliteConnection(cs);
                result.Open();
                var items = JsonNode.Parse(result.QuerySingle<string>("SELECT Items FROM QualityProfiles WHERE Id=1")).AsArray();
                Assert.That(items[0]["name"].GetValue<string>(), Is.EqualTo("custom"));
                Assert.That(items[0]["items"][0]["allowed"].GetValue<bool>(), Is.True);
                Assert.That(items[4]["quality"].GetValue<int>(), Is.EqualTo(6));
                Assert.That(items[4]["allowed"].GetValue<bool>(), Is.False);
                Assert.That(items[5]["allowed"].GetValue<bool>(), Is.False);
                Assert.That(result.QuerySingle<int>("SELECT Cutoff FROM QualityProfiles WHERE Id=1"), Is.EqualTo(3));
                Assert.That(result.QuerySingle<string>("SELECT Items FROM QualityProfiles WHERE Id=2"), Is.EqualTo("invalid"));
                Assert.That(result.QuerySingle<string>("SELECT Items FROM QualityProfiles WHERE Id=3"), Is.Null);
                Assert.That(result.QuerySingle<string>("SELECT Items FROM QualityProfiles WHERE Id=4"), Is.EqualTo("[]"));
                Assert.That(result.QuerySingle<string>("SELECT Items FROM QualityProfiles WHERE Id=5"), Is.EqualTo("[ {\"quality\":5}, {\"quality\":6}, {\"quality\":7} ]"));
                Assert.That(result.QuerySingle<int>("SELECT COUNT(*) FROM QualityDefinitions WHERE Quality BETWEEN 5 AND 7 AND Weight=Quality+8"), Is.EqualTo(3));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
