using System;
using System.IO;
using System.Runtime.InteropServices;

internal static class SetIcon
{
    private const uint RtIcon = 3;
    private const uint RtGroupIcon = 14;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr BeginUpdateResource(string fileName, bool deleteExistingResources);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateResource(IntPtr update, IntPtr type, IntPtr name,
                                              ushort language, byte[] data, uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool EndUpdateResource(IntPtr update, bool discard);

    private static IntPtr Id(int value)
    {
        return new IntPtr(value);
    }

    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("usage: set_icon <exe> <ico>");
            return 2;
        }

        string exe = Path.GetFullPath(args[0]);
        string ico = Path.GetFullPath(args[1]);
        if (!File.Exists(exe) || !File.Exists(ico))
        {
            Console.Error.WriteLine("set_icon: file not found");
            return 2;
        }

        byte[] iconFile = File.ReadAllBytes(ico);
        if (iconFile.Length < 6 || BitConverter.ToUInt16(iconFile, 0) != 0 ||
            BitConverter.ToUInt16(iconFile, 2) != 1)
        {
            Console.Error.WriteLine("set_icon: not an icon file");
            return 2;
        }

        int count = BitConverter.ToUInt16(iconFile, 4);
        if (count <= 0)
        {
            Console.Error.WriteLine("set_icon: icon has no images");
            return 2;
        }

        int[] widths = new int[count];
        int[] heights = new int[count];
        int[] colors = new int[count];
        int[] planes = new int[count];
        int[] bits = new int[count];
        byte[][] images = new byte[count][];

        for (int i = 0; i < count; i++)
        {
            int entry = 6 + i * 16;
            widths[i] = iconFile[entry];
            heights[i] = iconFile[entry + 1];
            colors[i] = iconFile[entry + 2];
            planes[i] = BitConverter.ToUInt16(iconFile, entry + 4);
            bits[i] = BitConverter.ToUInt16(iconFile, entry + 6);
            int bytes = (int)BitConverter.ToUInt32(iconFile, entry + 8);
            int offset = (int)BitConverter.ToUInt32(iconFile, entry + 12);
            images[i] = new byte[bytes];
            Buffer.BlockCopy(iconFile, offset, images[i], 0, bytes);
        }

        using (MemoryStream groupStream = new MemoryStream())
        using (BinaryWriter group = new BinaryWriter(groupStream))
        {
            group.Write((ushort)0);
            group.Write((ushort)1);
            group.Write((ushort)count);
            for (int i = 0; i < count; i++)
            {
                group.Write((byte)widths[i]);
                group.Write((byte)heights[i]);
                group.Write((byte)colors[i]);
                group.Write((byte)0);
                group.Write((ushort)planes[i]);
                group.Write((ushort)bits[i]);
                group.Write((uint)images[i].Length);
                group.Write((ushort)(i + 1));
            }
            group.Flush();

            IntPtr update = BeginUpdateResource(exe, false);
            if (update == IntPtr.Zero)
            {
                Console.Error.WriteLine("set_icon: BeginUpdateResource failed: " +
                                        Marshal.GetLastWin32Error());
                return 2;
            }

            bool ok = true;
            for (int i = 0; i < count; i++)
            {
                if (!UpdateResource(update, Id((int)RtIcon), Id(i + 1), 0,
                                    images[i], (uint)images[i].Length))
                {
                    ok = false;
                    break;
                }
            }

            if (ok)
            {
                byte[] groupBytes = groupStream.ToArray();
                ok = UpdateResource(update, Id((int)RtGroupIcon), Id(1), 0,
                                    groupBytes, (uint)groupBytes.Length);
            }

            if (!EndUpdateResource(update, !ok))
            {
                Console.Error.WriteLine("set_icon: EndUpdateResource failed: " +
                                        Marshal.GetLastWin32Error());
                return 2;
            }

            if (!ok)
            {
                Console.Error.WriteLine("set_icon: UpdateResource failed: " +
                                        Marshal.GetLastWin32Error());
                return 2;
            }
        }

        return 0;
    }
}
