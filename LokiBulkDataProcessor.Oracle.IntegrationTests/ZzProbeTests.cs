using System;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace LokiBulkDataProcessor.Oracle.IntegrationTests;

[TestFixture, Explicit]
public sealed class ZzProbeTests
{
    private const string Check = """
        SELECT COUNT(*) FROM ALL_INDEXES WHERE TABLE_OWNER = NVL(:owner, SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA'))
        AND TABLE_NAME = :tableName AND STATUS = 'UNUSABLE'
        """;
    private readonly OracleTestDatabase _db = new();

    [OneTimeSetUp] public Task Start() => _db.StartAsync();
    [OneTimeTearDown] public async Task Stop() => await _db.DisposeAsync();
    [SetUp] public Task Reset() => _db.ResetAsync();

    private static void Log(string s) => TestContext.Progress.WriteLine("PROBE " + s);

    private static DataTable Dups()
    {
        var t = new DataTable(); t.Columns.Add("Id", typeof(int)); t.Columns.Add("Name", typeof(string));
        t.Rows.Add(1, "a"); t.Rows.Add(1, "b"); return t;
    }

    private static void Load(OracleConnection c, DataTable t, string table, int timeout = 0)
    {
        using var bc = new OracleBulkCopy(c, OracleBulkCopyOptions.Default) { DestinationTableName = table, BulkCopyTimeout = timeout };
        foreach (DataColumn col in t.Columns) bc.ColumnMappings.Add(col.ColumnName, col.ColumnName.ToUpperInvariant());
        bc.WriteToServer(t);
    }

    private static object Scalar(OracleConnection c, string sql, params (string, object)[] p)
    {
        using var cmd = c.CreateCommand(); cmd.CommandText = sql; cmd.BindByName = true; cmd.CommandTimeout = 20;
        foreach (var (n, v) in p) cmd.Parameters.Add(n, v ?? DBNull.Value);
        return cmd.ExecuteScalar();
    }

    private static string Status(OracleConnection c) => (string)Scalar(c, "SELECT STATUS FROM USER_INDEXES WHERE INDEX_NAME = 'PK_KEYEDROWS'");

    [Test]
    public async Task A_RollbackRestores()
    {
        await using var c = await _db.OpenConnectionAsync();
        var tx = c.BeginTransaction();
        Load(c, Dups(), "KEYEDROWS");
        Log($"A in-tx status={Status(c)} count={Scalar(c, Check, ("owner", null), ("tableName", "KEYEDROWS"))}");
        await using (var other = await _db.OpenConnectionAsync())
        {
            var sw = Stopwatch.StartNew();
            try { Log($"A other-session status={Status(other)} after {sw.ElapsedMilliseconds}ms"); } catch (Exception e) { Log("A other err " + e.Message); }
        }
        tx.Rollback();
        Log($"A after rollback status={Status(c)}");
        await using var c2 = await _db.OpenConnectionAsync();
        Log($"A after rollback (new session) status={Status(c2)} rows={Scalar(c2, "SELECT COUNT(*) FROM KeyedRows")}");
        try { Scalar(c2, "INSERT INTO KeyedRows (Id, Name) VALUES (5, 'x')"); Log("A insert after rollback ok"); } catch (Exception e) { Log("A insert err " + e.Message); }
    }

    [Test]
    public async Task B_UpdateOnUnusable()
    {
        await using var c = await _db.OpenConnectionAsync();
        var tx = c.BeginTransaction(); Load(c, Dups(), "KEYEDROWS"); tx.Commit();
        Log($"B committed status={Status(c)}");
        try { Scalar(c, "UPDATE KeyedRows SET Name = 'u' WHERE Name = 'a'"); Log("B update by non-key ok"); } catch (OracleException e) { Log("B update err " + e.Number); }
        try { Scalar(c, "UPDATE KeyedRows SET Name = 'u'"); Log("B update all ok"); } catch (OracleException e) { Log("B update all err " + e.Number); }
        try { Scalar(c, "SELECT COUNT(*) FROM KeyedRows WHERE Id = 1"); Log("B select by key ok"); } catch (OracleException e) { Log("B select err " + e.Number); }
        try { Scalar(c, "SELECT COUNT(*) FROM V$SESSION"); Log("B v$session readable"); } catch (OracleException e) { Log("B v$session err " + e.Number); }
        Log("B dup rowids: " + Scalar(c, "SELECT COUNT(*) FROM KeyedRows k WHERE ROWID > (SELECT MIN(ROWID) FROM KeyedRows d WHERE d.Id = k.Id)"));
        try { Scalar(c, "DELETE FROM KeyedRows k WHERE ROWID > (SELECT MIN(ROWID) FROM KeyedRows d WHERE d.Id = k.Id)"); Log("B delete dups ok"); } catch (OracleException e) { Log("B delete dups err " + e.Number); }
        try { Scalar(c, "ALTER SESSION SET SKIP_UNUSABLE_INDEXES = TRUE"); Scalar(c, "DELETE FROM KeyedRows k WHERE ROWID > (SELECT MIN(ROWID) FROM KeyedRows d WHERE d.Id = k.Id)"); Log("B delete dups with skip ok"); } catch (OracleException e) { Log("B delete skip err " + e.Number); }
    }

