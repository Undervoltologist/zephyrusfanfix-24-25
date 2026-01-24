using ZephyrusFanFix.Generic;
using System;
using System.IO;

namespace ZephyrusFanFix.Logic;

public class FanStabilizer
{
    private readonly string _name;
    private readonly ushort _dcrAddr, _tachL, _tachH, _targetH, _targetL, _pwmTargetAddr;

    public int CurrentRpm { get; private set; }
    public int TargetRpm { get; private set; }
    public int CurrentTemp { get; private set; }
    public byte CurrentDcrValue { get; private set; }

    private int _lastTargetRpm = -1;
    private byte _currentActivePwm = 0;
    private bool _isLocked = false, _isInitialized = false, _isFanForceDisabled = false;
    private bool _hasSentFinalZero = false;

    private DateTime _lastStepTime = DateTime.MinValue;
    private DateTime _targetDetectedTime = DateTime.MinValue;

    private const byte MaxPwm = 200;
    private const byte SnapDownThreshold = 30;

    private readonly string _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_log.txt");

    public FanStabilizer(string name, ushort dcr, ushort tL, ushort tH, ushort tgH, ushort tgL, ushort pwmT)
    {
        _name = name; _dcrAddr = dcr; _tachL = tL; _tachH = tH;
        _targetH = tgH; _targetL = tgL; _pwmTargetAddr = pwmT;
    }

