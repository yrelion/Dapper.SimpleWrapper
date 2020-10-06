using System.Collections.Generic;
using System.Data;
using System.Transactions;
using Dapper.SimpleWrapper.Abstractions;

namespace Dapper.SimpleWrapper.Common
{
    public class TransactionContext : ITransactionContext
    {
        public string OriginatorName { get; set; }
        public IDbTransaction Transaction { get; set; }
        public TransactionStatus Status { get; set; }
        public List<string> Errors { get; }

        public TransactionContext()
        {
            Errors = new List<string>();
        }
    }
}
