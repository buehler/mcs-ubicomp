# Assignment 1 - Results

## Task 1

### a

Power consumption documentation: https://www.espruino.com/Puck.js#power-consumption
Accelerometer: `Puck.js v2: Accelerometer on Puck.accelOn() (12.5Hz) : 350uA``
BLE default: 600uA
5ms \* 100% CPU (4000uA) every 1.6 s

60s / 1.6s = 37.5 executions per minute.
1 execution = 5ms 100% CPU (4000uA)

plus the accelerometer and BLE per second.

37.5 \* 4000uA + 60 \* 350uA + 50 \* 600uA = 207000uA

Consumption: 207000uA/min = 3450uA/s = 3.45mA/s = 12420mA/h

### b

P = U \* I (Watt = Volt \* Ampere)
E = P \* t (Joules = Watt \* Seconds)

One exectuion = 2.5mW \* 1.6s = 4 Joules

Spec Energizer CR2032, typical capacity: 235 mAh (https://data.energizer.com/pdfs/cr2032.pdf)
Current: 3V

3V \* 0.235Ah \* 3600s = 2538J

2538J / 4J = 634.5 times

### c

Assuming that I understand the question correctly... ;-)

Use the power trace and the signal to calculate (with the formulas above) the usage.
The powertrace over time gives me W per timeunit (us or s or whatever). Which then can be converted
to Joules and thus gives us the consumption.

## Task 2

according to the spec of the sensor: 1LSb = 0.061 mg at ±2 g full scale.

```javascript
console.log('Start classifier');

setWatch(
  () => {
    console.log('Finish, button pressed');
    Puck.accelOff();
  },
  BTN,
  { edge: 'rising', debounce: 50, repeat: false }
);

// g per LSb
const scale = 0.000061;
let pkgCount = 0;

const map = (raw) => {
  const g = raw * scale;
  const mapToBit = Math.round((g / 2) * 127);
  return E.clip(mapToBit, -127, 127);
};

function classify(accel) {
  const acc = accel.acc;
  const x = map(acc.x);
  const y = map(acc.y);
  const z = map(acc.z);

  //console.log(`insert (${x}, ${y}, ${z}) at pos ${pkgCount}`);
  Infxl.insert(pkgCount++, x, y, z);

  if (pkgCount > 19) {
    //console.log("Calculate classification");
    const classification = Infxl.model();
    console.log('Classified as: ', classification);
    pkgCount = 0;
  }
}

Infxl.model();
Puck.accelOn(12.5);
Puck.on('accel', classify);
```

Result:

```
Start classifier
Classified as:  0
Classified as:  2
Classified as:  2
Classified as:  0
Classified as:  1
Classified as:  0
Finish, button pressed
```

## Task 3

Attached HTML / JS files :)

## Task 4

In juypiter notebook.

### a

The average was: 2.143474156465103e-06 Joules

### b

The average was: 6.262293543839088e-05 Joules

## Task 5

Connecting and transmitting uses a lot of energy compared to calculating the result on the puck. Because it is not a very big model (uses signed 8bit int instead of float and millions of params), it can do this very efficiently (with C) on the puck itself. This reduces the overhead of communication and runs efficient C code. Whereas the communication with a webservice must be done by polling the results, then fetching the stuff, and do the calucaltion on the machine. But collection of the data must be done anyway. So, the communication and transmission of the long string (60 numbers plus commas etc.) is definitely less energy efficient than keeping the last result in memory of the puck, discard everything else, and only transmit the result (0, 1, 2) over BLE if necessary.

Optimal solution in this case would include: calculate with a modulated (mod 5 or so) sliding window, only calculate with the model if there is actual movement (not only acceleration). So, just measure and keep the movement in memory (the last 20) and then calculate if the computer wants to know the current state. Otherwise, idle and just report internally.

## Helps used:

https://www.wolframalpha.com/
https://www.espruino.com/Puck.js#power-consumption
https://www.espruino.com/datasheets/LSM6DS3TR-C.pdf
