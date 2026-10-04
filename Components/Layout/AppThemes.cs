using MudBlazor;
using MudBlazor.Utilities;

namespace MyBlazorApp.Components.Layout;

/// <summary>
/// テーマメニューで選べるテーマの一覧。
/// Primary（ボタンなど）と AppBar の色を切り替える。ダークモード用には明るめの色を使う。
/// </summary>
public static class AppThemes
{
    public sealed record Item(string Key, string Name, MudTheme Theme)
    {
        /// <summary>メニューに表示する見本の色</summary>
        public string SampleColor => Theme.PaletteLight.Primary.ToString(MudColorOutputFormats.Hex);
    }

    public static readonly IReadOnlyList<Item> All =
    [
        new("default", "標準", WithContrastText(new MudTheme())),
        Create("green", "グリーン", "#2E7D32", "#66BB6A"),
        Create("blue", "ブルー", "#1565C0", "#42A5F5"),
        Create("orange", "オレンジ", "#E65100", "#FFA726"),
    ];

    /// <summary>キーに一致するテーマを返す。見つからなければ標準テーマ。</summary>
    public static Item Find(string? key) => All.FirstOrDefault(t => t.Key == key) ?? All[0];

    private static Item Create(string key, string name, string lightPrimary, string darkPrimary) =>
        new(key, name, WithContrastText(new MudTheme
        {
            PaletteLight = new PaletteLight
            {
                Primary = lightPrimary,
                AppbarBackground = lightPrimary,
            },
            PaletteDark = new PaletteDark
            {
                Primary = darkPrimary,
            },
        }));

    /// <summary>
    /// Primary の上に置く文字色 (ボタンの文字、表のヘッダーの文字など) と AppBar の文字色 (AppBar 上のボタンなど) を、
    /// 背景が濃い色なら白、薄い色なら黒にする。MudBlazor は既定で白固定のため、明るい色だと文字が読みにくくなる。
    /// </summary>
    private static MudTheme WithContrastText(MudTheme theme)
    {
        foreach (Palette palette in new Palette[] { theme.PaletteLight, theme.PaletteDark })
        {
            palette.PrimaryContrastText = ContrastText(palette.Primary);
            palette.AppbarText = ContrastText(palette.AppbarBackground);
        }
        return theme;
    }

    // WCAG の相対輝度で、白と黒のうちコントラストが高い方を選ぶ
    private static string ContrastText(MudColor background)
    {
        static double Linear(byte c)
        {
            double v = c / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        double luminance = 0.2126 * Linear(background.R) + 0.7152 * Linear(background.G) + 0.0722 * Linear(background.B);
        double contrastWithWhite = 1.05 / (luminance + 0.05);
        double contrastWithBlack = (luminance + 0.05) / 0.05;
        return contrastWithWhite >= contrastWithBlack ? "#FFFFFF" : "#000000";
    }
}
