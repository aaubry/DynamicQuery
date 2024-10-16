﻿using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using Xunit;

namespace System.Linq.Dynamic.Tests
{
    public class DynamicExpressionTests
    {
        [Fact]
        public void ParseSimpleExpressionWorks()
        {
            var expression = @"x.Length == 4";

            var expr = (Expression<Func<string, bool>>)DynamicExpression.ParseLambda(new[] { Expression.Parameter(typeof(string), "x") }, typeof(bool), expression);

            Assert.NotNull(expr);

            var values = new[] { "bar", "dog", "food", "water" }.AsQueryable();

            var results = values.Where(expr).ToList();

            Assert.Single(results);
            Assert.Equal("food", results[0]);
        }

        [Fact]
        public void ParseSubQueryExpressionWorks()
        {
            var expression = "x.Any(it == 'a')";

            var expr = (Expression<Func<IEnumerable<char>, bool>>)DynamicExpression.ParseLambda(new[] { Expression.Parameter(typeof(IEnumerable<char>), "x") }, typeof(bool), expression);

            Assert.NotNull(expr);

            var values = new[] { "bar", "dog", "food", "water" }.AsQueryable();

            var results = values.Where(expr).ToList();

            Assert.Equal(2, results.Count);
            Assert.Equal("bar", results[0]);
            Assert.Equal("water", results[1]);
        }

        [Fact]
        public void AccessEnumInExpressionWorks()
        {
            var expression = "it == Int32(MyEnum.Yes)";

            var expr = (Expression<Func<int, bool>>)DynamicExpression.ParseLambda(typeof(int), typeof(bool), expression, additionalAllowedTypes: new[] { typeof(MyEnum) });

            Assert.NotNull(expr);

            var func = expr.Compile();

            Assert.True(func((int)MyEnum.Yes));
            Assert.False(func((int)MyEnum.No));
        }

        [Fact]
        public void AccessibleTypesCanBeSpecifiedOnEachCall()
        {
            DynamicExpression.ParseLambda(typeof(int), typeof(bool), "it != null");
            DynamicExpression.ParseLambda(typeof(int), typeof(bool), "it == Int32(MyEnum.Yes)", additionalAllowedTypes: new[] { typeof(MyEnum) });
        }

        [Fact]
        public void EnumWithoutCastIsConvertedToDestinationType()
        {
            var expression = "it == MyEnum.Yes";

            var expr = (Expression<Func<int, bool>>)DynamicExpression.ParseLambda(typeof(int), typeof(bool), expression, additionalAllowedTypes: new[] { typeof(MyEnum) });

            Console.WriteLine(expr);

            Assert.NotNull(expr);

            var func = expr.Compile();

            Assert.True(func((int)MyEnum.Yes));
            Assert.False(func((int)MyEnum.No));
        }

        [Fact]
        public void EnumWithoutCastIsConvertedFromInt32ToInt64()
        {
            var expression = "it == MyEnum.Yes";

            var expr = (Expression<Func<long, bool>>)DynamicExpression.ParseLambda(typeof(long), typeof(bool), expression, additionalAllowedTypes: new[] { typeof(MyEnum) });

            Console.WriteLine(expr);

            Assert.NotNull(expr);

            var func = expr.Compile();

            Assert.True(func((long)MyEnum.Yes));
            Assert.False(func((long)MyEnum.No));
        }

        [Fact]
        public void CanParseFirstOrDefaultExpression()
        {
            var expression = "FirstOrDefault(it == \"2\")";

            var expr = (Expression<Func<IEnumerable<string>, string>>)DynamicExpression.ParseLambda(typeof(IEnumerable<string>), typeof(string), expression);

            Console.WriteLine(expr);

            Assert.NotNull(expr);

            var func = expr.Compile();

            Assert.Equal("2", func(new[] { "1", "2", "3" }));
            Assert.Null(func(new[] { "4" }));
        }

        [Fact]
        public void CanParseFirstOrDefaultExpressionWithoutParams()
        {
            var expression = "FirstOrDefault()";

            var expr = (Expression<Func<IEnumerable<string>, string>>)DynamicExpression.ParseLambda(typeof(IEnumerable<string>), typeof(string), expression);

            Console.WriteLine(expr);

            Assert.NotNull(expr);

            var func = expr.Compile();

            Assert.Equal("1", func(new[] { "1", "2", "3" }));
            Assert.Null(func(new string[] { }));
        }

        [Fact]
        public void CanParseNestedLambdasWithOuterVariableReference()
        {
            var expression = "resource.Any(allowed.Contains(it_1.Item1))";

            var parameters = new[]
            {
                Expression.Parameter(typeof(Tuple<string>[]), "resource"),
                Expression.Parameter(typeof(string[]), "allowed"),
            };

            var expr = (Expression<Func<Tuple<string>[], string[], bool>>)DynamicExpression.ParseLambda(parameters, typeof(bool), expression);

            Console.WriteLine(expr);

            Assert.NotNull(expr);

            var func = expr.Compile();

            Assert.True(func(new[] { Tuple.Create("1"), Tuple.Create("2") }, new[] { "1", "3" }));
            Assert.False(func(new[] { Tuple.Create("1"), Tuple.Create("2") }, new[] { "3" }));
        }

