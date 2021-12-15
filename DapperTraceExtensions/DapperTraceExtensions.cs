using Dapper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                            if (pp is Array)
                            {
                                if ((pp as Array).Length > pPos)
                                {
                                    var obj = (pp as Array).GetValue(pPos);
                                    if ((pp as Array).Length > pPos + 1 && (pp as Array).GetValue(pPos + 1) is string)
                                    {
                                        sb.AppendLine($"DECLARE @{name} {(pp as Array).GetValue(pPos + 1)}");
                                        pPos++;
                                    }
                                    else
                                    {
                                        sb.AppendLine($"DECLARE @{name} dbo.TYPE ");

                                    }
                                    sb.Append(PrepareTableParameters(obj, name));
                                    pPos++;
                                }
                            }
                            else
                            {
                                sb.AppendLine($"DECLARE @{name} dbo.TYPE ");
                                sb.Append(PrepareTableParameters(pp, name));
                            }
                        }
                        else sb.AppendLine($"DECLARE @{name} NVARCHAR(MAX) = '{pValue.ToString()}'");
                    }

                    if(!string.IsNullOrEmpty(s))
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

        private static string PrepareTableParameters(object t, string name)
        {
            if (t == null) return "";
            StringBuilder sb = new StringBuilder();
            bool firstRow = true;
            int i = 1;
            dynamic td = t;
            foreach (object y in td)
            {
                if (firstRow)
                {
                    sb.AppendLine($"insert into @{name} values");
                }
                else
                {
                    sb.Append(",");
                }
                sb.Append('(');

                bool firstCol = true;
                foreach (PropertyInfo prop in y.GetType().GetProperties())
                {
                    if (!firstCol)
                    {
                        sb.Append(',');
                    }
                    object x = prop.GetValue(y);
                    if (x == null)
                    {
                        sb.Append("NULL");
                    }
                    else if (x is decimal || x is double || x is int)
                    {
                        sb.Append($@"{x.ToString().Replace(',', '.')}");
                    }
                    else if (x is DateTime time)
                    {
                        sb.Append("'" + time.ToString("yyyy-MM-dd HH:mm:ss.fff") + "'");
                    }
                    else
                    {
                        sb.Append($@"'{x}'");
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
    }
}