using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;

namespace LogicCommon
{
    // SqlStatementStore.Build で組み立てた SQL 文とパラメータ。FromSqlRaw / ExecuteSqlRaw にそのまま渡す
    public sealed record SqlQuery(string Sql, object[] Parameters);

    // SQL ファイルの動的な部分を組み立てる。
    //  - "--#if 名前" から "--#endif" までの行は、その名前の引数が指定されているときだけ残す (入れ子可)。
    //    null・空文字・要素 0 件のコレクションは「指定なし」として扱う。
    //  - "@名前" は引数の値に置き換える。値は SQL 文に埋め込まず、EF Core の位置パラメータ ({0}, {1}, ...) にする。
    //  - コレクション (string 以外の IEnumerable) は要素数分のパラメータに展開する (IN 句用)。
    //    例: IN (@ids) -> IN ({0}, {1}, {2})
    //  - 要素がさらにコレクション (object[] など) の場合は、要素ごとに丸かっこで囲んだ行に展開する (VALUES 句用)。
    //    例: VALUES @rows (rows = [[1, "a"], [2, "b"]]) -> VALUES ({0}, {1}), ({2}, {3})
    //  - null は DBNull.Value にして渡す。
    //  - "--" で始まるコメント行の "@名前" は置き換えない。
    public static partial class SqlTemplate
    {
        [GeneratedRegex(@"^\s*--#if\s+(\w+)\s*$")]
        private static partial Regex IfRegex();

        [GeneratedRegex(@"^\s*--#endif\s*$")]
        private static partial Regex EndIfRegex();

        // 直前が @ や英数字の場合は対象外 (SQL Server の @@ROWCOUNT や、メールアドレスの一部など)
        [GeneratedRegex(@"(?<![@\w])@([A-Za-z_]\w*)")]
        private static partial Regex ParameterRegex();

        public static SqlQuery Render(string name, string template, object? parameters)
        {
            IReadOnlyDictionary<string, object?> values = ToDictionary(parameters);

            // 1. --#if / --#endif を評価して、残す行を決める
            List<string> lines = new();
            Stack<bool> conditions = new();
            int lineNo = 0;

            foreach (string rawLine in template.Split('\n'))
            {
                lineNo++;
                string line = rawLine.TrimEnd('\r');

                Match ifMatch = IfRegex().Match(line);
                if (ifMatch.Success)
                {
                    conditions.Push(IsSpecified(values, ifMatch.Groups[1].Value));
                    continue;
                }
                if (EndIfRegex().IsMatch(line))
                {
                    if (!conditions.TryPop(out _))
                    {
                        throw new FormatException($"{name}.sql の {lineNo} 行目: 対応する --#if がない --#endif です。");
                    }
                    continue;
                }
                if (conditions.All(c => c))
                {
                    lines.Add(line);
                }
            }
            if (conditions.Count != 0)
            {
                throw new FormatException($"{name}.sql: --#if に対応する --#endif がありません。");
            }

            // 2. @名前 を位置パラメータに置き換える (同じ名前が複数回出てきたら同じパラメータを使う)
            List<object> parameterValues = new();
            Dictionary<string, string> placeholders = new(StringComparer.OrdinalIgnoreCase);

            // 値をパラメータに追加し、位置パラメータの文字列 ({n}) を返す
            string AddValue(object? value)
            {
                string position = $"{{{parameterValues.Count}}}";
                parameterValues.Add(value ?? DBNull.Value);
                return position;
            }

            string ReplaceParameter(Match match)
            {
                string parameterName = match.Groups[1].Value;
                if (placeholders.TryGetValue(parameterName, out string? placeholder))
                {
                    return placeholder;
                }
                if (!values.TryGetValue(parameterName, out object? value))
                {
                    throw new ArgumentException($"{name}.sql: 引数 {parameterName} が指定されていません。", nameof(parameters));
                }

                if (IsCollection(value))
                {
                    List<string> items = new();
                    foreach (object? item in (IEnumerable)value!)
                    {
                        if (IsCollection(item))
                        {
                            // 行のリスト (VALUES 句用): 1 要素を (値, 値, ...) の 1 行にする
                            List<string> columns = new();
                            foreach (object? column in (IEnumerable)item!)
                            {
                                columns.Add(AddValue(column));
                            }
                            if (columns.Count == 0)
                            {
                                throw new ArgumentException(
                                    $"{name}.sql: 引数 {parameterName} に値が 0 個の行があります。", nameof(parameters));
                            }
                            items.Add($"({string.Join(", ", columns)})");
                        }
                        else
                        {
                            items.Add(AddValue(item));
                        }
                    }
                    if (items.Count == 0)
                    {
                        // IN () や VALUES だけの句は SQL として不正なので、空になりうる場合は --#if で囲んでもらう
                        throw new ArgumentException(
                            $"{name}.sql: 引数 {parameterName} が空のコレクションです。条件ごと外す場合は --#if {parameterName} で囲んでください。",
                            nameof(parameters));
                    }
                    placeholder = string.Join(", ", items);
                }
                else
                {
                    placeholder = AddValue(value);
                }

                placeholders[parameterName] = placeholder;
                return placeholder;
            }

            IEnumerable<string> renderedLines = lines.Select(line =>
                line.TrimStart().StartsWith("--", StringComparison.Ordinal)
                    ? line
                    : ParameterRegex().Replace(line, ReplaceParameter));

            return new SqlQuery(string.Join('\n', renderedLines).Trim(), parameterValues.ToArray());
        }

        // null・空文字・要素 0 件のコレクションは「指定なし」
        private static bool IsSpecified(IReadOnlyDictionary<string, object?> values, string name)
        {
            if (!values.TryGetValue(name, out object? value) || value is null)
            {
                return false;
            }
            if (value is string s)
            {
                return s.Length > 0;
            }
            if (IsCollection(value))
            {
                return ((IEnumerable)value).GetEnumerator().MoveNext();
            }
            return true;
        }

        private static bool IsCollection(object? value) =>
            value is IEnumerable and not string and not byte[];

        // 匿名型 (new { ids, dateFrom }) または IDictionary<string, object> を名前→値の辞書にする
        private static IReadOnlyDictionary<string, object?> ToDictionary(object? parameters)
        {
            if (parameters is null)
            {
                return new Dictionary<string, object?>();
            }
            if (parameters is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                return pairs.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
            }
            return parameters.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(p => p.Name, p => p.GetValue(parameters), StringComparer.OrdinalIgnoreCase);
        }
    }
}
