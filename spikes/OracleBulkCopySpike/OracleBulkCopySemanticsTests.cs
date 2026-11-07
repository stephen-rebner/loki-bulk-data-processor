// SPIKE CODE — throwaway. Answers behavioural questions about OracleBulkCopy before the
// Loki provider SPI is designed. Each test records what Oracle actually does; it is not
// a regression suite. Never merged.
using System.Data;
using System.Reflection;
using FastMember;
using Oracle.ManagedDataAccess.Client;
using Testcontainers.Oracle;

namespace OracleBulkCopySpike;

[TestFixture]
[NonParallelizable]
public class OracleBulkCopySemanticsTests
{
    private OracleContainer _container = null!;
    private string _cs = null!;

    [OneTimeSetUp]
    public async Task StartOracle()
    {
        _container = new OracleBuilder()
            .WithImage("gvenzl/oracle-free:23-slim-faststart")
            .Build();
        await _container.StartAsync();
        _cs = _container.GetConnectionString();
        await using var c = await OpenAsync();
        TestContext.Out.WriteLine($"Server version: {c.ServerVersion}");
        TestContext.Out.WriteLine($"Driver: {typeof(OracleConnection).Assembly.GetName().Version}");
    }

    [OneTimeTearDown]
    public async Task StopOracle() => await _container.DisposeAsync();

    [SetUp]
    public async Task ResetSchema()
    {
        await using var c = await OpenAsync();
        foreach (var t in new[] { "CHILD_ROWS", "PARENT_ROWS", "SPIKE_ROWS", "\"MixedCase\"", "TYPE_ROWS", "TRIGGER_LOG", "TRIGGER_ROWS" })
            await TryExec(c, $"DROP TABLE {t} CASCADE CONSTRAINTS PURGE");
        await Exec(c, "CREATE TABLE SPIKE_ROWS (ID NUMBER(10) PRIMARY KEY, NAME VARCHAR2(50), AMOUNT NUMBER(10) CHECK (AMOUNT >= 0))");
        await Exec(c, "CREATE TABLE PARENT_ROWS (ID NUMBER(10) PRIMARY KEY)");
        await Exec(c, "CREATE TABLE CHILD_ROWS (ID NUMBER(10) PRIMARY KEY, PARENT_ID NUMBER(10) REFERENCES PARENT_ROWS(ID))");
        await Exec(c, "CREATE TABLE \"MixedCase\" (\"Id\" NUMBER(10), \"Name\" VARCHAR2(50))");
        await Exec(c, "CREATE TABLE TYPE_ROWS (ID NUMBER(10), FLAG BOOLEAN, UID_COL RAW(16), TXT VARCHAR2(50), WHEN_COL TIMESTAMP)");
        await Exec(c, "CREATE TABLE TRIGGER_LOG (MSG VARCHAR2(50))");
        await Exec(c, "CREATE TABLE TRIGGER_ROWS (ID NUMBER(10) PRIMARY KEY, NAME VARCHAR2(50), AMOUNT NUMBER(10))");
        await Exec(c, "CREATE OR REPLACE TRIGGER TRIGGER_ROWS_TRG AFTER INSERT ON TRIGGER_ROWS FOR EACH ROW BEGIN INSERT INTO TRIGGER_LOG VALUES ('fired'); END;");
    }

    // ---------- Q1: external transaction ----------

    [Test]
    public async Task Q1a_ExternalTransaction_CallerRollsBack_AreRowsGone()
    {
        await using var c = await OpenAsync();
        await using var tx = (OracleTransaction)await c.BeginTransactionAsync();
        var outcome = Try(() => Copy(c, Rows(1, 5), "SPIKE_ROWS"));
        await tx.RollbackAsync();
        Record($"copy={outcome}; rows after caller rollback={await Count("SPIKE_ROWS")}");
    }

    [Test]
    public async Task Q1b_ExternalTransaction_DmlFirstThenBulkCopy()
    {
        await using var c = await OpenAsync();
        await using var tx = (OracleTransaction)await c.BeginTransactionAsync();
        await Exec(c, "INSERT INTO PARENT_ROWS VALUES (1)");
        var outcome = Try(() => Copy(c, Rows(1, 5), "SPIKE_ROWS"));
        await tx.RollbackAsync();
        Record($"copy after DML in same tx={outcome}; SPIKE_ROWS after rollback={await Count("SPIKE_ROWS")}; PARENT_ROWS after rollback={await Count("PARENT_ROWS")}");
    }

