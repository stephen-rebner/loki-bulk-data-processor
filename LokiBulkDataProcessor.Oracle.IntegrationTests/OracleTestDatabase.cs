using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using LokiBulkDataProcessor.IntegrationTests.Contract;
using LokiBulkDataProcessor.IntegrationTests.Contract.TestModels;
using Oracle.ManagedDataAccess.Client;
using Testcontainers.Oracle;

namespace LokiBulkDataProcessor.Oracle.IntegrationTests;

/// <summary>
/// One Oracle Database Free container for one test fixture. <see cref="StartAsync"/> creates the schema once
/// (<see cref="OracleSchema.CreateStatements"/>); <see cref="ResetAsync"/> empties every table before each test. All SQL is constant
/// and every value is a bind variable.
/// </summary>
public sealed class OracleTestDatabase : IAsyncDisposable
{
    /// <summary>
    /// Pinned by tag and digest (the multi-arch index digest): the build that <c>gvenzl/oracle-free:23-slim-faststart</c> pointed to when
    /// step 7 was written (Oracle AI Database 26ai Free, 23.26.3); the floating tag moves with every release, and the digest makes a
    /// re-pushed tag fail instead of silently changing the database under the tests. The CI <c>oracle-integration</c> job reads this
    /// line to pull the same image, so keep it a single-line string literal.
    /// </summary>
    public const string Image = "gvenzl/oracle-free:23.26.3-slim-faststart@sha256:f5ff19033860d662c821cb04eb10483fa94f14f78eae252d054291ea07028093";

    public const string Username = "loki_test";

    private readonly OracleContainer _container;

    public OracleTestDatabase()
    {
        Password = TestPasswords.NewPassword();
        _container = new OracleBuilder(Image).WithUsername(Username).WithPassword(Password).Build();
    }

    /// <summary>The application user's (and the container's SYS) password: random for each fixture's database.</summary>
    public string Password { get; }

    private string _connectionString;

    /// <summary>The application user's connection string; available after <see cref="StartAsync"/>.</summary>
    public string ConnectionString => _connectionString ?? throw new InvalidOperationException("The container has not been started.");

    /// <summary>The schema the test user owns (Oracle folds the unquoted user name to upper case).</summary>
    public static string SchemaName => Username.ToUpperInvariant();

    public async Task StartAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
        await ExecuteAsync(OracleSchema.CreateStatements);
    }

    /// <summary>
    /// Clears the connection pools first: a pooled connection left with an open transaction (a failed test) would otherwise
    /// block the TRUNCATEs with ORA-00054.
    /// </summary>
    public Task ResetAsync()
    {
        OracleConnection.ClearAllPools();
        return ExecuteAsync(OracleSchema.ResetStatements);
    }

    /// <summary>A new open connection; the caller disposes it.</summary>
    public async Task<OracleConnection> OpenConnectionAsync()
    {
        var connection = new OracleConnection(ConnectionString);
        try
        {
            await connection.OpenAsync();
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>Inserts a blog and returns its generated Id, inside <paramref name="transaction"/> or committed on its own connection.</summary>
    public async Task<int> InsertBlogAsync(string url, OracleTransaction transaction = null)
    {
        if (transaction is not null)
        {
            return await InsertBlogAsync(transaction.Connection, url);
        }

        await using OracleConnection connection = await OpenConnectionAsync();
        return await InsertBlogAsync(connection, url);
    }

    /// <summary>Runs a constant statement on <paramref name="transaction"/>'s connection (ODP.NET joins its transaction).</summary>
    public static async Task ExecuteAsync(OracleTransaction transaction, string sql, params OracleParameter[] parameters)
    {
        await using OracleCommand command = CreateCommand(transaction.Connection, sql, parameters);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Runs a constant statement on a connection of its own (autocommit).</summary>
    public async Task ExecuteAsync(string sql, params OracleParameter[] parameters)
    {
        await using OracleConnection connection = await OpenConnectionAsync();
        await using OracleCommand command = CreateCommand(connection, sql, parameters);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Runs a constant query on a connection of its own and maps every row.</summary>
    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<DbDataReader, T> map, params OracleParameter[] parameters)
    {
        await using OracleConnection connection = await OpenConnectionAsync();
        return await QueryAsync(connection, sql, map, parameters);
    }

    /// <summary>Runs a constant query on <paramref name="connection"/> (inside its active transaction, if any).</summary>
    public static async Task<IReadOnlyList<T>> QueryAsync<T>(OracleConnection connection, string sql, Func<DbDataReader, T> map,
        params OracleParameter[] parameters)
    {
        await using OracleCommand command = CreateCommand(connection, sql, parameters);
        await using DbDataReader reader = await command.ExecuteReaderAsync();
        var rows = new List<T>();
        while (await reader.ReadAsync())
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    public async Task<int> CountBlogsAsync() =>
        (await QueryAsync(OracleSchema.CountBlogs, reader => Convert.ToInt32(reader.GetValue(0))))[0];

    public Task<IReadOnlyList<PostRow>> ReadPostsAsync() =>
        QueryAsync(OracleSchema.SelectPosts, reader => new PostRow(
            Convert.ToInt32(reader.GetValue(0)), Convert.ToInt32(reader.GetValue(1)), NullableString(reader, 2), NullableString(reader, 3)));

    /// <summary>TestDbModels with NUMBER(1) mapped back to bool.</summary>
    public Task<IReadOnlyList<TestDbModel>> ReadTestDbModelsAsync() =>
        QueryAsync(OracleSchema.SelectTestDbModels, reader => new TestDbModel
        {
            StringColumn = NullableString(reader, 0),
            BoolColumn = Convert.ToInt32(reader.GetValue(1)) != 0,
            DateColumn = reader.GetDateTime(2),
            NullableBoolColumn = reader.IsDBNull(3) ? null : Convert.ToInt32(reader.GetValue(3)) != 0,
            NullableDateColumn = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
        });

    public static OracleParameter Bind(string name, object value) => new(name, value ?? DBNull.Value);

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    private static string NullableString(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static async Task<int> InsertBlogAsync(OracleConnection connection, string url)
    {
        var id = new OracleParameter("id", OracleDbType.Int32, ParameterDirection.Output);
        await using OracleCommand command = CreateCommand(connection, OracleSchema.InsertBlog, Bind("url", url), id);
        await command.ExecuteNonQueryAsync();
        return Convert.ToInt32(id.Value.ToString());
    }

    private async Task ExecuteAsync(IEnumerable<string> statements)
    {
        await using OracleConnection connection = await OpenConnectionAsync();
        foreach (string statement in statements)
        {
            await using OracleCommand command = CreateCommand(connection, statement);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static OracleCommand CreateCommand(OracleConnection connection, string sql, params OracleParameter[] parameters)
    {
        OracleCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.BindByName = true;
        command.Parameters.AddRange(parameters);
        return command;
    }
}
