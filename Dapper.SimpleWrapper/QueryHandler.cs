using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
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
        /// <param name="parameters">The named parameters to build the final query with</param>
        /// <param name="intermediaryAction">The action to run prior to the <paramref name="operation"/> execution</param>
        /// <param name="options">The <see cref="ListOptions"/> to add to the final SQL query</param>
        /// <returns>A <see cref="Task"/></returns>
        private async Task<TResult> QueryAsyncBase<TSubject, TResult>(Func<string, DynamicParameters, Task<TResult>> operation, string sql, DynamicParameters parameters = null,
            Action<DynamicParameters, ListOptions> intermediaryAction = null, ListOptions options = null)
            where TSubject : class
            where TResult : class
        {

            try
            {
                parameters = parameters ?? new DynamicParameters();
                intermediaryAction?.Invoke(parameters, options);

                AttachQueryOptions<TSubject>(ref sql, parameters, options);

                LogSql(sql, parameters);
                return await operation(sql, parameters);
            }
            catch (Exception e)
            {
                Connection.Close();
                HandleException(e);
                return default(TResult);
            }
        }

        protected async Task<IEnumerable<TResult>> QueryExplicitAsync<TResult>(string sql, Func<string, SqlMapper.IDynamicParameters, Task<IEnumerable<TResult>>> function, DynamicParameters parameters = null, Action<DynamicParameters, ListOptions> intermediaryAction = null, ListOptions options = null) where TResult : class
        {
            return await QueryAsyncBase<TResult, IEnumerable<TResult>>(function, sql, parameters, intermediaryAction, options);
        }

        protected async Task<IEnumerable<TResult>> QueryAsync<TResult>(string sql, DynamicParameters parameters = null, Action<DynamicParameters, ListOptions> intermediaryAction = null, ListOptions options = null) where TResult : class
        {
            return await QueryAsyncBase<TResult, IEnumerable<TResult>>((modifiedSql, modifiedParameters) => Connection.QueryAsync<TResult>(modifiedSql, modifiedParameters), sql, parameters, intermediaryAction, options);
        }

        protected async Task<TResult> QueryFirstAsync<TResult>(string sql, DynamicParameters parameters = null, Action<DynamicParameters, ListOptions> intermediaryAction = null) where TResult : class
        {
            return await QueryAsyncBase<TResult, TResult>((modifiedSql, modifiedParameters) => Connection.QueryFirstOrDefaultAsync<TResult>(sql, modifiedParameters), sql, parameters, intermediaryAction);
        }

        protected async Task<int> ExecuteAsync(string sql, DynamicParameters parameters = null, Action<DynamicParameters> intermediaryAction = null)
        {
            try
            {
                intermediaryAction?.Invoke(parameters ?? new DynamicParameters());
                LogSql(sql, parameters);
                var rowsAffected = await Connection.ExecuteAsync(sql, parameters);

                return rowsAffected;
            }
            catch (Exception e)
            {
                HandleException(e);
                HandleRollback();
                Connection.Close();
                return default(int);
            }
        }

        protected async Task<int> ExecuteSingleAsync(string sql, DynamicParameters parameters = null, Action<DynamicParameters> intermediaryAction = null)
        {
            try
            {
                intermediaryAction?.Invoke(parameters ?? new DynamicParameters());
                LogSql(sql, parameters);
                var rowsAffected = await Connection.ExecuteAsync(sql, parameters);

                if (rowsAffected != 1)
                    throw new IncompleteDatabaseOperationException();

                return rowsAffected;
            }
            catch (Exception e)
            {
                Connection.Close();
                HandleException(e);
                return default(int);
            }
        }

        protected async Task<T> ExecuteScalarAsync<T>(string sql, DynamicParameters parameters = null, Action<DynamicParameters> intermediaryAction = null)
        {
            try
            {
                intermediaryAction?.Invoke(parameters ?? new DynamicParameters());
                LogSql(sql, parameters);
                var value = await Connection.ExecuteScalarAsync<T>(sql, parameters);

                return value;
            }
            catch (Exception e)
            {
                HandleException(e);
                HandleRollback();
                Connection.Close();
                return default(T);
            }
        }

        #region Query ListOptions Attachment

        protected void AttachQueryOptions<TFilterable>(ref string sql, DynamicParameters parameters, ListOptions options) where TFilterable : class
        {
            if (options == null)
            {
                var defaultOptions = new ListOptions();

                QueryBuilder.AttachPagingOption(ref sql, defaultOptions.Page, defaultOptions.Size);
                QueryBuilder.AttachSizeOption(ref sql, defaultOptions.Size);

                return;
            }

            AttachSearchOptions<TFilterable>(ref sql, options.Search, parameters);
            AttachSortOptions<TFilterable>(ref sql, options.GetSortings());
            QueryBuilder.AttachPagingOption(ref sql, options.Page, options.Size);
            QueryBuilder.AttachSizeOption(ref sql, options.Size);
        }

        protected void AttachSortOptions<TFilterable>(ref string sql, IEnumerable<SortByClause> sortingClauses) where TFilterable : class
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
                var fieldName = propertyNameAttributes[clause.Path.ToUpper()].FieldName;
                var propertyName = sortablePropertyInfo.FirstOrDefault(x => string.Equals(x.Name, clause.Path, StringComparison.CurrentCultureIgnoreCase))?.Name;

                var field = fieldName == null ? propertyName?.ToUpper() : fieldName.ToUpper();

                sortQuerySegment += $"\n {field} {clause.Direction.ToUpper()} {(clause != sortingList.Last() ? "," : string.Empty)}";
            }

            sql += $"\n ORDER BY {sortQuerySegment}";
        }

        protected void AttachSearchOptions<TFilterable>(ref string sql, string term, DynamicParameters parameters) where TFilterable : class
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

        protected async Task<TResult> QueryFirstAsync<TResult>(string sql, DynamicParameters parameters = null, Action<DynamicParameters> intermediaryAction = null) where TResult : class
        {
            try
            {
                intermediaryAction?.Invoke(parameters ?? new DynamicParameters());
                LogSql(sql, parameters);
                return await Connection.QueryFirstOrDefaultAsync<TResult>(sql, parameters);
            }
            catch (Exception e)
            {
                Connection.Close();
                HandleException(e);
                return default(TResult);
            }
        }

        #region Post-Exception Processes

        protected abstract void HandleException(Exception e);
        protected abstract void HandleRollback();
        protected abstract void LogSql(string statement, DynamicParameters parameters);

        #endregion

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // here you add your dispose logic for resources.
                    Connection.Dispose();
                }
            }
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
