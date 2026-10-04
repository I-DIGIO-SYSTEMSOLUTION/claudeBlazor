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
        Create("default", "標準", "#7A4171", "#C270B3"),

        // MudBlazor.Colors (Material Design の色見本) の全色。ライトは Darken3、ダークは Lighten1 を使う
        // (green / blue / orange は以前からあるテーマのため、保存済みの Cookie が使えるようキーと色を変えない)
        Create("red", "レッド", Colors.Red.Darken3, Colors.Red.Lighten1),
        Create("pink", "ピンク", Colors.Pink.Darken3, Colors.Pink.Lighten1),
        Create("purple", "パープル", Colors.Purple.Darken3, Colors.Purple.Lighten1),
        Create("deep-purple", "ディープパープル", Colors.DeepPurple.Darken3, Colors.DeepPurple.Lighten1),
        Create("indigo", "インディゴ", Colors.Indigo.Darken3, Colors.Indigo.Lighten1),
        Create("blue", "ブルー", Colors.Blue.Darken3, Colors.Blue.Lighten1),
        Create("light-blue", "ライトブルー", Colors.LightBlue.Darken3, Colors.LightBlue.Lighten1),
        Create("cyan", "シアン", Colors.Cyan.Darken3, Colors.Cyan.Lighten1),
        Create("teal", "ティール", Colors.Teal.Darken3, Colors.Teal.Lighten1),
        Create("green", "グリーン", Colors.Green.Darken3, Colors.Green.Lighten1),
        Create("light-green", "ライトグリーン", Colors.LightGreen.Darken3, Colors.LightGreen.Lighten1),
        Create("lime", "ライム", Colors.Lime.Darken3, Colors.Lime.Lighten1),
        Create("yellow", "イエロー", Colors.Yellow.Darken3, Colors.Yellow.Lighten1),
        Create("amber", "アンバー", Colors.Amber.Darken3, Colors.Amber.Lighten1),
        Create("orange", "オレンジ", Colors.Orange.Darken4, Colors.Orange.Lighten1),
        Create("deep-orange", "ディープオレンジ", Colors.DeepOrange.Darken3, Colors.DeepOrange.Lighten1),
        Create("brown", "ブラウン", Colors.Brown.Darken3, Colors.Brown.Lighten1),
        Create("gray", "グレー", Colors.Gray.Darken3, Colors.Gray.Lighten1),
        Create("blue-gray", "ブルーグレー", Colors.BlueGray.Darken3, Colors.BlueGray.Lighten1),
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
