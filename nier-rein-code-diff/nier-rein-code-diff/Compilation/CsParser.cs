using System;
using System.Collections.Generic;

namespace nier_rein_code_diff.Compilation
{
    class CsParser : IDisposable
    {
        private readonly NodeReader _reader;

        public CsParser(NodeReader reader)
        {
            _reader = reader;
        }

        public SyntaxNode Parse()
        {
            var result = new SyntaxNode(SyntaxNodeKind.CompilationUnit);
            foreach (var node in ParseInternal())
                result.Nodes.Add(node);

            return result;
        }

        private IEnumerable<SyntaxNode> ParseInternal()
        {
            while (_reader.Peek().Kind != SyntaxNodeKind.EndOfFile)
            {
                switch (_reader.Peek().Kind)
                {
                    case SyntaxNodeKind.Identifier:
                        var text = _reader.Peek().Text;
                        if (IsClassKeyword(text))
                        {
                            yield return ResolveClass();
                            continue;
                        }

                        switch (text)
                        {
                            case "using":
                                yield return ResolveUsingStatement();
                                continue;

                            case "namespace":
                                yield return ResolveNamespace();
                                continue;

                            default:
                                throw new InvalidOperationException("Unknown code block");
                        }

                        //case SyntaxNodeKind.MultiLineComment:
                        //    yield return ResolveMultiLineComment(reader);
                        //    continue;
                }

                yield return _reader.Read();
            }

            yield return _reader.Read();
        }

        private SyntaxNode ResolveUsingStatement()
        {
            var result = new SyntaxNode(SyntaxNodeKind.UsingStatement);

            result.Nodes.Add(_reader.Read());
            result.Nodes.Add(ResolveFqdn());
            result.Nodes.Add(_reader.Read());

            return result;
        }

        private SyntaxNode ResolveNamespace()
        {
            var result = new SyntaxNode(SyntaxNodeKind.NamespaceUnit);

            result.Nodes.Add(ResolveNamespaceStatement());

            if (_reader.Peek().Kind != SyntaxNodeKind.OpenBrace)
                throw new InvalidOperationException("No namespace unit open brace.");
            result.Nodes.Add(_reader.Read());

            var peekedNode = _reader.Peek();
            while (peekedNode.Kind != SyntaxNodeKind.CloseBrace)
            {
                if (peekedNode.Kind == SyntaxNodeKind.DoubleSlash)
                    result.Nodes.Add(ResolveSingleLineComment());
                else if (peekedNode.Kind == SyntaxNodeKind.OpenBracket)
                    result.Nodes.Add(ResolveAttribute());
                else if (IsClassKeyword(peekedNode.Text))
                    result.Nodes.Add(ResolveClass());

                peekedNode = _reader.Peek();
            }

            result.Nodes.Add(_reader.Read());

            return result;
        }

