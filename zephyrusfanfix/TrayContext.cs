using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ZephyrusFanFix.Generic;
using ZephyrusFanFix.Logic;

namespace ZephyrusFanFix;

public class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _updateTimer;
    private readonly GlobalVariables _globals;
    private readonly FanStabilizer _fanCpu, _fanGpu, _fanSys;

    public TrayContext()
    {
        _globals = GlobalVariables.LoadFromFile("config.json");
        PawnIO.Initialize();

        _fanCpu = new FanStabilizer("CPU", _globals.CpuDcr, _globals.CpuRpmLow, _globals.CpuRpmHigh, _globals.CpuTargetRpmHigh, _globals.CpuTargetRpmLow, _globals.CpuTargetPwm);
        _fanGpu = new FanStabilizer("GPU", _globals.GpuDcr, _globals.GpuRpmLow, _globals.GpuRpmHigh, _globals.GpuTargetRpmHigh, _globals.GpuTargetRpmLow, _globals.GpuTargetPwm);
        _fanSys = new FanStabilizer("SYS", _globals.SysDcr, _globals.SysRpmLow, _globals.SysRpmHigh, _globals.SysTargetRpmHigh, _globals.SysTargetRpmLow, _globals.SysTargetPwm);

        _notifyIcon = new NotifyIcon { Icon = SystemIcons.Application, Visible = true };
        _notifyIcon.ContextMenuStrip = new ContextMenuStrip();
        _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, (s, e) => ExitApp());

        _updateTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _updateTimer.Tick += (s, e) => {
            if (!PawnIO.IsInitialized) return;
            _fanCpu.Update(_globals);
            _fanGpu.Update(_globals);
            _fanSys.Update(_globals);
            UpdateTooltip();
        };
        _updateTimer.Start();
    }

    private void UpdateTooltip()
    {
        StringBuilder sb = new StringBuilder();
        // Print tray
        sb.Append($"CPU: {_fanCpu.CurrentTemp}°C | DCR:{_fanCpu.CurrentDcrValue} | {_fanCpu.CurrentRpm}/{_fanCpu.TargetRpm} RPM\n");
        sb.Append($"GPU: {_fanGpu.CurrentTemp}°C | DCR:{_fanGpu.CurrentDcrValue} | {_fanGpu.CurrentRpm}/{_fanGpu.TargetRpm} RPM\n");
        sb.Append($"SYS: {_fanSys.CurrentTemp}°C | DCR:{_fanSys.CurrentDcrValue} | {_fanSys.CurrentRpm}/{_fanSys.TargetRpm} RPM");

        string status = sb.ToString();
        if (status.Length >= 128) status = status.Substring(0, 127);
        if (_notifyIcon.Text != status) _notifyIcon.Text = status;
    }

    private void ExitApp()
    {
        _updateTimer.Stop();
        if (PawnIO.IsInitialized)
        {
            // Enable fan control on exit
            PawnIO.DirectEcWrite(_globals.ActiveAddr, _globals.ActiveData, GlobalVariables.FanControlReg, GlobalVariables.ControlEnable);
            System.Threading.Thread.Sleep(50);
        }
        _notifyIcon.Visible = false;
        PawnIO.Close();
        Application.Exit();
        Environment.Exit(0);
    }
}