using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Text;

namespace DapperTraceExtensions
{
    internal class DynamicParameter
    {
        private readonly dynamic parameter;
        private readonly string name;
        private readonly BindingFlags bindFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        public DynamicParameter(dynamic parameter, string name = null)
        {
            this.parameter = parameter;
            this.name = name;
        }

        public string GetDeclaration()
        {
            var result = $"DECLARE @{name} {GetTypeName()}" + (HasValue() ? $" = {GetValue()}" : "");
            if (IsTableValuedParameter())
            {
                var inserts = GetInserts();
                if(!string.IsNullOrEmpty(inserts))
                {
                    result += Environment.NewLine + inserts;
                }
            }
            return result;
        }

        public string GetValue()
        {
            if (parameter is null)
            {
                return "NULL";
            }

            if (parameter is DateTime)
            {
                return $"'{parameter.ToString("yyyy-MM-dd HH:mm:ss.fff")}'";
            }

            if (parameter is bool)
            {
                return parameter ? "1" : "0";
            }

            if (parameter is int)
            {
                return $"{parameter}";
            }

            if (parameter is decimal || parameter is double)
            {
                return $"{parameter.ToString().Replace(",", ".")}";
            }

            if (parameter.GetType().IsGenericType)
            {
                var valueType = parameter.GetType();
                Type baseType = valueType.GetGenericTypeDefinition();
                if (baseType == typeof(KeyValuePair<,>))
                {
                    object kvpKey = valueType.GetProperty("Key").GetValue(parameter, null);
                    object kvpValue = valueType.GetProperty("Value").GetValue(parameter, null);

                    return $"{new DynamicParameter(kvpKey).GetValue()},{new DynamicParameter(kvpValue).GetValue()}";
                }
            }

            return $"'{parameter.ToString().Replace("'", "''")}'";
        }

        private bool IsTableValuedParameter() => parameter != null && parameter.GetType().ToString() == "Dapper.TableValuedParameter";
        private bool HasValue() => parameter != null && !IsTableValuedParameter();
        private string GetTypeName()
        {
            if (parameter is DateTime)
            {
                return "DATETIME";
            }

            if (parameter is bool)
            {
                return "BIT";
            }

            if (parameter is int)
            {
                return "INT";
            }

            if (parameter is decimal || parameter is double)
            {
                var precision = GetValue().Length - 1;
                var scale = 0;

                var split = GetValue().Split('.');
                if (split.Length > 1)
                {
                    scale = split[1].Length;
                }
                return $"DECIMAL({precision},{scale})";
            }

            if (IsTableValuedParameter())
            {
                FieldInfo nameField = parameter.GetType().GetField("typeName", bindFlags);
                return nameField?.GetValue(parameter);
            }

            return "NVARCHAR(MAX)";
        }

        private string GetInserts()
        {
            if (!IsTableValuedParameter())
            {
                return null;
            }

            FieldInfo tableField = parameter.GetType().GetField("table", bindFlags);
            DataTable dataTable = tableField.GetValue(parameter);

            return PrepareTableParameters(dataTable, name);
        }

        private static string PrepareTableParameters(DataTable table, string name)
        {
            if (table == null)
            {
                return "";
            }

            var sb = new StringBuilder();
            var firstRow = true;
            var i = 1;

            foreach (DataRow row in table.Rows)
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

                var firstCol = true;
                foreach (DataColumn column in table.Columns)
                {
                    if (!firstCol)
                    {
                        sb.Append(',');
                    }
                    var param = new DynamicParameter(row[column.ColumnName]);
                    sb.Append(param.GetValue());

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
            return sb.ToString();
        }
    }
}
