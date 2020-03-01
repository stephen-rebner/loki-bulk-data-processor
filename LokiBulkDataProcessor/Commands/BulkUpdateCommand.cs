using MediatR;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static Loki.BulkDataProcessor.Commands.BulkUpdate;

namespace Loki.BulkDataProcessor.Commands
{
    public static class BulkUpdate
    {
        public class Command : IRequest
        { 
            public int BatchSize { get; }

            public int Timeout { get; }

            public string ConnectionString { get; }

            public IEnumerable<object> DataToProcess { get; }

            public Command(int batchSize, int timeout, string connectionString, IEnumerable<object> dataToProcess)
            {
                BatchSize = batchSize;
                Timeout = timeout;
                ConnectionString = connectionString;
                DataToProcess = dataToProcess;
            }
        }
    }

    public class BulkUpDateCommandHandler : IRequestHandler<Command>
    {
        public Task<Unit> Handle(Command request, CancellationToken cancellationToken)
        {
            // todo: handle stuff

            throw new NotImplementedException();
        }
    }
}
