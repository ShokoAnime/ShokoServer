An Expression is anything that transforms data: a method that takes zero or more arguments and returns a result.
The expression types live in `Shoko.Abstractions/Filtering/Expressions/`, and a filter preset stores its tree as JSON
with the type of each node (`FilterExpressionConverter`). Expressions should not have more than 2 arguments of each
type (the `IWith…Parameter` and `IWithSecond…Parameter` containers). If one would need more, it should be redesigned.
For example, `And(HasTag("comedy"),HasTag("action"))`. This keeps expressions simple and their serialized form small.

The argument types an expression can take are listed in `FilterExpressionParameterType`: other expressions, selectors
(date, number, string and string set) and plain values (date, number, string, string set, time span and bool).
Integers are numbers like any other, coerced to double for simplicity.

A comparer (the date, number, string and string set ones in `Logic/`) always takes its left side as a selector in
`Left`. Its right side is either a second selector in `Right` or a plain value in `Parameter`. A `Right` that takes part
wins over `Parameter`, and a missing value from it is never replaced by `Parameter`; a `Right` that does not take part is
left out: `var right = Right is { IsApplicable: true } ? Right.Evaluate(…) : Parameter`.

The logic expressions leave out a side that is missing or does not take part (`IsApplicable` is false for a `Switch`
set to keep everything, unset or empty, and for a logic expression with nothing applicable in it), and let the other side
decide. A filter whose whole expression does not take part is no filter, so everything matches; evaluated directly,
a part that does not take part fails. An expression taking
boolean inputs follows the same rule.

A parameter is unset only when it is `null`: `0`, `""`, `false` and an empty set are all set, and nothing stands in for
a missing value. An expression missing a value it needs does not take part: a condition without its value, a selector
without its source, a date function without its time span. A comparer takes part only when its `Left` does and it has
something to compare it with, a `Right` that takes part or a set `Parameter`; a `Constant` only with a set `Parameter`.
Optional values, such as the label of a `Scope` or a `Switch`, never decide whether a part takes part.

A stored preset keeps every expression in one shape: `$type`, then `Left`, `Right`, `Parameter` and `SecondParameter`
as the container interfaces expose them (a number as a double, a season or role as its name), and for a sorting
expression `Descending` and `Next`. A class's own names for those values, such as `Year` or `IsOn`, are not stored
(`FilterExpressionContractResolver`), so any other public value of an expression has to stand behind a container.

FilterExpression is a single Expression, whether that's something like "Or" or "HasTag"
in `And(Or(HasTag('comedy'), HasTag('action')), Not(HasTag('18 restricted')))`. Expressions should be the least amount
of work possible. NAND can be expressed as `Not(And())`. Exceptions can be made if it would take more than 2 or 3
operations, such as XOR. An Exception was made for GreaterThanEqual, NotEqual, and LessThanEqual, only because most
people would expect those to exist.

This is all a design for the back end. Expressions can be communicated in many ways. For example, the above could be
written `tag: 'comedy' or tag: 'action' and not tag: '18 restricted'`
or `HasAnyTags('comedy', 'action') && HasNoTags('18 restriced')`. It just depends on how you map the input.

See https://en.m.wikipedia.org/wiki/Binary_expression_tree
