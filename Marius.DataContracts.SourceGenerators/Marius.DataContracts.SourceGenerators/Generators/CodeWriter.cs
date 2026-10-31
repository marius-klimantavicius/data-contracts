using System.Text;

namespace Marius.DataContracts.SourceGenerators.Generators;

public class CodeWriter
{
    private readonly StringBuilder _sb = new StringBuilder();
    private int _nameIndex;
    private int _indent;
    private string _indentValue = "";

    private readonly HashSet<string> _fileNames;
    private readonly HashSet<string> _names;

    public CodeWriter? Parent { get; }

    public CodeWriter()
    {
        _fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _names = new HashSet<string>(StringComparer.Ordinal);
    }

    public CodeWriter(CodeWriter parent)
    {
        Parent = parent;
        _fileNames = parent._fileNames;
        _names = parent._names;
    }

    public IndentDisposable Indent()
    {
        var result = new IndentDisposable(this, null);
        _indent++;
        _indentValue = new string(' ', _indent * 4);
        return result;
    }

    public IndentDisposable Block(string start = "{", string end = "}")
    {
        AppendLine(start);
        var result = new IndentDisposable(this, end);
        _indent++;
        _indentValue = new string(' ', _indent * 4);
        return result;
    }

    public void AppendLine(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            _sb.AppendLine();
            return;
        }

        _sb.Append(_indentValue);
        _sb.AppendLine(value);
    }

    public void AppendLine()
    {
        _sb.AppendLine();
    }

    public void Clear()
    {
        _sb.Clear();
        _indent = 0;
        _indentValue = "";
    }

    public string GetString()
    {
        return _sb.ToString();
    }

    public string LocalName(string prefix)
    {
        var owner = Parent ?? this;
        string name;
        do
        {
            name = $"{prefix}{owner._nameIndex++}";
        } while (!_names.Add(name));

        return name;
    }

    public void ReserveNames(IEnumerable<string> names)
    {
        foreach (var name in names)
            _names.Add(name);
    }

    public string FileName(string hintName)
    {
        var prefix = hintName;
        while (!_fileNames.Add(hintName))
            hintName = LocalName(prefix);

        return hintName;
    }

    public readonly struct IndentDisposable : IDisposable
    {
        private readonly CodeWriter _generator;
        private readonly string? _appendOnDispose;
        private readonly int _restoreIndent;
        private readonly string _restoreIntentValue;

        public IndentDisposable(CodeWriter generator, string? appendOnDispose)
        {
            _generator = generator;
            _appendOnDispose = appendOnDispose;
            _restoreIndent = generator._indent;
            _restoreIntentValue = generator._indentValue;
        }

        public void Dispose()
        {
            if (_generator == null!)
                return;

            _generator._indent = _restoreIndent;
            _generator._indentValue = _restoreIntentValue;
            if (_appendOnDispose != null)
                _generator.AppendLine(_appendOnDispose);
        }
    }
}