        [Fact]
        public void CanParseAs()
        {
            var expression = "(resource as System.String).Length";

            var parameters = new[]
            {
                Expression.Parameter(typeof(object), "resource"),
            };

            var expr = (Expression<Func<object, int>>)DynamicExpression.ParseLambda(parameters, typeof(int), expression);

            Console.WriteLine(expr);

            Assert.NotNull(expr);

            var func = expr.Compile();

            Assert.Equal(5, func("hello"));
        }

        [Fact]
        public void CanParseIs()
        {
            var expression = "resource is System.String";

            var parameters = new[]
            {
                Expression.Parameter(typeof(object), "resource"),
            };

            var expr = (Expression<Func<object, bool>>)DynamicExpression.ParseLambda(parameters, typeof(bool), expression);

            Console.WriteLine(expr);

            Assert.NotNull(expr);

            var func = expr.Compile();

            Assert.True(func("hello"));
            Assert.False(func(2));
        }

        [Fact]
        public void CanParseNew()
        {
            var expression = "new(resource.Length alias Len)";

            var parameters = new[]
            {
                Expression.Parameter(typeof(string), "resource"),
            };

            var expr = (Expression<Func<string, object>>)DynamicExpression.ParseLambda(parameters, typeof(object), expression);

            Console.WriteLine(expr);

            Assert.NotNull(expr);

            var func = expr.Compile();
        }

        [Fact]
        public void CanParseIsUsingBuiltInType()
        {
            var expression = "resource is Double";

            var parameters = new[]
            {
                Expression.Parameter(typeof(object), "resource"),
            };

            var expr = (Expression<Func<object, bool>>)DynamicExpression.ParseLambda(parameters, typeof(bool), expression);

            Console.WriteLine(expr);

            Assert.NotNull(expr);

            var func = expr.Compile();

            Assert.True(func(2.2));
        }

        [Fact]
        public void CanInvokeDynamicallyOnObject()
        {
            var expression = "it.works.well";

            var parameters = new[]
            {
                Expression.Parameter(typeof(object), "it"),
            };

            var expr = (Expression<Func<object, object>>)DynamicExpression.ParseLambda(parameters, typeof(object), expression);

            Console.WriteLine(expr);

            Assert.NotNull(expr);

            var func = expr.Compile();

            var obj = new { works = new { well = true, badly = false } };
            Assert.Equal(true, func(obj));
        }

        [Fact]
        public void CanInvokeDynamicallyOnDictionary()
        {
            var expression = "it.works.well";

            var parameters = new[]
            {
                Expression.Parameter(typeof(Dictionary<string, object>), "it"),
            };

            var expr = (Expression<Func<Dictionary<string, object>, object>>)DynamicExpression.ParseLambda(parameters, typeof(object), expression);

            Console.WriteLine(expr);

            Assert.NotNull(expr);

            var func = expr.Compile();

            var obj = new Dictionary<string, object>
            {
                {
                    "works",
                    new Dictionary<string, object>
                    {
                        { "well", true },
                        { "badly", false }
                    }
                }
            };
            Assert.Equal(true, func(obj));
        }

        [Fact]
        public void CanInvokeSelect()
        {
            var expr = DynamicExpression.ParseLambda<IEnumerable<Item>, IEnumerable>("it.Select(new(Id, Name))");
            var func = expr.Compile();

            var res = func(new[]
            {
                new Item { Id = 1, Name = "one", Title = "title one" },
                new Item { Id = 2, Name = "two", Title = "title two" },
            });

            var mappedItems = res.Cast<dynamic>().ToList();
            Assert.Equal(2, mappedItems.Count);

            Assert.Equal(1, mappedItems[0].Id);
            Assert.Equal("one", mappedItems[0].Name);

            Assert.Equal(2, mappedItems[1].Id);
            Assert.Equal("two", mappedItems[1].Name);
        }

        private class Item
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Title { get; set; }
        }

        [Fact]
        public void CanInvokeOrderBy()
        {
            var expr = DynamicExpression.ParseLambda<IEnumerable<Item>, IEnumerable>("it.OrderBy(Name)");
            var func = expr.Compile();

            var res = func(new[]
            {
                new Item { Id = 1, Name = "c", Title = "title one" },
                new Item { Id = 2, Name = "a", Title = "title two" },
                new Item { Id = 3, Name = "b", Title = "title two" },
            });

            var mappedItems = res.Cast<dynamic>().ToList();
            Assert.Equal(3, mappedItems.Count);

            Assert.Equal(2, mappedItems[0].Id);
            Assert.Equal(3, mappedItems[1].Id);
            Assert.Equal(1, mappedItems[2].Id);
        }

        [Fact]
        public void CanInvokeOrderByDescending()
        {
            var expr = DynamicExpression.ParseLambda<IEnumerable<Item>, IEnumerable>("it.OrderByDescending(Name)");
            var func = expr.Compile();

            var res = func(new[]
            {
                new Item { Id = 1, Name = "c", Title = "title one" },
                new Item { Id = 2, Name = "a", Title = "title two" },
                new Item { Id = 3, Name = "b", Title = "title two" },
            });

            var mappedItems = res.Cast<dynamic>().ToList();
            Assert.Equal(3, mappedItems.Count);

            Assert.Equal(1, mappedItems[0].Id);
            Assert.Equal(3, mappedItems[1].Id);
            Assert.Equal(2, mappedItems[2].Id);
        }
    }

    public enum MyEnum
    {
        Yes,
        No,
    }
}