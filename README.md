# DynamicQuery
An improvement of the original [Dynamic Linq MSDN sample](https://msdn.microsoft.com/en-us/vstudio/bb894665.aspx)

## Main Additions

* Allow to specify the list of types that can be used from within the expression.
* Improve enum handling, so that comparing enums will work.
* Add support for FirstOrDefault(), Contains(), OrderBy() and OrderByDescending().
* Allow referencing the parent "it" context using "it_x" syntax.
* Add support for "as" and "is" operators.


# Documentation

Most of the following document comes from the original sample, with a few additions.

## Dynamic Expressions and Queries in LINQ

Database applications frequently rely on “Dynamic SQL”—queries that are constructed at run-time through program logic. The LINQ infrastructure supports similar capabilities through dynamic construction of expression trees using the classes in the System.Linq.Expressions namespace. Expression trees are an appropriate abstraction for a variety of scenarios, but for others a string-based representation may be more convenient. The [Dynamic Expression API](#dynamic-expression-api) extends the core LINQ API with that capability. The API is located in the Dynamic.cs source file and provides

- Dynamic parsing of strings to produce expression trees (the [ParseLambda](#the-parselambda-methods) and [Parse](#the-parse-method) methods),
- Dynamic creation of “Data Classes” (the [CreateClass](#dynamic-data-classes) methods), and
- Dynamic string-based querying through LINQ providers (the [IQueryable extension methods](#iqueryable-extension-methods)).

The Dynamic Expression API relies on a simple [expression language](#expression-language) for formulating expressions and queries in strings.

## Dynamic Expression API

The Dynamic Expression API is brought into scope by using (importing) the `System.Linq.Dynamic` namespace. Below is an example of applying the Dynamic Expression API to a LINQ to SQL data source.

```c#
var query = db.Customers
    .Where("City = @0 and Orders.Count >= @1", "London", 10)
    .OrderBy("CompanyName")
    .Select("new(CompanyName alias Name, Phone)");
```

Note that expressions in the query are strings that could have been dynamically constructed at run-time.

### The ParseLambda Methods

The `System.Linq.Dynamic.DynamicExpression` class defines the following overloaded `ParseLambda` methods for dynamically parsing and creating lambda expressions.

```c#
public static LambdaExpression ParseLambda(  
    ParameterExpression[] parameters, Type resultType,  
    string expression, params object[] values);

public static LambdaExpression ParseLambda(  
    Type argumentType, Type resultType,  
    string expression, params object[] values);

public static Expression<Func<TArgument, TResult>>  
    ParseLambda<TArgument, TResult>(  
    string expression, params object[] values);
```

The first `ParseLambda` overload parses a lambda expression with the given `parameters` and `expression` body and returns an `Expression<Func<…>>` instance representing the result. If the `resultType` parameter is non-null it specifies the required result type for the expression. The `values` parameter supplies zero or more [substitution values](#substitution-values) that may be referenced in the expression.

The example

```c#
ParameterExpression x = Expression.Parameter(typeof(int), "x");
ParameterExpression y = Expression.Parameter(typeof(int), "y");
LambdaExpression e = DynamicExpression.ParseLambda(
    new ParameterExpression[] { x, y }, null, "(x + y) * 2");
```

creates and assigns an `Expression<Func<int, int, int>>` instance to `e` representing the expression `(x + y) * 2`. If a required result type is specified, as in

```c#
LambdaExpression e = DynamicExpression.ParseLambda(
    new ParameterExpression[] { x, y }, typeof(double), "(x + y) * 2");
```

the parsing operation will include an [implicit conversion](#conversions) to the given result type, in this case yielding an `Expression<Func<int, int, double>>` instance.

The second `ParseLambda` overload parses a lambda expression with a single unnamed parameter of a specified `argumentType`. This method corresponds to calling the first ParseLambda overload with a `parameters` argument containing a single `ParameterExpression` with an empty or null `Name` property.

­When parsing a lambda expression with a single unnamed parameter, the members of the unnamed parameter are automatically in scope in the expression string, and the [current instance](#current-instance) given by the unnamed parameter can be referenced in whole using the keyword `it`. The example

```c#
LambdaExpression e = DynamicExpression.ParseLambda(
    typeof(Customer), typeof(bool),
    "City = @0 and Orders.Count >= @1",
    "London", 10);
```

creates and assigns an `Expression<Func<Customer, bool>>` instance to `e`. Note that `City` and `Orders` are members of `Customer` that are automatically in scope. Also note the use of [substitution values](#substitution-values) to supply the constant values `"London"` and `10`.

The third `ParseLambda` overload is a genericly typed version of the second overload. The example below produces the same `Expression<Func<Customer, bool>>` instance as the example above, but is statically typed to that exact type.

```c#
Expression<Func<Customer, bool>> e =
    DynamicExpression.ParseLambda<Customer, bool>(
        "City = @0 and Orders.Count >= @1",
        "London", 10);
```

## The Parse Method

The `System.Linq.Dynamic.DynamicExpression` class defines the following method for parsing and creating expression tree fragments.

```c#
public static Expression Parse(Type resultType, string expression,
    params object[] values);
```

The Parse method parses the given `expression` and returns an expression tree. If the `resultType` parameter is non-null it specifies the required result type of the expression. The `values` parameter supplies zero or more [substitution values](#substitution-values) that may be referenced in the expression.

Unlike the `ParseLambda` methods, the `Parse` method returns an “unbound” expression tree fragment. The following example uses `Parse` to produce the same result as a [previous example](#the-parselambda-methods):

```c#
ParameterExpression x = Expression.Parameter(typeof(int), "x");
ParameterExpression y = Expression.Parameter(typeof(int), "y");
Dictionary<string, object> symbols = new Dictionary<string, object>();
symbols.Add("x", x);
symbols.Add("y", y);
Expression body = DynamicExpression.Parse(null, "(x + y) * 2", symbols);
LambdaExpression e = Expression.Lambda(
    body, new ParameterExpression[] { x, y });
```

Note the use of a `Dictionary<string, object>` to provide a dictionary of named [substitution values](#substitution-values) that can be referenced in the expression.

## Substitution Values

Several methods in the Dynamic Expression API permit _substitution values_ to be specified through a parameter array. Substitution values are referenced in an expression using [identifiers](#identifiers) of the form `@x`, where `x` is an index into the parameter array. The last element of the parameter array may be an object that implements `IDictionary<string, object>`. If so, this dictionary is used to map identifiers to substitution values during parsing.

An identifier that references a substitution value is processed as follows:

- If the value is of type `System.Linq.Expressions.LambdaExpression`, the identifier must occur as part of a [dynamic lambda invocation](#dynamic-lambda-invocation). This allows composition of dynamic lambda expressions.

- Otherwise, if the value is of type `System.Linq.Expressions.Expression`, the given expression is substituted for the identifier.

- Otherwise, the `Expression.Constant`  method is used to create a constant expression from the value which is then substituted for the identifier.

## Dynamic Data Classes

A data class is a class that contains only data members. The `System.Linq.Dynamic.DynamicExpression` class defines the following methods for dynamically creating data classes.

```c#
public static Type CreateClass(params DynamicProperty[] properties);
public static Type CreateClass(IEnumerable<DynamicProperty> properties);
```

The `CreateClass` method creates a new data class with a given set of public properties and returns the `System.Type` object for the newly created class. If a data class with an identical sequence of properties has already been created, the `System.Type` object for this class is returned.

Data classes implement private instance variables and read/write property accessors for the specified properties. Data classes also override the `Equals` and `GetHashCode` members to implement by-value equality.

Data classes are created in an in-memory assembly in the current application domain. All data classes inherit from `System.Linq.Dynamic.DynamicClass` and are given automatically generated names that should be considered private (the names will be unique within the application domain but not across multiple invocations of the application). Note that once created, a data class stays in memory for the lifetime of the current application domain. There is currently no way to unload a dynamically created data class.

The dynamic expression parser uses the `CreateClass` methods to generate classes from [data object initializers](#data-object-initializers). This feature in turn is often used with the dynamic `Select` method to create projections.

The example below uses `CreateClass` to create a data class with two properties, `Name` and `Birthday`, and then uses .NET reflection to create an instance of the class and assign values to the properties.

```c#
DynamicProperty[] props = new DynamicProperty[] {
    new DynamicProperty("Name", typeof(string)),
    new DynamicProperty("Birthday", typeof(DateTime)) };
Type type = DynamicExpression.CreateClass(props);
object obj = Activator.CreateInstance(type);
t.GetProperty("Name").SetValue(obj, "Albert", null);
t.GetProperty("Birthday").SetValue(obj, new DateTime(1879, 3, 14), null);
Console.WriteLine(obj);
```

## IQueryable Extension Methods

The `System.Linq.Dynamic.DynamicQueryable` class implements the following extension methods for dynamically querying objects that implement the `IQueryable<T>` interface.

```c#
public static IQueryable Where(this IQueryable source,
    string predicate, params object[] values);

public static IQueryable<T> Where<T>(this IQueryable<T> source,
    string predicate, params object[] values);

public static IQueryable Select(this IQueryable source,
    string selector, params object[] values);

public static IQueryable OrderBy(this IQueryable source,
    string ordering, params object[] values);

public static IQueryable<T> OrderBy<T>(this IQueryable<T> source,
    string ordering, params object[] values);

public static IQueryable Take(this IQueryable source, int count);

public static IQueryable Skip(this IQueryable source, int count);

public static IQueryable GroupBy(this IQueryable source,
    string keySelector, string elementSelector, params object[] values);

public static bool Any(this IQueryable source);

public static int Count(this IQueryable source);
```

These methods correspond to their `System.Linq.Queryable` counterparts, except that they operate on `IQueryable` instead of `IQueryable<T>` and use strings instead of lambda expressions to express predicates, selectors, and orderings. `IQueryable` is the non-generic base interface for `IQueryable<T>`, so the methods can be used even when `T` isn’t known on beforehand, i.e. when the source of a query is dynamically determined. (Note that because a dynamic predicate or ordering does not affect the result type, generic overloads are provided for `Where` and `OrderBy` in order to preserve strong typing when possible.)

The `predicate`, `selector`, `ordering`, `keySelector`, and `elementSelector` parameters are strings containing expressions written in the [expression language](#expression-language). In the expression strings, the members of the [current instance](#current-instance) are automatically in scope and the instance itself can be referenced using the keyword `it`.

The `OrderBy` method permits a sequence of orderings to be specified, separated by commas. Each ordering may optionally be followed by `asc` or `ascending` to indicate ascending order, or `desc` or `descending` to indicate descending order. The default order is ascending. The example

```c#
products.OrderBy("Category.CategoryName, UnitPrice descending");
```

orders a sequence of products by ascending category name and, within each category, descending unit price.

## The ParseException Class

The Dynamic Expression API reports parsing errors using the `System.Linq.Dynamic.ParseException` class. The `Position` property of the `ParseException` class gives the character index in the expression string at which the parsing error occurred.

# Expression Language

The expression language implemented by the Dynamic Expression API provides a simple and convenient way of writing expressions that can be parsed into LINQ expression trees. The language supports most of the constructs of expression trees, but it is by no means a complete query or programming language. In particular, the expression language does not support statements or declarations.

The expression language is designed to be familiar to C#, VB, and SQL users. For this reason, some operators are present in multiple forms, such as `&&` and `and`.

## Identifiers

An Identifier consists of a letter or underscore followed by any number of letters, digits, or underscores. In order to reference an identifier with the same spelling as a keyword, the identifier must be prefixed with a single @ character. Some examples of identifiers:

```
x   Hello  m_1   @true   @String
```

Identifiers of the from @x, where x is an integral number greater than or equal to zero, are used to denote the [substitution values](#substitution-values), if any, that were passed to the expression parser. For example:

```c#
customers.Where("Country = @0", country);
```

Casing is not significant in identifiers or keywords.

## Literals

The expression language supports integer, real, string, and character literals.

An _integer literal_ consists of a sequence of digits. The type of an integer literal is the first of the types `Int32`, `UInt32`, `Int64`, or `UInt64` that can represent the given value. An integer literal implicitly converts to any other [numeric type](#types) provided the number is in the range of that type. Some examples of integer literals:

```
0   123   10000
```

A _real literal_ consists of an integral part followed by a fractional part and/or an exponent. The integral part is a sequence of one or more digits. The fractional part is a decimal point followed by one or more digits. The exponent is the letter `e` or `E` followed by an optional `+` or `–` sign followed by one or more digits. The type of a real literal is `Double`. A real literal implicitly converts to any other [real type](#types) provided the number is in the range of that type. Some examples of real literals:

```
1.0   2.25   10000.0  1e0   1e10   1.2345E-4
```

A _string literal_ consists of zero or more characters enclosed in double quotes. Inside a string literal, a double quote is written as two consecutive double quotes. The type of a string literal is `String`. Some examples of string literals:

```
"hello"  ""   """quoted"""   "'"
```

A _character literal_ consists of a single character enclosed in single quotes. Inside a character literal, a single quote is written as two consecutive single quotes. The type of a character literal is `Char`. Some examples of character literals:

```
'A'   '1'   ''''   '"'
```

## Constants

The predefined constants `true` and `false` denote the two values of the type `Boolean`.

The predefined constant `null` denotes a null reference. The `null` constant is of type `Object`, but is also implicitly convertible to any reference type.

## Types

The expression language defines the following _primitive types_:

```
Object     Boolean    Char      String      SByte       Byte
Int16      UInt16     Int32     UInt32      Int64       UInt64
Decimal    Single     Double    DateTime    TimeSpan    Guid
```

The primitive types correspond to the similarly named types in the System namespace of the .NET Framework Base Class Library. The expression language also defines a set of _accessible types_ consisting of the primitive types and the following types from the System namespace:

- [Math](https://learn.microsoft.com/en-us/dotnet/api/system.math?view=net-8.0)
- [Convert](https://learn.microsoft.com/en-us/dotnet/api/system.convert?view=net-8.0)

The accessible types are the only types that can be explicitly referenced in expressions, and method invocations in the expression language are restricted to methods declared in the accessible types.

Additional types may be specified through the `additionalAllowedTypes` parameter.

The _nullable_ _form_ of a value type is referenced by writing a `?` after the type name. For example, `Int32?` denotes the nullable form of `Int32`.

The non-nullable and nullable forms of the types `SByte`, `Byte`, `Int16`, `UInt16`, `Int32`, `UInt32`, `Int64`, and `UInt64` are collectively called the _integral types_.

The non-nullable and nullable forms of the types `Single`, `Double`, and `Decimal` are collectively called the _real types_.

The integral types and real types are collectively called the _numeric types_.

## Conversions

The following conversions are implicitly performed by the expression language:

- From the the `null` literal to any reference type or nullable type.

- From an integer literal to an [integral type](#types) or [real type](#types) provided the number is within the range of that type.

- From a real literal to a [real type](#types) provided the number is within the range of that type.

- From a string literal to an enum type provided the string literal contains the name of a member of that enum type.

- From a source type that is assignment compatible with the target type according to the `Type.IsAssignableFrom` method in .NET.

- From a non-nullable value type to the nullable form of that value type.

- From a [numeric type](#types) to another numeric type with greater range.

The expression language permits explicit conversions using the syntax _type_(_expr_), where _type_ is a type name optionally followed by `?` and _expr_ is an expression. This syntax may be used to perform the following conversions:

- Between two types provided `Type.IsAssignableFrom` is true in one or both directions.

- Between two types provided one or both are interface types.

- Between the nullable and non-nullable forms of any value type.

- Between any two types belonging to the set consisting of `SByte`, `Byte`, `Int16`, `UInt16`, `Int32`, `UInt32`, `Int64`, `UInt64`, `Decimal`, `Single`, `Double`, `Char`, any enum type, as well as the nullable forms of those types.

## Operators

The table below shows the operators supported by the expression language in order of precedence from highest to lowest. Operators in the same category have equal precedence. In the table, `x`, `y`, and `z` denote expressions, `T` denotes a [type](#types), and `m` denotes a member.

| Category | Expression | Description |
|-|-|-|
| Primary | `x.m` | Instance field or instance property access. Any public field or property can be accessed. |
|| `x.m(…)` | Instance [method invocation](#method-and-constructor-invocations). The method must be public and must be declared in an [accessible type](#types). |
|| `T.m` | Static field or static property access. Any public field or property can be accessed. |
|| `T.m(…)` | Static [method invocation](#data-object-initializers). The method must be public and must be declared in an [accessible type](#types). |
|| `T(…)` | [Explicit conversion](#conversions) or [constructor invocation](#data-object-initializers). Note that `new` is not required in front of a constructor invocation. |
|| `new(…)` | [Data object initializer](#data-object-initializers). This construct can be used to perform dynamic projections. |
|| `it` | [Current instance](#current-instance). In contexts where members of a current object are implicitly in scope, `it` is used to refer to the entire object itself. |
|| `it_1, it_2, etc.` | [Current instance from a parent context](#current-instance). In contexts where members of a current object are implicitly in scope, `it_x` is used to refer to the entire object itself. |
|| `x(…)` | [Dynamic lambda invocation](#dynamic-lambda-invocation). Used to reference another dynamic lambda expression. |
|| `iif(x, y, z)` | Conditional expression. Alternate syntax for `x ? y : z`. |
| Unary | `-x` | Negation. Supported types are `Int32`, `Int64`, `Decimal`, `Single`, and `Double`. |
|| `!x`, `not x` | Logical negation. Operand must be of type `Boolean`. |
| Multiplicative | `x * y` | Multiplication. Supported types are `Int32`, `UInt32`, `Int64`, `UInt64`, `Decimal`, `Single`, and `Double`. |
|| `x / y` | Division. Supported types are `Int32`, `UInt32`, `Int64`, `UInt64`, `Decimal`, `Single`, and `Double`. \
|| `x % y`, `x mod y` | Remainder. Supported types are `Int32`, `UInt32`, `Int64`, `UInt64`, `Decimal`, `Single`, and `Double`. |
| Additive | `x + y` | Addition or string concatenation. Performs string concatenation if either operand is of type `String`. Otherwise, performs addition for the supported types `Int32`, `UInt32`, `Int64`, `UInt64`, `Decimal`, `Single`, `Double`, `DateTime`, and `TimeSpan`. |
|| `x – y` | Subtraction. Supported types are `Int32`, `UInt32`, `Int64`, `UInt64`, `Decimal`, `Single`, `Double`, `DateTime`, and `TimeSpan`. |
|| `x & y` | String concatenation. Operands may be of any type. |
| Relational and type testing | `x = y`, `x == y` | Equal. Supported for reference types and the [primitive types](#types). Assignment is not supported. |
|| `x != y`, `x <> y` | Not equal. Supported for reference types and the [primitive types](#types). |
|| `x < y` | Less than. Supported for all [primitive types](#types) except `Boolean`, `Object` and `Guid`. |
|| `x > y` | Greater than. Supported for all [primitive types](#types) except `Boolean`, `Object` and `Guid`. |
|| `x <= y` | Less than or equal. Supported for all [primitive types](#types) except `Boolean`, `Object` and `Guid`. |
|| `x >= y` | Greater than or equal. Supported for all [primitive types](#types) except `Boolean`, `Object` and `Guid`. |
|| `x is <typename>` | Checks if an object is compatible with a given type. E.g. `x is System.String` |
|| `x as <typename>` | The as operator is like a cast operation. However, if the conversion isn't possible, as returns null instead of raising an exception. E.g. `(x as System.String).Length` |
| Logical | `x && y`, `x and y` | Logical AND. Operands must be of type `Boolean`. |
|| `x || y`, `x or y` | Logical OR. Operands must be of type `Boolean`. |
| Conditional | `x ? y : z` | Evaluates `y` if `x` is true, evaluates `z` if `x` is false. |

## Method and Constructor Invocations

The expression language limits invocation of methods and constructors to those declared public in the [accessible types](#types). This restriction exists to protect against unintended side effects from invocation of arbitrary methods.

The expression language permits getting (but not setting) the value of any reachable public field, property, or indexer.

Overload resolution for methods, constructors, and indexers uses rules similar to C#. In informal terms, overload resolution will pick the best matching method, constructor, or indexer, or report an ambiguity error if no single best match can be identified.

Note that constructor invocations are not prefixed by `new`. The following example creates a `DateTime` instance for a specfic year, month, and day using a constructor invocation:

```c#
orders.Where("OrderDate >= DateTime(2007, 1, 1)");
```

## Data Object Initializers

A data object initializer creates a [data class](#dynamic-data-classes) and returns an instance of that class. The properties of the data class are inferred from the data object initializer. Specifically, a data object initializer of the form

```
new(e1 alias p1, e2 alias p2, e3 alias p3)
```

creates a data class with three properties, `p1`, `p2`, and `p3`, the types of which are inferred from the expressions `e1`, `e2`, and `e3`, and returns an instance of that data class with the properties initialized to the values computed by `e1`, `e2`, and `e3`. A property initializer may omit the `as` keyword and the property name provided the associated expression is a field or property access. The example

```c#
customers.Select("new(CompanyName alias Name, Phone)");
```

creates a data class with two properties, `Name` and `Phone`, and returns a sequence of instances of that data class initialized from the `CompanyName` and `Phone` properties of each customer.

## Current Instance

When parsing a lambda expression with a single unnamed parameter, the members of the unnamed parameter are automatically in scope in the expression string, and the [_current instance_](#current-instance) given by the unnamed parameter can be referenced in whole using the keyword `it`. For example,

```c#
customers.Where("Country = @0", country);
```

is equivalent to

```c#
customers.Where("it.Country = @0", country);
```

The [IQueryable extension methods](#iqueryable-extension-methods) all parse their expression arguments as lambda expressions with a single unnamed parameter.

It is possible to access the current instance of a parent scope by using the `it_x` syntax, where "x" is a number that indicates how many scopes to walk up. For example, `it_1` refers to the parent scope, and `it_2` refers to the grand-parent scope. `it_0` is equivalent to `it`.

## Dynamic Lambda Invocation

An expression can reference other dynamic lambda expressions through _dynamic lambda invocations_. A dynamic lambda invocation consists of a substitution variable identifier that references an instance of `System.Linq.Expressions.LambdaExpression`, followed by an argument list. The arguments supplied must be compatible with the parameter list of the given dynamic lambda expression.

The following parses two separate dynamic lambda expressions and then combines them in a predicate expression through dynamic lambda invocations:

```c#
Expression<Func<Customer, bool>> e1 = 
    DynamicExpression.ParseLambda<Customer, bool>("City = \"London\"");
Expression<Func<Customer, bool>> e2 =
    DynamicExpression.ParseLambda<Customer, bool>("Orders.Count >= 10");
IQueryable<Customer> query =
    db.Customers.Where("@0(it) and @1(it)", e1, e2);
```

It is of course possible to combine static and dynamic lambda expressions in this fashion:

```c#
Expression<Func<Customer, bool>> e1 =
    c => c.City == "London";
Expression<Func<Customer, bool>> e2 =
    DynamicExpression.ParseLambda<Customer, bool>("Orders.Count >= 10");
IQueryable<Customer> query =
    db.Customers.Where("@0(it) and @1(it)", e1, e2);
```

The examples above both have the same effect as:

```c#
IQueryable<Customer> query =
    db.Customers.Where(c => c.City == "London" && c.Orders.Count >= 10);
```

## operators

A subset of the Standard Query Operators is supported for objects that implement `IEnumerable<T>`. Specifically, the following constructs are permitted, where _seq_ is an `IEnumerable<T>` instance, _predicate_ is a boolean expression, and _selector_ is an expression of any type:

- _seq_ . **Where** ( _predicate_ )
- _seq_ . **Any** ( )
- _seq_ . **Any** ( _predicate_ )
- _seq_ . **All** ( _predicate_ )
- _seq_ . **Count** ( )
- _seq_ . **Count** ( _predicate_ )
- _seq_ . **Min** ( _selector_ )
- _seq_ . **Max** ( _selector_ )
- _seq_ . **Sum** ( _selector_ )
- _seq_ . **Average** ( _selector_ )
- _seq_ . **Contains** ( _value_ )
- _seq_ . **OrderBy** ( _selector_ )
- _seq_ . **OrderByDescending** ( _selector_ )

In the _predicate_ and _selector_ expressions, the members of the [current instance](#current-instance) for that sequence operator are automatically in scope, and the instance itself can be referenced using the keyword `it`. An example:

```c#
customers.Where("Orders.Any(Total >= 1000)");
```

## Enum type support

The expression language supports an [implicit conversion](#conversions) from a string literal to an enum type provided the string literal contains the name of a member of that enum type. For example,

```c#
orders.Where("OrderDate.DayOfWeek = \"Monday\"");
```

is equivalent to

```c#
orders.Where("OrderDate.DayOfWeek = @0", DayOfWeek.Monday);
```
