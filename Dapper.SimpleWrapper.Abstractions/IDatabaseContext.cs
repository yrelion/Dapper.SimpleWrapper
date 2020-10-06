using System.Data;

namespace Dapper.SimpleWrapper.Abstractions
{
    public interface IDatabaseContext
    {
        IDbConnection Connection { get; set; }
        IDbTransaction Transaction { get; }
        IDbTransaction BeginTransaction();
    }
}
