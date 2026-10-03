#nullable disable

using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace LogicCommon
{

    // 自然順 比較クラス
    public class NaturalComparer : IComparer<string>
    {
        // WIN32API
        private static class NativeMethods
        {
            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
            public static extern int StrCmpLogicalW(string psz1, string psz2);
        }
        private NaturalComparer()
        {
        }

#nullable enable
        static NaturalComparer? _Natural = null;
        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            // 文字列比較：自然順 - ここを変更することで任意の照合規則とすることが可能
            return NativeMethods.StrCmpLogicalW(x, y);
        }
#nullable disable

        public static NaturalComparer Natural
        {
            get
            {
                if (_Natural == null)
                    _Natural = new NaturalComparer();
                return _Natural;
            }
        }
    }

    public class CommonLogic
    {
        public static string EXTENTION_TEXT = ".txt";
        public static string EXTENTION_EXCEL = ".xlsx";
        public static string EXTENTION_CSV = ".csv";
        public static string EXTENTION_TSV = ".tsv";
        public static string EXTENTION_XML = ".xml";
        public static string EXTENTION_TIFF = ".tiff";
        public static string EXTENTION_ZIP = ".zip";
        public static string EXTENTION_LOG = ".log";

        private static readonly log4net.ILog logger =
            log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// ファイルが書き込み可能か、調べる
        /// ※ファイルが無い時は「書き込み不可」を返す。
        /// </summary>
        /// <param name="filePath">ファイル名</param>
        /// <returns>true: 書き込み可能 false: 書き込み不可か、ファイルが無い</returns>
        public static bool IsWritableFile(string filePath)
        {
            try
            {

                System.IO.FileInfo fi = new System.IO.FileInfo(filePath);
                fi.Refresh();

                if (!fi.Exists)
                {
                    return false;
                }

                // 書き込みモードでファイルを開けるか確認
                using (FileStream fp = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    // 開ける
                    return true;
                }
            }
            catch (DirectoryNotFoundException)
            {
                // ディレクトリがない
                return false;
            }
            catch (FileNotFoundException)
            {
                // ファイルがない
                return false;
            }
            catch (IOException)
            {
                // 開けない(ロック中)
                // これが純粋なファイルロック状態
                return false;
            }
            catch (Exception)
            {
                // その他例外
                return false;
            }
        }



        // 最初の数字を検索
        public static int GetStringFirestNumberIndex(string str)
        {
            // 半角全角問わず数字(0～9)
            string pattern = @"(\d+$)";

            System.Text.RegularExpressions.MatchCollection mc = System.Text.RegularExpressions.Regex.Matches(str, pattern);

            // １つめだけわかればいいとき
            if (mc.Count > 0)
            {
                // 一致がある場合、最初に一致した位置を取得する
                int firstIndex = mc[0].Index;
                return firstIndex;
            }
            else
            {
                return -1;
            }
        }

        //文字列の最後0の数値内容と0までの文字数を返却
        public static (int, string) GetStringLastZeroIndex(string str)
        {
            // 半角全角問わず数字(0～9)
            string pattern = @"(\d+$)";

            System.Text.RegularExpressions.MatchCollection mc = System.Text.RegularExpressions.Regex.Matches(str, pattern);

            if (mc.Count > 0)
            {
                // 半角全角問わず数字(0～9)
                pattern = @"(^[1-9]*0+)";
                int idx = mc[mc.Count - 1].Index;
                string val = mc[mc.Count - 1].Value;
                mc = System.Text.RegularExpressions.Regex.Matches(mc[mc.Count - 1].Value, pattern);

                if (mc.Count > 0)
                {
                    idx = mc[0].Index + idx;
                    // 0からのINDEX
                    return (idx, mc[0].Value);
                }
                else
                {
                    // 0からのINDEX
                    return (idx, val);
                }
            }
            else
            {
                return (-1, string.Empty);
            }
        }

        // 文字列の最後の数値内容と文字までの文字数を返却
        public static (int, string) GetStringLastCharIndex(string str)
        {
            // 半角全角問わず数字(0～9)
            string pattern = @"(\d+$)";

            System.Text.RegularExpressions.MatchCollection mc = System.Text.RegularExpressions.Regex.Matches(str, pattern);

            if (mc.Count > 0)
            {
                // 0からのINDEX
                return (mc[mc.Count - 1].Index, mc[mc.Count - 1].Value);
            }
            else
            {
                return (-1, string.Empty);
            }
        }

        // 文字列の文字までの文字列を返却
        public static string GetStringFirstChar(string str)
        {
            (int idx, string dmy) = GetStringLastCharIndex(str);
            if (idx == -1)
            {
                return string.Empty;
            }

            return str.Substring(0, idx);
        }


        // 文字列を比較して先頭から一致した文字列を返却
        public static string GetStringFirstMatchChars(string source, string target)
        {
            int chkLength;
            if (source.Length >= target.Length)
            {
                chkLength = target.Length;
            }
            else
            {
                chkLength = source.Length;
            }

            for (int j = chkLength; j > 0; j--)
            {
                if (source.Substring(0, j).Equals(target.Substring(0, j)))
                {
                    return source.Substring(0, j);
                }
            }

            return "";
        }

        // 引数の文字列から日付のFomratがあれば現在の日付を入れて返却する
        public static string ReplaceDateTime(string target)
        {
            DateTime dt = DateTime.Now;
            return ReplaceDateTime(target, dt);
        }


        // 引数の文字列から日付のFomratがあれば現在の日付を入れて返却する
        public static string ReplaceDateTime(string target, DateTime dt)
        {
            string result = target;
            Dictionary<string, string> bkRep = null;
            Dictionary<string, string> afRep = null;

            bool fFlg = false;

            if (target.Contains("${"))
            {
                bkRep = new();
                bkRep.Add("${yy}", "${1111}");
                bkRep.Add("${yyyy}", "${2222}");
                bkRep.Add("${MM}", "${3333}");
                bkRep.Add("${dd}", "${4444}");
                bkRep.Add("${HH}", "${5555}");
                bkRep.Add("${mm}", "${6666}");
                bkRep.Add("${ss}", "${7777}");

                foreach (KeyValuePair<string, string> ele in bkRep)
                {
                    if (target.Contains(ele.Key))
                    {
                        target = target.Replace(ele.Key, ele.Value);
                        fFlg = true;
                    }
                }
            }

            StringBuilder tb = new(target);

            if (target.Contains("yy") || target.Contains("MM") || target.Contains("dd") ||
                target.Contains("HH") || target.Contains("mm") || target.Contains("ss"))
            {
                System.Text.RegularExpressions.MatchCollection mc = System.Text.RegularExpressions.Regex.Matches(target, "(yy|MM|dd|HH|mm|ss)");

                // 一致がある場合
                if (mc.Count > 0)
                {
                    int startIdx = 0;
                    int endIdx = 0;
                    StringBuilder sb = new StringBuilder();

                    for (int i = 0; i < mc.Count; i++)
                    {
                        if (endIdx < mc[i].Index && endIdx > 0)
                        {
                            string rep = dt.ToString(sb.ToString());
                            int k = 0;
                            for (int j = startIdx; j < endIdx; j++)
                            {
                                tb[j] = rep[k++];
                            }
                            startIdx = 0;
                            endIdx = 0;
                            sb.Clear();
                        }

                        // 一致がある場合、最初に一致した位置を取得する
                        if (startIdx == 0 && endIdx == 0)
                        {
                            startIdx = mc[i].Index;
                        }

                        endIdx = mc[i].Index + mc[i].Length;
                        sb.Append(mc[i].Value);

                    }

                    if (sb.Length > 0)
                    {
                        string rep = dt.ToString(sb.ToString());
                        int k = 0;
                        for (int j = startIdx; j < endIdx; j++)
                        {
                            tb[j] = rep[k++];
                        }
                    }
                }
            }

            target = tb.ToString();

            if (fFlg)
            {
                afRep = new();
                afRep.Add("${1111}", "yy");
                afRep.Add("${2222}", "yyyy");
                afRep.Add("${3333}", "MM");
                afRep.Add("${4444}", "dd");
                afRep.Add("${5555}", "HH");
                afRep.Add("${6666}", "mm");
                afRep.Add("${7777}", "ss");

                foreach (KeyValuePair<string, string> ele in afRep)
                {
                    if (target.Contains(ele.Key))
                    {
                        target = target.Replace(ele.Key, ele.Value);
                    }
                }
            }

            return target;
        }

        // 引数の文字列から日付のFomratがあれば現在の日付を入れて返却する
        public static string ReplaceDateTimeMinimum(string target, DateTime dt)
        {
            string result = target;
            Dictionary<string, string> bkRep = null;
            Dictionary<string, string> afRep = null;

            bool fFlg = false;

            if (target.Contains("${"))
            {
                bkRep = new();
                bkRep.Add("${yyyy}", "${2222}");
                bkRep.Add("${MM}", "${3333}");
                bkRep.Add("${dd}", "${4444}");
                bkRep.Add("${HH}", "${5555}");
                bkRep.Add("${mm}", "${6666}");
                bkRep.Add("${ss}", "${7777}");

                foreach (KeyValuePair<string, string> ele in bkRep)
                {
                    if (target.Contains(ele.Key))
                    {
                        target = target.Replace(ele.Key, ele.Value);
                        fFlg = true;
                    }
                }
            }

            StringBuilder tb = new(target);

            if (target.Contains("yyyy") || target.Contains("yyyyMM") || target.Contains("MMdd") ||
                target.Contains("yy-") || target.Contains("MM-") || target.Contains("-dd") ||
                target.Contains("HH-") || target.Contains("mm-") || target.Contains("-ss") ||
                target.Contains("yy/") || target.Contains("MM/") || target.Contains("/dd") ||
                target.Contains("yy_") || target.Contains("MM_") || target.Contains("_dd") ||
                target.Contains("HH_") || target.Contains("_mm") || target.Contains("mm_") || target.Contains(":ss") ||
                target.Contains("HH:") || target.Contains(":mm") || target.Contains("mm:") || target.Contains(":ss") ||
                target.Contains("HHmm") || target.Contains("HHmmss"))
            {
                System.Text.RegularExpressions.MatchCollection mc = System.Text.RegularExpressions.Regex.Matches(target, "(yy|MM|dd|HH|mm|ss)");

                // 一致がある場合
                if (mc.Count > 0)
                {
                    int startIdx = 0;
                    int endIdx = 0;
                    StringBuilder sb = new StringBuilder();

                    for (int i = 0; i < mc.Count; i++)
                    {
                        if (endIdx < mc[i].Index && endIdx > 0)
                        {
                            string rep = dt.ToString(sb.ToString());
                            int k = 0;
                            for (int j = startIdx; j < endIdx; j++)
                            {
                                tb[j] = rep[k++];
                            }
                            startIdx = 0;
                            endIdx = 0;
                            sb.Clear();
                        }

                        // 一致がある場合、最初に一致した位置を取得する
                        if (startIdx == 0 && endIdx == 0)
                        {
                            startIdx = mc[i].Index;
                        }

                        endIdx = mc[i].Index + mc[i].Length;
                        sb.Append(mc[i].Value);

                    }

                    if (sb.Length > 0)
                    {
                        string rep = dt.ToString(sb.ToString());
                        int k = 0;
                        for (int j = startIdx; j < endIdx; j++)
                        {
                            tb[j] = rep[k++];
                        }
                    }
                }
            }

            target = tb.ToString();

            if (fFlg)
            {
                afRep = new();
                afRep.Add("${1111}", "yy");
                afRep.Add("${2222}", "yyyy");
                afRep.Add("${3333}", "MM");
                afRep.Add("${4444}", "dd");
                afRep.Add("${5555}", "HH");
                afRep.Add("${6666}", "mm");
                afRep.Add("${7777}", "ss");

                foreach (KeyValuePair<string, string> ele in afRep)
                {
                    if (target.Contains(ele.Key))
                    {
                        target = target.Replace(ele.Key, ele.Value);
                    }
                }
            }

            return target;
        }

        public static (Encoding, byte[]) GetJpEncoding(string file, long maxSize = 50 * 1024)
        //ファイルパス、最大読み取りバイト数
        {
            try
            {
                if (!File.Exists(file))//ファイルが存在しない場合
                {
                    return (null, null);
                }
                else if (new FileInfo(file).Length == 0)//ファイルサイズが0の場合
                {
                    return (null, null);
                }
                else//ファイルが存在しファイルサイズが0でない場合
                {
                    //バイナリ読み込み
                    byte[] bytes = null;
                    bool readAll = false;
                    using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        long size = fs.Length;

                        if (size <= maxSize)
                        {
                            bytes = new byte[size];
                            _ = fs.Read(bytes, 0, (int)size);
                            readAll = true;
                        }
                        else
                        {
                            bytes = new byte[maxSize];
                            _ = fs.Read(bytes, 0, (int)maxSize);
                        }
                    }

                    //判定
                    return GetJpEncoding(bytes, readAll);
                }
            }
            catch
            {
                return (null, null);
            }
        }

        public static (Encoding, byte[]) GetJpEncoding(byte[] bytes, bool readAll = false)
        {
            int len = bytes.Length;

            //BOM判定
            if (len >= 2 && bytes[0] == 0xfe && bytes[1] == 0xff)//UTF-16BE
            {
                byte[] rtnByte = new byte[2];
                rtnByte[0] = 0xfe;
                rtnByte[1] = 0xff;
                return (new UnicodeEncoding(true, true), rtnByte);
            }
            else if (len >= 2 && bytes[0] == 0xff && bytes[1] == 0xfe)//UTF-16LE
            {
                byte[] rtnByte = new byte[2];
                rtnByte[0] = 0xff;
                rtnByte[1] = 0xfe;

                return (new UnicodeEncoding(false, true), rtnByte);
            }
            else if (len >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)//UTF-8
            {
                byte[] rtnByte = new byte[3];
                rtnByte[0] = 0xEF;
                rtnByte[1] = 0xBB;
                rtnByte[1] = 0xBF;

                return (new UTF8Encoding(true, true), rtnByte);
            }
            else if (len >= 3 && bytes[0] == 0x2b && bytes[1] == 0x2f && bytes[2] == 0x76)//UTF-7
            {
                byte[] rtnByte = new byte[3];
                rtnByte[0] = 0x2B;
                rtnByte[1] = 0x2F;
                rtnByte[1] = 0x76;

                // UTF7は使われていないはずなので警告が出る
                return (Encoding.UTF7, rtnByte);
            }
            else if (len >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xfe && bytes[3] == 0xff)//UTF-32BE
            {
                byte[] rtnByte = new byte[4];
                rtnByte[0] = 0x00;
                rtnByte[1] = 0x00;
                rtnByte[2] = 0xFE;
                rtnByte[3] = 0xFF;

                return (new UTF32Encoding(true, true), rtnByte);
            }
            else if (len >= 4 && bytes[0] == 0xff && bytes[1] == 0xfe && bytes[2] == 0x00 && bytes[3] == 0x00)//UTF-32LE
            {
                byte[] rtnByte = new byte[4];
                rtnByte[0] = 0xFF;
                rtnByte[1] = 0xFE;
                rtnByte[2] = 0x00;
                rtnByte[3] = 0x00;

                return (new UTF32Encoding(false, true), rtnByte);
            }

            //文字コード判定と日本語の文章らしさをまとめて確認
            //Shift_JIS判定用
            bool sjis = true;         //すべてのバイトがShift_JISで使用するバイト範囲かどうか
            bool sjis_2ndbyte = false;//次回の判定がShift_JISの2バイト目の判定かどうか
            bool sjis_kana = false;   //かな判定用
            bool sjis_kanji = false;  //常用漢字判定用
            int counter_sjis = 0;     //Shift_JISらしさ

            //UTF-8判定用
            bool utf8 = true;            //すべてのバイトがUTF-8で使用するバイト範囲かどうか
            bool utf8_multibyte = false; //次回の判定がUTF-8の2バイト目以降の判定かどうか
            bool utf8_kana_kanji = false;//かな・常用漢字判定用
            int counter_utf8 = 0;        //UTF-8らしさ
            int counter_utf8_multibyte = 0;

            //EUC-JP判定用
            bool eucjp = true;            //すべてのバイトがEUC-JPで使用するバイト範囲かどうか
            bool eucjp_multibyte = false; //次回の判定がEUC-JPの2バイト目以降の判定かどうか
            bool eucjp_kana_kanji = false;//かな・常用漢字判定用
            int counter_eucjp = 0;        //EUC-JPらしさ
            int counter_eucjp_multibyte = 0;

            for (int i = 0; i < len; i++)
            {
                byte b = bytes[i];

                //Shift_JIS判定
                if (sjis)
                {
                    if (!sjis_2ndbyte)
                    {
                        if (b == 0x0D                   //CR
                            || b == 0x0A                //LF
                            || b == 0x09                //tab
                            || (0x20 <= b && b <= 0x7E))//ASCII文字
                        {
                            counter_sjis++;
                        }
                        else if ((0x81 <= b && b <= 0x9F) || (0xE0 <= b && b <= 0xFC))//Shift_JISの2バイト文字の1バイト目の場合
                        {
                            //2バイト目の判定を行う
                            sjis_2ndbyte = true;

                            if (0x82 <= b && b <= 0x83)//Shift_JISのかな
                            {
                                sjis_kana = true;
                            }
                            else if ((0x88 <= b && b <= 0x9F) || (0xE0 <= b && b <= 0xE3) || b == 0xE6 || b == 0xE7)//Shift_JISの常用漢字
                            {
                                sjis_kanji = true;
                            }
                        }
                        else if (0xA1 <= b && b <= 0xDF)//Shift_JISの1バイト文字の場合(半角カナ)
                        {
                            ;
                        }
                        else if (0x00 <= b && b <= 0x7F)//ASCIIコード
                        {
                            ;
                        }
                        else
                        {
                            //Shift_JISでない
                            counter_sjis = 0;
                            sjis = false;
                        }
                    }
                    else
                    {
                        if ((0x40 <= b && b <= 0x7E) || (0x80 <= b && b <= 0xFC))//Shift_JISの2バイト文字の2バイト目の場合
                        {
                            if (sjis_kana && 0x40 <= b && b <= 0xF1)//Shift_JISのかな
                            {
                                counter_sjis += 2;
                            }
                            else if (sjis_kanji && 0x40 <= b && b <= 0xFC && b != 0x7F)//Shift_JISの常用漢字
                            {
                                counter_sjis += 2;
                            }

                            sjis_2ndbyte = sjis_kana = sjis_kanji = false;
                        }
                        else
                        {
                            //Shift_JISでない
                            counter_sjis = 0;
                            sjis = false;
                        }
                    }
                }

                //UTF-8判定
                if (utf8)
                {
                    if (!utf8_multibyte)
                    {
                        if (b == 0x0D                   //CR
                            || b == 0x0A                //LF
                            || b == 0x09                //tab
                            || (0x20 <= b && b <= 0x7E))//ASCII文字
                        {
                            counter_utf8++;
                        }
                        else if (0xC2 <= b && b <= 0xDF)//2バイト文字の場合
                        {
                            utf8_multibyte = true;
                            counter_utf8_multibyte = 1;
                        }
                        else if (0xE0 <= b && b <= 0xEF)//3バイト文字の場合
                        {
                            utf8_multibyte = true;
                            counter_utf8_multibyte = 2;

                            if (b == 0xE3 || (0xE4 <= b && b <= 0xE9))
                            {
                                utf8_kana_kanji = true;//かな・常用漢字
                            }
                        }
                        else if (0xF0 <= b && b <= 0xF3)//4バイト文字の場合
                        {
                            utf8_multibyte = true;
                            counter_utf8_multibyte = 3;
                        }
                        else if (0x00 <= b && b <= 0x7F)//ASCIIコード
                        {
                            ;
                        }
                        else
                        {
                            //UTF-8でない
                            counter_utf8 = 0;
                            utf8 = false;
                        }
                    }
                    else
                    {
                        if (counter_utf8_multibyte > 0)
                        {
                            counter_utf8_multibyte--;

                            if (b < 0x80 || 0xBF < b)
                            {
                                //UTF-8でない
                                counter_utf8 = 0;
                                utf8 = false;
                            }
                        }

                        if (utf8 && counter_utf8_multibyte == 0)
                        {
                            if (utf8_kana_kanji)
                            {
                                counter_utf8 += 3;
                            }
                            utf8_multibyte = utf8_kana_kanji = false;
                        }
                    }
                }

                //EUC-JP判定
                if (eucjp)
                {
                    if (!eucjp_multibyte)
                    {
                        if (b == 0x0D                   //CR
                            || b == 0x0A                //LF
                            || b == 0x09                //tab
                            || (0x20 <= b && b <= 0x7E))//ASCII文字
                        {
                            counter_eucjp++;
                        }
                        else if (b == 0x8E || (0xA1 <= b && b <= 0xA8) || b == 0xAD || (0xB0 <= b && b <= 0xFE))//2バイト文字の場合
                        {
                            eucjp_multibyte = true;
                            counter_eucjp_multibyte = 1;

                            if (b == 0xA4 || b == 0xA5 || (0xB0 <= b && b <= 0xEE))
                            {
                                eucjp_kana_kanji = true;
                            }
                        }
                        else if (b == 0x8F)//3バイト文字の場合
                        {
                            eucjp_multibyte = true;
                            counter_eucjp_multibyte = 2;
                        }
                        else if (0x00 <= b && b <= 0x7F)//ASCIIコード
                        {
                            ;
                        }
                        else
                        {
                            //EUC-JPでない
                            counter_eucjp = 0;
                            eucjp = false;
                        }
                    }
                    else
                    {
                        if (counter_eucjp_multibyte > 0)
                        {
                            counter_eucjp_multibyte--;

                            if (b < 0xA1 || 0xFE < b)
                            {
                                //EUC-JPでない
                                counter_eucjp = 0;
                                eucjp = false;
                            }
                        }

                        if (eucjp && counter_eucjp_multibyte == 0)
                        {
                            if (eucjp_kana_kanji)
                            {
                                counter_eucjp += 2;
                            }
                            eucjp_multibyte = eucjp_kana_kanji = false;
                        }
                    }
                }

                //ISO-2022-JP
                if (b == 0x1B)
                {
                    if ((i + 2 < len && bytes[i + 1] == 0x24 && bytes[i + 2] == 0x40)                                                                           //1B-24-40
                        || (i + 2 < len && bytes[i + 1] == 0x24 && bytes[i + 2] == 0x42)                                                                        //1B-24-42
                        || (i + 2 < len && bytes[i + 1] == 0x28 && bytes[i + 2] == 0x4A)                                                                        //1B-28-4A
                        || (i + 2 < len && bytes[i + 1] == 0x28 && bytes[i + 2] == 0x49)                                                                        //1B-28-49
                        || (i + 2 < len && bytes[i + 1] == 0x28 && bytes[i + 2] == 0x42)                                                                        //1B-28-42
                        || (i + 3 < len && bytes[i + 1] == 0x24 && bytes[i + 2] == 0x48 && bytes[i + 3] == 0x44)                                                //1B-24-48-44
                        || (i + 3 < len && bytes[i + 1] == 0x24 && bytes[i + 2] == 0x48 && bytes[i + 3] == 0x4F)                                                //1B-24-48-4F
                        || (i + 3 < len && bytes[i + 1] == 0x24 && bytes[i + 2] == 0x48 && bytes[i + 3] == 0x51)                                                //1B-24-48-51
                        || (i + 3 < len && bytes[i + 1] == 0x24 && bytes[i + 2] == 0x48 && bytes[i + 3] == 0x50)                                                //1B-24-48-50
                        || (i + 5 < len && bytes[i + 1] == 0x26 && bytes[i + 2] == 0x40 && bytes[i + 3] == 0x1B && bytes[i + 4] == 0x24 && bytes[i + 5] == 0x42)//1B-26-40-1B-24-42
                    )
                    {
                        return (Encoding.GetEncoding(50220), null);//iso-2022-jp
                    }
                }
            }


            if (sjis || utf8 || eucjp)
            {
                //日本語らしさの最大値確認
                int max_value = counter_eucjp;
                if (counter_sjis > max_value)
                {
                    max_value = counter_sjis;
                }
                if (counter_utf8 > max_value)
                {
                    max_value = counter_utf8;
                }

                //文字コード判定
                if (max_value == counter_utf8)
                {
                    return (new UTF8Encoding(false, true), null);//utf8
                }
                else if (max_value == counter_sjis)
                {
                    return (Encoding.GetEncoding(932), null);//ShiftJIS
                }
                else
                {
                    return (Encoding.GetEncoding(51932), null);//EUC-JP
                }
            }
            else
            {
                return (Encoding.ASCII, null);
            }
        }

        /// <summary>
        /// BOMを調べて、文字コードを判別する。
        /// </summary>
        /// <param name="bytes">文字コードを調べるデータ。</param>
        /// <returns>BOMが見つかった時は、対応するEncodingオブジェクト。
        /// 見つからなかった時は、null。</returns>
        public static (Encoding, byte[]) DetectEncodingFromBOM(byte[] bytes)
        {
            int len = bytes.Length;

            if (bytes.Length < 2)
            {
                return (null, null);
            }
            if (len >= 2 && bytes[0] == 0xfe && bytes[1] == 0xff)
            {
                //UTF-16 BE
                return (new UnicodeEncoding(true, true), new byte[] { 0xfe, 0xff });
            }
            if (bytes[0] == 0xff && bytes[1] == 0xfe)
            {
                if (4 <= len &&
                    bytes[2] == 0x00 && bytes[3] == 0x00)
                {
                    //UTF-32 LE
                    return (new UTF32Encoding(false, true), new byte[] { 0xff, 0xfe, 0x00, 0x00 });
                }
                //UTF-16 LE
                return (new UnicodeEncoding(false, true), new byte[] { 0xff, 0xfe });
            }
            if (bytes.Length < 3)
            {
                return (null, null);
            }
            if (bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf)
            {
                //UTF-8
                return (new UTF8Encoding(true, true), new byte[] { 0xef, 0xbb, 0xbf });
            }
            if (bytes.Length < 4)
            {
                return (null, null);
            }
            if (bytes[0] == 0x00 && bytes[1] == 0x00 &&
                bytes[2] == 0xfe && bytes[3] == 0xff)
            {
                //UTF-32 BE
                return (new UTF32Encoding(true, true), new byte[] { 0x00, 0x00, 0xfe, 0xff });
            }

            return (null, null);
        }

        // 
        public static (bool, string) FileAppendDirectory(string fromDir, string toPath, bool FromFileDeleteFlg)
        {
            bool bres = false;
            string mes = string.Empty;
            try
            {
                DirectoryInfo di = new DirectoryInfo(fromDir);
                di.Refresh();

                List<System.IO.FileInfo> fi = di.GetFiles("*", System.IO.SearchOption.AllDirectories)
                    .OrderBy(x => x.Name, NaturalComparer.Natural).ToList();

                (bres, mes) = FileAppendDirectory(fromDir, fi, toPath, FromFileDeleteFlg);

            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                logger.Error(ex.StackTrace);
                return (false, ex.Message);
            }

            return (bres, mes);

        }

        public static (bool, string) FileAppendDirectory(string fromDir, List<System.IO.FileInfo> fi, string toPath, bool FromFileDeleteFlg)
        {
            try
            {

                DirectoryInfo di = new DirectoryInfo(fromDir);
                di.Refresh();

                if (!di.Exists)
                {
                    return (false, "追記元フォルダが存在しません。");
                }

                foreach (System.IO.FileInfo ele in fi)
                {
                    (bool bres, string mes) = FileAppend(ele.FullName, toPath);
                    if (!bres)
                    {
                        return (false, mes);
                    }
                }

                if (FromFileDeleteFlg)
                {
                    foreach (FileInfo file in fi)
                    {
                        FileShred.File(file.FullName);
                    }

                    foreach (DirectoryInfo dir in di.EnumerateDirectories())
                    {
                        dir.Delete();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                logger.Error(ex.StackTrace);
                return (false, ex.Message);
            }

            return (true, string.Empty);

        }


        //2つのファイルを1つにまとめる
        //BOM付きは未対応 -> 対応
        public static (bool, string) FileAppend(string fromPath, string toPath)
        {
            try
            {
                System.IO.FileInfo fi = new System.IO.FileInfo(toPath);
                fi.Refresh();
                bool existFlg = false;
                if(fi.Exists)
                {
                    existFlg = true;
                }
                fi = null;

                using (FileStream fsb = new(fromPath,
                                              FileMode.Open,
                                              FileAccess.Read,
                                              FileShare.ReadWrite))
                {

                    using (FileStream fsa = new(toPath,
                                                  FileMode.Append,
                                                  FileAccess.Write,
                                                  FileShare.ReadWrite))
                    {
                        long fileSize = fsb.Length; // ファイル長
                        long remain = fileSize;     // 読み込むべき残りのバイト数
                        long bufPos = 0;            // データ格納用配列内の追加位置
                        int bufsize = 40000;       // 一回のReadで読み込む最大バイト数
                                                   // バイト数を上げると処理は早くなるがメモリ消費量が大きくなる

                        byte[] bufC;
                        if (bufsize > fileSize)
                        {
                            bufC = new byte[fileSize];
                            _ = fsb.Read(bufC, 0, (int)fileSize);
                        }
                        else
                        {
                            bufC = new byte[bufsize];
                            _ = fsb.Read(bufC, 0, bufsize);
                        }

                        // BOM付きで元ファイルがある場合はBOMを除外
                        if (existFlg)
                        {
                            (Encoding encRes, byte[] bufBom) = GetJpEncoding(bufC, false);

                            fsb.Position = 0;

                            if (bufBom != null)
                            {
                                fsb.Position = bufBom.Length;
                                remain = fsb.Length - bufBom.Length;
                            }
                            else
                            {
                                fsb.Position = 0;
                            }
                        }
                        else
                        {
                            fsb.Position = 0;
                        }

                        while (remain > 0)
                        {
                            // bufsizeずつ読み込む
                            // 最後のバッファ調整処理
                            long remain2 = Math.Min(bufsize, remain);
                            byte[] bufR = new byte[remain2];
                            int readSize = fsb.Read(bufR, 0, (int)remain2);

                            bufPos = bufPos + readSize;
                            remain = remain - readSize;

                            fsa.Write(bufR, 0, bufR.Length);

                        }
                    }
                }
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                logger.Error(ex.StackTrace);

                return (false, ex.Message);
            }
        }

        /**
        //2つのファイルを1つにまとめる
        //X行目は除外する
        //BOM付きは未対応 -> 対応
        public static (bool, string) FileAppendRemoveFirstLine(string fromPath, string toPath, int removeLineCount, string appendFirstLine)
        {
            try
            {
                System.IO.FileInfo fi = new System.IO.FileInfo(toPath);
                fi.Refresh();
                bool existFlg = false;
                if (fi.Exists)
                {
                    existFlg = true;
                }
                fi = null;

                int lineCnt = 0;

                bool firstLineFlg = true;

                using (FileStream fsb = new(fromPath,
                                              FileMode.Open,
                                              FileAccess.Read,
                                              FileShare.ReadWrite))
                {

                    using (FileStream fsa = new(toPath,
                                                  FileMode.Append,
                                                  FileAccess.Write,
                                                  FileShare.ReadWrite))
                    {
                        long fileSize = fsb.Length; // ファイル長
                        long remain = fileSize;     // 読み込むべき残りのバイト数
                        long bufPos = 0;            // データ格納用配列内の追加位置
                        int bufsize = 40000;       // 一回のReadで読み込む最大バイト数
                                                   // バイト数を上げると処理は早くなるがメモリ消費量が大きくなる

                        byte[] bufC;
                        if (bufsize > fileSize)
                        {
                            bufC = new byte[fileSize];
                            fsb.Read(bufC, 0, (int)fileSize);
                        }
                        else
                        {
                            bufC = new byte[bufsize];
                            fsb.Read(bufC, 0, bufsize);
                        }

                        (Encoding encRes, byte[] bufBom) = GetJpEncoding(bufC, false);

                        // BOM付きで元ファイルがある場合はBOMを除外
                        if (existFlg)
                        {
                            fsb.Position = 0;

                            if (bufBom != null)
                            {
                                fsb.Position = bufBom.Length;
                                remain = fsb.Length - bufBom.Length;
                            }
                            else
                            {
                                fsb.Position = 0;
                            }
                        }
                        else
                        {
                            fsb.Position = 0;
                        }

                        while (remain > 0)
                        {
                            // bufsizeずつ読み込む
                            // 最後のバッファ調整処理
                            long remain2 = Math.Min(bufsize, remain);
                            byte[] bufR = new byte[remain2];
                            int readSize = fsb.Read(bufR, 0, (int)remain2);

                            bufPos = bufPos + readSize;
                            remain = remain - readSize;

                            if (firstLineFlg)
                            {

                                List<byte> tmpBuf = new List<byte>();

                                for (int i= 0; i < bufR.Length; i++)
                                {
                                    if (firstLineFlg && bufR[i] == 0x0A)
                                    {
                                        lineCnt++;
                                        if (lineCnt >= removeLineCount)
                                        {
                                            firstLineFlg = false;
                                            if (!string.IsNullOrEmpty(appendFirstLine))
                                            {
                                                tmpBuf.AddRange(encRes.GetBytes(appendFirstLine));
                                            }
                                        }

                                        continue;
                                    }

                                    if (!firstLineFlg)
                                    {
                                        tmpBuf.Add(bufR[i]);
                                    }
                                }

                                bufR = tmpBuf.ToArray();
                            }

                            fsa.Write(bufR, 0, bufR.Length);
                        }
                    }
                }
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                logger.Error(ex.StackTrace);

                return (false, ex.Message);
            }
        }
        **/

        //2つのファイルを1つにまとめる
        //X行目は除外する
        //BOM付きは未対応 -> 対応
        public static (bool, string) FileAppendRemoveFirstLine(string fromPath, string toPath, int removeLineCount, string appendFirstLine, int optionRemoveByte)
        {
            try
            {
                System.IO.FileInfo fi = new System.IO.FileInfo(toPath);
                fi.Refresh();
                bool existFlg = false;
                if (fi.Exists)
                {
                    existFlg = true;
                }
                fi = null;

                int lineCnt = 0;

                bool firstLineFlg = true;

                using (FileStream fsb = new(fromPath,
                                              FileMode.Open,
                                              FileAccess.Read,
                                              FileShare.ReadWrite))
                {

                    using (FileStream fsa = new(toPath,
                                                  FileMode.Append,
                                                  FileAccess.Write,
                                                  FileShare.ReadWrite))
                    {
                        long fileSize = fsb.Length; // ファイル長
                        long remain = fileSize;     // 読み込むべき残りのバイト数
                        long bufPos = 0;            // データ格納用配列内の追加位置
                        int bufsize = 40000;       // 一回のReadで読み込む最大バイト数
                                                   // バイト数を上げると処理は早くなるがメモリ消費量が大きくなる

                        byte[] bufC;
                        if (bufsize > fileSize)
                        {
                            bufC = new byte[fileSize];
                            _ = fsb.Read(bufC, 0, (int)fileSize);
                        }
                        else
                        {
                            bufC = new byte[bufsize];
                            _ = fsb.Read(bufC, 0, bufsize);
                        }

                        (Encoding encRes, byte[] bufBom) = GetJpEncoding(bufC, false);

                        // BOM付きで元ファイルがある場合はBOMを除外
                        if (existFlg)
                        {
                            fsb.Position = 0;

                            if (bufBom != null)
                            {
                                fsb.Position = bufBom.Length;
                                remain = fsb.Length - bufBom.Length;
                            }
                            else
                            {
                                fsb.Position = 0;
                            }
                        }
                        else
                        {
                            fsb.Position = 0;
                        }

                        while (remain > 0)
                        {
                            // bufsizeずつ読み込む
                            // 最後のバッファ調整処理
                            long remain2 = Math.Min(bufsize, remain);
                            byte[] bufR = new byte[remain2];
                            int readSize = fsb.Read(bufR, 0, (int)remain2);

                            bufPos = bufPos + readSize;
                            remain = remain - readSize;

                            if (firstLineFlg)
                            {

                                List<byte> tmpBuf = new List<byte>();

                                for (int i = 0; i < bufR.Length; i++)
                                {
                                    if (firstLineFlg)
                                    {
                                        if (bufR[i] == 0x0A && lineCnt < removeLineCount)
                                        {
                                            lineCnt++;
                                            continue;
                                        }

                                        if (lineCnt >= removeLineCount && optionRemoveByte >= 0)
                                        {
                                            if (optionRemoveByte <= 0)
                                            {
                                                firstLineFlg = false;
                                                if (!string.IsNullOrEmpty(appendFirstLine))
                                                {
                                                    tmpBuf.AddRange(encRes.GetBytes(appendFirstLine));
                                                }
                                            }
                                            optionRemoveByte--;
                                        }
                                    } 

                                    if (!firstLineFlg)
                                    {
                                        tmpBuf.Add(bufR[i]);
                                    }
                                }

                                bufR = tmpBuf.ToArray();
                            }

                            fsa.Write(bufR, 0, bufR.Length);
                        }
                    }
                }
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                logger.Error(ex.StackTrace);

                return (false, ex.Message);
            }
        }


        //ファイルのエンコードを変更する
        public static bool FileConvert(string fromPath, string toPath, Encoding fromEnc, Encoding toEnc)
        {
            try
            {

                bool fstFlg = true;

                using (FileStream fsb = new(fromPath,
                                              FileMode.Open,
                                              FileAccess.Read,
                                              FileShare.ReadWrite))
                {
                    using (FileStream fsa = new(toPath,
                                                  FileMode.Append,
                                                  FileAccess.Write,
                                                  FileShare.ReadWrite))
                    {
                        long fileSize = fsb.Length; // ファイル長
                        long remain = fileSize;     // 読み込むべき残りのバイト数
                        long bufPos = 0;            // データ格納用配列内の追加位置
                                                    //int bufsize = 40000;       // 一回のReadで読み込む最大バイト数
                                                    // バイト数を上げると処理は早くなるがメモリ消費量が大きくなる

                        if (fstFlg)
                        {
                            // 書き込み先BOM付きのエンコードはBOMを付与する
                            byte[] bom = toEnc.GetPreamble();
                            if (bom != null && bom.Length > 0)
                            {
                                fsa.Write(bom, 0, bom.Length);
                            }

                            // 書き込み元BOM付きのエンコードはBOMを削除する
                            bom = fromEnc.GetPreamble();
                            if (bom != null && bom.Length > 0)
                            {
                                fsb.Position = bom.Length;
                                remain = fsb.Length - bom.Length;
                            }
                            else
                            {
                                fsb.Position = 0;
                            }
                            fstFlg = false;
                        }

                        while (remain > 0)
                        {
                            // bufsizeずつ読み込む
                            // 最後のバッファ調整処理
                            //long remain2 = Math.Min(bufsize, remain);
                            long remain2 = remain;
                            byte[] bufR = new byte[remain2];
                            int readSize = fsb.Read(bufR, 0, (int)remain2);

                            bufPos = bufPos + readSize;
                            remain = remain - readSize;

                            byte[] resByte = Encoding.Convert(fromEnc, toEnc, bufR);

                            fsa.Write(resByte, 0, resByte.Length);
                        }
                    }
                }

                return true;

            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                logger.Error(ex.StackTrace);

                return false;
            }
        }

        // ファイルの先頭に引数の文字を設定する
        // ファイルのエンコードを変更する
        public static bool FileFirstAppend(string fromPath, string appendString)
        {
            try
            {

                string toPath = fromPath + ".tmp";

                using (FileStream fsb = new(fromPath,
                                              FileMode.Open,
                                              FileAccess.Read,
                                              FileShare.ReadWrite))
                {
                    using (FileStream fsa = new(toPath,
                                                  FileMode.Append,
                                                  FileAccess.Write,
                                                  FileShare.ReadWrite))
                    {
                        long fileSize = fsb.Length; // ファイル長
                        long remain = fileSize;     // 読み込むべき残りのバイト数
                        long bufPos = 0;            // データ格納用配列内の追加位置
                        int bufsize = 40000;       // 一回のReadで読み込む最大バイト数
                                                   // バイト数を上げると処理は早くなるがメモリ消費量が大きくなる

                        byte[] bufC;
                        if (bufsize > fileSize)
                        {
                            bufC = new byte[fileSize];
                            _ = fsb.Read(bufC, 0, (int)fileSize);
                        }
                        else
                        {
                            bufC = new byte[bufsize];
                            _ = fsb.Read(bufC, 0, bufsize);
                        }

                        (Encoding encRes, byte[] bufBom) = GetJpEncoding(bufC, false);

                        fsb.Position = 0;

                        // BOM付きのエンコードはBOMを付与する
                        if (bufBom != null)
                        {
                            fsa.Write(bufBom, 0, bufBom.Length);
                            fsb.Position = bufBom.Length;
                        }

                        // 先頭に文字追加
                        if (!string.IsNullOrEmpty(appendString))
                        {
                            if (encRes == null)
                            {
                                encRes = Encoding.GetEncoding("Shift_JIS");
                            }
                            byte[] appendByte = encRes.GetBytes(appendString);
                            fsa.Write(appendByte, 0, appendByte.Length);
                        }

                        while (remain > 0)
                        {
                            // bufsizeずつ読み込む
                            // 最後のバッファ調整処理
                            //long remain2 = Math.Min(bufsize, remain);
                            long remain2 = remain;
                            byte[] bufR = new byte[remain2];
                            int readSize = fsb.Read(bufR, 0, (int)remain2);

                            bufPos = bufPos + readSize;
                            remain = remain - readSize;

                            fsa.Write(bufR, 0, bufR.Length);
                        }
                    }
                }

                // ファイルコピー
                if (IsWritableFile(fromPath))
                {
                    if (FileShred.File(fromPath))
                    {
                        System.IO.FileInfo fi = new(toPath);
                        fi.Refresh();
                        fi.MoveTo(fromPath, true);
                    }
                    else
                    {
                        logger.Error("ファイルの完全削除に失敗しました。ファイル名:" + fromPath);
                        return false;
                    }
                }
                else
                {
                    logger.Error("ファイルが書き込み状態になっていません。ファイル名:" + fromPath);
                    return false;
                }

                return true;

            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                logger.Error(ex.StackTrace);

                return false;
            }
        }


        public static List<string> FileSplits(string fromPath, string toPath)
        {

            List<string> res = new();

            try
            {
                // ファイル名保管用
                string fileNameString = string.Empty;

                Encoding enc = new ASCIIEncoding();

                using (FileStream fsb = new(fromPath,
                                              FileMode.Open,
                                              FileAccess.Read,
                                              FileShare.ReadWrite))
                {
                    long fileSize = fsb.Length; // ファイル長
                    long remain = fileSize;     // 読み込むべき残りのバイト数
                    long bufPos = 0;            // データ格納用配列内の追加位置
                    int bufsize = 40000;       // 一回のReadで読み込む最大バイト数
                                               // バイト数を上げると処理は早くなるがメモリ消費量が大きくなる

                    // ファイル名保管用
                    List<byte> fileName = new();

                    // 書き込み用バッファ
                    List<byte> writeByte = new();
                    // 同一ファイルで一度でも書き込んだか
                    bool writeFlg = false;

                    // 初回処理かどうか
                    bool fst = true;
                    // ループ初期インデックス
                    int initIndex = 0;
                    // bomコード
                    List<byte> bom = new();

                    // ファイル名ルーチン処理中かどうか
                    int fileFlg = 0;

                    while (remain > 0)
                    {
                        // bufsizeずつ読み込む
                        // 最後のバッファ調整処理
                        long remain2 = Math.Min(bufsize, remain);
                        byte[] bufR = new byte[remain2];
                        int readSize = fsb.Read(bufR, 0, (int)remain2);

                        bufPos = bufPos + readSize;
                        remain = remain - readSize;

                        if (fst)
                        {
                            fst = false;
                            // ファイルがBOM付きか
                            (enc, byte[] bomB) = DetectEncodingFromBOM(bufR);

                            if (bomB == null)
                            {
                                // BOMではない場合
                                initIndex = 0;
                                (enc, byte[] dmy) = GetJpEncoding(bufR);
                                if (enc == null)
                                {
                                    enc = new ASCIIEncoding();
                                }
                            }
                            else
                            {
                                // BOMの場合
                                bom = bomB.ToList();
                                initIndex = bomB.Length;
                            }

                        }
                        else
                        {
                            // 初回処理以降はBOMがないため0にする
                            initIndex = 0;
                        }

                        List<byte> bufList = bufR.ToList();

                        for (int i = initIndex; i < bufList.Count; i++)
                        {
                            // 0x21は!マーク
                            if (bufList[i] == 0x21 || fileFlg != 0)
                            {

                                // 初回以降で!がある場合はファイルの切り替わりのためファイル出力を行う。
                                if (bufList[i] == 0x21 && !string.IsNullOrEmpty(fileNameString))
                                {
                                    FileMode mode2;
                                    if (writeFlg)
                                    {
                                        mode2 = FileMode.Append;
                                    }
                                    else
                                    {
                                        mode2 = FileMode.OpenOrCreate;
                                    }

                                    string toFilePath = Path.Combine(toPath, fileNameString);

                                    using (FileStream fsa2 = new(toFilePath,
                                                                  mode2,
                                                                  FileAccess.Write,
                                                                  FileShare.ReadWrite))
                                    {
                                        fsa2.Write(writeByte.ToArray(), 0, writeByte.Count);
                                        writeByte.Clear();
                                        res.Add(toFilePath);
                                        fileNameString = "";
                                        fileFlg = 0;
                                        writeFlg = false;
                                    }
                                }

                                if (fileFlg == 0)
                                {
                                    fileFlg = 1;
                                    i++;
                                }

                                while (i < bufList.Count)
                                {
                                    byte tmp = bufList[i++];
                                    if (tmp == 0x0d)
                                    {
                                        continue;
                                    }
                                    else if (tmp == 0x0a)
                                    {
                                        fileFlg = 0;
                                        fileNameString = enc.GetString(fileName.ToArray());
                                        fileName.Clear();
                                        if (bom.Count > 0)
                                        {
                                            writeByte.AddRange(bom);
                                        }
                                        break;
                                    }
                                    else
                                    {
                                        fileName.Add(tmp);
                                    }
                                }
                            }

                            if (i < bufList.Count)
                            {

                                writeByte.Add(bufList[i]);
                            }
                        }

                        FileMode mode;
                        if (writeFlg)
                        {
                            mode = FileMode.Append;
                        }
                        else
                        {
                            mode = FileMode.OpenOrCreate;
                        }

                        if (!string.IsNullOrEmpty(fileNameString))
                        {

                            using (FileStream fsa = new(Path.Combine(toPath, fileNameString),
                                                          mode,
                                                          FileAccess.Write,
                                                          FileShare.ReadWrite))
                            {
                                if (writeByte.Count > 0)
                                {
                                    fsa.Write(writeByte.ToArray(), 0, writeByte.Count);
                                    writeByte.Clear();
                                    writeFlg = true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                logger.Error(ex.StackTrace);
            }

            return res;
        }

        /// <summary>
        /// 指定したディレクトリの中から更新時刻が一番新しいファイルを取得します。
        /// </summary>
        /// <returns>
        /// 最新のファイルパス。
        /// ただしディレクトリにファイルが1つも無ければ空文字を返す。
        /// </returns>
        public static string GetLatestFile(string dir, string ext)
        {
            return Core(dir, (path, file)
                => File.GetLastWriteTime(path) > File.GetLastWriteTime(file), ext);
        }

        /// <summary>
        /// 指定したディレクトリの中から更新時刻が一番古いファイルを取得します。
        /// </summary>
        /// <returns>
        /// 最新のファイルパス。
        /// ただしディレクトリにファイルが1つも無ければ空文字を返す。
        /// </returns>
        public static string GetOldestFile(string dir, string ext)
        {
            return Core(dir, (path, file)
                => File.GetLastWriteTime(file) > File.GetLastWriteTime(path), ext);
        }

        private static string Core(string dir, Func<string, string, bool> compare, string ext)
        {
            if (!Directory.Exists(dir))
                throw new DirectoryNotFoundException($"Directory not found. path={dir}");

            string file = "";
            foreach (string path in Directory.EnumerateFiles(dir))
            {
                if (!string.IsNullOrEmpty(ext))
                {
                    string tExt = Path.GetExtension(path);

                    if (!string.IsNullOrEmpty(ext))
                    {
                        if (!tExt.Equals(ext))
                        {
                            continue;
                        }
                    }
                }

                if (string.IsNullOrEmpty(file))
                {
                    file = path;
                }
                else
                {
                    if (compare(path, file))
                    {
                        file = path;
                    }
                }
            }
            return file;
        }

        public static List<string> GetDirectoryOrderByDesc(string dir)
        {

            //古いものが先頭にくるようにファイルをソート
            List<string> filePathList = Directory
            //ディレクトリ内の全ファイルを取得
                .EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            //日付順に降順でソート
                .OrderBy(filePath => File.GetLastWriteTime(filePath).Date)
            //同じ日付内で時刻順に降順でソート
                .ThenBy(filePath => File.GetLastWriteTime(filePath).TimeOfDay)
                .ToList();

            return filePathList;
        }

        public static bool FolderCopy(string src_str, string dest_str)
        {
            bool flg = true;

            try
            {
                DirectoryInfo src_info = new(src_str);
                DirectoryInfo dest_info = new(dest_str);

                // コピー元のフォルダ存在チェック
                // 実行中にフォルダ削除されたりする場合を考慮
                src_info.Refresh();
                if (!src_info.Exists)
                {
                    logger.Error(new StringBuilder("コピー元のフォルダが見つかりませんでした。").Append(src_str).ToString());
                    return false;
                }

                // コピー先のフォルダ作成
                dest_info.Refresh();
                if (!dest_info.Exists)
                {
                    dest_info.Create();
                    dest_info.Attributes = src_info.Attributes;
                    dest_info.CreationTime = src_info.CreationTime;
                    dest_info.LastAccessTime = src_info.LastAccessTime;
                    dest_info.LastWriteTime = src_info.LastWriteTime;
                }
                else
                {
                    dest_info.Attributes = src_info.Attributes;
                    dest_info.CreationTime = src_info.CreationTime;
                    dest_info.LastAccessTime = src_info.LastAccessTime;
                    dest_info.LastWriteTime = src_info.LastWriteTime;
                }

                dest_info = null;

                // コピー元のフォルダとファイル情報を取得
                IEnumerable<DirectoryInfo> src_folders = src_info.EnumerateDirectories();
                IEnumerable<FileInfo> src_files = src_info.EnumerateFiles();

                // コピー元のファイルを全てコピー先のフォルダに上書きコピー
                string fileBaseName = string.Empty;
                FileInfo fi = null;
                // try catch のコストを考えてループの外で配置
                try
                {
                    foreach (FileInfo file in src_files)
                    {
                        fileBaseName = file.FullName;
                        string path = Path.Combine(dest_str, file.Name);
                        file.Refresh();
                        // 上書き
                        file.CopyTo(path, true);
                        // 権限コピー
                        fi = new(path);
                        fi.Refresh();
                        if (fi.Exists)
                        {
                            fi.Attributes = file.Attributes;
                            fi.CreationTime = file.CreationTime;
                            fi.LastAccessTime = file.LastAccessTime;
                            fi.LastWriteTime = file.LastWriteTime;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex.Message);
                    logger.Error(ex.StackTrace);
                    logger.Error(new StringBuilder("コピー元ファイル：").Append(fileBaseName).ToString());
                    flg = false;
                }
                finally
                {
                    fi = null;
                }

                // 再起呼び出しによりコピー元のフォルダを全てコピー先のフォルダにコピー
                try
                {
                    foreach (DirectoryInfo subfolder in src_folders)
                    {
                        subfolder.Refresh();
                        fileBaseName = subfolder.FullName;
                        string path = Path.Combine(dest_str, subfolder.Name);
                        bool tflg = FolderCopy(fileBaseName, path);
                        if (!tflg)
                        {
                            flg = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex.Message);
                    logger.Error(ex.StackTrace);
                    logger.Error(new StringBuilder("コピー元フォルダ：").Append(fileBaseName).ToString());
                    flg = false;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                logger.Error(ex.StackTrace);
                logger.Error(new StringBuilder("コピー元フォルダ").Append(src_str).ToString());
                logger.Error(new StringBuilder("コピー先フォルダ").Append(dest_str).ToString());
                return false;
            }

            return flg;
        }

        public static bool FolderCopy(string src_str, string dest_str, string searchPattern, string changePattern)
        {
            bool flg = true;

            try
            {
                DirectoryInfo src_info = new(src_str);
                DirectoryInfo dest_info = new(dest_str);

                // コピー元のフォルダ存在チェック
                // 実行中にフォルダ削除されたりする場合を考慮
                src_info.Refresh();
                if (!src_info.Exists)
                {
                    logger.Error(new StringBuilder("コピー元のフォルダが見つかりませんでした。").Append(src_str).ToString());
                    return false;
                }

                // コピー先のフォルダ作成
                dest_info.Refresh();
                if (!dest_info.Exists)
                {
                    dest_info.Create();
                    dest_info.Attributes = src_info.Attributes;
                    dest_info.CreationTime = src_info.CreationTime;
                    dest_info.LastAccessTime = src_info.LastAccessTime;
                    dest_info.LastWriteTime = src_info.LastWriteTime;
                }
                else
                {
                    dest_info.Attributes = src_info.Attributes;
                    dest_info.CreationTime = src_info.CreationTime;
                    dest_info.LastAccessTime = src_info.LastAccessTime;
                    dest_info.LastWriteTime = src_info.LastWriteTime;
                }

                dest_info = null;

                // コピー元のフォルダとファイル情報を取得
                IEnumerable<DirectoryInfo> src_folders = src_info.EnumerateDirectories();
                IEnumerable<FileInfo> src_files = src_info.EnumerateFiles(searchPattern);

                // コピー元のファイルを全てコピー先のフォルダに上書きコピー
                string fileBaseName = string.Empty;
                FileInfo fi = null;
                // try catch のコストを考えてループの外で配置
                try
                {
                    foreach (FileInfo file in src_files)
                    {
                        fileBaseName = file.FullName;

                        string fileName = file.Name;

                        fileName = ConvertString(fileName, searchPattern, changePattern);

                        string path = Path.Combine(dest_str, fileName);
                        file.Refresh();
                        // 上書き
                        file.CopyTo(path, true);
                        // 権限コピー
                        fi = new(path);
                        fi.Refresh();
                        if (fi.Exists)
                        {
                            fi.Attributes = file.Attributes;
                            fi.CreationTime = file.CreationTime;
                            fi.LastAccessTime = file.LastAccessTime;
                            fi.LastWriteTime = file.LastWriteTime;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex.Message);
                    logger.Error(ex.StackTrace);
                    logger.Error(new StringBuilder("コピー元ファイル：").Append(fileBaseName).ToString());
                    flg = false;
                }
                finally
                {
                    fi = null;
                }

                // 再起呼び出しによりコピー元のフォルダを全てコピー先のフォルダにコピー
                try
                {
                    foreach (DirectoryInfo subfolder in src_folders)
                    {
                        subfolder.Refresh();
                        fileBaseName = subfolder.FullName;
                        string path = Path.Combine(dest_str, subfolder.Name);
                        bool tflg = FolderCopy(fileBaseName, path);
                        if (!tflg)
                        {
                            flg = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex.Message);
                    logger.Error(ex.StackTrace);
                    logger.Error(new StringBuilder("コピー元フォルダ：").Append(fileBaseName).ToString());
                    flg = false;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                logger.Error(ex.StackTrace);
                logger.Error(new StringBuilder("コピー元フォルダ").Append(src_str).ToString());
                logger.Error(new StringBuilder("コピー先フォルダ").Append(dest_str).ToString());
                return false;
            }

            return flg;
        }

        static bool IsPureRegex(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return false;

            try
            {
                // 1. まず正しい正規表現の構文かチェック
                new Regex(pattern);

                // 2. エスケープ前後を比較し、変化がなければ「ただの文字列」として除外
                string escaped = Regex.Escape(pattern);
                return escaped != pattern;
            }
            catch (ArgumentException)
            {
                // 構文エラー
                return false;
            }
        }

        private static string ConvertToRegex(string input)
        {
            string pattern = string.Empty;

            foreach (char c in input)
            {
                pattern += c switch
                {
                    '?' => ".",
                    '*' => ".*",
                    _ => Regex.Escape(c.ToString()),
                };
            }
            return pattern;
        }

        // ファイルの正規表現サーチ
        // フォルダパスのファイルパス箇所に正規表現の記載が可能
        public static List<System.IO.FileInfo> MatchFileSearch(string fullPath)
        {
            // パスが正規表現かどうか
            string fileName = Path.GetFileName(fullPath);
            string path = Path.GetDirectoryName(fullPath);

            fileName = ConvertToRegex(fileName);
            bool checkReg = IsPureRegex(fileName);
            List<System.IO.FileInfo> fiList = new();

            if (checkReg)
            {
                System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(path);
                di.Refresh();
                if (di.Exists)
                {
                    IEnumerable<System.IO.FileInfo> targetFi = di.EnumerateFiles("*", SearchOption.TopDirectoryOnly);
                    if (targetFi.Count() == 0)
                    {
                        return fiList;
                    }

                    targetFi = targetFi.Where(x => Regex.IsMatch(x.Name, fileName));
                    return targetFi.ToList();
                }
                else
                {
                    return fiList;
                }
            }
            else
            {
                System.IO.FileInfo fi = new(fullPath);
                fi.Refresh();
                if (fi.Exists)
                {
                    fiList.Add(fi);
                }
                return fiList;
            }
        }

        // フォルダの正規表現サーチ
        // フォルダパスに正規表現の記載が可能
        public static List<DirectoryInfo> MatchFolderSearch(string parh)
        {
            // 1. ルートディレクトリを特定し、初期の DirectoryInfo を作成
            string root = Path.GetPathRoot(parh);
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                return new List<DirectoryInfo>();

            DirectoryInfo rootDir = new DirectoryInfo(root);

            // 2. ルート以降の各階層のパターンを配列に分解
            string remainingPath = parh.Substring(root.Length);
            string[] steps = remainingPath.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries
            );

            // 再帰処理の開始
            List<System.IO.DirectoryInfo> results = new List<System.IO.DirectoryInfo>();
            MatchLayers(rootDir, steps, 0, results);
            return results;

        }

        // 指定したパスの正規表現に該当するフォルダ一覧を返却
        private static void MatchLayers(DirectoryInfo currentDir, string[] steps, int index, List<System.IO.DirectoryInfo> results)
        {
            // すべての階層のチェックが終わったら結果に追加して終了
            if (index >= steps.Length)
            {
                results.Add(currentDir);
                return;
            }

            // 現在の階層のワイルドカードパターン
            string pattern = steps[index];

            try
            {
                List<System.IO.DirectoryInfo> nextDirs = MatchFolderSearch(currentDir.FullName, pattern);

                foreach (var nextDir in nextDirs)
                {
                    // 次の階層の検索へ進む（再帰）
                    MatchLayers(nextDir, steps, index + 1, results);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // アクセス権限のないフォルダはスキップ
            }
            catch (DirectoryNotFoundException)
            {
                // 途中でフォルダが見つからなくなった場合はスキップ
            }
        }

        // 対象フォルダと正義表現を引数にフォルダ一覧を返却
        private static List<System.IO.DirectoryInfo> MatchFolderSearch(string path, string target)
        {
            // パスが正規表現かどうか
            target = ConvertToRegex(target);
            bool checkReg = IsPureRegex(target);
            if (checkReg)
            {
                System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(path);
                di.Refresh();
                if (di.Exists)
                {
                    IEnumerable<System.IO.DirectoryInfo> targetDi = di.EnumerateDirectories("*", SearchOption.TopDirectoryOnly);
                    if (targetDi.Count() == 0)
                    {
                        return new List<System.IO.DirectoryInfo>();
                    }

                    targetDi = targetDi.Where(x => Regex.IsMatch(x.Name, target));
                    return targetDi.ToList();
                }
                else
                {
                    return new List<System.IO.DirectoryInfo>();
                }
            }
            else
            {
                path = System.IO.Path.Combine(path, target);
                System.IO.DirectoryInfo di = new(path);
                di.Refresh();
                if (di.Exists)
                {
                    List<System.IO.DirectoryInfo> diList = new();
                    diList.Add(di);
                    return diList;
                }
                else
                {
                    return new List<System.IO.DirectoryInfo>();
                }
            }
        }
        
        static string ConvertString(string source, string replacePattern, string keepPattern)
        {

            replacePattern = Regex.Escape(replacePattern)
                                .Replace("\\*", "(.*)");

            var match = Regex.Match(source, replacePattern);
            StringBuilder sb = new StringBuilder();

            if (match.Success)
            {
                int j = 1;
                for (int i = 0; i < keepPattern.Length; i++)
                {
                    if (i < keepPattern.Length && keepPattern[i] == '*')
                    {
                        if (match.Groups.Count > j)
                        {
                            sb.Append(match.Groups[j]);
                        }
                        j++;
                    }
                    else
                    {
                        sb.Append(keepPattern[i]);
                    }
                }
            }
            else
            {
                return source; 
            }

            return sb.ToString();
        }


        // ファイル名変更
        public static (bool, string, string) FileNameTargetChangeMove(string targetChar, string fromFileName, string toFileName, string fromFileDir, string toFileDir)
        {
            string tmpFilName = string.Empty;
            StringBuilder res = new();
            string toPath = string.Empty;
            bool flg = true;
            try
            {
                logger.Info("ファイル名置換処理開始");

                tmpFilName = FileNameTargetChange(targetChar, fromFileName, toFileName);

                string fromPath = Path.Combine(fromFileDir, fromFileName);
                toPath = Path.Combine(toFileDir, tmpFilName);

                logger.Info(new StringBuilder("変換前:").Append(fromPath).ToString());
                logger.Info(new StringBuilder("変換後:").Append(toPath).ToString());

                FileInfo file = new(fromPath);
                file.Refresh();
                // 上書きコピー
                file.MoveTo(toPath, true);
                file.Refresh();
                file = null;
                res.Append("ファイル名置換成功:").Append(toPath);
            }
            catch (Exception)
            {
                res.Append("ファイル名置換失敗").Append(toPath);
                flg = false;
            }

            logger.Info("ファイル名置換処理終了");

            return (flg, tmpFilName, res.ToString());
        }

        public static string FileNameTargetChange(string targetChar, string fromFileName, string toFileName)
        {
            string ext = string.Empty;
            string tmpFileName = toFileName;

            if (toFileName.Contains(targetChar))
            {
                // 拡張子がある場合分離
                ext = Path.GetExtension(fromFileName);
                string noExt = Path.GetFileNameWithoutExtension(fromFileName);

                if (!string.IsNullOrEmpty(noExt))
                {
                    tmpFileName = toFileName.Replace(targetChar, noExt);
                }
                else
                {
                    tmpFileName = toFileName.Replace(targetChar, fromFileName);
                }
            }
            else
            {
                tmpFileName = toFileName;
            }

            string toExt = Path.GetExtension(toFileName);

            // 変換先に拡張子がある場合は拡張子を付与しない
            if (!string.IsNullOrEmpty(ext) && string.IsNullOrEmpty(toExt))
            {
                tmpFileName = new StringBuilder(tmpFileName).Append(ext).ToString();
            }

            return tmpFileName;
        }

        // ファイル名が存在するか厳密にチェックする（大文字小文字のファイル名区別）
        public static bool FileExistsStrict(string DirectryName, string FileName)
        {

            bool flg = false;

            System.IO.DirectoryInfo di = new(DirectryName);
            di.Refresh();

            if (di.Exists)
            {
                int res = di.EnumerateFiles().Where(_ => _.Name.Equals(FileName)).Count();
                if (res > 0)
                {
                    flg = true;
                }
            }

            di = null;
            return flg;
        }

        public static List<string> ConvertListStringArrayToListCSV(
            List<string[]> list,
            string splitChar)
        {

            List<string> strings = new List<string>();

            foreach (string[] ele in list)
            {
                StringBuilder sb = new();
                foreach (string innerEle in ele)
                {
                    sb.Append(innerEle);
                    if (!string.IsNullOrEmpty(splitChar))
                    {
                        sb.Append(splitChar);
                    }
                }
                sb.Length = sb.Length - 1;
                strings.Add(sb.ToString());
            }
            return strings;
        }

        // timespanをhh:mm:ss形式の文字列で24時間を超える場合にtimespan型で返却する
        // 上記の形式だとtimespan型でデータを渡すとエラーになる
        public static bool TimeSpanFromString(string str, out TimeSpan ts)
        {
            try
            {
                if (string.IsNullOrEmpty(str))
                {
                    ts = TimeSpan.Zero;
                    return false;
                }
                else
                {
                    int hour = 0;
                    int millSec = 0;
                    string totalTime = str;
                    string[] timeArray = null;
                    if (totalTime.Contains('.'))
                    {
                        string[] dayArray = totalTime.Split('.');

                        if (dayArray[0].Contains(':'))
                        {
                            timeArray = dayArray[0].Split(':');
                        }
                        else
                        {
                            hour = int.Parse(dayArray[0]) * 24;
                            timeArray = dayArray[1].Split(':');
                        }

                        if (dayArray.Length == 3)
                        {
                            millSec = int.Parse(dayArray[2]);
                        }
                    }
                    else
                    {
                        timeArray = totalTime.Split(':');
                    }

                    hour = hour + int.Parse(timeArray[0]);

                    ts = new TimeSpan(0, hour, int.Parse(timeArray[1]), int.Parse(timeArray[2]), millSec);
                    return true;
                }
            }
            catch (Exception)
            {
                ts = TimeSpan.Zero;
                return false;
            }
        }

        // String形式の時刻で24時間以上超えた場合に日表示になっているフォーマットを時間表示になるように変換
        public static string TimeSpanStringHourFromDate(string str)
        {
            try
            {
                if (string.IsNullOrEmpty(str))
                {
                    return string.Empty;
                }
                else
                {
                    if (str.Contains('.'))
                    {
                        int hour = 0;
                        string[] dayArray = str.Split('.');

                        if (dayArray[0].Contains(':'))
                        {
                            return str;
                        }

                        string[] timeArray = dayArray[1].Split(':');

                        hour = int.Parse(dayArray[0]) * 24 + int.Parse(timeArray[0]);

                        return new StringBuilder()
                            .Append(hour.ToString().PadLeft(2, '0'))
                            .Append(':')
                            .Append(dayArray[1].Substring(3, dayArray[1].Length - 3)).ToString();
                    }
                    else
                    {
                        return str;
                    }
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }


        // timespan型をhh:mm:ss形式の文字列で返却。24時間超える場合でも日表示にしない
        public static String TimeSpanParse(TimeSpan ts)
        {
            StringBuilder sb = new();
            int hours = ts.Days * 24 + ts.Hours;
            sb.Append(hours.ToString().PadLeft(2, '0'))
                .Append(':')
                .Append(ts.Minutes.ToString().PadLeft(2, '0'))
                .Append(':')
                .Append(ts.Seconds.ToString().PadLeft(2, '0'));

            return sb.ToString();
        }

        public static bool MultiBootCheck(string exeName, string exePath, int processId)
        {
            //引数 のプロセスがあるか検索
            List<System.Diagnostics.Process> p =
                System.Diagnostics.Process.GetProcessesByName(exeName).ToList();

            if(p == null)
            {
                return false;
            }

            if (p.Count == 0)
            {
                return false;
            }

            List<System.Diagnostics.Process> p2 = 
                p.Where(x => exePath.Equals(x.MainModule.FileName) && processId != x.Id).ToList();

            if (p2 == null)
            {
                return false;
            }

            if (p2.Count == 0)
            {
                return false;
            }

            return true;

        }

        public static bool ProcessKill(string exeName, string exePath, int processId)
        {
            try
            {
                //引数のプロセスがあるか検索
                List<System.Diagnostics.Process> p =
                System.Diagnostics.Process.GetProcessesByName(exeName).ToList();

                if (p == null)
                {
                    return false;
                }

                if (p.Count == 0)
                {
                    return false;
                }

                List<System.Diagnostics.Process> p2 =
                    p.Where(x => exePath.Equals(x.MainModule.FileName) && processId != x.Id).ToList();

                if (p2 == null)
                {
                    return false;
                }

                if (p2.Count == 0)
                {
                    return false;
                }

                foreach (System.Diagnostics.Process ele in p2)
                {
                    try
                    {
                        ele.Refresh();
                        if (!ele.HasExited)
                        {
                            logger.Info(new StringBuilder("プロセスID:").Append(ele.Id).Append("のKill処理を実行します。").ToString());
                            ele.Kill(true);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex.Message);
                        logger.Error(ex.StackTrace);
                    }

                    try
                    {
                        ele.Refresh();
                        if (ele.HasExited)
                        {
                            logger.Info(new StringBuilder("プロセスID:").Append(ele.Id).Append("は終了しました。").ToString());
                        }
                        else
                        {
                            logger.Info(new StringBuilder("プロセスID:").Append(ele.Id).Append("は終了できませんでした。").ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex.Message);
                        logger.Error(ex.StackTrace);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                logger.Error(ex.StackTrace);
                return false;
            }

            return true;
        }

        public static (bool, string) RemoveCRLFFromFile(string path)
        {

            string createPath = new StringBuilder(path).Append(".tmp").ToString();

            try
            {
                using (FileStream fsb = new(path,
                                              FileMode.Open,
                                              FileAccess.Read,
                                              FileShare.ReadWrite))
                {

                    using (FileStream fsa = new(createPath,
                                                  FileMode.Create,
                                                  FileAccess.Write,
                                                  FileShare.ReadWrite))
                    {
                        long fileSize = fsb.Length; // ファイル長
                        long remain = fileSize;       // 読み込むべき残りのバイト数
                        int bufsize = 40000;           // 一回のReadで読み込む最大バイト数
                                                                   // バイト数を上げると処理は早くなるがメモリ消費量が大きくなる

                        List<byte> writeByte = new();

                        while (remain > 0)
                        {
                            // bufsizeずつ読み込む
                            // 最後のバッファ調整処理
                            long remain2 = Math.Min(bufsize, remain);
                            byte[] bufR = new byte[remain2];
                            int readSize = fsb.Read(bufR, 0, (int)remain2);
                            remain = remain - readSize;

                            for (int i = 0; i < readSize; i++)
                            {
                                // lfコード判定
                                if (bufR[i] == 0x0A || bufR[i] == 0x0D)
                                {
                                    continue;
                                }

                                // 書込み用バイトにコピー
                                writeByte.Add(bufR[i]);
                            }
                        }

                        if (writeByte.Count > 0)
                        {
                            fsa.Write(writeByte.ToArray(), 0, writeByte.Count);
                            writeByte.Clear();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                logger.Error(ex.StackTrace);

                return (false, ex.Message);
            }
            return (true, createPath);
        }
    }
}
