using Dapper;
using System.Collections.Generic;
using Xunit;

namespace DapperTraceExtensions.Test
{
    public class TableValuedParameters
    {
        DynamicParameters parameters;
        public TableValuedParameters()
        {
            parameters = new();
        }


        private class TestListObjectClass
        {
            public string? StringParam { get; set; }
            public int IntegerParam { get; set; }
        }

        [Fact]
        public void TestListObject()
        {
            List<TestListObjectClass> list = new();
            list.Add(new TestListObjectClass() { StringParam = "string test", IntegerParam = 12345 });
            list.Add(new TestListObjectClass() { StringParam = "string test2", IntegerParam = 23456 });
            parameters.Add("@parameter", list.AsTableParameter("dbo.typeName", new List<string> { "StringParam", "IntegerParam" }));

            Assert.Equal(
@"DECLARE @parameter dbo.typeName 
INSERT INTO @parameter VALUES
('string test',12345),('string test2',23456)
", parameters.GetQuery());

        }

        [Fact]
        public void TestListString()
        {
            List<string> list = new() { "a", "b", "c" };
            parameters.Add("@parameter", list.AsTableParameter("dbo.typeName"));

            Assert.Equal(
@"DECLARE @parameter dbo.typeName 
INSERT INTO @parameter VALUES
('a'),('b'),('c')
", parameters.GetQuery());
        }

        [Fact]
        public void TestListInt()
        {
            List<int> list = new() { 1, 2, 3, 4, 5 };
            parameters.Add("@parameter", list.AsTableParameter("dbo.typeName"));

            Assert.Equal(
@"DECLARE @parameter dbo.typeName 
INSERT INTO @parameter VALUES
(1),(2),(3),(4),(5)
", parameters.GetQuery());
        }

        [Fact]
        public void TestDictionary()
        {
            Dictionary<string, string> list = new()
            {
                { "a", "1" },
                { "b", "2" }
            };
            parameters.Add("@parameter", list.AsTableParameter("dbo.typeName"));

            Assert.Equal(
@"DECLARE @parameter dbo.typeName 
INSERT INTO @parameter VALUES
('a','1'),('b','2')
", parameters.GetQuery());
        }

        [Fact(Skip = "todo")]
        public void TestDictionaryInt()
        {
            Dictionary<string, int> list = new()
            {
                { "a", 1 },
                { "b", 2 }
            };
            parameters.Add("@parameter", list.AsTableParameter("dbo.typeName"));

            Assert.Equal(
@"DECLARE @parameter dbo.typeName 
INSERT INTO @parameter VALUES
('a',1),('b',2)
", parameters.GetQuery());
        }
    }

}