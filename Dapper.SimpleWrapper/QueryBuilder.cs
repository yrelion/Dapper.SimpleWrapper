using System.Collections.Generic;
using System.Linq;

namespace Dapper.SimpleWrapper
{
    internal static class QueryBuilder
    {
        public static void AddSearchQueryParameters(ref string query, IReadOnlyList<string> expressions, bool buildWhereClause = true)
        {
            string clause;

            var result = clause = string.Empty;

            if (!expressions.Any())
                return;

            foreach (var expression in expressions)
            {
                var condition = expressions[0].Equals(expression) ? string.Empty : "\n OR ";
                clause += $"{condition}{expression}";
            }

            if (buildWhereClause)
                result += "\n WHERE {0}";
            else
                result += "\n AND ({0})";

            query += string.Format(result, clause);
        }

        public static void AttachPagingOption(ref string sql, int page, int pageSize)
        {
            if (page <= 1)
                return;

            sql += $"\n OFFSET {(page - 1) * pageSize} ROWS";
        }

        public static void AttachSizeOption(ref string sql, int size)
        {
            sql += $"\n FETCH NEXT {size} ROWS ONLY";
        }
    }
}
