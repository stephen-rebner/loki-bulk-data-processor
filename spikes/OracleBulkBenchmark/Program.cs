// Throwaway benchmark spike for the 9.0.0 Oracle write-mode decision. Not shipped, not in the solution.
// Compares, inside an owned transaction (begin → write → commit), like Loki's pipeline:
//   Oracle array-bind INSERT (the planned default) at several chunk sizes, object[] and typed buffers
//   Oracle array-bind INSERT /*+ APPEND_VALUES */ (direct-path INSERT, one execute)
//   Oracle OracleBulkCopy direct path (the planned opt-in)
//   SQL Server SqlBulkCopy with CheckConstraints | KeepNulls (what Loki's SQL Server writer uses), as the reference
// Usage: dotnet run -c Release -- [oracleCs] [sqlCs] [volumes e.g. 10000,100000,1000000] [reps]
using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Oracle.ManagedDataAccess.Client;

string oraCs = Arg(0) ?? "User Id=loki;Password=Bench_pw1;Data Source=localhost:1521/FREEPDB1;Pooling=true";
string sqlCs = Arg(1) ?? "Server=localhost,1433;User Id=sa;Password=Bench_pw1!;TrustServerCertificate=true;Database=LokiBench";
int[] volumes = (Arg(2) ?? "10000,100000,1000000").Split(',').Select(int.Parse).ToArray();
int reps = int.Parse(Arg(3) ?? "3");
bool runSql = Environment.GetEnvironmentVariable("SKIP_SQL") != "1";

const int ParentCount = 1000;
string[] tables = ["BENCH_HEAP", "BENCH_IDX"];

OracleSchema.Create(oraCs, ParentCount);
if (runSql) SqlSchema.Create(sqlCs, ParentCount);

var methods = new List<Method>
{
    new("Oracle", "ArrayBind object[] chunk 1000", (t, d) => OracleWriters.ArrayBind(oraCs, t, d, 1000, typed: false, append: false)),
    new("Oracle", "ArrayBind object[] chunk 5000", (t, d) => OracleWriters.ArrayBind(oraCs, t, d, 5000, typed: false, append: false)),
    new("Oracle", "ArrayBind object[] chunk 20000", (t, d) => OracleWriters.ArrayBind(oraCs, t, d, 20000, typed: false, append: false)),
    new("Oracle", "ArrayBind typed[] chunk 5000", (t, d) => OracleWriters.ArrayBind(oraCs, t, d, 5000, typed: true, append: false)),
    new("Oracle", "ArrayBind APPEND_VALUES one execute", (t, d) => OracleWriters.ArrayBind(oraCs, t, d, d.Rows.Count, typed: true, append: true)),
    new("Oracle", "OracleBulkCopy BatchSize 0", (t, d) => OracleWriters.DirectPath(oraCs, t, d, 0)),
    new("Oracle", "OracleBulkCopy BatchSize 5000", (t, d) => OracleWriters.DirectPath(oraCs, t, d, 5000)),
};
if (runSql)
{
    methods.Add(new("SqlServer", "SqlBulkCopy BatchSize 0", (t, d) => SqlWriters.BulkCopy(sqlCs, t, d, 0)));
    methods.Add(new("SqlServer", "SqlBulkCopy BatchSize 5000", (t, d) => SqlWriters.BulkCopy(sqlCs, t, d, 5000)));
}

