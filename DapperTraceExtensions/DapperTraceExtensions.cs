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
        public static void WriteQuery(this DynamicParameters t, string s = "", object p = null)
        {
            if (Debugger.IsAttached)
            {
                Trace.WriteLine(PrepareQuery(t, s, p));
            }
        }

        /// <summary>
        /// Returns query string
        /// </summary>
        /// <param name="t"></param>
        /// <param name="s"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public static string GetQuery(this DynamicParameters t, string s = "", object p = null)
        {
            return PrepareQuery(t, s, p);
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
                foreach (T obj in enumerable)
                {
                    dataTable.Rows.Add(obj);
                }
            }
            else
            {
                PropertyInfo[] properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
                PropertyInfo[] readableProperties = properties.Where(w => w.CanRead).ToArray();

                if (readableProperties.Length > 1 && orderedColumnNames == null)
                    throw new ArgumentException("Ordered list of column names must be provided when TVP contains more than one column");

                var columnNames = (orderedColumnNames ?? readableProperties.Select(s => s.Name)).ToArray();

                foreach (string name in columnNames)
                {
                    dataTable.Columns.Add(name, readableProperties.Single(s => s.Name.Equals(name)).PropertyType);
                }

                foreach (T obj in enumerable)
                {
                    dataTable.Rows.Add(columnNames.Select(s => readableProperties.Single(s2 => s2.Name.Equals(s)).GetValue(obj)).ToArray());
                }
            }
            return dataTable.AsTableValuedParameter(typeName);
        }

        private static string PrepareQuery(DynamicParameters t, string s = "", object pp = null)
        {
            var sb = new StringBuilder();
            var sb2 = new StringBuilder();
            int pPos = 0;

            if (t != null)
            {
                if (t.GetType().ToString() == "Dapper.DynamicParameters")
                {
                    foreach (var name in t.ParameterNames)
                    {
                        sb2.AppendLine($"@{name} = @{name}");
                        var pValue = t.Get<dynamic>(name);
                        if (pValue == null)
                        {
                            sb.AppendLine($"DECLARE @{name} NVARCHAR(MAX)");
                            continue;
                        }
                        var type = pValue.GetType();
                        if (type == typeof(DateTime))
                        {
                            sb.AppendLine($"DECLARE @{name} DATETIME ='{pValue.ToString("yyyy-MM-dd HH:mm:ss.fff")}'");
                        }
                        else if (type == typeof(bool))
                        {
                            var value = (bool)pValue ? 1 : 0;
                            sb.AppendLine($"DECLARE @{name} BIT = {value}");
                        }
                        else if (type == typeof(int))
                        {
                            sb.AppendLine($"DECLARE @{name} INT = {pValue}");
                        }
                        //else if (type == typeof(List<int>))
                        //{
                        //    sb.AppendLine($"-- REPLACE @{name} IN SQL: ({string.Join(",", (List<int>)pValue)})");
                        //}
                        else if (type == typeof(decimal) || type == typeof(double))
                        {
                            var precision = pValue.ToString().Length - 1;
                            var scale = 0;

                            var split = pValue.ToString().Split(",");
                            if (split.Length > 1)
                            {
                                scale = split[1].Length;
                            }
                            sb.AppendLine($"DECLARE @{name} DECIMAL({precision},{scale}) = {pValue.ToString().Replace(",", ".")}");
                        }
                        else if (type.ToString() == "Dapper.TableValuedParameter")
                        {
                            //if (pp is Array)
                            //{
                            //    if ((pp as Array).Length > pPos)
                            //    {
                            //        var obj = (pp as Array).GetValue(pPos);
                            //        if ((pp as Array).Length > pPos + 1 && (pp as Array).GetValue(pPos + 1) is string)
                            //        {
                            //            sb.AppendLine($"DECLARE @{name} {(pp as Array).GetValue(pPos + 1)}");
                            //            pPos++;
                            //        }
                            //        else
                            //        {
                            //            sb.AppendLine($"DECLARE @{name} dbo.TYPE ");

                            //        }
                            //        sb.Append(PrepareTableParameters(obj, name));
                            //        pPos++;
                            //    }
                            //}
                            //else
                            {

                                BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                                FieldInfo tableField = pValue.GetType().GetField("table", bindFlags);
                                FieldInfo nameField = pValue.GetType().GetField("typeName", bindFlags);

                                DataTable dataTable = tableField.GetValue(pValue);
                                var typeName = nameField.GetValue(pValue);

                                sb.AppendLine($"DECLARE @{name} {typeName} ");
                                sb.Append(PrepareTableParameters(dataTable, name));
                            }
                        }
                        else sb.AppendLine($"DECLARE @{name} NVARCHAR(MAX) = '{pValue.ToString()}'");
                    }

                    if (!string.IsNullOrEmpty(s))
                    {
                        sb.AppendLine(string.Format("EXEC {0} --PROCEDURE", s));
                        if (sb2.Length > 0)
                        {
                            sb.Append(sb2.ToString());
                        }
                    }
                }
            }
            else
            {
                sb.AppendLine("NULL");
            }

            return sb.ToString();
        }

        private static string PrepareTableParameters(DataTable td, string name)
        {
            if (td == null) return "";
            StringBuilder sb = new StringBuilder();
            bool firstRow = true;
            int i = 1;

            foreach (DataRow row in td.Rows)
            {
                if (firstRow)
                {
                    sb.AppendLine($"INSERT INTO @{name} VALUES");
                }
                else
                {
                    sb.Append(",");
                }
                sb.Append('(');

                bool firstCol = true;
                foreach (DataColumn column in td.Columns)
                {
                    if (!firstCol)
                    {
                        sb.Append(',');
                    }
                    object value = row[column.ColumnName];
                    if (value == null)
                    {
                        sb.Append("NULL");
                    }
                    else if (value is decimal || value is double || value is int)
                    {
                        sb.Append($@"{value.ToString().Replace(',', '.')}");
                    }
                    else if (value is DateTime time)
                    {
                        sb.Append("'" + time.ToString("yyyy-MM-dd HH:mm:ss.fff") + "'");
                    }
                    else if (value.GetType().IsGenericType)
                    {
                        var valueType = value.GetType();
                        Type baseType = valueType.GetGenericTypeDefinition();
                        if (baseType == typeof(KeyValuePair<,>))
                        {
                            Type[] argTypes = baseType.GetGenericArguments();

                            object kvpKey = valueType.GetProperty("Key").GetValue(value, null);
                            object kvpValue = valueType.GetProperty("Value").GetValue(value, null);

                            sb.Append($"'{kvpKey}','{kvpValue}'");
                        }
                    }
                    else
                    {
                        sb.Append($@"'{value}'");
                    }
                    firstCol = false;
                }
                sb.Append(')');
                firstRow = false;
                if (++i == 1000)
                {
                    firstRow = true;
                    i = 1;
                    sb.AppendLine();
                }
            }
            sb.AppendLine();
            return sb.ToString();
        }

        //private static string PrepareTableParameters(object t, string name)
        //{
        //    if (t == null) return "";
        //    StringBuilder sb = new StringBuilder();
        //    bool firstRow = true;
        //    int i = 1;
        //    dynamic td = t;
        //    foreach (object y in td)
        //    {
        //        if (firstRow)
        //        {
        //            sb.AppendLine($"insert into @{name} values");
        //        }
        //        else
        //        {
        //            sb.Append(",");
        //        }
        //        sb.Append('(');

        //        bool firstCol = true;
        //        foreach (PropertyInfo prop in y.GetType().GetProperties())
        //        {
        //            if (!firstCol)
        //            {
        //                sb.Append(',');
        //            }
        //            object x = prop.GetValue(y);
        //            if (x == null)
        //            {
        //                sb.Append("NULL");
        //            }
        //            else if (x is decimal || x is double || x is int)
        //            {
        //                sb.Append($@"{x.ToString().Replace(',', '.')}");
        //            }
        //            else if (x is DateTime time)
        //            {
        //                sb.Append("'" + time.ToString("yyyy-MM-dd HH:mm:ss.fff") + "'");
        //            }
        //            else
        //            {
        //                sb.Append($@"'{x}'");
        //            }
        //            firstCol = false;
        //        }
        //        sb.Append(')');
        //        firstRow = false;
        //        if (++i == 1000)
        //        {
        //            firstRow = true;
        //            i = 1;
        //            sb.AppendLine();
        //        }
        //    }
        //    sb.AppendLine();
        //    return sb.ToString();
        //}
    }
}