        public SyntaxNode ResolveClass()
        {
            var result = new SyntaxNode(SyntaxNodeKind.Class);

            // Read class keywords
            var classStatement = new SyntaxNode(SyntaxNodeKind.ClassStatement);
            var peekedNode = _reader.Peek();
            while (peekedNode.Text != "class")
            {
                switch (peekedNode.Text)
                {
                    case "public":
                    case "internal":
                    case "private":
                        classStatement.Nodes.Add(_reader.Read().Clone(SyntaxNodeKind.Accessor));
                        break;

                    case "abstract":
                        classStatement.Nodes.Add(_reader.Read().Clone(SyntaxNodeKind.Abstract));
                        break;

                    case "sealed":
                        classStatement.Nodes.Add(_reader.Read().Clone(SyntaxNodeKind.Sealed));
                        break;

                    case "static":
                        classStatement.Nodes.Add(_reader.Read().Clone(SyntaxNodeKind.Static));
                        break;

                    default:
                        throw new InvalidOperationException("Unknown class identifier.");
                }

                peekedNode = _reader.Peek();
            }

            classStatement.Nodes.Add(_reader.Read());

            // Read class name
            if (_reader.Peek().Kind != SyntaxNodeKind.Identifier)
                throw new InvalidOperationException("No class name given.");

            var classIdentifierNode = _reader.Read();
            classStatement.Nodes.Add(classIdentifierNode);

            // Read inherited class
            if (_reader.Peek().Kind == SyntaxNodeKind.Colon)
            {
                var inheritance = new SyntaxNode(SyntaxNodeKind.ClassInheritance);
                inheritance.Nodes.Add(_reader.Read());
                inheritance.Nodes.Add(ResolveType());

                //if (_reader.Peek().Kind != SyntaxNodeKind.Identifier)
                //    throw new InvalidOperationException("Invalid inheritance identifier.");
                //inheritance.Nodes.Add(_reader.Read());

                classStatement.Nodes.Add(inheritance);
            }

            // Read optional single-line comment
            result.Nodes.Add(classStatement);
            if (_reader.Peek().Kind == SyntaxNodeKind.DoubleSlash)
                result.Nodes.Add(ResolveSingleLineComment());

            if (_reader.Peek().Kind != SyntaxNodeKind.OpenBrace)
                throw new InvalidOperationException("No valid class block.");

            // Read class block
            var classBlock = new SyntaxNode(SyntaxNodeKind.ClassBlock);
            classBlock.Nodes.Add(_reader.Read());

            while (_reader.Peek().Kind != SyntaxNodeKind.CloseBrace)
            {
                if (_reader.Peek().Kind == SyntaxNodeKind.DoubleSlash)
                    classBlock.Nodes.Add(ResolveSingleLineComment());
                else if (_reader.Peek().Kind == SyntaxNodeKind.OpenBracket)
                    classBlock.Nodes.Add(ResolveAttribute());
                else
                    classBlock.Nodes.Add(ResolveClassMember(classIdentifierNode.Text));
            }

            classBlock.Nodes.Add(_reader.Read());
            result.Nodes.Add(classBlock);

            return result;
        }

        private SyntaxNode ResolveAttribute()
        {
            var result = new SyntaxNode(SyntaxNodeKind.Attribute);

            // Read open bracket
            result.Nodes.Add(_reader.Read());

            // Read attribute FQDN
            result.Nodes.Add(ResolveFqdn());

            // Read ctor
            if (_reader.Peek().Kind == SyntaxNodeKind.OpenParen)
            {
                var ctorNode = new SyntaxNode(SyntaxNodeKind.AttributeCtor);
                ctorNode.Nodes.Add(_reader.Read());

                while (_reader.Peek().Kind != SyntaxNodeKind.CloseParen)
                {
                    // Read parameter value
                    ctorNode.Nodes.Add(_reader.Read());

                    if (_reader.Peek().Kind != SyntaxNodeKind.Comma && _reader.Peek().Kind != SyntaxNodeKind.CloseParen)
                        throw new InvalidOperationException("Invalid attribute ctor invocation parameter.");
                    if (_reader.Peek().Kind == SyntaxNodeKind.Comma)
                        ctorNode.Nodes.Add(_reader.Read());
                }

                ctorNode.Nodes.Add(_reader.Read());
                result.Nodes.Add(ctorNode);
            }

            // Read closing bracket
            if (_reader.Peek().Kind != SyntaxNodeKind.CloseBracket)
                throw new InvalidOperationException("Invalid attribute closing bracket.");

            result.Nodes.Add(_reader.Read());

            return result;
        }

