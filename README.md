# zephyrusfanfix

<img width="387" height="217" alt="image" src="https://github.com/user-attachments/assets/c72116fe-379f-43ee-83b1-d0da485fbf81" />


# Usage

First install the PawnIO driver: https://pawnio.eu/ (IT WON'T LAUNCH WITHOUT THE DRIVER)
If fans get stuck when you quit the app, just change the fan mode and they will continue working normally.
Adjust the following settings for each mode in config.json 
```
    TOO LOW VALUES CAN BE DANGEROUS AND CRASH YOUR LAPTOP (e.g less than 125ms)
    "UseSecondaryPorts": false, // If the app outputs 65536 and such, change this to "true", might work.
    
    "CpuTempLimit": 95, // When this temp is reached, the app will update the fan signal at 100ms, bypassing the value you set for ramp up/down, resulting in faster acceleration
    "CpuHighTemp": 90, // When this temp is reached, the app will update the fan signal at 500ms, bypassing the value you set for ramp up/down, resulting in faster acceleration
    
    "GpuTempLimit": 87, // When this temp is reached, the app will update the fan signal at 100ms, bypassing the value you set for ramp up/down, resulting in faster acceleration
    "GpuHighTemp": 80, // When this temp is reached, the app will update the fan signal at 500ms, bypassing the value you set for ramp up/down, resulting in faster acceleration
    
    "CpuRampUp": 2000, // CPU Fan will be updated every 2000ms when it's ramping up
    "CpuRampDown": 2000, // CPU Fan will be updated every 2000ms when it's ramping down
    
    "GpuRampUp": 2000, // GPU Fan will be updated every 2000ms when it's ramping up
    "GpuRampDown": 2000, // GPU Fan will be updated every 2000ms when it's ramping down
    
    "SysRampUp": 250, // SYS Fan will be updated every 250ms when it's ramping up
    "SysRampDown": 500 // SYS Fan will be updated every 500ms when it's ramping down
```


# Why? 
g14/g16 2024 & 2025 models are undeniably one of the best windows laptops of today. But small issues such as this and other software issues annoy the user. 

These new g14/g16's have a different BIOS compared to their previous models and the rest of the ASUS lineup. And they have a different fan control code as well, which lacks some logic. Fans oscillate like crazy, they keep overshooting, they're very unstable, and so on. I have reverse engineered the code and to put it extremely simply, this how the logic works:

# ASUS Fan Logic

It first calculates the difference between the target and the current fan speed. (e.g 3500 - 3586)
Then, based on that, it tells the fan controller to increase or decrease the PWM signal that's going to the fan, to make sure the fan keeps spinning at the current target speed, makes sense right? Well, not really.
There's a very important aspect that's missing in this code: Deadzone. A fan will never run at the same speed, it will always have slight fluctuations as it's a motor. But ASUS's EC code doesn't take this into account, so this is what happens: 
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

The result is an incredibly annoying oscillation that sometimes overshoots by 200-400 RPM, accompanied by all 3 fans that are separately oscillating on my g14 2024. Here's a video that captures it:

https://github.com/user-attachments/assets/8a8ecb5f-7730-4fc2-8729-f28b0efb3628



In manual mode, this behaviour is way worse due to some rules in the code being disabled in manual mode:
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
and continues oscillating at 3500. // This behaviour is apparently fixed on the 2025 models.
```

This laptop is amazing, it's as if it's a direct replacement to MacBooks while some aspects being even better. But small issues like this makes it annoying to use, requiring people to use 3rd party replacements. This can easily be fixed by a few lines of code, like the tray app I've done. 

# What this app does

```
Read stock fan curve target RPM from the EC. (hence it's perfectly safe thermals wise, it uses the exact same fan curve of your laptop)

If the target RPM changes, it checks if the current speed is greater or less than the target, and decides fan should speed up or slow down. 

When it gets within 25 RPM's from the target (Target RPM - Current RPM <= 25) the app locks the fan signal, to be never changed again till new fan target.

Result is a perfectly behaving smooth fan control without any annoying oscillation. 
```
# What should ASUS do to fix this?
The fan control needs to have a deadzone, such as 75 RPM, so as long as "Current RPM - Target RPM = not more than 75 and not less than 75" it won't try to change and "stabilize" fan speed. This will fix the oscillation.

FIX THIS ASUS, IT'LL BECOME A MUCH BETTER FEELING LAPTOP TO USE FOR THE AVERAGE USER.

# DISCLAIMER
THIS APP READS AND WRITES TO THE EC CHIP FOUND ON YOUR LAPTOP DIRECTLY, ALTERING BEHAVIOUR. I DON'T TAKE ANY RESPONSIBILIITIES REGARDING POTENTIAL ISSUES OR DAMAGE CAUSED BY THIS APP. G16 2024 AND G14/16 2025 HASN'T BEEN TESTED YET.


