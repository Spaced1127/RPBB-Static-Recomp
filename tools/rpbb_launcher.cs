using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

internal static class Program
{
    private const string GameId = "GBQE78";
    private const uint GcMagic = 0xC2339F3D;
    private const string KnownDolSha256 =
        "5f73b291a1b8c33a7cebda40638f00f74ff7faf3a5f3877190cd30b628ffa42d";
    private const string DefaultUserFolder = "RocketPowerRecomp";

    private sealed class Options
    {
        public bool Prepare;
        public string Game;
        public string Install;
        public string UserDir;
        public readonly List<string> Pass = new List<string>();
    }

    private sealed class FstFile
    {
        public string Path;
        public long Offset;
        public long Size;
    }

    private static int Main(string[] args)
    {
        try
        {
            Options o = Parse(args);
            if (string.IsNullOrEmpty(o.Game))
            {
                Error("--game is required.");
                return 2;
            }
            o.Game = Path.GetFullPath(o.Game);
            if (string.IsNullOrEmpty(o.Install))
                o.Install = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            if (string.IsNullOrEmpty(o.UserDir))
                o.UserDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    DefaultUserFolder);
            o.UserDir = Path.GetFullPath(o.UserDir);
            return o.Prepare ? Prepare(o) : Start(o);
        }
        catch (Exception ex)
        {
            Error(ex.Message);
            return 2;
        }
    }

    private static Options Parse(string[] args)
    {
        Options o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a == "--prepare")
                o.Prepare = true;
            else if (a == "--game")
                o.Game = Next(args, ref i);
            else if (a == "--install")
                o.Install = Next(args, ref i);
            else if (a == "--user-dir")
                o.UserDir = Next(args, ref i);
            else
                o.Pass.Add(a);
        }
        return o;
    }

    private static string Next(string[] args, ref int i)
    {
        if (i + 1 >= args.Length)
            throw new ArgumentException("Missing value after " + args[i]);
        i++;
        return args[i];
    }

    private static int Prepare(Options o)
    {
        if (Directory.Exists(o.Game))
            return PrepareFolder(o.Game);

        if (File.Exists(o.Game))
            return PrepareImage(o);

        Error("That path doesn't exist. Pick your .iso or the extracted game folder.");
        return 2;
    }

    private static int PrepareFolder(string folder)
    {
        string dol = Path.Combine(folder, "sys", "main.dol");
        if (!File.Exists(dol))
        {
            Error("That folder doesn't contain sys/main.dol. Pick the extracted game folder, or the .iso.");
            return 2;
        }

        string hash = Sha256(dol);
        if (!hash.Equals(KnownDolSha256, StringComparison.OrdinalIgnoreCase))
        {
            Error("That folder's main.dol isn't the USA build this port needs (SHA-256 " + hash + ").");
            return 2;
        }

        Progress(1.0, "Game folder verified");
        return 0;
    }

    private static int PrepareImage(Options o)
    {
        string ext = Path.GetExtension(o.Game).ToLowerInvariant();
        if (ext == ".rvz" || ext == ".wia" || ext == ".gcz" || ext == ".ciso" || ext == ".wbfs")
        {
            Error("That's a compressed " + ext + " image. In Dolphin, right-click the game, " +
                  "Convert File, ISO, then pick the .iso here.");
            return 2;
        }

        string dest = Path.Combine(o.UserDir, "game");
        using (FileStream fs = File.OpenRead(o.Game))
        {
            byte[] head = ReadAt(fs, 0, 0x2440);
            uint magic = Be32(head, 0x1C);
            string id = Encoding.ASCII.GetString(head, 0, 6);
            if (magic != GcMagic || id != GameId)
            {
                Error("That doesn't look like Rocket Power: Beach Bandits (USA). " +
                      "Its GameCube ID is " + id + ".");
                return 2;
            }

            Progress(0.02, "Reading the disc header");
            try
            {
                if (Directory.Exists(dest))
                    Directory.Delete(dest, true);
            }
            catch
            {
                // A previous extraction that can't be removed is replaced file by file below.
            }
            Extract(fs, head, dest);
        }

        Progress(1.0, "Game files ready");
        return 0;
    }

    private static void Extract(FileStream fs, byte[] head, string dest)
    {
        string sys = Path.Combine(dest, "sys");
        string files = Path.Combine(dest, "files");
        Directory.CreateDirectory(sys);
        Directory.CreateDirectory(files);

        WriteAt(sys, "boot.bin", head, 0, 0x440);
        WriteAt(sys, "bi2.bin", head, 0x440, 0x2000);

        uint dolOffset = Be32(head, 0x420);
        uint fstOffset = Be32(head, 0x424);
        uint fstSize = Be32(head, 0x428);

        byte[] app = ReadAt(fs, 0x2440, 0x20);
        long appSize = 0x20 + Be32(app, 0x14) + Be32(app, 0x18);
        Progress(0.08, "Writing system files");
        CopyRange(fs, 0x2440, appSize, Path.Combine(sys, "apploader.img"));

        byte[] dolHeader = ReadAt(fs, dolOffset, 0x100);
        long dolSize = DolSize(dolHeader);
        CopyRange(fs, dolOffset, dolSize, Path.Combine(sys, "main.dol"));
        CopyRange(fs, fstOffset, fstSize, Path.Combine(sys, "fst.bin"));

        byte[] fst = ReadAt(fs, fstOffset, (int)fstSize);
        List<FstFile> entries = ParseFst(fst);
        long total = 0;
        foreach (FstFile f in entries)
            total += f.Size;
        long done = 0;
        int lastPercent = -1;

        foreach (FstFile f in entries)
        {
            string relative = f.Path.Replace('/', Path.DirectorySeparatorChar);
            string target = Path.GetFullPath(Path.Combine(files, relative));
            if (!target.StartsWith(Path.GetFullPath(files), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The disc's file table names a path outside the game folder.");

            Directory.CreateDirectory(Path.GetDirectoryName(target));
            CopyRange(fs, f.Offset, f.Size, target);
            done += f.Size;

            int percent = total > 0 ? (int)(done * 100 / total) : 100;
            if (percent / 10 != lastPercent / 10)
            {
                lastPercent = percent;
                Progress(0.15 + 0.8 * done / Math.Max(1, total), "Extracting " + f.Path);
            }
        }

        string hash = Sha256(Path.Combine(sys, "main.dol"));
        if (!hash.Equals(KnownDolSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                "The extracted main.dol isn't the USA build this port needs (SHA-256 " + hash + ").");
    }

    private static List<FstFile> ParseFst(byte[] fst)
    {
        List<FstFile> result = new List<FstFile>();
        if (fst.Length < 12)
            throw new InvalidDataException("The disc's file table is too small.");

        uint rootWord = Be32(fst, 0);
        uint rootField1 = Be32(fst, 4);
        uint rootField2 = Be32(fst, 8);

        if ((rootWord & 0x00FFFFFFu) == 0 && rootField2 > 0 &&
            rootField2 <= (uint)(fst.Length / 12))
        {
            // The compact indexed FST this game uses: one 12-byte entry per file or
            // directory, a dir's third word is the end index of its subtree, and the
            // names follow the entry array.
            int count = (int)rootField2;
            int stringTable = count * 12;
            int index = 1;
            while (index < count)
                index = ParseFstEntry(fst, index, string.Empty, stringTable, result);
            return result;
        }

        throw new InvalidDataException("This disc uses an FST layout the setup doesn't know.");
    }

    private static int ParseFstEntry(byte[] fst, int index, string prefix, int stringTable,
                                     List<FstFile> result)
    {
        uint word = Be32(fst, index * 12);
        uint field1 = Be32(fst, index * 12 + 4);
        uint field2 = Be32(fst, index * 12 + 8);
        uint type = word >> 24;
        int nameOffset = (int)(word & 0x00FFFFFFu);
        string name = FstName(fst, stringTable + nameOffset);
        string path = prefix.Length == 0 ? name : prefix + "/" + name;

        if (type == 1)
        {
            int child = index + 1;
            int end = (int)field2;
            if (end < child || end > fst.Length / 12)
                throw new InvalidDataException("The disc's file table is damaged.");
            while (child < end)
                child = ParseFstEntry(fst, child, path, stringTable, result);
            return end;
        }

        result.Add(new FstFile { Path = path, Offset = field1, Size = field2 });
        return index + 1;
    }

    private static string FstName(byte[] fst, int offset)
    {
        if (offset < 0 || offset >= fst.Length)
            throw new InvalidDataException("The disc's file table has a bad name offset.");
        int end = offset;
        while (end < fst.Length && fst[end] != 0)
            end++;
        return Encoding.GetEncoding(932).GetString(fst, offset, end - offset);
    }

    private static long DolSize(byte[] header)
    {
        long max = 0x100;
        for (int i = 0; i < 7; i++)
        {
            uint off = Be32(header, i * 4);
            uint size = Be32(header, 0x90 + i * 4);
            if (size != 0)
                max = Math.Max(max, (long)off + size);
        }
        for (int i = 0; i < 11; i++)
        {
            uint off = Be32(header, 0x1C + i * 4);
            uint size = Be32(header, 0xAC + i * 4);
            if (size != 0)
                max = Math.Max(max, (long)off + size);
        }
        return max;
    }

    private static int Start(Options o)
    {
        string gamePath;
        if (Directory.Exists(o.Game))
            gamePath = o.Game;
        else if (File.Exists(o.Game))
            gamePath = Path.Combine(o.UserDir, "game");
        else
        {
            Error("Your copy of the game wasn't found. Pick it again on the game's page.");
            return 2;
        }

        if (!File.Exists(Path.Combine(gamePath, "sys", "main.dol")))
        {
            Error("The game files aren't ready. Open the game's setup and pick your copy of the game again.");
            return 2;
        }

        string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        string runner = Path.Combine(appDir, "moderngekko-run.exe");
        if (!File.Exists(runner))
        {
            Error("moderngekko-run.exe is missing from the install folder.");
            return 2;
        }

        EnsureConfig(o.UserDir, appDir);

        StringBuilder arguments = new StringBuilder();
        arguments.Append("--game \"").Append(gamePath).Append('"');
        arguments.Append(" --audio cubeb --allow-interpreter --user-dir \"").Append(o.UserDir).Append('"');
        foreach (string pass in o.Pass)
        {
            arguments.Append(' ');
            arguments.Append(Quote(pass));
        }

        ProcessStartInfo psi = new ProcessStartInfo(runner, arguments.ToString());
        psi.WorkingDirectory = appDir;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.EnvironmentVariables["MODERNGEKKO_STATICRECOMP"] = "0";

        string logDir = Path.Combine(o.UserDir, "logs");
        Directory.CreateDirectory(logDir);
        string logPath = Path.Combine(logDir, "run.log");
        object gate = new object();

        using (StreamWriter log = new StreamWriter(logPath, false, Encoding.UTF8))
        {
            log.AutoFlush = true;
            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    lock (gate) log.WriteLine(e.Data);
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    lock (gate) log.WriteLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                try { process.PriorityClass = ProcessPriorityClass.High; }
                catch { }
                process.WaitForExit();
                process.WaitForExit();
                return process.ExitCode;
            }
        }
    }

    private static void EnsureConfig(string userDir, string appDir)
    {
        string configDir = Path.Combine(userDir, "Config");
        Directory.CreateDirectory(configDir);
        Directory.CreateDirectory(Path.Combine(userDir, "GC"));
        Directory.CreateDirectory(Path.Combine(userDir, "logs"));

        string dolphin = Path.Combine(configDir, "Dolphin.ini");
        if (!File.Exists(dolphin))
        {
            File.WriteAllText(dolphin,
                "[Core]" + Environment.NewLine +
                "CPUCore = 1" + Environment.NewLine +
                "GFXBackend = Vulkan" + Environment.NewLine +
                "DSPHLE = False" + Environment.NewLine +
                "EmulationSpeed = 1.0" + Environment.NewLine +
                Environment.NewLine +
                "[DSP]" + Environment.NewLine +
                "Backend = Cubeb" + Environment.NewLine +
                "DSPThread = True" + Environment.NewLine +
                "Muted = False" + Environment.NewLine +
                "Volume = 100" + Environment.NewLine +
                Environment.NewLine +
                "[Display]" + Environment.NewLine +
                "Fullscreen = False" + Environment.NewLine);
        }
        else
        {
            EnsureIniKey(dolphin, "Core", "DSPHLE", "False");
            EnsureIniKey(dolphin, "Core", "EmulationSpeed", "1.0");
            EnsureIniKey(dolphin, "DSP", "Backend", "Cubeb");
        }

        string gfx = Path.Combine(configDir, "GFX.ini");
        if (!File.Exists(gfx))
        {
            File.WriteAllText(gfx,
                "[Settings]" + Environment.NewLine +
                "InternalResolution = 3" + Environment.NewLine +
                "ShaderCache = True" + Environment.NewLine +
                "ShaderCompilationMode = 2" + Environment.NewLine +
                "WaitForShadersBeforeStarting = True" + Environment.NewLine);
        }
        else
        {
            EnsureIniKey(gfx, "Settings", "InternalResolution", "3");
        }

        string frontend = Path.Combine(userDir, "config.ini");
        if (!File.Exists(frontend))
        {
            File.WriteAllText(frontend,
                "[Video]" + Environment.NewLine +
                "resolution=1920x1080" + Environment.NewLine +
                "backend=Vulkan" + Environment.NewLine +
                "fullscreen=false" + Environment.NewLine +
                "show_fps_in_title=true" + Environment.NewLine +
                "mute=false" + Environment.NewLine +
                "volume=100" + Environment.NewLine +
                "efb_access=true" + Environment.NewLine);
        }
        else
        {
            EnsureIniKey(frontend, "Video", "efb_access", "true");
        }

        string controller = Path.Combine(configDir, "GCPadNew.ini");
        string defaultController = Path.Combine(appDir, "defaults", "GCPadNew.ini");
        if (!File.Exists(controller) && File.Exists(defaultController))
            File.Copy(defaultController, controller);
    }

    private static void EnsureIniKey(string path, string section, string key, string value)
    {
        List<string> lines = new List<string>();
        if (File.Exists(path))
            lines.AddRange(File.ReadAllLines(path));

        int sectionStart = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            string t = lines[i].Trim();
            if (t.StartsWith("[") && t.EndsWith("]") &&
                t.Substring(1, t.Length - 2).Trim().Equals(section, StringComparison.OrdinalIgnoreCase))
            {
                sectionStart = i + 1;
                break;
            }
        }

        if (sectionStart < 0)
        {
            if (lines.Count > 0 && lines[lines.Count - 1].Trim().Length != 0)
                lines.Add(string.Empty);
            lines.Add("[" + section + "]");
            lines.Add(key + " = " + value);
            File.WriteAllLines(path, lines.ToArray());
            return;
        }

        int sectionEnd = lines.Count;
        for (int i = sectionStart; i < lines.Count; i++)
        {
            string t = lines[i].Trim();
            if (t.StartsWith("[") && t.EndsWith("]"))
            {
                sectionEnd = i;
                break;
            }
            int eq = t.IndexOf('=');
            if (eq > 0 && t.Substring(0, eq).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                return; // already set, keep the player's choice
        }

        lines.Insert(sectionEnd, key + " = " + value);
        File.WriteAllLines(path, lines.ToArray());
    }

    private static string Quote(string value)
    {
        if (value.Length == 0)
            return "\"\"";
        if (value.IndexOf(' ') < 0 && value.IndexOf('\t') < 0 && value.IndexOf('"') < 0)
            return value;
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static void WriteAt(string dir, string name, byte[] data, int offset, int count)
    {
        using (FileStream fs = File.Create(Path.Combine(dir, name)))
            fs.Write(data, offset, count);
    }

    private static byte[] ReadAt(FileStream fs, long offset, int count)
    {
        byte[] data = new byte[count];
        fs.Seek(offset, SeekOrigin.Begin);
        int done = 0;
        while (done < count)
        {
            int n = fs.Read(data, done, count - done);
            if (n <= 0)
                throw new EndOfStreamException("The disc image is shorter than its header says.");
            done += n;
        }
        return data;
    }

    private static void CopyRange(FileStream fs, long offset, long count, string path)
    {
        using (FileStream output = File.Create(path))
        {
            fs.Seek(offset, SeekOrigin.Begin);
            byte[] buffer = new byte[1024 * 1024];
            long left = count;
            while (left > 0)
            {
                int n = fs.Read(buffer, 0, (int)Math.Min(buffer.Length, left));
                if (n <= 0)
                    throw new EndOfStreamException("The disc image is shorter than its file table says.");
                output.Write(buffer, 0, n);
                left -= n;
            }
        }
    }

    private static uint Be32(byte[] data, int offset)
    {
        return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) |
               ((uint)data[offset + 2] << 8) | data[offset + 3];
    }

    private static string Sha256(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream fs = File.OpenRead(path))
        {
            byte[] hash = sha.ComputeHash(fs);
            StringBuilder text = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                text.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return text.ToString();
        }
    }

    private static void Progress(double fraction, string text)
    {
        if (fraction < 0) fraction = 0;
        if (fraction > 1) fraction = 1;
        Console.WriteLine("progress " + fraction.ToString("0.###", CultureInfo.InvariantCulture) + " " + text);
        Console.Out.Flush();
    }

    private static void Error(string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.Flush();
    }
}
