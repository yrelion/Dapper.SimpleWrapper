using System.Data;

namespace Dapper.SimpleWrapper.Abstractions
{
    public interface IConnectionFactory<TSettings> where TSettings : IDatabaseSettings
    {
        IDbConnection Create(string connectionString);
        IDbConnection CreateFromSettings();
    }
}
