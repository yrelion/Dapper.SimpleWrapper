using System;

namespace Dapper.SimpleWrapper.Models
{
    /// <summary>
    /// Represents an error of database querying
    /// </summary>
    public class InternalDatabaseException : Exception
    {
        public InternalDatabaseException() : base("A database error has occured.") { }
    }

    /// <summary>
    /// Represents an incomplete database operation
    /// </summary>
    public class IncompleteDatabaseOperationException : Exception
    {
        public IncompleteDatabaseOperationException() : base("The database operation could not be completed.") { }
    }
}
