using System;
using System.IO;
using System.Text;

namespace nier_rein_code_diff.Compilation
{
    public class Lexer : IDisposable
    {
        private readonly TextReader _reader;

        public SyntaxToken CurrentToken { get; private set; }

        public Lexer(TextReader reader)
        {
            _reader = reader;
        }

        public static Lexer FromFile(string path)
        {
            return new Lexer(new StreamReader(path));
        }

        public SyntaxToken Lex()
        {
            if (!TryReadChar(out var character))
                return CurrentToken = new SyntaxToken(SyntaxKind.EndOfFile);

            switch (character)
            {
                // Logical operators
                case '=':
                    if (!TryPeekChar(out var peekedChar) || (peekedChar != '=' && peekedChar != '>'))
                        return CurrentToken = new SyntaxToken(SyntaxKind.Equals) { Text = "=" };

                    TryReadChar(out character);
                    if (character == '=')
                        return CurrentToken = new SyntaxToken(SyntaxKind.EqualsEquals) { Text = "==" };

                    return CurrentToken = new SyntaxToken(SyntaxKind.ArrowRight) { Text = "=>" };

                case '<':
                    if (!TryPeekChar(out var peekedChar1) || peekedChar1 != '=')
                        return CurrentToken = new SyntaxToken(SyntaxKind.SmallerThan) { Text = "<" };

                    TryReadChar(out _);
                    return CurrentToken = new SyntaxToken(SyntaxKind.SmallerEquals) { Text = "<=" };

                case '>':
                    if (!TryPeekChar(out var peekedChar2) || peekedChar2 != '=')
                        return CurrentToken = new SyntaxToken(SyntaxKind.GreaterThan) { Text = ">" };

                    TryReadChar(out character);
                    return CurrentToken = new SyntaxToken(SyntaxKind.GreaterEquals) { Text = ">=" };

                // Parens
                case '(':
                    return CurrentToken = new SyntaxToken(SyntaxKind.OpenParen) { Text = "(" };

                case ')':
                    return CurrentToken = new SyntaxToken(SyntaxKind.CloseParen) { Text = ")" };

                // Braces
                case '{':
                    return CurrentToken = new SyntaxToken(SyntaxKind.OpenBrace) { Text = "{" };

                case '}':
                    return CurrentToken = new SyntaxToken(SyntaxKind.CloseBrace) { Text = "}" };

                // Brackets
                case '[':
                    return CurrentToken = new SyntaxToken(SyntaxKind.OpenBracket) { Text = "[" };

                case ']':
                    return CurrentToken = new SyntaxToken(SyntaxKind.CloseBracket) { Text = "]" };

                // Literals
                case '"':
                    return CurrentToken = ReadStringLiteral();

                case '+':
                    if (!TryPeekChar(out var peekedChar3))
                        return CurrentToken = new SyntaxToken(SyntaxKind.PlusSign) { Text = "+" };

                    if (peekedChar3 - 0x30 >= 0 && peekedChar3 - 0x30 <= 9)
                        goto case '0';

                    if (peekedChar3 == '=')
                    {
                        TryReadChar(out _);
                        return CurrentToken = new SyntaxToken(SyntaxKind.PlusEquals) { Text = "+=" };
                    }

                    return CurrentToken = new SyntaxToken(SyntaxKind.PlusSign) { Text = "+" };

                case '-':
                    if (!TryPeekChar(out var peekedChar4))
                        return CurrentToken = new SyntaxToken(SyntaxKind.MinusSign) { Text = "-" };

                    if (peekedChar4 - 0x30 >= 0 && peekedChar4 - 0x30 <= 9)
                        goto case '0';

                    if (peekedChar4 == '=')
                    {
                        TryReadChar(out _);
                        return CurrentToken = new SyntaxToken(SyntaxKind.MinusEquals) { Text = "-=" };
                    }

                    return CurrentToken = new SyntaxToken(SyntaxKind.MinusSign) { Text = "-" };

                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    return CurrentToken = ReadNumericLiteral(character);

                // All the 'common' identifier characters are represented directly in
                // these switch cases for optimal perf.  Calling IsIdentifierChar() functions is relatively
                // expensive.
                case 'a':
                case 'b':
                case 'c':
                case 'd':
                case 'e':
                case 'f':
                case 'g':
                case 'h':
                case 'i':
                case 'j':
                case 'k':
                case 'l':
                case 'm':
                case 'n':
                case 'o':
                case 'p':
                case 'q':
                case 'r':
                case 's':
                case 't':
                case 'u':
                case 'v':
                case 'w':
                case 'x':
                case 'y':
                case 'z':
                case 'A':
                case 'B':
                case 'C':
                case 'D':
                case 'E':
                case 'F':
                case 'G':
                case 'H':
                case 'I':
                case 'J':
                case 'K':
                case 'L':
                case 'M':
                case 'N':
                case 'O':
                case 'P':
                case 'Q':
                case 'R':
                case 'S':
                case 'T':
                case 'U':
                case 'V':
                case 'W':
                case 'X':
                case 'Y':
                case 'Z':
                case '_':
                    return CurrentToken = ReadIdentifier(character);

                // Special characters
                case '@':
                    return CurrentToken = new SyntaxToken(SyntaxKind.At) { Text = "@" };

                case '.':
                    return CurrentToken = new SyntaxToken(SyntaxKind.Dot) { Text = "." };

                case '!':
                    return CurrentToken = new SyntaxToken(SyntaxKind.ExclamationMark) { Text = "!" };

                case ':':
                    return CurrentToken = new SyntaxToken(SyntaxKind.Colon) { Text = ":" };

                case ';':
                    return CurrentToken = new SyntaxToken(SyntaxKind.SemiColon) { Text = ";" };

                case ',':
                    return CurrentToken = new SyntaxToken(SyntaxKind.Comma) { Text = "," };

                case ' ':
                    return CurrentToken = new SyntaxToken(SyntaxKind.WhiteSpace) { Text = " " };

                case '*':
                    if (!TryPeekChar(out var peekedChar5))
                        return CurrentToken = new SyntaxToken(SyntaxKind.Asterisk) { Text = "*" };

                    if (peekedChar5 == '/')
                    {
                        TryReadChar(out _);
                        return CurrentToken = new SyntaxToken(SyntaxKind.MultiLineCommentEnd) { Text = "*/" };
                    }

                    return CurrentToken = new SyntaxToken(SyntaxKind.Asterisk) { Text = "*" };

                case '#':
                    return CurrentToken = new SyntaxToken(SyntaxKind.Hashtag) { Text = "#" };

                case '|':
                    return CurrentToken = new SyntaxToken(SyntaxKind.Pipe) { Text = "|" };

                case '/':
                    if (!TryPeekChar(out var peekedChar6))
                        return CurrentToken = new SyntaxToken(SyntaxKind.Slash) { Text = "/" };

                    if (peekedChar6 == '/')
                    {
                        TryReadChar(out _);
                        return CurrentToken = new SyntaxToken(SyntaxKind.DoubleSlash) { Text = "//" };
                    }
                    else if (peekedChar6 == '*')
                    {
                        TryReadChar(out _);
                        return CurrentToken = new SyntaxToken(SyntaxKind.MultiLineComment) { Text = "/*" };
                    }

                    return CurrentToken = new SyntaxToken(SyntaxKind.Slash) { Text = "/" };

                // Escaped characters
                case '\t':
                    return CurrentToken = new SyntaxToken(SyntaxKind.Tabulator) { Text = "\t" };

                case '\n':
                    return CurrentToken = new SyntaxToken(SyntaxKind.NewLine) { Text = "\n" };

                case '\r':
                    if (!TryPeekChar(out var peekedChar7) || peekedChar7 != '\n')
                        return CurrentToken = new SyntaxToken(SyntaxKind.CarriageReturn) { Text = "\r" };

                    TryReadChar(out _);
                    return CurrentToken = new SyntaxToken(SyntaxKind.NewLine) { Text = "\r\n" };
            }

            return CurrentToken = ReadIdentifier(character);
        }

