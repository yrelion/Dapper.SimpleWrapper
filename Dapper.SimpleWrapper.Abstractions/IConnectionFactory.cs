using System.Data;

namespace Dapper.SimpleWrapper.Abstractions
{
    public interface IConnectionFactory
    {
        IDatabaseSettings Settings { get; }
        IDbConnection Create(string connectionString);
        IConnectionFactory WithSettings(IDatabaseSettings settings);
    }
}
