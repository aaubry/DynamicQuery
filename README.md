# DynamicQuery
An improvement of the original [Dynamic Linq MSDN sample](https://msdn.microsoft.com/en-us/vstudio/bb894665.aspx)

## Main Additions

* Allow to specify the list of types that can be used from within the expression.
* Improve enum handling, so that comparing enums will work.
* Add support for FirstOrDefault() and Contains().
* Allow referencing the parent "it" context using "it_x" syntax.
* Add support for "as" and "is" operators.