        private SyntaxNode ResolveClassMember(string className)
        {
            var list = new List<SyntaxNode>();

            // Read optional accessor
            var node = _reader.Peek();
            if (node.Kind == SyntaxNodeKind.Identifier && IsAccessor(node.Text))
                list.Add(_reader.Read().Clone(SyntaxNodeKind.Accessor));

            // Read optional override
            if (_reader.Peek().Kind == SyntaxNodeKind.Identifier &&
                (_reader.Peek().Text == "override" || _reader.Peek().Text == "static" || _reader.Peek().Text == "readonly"))
            {
                switch (_reader.Peek().Text)
                {
                    case "override":
                        list.Add(_reader.Read().Clone(SyntaxNodeKind.Override));
                        break;

                    case "static":
                        list.Add(_reader.Read().Clone(SyntaxNodeKind.Static));
                        break;

                    case "readonly":
                        list.Add(_reader.Read().Clone(SyntaxNodeKind.ReadOnly));
                        break;
                }
            }

            // Detect ctor syntax
            var isDecompiledCtor = _reader.Peek(1).Kind == SyntaxNodeKind.Dot && _reader.Peek(2).Kind == SyntaxNodeKind.Identifier && _reader.Peek(2).Text == "ctor";
            var isSourceCtor = _reader.Peek().Kind == SyntaxNodeKind.Identifier && _reader.Peek().Text == className;
            var isCtor = isSourceCtor || isDecompiledCtor;

            // Read type (can be member type or method return type)
            if (!isSourceCtor)
                list.Add(ResolveType());

            // Read member name (CUSTOM for dump.cs; <> is valid; .ctor is valid)
            var memberNameNode = new SyntaxNode(SyntaxNodeKind.MemberName);
            if (isDecompiledCtor)
            {
                var firstNode = _reader.Read();
                var secondNode = _reader.Read();
                memberNameNode.Nodes.Add(new SyntaxNode(SyntaxNodeKind.Identifier) { Trail = firstNode.Trail, Lead = secondNode.Lead, Text = firstNode.Text + secondNode.Text });
            }
            else
            {
                while (_reader.Peek().Kind == SyntaxNodeKind.Identifier ||
                       _reader.Peek().Kind == SyntaxNodeKind.SmallerThan || _reader.Peek().Kind == SyntaxNodeKind.GreaterThan)
                    memberNameNode.Nodes.Add(_reader.Read());
            }

            list.Add(memberNameNode);

            // Determine member type and body
            SyntaxNode result;
            switch (_reader.Peek().Kind)
            {
                case SyntaxNodeKind.SemiColon:
                    result = new SyntaxNode(SyntaxNodeKind.Field);
                    result.Nodes.Add(_reader.Read());
                    break;

                case SyntaxNodeKind.OpenBrace:
                    result = new SyntaxNode(SyntaxNodeKind.Property);
                    result.Nodes.Add(ResolvePropertyBody());
                    break;

                case SyntaxNodeKind.OpenParen:
                    result = isCtor ? new SyntaxNode(SyntaxNodeKind.Ctor) : new SyntaxNode(SyntaxNodeKind.Method);
                    result.Nodes.Add(ResolveMethodBody());
                    break;

                default:
                    throw new InvalidOperationException("Unknown member body.");
            }

            list.Reverse();
            foreach (var n in list)
                result.Nodes.Insert(0, n);

            return result;
        }

        private SyntaxNode ResolvePropertyBody()
        {
            var result = new SyntaxNode(SyntaxNodeKind.PropertyBody);
            result.Nodes.Add(_reader.Read());

            // TODO: Better parsing
            while (_reader.Peek().Kind != SyntaxNodeKind.CloseBrace)
                result.Nodes.Add(_reader.Read());
            result.Nodes.Add(_reader.Read());

            return result;
        }