    [Test]
    public async Task C_TimeoutUnderLock()
    {
        await using var blocker = await _db.OpenConnectionAsync();
        var lockTx = blocker.BeginTransaction();
        Scalar(blocker, OracleSchema.LockTestDbModels);
        var release = Task.Run(async () => { await Task.Delay(8000); lockTx.Rollback(); Log("C released at 8s"); });
        await using var c = await _db.OpenConnectionAsync();
        var tx = c.BeginTransaction();
        var t = new DataTable(); t.Columns.Add("StringColumn", typeof(string)); t.Columns.Add("BoolColumn", typeof(bool)); t.Columns.Add("DateColumn", typeof(DateTime));
        t.Rows.Add("x", true, DateTime.Now);
        var sw = Stopwatch.StartNew();
        try { Load(c, t, "TESTDBMODELS", timeout: 1); Log($"C load ok after {sw.ElapsedMilliseconds}ms"); }
        catch (Exception e) { Log($"C load err after {sw.ElapsedMilliseconds}ms {e.GetType().Name} {(e as OracleException)?.Number} {e.Message}"); }
        await release;
        try { tx.Rollback(); Log("C rollback ok"); } catch (Exception e) { Log("C rollback err " + e.Message); }
    }

    private static void Try(string label, Func<object> f) { try { Log($"{label}: {f()}"); } catch (Exception e) { Log($"{label}: ERR {(e as OracleException)?.Number} {e.Message.Split('\n')[0]}"); } }

    [Test]
    public async Task D_InTx()
    {
        await using var c = await _db.OpenConnectionAsync();
        var tx = c.BeginTransaction();
        Load(c, Dups(), "KEYEDROWS");
        Try("D status", () => Status(c));
        Try("D all_indexes", () => Scalar(c, "SELECT STATUS || '/' || INDEX_TYPE || '/' || UNIQUENESS FROM ALL_INDEXES WHERE INDEX_NAME = 'PK_KEYEDROWS'"));
        Try("D count full", () => Scalar(c, "SELECT /*+ FULL(k) */ COUNT(*) FROM KeyedRows k"));
        Try("D count via index", () => Scalar(c, "SELECT /*+ INDEX(k PK_KEYEDROWS) */ COUNT(*) FROM KeyedRows k WHERE Id = 1"));
        Try("D index fast full", () => Scalar(c, "SELECT /*+ INDEX_FFS(k PK_KEYEDROWS) */ COUNT(Id) FROM KeyedRows k"));
        Try("D obj last_ddl", () => Scalar(c, "SELECT TO_CHAR(LAST_DDL_TIME,'HH24:MI:SS') FROM USER_OBJECTS WHERE OBJECT_NAME = 'PK_KEYEDROWS'"));
        Try("D insert new key", () => Scalar(c, "INSERT INTO KeyedRows (Id, Name) VALUES (7, 'c')"));
        Try("D status after insert", () => Status(c));
        tx.Commit();
        Try("D status after commit", () => Status(c));
    }

    [Test]
    public async Task E_NoTx()
    {
        await using var c = await _db.OpenConnectionAsync();
        Load(c, Dups(), "KEYEDROWS");
        Try("E status autocommit", () => Status(c));
    }

    [Test]
    public async Task F_InTxStatusFromOtherSessionAfterDelay()
    {
        await using var c = await _db.OpenConnectionAsync();
        var tx = c.BeginTransaction();
        Load(c, Dups(), "KEYEDROWS");
        await Task.Delay(2000);
        Try("F status", () => Status(c));
        Try("F ind$ flags", () => Scalar(c, "SELECT STATUS FROM ALL_INDEXES WHERE INDEX_NAME = 'PK_KEYEDROWS'"));
        tx.Rollback();
    }

    private void ZeroRow(OracleConnection c, string t, string col)
    {
        Try($"G {t} insert0", () => Scalar(c, $"INSERT INTO {t} ({col}) SELECT NULL FROM DUAL WHERE 1 = 0"));
        Try($"G {t} update0", () => Scalar(c, $"UPDATE {t} SET {col} = {col} WHERE 1 = 0"));
        Try($"G {t} delete0", () => Scalar(c, $"DELETE FROM {t} WHERE 1 = 0"));
        Try($"G {t} lock", () => Scalar(c, $"LOCK TABLE {t} IN EXCLUSIVE MODE NOWAIT"));
    }

    [Test]
    public async Task G_Detect()
    {
        await using var c = await _db.OpenConnectionAsync();
        c.InfoMessage += (_, e) => Log($"G infomessage: {e.Message} / {string.Join(",", System.Linq.Enumerable.Select(System.Linq.Enumerable.Cast<OracleError>(e.Errors), x => x.Number))}");
        var tx = c.BeginTransaction();
        Load(c, Dups(), "KEYEDROWS");
        ZeroRow(c, "KEYEDROWS", "Name");
        ZeroRow(c, "KEYEDROWS", "Id");
        tx.Rollback();
        var tx2 = c.BeginTransaction();
        var t = new DataTable(); t.Columns.Add("StringColumn", typeof(string)); t.Columns.Add("BoolColumn", typeof(bool)); t.Columns.Add("DateColumn", typeof(DateTime));
        t.Rows.Add("x", true, DateTime.Now);
        Load(c, t, "TESTDBMODELS");
        ZeroRow(c, "TESTDBMODELS", "StringColumn");
        tx2.Commit();
        Try("G committed rows", () => Scalar(c, "SELECT COUNT(*) FROM TestDbModels"));
    }

    [Test]
    public async Task H_NonUniqueIndex()
    {
        await using var c = await _db.OpenConnectionAsync();
        var tx = c.BeginTransaction();
        var t = new DataTable(); t.Columns.Add("Title", typeof(string)); t.Columns.Add("BlogId", typeof(int));
        t.Rows.Add("a", 5); t.Rows.Add("b", 5);
        Load(c, t, "POSTS");
        ZeroRow(c, "POSTS", "Title");
        tx.Commit();
        Try("H statuses", () => Scalar(c, "SELECT LISTAGG(INDEX_NAME || '=' || STATUS, ',') FROM USER_INDEXES WHERE TABLE_NAME = 'POSTS'"));
    }
}