Console.WriteLine("volume\ttable\tengine\tmethod\tmedian_ms\tmin_ms\trows_per_sec\tnote");
foreach (int volume in volumes)
{
    DataTable data = Data.Build(volume, ParentCount);
    foreach (string table in tables)
    {
        foreach (Method method in methods)
        {
            var timings = new List<double>();
            string note = "";
            for (int rep = 0; rep < reps + 1; rep++)   // rep 0 is a warm-up and is discarded
            {
                Reset(method.Engine, table);
                try
                {
                    var sw = Stopwatch.StartNew();
                    await method.Run(table, data);
                    sw.Stop();
                    long count = Count(method.Engine, table);
                    if (count != volume) { note = $"row count {count} != {volume}"; }
                    if (rep > 0) timings.Add(sw.Elapsed.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    note = $"FAILED: {ex.GetType().Name}: {FirstLine(ex.Message)}";
                    break;
                }
            }
            if (method.Engine == "Oracle" && note == "") note = OracleSchema.UnusableIndexes(oraCs, table);
            if (timings.Count == 0)
            {
                Console.WriteLine($"{volume}\t{table}\t{method.Engine}\t{method.Name}\t-\t-\t-\t{note}");
                continue;
            }
            timings.Sort();
            double median = timings[timings.Count / 2];
            Console.WriteLine($"{volume}\t{table}\t{method.Engine}\t{method.Name}\t{median:F0}\t{timings[0]:F0}\t{volume / (median / 1000):F0}\t{note}");
        }
    }
}

void Reset(string engine, string table)
{
    if (engine == "Oracle") OracleSchema.Truncate(oraCs, table); else SqlSchema.Truncate(sqlCs, table);
}

long Count(string engine, string table) =>
    engine == "Oracle" ? OracleSchema.Count(oraCs, table) : SqlSchema.Count(sqlCs, table);

string Arg(int i) => string.IsNullOrEmpty(args.ElementAtOrDefault(i)) ? null : args[i];

static string FirstLine(string s) => s.Split('\n')[0].Trim();

record Method(string Engine, string Name, Func<string, DataTable, Task> Run);

static class Data
{
    public static DataTable Build(int rows, int parents)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("ParentId", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Amount", typeof(decimal));
        table.Columns.Add("Created", typeof(DateTime));
        table.Columns.Add("Flag", typeof(int));
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        table.BeginLoadData();
        for (int i = 1; i <= rows; i++)
        {
            table.Rows.Add(i, (i % parents) + 1, $"Name {i:D10} {(i * 7919) % 100000:D5}",
                Math.Round(i * 1.37m, 2), start.AddSeconds(i), i % 2);
        }
        table.EndLoadData();
        table.AcceptChanges();
        return table;
    }
}

static class OracleSchema
{
    public static void Create(string cs, int parents)
    {
        using var c = new OracleConnection(cs);
        c.Open();
        foreach (string t in new[] { "BENCH_IDX", "BENCH_HEAP", "BENCH_PARENT" })
        {
            Exec(c, $"BEGIN EXECUTE IMMEDIATE 'DROP TABLE {t} PURGE'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -942 THEN RAISE; END IF; END;");
        }
        Exec(c, "CREATE TABLE BENCH_PARENT (ID NUMBER(10) PRIMARY KEY)");
        Exec(c, $"INSERT INTO BENCH_PARENT SELECT LEVEL FROM DUAL CONNECT BY LEVEL <= {parents}");
        Exec(c, "COMMIT");
        const string cols = "ID NUMBER(10) NOT NULL, PARENT_ID NUMBER(10) NOT NULL, NAME VARCHAR2(100), AMOUNT NUMBER(18,2), CREATED TIMESTAMP, FLAG NUMBER(1)";
        Exec(c, $"CREATE TABLE BENCH_HEAP ({cols})");
        Exec(c, $"CREATE TABLE BENCH_IDX ({cols}, CONSTRAINT BENCH_IDX_PK PRIMARY KEY (ID), " +
                "CONSTRAINT BENCH_IDX_FK FOREIGN KEY (PARENT_ID) REFERENCES BENCH_PARENT (ID))");
        Exec(c, "CREATE INDEX BENCH_IDX_NAME ON BENCH_IDX (NAME)");
    }

    public static void Truncate(string cs, string table)
    {
        using var c = new OracleConnection(cs);
        c.Open();
        Exec(c, $"TRUNCATE TABLE {table}");
        // A failed direct-path load can leave indexes UNUSABLE; rebuild so every run starts clean.
        using var cmd = new OracleCommand($"SELECT INDEX_NAME FROM USER_INDEXES WHERE TABLE_NAME = '{table}' AND STATUS = 'UNUSABLE'", c);
        var names = new List<string>();
        using (var r = cmd.ExecuteReader()) while (r.Read()) names.Add(r.GetString(0));
        foreach (string n in names) Exec(c, $"ALTER INDEX {n} REBUILD");
    }

    public static string UnusableIndexes(string cs, string table)
    {
        using var c = new OracleConnection(cs);
        c.Open();
        using var cmd = new OracleCommand($"SELECT LISTAGG(INDEX_NAME, ',') FROM USER_INDEXES WHERE TABLE_NAME = '{table}' AND STATUS = 'UNUSABLE'", c);
        object v = cmd.ExecuteScalar();
        return v is string s && s.Length > 0 ? $"UNUSABLE: {s}" : "";
    }

    public static long Count(string cs, string table)
    {
        using var c = new OracleConnection(cs);
        c.Open();
        using var cmd = new OracleCommand($"SELECT COUNT(*) FROM {table}", c);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static void Exec(OracleConnection c, string sql)
    {
        using var cmd = new OracleCommand(sql, c);
        cmd.ExecuteNonQuery();
    }
}

static class OracleWriters
{
    // Mirrors the planned ArrayBindOracleWriter: stream the IDataReader into per-column buffers, one execute per chunk.
    public static async Task ArrayBind(string cs, string table, DataTable data, int chunk, bool typed, bool append)
    {
        await using var c = new OracleConnection(cs);
        await c.OpenAsync();
        await using var tx = (OracleTransaction)await c.BeginTransactionAsync();
        using IDataReader reader = data.CreateDataReader();

        using var cmd = c.CreateCommand();
        string hint = append ? "/*+ APPEND_VALUES */ " : "";
        cmd.CommandText = $"INSERT {hint}INTO {table} (ID, PARENT_ID, NAME, AMOUNT, CREATED, FLAG) VALUES (:1, :2, :3, :4, :5, :6)";
        cmd.BindByName = false;
        OracleDbType[] types = [OracleDbType.Int32, OracleDbType.Int32, OracleDbType.Varchar2, OracleDbType.Decimal, OracleDbType.TimeStamp, OracleDbType.Int16];
        foreach (OracleDbType t in types) cmd.Parameters.Add(new OracleParameter { OracleDbType = t, Direction = ParameterDirection.Input });

        int n = 0;
        if (typed)
        {
            var id = new int[chunk]; var pid = new int[chunk]; var name = new string[chunk];
            var amount = new decimal[chunk]; var created = new DateTime[chunk]; var flag = new short[chunk];
            async Task Flush()
            {
                cmd.ArrayBindCount = n;
                cmd.Parameters[0].Value = n == chunk ? id : id[..n];
                cmd.Parameters[1].Value = n == chunk ? pid : pid[..n];
                cmd.Parameters[2].Value = n == chunk ? name : name[..n];
                cmd.Parameters[3].Value = n == chunk ? amount : amount[..n];
                cmd.Parameters[4].Value = n == chunk ? created : created[..n];
                cmd.Parameters[5].Value = n == chunk ? flag : flag[..n];
                await cmd.ExecuteNonQueryAsync();
                n = 0;
            }
            while (reader.Read())
            {
                id[n] = reader.GetInt32(0); pid[n] = reader.GetInt32(1); name[n] = reader.GetString(2);
                amount[n] = reader.GetDecimal(3); created[n] = reader.GetDateTime(4); flag[n] = (short)reader.GetInt32(5);
                if (++n == chunk) await Flush();
            }
            if (n > 0) await Flush();
        }
        else
        {
            var cols = new object[6][];
            for (int i = 0; i < 6; i++) cols[i] = new object[chunk];
            async Task Flush()
            {
                cmd.ArrayBindCount = n;
                for (int i = 0; i < 6; i++) cmd.Parameters[i].Value = n == chunk ? cols[i] : cols[i][..n];
                await cmd.ExecuteNonQueryAsync();
                for (int i = 0; i < 6; i++) Array.Clear(cols[i], 0, n);
                n = 0;
            }
            while (reader.Read())
            {
                for (int i = 0; i < 6; i++)
                {
                    object v = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                    cols[i][n] = i == 5 ? (object)Convert.ToInt16(v) : v;   // Flag → NUMBER(1) via Int16, as the type map does for bool
                }
                if (++n == chunk) await Flush();
            }
            if (n > 0) await Flush();
        }
        await tx.CommitAsync();
    }

    // Mirrors the planned DirectPathOracleWriter: OracleBulkCopyOptions.Default, joins the connection's transaction.
    public static async Task DirectPath(string cs, string table, DataTable data, int batchSize)
    {
        await using var c = new OracleConnection(cs);
        await c.OpenAsync();
        await using var tx = (OracleTransaction)await c.BeginTransactionAsync();
        using IDataReader reader = data.CreateDataReader();
        using (var bulk = new OracleBulkCopy(c, OracleBulkCopyOptions.Default) { DestinationTableName = table, BatchSize = batchSize, BulkCopyTimeout = 600 })
        {
            (string src, string dst)[] map = [("Id", "ID"), ("ParentId", "PARENT_ID"), ("Name", "NAME"), ("Amount", "AMOUNT"), ("Created", "CREATED"), ("Flag", "FLAG")];
            foreach (var (src, dst) in map) bulk.ColumnMappings.Add(new OracleBulkCopyColumnMapping(src, dst));
            bulk.WriteToServer(reader);
        }
        await tx.CommitAsync();
    }
}

static class SqlSchema
{
    public static void Create(string cs, int parents)
    {
        var master = new SqlConnectionStringBuilder(cs) { InitialCatalog = "master" }.ConnectionString;
        using (var m = new SqlConnection(master))
        {
            m.Open();
            Exec(m, "IF DB_ID('LokiBench') IS NULL CREATE DATABASE LokiBench");
            Exec(m, "ALTER DATABASE LokiBench SET RECOVERY SIMPLE");
        }
        SqlConnection.ClearAllPools();
        using var c = new SqlConnection(cs);
        c.Open();
        Exec(c, "DROP TABLE IF EXISTS BENCH_IDX; DROP TABLE IF EXISTS BENCH_HEAP; DROP TABLE IF EXISTS BENCH_PARENT;");
        Exec(c, "CREATE TABLE BENCH_PARENT (ID INT PRIMARY KEY)");
        Exec(c, $"INSERT INTO BENCH_PARENT SELECT TOP ({parents}) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) FROM sys.all_objects a CROSS JOIN sys.all_objects b");
        const string cols = "ID INT NOT NULL, PARENT_ID INT NOT NULL, NAME NVARCHAR(100), AMOUNT DECIMAL(18,2), CREATED DATETIME2, FLAG TINYINT";
        Exec(c, $"CREATE TABLE BENCH_HEAP ({cols})");
        Exec(c, $"CREATE TABLE BENCH_IDX ({cols}, CONSTRAINT BENCH_IDX_PK PRIMARY KEY (ID), " +
                "CONSTRAINT BENCH_IDX_FK FOREIGN KEY (PARENT_ID) REFERENCES BENCH_PARENT (ID))");
        Exec(c, "CREATE INDEX BENCH_IDX_NAME ON BENCH_IDX (NAME)");
    }

    public static void Truncate(string cs, string table)
    {
        using var c = new SqlConnection(cs);
        c.Open();
        Exec(c, $"TRUNCATE TABLE {table}");
    }

    public static long Count(string cs, string table)
    {
        using var c = new SqlConnection(cs);
        c.Open();
        using var cmd = new SqlCommand($"SELECT COUNT_BIG(*) FROM {table}", c);
        return (long)cmd.ExecuteScalar();
    }

    private static void Exec(SqlConnection c, string sql)
    {
        using var cmd = new SqlCommand(sql, c) { CommandTimeout = 120 };
        cmd.ExecuteNonQuery();
    }
}

static class SqlWriters
{
    // Mirrors Loki's SqlServerBulkWriter options: CheckConstraints | KeepNulls, inside the pipeline's transaction.
    public static async Task BulkCopy(string cs, string table, DataTable data, int batchSize)
    {
        await using var c = new SqlConnection(cs);
        await c.OpenAsync();
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync();
        using IDataReader reader = data.CreateDataReader();
        using (var bulk = new SqlBulkCopy(c, SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.KeepNulls, tx)
               { DestinationTableName = table, BatchSize = batchSize, BulkCopyTimeout = 600 })
        {
            (string src, string dst)[] map = [("Id", "ID"), ("ParentId", "PARENT_ID"), ("Name", "NAME"), ("Amount", "AMOUNT"), ("Created", "CREATED"), ("Flag", "FLAG")];
            foreach (var (src, dst) in map) bulk.ColumnMappings.Add(src, dst);
            await bulk.WriteToServerAsync(reader);
        }
        await tx.CommitAsync();
    }
}