        private SyntaxNode ResolveMethodBody()
        {
            var result = new SyntaxNode(SyntaxNodeKind.MethodBody);
            result.Nodes.Add(_reader.Read());

            // Read parameters
            while (_reader.Peek().Kind != SyntaxNodeKind.CloseParen)
            {
                result.Nodes.Add(ResolveTypedParameter());

                if (_reader.Peek().Kind != SyntaxNodeKind.Comma && _reader.Peek().Kind != SyntaxNodeKind.CloseParen)
                    throw new InvalidOperationException("Invalid parameter.");
                if (_reader.Peek().Kind == SyntaxNodeKind.Comma)
                    result.Nodes.Add(_reader.Read());
            }
            result.Nodes.Add(_reader.Read());

            // Read optional inherited operator
            if (_reader.Peek().Kind == SyntaxNodeKind.Colon)
            {
                var invok = new SyntaxNode(SyntaxNodeKind.MethodCtorInvocation);
                invok.Nodes.Add(_reader.Read());

                if (_reader.Peek().Kind != SyntaxNodeKind.Identifier ||
                   _reader.Peek().Text != "base" && _reader.Peek().Text != "this")
                    throw new InvalidOperationException("Invalid ctor invocation target.");
                invok.Nodes.Add(_reader.Read());

                if (_reader.Peek().Kind != SyntaxNodeKind.OpenParen)
                    throw new InvalidOperationException("Invalid ctor invocation statement.");

                // Read parameters
                invok.Nodes.Add(_reader.Read());
                while (_reader.Peek().Kind != SyntaxNodeKind.CloseParen)
                {
                    if (_reader.Peek().Kind != SyntaxNodeKind.Identifier)
                        throw new InvalidOperationException("Invalid ctor invocation parameter.");
                    invok.Nodes.Add(_reader.Read());

                    if (_reader.Peek().Kind != SyntaxNodeKind.Comma && _reader.Peek().Kind != SyntaxNodeKind.CloseParen)
                        throw new InvalidOperationException("Invalid ctor invocation parameter.");
                    if (_reader.Peek().Kind == SyntaxNodeKind.Comma)
                        invok.Nodes.Add(_reader.Read());
                }
                invok.Nodes.Add(_reader.Read());

                result.Nodes.Add(invok);
            }

            // Read braces and body content
            if (_reader.Peek().Kind != SyntaxNodeKind.OpenBrace)
                throw new InvalidOperationException("Invalid method body.");

            while (_reader.Peek().Kind != SyntaxNodeKind.CloseBrace)
                result.Nodes.Add(_reader.Read());
            result.Nodes.Add(_reader.Read());

            return result;
        }

        private SyntaxNode ResolveTypedParameter()
        {
            var result = new SyntaxNode(SyntaxNodeKind.Parameter);

            // Resolve optional out keyword
            if (_reader.Peek().Kind == SyntaxNodeKind.Identifier && _reader.Peek().Text == "out")
                result.Nodes.Add(_reader.Read().Clone(SyntaxNodeKind.Out));

            // Resolve parameter type
            result.Nodes.Add(ResolveType());

            // Read parameter name
            if (_reader.Peek().Kind != SyntaxNodeKind.Identifier)
                throw new InvalidOperationException("No parameter name given.");
            result.Nodes.Add(_reader.Read());

            // Read optional value
            if (_reader.Peek().Kind == SyntaxNodeKind.Equals)
            {
                var valueNode = new SyntaxNode(SyntaxNodeKind.ParameterValue);
                valueNode.Nodes.Add(_reader.Read());

                if (_reader.Peek().Kind != SyntaxNodeKind.StringLiteral && _reader.Peek().Kind != SyntaxNodeKind.NumericLiteral && _reader.Peek().Kind != SyntaxNodeKind.Identifier)
                    throw new InvalidOperationException("Invalid parameter value.");
                valueNode.Nodes.Add(_reader.Read());

                result.Nodes.Add(valueNode);
            }

            return result;
        }

