using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Source2Unity.Formats.Common;
using Source2Unity.Formats.Vpk;

namespace Source2Unity.Formats.KeyValues
{
    /// <summary>
    /// KeyValues v1 text parser used by VMT, VDF, RES, and similar Valve formats.
    /// </summary>
    public static class KvParser
    {
        public static KvObject Parse(string text, KvParseOptions options = null)
        {
            options ??= KvParseOptions.Default;
            var tokenizer = new KvTokenizer(text);
            return ParseRoot(tokenizer, options);
        }

        public static KvObject Parse(Stream stream, KvParseOptions options = null)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            return Parse(reader.ReadToEnd(), options);
        }

        private static KvObject ParseRoot(KvTokenizer tokenizer, KvParseOptions options)
        {
            if (!tokenizer.TryReadString(out string name))
                throw new InvalidDataException("KeyValues document must start with a quoted key.");

            if (tokenizer.TryMatch('{'))
            {
                var children = ParseObjectBody(tokenizer, options);
                return new KvObject { Name = name, Children = children };
            }

            if (!tokenizer.TryReadString(out string value))
                throw new InvalidDataException($"Expected value after key '{name}'.");

            return new KvObject { Name = name, Value = value, Children = Array.Empty<KvObject>() };
        }

        private static List<KvObject> ParseObjectBody(KvTokenizer tokenizer, KvParseOptions options)
        {
            var children = new List<KvObject>();

            while (true)
            {
                children.AddRange(ProcessDirectives(tokenizer, options));

                if (tokenizer.TryMatch('}'))
                    return children;

                if (!tokenizer.TryReadString(out string key))
                    throw new InvalidDataException("Expected quoted key inside object.");

                if (tokenizer.TryMatch('{'))
                {
                    children.Add(new KvObject
                    {
                        Name = key,
                        Children = ParseObjectBody(tokenizer, options)
                    });
                    continue;
                }

                if (!tokenizer.TryReadString(out string value))
                    throw new InvalidDataException($"Expected value or object for key '{key}'.");

                children.Add(new KvObject
                {
                    Name = key,
                    Value = value,
                    Children = Array.Empty<KvObject>()
                });
            }
        }

        private static List<KvObject> ProcessDirectives(KvTokenizer tokenizer, KvParseOptions options)
        {
            var merged = new List<KvObject>();

            while (tokenizer.TryReadDirective(out string directive, out string argument))
            {
                if (directive != "include" || options?.IncludeResolver == null || string.IsNullOrEmpty(argument))
                    continue;

                var included = LoadIncluded(argument, options);
                if (included == null)
                    continue;

                foreach (var entry in KvMerge.FlattenInclude(included))
                    merged.Add(entry);
            }

            return merged;
        }

        private static KvObject LoadIncluded(string includeArgument, KvParseOptions options)
        {
            string includePath = ResolveIncludePath(options.SourcePath, includeArgument);
            var visited = options.IncludeStack ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (visited.Contains(includePath))
                return null;

            if (!options.IncludeResolver.TryOpenRead(includePath, out var stream))
                return null;

            using (stream)
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                var childOptions = new KvParseOptions
                {
                    IncludeResolver = options.IncludeResolver,
                    SourcePath = includePath,
                    IncludeStack = new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase) { includePath }
                };

                return Parse(reader.ReadToEnd(), childOptions);
            }
        }

        private static string ResolveIncludePath(string sourcePath, string includeArgument)
        {
            includeArgument = includeArgument.Replace('\\', '/').Trim();
            if (string.IsNullOrEmpty(sourcePath))
                return includeArgument;

            string dir = GetDirectory(sourcePath);
            if (string.IsNullOrEmpty(dir))
                return includeArgument;

            return VpkPath.Combine(dir, includeArgument);
        }

        private static string GetDirectory(string path)
        {
            path = path.Replace('\\', '/');
            int slash = path.LastIndexOf('/');
            return slash < 0 ? string.Empty : path.Substring(0, slash);
        }

        private sealed class KvTokenizer
        {
            private readonly string _text;
            private int _pos;

            public KvTokenizer(string text)
            {
                _text = text ?? string.Empty;
            }

            public bool TryMatch(char c)
            {
                SkipWhitespaceAndComments();
                if (_pos >= _text.Length || _text[_pos] != c)
                    return false;
                _pos++;
                return true;
            }

            public bool TryReadString(out string value)
            {
                SkipWhitespaceAndComments();
                value = null;
                if (_pos >= _text.Length || _text[_pos] != '"')
                    return false;

                _pos++;
                var sb = new StringBuilder();
                while (_pos < _text.Length)
                {
                    char c = _text[_pos++];
                    if (c == '"')
                    {
                        value = sb.ToString();
                        return true;
                    }

                    if (c == '\\' && _pos < _text.Length)
                    {
                        char esc = _text[_pos++];
                        sb.Append(esc switch
                        {
                            'n' => '\n',
                            't' => '\t',
                            'r' => '\r',
                            '\\' => '\\',
                            '"' => '"',
                            _ => esc
                        });
                        continue;
                    }

                    sb.Append(c);
                }

                throw new InvalidDataException("Unterminated quoted string in KeyValues.");
            }

            public bool TryReadDirective(out string directive, out string argument)
            {
                SkipWhitespaceAndComments();
                directive = null;
                argument = null;

                if (_pos >= _text.Length || _text[_pos] != '#')
                    return false;

                _pos++;
                int start = _pos;
                while (_pos < _text.Length && char.IsLetter(_text[_pos]))
                    _pos++;

                if (_pos == start)
                    return false;

                directive = _text.Substring(start, _pos - start).ToLowerInvariant();
                SkipWhitespaceAndComments();

                if (_pos < _text.Length && _text[_pos] == '"')
                {
                    TryReadString(out argument);
                }
                else
                {
                    start = _pos;
                    while (_pos < _text.Length && !char.IsWhiteSpace(_text[_pos]) && _text[_pos] != '\n' && _text[_pos] != '\r')
                        _pos++;
                    if (_pos > start)
                        argument = _text.Substring(start, _pos - start);
                }

                return true;
            }

            private void SkipWhitespaceAndComments()
            {
                while (_pos < _text.Length)
                {
                    char c = _text[_pos];
                    if (char.IsWhiteSpace(c))
                    {
                        _pos++;
                        continue;
                    }

                    if (c == '/' && _pos + 1 < _text.Length && _text[_pos + 1] == '/')
                    {
                        _pos += 2;
                        while (_pos < _text.Length && _text[_pos] != '\n')
                            _pos++;
                        continue;
                    }

                    break;
                }
            }
        }
    }
}
