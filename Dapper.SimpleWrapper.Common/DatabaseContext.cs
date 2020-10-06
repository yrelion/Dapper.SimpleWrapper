using System;
using System.Data;
using Dapper.SimpleWrapper.Abstractions;

namespace Dapper.SimpleWrapper.Common
{
    public abstract class DatabaseContext : IDatabaseContext
    {
        private bool _disposed;

        public IDbConnection Connection { get; set; }
        public IDbTransaction Transaction { get; set; }

        public IDbTransaction BeginTransaction()
        {
            if (Connection.State != ConnectionState.Open)
            {
                Connection.Open();
                Transaction = Connection.BeginTransaction();
            }

            return Transaction;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
                Connection.Dispose();
            
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
