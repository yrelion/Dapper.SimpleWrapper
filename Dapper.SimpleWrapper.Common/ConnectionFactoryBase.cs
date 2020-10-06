using System.Data;
using Dapper.SimpleWrapper.Abstractions;

namespace Dapper.SimpleWrapper.Common
{
    public abstract class ConnectionFactoryBase<TSettings> : IConnectionFactory<TSettings> where TSettings : class, IDatabaseSettings, new()
    {
        public IDatabaseSettings Settings { get; set; }
        public abstract IDbConnection Create(string connectionString);
        public abstract IDbConnection CreateFromSettings();
    }
}
