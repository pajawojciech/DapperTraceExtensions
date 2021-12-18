using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Text;

namespace DapperTraceExtensions
{
    internal class DynamicParameter
    {
        public dynamic Parameter;
        private readonly string Name;
        private readonly BindingFlags bindFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        public DynamicParameter(dynamic parameter, string name = null)
        {
            Parameter = parameter;
            Name = name;
        }

        public string GetDeclaration()
        {
            string result = $"DECLARE @{Name} {GetTypeName()}" + (HasValue() ? $" = {GetValue()}" : "");
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
            if (Parameter is null)
            {
                return "NULL";
            }

            if (Parameter is DateTime)
            {
                return $"'{Parameter.ToString("yyyy-MM-dd HH:mm:ss.fff")}'";
            }

            if (Parameter is bool)
            {
                return Parameter ? "1" : "0";
            }

            if (Parameter is int)
            {
                return $"{Parameter}";
            }

            if (Parameter is decimal || Parameter is double)
            {
                return $"{Parameter.ToString().Replace(",", ".")}";
            }

            if (Parameter.GetType().IsGenericType)
            {
                var valueType = Parameter.GetType();
                Type baseType = valueType.GetGenericTypeDefinition();
                if (baseType == typeof(KeyValuePair<,>))
                {
                    object kvpKey = valueType.GetProperty("Key").GetValue(Parameter, null);
                    object kvpValue = valueType.GetProperty("Value").GetValue(Parameter, null);

                    return $"{new DynamicParameter(kvpKey).GetValue()},{new DynamicParameter(kvpValue).GetValue()}";
                }
            }

            return $"'{Parameter.ToString().Replace("'", "''")}'";
        }

        private bool IsTableValuedParameter() => Parameter != null && Parameter.GetType().ToString() == "Dapper.TableValuedParameter";
        private bool HasValue() => Parameter != null && !IsTableValuedParameter();
        private string GetTypeName()
        {
            if (Parameter is DateTime)
            {
                return "DATETIME";
            }

            if (Parameter is bool)
            {
                return "BIT";
            }

            if (Parameter is int)
            {
                return "INT";
            }

            if (Parameter is decimal || Parameter is double)
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
                FieldInfo nameField = Parameter.GetType().GetField("typeName", bindFlags);
                return nameField?.GetValue(Parameter);
            }

            return "NVARCHAR(MAX)";
        }

        private string GetInserts()
        {
            if (!IsTableValuedParameter())
            {
                return null;
            }

            FieldInfo tableField = Parameter.GetType().GetField("table", bindFlags);
            DataTable dataTable = tableField.GetValue(Parameter);

            return PrepareTableParameters(dataTable, Name);
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
                    var parameter = new DynamicParameter(row[column.ColumnName]);
                    sb.Append(parameter.GetValue());

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
