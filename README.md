# zephyrusfanfix
<img width="1666" height="1315" alt="image" src="https://github.com/user-attachments/assets/0205ea40-8474-4fbd-8d4c-573569e3bea3" />

# Usage

Install the PawnIO driver and the .Net 8.0 Runtime: https://pawnio.eu/ - https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.23-windows-x64-installer (IT WON'T LAUNCH WITHOUT THESE)
If fans get stuck when you quit the app, just change the fan mode and they will continue working normally.
App has 3 modes: 
```
Read Only: Only reads hardware stats.
StockFix: Uses the stock fan control values, but fixes it and you can slightly modify the behaviour if you enable Mode Settings.
CustomCurve: App calculates everything based on your settings, and controls the fans.

Mode Settings:
TempLimit: Fan will update at 100ms regardless of what you've set for ramp up/down, when the temp is above the threshold you specified.
HighTemp: Fan will update at 500ms regardless of what you've set for ramp up/down, when the temp is above the threshold you specified.
Ramp Up: Fan signal will get updated based on the duration you set while accelerating.
Ramp Down: Fan signal will get updated based on the duration you set while decelerating.
FanChangeDelay: Fan will not change speed after the target changes, before the duration you set.
```


# Why? 
g14/g16 2024 & 2025 models are undeniably one of the best windows laptops of today. But small issues such as this and other software issues annoy the user. 

These new g14/g16's have a different BIOS compared to their previous models and the rest of the ASUS lineup. And they have a different fan control code as well, which lacks some logic. Fans oscillate like crazy, they keep overshooting, they're very unstable, and so on. I have reverse engineered the code and to put it extremely simply, this how the logic works:

# ASUS Fan Logic

It first calculates the difference between the target and the current fan speed. (e.g 3500 - 3586)
Then, based on that, it tells the fan controller to increase or decrease the PWM signal that's going to the fan, to make sure the fan keeps spinning at the current target speed, makes sense right? Well, not really.
There's a very important aspect that's completely wrong in this code: Deadzone. A fan will never run at the same speed, it will always have slight fluctuations as it's a motor. 

ASUS's EC code desyncs with it's own functions, the actual fan PWM registers can only be updated with +1/-1 increments, whereas the Target PWM is updated by +2/-2 increments, which lets the Target PWM change before the fan even reaches the Target PWM, hence the fan ends up having to "chase" the continuously changing Target RPM, and in the end, the behaviour is this: 
```
Fan Target: 3500 
Fan Current speed: 
3534 (reduce fan signal) 
3486 (increase fan signal) 
3501 (reduce fan signal)
3450 (increase fan signal)
3564 (reduce fan signal)
3499 (increase fan signal)
```
...and so on. 

The result is an incredibly annoying oscillation that sometimes overshoots by 200-400 RPM, accompanied by all 3 fans that are extremely whiny and separately oscillating on my g14 2024. Here's a video that captures it:

https://github.com/user-attachments/assets/8a8ecb5f-7730-4fc2-8729-f28b0efb3628



In manual mode, this behaviour is way worse due to manual mode having way different (and also wrong) Target PWM calculations and adjustments:
```
Fan Target: 3500
Fan ramps up to 5000, 
Fan slows down to 2000, 
then 4500, 
then 2500, 
then 4000, 
then 3000, 
then 3800, 
then 3300, 
then 3600, 
then 3400, 
and continues oscillating at 3500. // This behaviour is apparently fixed on the 2025 models.https://github.com/Undervoltologist/zephyrusfanfix-24-25/blob/master/README.md
```

This laptop is amazing, it's as if it's a direct replacement to MacBooks while some aspects being even better. But small issues like this makes it annoying to use, requiring people to use 3rd party replacements. This can easily be fixed by a few lines of code. 

# What this app does

Read stock fan curve target RPM from the EC. (hence it's perfectly safe thermals wise, it uses the exact same fan curve of your laptop)

If the target RPM changes, it checks if the current speed is greater or less than the target, and decides fan should speed up or slow down. 

When it gets within 15 RPM's from the target (Target RPM - Current RPM <= 15) the app locks the fan signal, to be never changed again till: new fan target comes up / difference between current RPM and target RPM becomes more than 100.

Result is a perfectly behaving smooth fan control without any annoying oscillation. 

# What should ASUS do to fix this?
The fan control needs to have a deadzone, such as 100 RPM, so as long as "Current RPM - Target RPM = not more than 100 and not less than 100" it won't try to change and "stabilize" fan speed. This will fix the oscillation.

FIX THIS ASUS, IT'LL BECOME A MUCH BETTER FEELING LAPTOP TO USE FOR THE AVERAGE USER. THE ONLY REASON I DID THIS IS BECAUSE THE LAPTOP IS UNBEARABLE TO USE WITH THESE FAN CURVES + OSCILLATION. THE FAN TABLES ARE ALSO REALLY BAD.

# DISCLAIMER
THIS APP READS AND WRITES TO THE EC CHIP FOUND ON YOUR LAPTOP DIRECTLY, ALTERING BEHAVIOUR. I DON'T TAKE ANY RESPONSIBILIITIES REGARDING POTENTIAL ISSUES OR DAMAGE CAUSED BY THIS APP. G16 2024 AND G14/16 2025 HASN'T BEEN TESTED YET.


