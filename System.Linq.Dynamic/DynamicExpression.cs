using System.Collections.Generic;
using System.Linq.Expressions;

namespace System.Linq.Dynamic
{
	public static class DynamicExpression
	{
		public static Expression Parse(Type resultType, string expression, ICollection<Type> additionalAllowedTypes = null, params object[] values)
		{
			ExpressionParser parser = new ExpressionParser(null, expression, values, additionalAllowedTypes);
			return parser.Parse(resultType);
		}

		public static LambdaExpression ParseLambda(Type itType, Type resultType, string expression, ICollection<Type> additionalAllowedTypes = null, params object[] values)
		{
			return ParseLambda(new ParameterExpression[] { Expression.Parameter(itType, "") }, resultType, expression, additionalAllowedTypes, values);
		}

		public static LambdaExpression ParseLambda(ParameterExpression[] parameters, Type resultType, string expression, ICollection<Type> additionalAllowedTypes = null, params object[] values)
		{
			ExpressionParser parser = new ExpressionParser(parameters, expression, values, additionalAllowedTypes);
			return Expression.Lambda(parser.Parse(resultType), parameters);
		}

		public static Expression<Func<T, S>> ParseLambda<T, S>(string expression, ICollection<Type> additionalAllowedTypes = null, params object[] values)
		{
			return (Expression<Func<T, S>>)ParseLambda(typeof(T), typeof(S), expression, additionalAllowedTypes, values);
		}

		public static Type CreateClass(params DynamicProperty[] properties)
		{
			return ClassFactory.Instance.GetDynamicClass(properties);
		}

		public static Type CreateClass(IEnumerable<DynamicProperty> properties)
		{
			return ClassFactory.Instance.GetDynamicClass(properties);
		}
	}
}
