using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dapper.SimpleWrapper.Abstractions;
using Dapper.SimpleWrapper.Common;
using Dapper.SimpleWrapper.Common.Utilities;
using Dapper.SimpleWrapper.Models;
using Indice.Types;

namespace Dapper.SimpleWrapper
{
    public abstract class QueryHandler : IDisposable
    {
        private bool _disposed;
        protected IDbConnection Connection { get; }
        protected IDbTransaction Transaction { get; }

        protected QueryHandler(IDatabaseContext<IDatabaseSettings> databaseContext)
        {
            Transaction = databaseContext.TransactionContext.Transaction;
            Connection = databaseContext.Connection;
        }

        /// <summary>
        /// Query base method which runs the provided query operation given a set of common parameters
        /// </summary>
        /// <typeparam name="TSubject">The subject type that is to be manipulated by the query options attachment process</typeparam>
        /// <typeparam name="TResult">The result type</typeparam>
        /// <param name="operation">The query function to run</param>
        /// <param name="sql">The SQL query string</param>
        /// <param name="parameters">The named parameters to feed the query with</param>
        /// <param name="intermediaryAction">The action to run prior to the <paramref name="operation"/> execution that manipulates the <see cref="parameters"/> and <see cref="options"/></param>
        /// <param name="options">The <see cref="ListOptions"/> to add to the final SQL query</param>
        /// <param name="queryOptionsAction">The action to run upon the query prior to the <see cref="operation"/> execution that manipulates the <see cref="sql"/></param>
        /// <returns>The <see cref="TResult"/></returns>
        private async Task<TResult> QueryAsyncBase<TSubject, TResult>(Func<string, DynamicParameters, Task<TResult>> operation, string sql, DynamicParameters parameters = null,
            Action<DynamicParameters, ListOptions> intermediaryAction = null, ListOptions options = null, Func<string> queryOptionsAction = null)
        {
            try
            {
                intermediaryAction?.Invoke(parameters ?? new DynamicParameters(), options);
                sql = queryOptionsAction?.Invoke();
                LogSqlQuery(sql, parameters);
                return await operation(sql, parameters);
            }
            catch (Exception e)
            {
                Connection.Close();
                HandleException(e);
                return default(TResult);
            }
        }

        /// <summary>
        /// Procedure base method which runs the provided procedure operation given a set of common parameters
        /// </summary>
        /// <param name="operation">The command execution function to run</param>
        /// <param name="command">The SQL Command to execute</param>
        /// <param name="parameters">The named parameters to feed the command with</param>
        /// <param name="intermediaryAction">The action to run prior to the <paramref name="command"/> execution</param>
        /// <param name="postExecutionAction">The action to run after the <paramref name="command"/> execution</param>
        /// <returns>The <see cref="CommandExecutionResult"/></returns>
        private async Task<CommandExecutionResult> ExecuteAsyncBase(Func<Task<int>> operation, string command, DynamicParameters parameters = null,
            Action<DynamicParameters> intermediaryAction = null, Action<int> postExecutionAction = null)
        {
            try
            {
                intermediaryAction?.Invoke(parameters ?? new DynamicParameters());
                LogSqlOperation(command, parameters);
                var rowsAffected = await operation.Invoke();
                postExecutionAction?.Invoke(rowsAffected);

                return new CommandExecutionResult
                {
                    Parameters = parameters,
                    RowsAffected = rowsAffected
                };
            }
            catch (Exception e)
            {
                HandleException(e);
                HandleRollback();
                Connection.Close();
                return null;
            }
        }

        /// <summary>
        /// Runs a query returning multiple filtered results via a custom Dapper method
        /// </summary>
        /// <typeparam name="TResult">The result type</typeparam>
        /// <param name="sql">The SQL query string</param>
        /// <param name="function">The explicit function to run in which a custom Dapper operation may be run</param>
        /// <param name="parameters">The named parameters to feed the query with</param>
        /// <param name="intermediaryAction">The action to run prior to the <paramref name="sql"/> execution</param>
        /// <param name="options">The <see cref="ListOptions"/> to add to the final SQL query</param>
        /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="TResult"/></returns>
        protected async Task<IEnumerable<TResult>> QueryExplicitAsync<TResult>(string sql, Func<string, SqlMapper.IDynamicParameters, Task<IEnumerable<TResult>>> function,
            DynamicParameters parameters = null, Action<DynamicParameters, ListOptions> intermediaryAction = null, ListOptions options = null)
            where TResult : class
        {
            return await QueryAsyncBase<TResult, IEnumerable<TResult>>(function,
                sql, parameters, intermediaryAction, options, () => AttachQueryOptions<TResult>(ref sql, parameters, options));
        }

