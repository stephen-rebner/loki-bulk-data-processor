namespace LokiBulkDataProcessor.Oracle.IntegrationTests;

/// <summary>
/// The constant SQL of the Oracle test schema: the contract tables (Blogs, Posts, TestDbModels) and the Oracle-only tables.
/// Every statement is a constant; values are always bind variables and no identifier is ever interpolated.
/// </summary>
/// <remarks>
/// Every contract <c>Id</c> is <c>GENERATED ALWAYS AS IDENTITY</c>, so a contract test that tried to write an identity column would
/// fail with ORA-32795. <c>TestDbModels.NullableBoolColumn</c> has <c>DEFAULT 1</c>, so the KeepNulls contract test can tell an
/// explicit NULL from a defaulted value (SQL Server has the same default, added by an EF migration).
/// </remarks>
internal static class OracleSchema
{
    /// <summary>The Oracle-only table with a quoted mixed-case name, for the identifier resolution tests.</summary>
    public const string MixedCaseTable = "MixedCase";

    /// <summary>The primary key index of <c>KeyedRows</c>, checked after a duplicate-key failure.</summary>
    public const string KeyedRowsPrimaryKey = "PK_KEYEDROWS";

    public const string TriggerStamp = "set by trigger";

    public static readonly string[] CreateStatements =
    {
        """
        CREATE TABLE Blogs (
            Id NUMBER(10) GENERATED ALWAYS AS IDENTITY CONSTRAINT PK_Blogs PRIMARY KEY,
            Url VARCHAR2(2000))
        """,
        """
        CREATE TABLE Posts (
            Id NUMBER(10) GENERATED ALWAYS AS IDENTITY CONSTRAINT PK_Posts PRIMARY KEY,
            BlogId NUMBER(10) NOT NULL CONSTRAINT FK_Posts_Blogs_BlogId REFERENCES Blogs (Id) ON DELETE CASCADE,
            Title VARCHAR2(4000),
            Content VARCHAR2(4000))
        """,
        "CREATE INDEX IX_Posts_BlogId ON Posts (BlogId)",
        """
        CREATE TABLE TestDbModels (
            Id NUMBER(10) GENERATED ALWAYS AS IDENTITY CONSTRAINT PK_TestDbModels PRIMARY KEY,
            StringColumn VARCHAR2(4000),
            BoolColumn NUMBER(1) NOT NULL,
            DateColumn TIMESTAMP(7) NOT NULL,
            NullableBoolColumn NUMBER(1) DEFAULT 1,
            NullableDateColumn TIMESTAMP(7))
        """,
        """
        CREATE TABLE OracleTypes (
            Id NUMBER(10) GENERATED ALWAYS AS IDENTITY CONSTRAINT PK_OracleTypes PRIMARY KEY,
            Label VARCHAR2(100) NOT NULL,
            TextColumn VARCHAR2(4000),
            ShortText VARCHAR2(10),
            ClobColumn CLOB,
            NumberFlag NUMBER(1),
            BooleanFlag BOOLEAN,
            GuidColumn RAW(16),
            BytesColumn RAW(2000),
            TimestampColumn TIMESTAMP(7),
            OffsetColumn TIMESTAMP(7) WITH TIME ZONE,
            IntervalColumn INTERVAL DAY(9) TO SECOND(7),
            DateColumn DATE,
            DecimalColumn NUMBER(28, 10),
            DoubleColumn BINARY_DOUBLE,
            LongColumn NUMBER(19))
        """,
        "CREATE TABLE KeyedRows (Id NUMBER(10) CONSTRAINT PK_KeyedRows PRIMARY KEY, Name VARCHAR2(100))",
        """
        CREATE TABLE TriggeredRows (
            Id NUMBER(10) GENERATED ALWAYS AS IDENTITY CONSTRAINT PK_TriggeredRows PRIMARY KEY,
            Name VARCHAR2(100),
            Stamp VARCHAR2(100))
        """,
        """
        CREATE OR REPLACE TRIGGER TriggeredRows_BI BEFORE INSERT ON TriggeredRows FOR EACH ROW
        BEGIN
            :NEW.Stamp := 'set by trigger';
        END;
        """,
        """
        CREATE TABLE "MixedCase" (
            "Id" NUMBER(10) GENERATED ALWAYS AS IDENTITY CONSTRAINT "PK_MixedCase" PRIMARY KEY,
            "Name" VARCHAR2(100))
        """
    };

    /// <summary>Empties every table and restarts the contract identities. Run after <c>OracleConnection.ClearAllPools()</c>.</summary>
    public static readonly string[] ResetStatements =
    {
        "TRUNCATE TABLE Posts",
        "DELETE FROM Blogs",
        "TRUNCATE TABLE TestDbModels",
        "TRUNCATE TABLE OracleTypes",
        "TRUNCATE TABLE KeyedRows",
        "TRUNCATE TABLE TriggeredRows",
        """TRUNCATE TABLE "MixedCase" """,
        "ALTER TABLE Blogs MODIFY Id GENERATED ALWAYS AS IDENTITY (START WITH 1)",
        "ALTER TABLE Posts MODIFY Id GENERATED ALWAYS AS IDENTITY (START WITH 1)",
        "ALTER TABLE TestDbModels MODIFY Id GENERATED ALWAYS AS IDENTITY (START WITH 1)"
    };

    public const string InsertBlog = "INSERT INTO Blogs (Url) VALUES (:url) RETURNING Id INTO :id";

    public const string CountBlogs = "SELECT COUNT(*) FROM Blogs";

    public const string SelectPosts = "SELECT Id, BlogId, Title, Content FROM Posts ORDER BY Id";

    public const string SelectTestDbModels =
        "SELECT StringColumn, BoolColumn, DateColumn, NullableBoolColumn, NullableDateColumn FROM TestDbModels ORDER BY Id";

    /// <summary>Blocks every other session's INSERT into TestDbModels until the locking transaction ends.</summary>
    public const string LockTestDbModels = "LOCK TABLE TestDbModels IN EXCLUSIVE MODE";

    public const string InsertKeyedRow = "INSERT INTO KeyedRows (Id, Name) VALUES (:id, :name)";

    public const string SelectKeyedRowNames = "SELECT Name FROM KeyedRows ORDER BY Id";

    public const string IndexStatus = "SELECT STATUS FROM USER_INDEXES WHERE INDEX_NAME = :indexName";

    public const string SelectTriggeredRows = "SELECT Name, Stamp FROM TriggeredRows ORDER BY Id";

    public const string SelectMixedCaseNames = """SELECT "Name" FROM "MixedCase" ORDER BY "Id" """;

    public const string SelectOracleTypes = "SELECT * FROM OracleTypes ORDER BY Id";
}
