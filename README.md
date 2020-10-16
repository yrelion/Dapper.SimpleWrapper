# Dapper wrapper for Oracle
This wrapper aims at compacting the usage of common [Dapper.Oracle](https://github.com/DIPSAS/Dapper.Oracle) methods, dynamic parameter definitions and query options. Furthermore it provides an opinionated way of handling bounded contexts.

## Features (WIP)

- Database Context per Domain
- Dynamic query searchable/sortable field attachment
- Cross-service transaction fulfillment

## Infrastructural Requirements
This project is built around the assumption that its utilization will reside on the persistence layer and as such is designed with the repository pattern in mind along with an IoC Container for dependency injection.

The examples below use .NET Core's IoC.

## Setup

1. Implement the `IDatabaseSettings` to define the database settings for a bounded context and add it as a singleton to your IoC container.

```csharp
public class CustomerDatabaseSettings : IDatabaseSettings
{
    public string Username { get; set; }
    public string Password { get; set; }
    public string ConnectionString { get; set; }
    public bool UseTestDatabase { get; set; }
}
```

2. Implement the abstract `ConnectionFactoryBase` and add it as a singleton to your IoC container.

```csharp
public class ConnectionFactory<TSettings> : ConnectionFactoryBase<TSettings>
where TSettings : class, IDatabaseSettings, new()
{
    public ConnectionFactory(IOptions<TSettings> options) {
        Settings = options.Value;
    }

    public override IDbConnection Create(string connectionString) {
        return new OracleConnection(connectionString);
    }

    public override IDbConnection CreateFromSettings() {
        var connectionString = string.Format(Settings.ConnectionString,
            Settings.Username, Settings.Password);

        return new OracleConnection(connectionString);
    }
}
```

```csharp
services.AddSingleton<IConnectionFactory<CustomerDatabaseSettings>, ConnectionFactory<CustomerDatabaseSettings>>();
```

3. Define the interface representing the database context of a bounded context.

```csharp
public interface ICustomerDatabaseContext : IDatabaseContext<CustomerDatabaseSettings> { }
```

1. Define the class which implements the newly created interface (`ICustomerDatabaseContext`) and extends the `DbContext`. Subsequently add it as a scoped service to your IoC container.

```csharp
public class CustomerDbContext : DbContext<CustomerDatabaseSettings>, ICustomerDatabaseContext
{
    public CustomerDbContext(IConnectionFactory<CustomerDatabaseSettings> factory) : base(factory) { }
}
```

```csharp
services.AddScoped<ICustomerDatabaseContext, CustomerDbContext>();
```

5. Extend the abstract `QueryHandler` to implement repositorial cross-cutting concerns such as exception handling or statement logging as seen fit.

```csharp
public abstract class SampleQueryHandler : QueryHandler
{
    protected SampleQueryHandler(IDatabaseContext<IDatabaseSettings> databaseContext) : base(databaseContext) { }

    protected override void HandleException(Exception e) {
        Log.Error("Exception: {@e}", e);
        throw new InternalDatabaseException();
    }

    protected override void HandleRollback() {
        if (Transaction == null)
            return;

        Transaction.Rollback();
        Log.Warning("[TRANSACTION] Rollback performed");
    }

    protected override void LogSqlQuery(string statement, DynamicParameters parameters) { 
        // Log sql query if required
    }

    protected override void LogSqlOperation(string statement, DynamicParameters parameters) {
        // Log sql operation if required
    }
}
```
## Usage (WIP)

A repository acting on a bounded context is simply implemented by injecting the desired database context and passing it to constructor of the base class.

```csharp
public class CustomerRepository : SampleQueryHandler
{
    public CustomerRepository(ICustomerDatabaseContext databaseContext) : base(databaseContext) { }
}
```

Retrieving sets of objects

```csharp
IEnumerable<Customer> result = await QueryAsync<Customer>("SELECT * FROM CUSTOMERS WHERE TYPE = :TYPE", new DynamicParameters(), (parameters, listOptions) => {
    parameters.Add("@TYPE", 2, DbType.Int32, ParameterDirection.Input);
}, options);
```

Retrieving single object

```csharp
Customer result = await QueryFirstAsync<Customer>("SELECT * FROM CUSTOMERS WHERE ID = :ID", new DynamicParameters(), (parameters, listOptions) => {
    parameters.Add("@ID", 58, DbType.Int32, ParameterDirection.Input);
}, options);
```