        private SyntaxToken ReadStringLiteral()
        {
            var sb = new StringBuilder();

            while (true)
            {
                if (!TryReadChar(out var character) || character == '"')
                    return new SyntaxToken(SyntaxKind.StringLiteral) { Text = sb.ToString() };

                sb.Append(character);
            }
        }

        private SyntaxToken ReadNumericLiteral(char init)
        {
            var sb = new StringBuilder();
            sb.Append(init);

            var dots = 0;
            while (true)
            {
                if (!TryPeekChar(out var character))
                    return dots > 0 ?
                        ParseFloatingPointToToken(new SyntaxToken(SyntaxKind.NumericLiteral), sb.ToString()) :
                        ParseIntegerToToken(new SyntaxToken(SyntaxKind.NumericLiteral), sb.ToString());

                switch (character)
                {
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        TryReadChar(out character);
                        sb.Append(character);
                        continue;

                    case '.':
                        dots++;

                        if (dots > 1)
                            throw new InvalidOperationException("Found second decimal point in numeric value");

                        TryReadChar(out character);
                        sb.Append(character);
                        continue;
                }

                return dots > 0 ?
                    ParseFloatingPointToToken(new SyntaxToken(SyntaxKind.NumericLiteral), sb.ToString()) :
                    ParseIntegerToToken(new SyntaxToken(SyntaxKind.NumericLiteral), sb.ToString());
            }
        }

