using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

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
        public string WindowMode = "borderless";
        public readonly List<string> Pass = new List<string>();
    }

    private sealed class FstFile
    {
        public string Path;
        public long Offset;
        public long Size;
    }

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 0)
            return RunLauncher();

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
            else if (a == "--window-mode")
                o.WindowMode = Next(args, ref i).ToLowerInvariant();
            else if (!a.StartsWith("-") && string.IsNullOrEmpty(o.Game))
                o.Game = a; // dragging an ISO/folder onto the launcher
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

        try
        {
            File.WriteAllText(Path.Combine(dest, "source.txt"), SourceStamp(o.Game));
        }
        catch
        {
            // The marker only skips re-extraction; a failed write just means it runs again.
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
        string runner = Path.Combine(appDir, "rpbb.exe");
        if (!File.Exists(runner))
        {
            Error("rpbb.exe is missing from the install folder.");
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
                ApplyWindowMode(process, o.WindowMode);
                try
                {
                    process.WaitForExit();
                    process.WaitForExit();
                    return process.ExitCode;
                }
                finally
                {
                    NativeWindow.Restore();
                }
            }
        }
    }

    private static void ApplyWindowMode(Process process, string mode)
    {
        if (string.IsNullOrEmpty(mode) || mode == "windowed")
            return;

        NativeWindow.SetProcessDpiAwareness();
        IntPtr hwnd = WaitForMainWindow(process, 30000);
        if (hwnd == IntPtr.Zero)
            return;

        NativeWindow.Enter(hwnd, mode == "fullscreen");

        // The runner can put its File/View menu back when the window is resized or
        // the game changes scenes. Keep it off while the game owns the screen.
        Thread watcher = new Thread(delegate()
        {
            while (true)
            {
                try
                {
                    if (process.HasExited)
                        break;
                    NativeWindow.RemoveMenu(hwnd);
                }
                catch
                {
                    break;
                }
                Thread.Sleep(500);
            }
        });
        watcher.IsBackground = true;
        watcher.Start();
    }

    private static IntPtr WaitForMainWindow(Process process, int timeoutMs)
    {
        int waited = 0;
        while (waited < timeoutMs)
        {
            process.Refresh();
            IntPtr hwnd = process.MainWindowHandle;
            if (hwnd != IntPtr.Zero)
                return hwnd;
            Thread.Sleep(100);
            waited += 100;
        }
        return IntPtr.Zero;
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
                "aspect=auto" + Environment.NewLine +
                "widescreen_hack=false" + Environment.NewLine +
                "efb_access=true" + Environment.NewLine);
        }
        else
        {
            EnsureIniKey(frontend, "Video", "aspect", "auto");
            EnsureIniKey(frontend, "Video", "widescreen_hack", "false");
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

    private static Action<double, string> ProgressSink;

    private static void Progress(double fraction, string text)
    {
        if (fraction < 0) fraction = 0;
        if (fraction > 1) fraction = 1;
        Console.WriteLine("progress " + fraction.ToString("0.###", CultureInfo.InvariantCulture) + " " + text);
        Console.Out.Flush();
        Action<double, string> sink = ProgressSink;
        if (sink != null)
        {
            try { sink(fraction, text); }
            catch { }
        }
    }

    private static string LastError;

    private static void Error(string message)
    {
        LastError = message;
        Console.Error.WriteLine(message);
        Console.Error.Flush();
    }

    private static int RunLauncher()
    {
        try { HideConsole(); }
        catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
        {
            ReportLauncherError(e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
        {
            ReportLauncherError(e.ExceptionObject as Exception);
        };
        Application.Run(new LauncherForm());
        return 0;
    }

    private static void ReportLauncherError(Exception ex)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                DefaultUserFolder);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "launcher-crash.txt"),
                ex == null ? "Unknown launcher error." : ex.ToString());
        }
        catch { }

        try
        {
            MessageBox.Show(ex == null ? "The launcher hit an unknown error." : ex.ToString(),
                "Rocket Power: Beach Bandits - Launcher", MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch { }
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private static void HideConsole()
    {
        IntPtr hwnd = GetConsoleWindow();
        if (hwnd != IntPtr.Zero)
            ShowWindow(hwnd, 0);
    }

    private static string GetIniKey(string path, string section, string key, string fallback)
    {
        if (!File.Exists(path))
            return fallback;
        bool inSection = false;
        foreach (string line in File.ReadAllLines(path))
        {
            string t = line.Trim();
            if (t.StartsWith("[") && t.EndsWith("]"))
            {
                inSection = t.Substring(1, t.Length - 2).Trim().Equals(section,
                    StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!inSection)
                continue;
            int eq = t.IndexOf('=');
            if (eq <= 0)
                continue;
            if (t.Substring(0, eq).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                return t.Substring(eq + 1).Trim();
        }
        return fallback;
    }

    private static void SetIniKey(string path, string section, string key, string value)
    {
        List<string> lines = File.Exists(path)
            ? new List<string>(File.ReadAllLines(path))
            : new List<string>();
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
            {
                lines[i] = key + " = " + value;
                File.WriteAllLines(path, lines.ToArray());
                return;
            }
        }
        lines.Insert(sectionEnd, key + " = " + value);
        File.WriteAllLines(path, lines.ToArray());
    }

    private static string SourceStamp(string path)
    {
        FileInfo info = new FileInfo(path);
        return info.FullName + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks;
    }

    private static bool NeedsPrepare(Options o)
    {
        if (Directory.Exists(o.Game))
            return true;
        string dest = Path.Combine(o.UserDir, "game");
        if (!File.Exists(Path.Combine(dest, "sys", "main.dol")))
            return true;
        string marker = Path.Combine(dest, "source.txt");
        if (!File.Exists(marker))
            return true;
        return File.ReadAllText(marker).Trim() != SourceStamp(o.Game);
    }

    private sealed class Choice
    {
        public readonly string Label;
        public readonly string Value;

        public Choice(string label, string value)
        {
            Label = label;
            Value = value;
        }

        public override string ToString()
        {
            return Label;
        }
    }

    private sealed class LauncherForm : Form
    {
        private static readonly Color BgTop = Color.FromArgb(4, 14, 21);
        private static readonly Color BgBottom = Color.FromArgb(9, 48, 58);
        private static readonly Color SidebarColor = Color.FromArgb(6, 22, 30);
        private static readonly Color CardColor = Color.FromArgb(7, 27, 36);
        private static readonly Color FieldColor = Color.FromArgb(18, 44, 56);
        private static readonly Color CardBorder = Color.FromArgb(32, 64, 74);
        private static readonly Color Accent = Color.FromArgb(245, 195, 30);
        private static readonly Color AccentHover = Color.FromArgb(255, 214, 62);
        private static readonly Color TextMain = Color.FromArgb(232, 241, 245);
        private static readonly Color TextDim = Color.FromArgb(138, 166, 178);
        private static readonly Color ReadyGreen = Color.FromArgb(38, 118, 84);
        private static readonly Color NotReadyRed = Color.FromArgb(128, 48, 58);

        private static readonly string[] ResLabels =
        {
            "1x (640x528)", "2x (1280x720)", "3x (1920x1080)", "4x (2560x1440)",
            "6x (3840x2160)", "8x (5120x2880)", "12x (7680x4320)"
        };

        private static readonly string[] ResValues =
        {
            "640x528", "1280x720", "1920x1080", "2560x1440",
            "3840x2160", "5120x2880", "7680x4320"
        };

        private static readonly string[] ResScales =
        {
            "1", "2", "3", "4", "6", "8", "12"
        };

        private readonly string _appDir;
        private readonly string _userDir;
        private readonly string _launcherIni;
        private TextBox _gameBox;
        private ComboBox _resolution;
        private ComboBox _graphics;
        private ComboBox _window;
        private ComboBox _audio;
        private ComboBox _aspect;
        private CheckBox _fps;
        private CheckBox _wideHack;
        private TrackBar _volume;
        private Label _volumeValue;
        private AccentButton _play;
        private Button _prepare;
        private Button _settings;
        private Label _status;
        private Label _statusDetail;
        private Label _gameStatus;
        private Label _launchSummary;
        private Label _sideStatus;
        private Label _controllerStatus;
        private PillLabel _badge;
        private AccentProgress _progress;
        private TransparentPanel[] _pages;
        private NavButton[] _navs;
        private bool _busy;
        private bool _gameRunning;
        private bool _loaded;

        private sealed class TransparentPanel : Panel
        {
            public TransparentPanel()
            {
                SetStyle(ControlStyles.SupportsTransparentBackColor |
                         ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer, true);
                BackColor = Color.Transparent;
            }
        }

        private sealed class CardPanel : Panel
        {
            public string Title;

            public CardPanel()
            {
                SetStyle(ControlStyles.SupportsTransparentBackColor |
                         ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer, true);
                BackColor = Color.Transparent;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
                using (GraphicsPath path = Rounded(bounds, 10))
                {
                    using (SolidBrush fill = new SolidBrush(Color.FromArgb(232, CardColor)))
                        e.Graphics.FillPath(fill, path);
                    using (Pen border = new Pen(Color.FromArgb(70, CardBorder), 1))
                        e.Graphics.DrawPath(border, path);
                }

                if (!string.IsNullOrEmpty(Title))
                {
                    using (Font titleFont = new Font("Segoe UI", 8.5F, FontStyle.Bold))
                    using (SolidBrush titleBrush = new SolidBrush(Accent))
                        e.Graphics.DrawString(Title, titleFont, titleBrush, 18, 13);
                    using (Pen rule = new Pen(Color.FromArgb(90, Accent), 1))
                        e.Graphics.DrawLine(rule, 18, 35, Width - 18, 35);
                }
                base.OnPaint(e);
            }
        }

        private sealed class NavButton : Button
        {
            private bool _active;

            public bool Active
            {
                get { return _active; }
                set { _active = value; Invalidate(); }
            }

            public NavButton()
            {
                SetStyle(ControlStyles.UserPaint |
                         ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer, true);
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                TextAlign = ContentAlignment.MiddleLeft;
                Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                Cursor = Cursors.Hand;
                ForeColor = TextMain;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(SidebarColor);
                if (_active)
                {
                    using (SolidBrush fill = new SolidBrush(Color.FromArgb(20, 56, 68)))
                        e.Graphics.FillRectangle(fill, 0, 4, Width, Height - 8);
                    using (SolidBrush bar = new SolidBrush(Accent))
                        e.Graphics.FillRectangle(bar, 0, 4, 4, Height - 8);
                }

                TextRenderer.DrawText(e.Graphics, Text, Font,
                    new Rectangle(22, 0, Width - 22, Height),
                    _active ? Accent : Color.FromArgb(184, 208, 218),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
        }

        private sealed class AccentButton : Button
        {
            private bool _hover;

            public AccentButton()
            {
                SetStyle(ControlStyles.UserPaint |
                         ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer, true);
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                BackColor = Color.Transparent;
                ForeColor = Color.FromArgb(12, 18, 22);
                Font = new Font("Segoe UI", 18F, FontStyle.Bold);
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                _hover = true;
                Invalidate();
                base.OnMouseEnter(e);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                _hover = false;
                Invalidate();
                base.OnMouseLeave(e);
            }

            protected override void OnEnabledChanged(EventArgs e)
            {
                Invalidate();
                base.OnEnabledChanged(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
                Color fillColor = !Enabled
                    ? Color.FromArgb(89, 96, 84)
                    : (_hover ? AccentHover : Accent);
                using (GraphicsPath path = Rounded(bounds, 10))
                using (SolidBrush fill = new SolidBrush(fillColor))
                    e.Graphics.FillPath(fill, path);

                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle,
                    Enabled ? ForeColor : Color.FromArgb(60, 64, 58),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix);
            }
        }

        private sealed class PillLabel : Control
        {
            public Color FillColor = ReadyGreen;

            public PillLabel()
            {
                SetStyle(ControlStyles.UserPaint |
                         ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                ForeColor = Color.White;
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            }

            public void SetState(string text, Color color)
            {
                Text = text;
                FillColor = color;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
                using (GraphicsPath path = Rounded(bounds, Height / 2))
                using (SolidBrush fill = new SolidBrush(FillColor))
                    e.Graphics.FillPath(fill, path);
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix);
            }
        }

        private sealed class AccentProgress : Control
        {
            private int _value;

            public int Value
            {
                get { return _value; }
                set
                {
                    int next = value < 0 ? 0 : (value > 100 ? 100 : value);
                    if (next == _value)
                        return;
                    _value = next;
                    Invalidate();
                }
            }

            public AccentProgress()
            {
                SetStyle(ControlStyles.UserPaint |
                         ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer, true);
                BackColor = CardColor;
                Height = 10;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle track = new Rectangle(0, 0, Width - 1, Height - 1);
                using (GraphicsPath path = Rounded(track, Height / 2))
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(18, 48, 60)))
                    e.Graphics.FillPath(fill, path);

                int width = (Width - 1) * _value / 100;
                if (width > 2)
                {
                    Rectangle bar = new Rectangle(0, 0, width, Height - 1);
                    using (GraphicsPath path = Rounded(bar, Height / 2))
                    using (SolidBrush fill = new SolidBrush(Accent))
                        e.Graphics.FillPath(fill, path);
                }
            }
        }

        private static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        public LauncherForm()
        {
            _appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            _userDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                DefaultUserFolder);
            _launcherIni = Path.Combine(_userDir, "launcher.ini");

            Text = "Rocket Power: Beach Bandits - Launcher";
            ClientSize = new Size(1180, 800);
            MinimumSize = new Size(1196, 839);
            MaximumSize = new Size(1196, 839);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = BgTop;
            ForeColor = TextMain;
            Font = new Font("Segoe UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
            BackgroundImage = CreateBackground(1180, 800);
            BackgroundImageLayout = ImageLayout.Stretch;

            BuildUi();
            LoadSettings();
            _loaded = true;
        }

        private static Bitmap CreateBackground(int width, int height)
        {
            Bitmap bitmap = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle all = new Rectangle(0, 0, width, height);
                using (LinearGradientBrush sky = new LinearGradientBrush(
                    all, BgTop, BgBottom, 90F))
                    g.FillRectangle(sky, all);

                // Warm low sun and its glow.
                float sunX = width - 250F;
                float sunY = 210F;
                for (int i = 22; i >= 1; i--)
                {
                    float d = i * 36F;
                    int alpha = i < 6 ? 5 : 3;
                    using (SolidBrush glow = new SolidBrush(Color.FromArgb(alpha, 255, 158, 64)))
                        g.FillEllipse(glow, sunX - d, sunY - d, d * 2, d * 2);
                }
                using (GraphicsPath sunPath = new GraphicsPath())
                {
                    RectangleF sunRect = new RectangleF(sunX - 62, sunY - 62, 124, 124);
                    sunPath.AddEllipse(sunRect);
                    using (PathGradientBrush sun = new PathGradientBrush(sunPath))
                    {
                        sun.CenterColor = Color.FromArgb(190, 255, 186, 92);
                        sun.SurroundColors = new Color[] { Color.FromArgb(0, 255, 150, 50) };
                        g.FillEllipse(sun, sunRect);
                    }
                }

                using (SolidBrush ray = new SolidBrush(Color.FromArgb(7, 124, 226, 232)))
                {
                    Point[] first = {
                        new Point(width - 390, 0), new Point(width - 230, 0),
                        new Point(width - 760, height), new Point(width - 1010, height)
                    };
                    Point[] second = {
                        new Point(width - 150, 0), new Point(width - 70, 0),
                        new Point(width - 450, height), new Point(width - 570, height)
                    };
                    g.FillPolygon(ray, first);
                    g.FillPolygon(ray, second);
                }

                DrawWave(g, width, height, height - 210F, 24F, Color.FromArgb(22, 12, 64, 74));
                DrawWave(g, width, height, height - 150F, 20F, Color.FromArgb(30, 10, 54, 64));
                DrawWave(g, width, height, height - 80F, 16F, Color.FromArgb(42, 7, 40, 50));

                Random bubbles = new Random(7);
                for (int i = 0; i < 65; i++)
                {
                    int x = bubbles.Next(width);
                    int y = bubbles.Next(height - 40);
                    int d = bubbles.Next(3, 14);
                    using (Pen ring = new Pen(Color.FromArgb(bubbles.Next(7, 26), 128, 222, 232), 1))
                        g.DrawEllipse(ring, x, y, d, d);
                }
            }
            return bitmap;
        }

        private static void DrawWave(Graphics g, int width, int height, float baseY,
                                     float amplitude, Color color)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                PointF previous = new PointF(0, baseY);
                path.AddLine(new PointF(0, height), previous);
                for (int x = 0; x <= width; x += 10)
                {
                    float y = baseY + (float)Math.Sin((x + baseY) / 130.0) * amplitude;
                    PointF next = new PointF(Math.Min(width, x + 10), y);
                    path.AddLine(previous, next);
                    previous = next;
                }
                path.AddLine(previous, new PointF(width, height));
                path.CloseFigure();
                using (SolidBrush fill = new SolidBrush(color))
                    g.FillPath(fill, path);
            }
        }

        private void BuildUi()
        {
            SuspendLayout();

            Label eyebrow = new Label();
            eyebrow.Text = "ROCKET POWER";
            eyebrow.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            eyebrow.ForeColor = Accent;
            eyebrow.BackColor = Color.Transparent;
            eyebrow.AutoSize = true;
            eyebrow.Location = new Point(28, 20);
            Controls.Add(eyebrow);

            Label title = new Label();
            title.Text = "Beach Bandits";
            title.Font = new Font("Segoe UI", 24F, FontStyle.Bold);
            title.ForeColor = TextMain;
            title.BackColor = Color.Transparent;
            title.AutoSize = true;
            title.Location = new Point(25, 38);
            Controls.Add(title);

            _badge = new PillLabel();
            _badge.Location = new Point(942, 26);
            _badge.Size = new Size(210, 30);
            _badge.SetState("NO GAME IMPORTED", NotReadyRed);
            Controls.Add(_badge);

            Label headerSub = new Label();
            headerSub.Text = "JIT recompilation  \u2022  PC  \u2022  60 FPS";
            headerSub.ForeColor = TextDim;
            headerSub.BackColor = Color.Transparent;
            headerSub.AutoSize = false;
            headerSub.TextAlign = ContentAlignment.MiddleRight;
            headerSub.Location = new Point(700, 62);
            headerSub.Size = new Size(452, 22);
            Controls.Add(headerSub);

            Panel headerRule = new Panel();
            headerRule.BackColor = Color.FromArgb(45, 32, 64, 74);
            headerRule.Location = new Point(0, 108);
            headerRule.Size = new Size(1180, 1);
            Controls.Add(headerRule);

            Panel sidebar = new Panel();
            sidebar.BackColor = SidebarColor;
            sidebar.Location = new Point(0, 109);
            sidebar.Size = new Size(190, 691);
            Controls.Add(sidebar);

            string[] navNames = { "PLAY", "GRAPHICS", "AUDIO", "CONTROLLER" };
            _navs = new NavButton[navNames.Length];
            for (int i = 0; i < navNames.Length; i++)
            {
                NavButton nav = new NavButton();
                nav.Text = navNames[i];
                nav.Location = new Point(0, 26 + i * 52);
                nav.Size = new Size(190, 44);
                int index = i;
                nav.Click += delegate { ShowPage(index); };
                sidebar.Controls.Add(nav);
                _navs[i] = nav;
            }

            Label sideNote = new Label();
            sideNote.Text = "Your disc, your files.\nNo game data ships with this app.";
            sideNote.ForeColor = Color.FromArgb(120, 148, 160);
            sideNote.BackColor = Color.Transparent;
            sideNote.Font = new Font("Segoe UI", 8.5F);
            sideNote.AutoSize = false;
            sideNote.Location = new Point(18, 540);
            sideNote.Size = new Size(158, 50);
            sidebar.Controls.Add(sideNote);

            Panel sideRule = new Panel();
            sideRule.BackColor = Color.FromArgb(35, 32, 64, 74);
            sideRule.Location = new Point(18, 610);
            sideRule.Size = new Size(154, 1);
            sidebar.Controls.Add(sideRule);

            _sideStatus = new Label();
            _sideStatus.Text = "Import a disc to begin";
            _sideStatus.ForeColor = TextDim;
            _sideStatus.BackColor = Color.Transparent;
            _sideStatus.Font = new Font("Segoe UI", 8.5F);
            _sideStatus.AutoSize = false;
            _sideStatus.Location = new Point(18, 626);
            _sideStatus.Size = new Size(158, 40);
            sidebar.Controls.Add(_sideStatus);

            _pages = new TransparentPanel[4];
            for (int i = 0; i < _pages.Length; i++)
            {
                TransparentPanel page = new TransparentPanel();
                page.Location = new Point(210, 122);
                page.Size = new Size(950, 648);
                page.Visible = false;
                Controls.Add(page);
                _pages[i] = page;
            }

            BuildPlayPage(_pages[0]);
            BuildGraphicsPage(_pages[1]);
            BuildAudioPage(_pages[2]);
            BuildControllerPage(_pages[3]);
            ShowPage(0);

            ResumeLayout(false);
            PerformLayout();
        }

        private void BuildPlayPage(Control page)
        {
            CardPanel gameCard = MakeCard(page, 0, 0, 440, 420, "GAME");

            MakeLabel(gameCard, "Rocket Power: Beach Bandits", 18, 50, 404, 24,
                      TextMain, 12F, FontStyle.Bold);
            _gameStatus = MakeLabel(gameCard, "Pick your USA disc image or extracted folder.",
                                    18, 78, 404, 20, TextDim, 8.5F, FontStyle.Regular);

            MakeLabel(gameCard, "Game copy", 18, 112, 404, 18,
                      TextMain, 9F, FontStyle.Bold);
            _gameBox = MakeTextBox(gameCard, 18, 134, 404);
            _gameBox.TextChanged += delegate
            {
                UpdateReadyState();
                UpdateSummary();
            };

            Button browseFolder = MakeButton(gameCard, "Browse folder", 18, 172, 196, 30);
            browseFolder.Click += delegate { BrowseFolder(); };
            Button browseIso = MakeButton(gameCard, "Browse ISO...", 226, 172, 196, 30);
            browseIso.Click += delegate { BrowseIso(); };

            MakeLabel(gameCard, "An ISO is extracted and verified on first play.",
                      18, 212, 404, 20, TextDim, 8.5F, FontStyle.Regular);

            MakeLabel(gameCard, "Disc ID", 18, 252, 110, 18, TextDim, 8.5F, FontStyle.Regular);
            MakeLabel(gameCard, "GBQE78  \u2022  USA, Redump verified",
                      140, 252, 280, 18, TextMain, 8.5F, FontStyle.Regular);

            MakeLabel(gameCard, "Save data", 18, 282, 110, 18, TextDim, 8.5F, FontStyle.Regular);
            MakeLabel(gameCard, @"%LOCALAPPDATA%\RocketPowerRecomp\GC",
                      140, 282, 280, 18, TextMain, 8.5F, FontStyle.Regular);

            Button openSaves = MakeButton(gameCard, "Open saves", 18, 314, 196, 30);
            openSaves.Click += delegate { OpenSaves(); };

            MakeLabel(gameCard, "Your disc, your files.\nNo game data ships with this app.",
                      18, 364, 404, 40, TextDim, 8.5F, FontStyle.Regular);

            CardPanel launchCard = MakeCard(page, 460, 0, 490, 420, "LAUNCH");

            _play = new AccentButton();
            _play.Text = "PLAY";
            _play.Location = new Point(18, 52);
            _play.Size = new Size(454, 88);
            _play.Click += delegate { PlayOrPrepare(true); };
            launchCard.Controls.Add(_play);

            _prepare = MakeButton(launchCard, "Extract / verify", 18, 158, 220, 34);
            _prepare.Click += delegate { PlayOrPrepare(false); };
            _settings = MakeButton(launchCard, "Open settings", 252, 158, 220, 34);
            _settings.Click += delegate { OpenSettings(); };

            MakeLabel(launchCard, "Current setup", 18, 212, 454, 18,
                      TextDim, 8.5F, FontStyle.Regular);
            _launchSummary = MakeLabel(launchCard, "", 18, 234, 454, 40,
                                       TextMain, 9F, FontStyle.Regular);

            MakeLabel(launchCard,
                      "Settings save automatically.\nYour extracted game folder is reused between sessions.",
                      18, 304, 454, 44, TextDim, 8.5F, FontStyle.Regular);

            Button quit = MakeButton(launchCard, "Quit", 18, 366, 454, 32);
            quit.Click += delegate { Close(); };

            CardPanel statusCard = MakeCard(page, 0, 440, 950, 190, "STATUS");
            _status = MakeLabel(statusCard, "Ready.", 18, 50, 914, 22,
                                TextMain, 9F, FontStyle.Regular);
            _statusDetail = MakeLabel(statusCard,
                                      "Pick your USA disc image or extracted game folder.",
                                      18, 76, 914, 40, TextDim, 8.5F, FontStyle.Regular);

            _progress = new AccentProgress();
            _progress.Location = new Point(18, 132);
            _progress.Size = new Size(914, 10);
            statusCard.Controls.Add(_progress);
        }

        private void BuildGraphicsPage(Control page)
        {
            CardPanel card = MakeCard(page, 0, 0, 950, 632, "GRAPHICS");

            MakeLabel(card, "Internal resolution (EFB upscale)", 18, 56, 420, 20,
                      TextMain, 9F, FontStyle.Bold);
            _resolution = MakeCombo(card, 18, 80, 420);
            for (int i = 0; i < ResLabels.Length; i++)
                _resolution.Items.Add(new Choice(ResLabels[i], ResValues[i]));
            _resolution.SelectedIndexChanged += delegate
            {
                UpdateSummary();
                if (_loaded) SaveSettings();
            };

            MakeLabel(card,
                      "The 3D scene renders at this resolution, not the window size. 3x is 1920x1080.",
                      18, 112, 900, 20, TextDim, 8.5F, FontStyle.Regular);

            MakeLabel(card, "Graphics API", 18, 154, 420, 20,
                      TextMain, 9F, FontStyle.Bold);
            _graphics = MakeCombo(card, 18, 178, 420);
            _graphics.Items.Add(new Choice("Vulkan", "vulkan"));
            _graphics.Items.Add(new Choice("D3D12", "d3d12"));
            _graphics.Items.Add(new Choice("OpenGL", "opengl"));
            _graphics.SelectedIndexChanged += delegate
            {
                UpdateSummary();
                if (_loaded) SaveSettings();
            };

            MakeLabel(card, "Window mode", 500, 154, 420, 20,
                      TextMain, 9F, FontStyle.Bold);
            _window = MakeCombo(card, 500, 178, 420);
            _window.Items.Add(new Choice("Fullscreen windowed", "borderless"));
            _window.Items.Add(new Choice("Fullscreen", "fullscreen"));
            _window.Items.Add(new Choice("Windowed", "windowed"));
            _window.SelectedIndexChanged += delegate
            {
                UpdateSummary();
                if (_loaded) SaveSettings();
            };

            MakeLabel(card, "Window title", 18, 236, 420, 20,
                      TextMain, 9F, FontStyle.Bold);
            _fps = new CheckBox();
            _fps.Text = "Show FPS in the window title";
            _fps.ForeColor = TextMain;
            _fps.BackColor = Color.Transparent;
            _fps.FlatStyle = FlatStyle.Flat;
            _fps.AutoSize = true;
            _fps.Location = new Point(18, 260);
            _fps.CheckedChanged += delegate { if (_loaded) SaveSettings(); };
            card.Controls.Add(_fps);

            MakeLabel(card,
                      "Fullscreen windowed is borderless at your desktop resolution. Fullscreen uses your monitor's native resolution at 60 Hz and restores the desktop mode on exit.",
                      18, 306, 900, 44, TextDim, 8.5F, FontStyle.Regular);

            MakeLabel(card, "Framing", 18, 356, 200, 20,
                      Accent, 9F, FontStyle.Bold);

            MakeLabel(card, "Aspect ratio", 18, 388, 420, 20,
                      TextMain, 9F, FontStyle.Bold);
            _aspect = MakeCombo(card, 18, 412, 420);
            _aspect.Items.Add(new Choice("Auto (game native)", "auto"));
            _aspect.Items.Add(new Choice("16:9 widescreen", "wide"));
            _aspect.Items.Add(new Choice("4:3 standard", "standard"));
            _aspect.Items.Add(new Choice("Stretch", "stretch"));
            _aspect.SelectedIndexChanged += delegate
            {
                UpdateSummary();
                if (_loaded) SaveSettings();
            };

            MakeLabel(card,
                      "Auto keeps 4:3. 16:9 fills a widescreen display.",
                      500, 388, 420, 20, TextDim, 8.5F, FontStyle.Regular);

            _wideHack = new CheckBox();
            _wideHack.Text = "Widescreen hack (stretch 3D)";
            _wideHack.ForeColor = TextMain;
            _wideHack.BackColor = Color.Transparent;
            _wideHack.FlatStyle = FlatStyle.Flat;
            _wideHack.AutoSize = true;
            _wideHack.Location = new Point(18, 450);
            _wideHack.CheckedChanged += delegate { if (_loaded) SaveSettings(); };
            card.Controls.Add(_wideHack);

            MakeLabel(card,
                      "Aspect ratio sets the output shape. The widescreen hack widens the 3D projection to match it; the 2D HUD may stretch.",
                      18, 484, 900, 22, TextDim, 8.5F, FontStyle.Regular);
            MakeLabel(card,
                      "For a full-width 16:9 picture, choose 16:9 widescreen and enable the widescreen hack.",
                      18, 512, 900, 22, TextDim, 8.5F, FontStyle.Regular);
        }

        private void BuildAudioPage(Control page)
        {
            CardPanel card = MakeCard(page, 0, 0, 950, 460, "AUDIO");

            MakeLabel(card, "Output", 18, 56, 420, 20,
                      TextMain, 9F, FontStyle.Bold);
            _audio = MakeCombo(card, 18, 80, 420);
            _audio.Items.Add(new Choice("Cubeb (recommended)", "cubeb"));
            _audio.Items.Add(new Choice("Off", "null"));
            _audio.SelectedIndexChanged += delegate
            {
                if (_loaded) SaveSettings();
            };

            MakeLabel(card, "Volume", 18, 140, 420, 20,
                      TextMain, 9F, FontStyle.Bold);
            _volume = new TrackBar();
            _volume.Minimum = 0;
            _volume.Maximum = 100;
            _volume.TickStyle = TickStyle.None;
            _volume.SmallChange = 5;
            _volume.LargeChange = 10;
            _volume.Value = 100;
            _volume.BackColor = CardColor;
            _volume.Location = new Point(14, 164);
            _volume.Size = new Size(420, 42);
            _volume.ValueChanged += delegate
            {
                _volumeValue.Text = _volume.Value + "%";
                if (_loaded) SaveSettings();
            };
            card.Controls.Add(_volume);

            _volumeValue = MakeLabel(card, "100%", 446, 172, 80, 22,
                                     TextMain, 9F, FontStyle.Regular);

            MakeLabel(card,
                      "DSP LLE is always used. If the game is silent, check the Windows volume mixer for rpbb.",
                      18, 236, 900, 22, TextDim, 8.5F, FontStyle.Regular);
            MakeLabel(card,
                      "Choosing Off mutes the game but keeps emulation running normally.",
                      18, 264, 900, 22, TextDim, 8.5F, FontStyle.Regular);
        }

        private void BuildControllerPage(Control page)
        {
            CardPanel card = MakeCard(page, 0, 0, 950, 500, "CONTROLLER");

            MakeLabel(card, "Controller profile", 18, 56, 420, 20,
                      TextMain, 9F, FontStyle.Bold);
            MakeLabel(card, Path.Combine(_userDir, "Config", "GCPadNew.ini"),
                      18, 82, 900, 22, TextMain, 9F, FontStyle.Regular);

            _controllerStatus = MakeLabel(card, "", 18, 112, 900, 22,
                                           TextDim, 8.5F, FontStyle.Regular);

            Button openProfile = MakeButton(card, "Open controller profile",
                                            18, 150, 240, 34);
            openProfile.Click += delegate { OpenControllerProfile(); };
            Button openConfig = MakeButton(card, "Open config folder",
                                           274, 150, 240, 34);
            openConfig.Click += delegate { OpenSettings(); };

            MakeLabel(card,
                      "A DualSense profile ships with this release. To use an Xbox, Switch or other pad, open the profile and adjust the SDL mapping, or replace it with a GCPadNew.ini from another ModernGekko build.",
                      18, 212, 900, 60, TextDim, 8.5F, FontStyle.Regular);

            MakeLabel(card,
                      "The launcher never edits the game's files. Controller settings live in your user folder.",
                      18, 290, 900, 22, TextDim, 8.5F, FontStyle.Regular);
        }

        private static Label MakeLabel(Control parent, string text, int x, int y,
                                       int width, int height, Color color,
                                       float size, FontStyle style)
        {
            Label label = new Label();
            label.Text = text;
            label.Location = new Point(x, y);
            label.Size = new Size(width, height);
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.Font = new Font("Segoe UI", size, style);
            label.AutoSize = false;
            parent.Controls.Add(label);
            return label;
        }

        private static TextBox MakeTextBox(Control parent, int x, int y, int width)
        {
            TextBox box = new TextBox();
            box.Location = new Point(x, y);
            box.Size = new Size(width, 26);
            box.BackColor = FieldColor;
            box.ForeColor = TextMain;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.Font = new Font("Segoe UI", 9F);
            parent.Controls.Add(box);
            return box;
        }

        private static ComboBox MakeCombo(Control parent, int x, int y, int width)
        {
            ComboBox combo = new ComboBox();
            combo.Location = new Point(x, y);
            combo.Size = new Size(width, 26);
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.FlatStyle = FlatStyle.Flat;
            combo.BackColor = FieldColor;
            combo.ForeColor = TextMain;
            combo.Font = new Font("Segoe UI", 9F);
            parent.Controls.Add(combo);
            return combo;
        }

        private static Button MakeButton(Control parent, string text, int x, int y,
                                         int width, int height)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, height);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = CardBorder;
            button.FlatAppearance.BorderSize = 1;
            button.BackColor = Color.FromArgb(16, 44, 56);
            button.ForeColor = TextMain;
            button.Font = new Font("Segoe UI", 9F);
            button.UseVisualStyleBackColor = false;
            parent.Controls.Add(button);
            return button;
        }

        private static CardPanel MakeCard(Control parent, int x, int y, int width,
                                          int height, string title)
        {
            CardPanel card = new CardPanel();
            card.Location = new Point(x, y);
            card.Size = new Size(width, height);
            card.Title = title;
            parent.Controls.Add(card);
            return card;
        }

        private void ShowPage(int index)
        {
            for (int i = 0; i < _pages.Length; i++)
                _pages[i].Visible = i == index;
            for (int i = 0; i < _navs.Length; i++)
                _navs[i].Active = i == index;
            if (index == 0)
                UpdateReadyState();
        }

        private void LoadSettings()
        {
            EnsureConfig(_userDir, _appDir);

            string configPath = Path.Combine(_userDir, "config.ini");
            string game = GetIniKey(_launcherIni, "Launcher", "Game", string.Empty);
            if (string.IsNullOrEmpty(game))
            {
                string extracted = Path.Combine(_userDir, "game");
                if (File.Exists(Path.Combine(extracted, "sys", "main.dol")))
                    game = extracted;
            }
            if (string.IsNullOrEmpty(game))
            {
                string bundledFolder = Path.Combine(_appDir, "game");
                if (File.Exists(Path.Combine(bundledFolder, "sys", "main.dol")))
                    game = bundledFolder;
                else
                {
                    string bundledIso = Path.Combine(_appDir, "game.iso");
                    if (File.Exists(bundledIso))
                        game = bundledIso;
                }
            }
            _gameBox.Text = game;

            string resolution = GetIniKey(configPath, "Video", "resolution", DefaultResolution());
            if (Array.IndexOf(ResValues, resolution) < 0)
                resolution = DefaultResolution();
            SelectChoice(_resolution, resolution);

            SelectChoice(_graphics,
                         GetIniKey(_launcherIni, "Launcher", "Graphics", "vulkan"));
            SelectChoice(_window,
                         GetIniKey(_launcherIni, "Launcher", "WindowMode", "borderless"));
            SelectChoice(_audio,
                         GetIniKey(_launcherIni, "Launcher", "Audio", "cubeb"));
            SelectChoice(_aspect,
                         GetIniKey(configPath, "Video", "aspect", "auto"));
            _wideHack.Checked = GetIniKey(configPath, "Video", "widescreen_hack", "false")
                .Equals("true", StringComparison.OrdinalIgnoreCase);

            string fps = GetIniKey(_launcherIni, "Launcher", "ShowFps",
                          GetIniKey(configPath, "Video", "show_fps_in_title", "true"));
            _fps.Checked = !fps.Equals("false", StringComparison.OrdinalIgnoreCase);

            int volume = 100;
            int.TryParse(GetIniKey(configPath, "Video", "volume", "100"), out volume);
            if (volume < 0) volume = 0;
            if (volume > 100) volume = 100;
            _volume.Value = volume;
            _volumeValue.Text = volume + "%";
            _volume.Enabled = Selected(_audio) != "null";

            UpdateSummary();
            UpdateReadyState();
            UpdateControllerStatus();
        }

        private void SaveSettings()
        {
            SetIniKey(_launcherIni, "Launcher", "Game", _gameBox.Text.Trim());
            SetIniKey(_launcherIni, "Launcher", "Resolution", Selected(_resolution));
            SetIniKey(_launcherIni, "Launcher", "Graphics", Selected(_graphics));
            SetIniKey(_launcherIni, "Launcher", "WindowMode", Selected(_window));
            SetIniKey(_launcherIni, "Launcher", "ShowFps", _fps.Checked ? "true" : "false");
            SetIniKey(_launcherIni, "Launcher", "Audio", Selected(_audio));
            SetIniKey(_launcherIni, "Launcher", "Aspect", Selected(_aspect));
            SetIniKey(_launcherIni, "Launcher", "WidescreenHack",
                _wideHack.Checked ? "true" : "false");
            SetIniKey(_launcherIni, "Launcher", "Volume", _volume.Value.ToString(
                CultureInfo.InvariantCulture));

            EnsureConfig(_userDir, _appDir);

            string configPath = Path.Combine(_userDir, "config.ini");
            SetIniKey(configPath, "Video", "resolution", Selected(_resolution));
            SetIniKey(configPath, "Video", "backend",
                Selected(_graphics) == "opengl" ? "OpenGL" : "Vulkan");
            SetIniKey(configPath, "Video", "show_fps_in_title",
                _fps.Checked ? "true" : "false");
            SetIniKey(configPath, "Video", "aspect", Selected(_aspect));
            SetIniKey(configPath, "Video", "widescreen_hack",
                _wideHack.Checked ? "true" : "false");
            SetIniKey(configPath, "Video", "mute",
                Selected(_audio) == "null" ? "true" : "false");
            SetIniKey(configPath, "Video", "volume",
                _volume.Value.ToString(CultureInfo.InvariantCulture));

            SetIniKey(Path.Combine(_userDir, "Config", "GFX.ini"),
                "Settings", "InternalResolution", ResolutionScale(Selected(_resolution)));
            SetIniKey(Path.Combine(_userDir, "Config", "Dolphin.ini"),
                "Core", "GFXBackend", GraphicsName(Selected(_graphics)));
        }

        private static string DefaultResolution()
        {
            return "1920x1080";
        }

        private static string ResolutionScale(string value)
        {
            int index = Array.IndexOf(ResValues, value);
            return index >= 0 ? ResScales[index] : "3";
        }

        private static string ResolutionLabel(string value)
        {
            int index = Array.IndexOf(ResValues, value);
            return index >= 0 ? ResLabels[index] : ResLabels[2];
        }

        private static void SelectChoice(ComboBox box, string value)
        {
            for (int i = 0; i < box.Items.Count; i++)
            {
                Choice choice = box.Items[i] as Choice;
                if (choice != null && choice.Value == value)
                {
                    box.SelectedIndex = i;
                    return;
                }
            }
            if (box.Items.Count > 0)
                box.SelectedIndex = 0;
        }

        private static string Selected(ComboBox box)
        {
            Choice choice = box.SelectedItem as Choice;
            return choice != null ? choice.Value : string.Empty;
        }

        private static string GraphicsName(string value)
        {
            if (value == "d3d12") return "D3D12";
            if (value == "opengl") return "OpenGL";
            return "Vulkan";
        }

        private void UpdateSummary()
        {
            if (_launchSummary == null || _resolution == null)
                return;
            string graphics = GraphicsName(Selected(_graphics));
            string window = Selected(_window) == "fullscreen"
                ? "Fullscreen"
                : (Selected(_window) == "windowed" ? "Windowed" : "Fullscreen windowed");
            string framing = Selected(_aspect) == "wide"
                ? "16:9"
                : (Selected(_aspect) == "standard"
                    ? "4:3"
                    : (Selected(_aspect) == "stretch" ? "Stretch" : "Auto framing (4:3)"));
            if (_wideHack.Checked)
                framing += " + widescreen hack";
            _launchSummary.Text = ResolutionLabel(Selected(_resolution)) +
                " internal  \u2022  " + graphics +
                "\n" + window + "  \u2022  " + framing;
        }

        private void UpdateReadyState()
        {
            string game = _gameBox.Text.Trim();
            bool ready = false;
            bool folder = false;
            if (Directory.Exists(game))
            {
                folder = File.Exists(Path.Combine(game, "sys", "main.dol"));
                ready = folder;
            }
            else if (File.Exists(game))
            {
                string ext = Path.GetExtension(game).ToLowerInvariant();
                ready = ext == ".iso" || ext == ".gcm";
            }

            _badge.SetState(ready ? "GBQE78  \u2022  READY" : "NO GAME IMPORTED",
                            ready ? ReadyGreen : NotReadyRed);
            _gameStatus.Text = ready
                ? (folder
                    ? "USA GBQE78  \u2022  extracted game folder"
                    : "USA GBQE78  \u2022  disc image selected")
                : "Pick your USA disc image or extracted folder.";
            _sideStatus.Text = ready ? "Game files verified" : "Import a disc to begin";
            if (_statusDetail != null && !_busy && _progress != null && _progress.Value == 0)
                _statusDetail.Text = ready
                    ? "Ready to play. Settings save automatically."
                    : "Pick your USA disc image or extracted game folder.";
        }

        private void UpdateControllerStatus()
        {
            if (_controllerStatus == null)
                return;
            string profile = Path.Combine(_userDir, "Config", "GCPadNew.ini");
            _controllerStatus.Text = File.Exists(profile)
                ? "DualSense profile installed."
                : "No controller profile yet. A DualSense profile is installed on first play.";
        }

        private void BrowseIso()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Pick your Rocket Power: Beach Bandits (USA) disc image";
                dialog.Filter = "GameCube disc image (*.iso)|*.iso|All files (*.*)|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _gameBox.Text = dialog.FileName;
            }
        }

        private void BrowseFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Pick the extracted game folder (contains sys\\main.dol and files\\)";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _gameBox.Text = dialog.SelectedPath;
            }
        }

        private void OpenSettings()
        {
            try { Process.Start("explorer.exe", _userDir); }
            catch { }
        }

        private void OpenSaves()
        {
            try
            {
                string saveDir = Path.Combine(_userDir, "GC");
                Directory.CreateDirectory(saveDir);
                Process.Start("explorer.exe", saveDir);
            }
            catch { }
        }

        private void OpenControllerProfile()
        {
            string path = Path.Combine(_userDir, "Config", "GCPadNew.ini");
            if (!File.Exists(path))
            {
                EnsureConfig(_userDir, _appDir);
                UpdateControllerStatus();
            }
            if (!File.Exists(path))
            {
                MessageBox.Show(this, "The controller profile isn't there yet. Play the game once to create it.",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try { Process.Start("notepad.exe", "\"" + path + "\""); }
            catch { }
        }

        private Options BuildOptions()
        {
            string game = _gameBox.Text.Trim();
            if (game.Length == 0)
            {
                MessageBox.Show(this, "Pick your USA ISO or extracted game folder first.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
            if (!Directory.Exists(game) && !File.Exists(game))
            {
                MessageBox.Show(this, "That game path doesn't exist. Pick it again.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            SaveSettings();

            Options o = new Options();
            o.Game = Path.GetFullPath(game);
            o.Install = _appDir;
            o.UserDir = _userDir;
            o.WindowMode = Selected(_window);
            o.Pass.Add("--graphics");
            o.Pass.Add(Selected(_graphics));
            if (Selected(_audio) == "null")
            {
                o.Pass.Add("--audio");
                o.Pass.Add("null");
            }
            return o;
        }

        private void PlayOrPrepare(bool startGame)
        {
            if (_busy)
                return;
            Options o = BuildOptions();
            if (o == null)
                return;

            ShowPage(0);
            _busy = true;
            _gameRunning = false;
            SetControlsEnabled(false);
            _progress.Value = 0;
            _status.Text = startGame ? "Preparing..." : "Checking your copy...";
            _statusDetail.Text = "This can take a minute on the first run.";
            LastError = null;

            Thread worker = new Thread(delegate()
            {
                int code = 1;
                try
                {
                    ProgressSink = UpdateProgress;
                    code = NeedsPrepare(o) ? Prepare(o) : 0;
                    if (code == 0 && startGame)
                    {
                        BeginInvoke(new Action(delegate
                        {
                            _gameRunning = true;
                            Hide();
                        }));
                        code = Start(o);
                    }
                }
                catch (Exception ex)
                {
                    code = 2;
                    LastError = ex.Message;
                }
                finally
                {
                    ProgressSink = null;
                }

                int result = code;
                bool started = startGame;
                BeginInvoke(new Action(delegate
                {
                    if (started)
                    {
                        _gameRunning = false;
                        Show();
                        Activate();
                    }
                    _busy = false;
                    SetControlsEnabled(true);
                    if (result == 0)
                    {
                        _status.Text = started ? "Game closed." : "Game files ready.";
                        _statusDetail.Text = started
                            ? "Settings save automatically."
                            : "You can press Play now.";
                        _progress.Value = 100;
                    }
                    else
                    {
                        _status.Text = started ? "The game couldn't start." : "That copy couldn't be used.";
                        _statusDetail.Text = string.IsNullOrEmpty(LastError)
                            ? "Check your USA ISO or extracted folder, then try again."
                            : LastError;
                        MessageBox.Show(this, _statusDetail.Text, Text,
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }));
            });
            worker.IsBackground = true;
            worker.Start();
        }

        private void SetControlsEnabled(bool enabled)
        {
            _play.Enabled = enabled;
            _prepare.Enabled = enabled;
            _gameBox.Enabled = enabled;
            _resolution.Enabled = enabled;
            _graphics.Enabled = enabled;
            _window.Enabled = enabled;
            _audio.Enabled = enabled;
            _aspect.Enabled = enabled;
            _fps.Enabled = enabled;
            _wideHack.Enabled = enabled;
            _volume.Enabled = enabled && Selected(_audio) != "null";
        }

        private void UpdateProgress(double fraction, string text)
        {
            if (IsDisposed || !IsHandleCreated)
                return;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    if (IsDisposed)
                        return;
                    int value = (int)Math.Round(fraction * 100.0);
                    if (value < 0) value = 0;
                    if (value > 100) value = 100;
                    _progress.Value = value;
                    _status.Text = text;
                }));
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_gameRunning)
            {
                DialogResult result = MessageBox.Show(this,
                    "The game is still running. Close the launcher anyway?",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result != DialogResult.Yes)
                    e.Cancel = true;
            }
            if (!e.Cancel)
            {
                try { SaveSettings(); }
                catch { }
            }
            base.OnFormClosing(e);
        }
    }

    private static class NativeWindow
    {
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const long WS_CAPTION = 0x00C00000L;
        private const long WS_THICKFRAME = 0x00040000L;
        private const long WS_MINIMIZEBOX = 0x00020000L;
        private const long WS_MAXIMIZEBOX = 0x00010000L;
        private const long WS_SYSMENU = 0x00080000L;
        private const long WS_POPUP = 0x80000000L;
        private const long WS_VISIBLE = 0x10000000L;
        private const long WS_EX_DLGMODALFRAME = 0x00000001L;
        private const long WS_EX_CLIENTEDGE = 0x00000200L;
        private const long WS_EX_STATICEDGE = 0x00020000L;
        private const long WS_EX_WINDOWEDGE = 0x00000100L;
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int ENUM_CURRENT_SETTINGS = -1;
        private const int CDS_FULLSCREEN = 0x00000004;
        private const int DISP_CHANGE_SUCCESSFUL = 0;
        private const uint DM_PELSWIDTH = 0x00080000;
        private const uint DM_PELSHEIGHT = 0x00100000;
        private const uint DM_DISPLAYFREQUENCY = 0x00400000;
        private const uint DM_BITSPERPEL = 0x00040000;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOOWNERZORDER = 0x0200;
        private const int SW_SHOW = 5;

        private static bool _saved;
        private static bool _modeChanged;
        private static IntPtr _hwnd;
        private static IntPtr _style;
        private static IntPtr _exStyle;
        private static IntPtr _menu;
        private static RECT _rect;
        private static string _device;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public uint dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmDisplayFlags;
            public uint dmDisplayFrequency;
            public uint dmICMMethod;
            public uint dmICMIntent;
            public uint dmMediaType;
            public uint dmDitherType;
            public uint dmReserved1;
            public uint dmReserved2;
            public uint dmPanningWidth;
            public uint dmPanningHeight;
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX info);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hwnd, int command);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetMenu(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern bool SetMenu(IntPtr hwnd, IntPtr menu);

        [DllImport("user32.dll")]
        private static extern bool DrawMenuBar(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettings(string device, int mode, ref DEVMODE modeInfo);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ChangeDisplaySettingsEx(string device, ref DEVMODE mode, IntPtr hwnd,
                                                          uint flags, IntPtr param);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ChangeDisplaySettingsEx(string device, IntPtr mode, IntPtr hwnd,
                                                          uint flags, IntPtr param);

        public static void SetProcessDpiAwareness()
        {
            try
            {
                SetProcessDpiAwarenessContext(new IntPtr(-4));
            }
            catch
            {
                try { SetProcessDPIAware(); }
                catch { }
            }
        }

        public static void RemoveMenu(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return;
            SetMenu(hwnd, IntPtr.Zero);
            DrawMenuBar(hwnd);
        }

        public static void Enter(IntPtr hwnd, bool changeMode)
        {
            if (_saved)
                return;

            _hwnd = hwnd;
            _style = GetWindowLongPtr(hwnd, GWL_STYLE);
            _exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            _menu = GetMenu(hwnd);
            SetMenu(hwnd, IntPtr.Zero);
            DrawMenuBar(hwnd);
            GetWindowRect(hwnd, out _rect);

            MONITORINFOEX monitor = MonitorInfo(hwnd);
            _device = monitor.szDevice;

            if (changeMode && TrySetMode())
            {
                _modeChanged = true;
                monitor = MonitorInfo(hwnd);
            }

            long style = _style.ToInt64();
            long exStyle = _exStyle.ToInt64();
            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU);
            style |= WS_POPUP | WS_VISIBLE;
            exStyle &= ~(WS_EX_DLGMODALFRAME | WS_EX_CLIENTEDGE | WS_EX_STATICEDGE | WS_EX_WINDOWEDGE);
            SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(style));
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(exStyle));

            SetWindowPos(hwnd, IntPtr.Zero, monitor.rcMonitor.Left, monitor.rcMonitor.Top,
                         monitor.rcMonitor.Right - monitor.rcMonitor.Left,
                         monitor.rcMonitor.Bottom - monitor.rcMonitor.Top,
                         SWP_FRAMECHANGED | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);
            ShowWindow(hwnd, SW_SHOW);
            SetForegroundWindow(hwnd);
            _saved = true;
        }

        public static void Restore()
        {
            if (!_saved)
                return;

            if (_modeChanged)
            {
                try { ChangeDisplaySettingsEx(_device, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero); }
                catch { }
                _modeChanged = false;
            }

            if (_hwnd != IntPtr.Zero)
            {
                SetMenu(_hwnd, _menu);
                DrawMenuBar(_hwnd);
                SetWindowLongPtr(_hwnd, GWL_STYLE, _style);
                SetWindowLongPtr(_hwnd, GWL_EXSTYLE, _exStyle);
                SetWindowPos(_hwnd, IntPtr.Zero, _rect.Left, _rect.Top,
                             _rect.Right - _rect.Left, _rect.Bottom - _rect.Top,
                             SWP_FRAMECHANGED | SWP_SHOWWINDOW | SWP_NOOWNERZORDER);
            }

            _saved = false;
        }

        private static MONITORINFOEX MonitorInfo(IntPtr hwnd)
        {
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            MONITORINFOEX info = new MONITORINFOEX();
            info.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
            GetMonitorInfo(monitor, ref info);
            return info;
        }

        private static bool TrySetMode()
        {
            DEVMODE mode = new DEVMODE();
            mode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            if (!EnumDisplaySettings(_device, ENUM_CURRENT_SETTINGS, ref mode))
                return false;

            mode.dmDisplayFrequency = 60;
            mode.dmBitsPerPel = 32;
            mode.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY | DM_BITSPERPEL;
            return ChangeDisplaySettingsEx(_device, ref mode, IntPtr.Zero, CDS_FULLSCREEN, IntPtr.Zero) ==
                   DISP_CHANGE_SUCCESSFUL;
        }
    }
}