    private void Log(string message) =>
        File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss.fff}] [{_name}] {message}{Environment.NewLine}");

    public void Update(GlobalVariables globals)
    {
        // 1. DATA & MODE
        byte modeVal = PawnIO.DirectEcRead(globals.ActiveAddr, globals.ActiveData, globals.ModeReg);
        ModeSettings cfg = modeVal switch { 0x01 => globals.Turbo, 0x02 => globals.Silent, _ => globals.Performance };

        CurrentRpm = ReadActualRpm(globals);
        int rawBiosTarget = ReadBiosTargetRpm(globals);
        CurrentDcrValue = PawnIO.DirectEcRead(globals.ActiveAddr, globals.ActiveData, _dcrAddr);

        int cpuTemp = PawnIO.DirectEcRead(globals.ActiveAddr, globals.ActiveData, globals.CpuTempReg);
        int gpuTemp = PawnIO.DirectEcRead(globals.ActiveAddr, globals.ActiveData, globals.GpuTempReg);
        CurrentTemp = _name switch { "GPU" => gpuTemp, "SYS" => PawnIO.DirectEcRead(globals.ActiveAddr, globals.ActiveData, globals.SysTempReg), _ => cpuTemp };

        bool thermalPanic = (cpuTemp >= cfg.CpuTempLimit) || (gpuTemp >= cfg.GpuTempLimit);
        bool isHighTemp = (cpuTemp >= cfg.CpuHighTemp) || (gpuTemp >= cfg.GpuHighTemp);

        // 2. TARGET & DELAY
        if (thermalPanic || isHighTemp)
        {
            _isFanForceDisabled = false;
            _targetDetectedTime = DateTime.MinValue;
        }
        else if (rawBiosTarget > 0)
        {
            if (!_isInitialized)
            {
                _isFanForceDisabled = false;
            }
            else if (_isFanForceDisabled)
            {
                if (_targetDetectedTime == DateTime.MinValue) _targetDetectedTime = DateTime.Now;
                if ((DateTime.Now - _targetDetectedTime).TotalMilliseconds >= cfg.FanStartDelay)
                {
                    _isFanForceDisabled = false;
                }
            }
        }
        else
        {
            _isFanForceDisabled = true;
            _targetDetectedTime = DateTime.MinValue;
        }

        TargetRpm = _isFanForceDisabled ? 0 : rawBiosTarget;

        // 3. INIT
        if (!_isInitialized)
        {
            _currentActivePwm = PawnIO.DirectEcRead(globals.ActiveAddr, globals.ActiveData, _pwmTargetAddr);
            _lastTargetRpm = TargetRpm;
            _isInitialized = true;
        }

        // 4. KICKSTART & RESET LOGIC
        if (TargetRpm > 0)
        {
            _hasSentFinalZero = false;

            if (CurrentRpm == 0 && _currentActivePwm < 28)
            {
                _currentActivePwm = 28;
                ApplyPwm(globals);
                _isLocked = false;
            }
        }
        else if (TargetRpm == 0 && CurrentRpm == 0 && !_hasSentFinalZero)
        {
            _currentActivePwm = 0;
            ApplyPwm(globals);
            _hasSentFinalZero = true;
        }

        // 5. LOCK ENFORCEMENT
        if (_isLocked && TargetRpm == _lastTargetRpm)
        {
            PawnIO.DirectEcWrite(globals.ActiveAddr, globals.ActiveData, GlobalVariables.FanControlReg, GlobalVariables.ControlDisable);
            return;
        }

        // 6. TIERED RAMP INTERVAL
        bool isRampingUp = CurrentRpm < TargetRpm;
        int baseRamp = isRampingUp ?
            (_name == "CPU" ? cfg.CpuRampUp : (_name == "GPU" ? cfg.GpuRampUp : cfg.SysRampUp)) :
            (_name == "CPU" ? cfg.CpuRampDown : (_name == "GPU" ? cfg.GpuRampDown : cfg.SysRampDown));

        int finalRamp = baseRamp;
        if (isRampingUp)
        {
            if (thermalPanic) finalRamp = Math.Min(100, baseRamp);
            else if (isHighTemp) finalRamp = Math.Min(500, baseRamp);
        }
        else if (TargetRpm == 0)
        {
            finalRamp = Math.Min(500, baseRamp);
        }

        // 7. ADJUSTMENT LOOP
        if (Math.Abs(CurrentRpm - TargetRpm) <= 25 && TargetRpm > 1)
        {
            _isLocked = true;
            _lastTargetRpm = TargetRpm;
            return;
        }

        if ((DateTime.Now - _lastStepTime).TotalMilliseconds >= finalRamp)
        {
            bool changed = false;
            _isLocked = false;

            if (isRampingUp && _currentActivePwm < MaxPwm)
            {
                _currentActivePwm++;
                changed = true;
            }
            else if (CurrentRpm > TargetRpm)
            {
                if (TargetRpm == 0)
                {
                    if (_currentActivePwm > 0) { _currentActivePwm--; changed = true; }
                }
                else
                {
                    if (_currentActivePwm <= SnapDownThreshold)
                    {
                        if (_currentActivePwm != SnapDownThreshold) { _currentActivePwm = SnapDownThreshold; changed = true; }
                    }
                    else { _currentActivePwm--; changed = true; }
                }
            }

            if (changed)
            {
                ApplyPwm(globals);
                _lastStepTime = DateTime.Now;
            }
            _lastTargetRpm = TargetRpm;
        }
    }

    private void ApplyPwm(GlobalVariables globals)
    {
        byte eff = (_name == "SYS" && _currentActivePwm < MaxPwm && _currentActivePwm > 0) ? (byte)(_currentActivePwm + 1) : _currentActivePwm; // SYS FAN PWM offset fix attempt

        PawnIO.DirectEcWrite(globals.ActiveAddr, globals.ActiveData, GlobalVariables.FanControlReg, GlobalVariables.ControlDisable);
        PawnIO.DirectEcWrite(globals.ActiveAddr, globals.ActiveData, _pwmTargetAddr, eff);
        PawnIO.DirectEcWrite(globals.ActiveAddr, globals.ActiveData, _dcrAddr, eff);
    }

    public void Shutdown(GlobalVariables g)
    {
        PawnIO.DirectEcWrite(g.ActiveAddr, g.ActiveData, _dcrAddr, 0x00);
        PawnIO.DirectEcWrite(g.ActiveAddr, g.ActiveData, GlobalVariables.FanControlReg, 0x00);
    }

    private int ReadActualRpm(GlobalVariables g)
    {
        int c = PawnIO.DirectEcRead(g.ActiveAddr, g.ActiveData, _tachL) + (PawnIO.DirectEcRead(g.ActiveAddr, g.ActiveData, _tachH) << 8);
        return (c <= 0 || c == 0xFFFF) ? 0 : 2156250 / c;
    }

    private int ReadBiosTargetRpm(GlobalVariables g) =>
        (PawnIO.DirectEcRead(g.ActiveAddr, g.ActiveData, _targetH) << 8) | PawnIO.DirectEcRead(g.ActiveAddr, g.ActiveData, _targetL);
}