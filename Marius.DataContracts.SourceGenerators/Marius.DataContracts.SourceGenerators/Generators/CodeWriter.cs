using System.Text;

namespace Marius.DataContracts.SourceGenerators.Generators;

public class CodeWriter
{
    private readonly StringBuilder _sb = new StringBuilder();
    private int _nameIndex;
    private int _indent;
    private string _indentValue = "";

    private readonly HashSet<string> _fileNames;

    public CodeWriter? Parent { get; }
    
    public CodeWriter()
    {
        _fileNames = new HashSet<string>();
    }

    public CodeWriter(CodeWriter parent)
    {
        Parent = parent;
        _fileNames = parent._fileNames;
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
        if (Parent != null)
            return $"{prefix}{Parent._nameIndex++}";

        return $"{prefix}{_nameIndex++}";
    }

    public string FileName(string hintName)
    {
        if (!_fileNames.Add(hintName))
        {
            hintName = LocalName(hintName);
            _fileNames.Add(hintName);
        }

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