    [Test]
    public async Task Q1c_ExternalTransaction_BulkCopyThenDmlThenCommit()
    {
        await using var c = await OpenAsync();
        await using var tx = (OracleTransaction)await c.BeginTransactionAsync();
        var copy = Try(() => Copy(c, Rows(1, 5), "SPIKE_ROWS"));
        var dml = await TryAsync(() => Exec(c, "INSERT INTO PARENT_ROWS VALUES (1)"));
        var commit = await TryAsync(() => tx.CommitAsync());
        Record($"copy={copy}; DML after copy={dml}; commit={commit}; SPIKE_ROWS={await Count("SPIKE_ROWS")}; PARENT_ROWS={await Count("PARENT_ROWS")}");
    }

    [Test]
    public async Task Q1d_UseInternalTransaction_WithOpenCallerTransaction()
    {
        await using var c = await OpenAsync();
        await using var tx = (OracleTransaction)await c.BeginTransactionAsync();
        var outcome = Try(() => Copy(c, Rows(1, 5), "SPIKE_ROWS", OracleBulkCopyOptions.UseInternalTransaction));
        await TryAsync(() => tx.RollbackAsync());
        Record($"UseInternalTransaction with open tx={outcome}; rows={await Count("SPIKE_ROWS")}");
    }

    [Test]
    public async Task Q1e_NoTransaction_IsCopyAutoCommitted()
    {
        await using var c = await OpenAsync();
        var outcome = Try(() => Copy(c, Rows(1, 5), "SPIKE_ROWS"));
        Record($"copy={outcome}; rows visible from another session={await Count("SPIKE_ROWS")}");
    }

    [Test]
    public async Task Q1f_FailureMidCopy_PartialRowsLeft([Values(0, 2)] int batchSize)
    {
        await using var c = await OpenAsync();
        var rows = Rows(1, 6);
        rows[4].Amount = -1; // violates CHECK on the 5th row
        var outcome = Try(() => Copy(c, rows, "SPIKE_ROWS", batchSize: batchSize));
        Record($"batchSize={batchSize}; copy={outcome}; rows left={await Count("SPIKE_ROWS")}");
    }

    [Test]
    public async Task Q1g_FailureMidCopy_InsideCallerTransaction_ThenRollback()
    {
        await using var c = await OpenAsync();
        await using var tx = (OracleTransaction)await c.BeginTransactionAsync();
        var rows = Rows(1, 6);
        rows[4].Amount = -1;
        var outcome = Try(() => Copy(c, rows, "SPIKE_ROWS", batchSize: 2));
        var rb = await TryAsync(() => tx.RollbackAsync());
        Record($"copy={outcome}; rollback={rb}; rows left={await Count("SPIKE_ROWS")}");
    }

    // ---------- Q3: identifier casing ----------

    [TestCase("SPIKE_ROWS", "ID", "NAME", "AMOUNT")]
    [TestCase("spike_rows", "ID", "NAME", "AMOUNT")]
    [TestCase("SPIKE_ROWS", "Id", "Name", "Amount")]
    [TestCase("SPIKE_ROWS", "id", "name", "amount")]
    [TestCase("\"SPIKE_ROWS\"", "\"ID\"", "\"NAME\"", "\"AMOUNT\"")]
    public async Task Q3a_Casing_UnquotedUpperTable(string table, string id, string name, string amount)
    {
        await using var c = await OpenAsync();
        var outcome = Try(() => Copy(c, Rows(1, 3), table, map: [("Id", id), ("Name", name), ("Amount", amount)]));
        Record($"table={table} cols={id},{name},{amount}: {outcome}; rows={await Count("SPIKE_ROWS")}");
    }

