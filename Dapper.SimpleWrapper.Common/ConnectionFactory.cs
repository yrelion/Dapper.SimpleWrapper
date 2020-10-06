using System.Data;
using Dapper.SimpleWrapper.Abstractions;

namespace Dapper.SimpleWrapper.Common
{
    public abstract class ConnectionFactory : IConnectionFactory
    {
        public IDatabaseSettings Settings { get; set; }

        public abstract IDbConnection Create(string connectionString);

        public IConnectionFactory WithSettings(IDatabaseSettings settings)
        {
            Settings = settings;
            return this;
        }
    }
}
