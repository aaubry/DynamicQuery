using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace System.Linq.Dynamic
{
	public static class DynamicQueryable
	{
		public static IQueryable<T> Where<T>(this IQueryable<T> source, string predicate, params object[] values)
		{
			return (IQueryable<T>)Where((IQueryable)source, predicate, values);
		}

		public static IQueryable Where(this IQueryable source, string predicate, params object[] values)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			if (predicate == null)
				throw new ArgumentNullException("predicate");
			LambdaExpression lambda = DynamicExpression.ParseLambda(source.ElementType, typeof(bool), predicate, values: values);
			return source.Provider.CreateQuery(
				Expression.Call(
					typeof(Queryable), "Where",
					new Type[] { source.ElementType },
					source.Expression, Expression.Quote(lambda)));
		}

		public static IQueryable Select(this IQueryable source, string selector, params object[] values)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			if (selector == null)
				throw new ArgumentNullException("selector");
			LambdaExpression lambda = DynamicExpression.ParseLambda(source.ElementType, null, selector, values: values);
			return source.Provider.CreateQuery(
				Expression.Call(
					typeof(Queryable), "Select",
					new Type[] { source.ElementType, lambda.Body.Type },
					source.Expression, Expression.Quote(lambda)));
		}

		public static IQueryable<T> OrderBy<T>(this IQueryable<T> source, string ordering, params object[] values)
		{
			if (source == null)
			{
				throw new ArgumentNullException("source");
			}
			if (ordering == null)
			{
				throw new ArgumentNullException("ordering");
			}
			var parameters = new[]
			{
                Expression.Parameter(source.ElementType, "")
			};

			var parser = new ExpressionParser(parameters, ordering, values);
			var orderings = parser.ParseOrdering();
			
			object result = source;

			var helperMethodName = "OrderByHelper";
			foreach (var order in orderings)
			{
				var keySelectorExpression = Expression.Lambda(
					order.Selector,
					order.Parameter
				);

				var orderByHelperMethod = typeof(DynamicQueryable)
					.GetMethod(helperMethodName, BindingFlags.NonPublic | BindingFlags.Static)
					.MakeGenericMethod(typeof(T), order.Selector.Type);

				result = orderByHelperMethod.Invoke(null, new object[] { result, keySelectorExpression, order.Ascending });

				helperMethodName = "ThenByHelper";
			}

			return (IOrderedQueryable<T>)result;
		}

		private static IOrderedQueryable<T> OrderByHelper<T, TKey>(IQueryable<T> source, Expression<Func<T, TKey>> keySelector, bool ascending)
		{
			return ascending
				? source.OrderBy(keySelector)
				: source.OrderByDescending(keySelector);
		}

		private static IOrderedQueryable<T> ThenByHelper<T, TKey>(IOrderedQueryable<T> source, Expression<Func<T, TKey>> keySelector, bool ascending)
		{
			return ascending
				? source.ThenBy(keySelector)
				: source.ThenByDescending(keySelector);
		}

		public static IQueryable OrderBy(this IQueryable source, string ordering, params object[] values)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			if (ordering == null)
				throw new ArgumentNullException("ordering");
			ParameterExpression[] parameters = new ParameterExpression[] {
                Expression.Parameter(source.ElementType, "") };
			ExpressionParser parser = new ExpressionParser(parameters, ordering, values);
			IEnumerable<DynamicOrdering> orderings = parser.ParseOrdering();
			Expression queryExpr = source.Expression;
			string methodAsc = "OrderBy";
			string methodDesc = "OrderByDescending";
			foreach (DynamicOrdering o in orderings)
			{
				queryExpr = Expression.Call(
					typeof(Queryable), o.Ascending ? methodAsc : methodDesc,
					new Type[] { source.ElementType, o.Selector.Type },
					queryExpr, Expression.Quote(Expression.Lambda(o.Selector, parameters)));
				methodAsc = "ThenBy";
				methodDesc = "ThenByDescending";
			}
			return source.Provider.CreateQuery(queryExpr);
		}

		public static IQueryable Take(this IQueryable source, int count)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			return source.Provider.CreateQuery(
				Expression.Call(
					typeof(Queryable), "Take",
					new Type[] { source.ElementType },
					source.Expression, Expression.Constant(count)));
		}

		public static IQueryable Skip(this IQueryable source, int count)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			return source.Provider.CreateQuery(
				Expression.Call(
					typeof(Queryable), "Skip",
					new Type[] { source.ElementType },
					source.Expression, Expression.Constant(count)));
		}

		public static IQueryable GroupBy(this IQueryable source, string keySelector, string elementSelector, params object[] values)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			if (keySelector == null)
				throw new ArgumentNullException("keySelector");
			if (elementSelector == null)
				throw new ArgumentNullException("elementSelector");
			LambdaExpression keyLambda = DynamicExpression.ParseLambda(source.ElementType, null, keySelector, values: values);
			LambdaExpression elementLambda = DynamicExpression.ParseLambda(source.ElementType, null, elementSelector, values: values);
			return source.Provider.CreateQuery(
				Expression.Call(
					typeof(Queryable), "GroupBy",
					new Type[] { source.ElementType, keyLambda.Body.Type, elementLambda.Body.Type },
					source.Expression, Expression.Quote(keyLambda), Expression.Quote(elementLambda)));
		}

		public static bool Any(this IQueryable source)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			return (bool)source.Provider.Execute(
				Expression.Call(
					typeof(Queryable), "Any",
					new Type[] { source.ElementType }, source.Expression));
		}

		public static int Count(this IQueryable source)
		{
			if (source == null)
				throw new ArgumentNullException("source");
			return (int)source.Provider.Execute(
				Expression.Call(
					typeof(Queryable), "Count",
					new Type[] { source.ElementType }, source.Expression));
		}
	}
}