        private SyntaxNode ResolveType()
        {
            var result = new SyntaxNode(SyntaxNodeKind.Type);

            if (_reader.Peek().Kind == SyntaxNodeKind.OpenParen)
            {
                var tupleNode = new SyntaxNode(SyntaxNodeKind.TupleType);
                tupleNode.Nodes.Add(_reader.Read());

                while (_reader.Peek().Kind != SyntaxNodeKind.CloseParen)
                {
                    tupleNode.Nodes.Add(ResolveType());

                    if (_reader.Peek().Kind != SyntaxNodeKind.Comma && _reader.Peek().Kind != SyntaxNodeKind.CloseParen)
                        throw new InvalidOperationException("Invalid tuple type.");
                    if (_reader.Peek().Kind == SyntaxNodeKind.Comma)
                        tupleNode.Nodes.Add(_reader.Read());
                }

                tupleNode.Nodes.Add(_reader.Read());

                result.Nodes.Add(tupleNode);
            }
            else
            {
                if (_reader.Peek().Kind == SyntaxNodeKind.Identifier && _reader.Peek().Text == "void")
                {
                    result.Nodes.Add(new SyntaxNode(SyntaxNodeKind.Fqdn) { Nodes = { _reader.Read() } });
                    return result;
                }

                // Read type FQDN
                result.Nodes.Add(ResolveFqdn());

                // Read optional generic type
                if (_reader.Peek().Kind == SyntaxNodeKind.SmallerThan)
                {
                    var genericNode = new SyntaxNode(SyntaxNodeKind.GenericTypes);
                    genericNode.Nodes.Add(_reader.Read());

                    while (_reader.Peek().Kind != SyntaxNodeKind.GreaterThan)
                    {
                        genericNode.Nodes.Add(ResolveType());

                        if (_reader.Peek().Kind != SyntaxNodeKind.Comma && _reader.Peek().Kind != SyntaxNodeKind.GreaterThan)
                            throw new InvalidOperationException("Invalid generic type.");
                        if (_reader.Peek().Kind == SyntaxNodeKind.Comma)
                            genericNode.Nodes.Add(_reader.Read());
                    }

                    genericNode.Nodes.Add(_reader.Read());

                    result.Nodes.Add(genericNode);
                }
            }

            // Read optional array indicator
            if (_reader.Peek().Kind == SyntaxNodeKind.OpenBracket)
            {
                if (_reader.Peek(1).Kind != SyntaxNodeKind.CloseBracket)
                    throw new InvalidOperationException("Invalid type array indicator.");

                var arrayType = new SyntaxNode(SyntaxNodeKind.ArrayType);
                arrayType.Nodes.Add(_reader.Read());
                arrayType.Nodes.Add(_reader.Read());

                result.Nodes.Add(arrayType);
            }

            return result;
        }

        private SyntaxNode ResolveSingleLineComment()
        {
            var result = new SyntaxNode(SyntaxNodeKind.Comment);

            result.Nodes.Add(_reader.Read());

            var commentTextNode = new SyntaxNode(SyntaxNodeKind.CommentText);

            var peekedNode = _reader.Peek();
            while (!peekedNode.HasTrail(SyntaxNodeKind.NewLine))
            {
                commentTextNode.Text += _reader.Read();

                peekedNode = _reader.Peek();
            }

            peekedNode = _reader.Read();
            commentTextNode.Text += peekedNode.Text;
            commentTextNode.Trail = peekedNode.Trail;

            result.Nodes.Add(commentTextNode);

            return result;
        }

        private SyntaxNode ResolveNamespaceStatement()
        {
            var result = new SyntaxNode(SyntaxNodeKind.NamespaceStatement);

            result.Nodes.Add(_reader.Read());
            result.Nodes.Add(ResolveFqdn());

            return result;
        }

        private SyntaxNode ResolveFqdn()
        {
            var result = new SyntaxNode(SyntaxNodeKind.Fqdn);

            var peekedNode = _reader.Peek();
            var lastKind = -1;
            while (peekedNode.Kind == SyntaxNodeKind.Identifier || peekedNode.Kind == SyntaxNodeKind.Dot)
            {
                if (lastKind == (int)peekedNode.Kind)
                    break;

                result.Nodes.Add(_reader.Read());
                lastKind = (int)peekedNode.Kind;

                peekedNode = _reader.Peek();
            }

            return result;
        }

        private bool IsClassKeyword(string identifier)
        {
            return IsClassAccessor(identifier) ||
                   identifier == "sealed" || identifier == "abstract" || identifier == "static" ||
                   identifier == "class";
        }

        private bool IsClassAccessor(string identifier)
        {
            return identifier == "public" || identifier == "internal" || identifier == "private";
        }

        private bool IsAccessor(string identifier)
        {
            return IsClassAccessor(identifier) || identifier == "protected";
        }

        public void Dispose()
        {
            _reader?.Dispose();
        }
    }
}
