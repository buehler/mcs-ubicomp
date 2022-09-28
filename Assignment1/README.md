# Energy-conscious Deployment of ML Model

## Overview

In this exercise, you will try out two strategies for using a pre-trained Artificial Neural Network which can recognize three specific gestures sensed by an accelerometer. The model can detect up-down motion, sideways motion, and other (called random).

We are using the Puck.js device which can be programmed using the Espruino framework.


## Variant-1: Model runs on Puck.js

The original model was developed in C, using an [infXL toolchain](https://cloud.infxl.com/). 
Embedding it as inline C code within JS would have significant performance issues due to its high memory requirements.
The pre-trained model has been kept in C and integrated the infXL model in a modified Espruino software library, found [here](https://github.com/Interactions-HSG/UbiComp-Espruino).
You will need to use the wrapper functions to pass the accelerometer data to it.

### Compiling custom Espruino firmware 

1. Clone the repositoy a Linux/Mac machine. 

The model is found in the `\lib\misc\infxl.*` files. 
The the JS wrappers are also there. 

2. To test on a native system:

`make clean ; make`
Once the build completes, try running the espruino locally:
`./espruino`
-> then at the >> prompt type `Infxl.model()` this should return 1

3. Now, you need to build the firmware for the NRF52 chip which is on the Puck. For this:

`make clean ; DFU_UPDATE_BUILD=1 BOARD=PUCKJS RELEASE=1 make`

This should create a Zip file.

4. Install the NRF Connect app on your smart phone. Copy the zip file to your phone.

5. Put Puck.js into DFU mode: Disconnect power (pull out the battery), then while keeping button pressed, insert the battery. Release button as soon as the led turns blue. It is now in DFU mode.

6. Start the NRF Connect app and scan. You will see "Puck DFU" listed. Connect, go to DFU tab, and select the zip file to upload. 

It takes few minutes to upload. Don't fiddle with anything - if the firmware update is interupted the device will get bricked.

7. After the upload is done, power off the puck and power on again. This returns the devices to normal BLE mode.

8. To test that the model works, go to the Espruino Web IDE, connect to the Puck.js device and call `Infxl.model()` - this processes a dummy data and returns 1.

9. The only other interface the model has is `Infxl.insert(i, x, y, z)` where `i` is the sample index (0..19), and `x,y,z` are acceleration values which are scaled to -127..127 corresponding to -2g..+2g acceleration.

10. To use the model, insert 20 `x,y,z` samples (one triplet at a time) and then call `Infxl.model()` to return the classification result.


## Variant-2: Model runs on a browser application

The main code that runs on your WebBrowser is visible in the [code](js_model/). 
This code has a function getData, which scrapes the data from the WebBLE UART service. 
The accelerometer data is then passed on to the classification model, and optionally to a graphical plot.

```
  function getData() {
    UART.eval('dumpData()', function(response) {
		document.getElementById("sensordata").value = response;
		UART.close();
		var data = extractDataFromResponse(response);
		makeChart(data);
		doClassification(data);
    });
  }
```

To summarize:

0. When `UART.eval` is called the WebBluetooth javascript asks you to connect to a BLE device which is advertising UART service (which, Puck is doing)
1. The javascript function uses the WebBluetooth UART connection to the Puck to invoke a method called `dumpData` (see [code](puck_js/)).
2. The software on the Puck.js should send the measurements (20 samples of 3 values - i.e. 60 elements) in a comma separated string via the JS console.
3. The raw data is passed to the javascript version of the NN model and the result is obtained.
4. (Optional) The raw data is displayed in a chart in the html. Compare the execution time of the JS model to the C model.
