An Expression is anything that transforms data: a method that takes zero or more arguments and returns a result.
The expression types live in `Shoko.Abstractions/Filtering/Expressions/`, and a filter preset stores its tree as JSON
with the type of each node (`FilterExpressionConverter`). Expressions should not have more than 2 arguments of each
type (the `IWith…Parameter` and `IWithSecond…Parameter` containers). If one would need more, it should be redesigned.
For example, `And(HasTag("comedy"),HasTag("action"))`. This keeps expressions simple and their serialized form small.

The argument types an expression can take are listed in `FilterExpressionParameterType`: other expressions, selectors
(date, number, string and string set) and plain values (date, number, string, string set, time span and bool).
Integers are numbers like any other, coerced to double for simplicity.

FilterExpression is a single Expression, whether that's something like "Or" or "HasTag"
in `And(Or(HasTag('comedy'), HasTag('action')), Not(HasTag('18 restricted')))`. Expressions should be the least amount
of work possible. NAND can be expressed as `Not(And())`. Exceptions can be made if it would take more than 2 or 3
operations, such as XOR. An Exception was made for GreaterThanEqual, NotEqual, and LessThanEqual, only because most
people would expect those to exist.

This is all a design for the back end. Expressions can be communicated in many ways. For example, the above could be
written `tag: 'comedy' or tag: 'action' and not tag: '18 restricted'`
or `HasAnyTags('comedy', 'action') && HasNoTags('18 restriced')`. It just depends on how you map the input.

See https://en.m.wikipedia.org/wiki/Binary_expression_tree
