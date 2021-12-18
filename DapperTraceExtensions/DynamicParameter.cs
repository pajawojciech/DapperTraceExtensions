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

        public DynamicParameter(dynamic parameter, string name)
        {
            Parameter = parameter;
            Name = name;
        }

        public string GetDeclaration()
        {
            string result = $"DECLARE @{Name} {GetTypeName()}" + (HasValue() ? $" = {GetValue()}" : "");
            if (IsTableValuedParameter())
            {
                result += Environment.NewLine + GetTableValues();
            }
            return result;
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

            if(IsTableValuedParameter())
            {
                FieldInfo nameField = Parameter.GetType().GetField("typeName", bindFlags);
                return nameField?.GetValue(Parameter);
            }

            return "NVARCHAR(MAX)";
        }

        private string GetValue()
        {
            if (Parameter == null)
            {
                return "NULL";
            }

            if (Parameter is DateTime)
            {
                return $"'{Parameter.ToString("yyyy-MM-dd HH:mm:ss.fff")}'";
            }

            if (Parameter is bool value)
            {
                return value ? "1" : "0";
            }

            if (Parameter is int)
            {
                return $"{Parameter}";
            }

            if (Parameter is decimal || Parameter is double)
            {
                return $"{Parameter.ToString().Replace(",", ".")}";
            }

            return $"'{Parameter.ToString().Replace("'", "''")}'";
        }

        private string GetTableValues()
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
                        sb.Append($@"'{value.ToString().Replace("'", "''")}'");
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
            return sb.ToString();
        }
    }
}
