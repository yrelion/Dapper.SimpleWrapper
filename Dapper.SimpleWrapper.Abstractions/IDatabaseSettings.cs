﻿namespace Dapper.SimpleWrapper.Abstractions
{
    public interface IDatabaseSettings
    {
        string Username { get; set; }
        string Password { get; set; }
        string ConnectionString { get; set; }
        DatabaseEnvironment Environment { get; set; }
    }
}
