using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZephyrusFanFix.Generic;

public class ModeSettings
{
    public int CpuTempLimit { get; set; } = 87;
    public int CpuHighTemp { get; set; } = 80;
    public int GpuTempLimit { get; set; } = 87;
    public int GpuHighTemp { get; set; } = 82;

    public int FanStartDelay { get; set; } = 2000;
    public int CpuRampUp { get; set; } = 2000;
    public int CpuRampDown { get; set; } = 2000;
    public int GpuRampUp { get; set; } = 2000;
    public int GpuRampDown { get; set; } = 2000;
    public int SysRampUp { get; set; } = 1000;
    public int SysRampDown { get; set; } = 1000;
}
public class GlobalVariables
{
    public byte EcAddrPort { get; set; } = 0x2E;
    public byte EcDataPort { get; set; } = 0x2F;
    public byte EcAddrPort2 { get; set; } = 0x2E;
    public byte EcDataPort2 { get; set; } = 0x2F;
    public bool UseSecondaryPorts { get; set; } = true;

    [JsonIgnore] public byte ActiveAddr => UseSecondaryPorts ? EcAddrPort2 : EcAddrPort;
    [JsonIgnore] public byte ActiveData => UseSecondaryPorts ? EcDataPort2 : EcDataPort;

    // Modes
    public ModeSettings Performance { get; set; } = new();
    public ModeSettings Turbo { get; set; } = new();
    public ModeSettings Silent { get; set; } = new();

    // EC Regs
    public ushort ModeReg { get; set; } = 0x306;
    public ushort CpuTempReg { get; set; } = 0x358;
    public ushort GpuTempReg { get; set; } = 0x3c5;
    public ushort SysTempReg { get; set; } = 0x450;

    public const ushort FanControlReg = 0x484;
    public const byte ControlEnable = 0;
    public const byte ControlDisable = 8;
    
    public ushort CpuTargetPwm { get; set; } = 0x457;
    public ushort CpuDcr { get; set; } = 0x1806;
    public ushort CpuRpmLow { get; set; } = 0x181E;
    public ushort CpuRpmHigh { get; set; } = 0x181F;
    public ushort CpuTargetRpmHigh { get; set; } = 0x4AE;
    public ushort CpuTargetRpmLow { get; set; } = 0x4AF;

    public ushort GpuTargetPwm { get; set; } = 0x44D;
    public ushort GpuDcr { get; set; } = 0x1807;
    public ushort GpuRpmLow { get; set; } = 0x1820;
    public ushort GpuRpmHigh { get; set; } = 0x1821;
    public ushort GpuTargetRpmHigh { get; set; } = 0x4B0;
    public ushort GpuTargetRpmLow { get; set; } = 0x4B1;

    public ushort SysTargetPwm { get; set; } = 0x4E1;
    public ushort SysDcr { get; set; } = 0x1808;    
    public ushort SysRpmLow { get; set; } = 0x1845;
    public ushort SysRpmHigh { get; set; } = 0x1846;
    public ushort SysTargetRpmHigh { get; set; } = 0x4E4;
    public ushort SysTargetRpmLow { get; set; } = 0x4E5;

    public static GlobalVariables LoadFromFile(string path) =>
        File.Exists(path) ? JsonSerializer.Deserialize<GlobalVariables>(File.ReadAllText(path)) ?? new() : new();
}