        /// <summary>
        /// Runs a query returning multiple filtered results
        /// </summary>
        /// <typeparam name="TResult">The result type</typeparam>
        /// <param name="sql">The SQL query string</param>
        /// <param name="parameters">The named parameters to feed the query with</param>
        /// <param name="intermediaryAction">The action to run prior to the <paramref name="sql"/> execution</param>
        /// <param name="options">The <see cref="ListOptions"/> to add to the final SQL query</param>
        /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="TResult"/></returns>
        protected async Task<IEnumerable<TResult>> QueryAsync<TResult>(string sql, DynamicParameters parameters = null, Action<DynamicParameters, ListOptions> intermediaryAction = null,
            ListOptions options = null)
            where TResult : class
        {
            return await QueryAsyncBase<TResult, IEnumerable<TResult>>((modifiedSql, modifiedParameters) => Connection.QueryAsync<TResult>(modifiedSql, modifiedParameters),
                sql, parameters, intermediaryAction, options, () => AttachQueryOptions<TResult>(ref sql, parameters, options));
        }

        /// <summary>
        /// Runs a query returning multiple filtered results along with the total record count present in the database
        /// </summary>
        /// <typeparam name="TResult">The result type</typeparam>
        /// <param name="sql">The SQL query string</param>
        /// <param name="parameters">The named parameters to feed the query with</param>
        /// <param name="intermediaryAction">The action to run prior to the <paramref name="sql"/> execution</param>
        /// <param name="options">The <see cref="ListOptions"/> to add to the final SQL query</param>
        /// <returns>A <see cref="ResultSet{T}"/> of <see cref="TResult"/></returns>
        protected async Task<ResultSet<TResult>> QueryResultSetAsync<TResult>(string sql, DynamicParameters parameters = null, Action<DynamicParameters, ListOptions> intermediaryAction = null,
            ListOptions options = null)
            where TResult : class
        {
            var items = await QueryAsyncBase<TResult, IEnumerable<TResult>>((modifiedSql, modifiedParameters) => Connection.QueryAsync<TResult>(modifiedSql, modifiedParameters),
                sql, parameters, intermediaryAction, options, () => AttachQueryOptions<TResult>(ref sql, parameters, options));

            var splitSql = sql.Substring(sql.IndexOf("from", StringComparison.OrdinalIgnoreCase));

            QueryBuilder.RemoveQueryClauses(ref splitSql, new[] { "order by", "offset", "fetch" });

            var countSql = $"SELECT COUNT({0}) {splitSql}";

            var count = await Connection.ExecuteScalarAsync<int>(countSql, parameters);

            return new ResultSet<TResult>(items, count);
        }

        /// <summary>
        /// Runs a query returning a single result
        /// </summary>
        /// <typeparam name="TResult">The result type</typeparam>
        /// <param name="sql">The SQL query string</param>
        /// <param name="parameters">The named parameters to feed the query with</param>
        /// <param name="intermediaryAction">The action to run prior to the <paramref name="sql"/> execution</param>
        /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="TResult"/></returns>
        protected async Task<TResult> QueryFirstAsync<TResult>(string sql, DynamicParameters parameters = null, Action<DynamicParameters, ListOptions> intermediaryAction = null)
            where TResult : class
        {
            return await QueryAsyncBase<TResult, TResult>((modifiedSql, modifiedParameters) => Connection.QueryFirstOrDefaultAsync<TResult>(sql, modifiedParameters),
                sql, parameters, intermediaryAction);
        }

        /// <summary>
        /// Runs a query that selects a single value
        /// </summary>
        /// <typeparam name="TResult">The result value type</typeparam>
        /// <param name="sql">The SQL query string</param>
        /// <param name="parameters">The named parameters to feed the query with</param>
        /// <param name="intermediaryAction">The action to run prior to the <paramref name="sql"/> execution</param>
        /// <returns>The single query value</returns>
        protected async Task<TResult> QueryScalarAsync<TResult>(string sql, DynamicParameters parameters = null, Action<DynamicParameters, ListOptions> intermediaryAction = null)
        {
            return await QueryAsyncBase<TResult, TResult>((modifiedSql, modifiedParameters) => Connection.ExecuteScalarAsync<TResult>(sql, modifiedParameters),
                sql, parameters, intermediaryAction);
        }

