using Dapper;
using System;
using Xunit;

namespace DapperTraceExtensions.Test
{
    public class SingleParameters
    {
        DynamicParameters parameters;
        public SingleParameters()
        {
            parameters = new();
        }

        [Fact]
        public void TestInteger()
        {
            parameters.Add("@min", int.MinValue);
            parameters.Add("@max", int.MaxValue);
            parameters.Add("@zero", 0);

            var result = parameters.GetQuery();
            parameters.WriteQuery();

            Assert.Equal(
@"DECLARE @min INT = -2147483648
DECLARE @max INT = 2147483647
DECLARE @zero INT = 0
", result);
        }

        [Fact]
        public void TestString()
        {
            var value = "String test";
            parameters.Add("@name", value);

            var result = parameters.GetQuery();

            Assert.Equal(
@"DECLARE @name NVARCHAR(MAX) = 'String test'
", result);
        }

        [Fact]
        public void TestStringSpecialChars()
        {
            var value = @"~!@#$%^&*()_+`-={}|[]\:"";'<>?,. /";
            parameters.Add("@specialChars", value);

            var result = parameters.GetQuery();

            Assert.Equal(@"DECLARE @specialChars NVARCHAR(MAX) = '~!@#$%^&*()_+`-={}|[]\:"";''<>?,. /'
", result);
        }

        [Fact]
        public void TestNull()
        {
            parameters.Add("@name", null);

            var result = parameters.GetQuery();

            Assert.Equal(
@"DECLARE @name NVARCHAR(MAX)
", result);
        }

        [Fact]
        public void TestDateTime()
        {
            parameters.Add("@min", Convert.ToDateTime("1753-01-01 00:00:00.000"));
            parameters.Add("@max", Convert.ToDateTime("9999-12-31 23:59:59.997"));
            parameters.Add("@min2", DateTime.MinValue);
            parameters.Add("@max2", DateTime.MaxValue);

            var result = parameters.GetQuery();

            Assert.Equal(
@"DECLARE @min DATETIME = '1753-01-01 00:00:00.000'
DECLARE @max DATETIME = '9999-12-31 23:59:59.997'
DECLARE @min2 DATETIME2 = '0001-01-01 00:00:00.000'
DECLARE @max2 DATETIME2 = '9999-12-31 23:59:59.999'
", result);
        }

        [Fact]
        public void TestBool()
        {
            parameters.Add("@true", true);
            parameters.Add("@false", false);

            var result = parameters.GetQuery();

            Assert.Equal(
@"DECLARE @true BIT = 1
DECLARE @false BIT = 0
", result);
        }

        [Fact]
        public void TestDecimal()
        {
            parameters.Add("@min", decimal.MinValue);
            parameters.Add("@max", decimal.MaxValue);
            parameters.Add("@positive", 12.3456M);
            parameters.Add("@negative", -12.3456M);

            var result = parameters.GetQuery();

            Assert.Equal(
@"DECLARE @min DECIMAL(29,0) = -79228162514264337593543950335
DECLARE @max DECIMAL(29,0) = 79228162514264337593543950335
DECLARE @positive DECIMAL(6,4) = 12.3456
DECLARE @negative DECIMAL(6,4) = -12.3456
", result);
        }

        [Fact]
        public void TestDouble()
        {
            parameters.Add("@min", double.MinValue);
            parameters.Add("@max", double.MaxValue);
            parameters.Add("@positive", 12.3456D);
            parameters.Add("@negative", -12.345678901234567890123456789D);
            parameters.Add("@positiveF", 12.3456F);
            parameters.Add("@negativeF", -12.3456F);

            var result = parameters.GetQuery();

            Assert.Equal(
@"DECLARE @min FLOAT = '-1.7976931348623157E+308'
DECLARE @max FLOAT = '1.7976931348623157E+308'
DECLARE @positive FLOAT = '12.3456'
DECLARE @negative FLOAT = '-12.345678901234567'
DECLARE @positiveF FLOAT = '12.3456'
DECLARE @negativeF FLOAT = '-12.3456'
", result);
        }

        [Fact]
        public void TestEmpty()
        {
            var result = parameters.GetQuery();

            Assert.Equal(
@"", result);
        }
    }
}
