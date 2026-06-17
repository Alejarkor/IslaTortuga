using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace IslaTortuga.GameServer.Control
{
    /// <summary>
    /// Parser JSON minimalista y thread-safe (C# puro, sin UnityEngine), pensado para
    /// leer los cuerpos pequeños del plano de control en el hilo del HttpListener (no en
    /// el hilo principal de Unity, así que JsonUtility no sirve). Devuelve un grafo de
    /// objetos: Dictionary&lt;string, object&gt;, List&lt;object&gt;, string, double, bool o null.
    /// </summary>
    public static class JsonReader
    {
        public static object Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var parser = new Parser(text);
            parser.SkipWhitespace();
            var value = parser.ParseValue();
            parser.SkipWhitespace();
            if (!parser.AtEnd)
            {
                throw new FormatException("JSON con contenido sobrante tras el valor raíz.");
            }
            return value;
        }

        // --- Helpers tipados sobre un objeto ya parseado ---

        public static string GetString(IDictionary<string, object> obj, string key, string fallback = null)
        {
            return obj != null && obj.TryGetValue(key, out var v) && v is string s ? s : fallback;
        }

        public static int GetInt(IDictionary<string, object> obj, string key, int fallback = 0)
        {
            if (obj != null && obj.TryGetValue(key, out var v) && v is double d)
            {
                return (int)Math.Round(d);
            }
            return fallback;
        }

        public static List<string> GetStringList(IDictionary<string, object> obj, string key)
        {
            var result = new List<string>();
            if (obj != null && obj.TryGetValue(key, out var v) && v is List<object> list)
            {
                foreach (var item in list)
                {
                    if (item is string s)
                    {
                        result.Add(s);
                    }
                }
            }
            return result;
        }

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;

            public Parser(string s)
            {
                _s = s;
                _i = 0;
            }

            public bool AtEnd => _i >= _s.Length;

            public void SkipWhitespace()
            {
                while (_i < _s.Length)
                {
                    var c = _s[_i];
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                    {
                        _i++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            public object ParseValue()
            {
                if (AtEnd)
                {
                    throw new FormatException("JSON vacío o incompleto.");
                }

                var c = _s[_i];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't':
                    case 'f': return ParseBool();
                    case 'n': return ParseNull();
                    default: return ParseNumber();
                }
            }

            private Dictionary<string, object> ParseObject()
            {
                var obj = new Dictionary<string, object>();
                Expect('{');
                SkipWhitespace();
                if (Peek() == '}')
                {
                    _i++;
                    return obj;
                }

                while (true)
                {
                    SkipWhitespace();
                    var key = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    SkipWhitespace();
                    obj[key] = ParseValue();
                    SkipWhitespace();
                    var c = Next();
                    if (c == '}')
                    {
                        break;
                    }
                    if (c != ',')
                    {
                        throw new FormatException($"Se esperaba ',' o '}}' en posición {_i}.");
                    }
                }
                return obj;
            }

            private List<object> ParseArray()
            {
                var arr = new List<object>();
                Expect('[');
                SkipWhitespace();
                if (Peek() == ']')
                {
                    _i++;
                    return arr;
                }

                while (true)
                {
                    SkipWhitespace();
                    arr.Add(ParseValue());
                    SkipWhitespace();
                    var c = Next();
                    if (c == ']')
                    {
                        break;
                    }
                    if (c != ',')
                    {
                        throw new FormatException($"Se esperaba ',' o ']' en posición {_i}.");
                    }
                }
                return arr;
            }

            private string ParseString()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd)
                    {
                        throw new FormatException("Cadena JSON sin cerrar.");
                    }
                    var c = _s[_i++];
                    if (c == '"')
                    {
                        break;
                    }
                    if (c == '\\')
                    {
                        if (AtEnd)
                        {
                            throw new FormatException("Escape JSON sin cerrar.");
                        }
                        var esc = _s[_i++];
                        switch (esc)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                if (_i + 4 > _s.Length)
                                {
                                    throw new FormatException("Escape unicode incompleto.");
                                }
                                var hex = _s.Substring(_i, 4);
                                _i += 4;
                                sb.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                                break;
                            default:
                                throw new FormatException($"Escape JSON inválido: \\{esc}");
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                return sb.ToString();
            }

            private object ParseNumber()
            {
                var start = _i;
                while (_i < _s.Length)
                {
                    var c = _s[_i];
                    if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E')
                    {
                        _i++;
                    }
                    else
                    {
                        break;
                    }
                }

                var token = _s.Substring(start, _i - start);
                if (token.Length == 0 ||
                    !double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    throw new FormatException($"Número JSON inválido: '{token}'.");
                }
                return value;
            }

            private bool ParseBool()
            {
                if (Matches("true"))
                {
                    _i += 4;
                    return true;
                }
                if (Matches("false"))
                {
                    _i += 5;
                    return false;
                }
                throw new FormatException($"Token booleano inválido en posición {_i}.");
            }

            private object ParseNull()
            {
                if (Matches("null"))
                {
                    _i += 4;
                    return null;
                }
                throw new FormatException($"Token null inválido en posición {_i}.");
            }

            private bool Matches(string literal)
            {
                return _i + literal.Length <= _s.Length &&
                       _s.Substring(_i, literal.Length) == literal;
            }

            private char Peek() => AtEnd ? '\0' : _s[_i];

            private char Next()
            {
                if (AtEnd)
                {
                    throw new FormatException("Fin de JSON inesperado.");
                }
                return _s[_i++];
            }

            private void Expect(char c)
            {
                var actual = Next();
                if (actual != c)
                {
                    throw new FormatException($"Se esperaba '{c}' pero se encontró '{actual}' en posición {_i - 1}.");
                }
            }
        }
    }
}
