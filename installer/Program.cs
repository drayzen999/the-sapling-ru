using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace SaplingRU
{
    // Установщик русификатора The Sapling v13.57a: накладывает бинарные патчи на файлы игры
    // (шрифты с кириллицей и разрешение соотношения сторон 21:9) и умеет откатывать их из бэкапа.
    static class Program
    {
        const string DataDir = "the_sapling_Data";
        const string BackupDir = @"SaplingRU\backup";

        // Файлы мода, которые удаляются при деинсталляции (папки — целиком)
        static readonly string[] ModFiles = { "winhttp.dll", "doorstop_config.ini", ".doorstop_version", "changelog.txt", "SaplingRU-README.txt" };
        static readonly string[] ModDirs = { "BepInEx", "SaplingRU" };

        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("Русификатор The Sapling + поддержка 21:9");
            Console.WriteLine();
            try
            {
                string game = FindGameDir(args);
                if (game == null)
                {
                    Console.WriteLine("Не найдена папка игры. Распакуйте архив в папку The Sapling (где лежит the_sapling.exe)");
                    Console.WriteLine("и запустите установщик оттуда, либо передайте путь к игре аргументом.");
                    return Exit(1);
                }
                Console.WriteLine("Папка игры: " + game);

                bool uninstall = Array.Exists(args, a => a == "/uninstall" || a == "--uninstall");
                if (!uninstall && !Array.Exists(args, a => a == "/install" || a == "--install"))
                {
                    Console.Write("1 — установить, 2 — удалить: ");
                    uninstall = Console.ReadLine()?.Trim() == "2";
                }
                return Exit(uninstall ? Uninstall(game) : Install(game));
            }
            catch (Exception e)
            {
                Console.WriteLine("Ошибка: " + e.Message);
                return Exit(1);
            }
        }

        static int Exit(int code)
        {
            Console.WriteLine();
            Console.WriteLine("Нажмите Enter для выхода.");
            Console.ReadLine();
            return code;
        }

        static string FindGameDir(string[] args)
        {
            var candidates = new List<string>();
            foreach (var a in args)
                if (!a.StartsWith("/") && !a.StartsWith("--"))
                    candidates.Add(a);
            candidates.Add(AppDomain.CurrentDomain.BaseDirectory);
            candidates.Add(Environment.CurrentDirectory);
            foreach (var c in candidates)
                if (File.Exists(Path.Combine(c, "the_sapling.exe")) && Directory.Exists(Path.Combine(c, DataDir)))
                    return Path.GetFullPath(c);
            return null;
        }

        static int Install(string game)
        {
            if (!File.Exists(Path.Combine(game, "winhttp.dll")) || !Directory.Exists(Path.Combine(game, @"BepInEx\plugins\SaplingFixes")))
                Console.WriteLine("Внимание: файлы мода (BepInEx, переводы) не найдены — распакуйте архив целиком в папку игры.");

            var entries = ReadManifest();
            // Сначала проверяем все файлы, чтобы не пропатчить игру наполовину
            foreach (var e in entries)
            {
                string hash = Sha256(Path.Combine(game, DataDir, e.File));
                if (hash != e.Src && hash != e.Dst)
                {
                    Console.WriteLine($"{e.File}: файл отличается от версии v13.57a — патч не подходит к этой версии игры.");
                    Console.WriteLine("Перевод интерфейса работать будет, но шрифты без кириллицы и без 21:9 в окне запуска.");
                    return 1;
                }
            }
            Directory.CreateDirectory(Path.Combine(game, BackupDir));
            foreach (var e in entries)
            {
                string path = Path.Combine(game, DataDir, e.File);
                if (Sha256(path) == e.Dst)
                {
                    Console.WriteLine($"{e.File}: уже пропатчен");
                    continue;
                }
                string backup = Path.Combine(game, BackupDir, e.File);
                File.Copy(path, backup, true);
                byte[] result = ApplyPatch(File.ReadAllBytes(path), LoadResource(e.File + ".srup"));
                if (Sha256(result) != e.Dst)
                    throw new InvalidDataException(e.File + ": результат патча не совпал с ожидаемым, файл не изменён");
                File.WriteAllBytes(path, result);
                Console.WriteLine($"{e.File}: пропатчен (оригинал в {BackupDir})");
            }
            Console.WriteLine();
            Console.WriteLine("Готово. В окне запуска игры выберите нужное разрешение (например, 3440 x 1440).");
            return 0;
        }

        static int Uninstall(string game)
        {
            foreach (var e in ReadManifest())
            {
                string path = Path.Combine(game, DataDir, e.File);
                string backup = Path.Combine(game, BackupDir, e.File);
                if (Sha256(path) == e.Src)
                    continue;
                if (File.Exists(backup) && Sha256(backup) == e.Src)
                {
                    File.Copy(backup, path, true);
                    Console.WriteLine($"{e.File}: восстановлен оригинал");
                }
                else
                {
                    Console.WriteLine($"{e.File}: нет бэкапа — восстановите файл проверкой целостности в Steam");
                }
            }
            foreach (var f in ModFiles)
                File.Delete(Path.Combine(game, f));
            foreach (var d in ModDirs)
                if (Directory.Exists(Path.Combine(game, d)))
                    Directory.Delete(Path.Combine(game, d), true);
            Console.WriteLine("Мод удалён. Сам установщик можно удалить вручную.");
            return 0;
        }

        // ---------- патчи ----------

        struct Entry { public string File, Src, Dst; }

        static List<Entry> ReadManifest()
        {
            var list = new List<Entry>();
            foreach (var line in Encoding.ASCII.GetString(LoadResource("manifest.txt")).Split('\n'))
            {
                var p = line.Trim().Split('|');
                if (p.Length == 3)
                    list.Add(new Entry { File = p[0], Src = p[1], Dst = p[2] });
            }
            return list;
        }

        static byte[] LoadResource(string name)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (s == null)
                    throw new FileNotFoundException("В установщике нет ресурса " + name);
                var ms = new MemoryStream();
                s.CopyTo(ms);
                return ms.ToArray();
            }
        }

        // Формат SRUP1: "SRUP1" + gzip( [len][control] [len][diff] [len][extra] ), control — тройки bsdiff
        static byte[] ApplyPatch(byte[] old, byte[] patch)
        {
            if (Encoding.ASCII.GetString(patch, 0, 5) != "SRUP1")
                throw new InvalidDataException("неизвестный формат патча");
            byte[] body;
            using (var gz = new GZipStream(new MemoryStream(patch, 5, patch.Length - 5), CompressionMode.Decompress))
            {
                var ms = new MemoryStream();
                gz.CopyTo(ms);
                body = ms.ToArray();
            }
            int pos = 0;
            byte[] control = ReadBlob(body, ref pos), diff = ReadBlob(body, ref pos), extra = ReadBlob(body, ref pos);

            var result = new MemoryStream();
            long oldPos = 0, diffPos = 0, extraPos = 0;
            for (int c = 0; c + 24 <= control.Length; c += 24)
            {
                long x = OffTin(control, c), y = OffTin(control, c + 8), z = OffTin(control, c + 16);
                for (long i = 0; i < x; i++)
                {
                    long o = oldPos + i;
                    byte b = (byte)(diff[diffPos + i] + (o >= 0 && o < old.Length ? old[o] : 0));
                    result.WriteByte(b);
                }
                diffPos += x;
                oldPos += x;
                result.Write(extra, (int)extraPos, (int)y);
                extraPos += y;
                oldPos += z;
            }
            return result.ToArray();
        }

        static byte[] ReadBlob(byte[] data, ref int pos)
        {
            long len = BitConverter.ToInt64(data, pos);
            pos += 8;
            var blob = new byte[len];
            Buffer.BlockCopy(data, pos, blob, 0, (int)len);
            pos += (int)len;
            return blob;
        }

        // Число bsdiff: 8 байт little-endian, знак в старшем бите последнего байта
        static long OffTin(byte[] b, int at)
        {
            long y = b[at + 7] & 0x7F;
            for (int i = 6; i >= 0; i--)
                y = y * 256 + b[at + i];
            return (b[at + 7] & 0x80) != 0 ? -y : y;
        }

        static string Sha256(string path) => File.Exists(path) ? Sha256(File.ReadAllBytes(path)) : "";

        static string Sha256(byte[] data)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }
    }
}
