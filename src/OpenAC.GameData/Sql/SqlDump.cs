using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace OpenAC.GameData.Sql;

/// <summary>One table read from a MySQL dump: its column names and every row.</summary>
internal sealed class SqlTable(string name, IReadOnlyList<string> columns)
{
    public string Name { get; } = name;

    public IReadOnlyList<string> Columns { get; } = columns;

    /// <summary>Each value is a <c>long</c>, a <c>double</c>, a <c>string</c> or null.</summary>
    public List<object?[]> Rows { get; } = [];

    public int Column(string name)
    {
        for (int i = 0; i < Columns.Count; i++)
        {
            if (string.Equals(Columns[i], name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        throw new KeyNotFoundException($"Table '{Name}' has no column '{name}'.");
    }
}

/// <summary>
/// Reads the tables a caller asks for out of a <c>mysqldump</c> file: the
/// <c>CREATE TABLE</c> statement for the column names, and the extended
/// <c>INSERT INTO ... VALUES (...),(...);</c> lines for the rows. No database
/// server is needed.
/// </summary>
internal static class SqlDump
{
    private const string CreatePrefix = "CREATE TABLE `";
    private const string InsertPrefix = "INSERT INTO `";

    /// <summary>Reads a <c>.sql</c> file, or the single <c>.sql</c> entry of a <c>.zip</c>.</summary>
    public static Dictionary<string, SqlTable> Load(string path, IReadOnlyCollection<string> tables)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using ZipArchive zip = ZipFile.OpenRead(path);
            ZipArchiveEntry entry = zip.Entries.Single(
                static e => e.FullName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase));
            using var zipReader = new StreamReader(entry.Open(), Encoding.UTF8);
            return Read(zipReader, tables);
        }
        using var reader = new StreamReader(path, Encoding.UTF8);
        return Read(reader, tables);
    }

    public static Dictionary<string, SqlTable> Read(TextReader reader, IReadOnlyCollection<string> tables)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var wanted = new HashSet<string>(tables, StringComparer.Ordinal);
        var result = new Dictionary<string, SqlTable>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith(CreatePrefix, StringComparison.Ordinal))
            {
                string name = QuotedName(line, CreatePrefix.Length);
                if (wanted.Contains(name))
                    result[name] = new SqlTable(name, ReadColumns(reader));
            }
            else if (line.StartsWith(InsertPrefix, StringComparison.Ordinal))
            {
                string name = QuotedName(line, InsertPrefix.Length);
                if (result.TryGetValue(name, out SqlTable? table))
                    ParseInsert(line, InsertPrefix.Length + name.Length + 1, table);
            }
        }
        foreach (string name in wanted)
        {
            if (!result.ContainsKey(name))
                throw new InvalidDataException($"The dump has no table '{name}'.");
        }
        return result;
    }

    private static string QuotedName(string line, int start)
    {
        int end = line.IndexOf('`', start);
        if (end < 0)
            throw new InvalidDataException($"Unterminated table name: {line[..Math.Min(80, line.Length)]}");
        return line[start..end];
    }

    private static List<string> ReadColumns(TextReader reader)
    {
        var columns = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            string trimmed = line.TrimStart();
            if (!trimmed.StartsWith('`'))
                break;
            columns.Add(QuotedName(trimmed, 1));
        }
        return columns;
    }

    /// <summary>
    /// Parses one <c>INSERT</c> line from just after the table name. The line
    /// may name its columns (<c>(`a`, `b`) VALUES</c>); columns it leaves out
    /// are null in the row.
    /// </summary>
    internal static void ParseInsert(string line, int position, SqlTable table)
    {
        const string values = "VALUES ";
        int i = position;
        Expect(line, ref i, ' ');
        int[] targets;
        if (i < line.Length && line[i] == '(')
        {
            int close = line.IndexOf(')', i);
            if (close < 0)
                throw new InvalidDataException($"Unterminated column list for table '{table.Name}'.");
            targets = line[(i + 1)..close]
                .Split(',', StringSplitOptions.TrimEntries)
                .Select(column => table.Column(column.Trim('`')))
                .ToArray();
            i = close + 1;
            Expect(line, ref i, ' ');
        }
        else
        {
            targets = Enumerable.Range(0, table.Columns.Count).ToArray();
        }
        if (string.CompareOrdinal(line, i, values, 0, values.Length) != 0)
            throw new InvalidDataException($"Expected VALUES after table '{table.Name}'.");
        i += values.Length;
        int width = table.Columns.Count;
        while (true)
        {
            Expect(line, ref i, '(');
            var row = new object?[width];
            for (int c = 0; c < targets.Length; c++)
            {
                if (c > 0)
                    Expect(line, ref i, ',');
                row[targets[c]] = ReadValue(line, ref i);
            }
            Expect(line, ref i, ')');
            table.Rows.Add(row);
            if (i < line.Length && line[i] == ',')
            {
                i++;
                continue;
            }
            Expect(line, ref i, ';');
            return;
        }
    }

    private static void Expect(string line, ref int i, char expected)
    {
        if (i >= line.Length || line[i] != expected)
        {
            string found = i < line.Length ? line[i].ToString() : "end of line";
            throw new InvalidDataException($"Column {i}: expected '{expected}', found {found}.");
        }
        i++;
    }

    private static object? ReadValue(string line, ref int i)
    {
        if (i < line.Length && line[i] == '\'')
            return ReadString(line, ref i);
        int start = i;
        while (i < line.Length && line[i] != ',' && line[i] != ')')
            i++;
        ReadOnlySpan<char> token = line.AsSpan(start, i - start);
        if (token.SequenceEqual("NULL"))
            return null;
        if (long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long whole))
            return whole;
        return double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static string ReadString(string line, ref int i)
    {
        i++; // opening quote
        var sb = new StringBuilder();
        while (true)
        {
            if (i >= line.Length)
                throw new InvalidDataException("Unterminated string value.");
            char c = line[i++];
            if (c == '\'')
            {
                if (i < line.Length && line[i] == '\'')
                {
                    sb.Append('\'');
                    i++;
                    continue;
                }
                return sb.ToString();
            }
            if (c != '\\')
            {
                sb.Append(c);
                continue;
            }
            char escaped = line[i++];
            sb.Append(escaped switch
            {
                '0' => '\0',
                'b' => '\b',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                'Z' => '\u001A',
                _ => escaped,
            });
        }
    }
}
