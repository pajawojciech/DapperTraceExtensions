using Dapper;
using System;
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

        private class Human
        {
            public string? Name { get; set; }
            public string? Surname { get; set; }
            public DateTime Birthday { get; set; }
        }

        [Fact]
        public void TestListObject()
        {

            List<Human> list = new();
            list.Add(new Human() { Name = "Clayton", Surname = "Guidry", Birthday = Convert.ToDateTime("1971-11-05") });
            list.Add(new Human() { Name = "Michael", Surname = "Young", Birthday = Convert.ToDateTime("1971-05-11") });
            list.Add(new Human() { Name = "Margaret", Surname = "Weeks", Birthday = Convert.ToDateTime("1966-01-27") });
            list.Add(new Human() { Name = "John", Surname = "Jackson", Birthday = Convert.ToDateTime("1952-01-25") });
            list.Add(new Human() { Name = "Kenneth", Surname = "More", Birthday = Convert.ToDateTime("1939-06-28") });

            parameters.Add("@people", list.AsTableParameter("dbo.HumanTableValueType", new List<string> { "Name", "Surname", "Birthday" }));
            parameters.Add("@company", "Optimization Business");
            parameters.Add("@exampleInt", 12345);

            Assert.Equal(
@"DECLARE @people dbo.HumanTableValueType
INSERT INTO @people VALUES
('Clayton','Guidry','1971-11-05 00:00:00.000'),('Michael','Young','1971-05-11 00:00:00.000'),('Margaret','Weeks','1966-01-27 00:00:00.000'),('John','Jackson','1952-01-25 00:00:00.000'),('Kenneth','More','1939-06-28 00:00:00.000')
DECLARE @company NVARCHAR(MAX) = 'Optimization Business'
DECLARE @exampleInt INT = 12345
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