        private SyntaxToken ReadIdentifier(char init)
        {
            var sb = new StringBuilder();
            sb.Append(init);

            while (true)
            {
                if (!TryPeekChar(out var character))
                    return new SyntaxToken(SyntaxKind.Identifier) { Text = sb.ToString() };

                switch (character)
                {
                    case '=':
                    case '<':
                    case '>':
                    case '-':
                    case ' ':
                    case '.':
                    case ',':
                    case ':':
                    case ';':
                    case '*':
                    case '#':
                    case '`':
                    case '\n':
                    case '\r':
                    case '\t':
                    case '(':
                    case ')':
                    case '{':
                    case '}':
                    case '[':
                    case ']':
                        return new SyntaxToken(SyntaxKind.Identifier) { Text = sb.ToString() };
                }

                TryReadChar(out character);
                sb.Append(character);
            }
        }

        private SyntaxToken ParseIntegerToToken(SyntaxToken token, string numericLiteral)
        {
            if (int.TryParse(numericLiteral, out var intNumber))
            {
                token.IntValue = intNumber;
                token.Text = numericLiteral;
                return token;
            }

            if (long.TryParse(numericLiteral, out var longNumber))
            {
                token.LongValue = longNumber;
                token.Text = numericLiteral;
                return token;
            }

            throw new InvalidOperationException("Unsupported integer numeric literal.");
        }

        private SyntaxToken ParseFloatingPointToToken(SyntaxToken token, string numericLiteral)
        {
            if (float.TryParse(numericLiteral, out var floatNumber))
            {
                token.FloatValue = floatNumber;
                token.Text = numericLiteral;
                return token;
            }

            if (double.TryParse(numericLiteral, out var doubleNumber))
            {
                token.DoubleValue = doubleNumber;
                token.Text = numericLiteral;
                return token;
            }

            throw new InvalidOperationException("Unsupported floating point numeric literal.");
        }

        internal bool TryReadChar(out char character)
        {
            character = default;

            var result = _reader.Read();
            if (result < 0)
                return false;

            character = (char)result;
            return true;
        }

        internal bool TryPeekChar(out char character)
        {
            character = default;

            var result = _reader.Peek();
            if (result < 0)
                return false;

            character = (char)result;
            return true;
        }

        public void Dispose()
        {
            _reader?.Dispose();
        }
    }

    public class SyntaxToken
    {
        public SyntaxKind Kind { get; }

        public string Text { get; set; }

        public float FloatValue { get; set; }
        public double DoubleValue { get; set; }

        public int IntValue { get; set; }
        public long LongValue { get; set; }

        public SyntaxToken(SyntaxKind kind)
        {
            Kind = kind;
        }

        public override string ToString()
        {
            return Kind.ToString();
        }
    }

    public enum SyntaxKind
    {
        None,

        At,
        Dot,
        Colon,
        SemiColon,
        Comma,
        Pipe,
        Apostrophe,
        Asterisk,
        WhiteSpace,
        Tabulator,
        Hashtag,
        Slash,
        ExclamationMark,

        // ()
        OpenParen,
        CloseParen,

        // {}
        OpenBrace,
        CloseBrace,

        // []
        OpenBracket,
        CloseBracket,

        // Logial operators
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

        // Literals
        StringLiteral,
        NumericLiteral,

        Identifier,
        DoubleSlash,
        MultiLineComment,
        MultiLineCommentEnd,

        // \n or \r\n
        NewLine,
        CarriageReturn,

        EndOfFile
    }
}
