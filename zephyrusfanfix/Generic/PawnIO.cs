using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace ZephyrusFanFix.Generic;

public static class PawnIO
{
    private static SafeFileHandle _driverHandle;
    private static int _currentSlot = -1;

    private const uint DEVICE_TYPE = 41394u << 16;
    private const uint IOCTL_LOAD_BINARY = DEVICE_TYPE | (0x821 << 2);
    private const uint IOCTL_EXECUTE_FN = DEVICE_TYPE | (0x841 << 2);
    private const int FN_NAME_LENGTH = 32;

    public static bool IsInitialized = false;

    public static bool Initialize()
    {
        if (IsInitialized) return true;

        try
        {
            EnsureDriverInstalled();

            _driverHandle = CreateFile(@"\\.\Global\PawnIO", 0xC0000000, 0, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (_driverHandle.IsInvalid)
            {
                _driverHandle = CreateFile(@"\\.\PawnIO", 0xC0000000, 0, IntPtr.Zero, 3, 0, IntPtr.Zero);
                if (_driverHandle.IsInvalid) return false;
            }

            // Automatically finds the .bin file inside your resources
            byte[] binBytes = LoadResourceBytes("LpcIO.bin");

            DeviceIoControl(_driverHandle, IOCTL_LOAD_BINARY, binBytes, (uint)binBytes.Length, null, 0, out _, IntPtr.Zero);
            ExecutePawn("ioctl_find_bars");

            IsInitialized = true;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PawnIO Init Failed: {ex.Message}");
            return false;
        }
    }

    private static void EnsureDriverInstalled()
    {
        string destPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PawnIO.sys");
        if (!File.Exists(destPath))
        {
            // Automatically finds the .sys file inside your resources
            byte[] sysBytes = LoadResourceBytes("PawnIO.sys");
            File.WriteAllBytes(destPath, sysBytes);
        }
    }

    private static byte[] LoadResourceBytes(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        // This finds the resource even if your project name is different
        string resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(r => r.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
            throw new FileNotFoundException($"Resource containing '{fileName}' not found. Make sure Build Action is 'Embedded Resource'.");

        using var stream = assembly.GetManifestResourceStream(resourceName);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static void EnsureSlotForPort(uint port)
    {
        int targetSlot = (port == 0x2E || port == 0x2F) ? 0 : (port == 0x4E || port == 0x4F) ? 1 : -1;
        if (targetSlot != -1 && _currentSlot != targetSlot)
        {
            ExecutePawn("ioctl_select_slot", (long)targetSlot);
            _currentSlot = targetSlot;
        }
    }

    public static byte DirectEcRead(byte ecAddrPort, byte ecDataPort, ushort addr)
    {
        WriteIoPortByte(ecAddrPort, 0x2E);
        WriteIoPortByte(ecDataPort, 0x11);
        WriteIoPortByte(ecAddrPort, 0x2F);
        WriteIoPortByte(ecDataPort, (byte)((addr >> 8) & 0xFF));
        WriteIoPortByte(ecAddrPort, 0x2E);
        WriteIoPortByte(ecDataPort, 0x10);
        WriteIoPortByte(ecAddrPort, 0x2F);
        WriteIoPortByte(ecDataPort, (byte)(addr & 0xFF));
        WriteIoPortByte(ecAddrPort, 0x2E);
        WriteIoPortByte(ecDataPort, 0x12);
        WriteIoPortByte(ecAddrPort, 0x2F);
        return ReadIoPortByte(ecDataPort);
    }

    public static void DirectEcWrite(byte ecAddrPort, byte ecDataPort, ushort addr, byte data)
    {
        WriteIoPortByte(ecAddrPort, 0x2E);
        WriteIoPortByte(ecDataPort, 0x11);
        WriteIoPortByte(ecAddrPort, 0x2F);
        WriteIoPortByte(ecDataPort, (byte)((addr >> 8) & 0xFF));
        WriteIoPortByte(ecAddrPort, 0x2E);
        WriteIoPortByte(ecDataPort, 0x10);
        WriteIoPortByte(ecAddrPort, 0x2F);
        WriteIoPortByte(ecDataPort, (byte)(addr & 0xFF));
        WriteIoPortByte(ecAddrPort, 0x2E);
        WriteIoPortByte(ecDataPort, 0x12);
        WriteIoPortByte(ecAddrPort, 0x2F);
        WriteIoPortByte(ecDataPort, data);
    }

    public static byte ReadIoPortByte(uint port)
    {
        EnsureSlotForPort(port);
        var result = ExecutePawn("ioctl_pio_inb", (long)port);
        return result.Length > 0 ? (byte)(result[0] & 0xFF) : (byte)0;
    }

    public static void WriteIoPortByte(uint port, byte value)
    {
        EnsureSlotForPort(port);
        ExecutePawn("ioctl_pio_outb", (long)port, (long)value);
    }

    private static long[] ExecutePawn(string funcName, params long[] args)
    {
        if (_driverHandle == null || _driverHandle.IsInvalid) return Array.Empty<long>();
        int inputSize = FN_NAME_LENGTH + (args.Length * 8);
        byte[] inputBuffer = new byte[inputSize];
        Encoding.ASCII.GetBytes(funcName).CopyTo(inputBuffer, 0);
        if (args.Length > 0) Buffer.BlockCopy(args, 0, inputBuffer, FN_NAME_LENGTH, args.Length * 8);
        byte[] outputBuffer = new byte[8];
        if (DeviceIoControl(_driverHandle, IOCTL_EXECUTE_FN, inputBuffer, (uint)inputBuffer.Length, outputBuffer, 8, out uint bytesReturned, IntPtr.Zero))
        {
            if (bytesReturned >= 8) return new[] { BitConverter.ToInt64(outputBuffer, 0) };
        }
        return Array.Empty<long>();
    }

    public static void Close()
    {
        if (_driverHandle != null && !_driverHandle.IsInvalid) _driverHandle.Close();
        IsInitialized = false;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, byte[] lpInBuffer, uint nInBufferSize, byte[] lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);
}