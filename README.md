# zephyrusfanfix

<img width="385" height="215" alt="image" src="https://github.com/user-attachments/assets/b915dc0d-cd1e-47f1-9828-d0c3b108a729" />

# Usage



# Why? 
g14/g16 2024 & 2025 models are undeniably one of the best windows laptops of today. But small issues such as this and other software issues annoy the user. 

These new g14/g16's have a different BIOS compared to their previous models and the rest of the ASUS lineup. And they have a different fan control code as well, which lacks some logic. Fans oscillate like crazy, they keep overshooting, they're very unstable, and so on. I have reverse engineered the code and to put it extremely simply, this how the logic works:

It first calculates the difference between the target and the current fan speed. (e.g 3500 - 3586)
Then, based on that, it tells the fan controller to increase or decrease the PWM signal that's going to the fan, to make sure the fan keeps spinning at the current target speed, makes sense right? Well, not really.
There's a very important aspect that's missing in this code: Deadzone. A fan will never run at the same speed, it will always have slight fluctuations as it's a motor. But ASUS's EC code doesn't take this into account, so this is what happens: 
Fan Target: 3500 
Fan Current speed: 
3534 (reduce fan signal) 
3486 (increase fan signal) 
3501 (reduce fan signal)
3450 (increase fan signal)
3564 (reduce fan signal)
3499 (increase fan signal)
...and so on.

The result is an incredibly annoying oscillation that sometimes overshoots by 200-400 RPM, accompanied by all 3 fans that are separately oscillating on my g14 2024. In manual mode, this behaviour is way worse due to some rules in the code being disabled because it's manual mode:
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

This laptop is amazing, it's as if it's a direct replacement to MacBooks while some aspects being even better. But small issues like this makes it worse. This can easily be fixed by a few lines of code, like the tray app I've done. This app reads the target RPM from the EC (hence it's perfectly safe thermals wise, it uses the exact same fan curve of your laptop) and if the current target is higher or lower than the current fan speed, it starts changing the fan signal, till the current fan speed is 25 RPM's within the target RPM, and it locks the the signal till the target RPM changes again. Result is a perfectly behaving smooth fan control without any annoying oscillation. 


