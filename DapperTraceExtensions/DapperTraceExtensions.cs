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
#if DEBUG
            Debug.WriteLine("DEBUG");
            Debug.WriteLine(PrepareQuery(t, s, p));
#else
            Trace.WriteLine("RELEASE");
            Trace.WriteLine(PrepareQuery(t, s, p));
#endif
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
                    sb.Append("--SQL\n");
                    foreach (var name in t.ParameterNames)
                    {
                        sb2.AppendFormat("@{0} = @{0},", name);
                        var pValue = t.Get<dynamic>(name);
                        if (pValue == null)
                        {
                            sb.AppendFormat("DECLARE @{0} VARCHAR(MAX) \n", name);
                            continue;
                        }
                        var type = pValue.GetType();
                        if (type == typeof(DateTime))
                        {
                            sb.AppendFormat("DECLARE @{0} DATETIME ='{1}'\n", name, pValue.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                        }
                        else if (type == typeof(bool))
                        {
                            sb.AppendFormat("DECLARE @{0} BIT = {1}\n", name, (bool)pValue ? 1 : 0);
                        }
                        else if (type == typeof(int))
                        {
                            sb.AppendFormat("DECLARE @{0} INT = {1}\n", name, pValue);
                        }
                        else if (type == typeof(List<int>))
                        {
                            sb.AppendFormat("-- REPLACE @{0} IN SQL: ({1})\n", name, string.Join(",", (List<int>)pValue));
                        }
                        else if (type == typeof(decimal) || type == typeof(double))
                        {
                            sb.AppendFormat("DECLARE @{0} DECIMAL({2},{3}) = {1}\n", name, pValue.ToString().Replace(",", "."), pValue.ToString().Length - 1, pValue.ToString().Split(",")[1].Length);
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
                                        sb.AppendFormat("\nDECLARE @{0} {1} \n", name, (pp as Array).GetValue(pPos + 1).ToString());
                                        pPos++;
                                    }
                                    else
                                    {
                                        sb.AppendFormat("\nDECLARE @{0} dbo.TYPE \n", name);

                                    }
                                    sb.Append(PrepareTableParameters(obj, name));
                                    pPos++;
                                }
                            }
                            else
                            {
                                sb.AppendFormat("\nDECLARE @{0} dbo.TYPE \n", name);
                                sb.Append(PrepareTableParameters(pp, name));
                            }
                        }
                        else sb.AppendFormat("DECLARE @{0} NVARCHAR(MAX) = '{1}'\n", name, pValue.ToString());
                    }

                    sb.AppendLine(string.Format("EXEC {0} --PROCEDURE", s));
                    if (sb2.Length > 0)
                    {
                        sb2.Remove(sb2.Length - 1, 1);
                        sb.AppendLine(sb2.ToString());
                    }
                    sb.AppendLine("--END SQL");
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
                    sb.AppendFormat("insert into @{0} values", name);
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