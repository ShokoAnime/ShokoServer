An Expression is anything that transforms data: a method that takes zero or more arguments and returns a result.
Expressions are stored with TPH discriminated on Type. Expressions should not have more than 5 Arguments of each type. If it would, then it
should be redesigned. For example, `And(HasTag("comedy"),HasTag("action"))`. This is to keep the database schema
reasonable and expressions simple. I considered a one-to-many map for arguments, and making that automatic for
navigation properties seemed like a lot of work when we can design around it.

Acceptable database types (can be mapped to other CLR types like enums) for Arguments are:

- String
- Double (integers should be coerced to double for simplicity)
- DateTime

FilterExpression is a single Expression, whether that's something like "Or" or "HasTag"
in `And(Or(HasTag('comedy'), HasTag('action')), Not(HasTag('18 restricted')))`. Expressions should be the least amount
of work possible. NAND can be expressed as `Not(And())`. Exceptions can be made if it would take more than 2 or 3
operations, such as XOR. An Exception was made for GreaterThanEqual, NotEqual, and LessThanEqual, only because most
people would expect those to exist.

This is all a design for the back end. Expressions can be communicated in many ways. For example, the above could be
written `tag: 'comedy' or tag: 'action' and not tag: '18 restricted'`
or `HasAnyTags('comedy', 'action') && HasNoTags('18 restriced')`. It just depends on how you map the input.

See https://en.m.wikipedia.org/wiki/Binary_expression_tree
