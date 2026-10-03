#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LogicCommon
{
    public class FileShred
    {
        // NIST SP 800-88 Rev. 1 standard 方式でファイルを削除するライブラリ

        // ストリームのバッファサイズを1MBで定義
        // https://stackoverflow.com/questions/1862982/c-sharp-filestream-optimal-buffer-size-for-writing-large-files#comment123344066_1863003
        private const int BufSize = 1 * 1024 * 1024;


        public static (bool, List<string>) Directory(string dirPath, bool own)
        {
            System.IO.DirectoryInfo di = new(dirPath);

            List<string> targetFile = new();

            di.Refresh();
            if (!di.Exists)
            {
                return (false, targetFile);
            }

            IEnumerable<System.IO.FileInfo> files = di.EnumerateFiles("*", SearchOption.AllDirectories);
            IEnumerable<System.IO.DirectoryInfo> dices = di.EnumerateDirectories("*", SearchOption.AllDirectories);

            // フォルダ削除
            if (!files.Any() && own)
            {
                try
                {
                    di.Delete(true);
                }
                catch (Exception)
                {
                    return (false, targetFile);
                }
                return (true, targetFile);
            }
            else if(!files.Any())
            {
                return (true, targetFile);
            }

            int cnt = files.Count();
            int procCnt = 0;
            byte[] buf = Enumerable.Repeat((byte)0x00, BufSize).ToArray();

            foreach (System.IO.FileInfo fi in files.OrderBy(f => f.Name).ToList())
            {
                if(File(fi.FullName, buf))
                {
                    procCnt++;
                    targetFile.Add(fi.FullName);
                }
            }

            if(procCnt == cnt)
            {
                di.Refresh();
                if(own)
                {
                    // 自身のディレクトリも削除
                    di.Delete(true);
                }
                else
                {
                    foreach (System.IO.DirectoryInfo dic in dices.OrderBy(f => f.Name))
                    {
                        // 自身のディレクトリ配下のディレクトリを削除
                        dic.Delete();
                    }
                }
            }
            else
            {
                return (false, targetFile);
            }

            return (true, targetFile);
        }

        public static bool File(string filePath)
        {
            bool deleteFlg = true;

            System.IO.FileInfo fi = new(filePath);
            fi.Refresh();
            if (!fi.Exists)
            {
                return false;
            }

            try
            {
                using (FileStream stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    BufSize,
                    FileOptions.Asynchronous))
                {
                    if (!stream.CanRead)
                    {
                        return false;
                    }

                    if (!stream.CanWrite)
                    {
                        return false;
                    }

                    long size = stream.Length;

                    byte[] buf;
                    long fileSize = stream.Length; // ファイル長
                    int bufSize = BufSize;         // 一回のReadで読み込む最大バイト数

                    // 格納用空配列作成
                    if (fileSize <= bufSize)
                    {
                        buf = Enumerable.Repeat((byte)0x00, (int)fileSize).ToArray();
                    }
                    else
                    {
                        buf = Enumerable.Repeat((byte)0x00, bufSize).ToArray();
                    }

                    // 最大3回ベリファイする
                    // 1回目で成功したら1回目で終了
                    for (int i = 0; i < 3; i++)
                    {
                        FileOverwrite(stream, buf);

                        // ファイルサイズが変わっていないか
                        if (stream.Length != size)
                        {
                            deleteFlg = false;
                            break;
                        }

                        deleteFlg = IsFullOfZeros(stream);
                        if (deleteFlg) { break; }
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }

            if (deleteFlg)
            {
                try
                {
                    fi.Refresh();
                    fi.Delete();
                }
                catch (Exception)
                {
                    throw;
                }
            }

            return deleteFlg;
        }

        // 複数実行用
        private static bool File(string filePath, byte[] buf)
        {
            bool deleteFlg = true;

            try
            {
                using (FileStream stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    BufSize,
                    FileOptions.Asynchronous))
                {
                    if (!stream.CanRead)
                    {
                        return false;
                    }

                    if (!stream.CanWrite)
                    {
                        return false;
                    }

                    long size = stream.Length;

                    // 最大3回ベリファイする
                    // 1回目で成功したら1回目で終了
                    for (int i = 0; i < 3; i++)
                    {
                        FileOverwrite(stream, buf);

                        // ファイルサイズが変わっていないか
                        if (stream.Length != size)
                        {
                            deleteFlg = false;
                            break;
                        }

                        deleteFlg = IsFullOfZeros(stream);
                        if (deleteFlg) { break; }
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }

            if (deleteFlg)
            {
                try
                {
                    System.IO.FileInfo fi = new(filePath);
                    fi.Refresh();
                    fi.Delete();
                }
                catch (Exception)
                {
                    throw;
                }
            }

            return deleteFlg;

        }

        private static void FileOverwrite(FileStream stream, byte[] buf)
        {
            // streamの位置を先頭に
            stream.Position = 0;

            long fileSize = stream.Length; // ファイル長
            int bufSize = BufSize;         // 一回のReadで読み込む最大バイト数
                                           // バイト数を上げると処理は早くなるがメモリ消費量が大きくなる
            long numberOfChunks = fileSize / bufSize;

            // バイト群回数分ループ
            for (int i = 0; i < numberOfChunks; i++)
            {
                stream.Write(buf, 0, buf.Length);
            }

            // 残りのバイト書き込み
            if (stream.Position != stream.Length)
            {
                stream.Write(buf, 0, (int)(fileSize % bufSize));
            }

            stream.Position = 0;
        }

        private static bool IsFullOfZeros(FileStream stream)
        {
            stream.Position = 0;

            long fileSize = stream.Length; // ファイル長
            long remain = fileSize;        // 読み込むべき残りのバイト数
            int bufSize = BufSize;         // 一回のReadで読み込む最大バイト数
                                           // バイト数を上げると処理は早くなるがメモリ消費量が大きくなる

            byte[] bufR;
            long numberOfChunks = fileSize / bufSize;

            if (fileSize <= bufSize)
            {
                bufR = new byte[fileSize];
            }
            else
            {
                bufR = new byte[bufSize];
            }

            // バイト群回数分ループ
            for (int i = 0; i < numberOfChunks; i++)
            {
                int readSize = stream.Read(bufR, 0, (int)bufSize);
                remain = remain - readSize;

                if (bufR.Where(x => x != 0x00).Any())
                {
                    stream.Position = 0;
                    return false;
                }
            }

            // 残りバイト処理
            while (remain > 0)
            {
                int readSize = stream.Read(bufR, 0, (int)remain);

                if (bufR.AsSpan(0, readSize).ToArray().Where(x => x != 0x00).Any())
                {
                    stream.Position = 0;
                    return false;
                }
 
                remain = remain - readSize;
            }

            return true;
        }
    }
}