    [TestCase("MixedCase", "Id", "Name")]
    [TestCase("\"MixedCase\"", "Id", "Name")]
    [TestCase("\"MixedCase\"", "\"Id\"", "\"Name\"")]
    [TestCase("MIXEDCASE", "ID", "NAME")]
    public async Task Q3b_Casing_QuotedMixedCaseTable(string table, string id, string name)
    {
        await using var c = await OpenAsync();
        var outcome = Try(() => Copy(c, Rows(1, 3), table, map: [("Id", id), ("Name", name)]));
        Record($"table={table} cols={id},{name}: {outcome}; rows={await Count("\"MixedCase\"")}");
    }

    [Test]
    public async Task Q3c_NoMappings_SourceNamesDifferInCaseFromColumns()
    {
        await using var c = await OpenAsync();
        // ObjectReader exposes Id/Name/Amount; table columns are ID/NAME/AMOUNT; no ColumnMappings added.
        var outcome = Try(() => Copy(c, Rows(1, 3), "SPIKE_ROWS"));
        Record($"no mappings: {outcome}; rows={await Count("SPIKE_ROWS")}");
    }

    // ---------- Q4: types ----------

    [Test]
    public async Task Q4_Types_BoolGuidNullEmptyStringDateTime()
    {
        await using var c = await OpenAsync();
        var dt = new DataTable("TYPE_ROWS");
        dt.Columns.Add("ID", typeof(int));
        dt.Columns.Add("FLAG", typeof(bool));
        dt.Columns.Add("UID_COL", typeof(Guid));
        dt.Columns.Add("TXT", typeof(string));
        dt.Columns.Add("WHEN_COL", typeof(DateTime));
        var g = Guid.NewGuid();
        dt.Rows.Add(1, true, g, "", new DateTime(2026, 1, 2, 3, 4, 5));
        dt.Rows.Add(2, false, DBNull.Value, DBNull.Value, DBNull.Value);
        var outcome = Try(() =>
        {
            using var bc = new OracleBulkCopy(c) { DestinationTableName = "TYPE_ROWS" };
            bc.WriteToServer(dt.CreateDataReader());
        });
        var read = await TryAsync(async () =>
        {
            await using var cmd = new OracleCommand("SELECT ID, FLAG, RAWTOHEX(UID_COL), TXT, NVL2(TXT,'notnull','NULL'), WHEN_COL FROM TYPE_ROWS ORDER BY ID", c);
            await using var r = await cmd.ExecuteReaderAsync();
            var sb = new System.Text.StringBuilder();
            while (await r.ReadAsync())
                sb.Append($"[{r[0]}|{r[1]}|{r[2]}|'{r[3]}'|{r[4]}|{r[5]}] ");
            TestContext.Out.WriteLine($"guid written={g:N} ; rows={sb}");
        });
        Record($"type copy={outcome}; read={read}");
    }

    // ---------- Q5: async surface ----------

    [Test]
    public void Q5_AsyncSurface()
    {
        var methods = typeof(OracleBulkCopy).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.StartsWith("WriteToServer"))
            .Select(m => $"{m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
        var ctors = typeof(OracleBulkCopy).GetConstructors().Select(ci => $"ctor({string.Join(", ", ci.GetParameters().Select(p => p.ParameterType.Name))})");
        var props = typeof(OracleBulkCopy).GetProperties().Select(p => p.Name);
        var options = Enum.GetNames<OracleBulkCopyOptions>();
        Record($"methods: {string.Join("; ", methods)}\nctors: {string.Join("; ", ctors)}\nprops: {string.Join(", ", props)}\noptions: {string.Join(", ", options)}");
    }

    // ---------- Q6: constraints / triggers ----------

    [Test]
    public async Task Q6a_ForeignKey_Enforced()
    {
        await using var c = await OpenAsync();
        var dt = new DataTable();
        dt.Columns.Add("ID", typeof(int));
        dt.Columns.Add("PARENT_ID", typeof(int));
        dt.Rows.Add(1, 999); // no such parent
        var outcome = Try(() =>
        {
            using var bc = new OracleBulkCopy(c) { DestinationTableName = "CHILD_ROWS" };
            bc.WriteToServer(dt.CreateDataReader());
        });
        var fk = await Scalar("SELECT STATUS || '/' || VALIDATED FROM USER_CONSTRAINTS WHERE TABLE_NAME='CHILD_ROWS' AND CONSTRAINT_TYPE='R'");
        Record($"orphan FK row copy={outcome}; rows={await Count("CHILD_ROWS")}; FK status after={fk}");
    }

    [Test]
    public async Task Q6b_Trigger_Fires_AndIndexState()
    {
        await using var c = await OpenAsync();
        var outcome = Try(() => Copy(c, Rows(1, 3), "TRIGGER_ROWS"));
        Record($"copy into table WITH trigger={outcome}; rows={await Count("TRIGGER_ROWS")}; trigger log rows={await Count("TRIGGER_LOG")}");
    }

    [Test]
    public async Task Q6c_DuplicatePrimaryKey()
    {
        await using var c = await OpenAsync();
        var rows = Rows(1, 3);
        rows[2].Id = 1;
        var outcome = Try(() => Copy(c, rows, "SPIKE_ROWS"));
        var idx = await Scalar("SELECT LISTAGG(INDEX_NAME || '=' || STATUS, ',') FROM USER_INDEXES WHERE TABLE_NAME='SPIKE_ROWS'");
        Record($"dup PK copy={outcome}; rows={await Count("SPIKE_ROWS")}; indexes={idx}");
    }

    [Test]
    public async Task Q6d_DuplicatePrimaryKey_WithEnforceIndexChecks()
    {
        await using var c = await OpenAsync();
        var rows = Rows(1, 3);
        rows[2].Id = 1;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var outcome = Try(() => Copy(c, rows, "SPIKE_ROWS", OracleBulkCopyOptions.EnforceIndexChecks, timeout: 15));
        TestContext.Out.WriteLine($"FINDING-PART Q6d copy returned after {sw.Elapsed.TotalSeconds:F0}s: {outcome}");
        var idx = await Scalar("SELECT LISTAGG(INDEX_NAME || '=' || STATUS, ',') FROM USER_INDEXES WHERE TABLE_NAME='SPIKE_ROWS'");
        Record($"dup PK + EnforceIndexChecks copy={outcome}; rows={await Count("SPIKE_ROWS")}; indexes={idx}");
    }

    [Test]
    public async Task Q6e_AfterUnusableIndex_NormalInsertStillWorks()
    {
        await using var c = await OpenAsync();
        var rows = Rows(1, 3);
        rows[2].Id = 1;
        Try(() => Copy(c, rows, "SPIKE_ROWS"));
        var insert = await TryAsync(() => Exec(c, "INSERT INTO SPIKE_ROWS VALUES (50, 'x', 1)"));
        var select = await Scalar("SELECT COUNT(*) FROM SPIKE_ROWS WHERE ID = 1");
        Record($"after dup-PK load: plain INSERT={insert}; SELECT by PK={select}");
    }

    [Test]
    public async Task Q1h_ExternalTransaction_FailedCopy_CallerCanStillCommitEarlierWork()
    {
        await using var c = await OpenAsync();
        await using var tx = (OracleTransaction)await c.BeginTransactionAsync();
        await Exec(c, "INSERT INTO PARENT_ROWS VALUES (7)");
        var rows = Rows(1, 4);
        rows[2].Amount = -1;
        var copy = Try(() => Copy(c, rows, "SPIKE_ROWS"));
        var commit = await TryAsync(() => tx.CommitAsync());
        Record($"copy={copy}; caller commit={commit}; PARENT_ROWS={await Count("PARENT_ROWS")}; SPIKE_ROWS={await Count("SPIKE_ROWS")}");
    }

    [Test]
    public async Task Q1i_ExternalTransaction_SecondCopyInSameTransaction()
    {
        await using var c = await OpenAsync();
        await using var tx = (OracleTransaction)await c.BeginTransactionAsync();
        var first = Try(() => Copy(c, Rows(1, 3), "SPIKE_ROWS"));
        var second = Try(() => Copy(c, Rows(10, 3), "SPIKE_ROWS", timeout: 15));
        var commit = await TryAsync(() => tx.CommitAsync());
        Record($"first={first}; second copy same table same tx={second}; commit={commit}; rows={await Count("SPIKE_ROWS")}");
    }

    // ---------- helpers ----------

    public sealed class Row { public int Id { get; set; } public string? Name { get; set; } public int Amount { get; set; } }

    private static List<Row> Rows(int from, int count) =>
        Enumerable.Range(from, count).Select(i => new Row { Id = i, Name = $"n{i}", Amount = i }).ToList();

    private static void Copy(OracleConnection c, List<Row> rows, string table,
        OracleBulkCopyOptions options = OracleBulkCopyOptions.Default, int batchSize = 0,
        (string src, string dst)[]? map = null, int timeout = 30)
    {
        using var reader = ObjectReader.Create(rows, "Id", "Name", "Amount");
        using var bc = new OracleBulkCopy(c, options) { DestinationTableName = table, BatchSize = batchSize, BulkCopyTimeout = timeout };
        foreach (var (src, dst) in map ?? []) bc.ColumnMappings.Add(src, dst);
        bc.WriteToServer(reader);
    }

    private async Task<OracleConnection> OpenAsync()
    {
        var c = new OracleConnection(_cs);
        await c.OpenAsync();
        return c;
    }

    /// <summary>Counts from a separate session, so only committed rows are visible.</summary>
    private async Task<string> Count(string table) => await Scalar($"SELECT COUNT(*) FROM {table}");

    private async Task<string> Scalar(string sql)
    {
        try
        {
            await using var c = await OpenAsync();
            await using var cmd = new OracleCommand(sql, c);
            return Convert.ToString(await cmd.ExecuteScalarAsync()) ?? "null";
        }
        catch (Exception e) { return $"ERR {Short(e)}"; }
    }

    private static async Task Exec(OracleConnection c, string sql)
    {
        await using var cmd = new OracleCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task TryExec(OracleConnection c, string sql)
    {
        try { await Exec(c, sql); } catch (OracleException) { }
    }

    private static string Try(Action a)
    {
        try { a(); return "OK"; } catch (Exception e) { return $"THREW {Short(e)}"; }
    }

    private static async Task<string> TryAsync(Func<Task> a)
    {
        try { await a(); return "OK"; } catch (Exception e) { return $"THREW {Short(e)}"; }
    }

    private static string Short(Exception e) => $"{e.GetType().Name}: {e.Message.Split('\n')[0]}";

    private static void Record(string finding)
    {
        TestContext.Out.WriteLine($"FINDING {TestContext.CurrentContext.Test.Name}: {finding}");
        Assert.Pass(finding);
    }
}
