using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace MyHttpServer.ORM.UnitTests;

[TestClass]
public sealed class OrmTests
{
    [TestMethod]
    public void RejectsInvalidTableMapping()
    {
        using var database = NpgsqlDataSource.Create("Host=localhost;Username=postgres");
        var repository = new Orm<BrokenMapping>(database);
        var error = Assert.ThrowsExactlyAsync<TypeInitializationException>(
            () => repository.ReadByIdAsync(1)).GetAwaiter().GetResult();
        Assert.IsInstanceOfType<ArgumentException>(error.InnerException);
    }

    [TestMethod]
    [TestCategory("Postgres")]
    public async Task CrudMapsRecordsAndKeepsStringValuesIntact()
    {
        var connection = Environment.GetEnvironmentVariable("ORIS_TEST_DB");
        if (string.IsNullOrWhiteSpace(connection))
        {
            Assert.Inconclusive("Задай ORIS_TEST_DB со строкой подключения к тестовой БД.");
            return;
        }

        string schema = "test_" + Guid.NewGuid().ToString("N");
        await using var admin = NpgsqlDataSource.Create(connection);
        await using (var create = admin.CreateCommand($"CREATE SCHEMA {schema}"))
            await create.ExecuteNonQueryAsync();
        try
        {
            var options = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema };
            await using var db = NpgsqlDataSource.Create(options.ConnectionString);
            await using (var create = db.CreateCommand(
                "CREATE TABLE records (id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY, label text, valid boolean NOT NULL)"))
                await create.ExecuteNonQueryAsync();

            var orm = new Orm<WorkRecord>(db);
            int id = await orm.CreateAsync(new() { Label = "Тест O'Коннор", Valid = true });
            var loaded = await orm.ReadByIdAsync(id);
            Assert.IsNotNull(loaded);
            Assert.AreEqual("Тест O'Коннор", loaded.Label);
            Assert.IsTrue(loaded.Valid);
            Assert.IsTrue(await orm.UpdateAsync(id, new() { Label = null, Valid = false }));

            var updated = (await orm.ReadAllAsync()).Single();
            Assert.AreEqual(id, updated.Id);
            Assert.IsNull(updated.Label);
            Assert.IsFalse(updated.Valid);
            Assert.IsTrue(await orm.DeleteAsync(id));
            Assert.IsFalse(await orm.DeleteAsync(id));
            Assert.IsNull(await orm.ReadByIdAsync(id));
        }
        finally
        {
            await using var cleanup = admin.CreateCommand($"DROP SCHEMA {schema} CASCADE");
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    [Table("invalid table")]
    public sealed class BrokenMapping
    {
        [Column("id", key: true)]
        public int Id { get; set; }
    }

    [Table("records")]
    public sealed class WorkRecord
    {
        [Column("id", key: true)]
        public int Id { get; set; }

        [Column("label")]
        public string? Label { get; set; }

        [Column("valid")]
        public bool Valid { get; set; }
    }
}
