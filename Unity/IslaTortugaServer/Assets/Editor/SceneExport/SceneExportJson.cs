using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace IslaTortuga.Unity.SceneExport.Editor
{
    internal static class SceneExportJson
    {
        public static object Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return Parser.Parse(json);
        }

        public static string Serialize(object value)
        {
            var builder = new StringBuilder(2048);
            Serializer.SerializeValue(value, builder);
            return builder.ToString();
        }

        private sealed class Parser : IDisposable
        {
            private readonly StringReader _json;

            private Parser(string json)
            {
                _json = new StringReader(json);
            }

            public static object Parse(string json)
            {
                using (var parser = new Parser(json))
                {
                    return parser.ParseValue();
                }
            }

            public void Dispose()
            {
                _json.Dispose();
            }

            private Dictionary<string, object> ParseObject()
            {
                var table = new Dictionary<string, object>(StringComparer.Ordinal);

                _json.Read();

                while (true)
                {
                    switch (NextToken)
                    {
                        case Token.None:
                            return null;
                        case Token.CurlyClose:
                            _json.Read();
                            return table;
                        default:
                            var name = ParseString();
                            if (name == null)
                            {
                                return null;
                            }

                            if (NextToken != Token.Colon)
                            {
                                return null;
                            }

                            _json.Read();
                            table[name] = ParseValue();
                            break;
                    }

                    switch (NextToken)
                    {
                        case Token.Comma:
                            _json.Read();
                            break;
                        case Token.CurlyClose:
                            _json.Read();
                            return table;
                        default:
                            return table;
                    }
                }
            }

            private List<object> ParseArray()
            {
                var array = new List<object>();

                _json.Read();

                var parsing = true;
                while (parsing)
                {
                    var nextToken = NextToken;
                    switch (nextToken)
                    {
                        case Token.None:
                            return null;
                        case Token.SquaredClose:
                            _json.Read();
                            break;
                        default:
                            array.Add(ParseValue());
                            break;
                    }

                    switch (NextToken)
                    {
                        case Token.Comma:
                            _json.Read();
                            break;
                        case Token.SquaredClose:
                            _json.Read();
                            parsing = false;
                            break;
                        default:
                            parsing = false;
                            break;
                    }
                }

                return array;
            }

            private object ParseValue()
            {
                switch (NextToken)
                {
                    case Token.String:
                        return ParseString();
                    case Token.Number:
                        return ParseNumber();
                    case Token.CurlyOpen:
                        return ParseObject();
                    case Token.SquaredOpen:
                        return ParseArray();
                    case Token.True:
                        _json.Read();
                        _json.Read();
                        _json.Read();
                        _json.Read();
                        return true;
                    case Token.False:
                        _json.Read();
                        _json.Read();
                        _json.Read();
                        _json.Read();
                        _json.Read();
                        return false;
                    case Token.Null:
                        _json.Read();
                        _json.Read();
                        _json.Read();
                        _json.Read();
                        return null;
                    default:
                        return null;
                }
            }

            private string ParseString()
            {
                var builder = new StringBuilder();
                char c;

                _json.Read();

                var parsing = true;
                while (parsing)
                {
                    if (_json.Peek() == -1)
                    {
                        break;
                    }

                    c = NextChar;
                    switch (c)
                    {
                        case '"':
                            parsing = false;
                            break;
                        case '\\':
                            if (_json.Peek() == -1)
                            {
                                parsing = false;
                                break;
                            }

                            c = NextChar;
                            switch (c)
                            {
                                case '"':
                                case '\\':
                                case '/':
                                    builder.Append(c);
                                    break;
                                case 'b':
                                    builder.Append('\b');
                                    break;
                                case 'f':
                                    builder.Append('\f');
                                    break;
                                case 'n':
                                    builder.Append('\n');
                                    break;
                                case 'r':
                                    builder.Append('\r');
                                    break;
                                case 't':
                                    builder.Append('\t');
                                    break;
                                case 'u':
                                    var hex = new char[4];
                                    for (var index = 0; index < 4; index++)
                                    {
                                        hex[index] = NextChar;
                                    }

                                    builder.Append((char)Convert.ToInt32(new string(hex), 16));
                                    break;
                            }

                            break;
                        default:
                            builder.Append(c);
                            break;
                    }
                }

                return builder.ToString();
            }

            private object ParseNumber()
            {
                var number = NextWord;
                if (number.IndexOf('.') == -1 &&
                    number.IndexOf('e') == -1 &&
                    number.IndexOf('E') == -1)
                {
                    if (long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLong))
                    {
                        return parsedLong;
                    }
                }

                if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDouble))
                {
                    return parsedDouble;
                }

                return 0d;
            }

            private void EatWhitespace()
            {
                while (char.IsWhiteSpace(PeekChar))
                {
                    _json.Read();

                    if (_json.Peek() == -1)
                    {
                        break;
                    }
                }
            }

            private char PeekChar
            {
                get { return Convert.ToChar(_json.Peek()); }
            }

            private char NextChar
            {
                get { return Convert.ToChar(_json.Read()); }
            }

            private string NextWord
            {
                get
                {
                    var builder = new StringBuilder();

                    while (_json.Peek() != -1 && !IsWordBreak(PeekChar))
                    {
                        builder.Append(NextChar);
                    }

                    return builder.ToString();
                }
            }

            private Token NextToken
            {
                get
                {
                    EatWhitespace();

                    if (_json.Peek() == -1)
                    {
                        return Token.None;
                    }

                    switch (PeekChar)
                    {
                        case '{':
                            return Token.CurlyOpen;
                        case '}':
                            return Token.CurlyClose;
                        case '[':
                            return Token.SquaredOpen;
                        case ']':
                            return Token.SquaredClose;
                        case ',':
                            return Token.Comma;
                        case '"':
                            return Token.String;
                        case ':':
                            return Token.Colon;
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
                        case '-':
                            return Token.Number;
                    }

                    var word = NextWord;
                    switch (word)
                    {
                        case "false":
                            return Token.False;
                        case "true":
                            return Token.True;
                        case "null":
                            return Token.Null;
                    }

                    return Token.None;
                }
            }

            private static bool IsWordBreak(char c)
            {
                return char.IsWhiteSpace(c) || c == ',' || c == ':' || c == ']' || c == '}' || c == '[' || c == '{' || c == '"';
            }

            private enum Token
            {
                None,
                CurlyOpen,
                CurlyClose,
                SquaredOpen,
                SquaredClose,
                Colon,
                Comma,
                String,
                Number,
                True,
                False,
                Null,
            }
        }

        private static class Serializer
        {
            public static void SerializeValue(object value, StringBuilder builder)
            {
                if (value == null)
                {
                    builder.Append("null");
                    return;
                }

                if (value is string stringValue)
                {
                    SerializeString(stringValue, builder);
                    return;
                }

                if (value is bool boolValue)
                {
                    builder.Append(boolValue ? "true" : "false");
                    return;
                }

                if (value is IDictionary dictionary)
                {
                    SerializeObject(dictionary, builder);
                    return;
                }

                if (value is IList list)
                {
                    SerializeArray(list, builder);
                    return;
                }

                if (IsNumeric(value))
                {
                    SerializeNumber(Convert.ToDouble(value, CultureInfo.InvariantCulture), builder);
                    return;
                }

                SerializeString(value.ToString(), builder);
            }

            private static void SerializeObject(IDictionary dictionary, StringBuilder builder)
            {
                var first = true;
                builder.Append('{');

                foreach (DictionaryEntry entry in dictionary)
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }

                    SerializeString(entry.Key.ToString(), builder);
                    builder.Append(':');
                    SerializeValue(entry.Value, builder);
                    first = false;
                }

                builder.Append('}');
            }

            private static void SerializeArray(IList array, StringBuilder builder)
            {
                builder.Append('[');
                var first = true;

                foreach (var entry in array)
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }

                    SerializeValue(entry, builder);
                    first = false;
                }

                builder.Append(']');
            }

            private static void SerializeString(string value, StringBuilder builder)
            {
                builder.Append('"');

                for (var index = 0; index < value.Length; index++)
                {
                    var c = value[index];
                    switch (c)
                    {
                        case '"':
                            builder.Append("\\\"");
                            break;
                        case '\\':
                            builder.Append("\\\\");
                            break;
                        case '\b':
                            builder.Append("\\b");
                            break;
                        case '\f':
                            builder.Append("\\f");
                            break;
                        case '\n':
                            builder.Append("\\n");
                            break;
                        case '\r':
                            builder.Append("\\r");
                            break;
                        case '\t':
                            builder.Append("\\t");
                            break;
                        default:
                            if (c < ' ')
                            {
                                builder.Append("\\u");
                                builder.Append(((int)c).ToString("x4"));
                            }
                            else
                            {
                                builder.Append(c);
                            }

                            break;
                    }
                }

                builder.Append('"');
            }

            private static void SerializeNumber(double number, StringBuilder builder)
            {
                builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
            }

            private static bool IsNumeric(object value)
            {
                return value is sbyte ||
                       value is byte ||
                       value is short ||
                       value is ushort ||
                       value is int ||
                       value is uint ||
                       value is long ||
                       value is ulong ||
                       value is float ||
                       value is double ||
                       value is decimal;
            }
        }
    }
}
