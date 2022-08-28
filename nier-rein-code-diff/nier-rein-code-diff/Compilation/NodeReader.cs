using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace nier_rein_code_diff.Compilation
{
    public class NodeReader : IDisposable
    {
        private readonly Lexer _lexer;

        private int _currentNodePosition;
        private readonly IDictionary<int, SyntaxNode> _peekedNodes;

        public NodeReader(Lexer lexer)
        {
            _lexer = lexer;
            _peekedNodes = new Dictionary<int, SyntaxNode>();
        }

        public static NodeReader FromFile(string path)
        {
            return new NodeReader(Lexer.FromFile(path));
        }

        public SyntaxNode Peek(int position = 0)
        {
            if (position < 0)
                throw new ArgumentOutOfRangeException(nameof(position));

            if (_peekedNodes.ContainsKey(_currentNodePosition + position))
                return _peekedNodes[_currentNodePosition + position];

            var currentPosition = _currentNodePosition;
            for (int i = currentPosition; i <= currentPosition + position; i++)
                _peekedNodes[i] = Read();
            _currentNodePosition -= position + 1;

            return _peekedNodes[_currentNodePosition + position];
        }

        public SyntaxNode Read()
        {
            if (_peekedNodes.ContainsKey(_currentNodePosition))
            {
                var nextNode = _peekedNodes[_currentNodePosition];
                _peekedNodes.Remove(_currentNodePosition);

                _currentNodePosition++;
                return nextNode;
            }

            if (_lexer.CurrentToken == null)
                _lexer.Lex();

            var lead = ReadEmptyCharacters();

            var valueToken = _lexer.CurrentToken;
            _lexer.Lex();

            var trail = ReadEmptyCharacters();

            var node = ResolveToken(valueToken);
            node.Lead = lead.Select(ResolveToken).ToList();
            node.Trail = trail.Select(ResolveToken).ToList();

            _currentNodePosition++;
            return node;
        }

        protected SyntaxNode ResolveToken(SyntaxToken token)
        {
            switch (token.Kind)
            {
                case SyntaxKind.Identifier:
                    return new SyntaxNode(SyntaxNodeKind.Identifier) { Text = token.Text };

                // Braces
                case SyntaxKind.OpenBrace:
                    return new SyntaxNode(SyntaxNodeKind.OpenBrace) { Text = token.Text };
                case SyntaxKind.CloseBrace:
                    return new SyntaxNode(SyntaxNodeKind.CloseBrace) { Text = token.Text };

                // Brackets
                case SyntaxKind.OpenBracket:
                    return new SyntaxNode(SyntaxNodeKind.OpenBracket) { Text = token.Text };
                case SyntaxKind.CloseBracket:
                    return new SyntaxNode(SyntaxNodeKind.CloseBracket) { Text = token.Text };

                // Paren
                case SyntaxKind.OpenParen:
                    return new SyntaxNode(SyntaxNodeKind.OpenParen) { Text = token.Text };
                case SyntaxKind.CloseParen:
                    return new SyntaxNode(SyntaxNodeKind.CloseParen) { Text = token.Text };

                // Special characters
                case SyntaxKind.At:
                    return new SyntaxNode(SyntaxNodeKind.At) { Text = token.Text };
                case SyntaxKind.Dot:
                    return new SyntaxNode(SyntaxNodeKind.Dot) { Text = token.Text };
                case SyntaxKind.Colon:
                    return new SyntaxNode(SyntaxNodeKind.Colon) { Text = token.Text };
                case SyntaxKind.SemiColon:
                    return new SyntaxNode(SyntaxNodeKind.SemiColon) { Text = token.Text };
                case SyntaxKind.Comma:
                    return new SyntaxNode(SyntaxNodeKind.Comma) { Text = token.Text };
                case SyntaxKind.Asterisk:
                    return new SyntaxNode(SyntaxNodeKind.Asterisk) { Text = token.Text };
                case SyntaxKind.WhiteSpace:
                    return new SyntaxNode(SyntaxNodeKind.WhiteSpace) { Text = token.Text };
                case SyntaxKind.Tabulator:
                    return new SyntaxNode(SyntaxNodeKind.Tabulator) { Text = token.Text };
                case SyntaxKind.Hashtag:
                    return new SyntaxNode(SyntaxNodeKind.Hashtag) { Text = token.Text };
                case SyntaxKind.ExclamationMark:
                    return new SyntaxNode(SyntaxNodeKind.ExclamationMark) { Text = token.Text };
                case SyntaxKind.Pipe:
                    return new SyntaxNode(SyntaxNodeKind.Pipe) { Text = token.Text };
                case SyntaxKind.Slash:
                    return new SyntaxNode(SyntaxNodeKind.Slash) { Text = token.Text };

                // Comments
                case SyntaxKind.DoubleSlash:
                    return new SyntaxNode(SyntaxNodeKind.DoubleSlash) { Text = token.Text };
                case SyntaxKind.MultiLineComment:
                    return new SyntaxNode(SyntaxNodeKind.MultiLineComment) { Text = token.Text };
                case SyntaxKind.MultiLineCommentEnd:
                    return new SyntaxNode(SyntaxNodeKind.MultiLineCommentEnd) { Text = token.Text };

                // Literals
                case SyntaxKind.StringLiteral:
                    return new SyntaxNode(SyntaxNodeKind.StringLiteral) { Text = token.Text };
                case SyntaxKind.NumericLiteral:
                    return new SyntaxNode(SyntaxNodeKind.NumericLiteral)
                    {
                        IntValue = token.IntValue,
                        LongValue = token.LongValue,
                        FloatValue = token.FloatValue,
                        DoubleValue = token.DoubleValue,
                        Text = token.Text
                    };

                // Logical operators
                case SyntaxKind.ArrowRight:
                    return new SyntaxNode(SyntaxNodeKind.ArrowRight) { Text = token.Text };
                case SyntaxKind.Equals:
                    return new SyntaxNode(SyntaxNodeKind.Equals) { Text = token.Text };
                case SyntaxKind.EqualsEquals:
                    return new SyntaxNode(SyntaxNodeKind.EqualsEquals) { Text = token.Text };
                case SyntaxKind.PlusEquals:
                    return new SyntaxNode(SyntaxNodeKind.PlusEquals) { Text = token.Text };
                case SyntaxKind.MinusSign:
                    return new SyntaxNode(SyntaxNodeKind.MinusSign) { Text = token.Text };
                case SyntaxKind.GreaterThan:
                    return new SyntaxNode(SyntaxNodeKind.GreaterThan) { Text = token.Text };
                case SyntaxKind.GreaterEquals:
                    return new SyntaxNode(SyntaxNodeKind.GreaterEquals) { Text = token.Text };
                case SyntaxKind.SmallerThan:
                    return new SyntaxNode(SyntaxNodeKind.SmallerThan) { Text = token.Text };
                case SyntaxKind.SmallerEquals:
                    return new SyntaxNode(SyntaxNodeKind.SmallerEquals) { Text = token.Text };

                case SyntaxKind.NewLine:
                    return new SyntaxNode(SyntaxNodeKind.NewLine) { Text = token.Text };
                case SyntaxKind.CarriageReturn:
                    return new SyntaxNode(SyntaxNodeKind.CarriageReturn) { Text = token.Text };

                case SyntaxKind.EndOfFile:
                    return new SyntaxNode(SyntaxNodeKind.EndOfFile);

                default:
                    throw new InvalidOperationException($"Unknown token kind {token.Kind}.");
            }
        }

        protected bool IsEmptyChar(SyntaxKind kind)
        {
            return kind == SyntaxKind.WhiteSpace || kind == SyntaxKind.NewLine || kind == SyntaxKind.Tabulator;
        }

        private IList<SyntaxToken> ReadEmptyCharacters()
        {
            var result = new List<SyntaxToken>();

            while (IsEmptyChar(_lexer.CurrentToken.Kind))
            {
                result.Add(_lexer.CurrentToken);
                _lexer.Lex();
            }

            return result;
        }

        public void Dispose()
        {
            _lexer?.Dispose();
        }
    }

    [DebuggerDisplay("{Kind.ToString()}")]
    public class SyntaxNode
    {
        private static StringBuilder _sb = new StringBuilder();

        public SyntaxNodeKind Kind { get; }

        public string Text { get; set; }

        public int IntValue { get; set; }
        public long LongValue { get; set; }
        public float FloatValue { get; set; }
        public double DoubleValue { get; set; }

        public IList<SyntaxNode> Lead { get; set; }
        public IList<SyntaxNode> Trail { get; set; }

        public IList<SyntaxNode> Nodes { get; set; }

        public SyntaxNode(SyntaxNodeKind kind)
        {
            Kind = kind;
            Lead = new List<SyntaxNode>();
            Trail = new List<SyntaxNode>();
            Nodes = new List<SyntaxNode>();
        }

        public SyntaxNode Clone(SyntaxNodeKind kind)
        {
            return new SyntaxNode(kind)
            {
                Text = Text,

                IntValue = IntValue,
                LongValue = LongValue,
                FloatValue = FloatValue,
                DoubleValue = DoubleValue,

                Nodes = Nodes.ToList(),
                Lead = Lead.ToList(),
                Trail = Trail.ToList()
            };
        }

        public SyntaxNode FirstOrDefault(SyntaxNodeKind kind)
        {
            return Nodes.FirstOrDefault(x => x.Kind == kind);
        }

        public SyntaxNode[] Where(SyntaxNodeKind kind)
        {
            return Nodes.Where(x => x.Kind == kind).ToArray();
        }

        public bool Has(SyntaxNodeKind kind)
        {
            return Nodes.Any(x => x.Kind == kind);
        }

        public bool HasTrail(SyntaxNodeKind kind)
        {
            return Trail.Any(x => x.Kind == kind);
        }

        public override string ToString()
        {
            foreach (var lead in Lead)
                _sb.Append(lead.Text);

            if (!string.IsNullOrEmpty(Text)) 
                _sb.Append(Kind == SyntaxNodeKind.StringLiteral ? $"\"{Text}\"" : Text);

            foreach (var trail in Trail/*.Where(x => x.Kind != SyntaxKind.NewLine)*/)
                _sb.Append(trail.Text);

            var result = _sb.ToString();
            _sb.Clear();

            return result;
        }

        public void ToFile(string path)
        {
            File.WriteAllText(path, BuildFullString());
        }

        public string BuildFullString()
        {
            var sb = new StringBuilder();
            BuildFileContent(this, sb);

            return sb.ToString();
        }

        private void BuildFileContent(SyntaxNode parentNode, StringBuilder sb)
        {
            sb.Append(parentNode);

            foreach (var node in parentNode.Nodes)
                BuildFileContent(node, sb);
        }
    }

    public enum SyntaxNodeKind
    {
        CompilationUnit,

        // C#-specific
        UsingStatement,
        Fqdn,

        NamespaceUnit,
        NamespaceStatement,

        Class,
        Accessor,
        Abstract,
        Override,
        Sealed,
        Static,
        ReadOnly,
        ClassInheritance,
        ClassStatement,
        ClassBlock,
        Attribute,
        AttributeCtor,

        Field,
        Property,
        Method,
        Ctor,
        MemberName,

        Parameter,
        ParameterValue,
        Out,

        Type,
        TupleType,
        GenericTypes,
        ArrayType,

        PropertyBody,
        MethodBody,

        MethodCtorInvocation,

        Comment,
        CommentText,

        // Logical operators
        MinusSign,
        PlusSign,
        PlusEquals,
        MinusEquals,
        Equals,
        EqualsEquals,
        ArrowRight,
        GreaterEquals,
        SmallerEquals,
        GreaterThan,
        SmallerThan,

        // Special characters
        At,
        Dot,
        Colon,
        SemiColon,
        Comma,
        Pipe,
        Asterisk,
        WhiteSpace,
        Tabulator,
        Hashtag,
        Slash,
        ExclamationMark,

        // Keywords
        Namespace,
        FullyQualifiedName,
        Form,
        Commands,
        Intents,
        Contributions,
        ObjectType,
        Flow,
        App,
        And,
        Or,
        Not,

        // Form related
        FormBody,
        Context,
        Definitions,
        Header,
        Events,
        State,

        // Context related
        ContextObjectDeclaration,
        ContextObjectModifiers,
        ContextObjectConstructor,
        PrimaryModifier,
        ImportModifier,
        OptionalModifier,

        // DataObject related
        DataObjectDeclaration,
        DataObjectConstructor,
        DataObjectConstructorExpression,
        DataObjectBody,

        // AppId related
        AppId,
        AppIdDeclaration,
        AppIdConstructor,
        AppIdConstructorExpression,

        // Rank related
        Rank,
        RankDeclaration,
        RankConstructor,

        // Definition related
        DefinitionBody,
        DefinitionDeclaration,
        BehaviourDefinition,
        ConditionDefinition,
        DefinitionMethodInvocation,

        // Header related
        HeaderDeclaration,
        HeaderBody,

        // Event related
        EventDeclaration,

        // State related
        PropertyWidget,
        Widget,
        WidgetInitParameter,
        WidgetBody,

        // Language related
        Language,
        DefaultLanguage,
        Localization,
        LocalizationValue,

        // Intent related
        IntentDeclaration,
        IntentBody,

        // Command related
        CommandDeclaration,
        CommandDerivation,
        CommandBody,
        TranslationPropertyDeclaration,

        // Contribution related
        CommandContribution,
        DataObjectContribution,
        ContributionParameter,
        ContributionBody,

        // ObjectType related
        ObjectTypeBody,
        ObjectTypeMember,
        ObjectTypeMemberBody,
        ObjectTypeSummary,

        // Flow related
        InitializationIntent,
        ExternalIntents,
        InternalIntents,

        // App related
        AppBody,
        AppRequirementDeclaration,
        AppRequirements,
        AppRequirement,
        ObjectTypeRequirements,
        ObjectTypeRequirement,
        ObjectTypeRequirementCtor,
        LicenseRequirements,
        ServiceRequirements,
        AppDependencyDeclaration,

        // Custom widget related
        CustomWidget,
        CustomWidgetAttribute,
        CustomWidgetAttributeValue,
        CustomWidgetExtensions,
        CustomWidgetBody,
        CustomWidgetProperty,
        CustomWidgetPropertyOverride,
        CustomWidgetPropertyType,
        CustomWidgetPropertyTypeArray,
        CustomWidgetPropertyValue,
        CustomWidgetPropertyValueList,
        CustomWidgetPropertyEnumValues,
        CustomWidgetInitProperty,

        // ()
        OpenParen,
        CloseParen,

        // {}
        OpenBrace,
        CloseBrace,

        // []
        OpenBracket,
        CloseBracket,

        Identifier,
        DoubleSlash,
        MultiLineComment,
        MultiLineCommentEnd,

        NewLine,
        CarriageReturn,

        // Literals
        StringLiteral,
        NumericLiteral,

        EndOfFile
    }
}