        /// <summary>
        /// Executes a stored procedure
        /// </summary>
        /// <param name="procedureName">The procedure name to execute</param>
        /// <param name="parameters">The named parameters to feed the query with</param>
        /// <param name="intermediaryAction">The action to run prior to the procedure execution</param>
        /// <returns>The <see cref="CommandExecutionResult"/></returns>
        protected async Task<CommandExecutionResult> ExecuteProcedureAsync(string procedureName, DynamicParameters parameters = null, Action<DynamicParameters> intermediaryAction = null)
        {
            return await ExecuteAsyncBase(() => Connection.ExecuteAsync(procedureName, parameters, commandType: CommandType.StoredProcedure),
                procedureName, parameters, intermediaryAction);
        }

        /// <summary>
        /// Executes an operation meant to affect multiple rows
        /// </summary>
        /// <param name="sql">The SQL query string</param>
        /// <param name="parameters">The named parameters to feed the query with</param>
        /// <param name="intermediaryAction">The action to run prior to the <paramref name="sql"/> execution</param>
        /// <returns>The number of affected rows</returns>
        protected async Task<CommandExecutionResult> ExecuteAsync(string sql, DynamicParameters parameters = null, Action<DynamicParameters> intermediaryAction = null)
        {
            return await ExecuteAsyncBase(() => Connection.ExecuteAsync(sql, parameters), sql, parameters, intermediaryAction);
        }

        /// <summary>
        /// Executes an operation meant to affect a single row. This is used only when the record is known to exist and failure to operate upon it specifically
        /// results in an incomplete operation.
        /// </summary>
        /// <param name="sql">The SQL query string</param>
        /// <param name="parameters">The named parameters to feed the query with</param>
        /// <param name="intermediaryAction">The action to run prior to the <paramref name="sql"/> execution</param>
        /// <exception cref="IncompleteDatabaseOperationException"/>
        protected async Task<CommandExecutionResult> ExecuteSingleAsync(string sql, DynamicParameters parameters = null, Action<DynamicParameters> intermediaryAction = null)
        {
            return await ExecuteAsyncBase(() => Connection.ExecuteAsync(sql, parameters), sql, parameters, intermediaryAction, rowsAffected =>
            {
                if (rowsAffected != 1)
                    throw new IncompleteDatabaseOperationException();
            });
        }

        #region Query ListOptions Attachment

        /// <summary>
        /// Edits the sql query by adding meta-filtering based on the parameters and their ability to be filtered
        /// </summary>
        /// <typeparam name="TFilterable">The filterable entity</typeparam>
        /// <param name="sql">The sql to be modified</param>
        /// <param name="parameters">The named parameters to use as searchable fields</param>
        /// <param name="options">The <see cref="ListOptions"/> to add to the final SQL query</param>
        protected string AttachQueryOptions<TFilterable>(ref string sql, DynamicParameters parameters, ListOptions options)
            where TFilterable : class
        {
            if (options == null)
            {
                var defaultOptions = new ListOptions();

                QueryBuilder.AttachPagingOption(ref sql, defaultOptions.Page, defaultOptions.Size);
                QueryBuilder.AttachSizeOption(ref sql, defaultOptions.Size);

                return sql;
            }

            AttachSearchOptions<TFilterable>(ref sql, options.Search, parameters);
            AttachSortOptions<TFilterable>(ref sql, options.GetSortings());
            QueryBuilder.AttachPagingOption(ref sql, options.Page, options.Size);
            QueryBuilder.AttachSizeOption(ref sql, options.Size);

            return sql;
        }

