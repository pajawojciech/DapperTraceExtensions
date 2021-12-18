using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DapperTraceExtensions
{
    public static class DapperTraceExtensions
    {
        /// <summary>
        /// Writes query to debug output
        /// </summary>
        /// <param name="t"></param>
        /// <param name="s"></param>
        /// <param name="p"></param>
        public static void WriteQuery(this DynamicParameters parameters, string query = "")
        {
            if (Debugger.IsAttached)
            {
                var result =
$@"--SQL
{ PrepareQuery(parameters, query) }
--END SQL";

                Trace.WriteLine(result);
            }
        }

        /// <summary>
        /// Returns query string
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="s"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public static string GetQuery(this DynamicParameters parameters, string query = "")
        {
            return PrepareQuery(parameters, query);
        }

        /// <summary>
        /// This extension converts an enumerable set to a Dapper TVP
        /// </summary>
        /// <typeparam name="T">type of enumerbale</typeparam>
        /// <param name="enumerable">list of values</param>
        /// <param name="typeName">database type name</param>
        /// <param name="orderedColumnNames">if more than one column in a TVP, 
        /// columns order must mtach order of columns in TVP</param>
        /// <returns>a custom query parameter</returns>
        public static SqlMapper.ICustomQueryParameter AsTableParameter<T>(this IEnumerable<T> enumerable, string typeName, IEnumerable<string> orderedColumnNames = null)
        {
            var dataTable = new DataTable();
            if (typeof(T).IsValueType || typeof(T).FullName.Equals("System.String"))
            {
                dataTable.Columns.Add(orderedColumnNames == null ?
                    "NONAME" : orderedColumnNames.First(), typeof(T));
                foreach (var obj in enumerable)
                {
                    dataTable.Rows.Add(obj);
                }
            }
            else
            {
                var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var readableProperties = properties.Where(w => w.CanRead).ToArray();

                if (readableProperties.Length > 1 && orderedColumnNames == null)
                {
                    throw new ArgumentException("Ordered list of column names must be provided when TVP contains more than one column");
                }

                var columnNames = (orderedColumnNames ?? readableProperties.Select(s => s.Name)).ToArray();

                foreach (var name in columnNames)
                {
                    dataTable.Columns.Add(name, readableProperties.Single(s => s.Name.Equals(name)).PropertyType);
                }

                foreach (var obj in enumerable)
                {
                    dataTable.Rows.Add(columnNames.Select(s => readableProperties.Single(s2 => s2.Name.Equals(s)).GetValue(obj)).ToArray());
                }
            }
            return dataTable.AsTableValuedParameter(typeName);
        }

        private static string PrepareQuery(DynamicParameters parameters, string query = "")
        {
            var sb = new StringBuilder();
            var sbAdditional = new StringBuilder();

            if (parameters != null)
            {
                foreach (var name in parameters.ParameterNames)
                {
                    sbAdditional.AppendLine($"@{name} = @{name}");
                    var pValue = parameters.Get<dynamic>(name);

                    var parameter = new DynamicParameter(pValue, name);
                    sb.AppendLine(parameter.GetDeclaration());
                }

                if (!string.IsNullOrEmpty(query))
                {
                    sb.AppendLine(string.Format("EXEC {0}", query));
                    if (sbAdditional.Length > 0)
                    {
                        sb.Append(sbAdditional.ToString());
                    }
                }
            }
            else
            {
                sb.AppendLine("NULL");
            }

            return sb.ToString();
        }
    }
}
