using System.Globalization;

namespace DenoPlayer.Helpers;

/// <summary>
/// Windows 탐색기 스타일 자연 정렬. "file2" &lt; "file10".
/// StrCmpLogicalW가 적합하지만 cross-arch 안전성을 위해 순수 C#로 구현.
/// </summary>
public sealed class NaturalStringComparer : IComparer<string>
{
    public static readonly NaturalStringComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int ix = 0, iy = 0;
        while (ix < x.Length && iy < y.Length)
        {
            var cx = x[ix];
            var cy = y[iy];
            if (char.IsDigit(cx) && char.IsDigit(cy))
            {
                int sx = ix, sy = iy;
                while (ix < x.Length && char.IsDigit(x[ix])) ix++;
                while (iy < y.Length && char.IsDigit(y[iy])) iy++;
                var nx = x.AsSpan(sx, ix - sx).TrimStart('0');
                var ny = y.AsSpan(sy, iy - sy).TrimStart('0');
                if (nx.Length != ny.Length) return nx.Length - ny.Length;
                var cmp = nx.SequenceCompareTo(ny);
                if (cmp != 0) return cmp;
            }
            else
            {
                var cmp = string.Compare(
                    x, ix, y, iy, 1,
                    CultureInfo.CurrentCulture,
                    CompareOptions.IgnoreCase);
                if (cmp != 0) return cmp;
                ix++; iy++;
            }
        }
        return x.Length - y.Length;
    }
}
