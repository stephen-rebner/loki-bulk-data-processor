using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Loki.BulkDataProcessor.Providers;
using Moq;

namespace LokiBulkDataProcessor.UnitTests.TestDoubles;

/// <summary>A Moq IBulkCopyProvider wired to a FakeDbConnection and a recording writer.</summary>
internal sealed class FakeProvider
{
    public FakeProvider(string name = "Fake")
    {
        Connection = new FakeDbConnection();
        Writer = new Mock<IBulkWriter>();
        Writer.Setup(w => w.WriteAsync(It.IsAny<IDataReader>(), It.IsAny<BulkWriteTarget>(), It.IsAny<CancellationToken>()))
            .Callback<IDataReader, BulkWriteTarget, CancellationToken>(RecordWrite)
            .Returns(() => WriteResult());
        Mock.Setup(p => p.Name).Returns(name);
        Mock.Setup(p => p.CreateConnection(It.IsAny<string>())).Returns(() => Connection);
        Mock.Setup(p => p.CreateWriter(It.IsAny<BulkWriteContext>()))
            .Callback<BulkWriteContext>(context => Contexts.Add(context))
            .Returns(() => Writer.Object);
        Mock.Setup(p => p.TranslateException(It.IsAny<Exception>(), It.IsAny<BulkWriteTarget>()))
            .Returns<Exception, BulkWriteTarget>((exception, _) => new InvalidOperationException("translated", exception));
    }

    public Mock<IBulkCopyProvider> Mock { get; } = new();

    public IBulkCopyProvider Object => Mock.Object;

    public FakeDbConnection Connection { get; set; }

    public Mock<IBulkWriter> Writer { get; }

    /// <summary>Thrown by the writer's WriteAsync when set.</summary>
    public Exception WriteException { get; set; }

    public List<BulkWriteContext> Contexts { get; } = new();

    public List<BulkWriteTarget> Targets { get; } = new();

    public List<IDataReader> Readers { get; } = new();

    /// <summary>Column names the reader exposed while it was being written (it may be disposed afterwards).</summary>
    public List<string[]> ReaderColumns { get; } = new();

    public void AcceptExternal(FakeDbTransaction transaction) =>
        Mock.Setup(p => p.AdoptExternalTransaction(It.IsAny<IDbTransaction>())).Returns(transaction);

    public void RejectExternal() =>
        Mock.Setup(p => p.AdoptExternalTransaction(It.IsAny<IDbTransaction>()))
            .Throws(new ArgumentException("wrong driver", "transaction"));

    private void RecordWrite(IDataReader reader, BulkWriteTarget target, CancellationToken _)
    {
        Readers.Add(reader);
        Targets.Add(target);
        string[] names = new string[reader.FieldCount];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = reader.GetName(i);
        }

        ReaderColumns.Add(names);
    }

    private Task WriteResult() => WriteException is null ? Task.CompletedTask : Task.FromException(WriteException);
}