        /// <summary>
        /// Edits the sql query by adding sorting based on sortable properties of <see cref="TFilterable"/>
        /// </summary>
        /// <typeparam name="TFilterable">The filterable entity</typeparam>
        /// <param name="sql">The sql to be modified</param>
        /// <param name="sortingClauses">The sorting clauses</param>
        protected void AttachSortOptions<TFilterable>(ref string sql, IEnumerable<SortByClause> sortingClauses)
            where TFilterable : class
        {
            var sortablePropertyInfo = TypeInfoExtractor.GetPropertiesByAttribute<TFilterable, SortableAttribute>().ToList();
            var propertyNameAttributes = sortablePropertyInfo.ToDictionary(k => k.Name.ToUpper(), v => v.GetCustomAttribute(typeof(SortableAttribute)) as SortableAttribute);

            if (sortingClauses == null || !propertyNameAttributes.Any())
                return;

            var sortingList = sortingClauses.ToList();

            // keep only sortable fields
            sortingList.RemoveAll(s => !propertyNameAttributes.Any(f => f.Key.ToUpper().Equals(s.Path.ToUpper())));

            if (!sortingList.Any())
                return;

            var sortQuerySegment = string.Empty;

            foreach (SortByClause clause in sortingList)
            {
                var fieldName = propertyNameAttributes[clause.Path.ToUpper()]?.FieldName;
                var propertyName = sortablePropertyInfo.FirstOrDefault(x => string.Equals(x.Name, clause.Path, StringComparison.CurrentCultureIgnoreCase))?.Name;

                var field = fieldName == null ? propertyName?.ToUpper() : fieldName.ToUpper();

                sortQuerySegment += $"\n {field} {clause.Direction.ToUpper()} {(clause != sortingList.Last() ? "," : string.Empty)}";
            }

            sql += $"\n ORDER BY {sortQuerySegment}";
        }

        /// <summary>
        /// Edits the sql query by adding search on searchable properties of <see cref="TFilterable"/>
        /// </summary>
        /// <typeparam name="TFilterable">The filterable entity</typeparam>
        /// <param name="sql">The sql to be modified</param>
        /// <param name="term">The search term</param>
        /// <param name="parameters">The parameters object to use to include the additional search parameters</param>
        protected void AttachSearchOptions<TFilterable>(ref string sql, string term, DynamicParameters parameters)
            where TFilterable : class
        {
            var searchablePropertyInfo = TypeInfoExtractor.GetPropertiesByAttribute<TFilterable, SearchableAttribute>().ToList();
            var propertyNameAttributes = searchablePropertyInfo.ToDictionary(k => k.Name.ToUpper(), v => v.GetCustomAttribute(typeof(SearchableAttribute)) as SearchableAttribute);

            var isPreParameterized = parameters.ParameterNames.Any();

            if (string.IsNullOrEmpty(term) || !propertyNameAttributes.Any())
                return;

            List<string> expressions = new List<string>();

            foreach (KeyValuePair<string, SearchableAttribute> property in propertyNameAttributes)
            {
                if (term.Length < property.Value.MinimumLength)
                    continue;

                var fieldName = property.Value.FieldName;
                var propertyName = searchablePropertyInfo.FirstOrDefault(x => string.Equals(x.Name, property.Key, StringComparison.CurrentCultureIgnoreCase))?.Name;

                var field = fieldName == null ? propertyName?.ToUpper() : fieldName.ToUpper();

                expressions.Add($"UPPER({field}) LIKE UPPER(:{field})");
                parameters.Add($"@{field}", $"%{term}%", null, ParameterDirection.Input);
            }

            if (isPreParameterized)
            { // If parameters exist before adding new ones, then a where clause is assumed to be present in the sql query
                QueryBuilder.AddSearchQueryParameters(ref sql, expressions, buildWhereClause: false);
            }
            else
            {
                QueryBuilder.AddSearchQueryParameters(ref sql, expressions);
            }
        }

        #endregion

        /// <summary>
        /// Handles the exception during query execution
        /// </summary>
        /// <param name="e">The exception</param>
        protected abstract void HandleException(Exception e);

        /// <summary>
        /// Handles the rollback during query execution
        /// </summary>
        protected abstract void HandleRollback();

        /// <summary>
        /// Logs the sql operation prior to its execution
        /// </summary>
        /// <param name="statement">The sql statement</param>
        /// <param name="parameters">The parameters passed</param>
        protected abstract void LogSqlOperation(string statement, DynamicParameters parameters);

        /// <summary>
        /// Logs the sql query prior to its execution
        /// </summary>
        /// <param name="statement">The sql statement</param>
        /// <param name="parameters">The parameters passed</param>
        protected abstract void LogSqlQuery(string statement, DynamicParameters parameters);

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
