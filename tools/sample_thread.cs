using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

static class SampleThread
{
    [StructLayout(LayoutKind.Sequential)]
    struct MODULEINFO
    {
        public IntPtr lpBaseOfDll;
        public uint SizeOfImage;
        public IntPtr EntryPoint;
    }

    [StructLayout(LayoutKind.Sequential, Size = 1232)]
    struct CONTEXT
    {
        public ulong P1Home, P2Home, P3Home, P4Home, P5Home, P6Home;
        public uint ContextFlags;
        public uint MxCsr;
        public ushort SegCs, SegDs, SegEs, SegFs, SegGs, SegSs;
        public uint EFlags;
        public ulong Dr0, Dr1, Dr2, Dr3, Dr6, Dr7;
        public ulong Rax, Rcx, Rdx, Rbx, Rsp, Rbp, Rsi, Rdi, R8, R9, R10, R11, R12, R13, R14, R15;
        public ulong Rip;
    }

    const uint THREAD_SUSPEND_RESUME = 0x0002;
    const uint THREAD_GET_CONTEXT = 0x0008;
    const uint THREAD_QUERY_INFORMATION = 0x0040;
    const uint CONTEXT_CONTROL = 0x00100001;
    const uint PROCESS_QUERY_INFORMATION = 0x0400;
    const uint PROCESS_VM_READ = 0x0010;
    const uint LIST_MODULES_ALL = 0x03;

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenThread(uint access, bool inherit, uint threadId);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint SuspendThread(IntPtr hThread);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint ResumeThread(IntPtr hThread);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetThreadContext(IntPtr hThread, IntPtr lpContext);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("psapi.dll", SetLastError = true)]
    static extern bool EnumProcessModulesEx(IntPtr hProcess, [Out] IntPtr[] modules, uint cb, out uint needed, uint filter);
    [DllImport("psapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern uint GetModuleBaseName(IntPtr hProcess, IntPtr hModule, StringBuilder name, uint size);
    [DllImport("psapi.dll", SetLastError = true)]
    static extern bool GetModuleInformation(IntPtr hProcess, IntPtr hModule, out MODULEINFO info, uint cb);

    class Module
    {
        public string Name;
        public ulong Base;
        public ulong End;
    }

    static List<Module> GetModules(IntPtr hProcess)
    {
        var list = new List<Module>();
        IntPtr[] mods = new IntPtr[1024];
        uint needed;
        if (!EnumProcessModulesEx(hProcess, mods, (uint)(mods.Length * IntPtr.Size), out needed, LIST_MODULES_ALL))
            return list;
        int count = (int)(needed / (uint)IntPtr.Size);
        for (int i = 0; i < count; i++)
        {
            MODULEINFO info;
            if (!GetModuleInformation(hProcess, mods[i], out info, (uint)Marshal.SizeOf(typeof(MODULEINFO))))
                continue;
            var sb = new StringBuilder(512);
            GetModuleBaseName(hProcess, mods[i], sb, (uint)sb.Capacity);
            list.Add(new Module
            {
                Name = sb.ToString(),
                Base = (ulong)info.lpBaseOfDll.ToInt64(),
                End = (ulong)info.lpBaseOfDll.ToInt64() + info.SizeOfImage
            });
        }
        list.Sort((a, b) => a.Base.CompareTo(b.Base));
        return list;
    }

    static string Resolve(List<Module> modules, ulong rip)
    {
        int lo = 0, hi = modules.Count - 1, found = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (modules[mid].Base <= rip) { found = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        if (found >= 0 && rip < modules[found].End)
            return modules[found].Name + "," + (rip - modules[found].Base).ToString("X");
        return "unknown," + rip.ToString("X");
    }

    static int Main(string[] args)
    {
        uint pid = 0;
        double seconds = 20;
        int intervalMs = 1;
        string outPath = "samples.csv";
        int topThreads = 1;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pid": pid = uint.Parse(args[++i]); break;
                case "--seconds": seconds = double.Parse(args[++i]); break;
                case "--interval": intervalMs = int.Parse(args[++i]); break;
                case "--out": outPath = args[++i]; break;
                case "--threads": topThreads = int.Parse(args[++i]); break;
            }
        }
        if (pid == 0)
        {
            Console.Error.WriteLine("usage: --pid <pid> [--seconds 20] [--interval 1] [--threads 1] [--out samples.csv]");
            return 2;
        }

        Process proc = Process.GetProcessById((int)pid);
        IntPtr hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, pid);
        if (hProcess == IntPtr.Zero) { Console.Error.WriteLine("OpenProcess failed: " + Marshal.GetLastWin32Error()); return 3; }
        List<Module> modules = GetModules(hProcess);

        // Pick the busiest threads over one second.
        var before = new Dictionary<int, TimeSpan>();
        proc.Refresh();
        foreach (ProcessThread t in proc.Threads) before[t.Id] = t.TotalProcessorTime;
        System.Threading.Thread.Sleep(1000);
        proc.Refresh();
        var busy = new List<KeyValuePair<int, double>>();
        foreach (ProcessThread t in proc.Threads)
        {
            TimeSpan old;
            if (!before.TryGetValue(t.Id, out old)) old = TimeSpan.Zero;
            double ms = (t.TotalProcessorTime - old).TotalMilliseconds;
            busy.Add(new KeyValuePair<int, double>(t.Id, ms));
        }
        busy.Sort((a, b) => b.Value.CompareTo(a.Value));
        var chosen = new List<int>();
        for (int i = 0; i < busy.Count && chosen.Count < topThreads; i++)
        {
            if (busy[i].Value > 5) chosen.Add(busy[i].Key);
        }
        if (chosen.Count == 0) chosen.Add(busy.Count > 0 ? busy[0].Key : 0);
        Console.WriteLine("PID " + pid + " modules " + modules.Count);
        foreach (int id in chosen)
        {
            double ms = 0;
            foreach (var kv in busy) if (kv.Key == id) ms = kv.Value;
            Console.WriteLine("sampling thread " + id + " cpu-ms/s=" + ms.ToString("F0"));
        }

        var handles = new List<IntPtr>();
        foreach (int id in chosen)
        {
            IntPtr h = OpenThread(THREAD_SUSPEND_RESUME | THREAD_GET_CONTEXT | THREAD_QUERY_INFORMATION, false, (uint)id);
            if (h == IntPtr.Zero) Console.Error.WriteLine("OpenThread " + id + " failed: " + Marshal.GetLastWin32Error());
            handles.Add(h);
        }

        var sb = new StringBuilder();
        sb.AppendLine("t_ms,thread,module,offset");
        var sw = Stopwatch.StartNew();
        long ticks = (long)(seconds * 1000 / intervalMs);
        int n = 0;
        IntPtr rawCtx = Marshal.AllocHGlobal(1232 + 16);
        IntPtr ctx = (IntPtr)(((long)rawCtx + 15L) & ~15L);
        while (n < ticks)
        {
            long target = (long)n * intervalMs;
            while (sw.ElapsedMilliseconds < target) System.Threading.Thread.SpinWait(50);
            for (int i = 0; i < handles.Count; i++)
            {
                IntPtr h = handles[i];
                if (h == IntPtr.Zero) continue;
                uint sr = SuspendThread(h);
                if (sr == 0xFFFFFFFF)
                {
                    if (n < 3) Console.Error.WriteLine("SuspendThread " + chosen[i] + " failed: " + Marshal.GetLastWin32Error());
                    continue;
                }
                Marshal.WriteInt32(ctx, 0x30, (int)CONTEXT_CONTROL);
                if (GetThreadContext(h, ctx))
                {
                    ulong rip = (ulong)Marshal.ReadInt64(ctx, 0xF8);
                    sb.Append(sw.ElapsedMilliseconds).Append(',').Append(chosen[i]).Append(',').Append(Resolve(modules, rip)).AppendLine();
                }
                else if (n < 3) Console.Error.WriteLine("GetThreadContext " + chosen[i] + " failed: " + Marshal.GetLastWin32Error());
                ResumeThread(h);
            }
            n++;
        }
        sw.Stop();
        File.WriteAllText(outPath, sb.ToString());
        foreach (IntPtr h in handles) if (h != IntPtr.Zero) CloseHandle(h);
        CloseHandle(hProcess);
        Marshal.FreeHGlobal(rawCtx);
        Console.WriteLine("wrote " + outPath + " samples=" + n * chosen.Count + " elapsed_ms=" + sw.ElapsedMilliseconds);
        return 0;
    }
}
