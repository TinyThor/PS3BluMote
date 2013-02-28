/*
Copyright (c) 2011 Ben Barron
Hibernation Code by miljbee

Permission is hereby granted, free of charge, to any person obtaining a copy 
of this software and associated documentation files (the "Software"), to deal 
in the Software without restriction, including without limitation the rights 
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
copies of the Software, and to permit persons to whom the Software is furnished 
to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all 
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A 
PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT 
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE 
SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Timers;
//using System.Diagnostics;

using HidLibrary;
using WindowsAPI;

using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;

namespace PS3BluMote
{
    class PS3Remote
    {
        public event EventHandler<EventArgs> BatteryLifeChanged;
        public event EventHandler<ButtonData> ButtonDown;
        public event EventHandler<ButtonData> ButtonReleased;
        public event EventHandler<EventArgs> Connected;
        public event EventHandler<EventArgs> Disconnected;
        public event EventHandler<EventArgs> Hibernated;
        public event EventHandler<EventArgs> Awake;

        private HidDevice hidRemote = null;
        private Timer timerFindRemote = null;
        private Timer timerHibernation = null;
        private int vendorId = 0x054c;
        private int productId = 0x0306;
        private Button lastButton = Button.Angle;
        private bool isButtonDown = false;
        private bool _hibernationEnabled;
        private int _hibernationInterval=180000;
        private string _btAddress;
        private bool _hibernated;

        private string insertSound;
        private string removeSound;

        private byte _batteryLife = 100;

        #region "Remote button codes"
        static byte[][] buttonCodes = 
        {
	        new byte[] { 0, 0, 0, 22 },     //Eject
	        new byte[] { 0, 0, 0, 100 },    //Audio
	        new byte[] { 0, 0, 0, 101 },    //Angle
	        new byte[] { 0, 0, 0, 99 },     //Subtitle
	        new byte[] { 0, 0, 0, 15 },     //Clear
	        new byte[] { 0, 0, 0, 40 },     //Time
	        new byte[] { 0, 0, 0, 0 },      //NUM_1
	        new byte[] { 0, 0, 0, 1 },      //NUM_2
	        new byte[] { 0, 0, 0, 2 },      //NUM_3
	        new byte[] { 0, 0, 0, 3 },      //NUM_4
	        new byte[] { 0, 0, 0, 4 },      //NUM_5
	        new byte[] { 0, 0, 0, 5 },      //NUM_6
	        new byte[] { 0, 0, 0, 6 },      //NUM_7
	        new byte[] { 0, 0, 0, 7 },      //NUM_8
	        new byte[] { 0, 0, 0, 8 },      //NUM_9
	        new byte[] { 0, 0, 0, 9 },      //NUM_0
	        new byte[] { 0, 0, 0, 128 },    //Blue
	        new byte[] { 0, 0, 0, 129 },    //Red
	        new byte[] { 0, 0, 0, 130 },    //Green
	        new byte[] { 0, 0, 0, 131 },    //Yellow
	        new byte[] { 0, 0, 0, 112 },    //Display
	        new byte[] { 0, 0, 0, 26 },     //Top_Menu
	        new byte[] { 0, 0, 0, 64 },     //PopUp_Menu
	        new byte[] { 0, 0, 0, 14 },     //Return
	        new byte[] { 0, 16, 0, 92 },    //Triangle
	        new byte[] { 0, 32, 0, 93 },    //Circle
	        new byte[] { 0, 128, 0, 95 },   //Square
	        new byte[] { 0, 64, 0, 94 },    //Cross
	        new byte[] { 16, 0, 0, 84 },    //Arrow_Up
	        new byte[] { 64, 0, 0, 86 },    //Arrow_Down
	        new byte[] { 128, 0, 0, 87 },   //Arrow_Left
	        new byte[] { 32, 0, 0, 85 },    //Arrow_Right
	        new byte[] { 0, 0, 8, 11 },     //Enter
	        new byte[] { 0, 4, 0, 90 },     //L1
	        new byte[] { 0, 1, 0, 88 },     //L2
	        new byte[] { 2, 0, 0, 81 },     //L3
	        new byte[] { 0, 8, 0, 91 },     //R1
	        new byte[] { 0, 2, 0, 89 },     //R2
	        new byte[] { 4, 0, 0, 82 },     //R3
	        new byte[] { 0, 0, 1, 67 },     //Playstation
	        new byte[] { 1, 0, 0, 80 },     //Select
	        new byte[] { 8, 0, 0, 83 },     //Start
	        new byte[] { 0, 0, 0, 50 },     //Play
	        new byte[] { 0, 0, 0, 56 },     //Stop
	        new byte[] { 0, 0, 0, 57 },     //Pause
	        new byte[] { 0, 0, 0, 51 },     //Scan_Back
	        new byte[] { 0, 0, 0, 52 },     //Scan_Forward
	        new byte[] { 0, 0, 0, 48 },     //Prev
	        new byte[] { 0, 0, 0, 49 },     //Next
	        new byte[] { 0, 0, 0, 96 },     //Step_Back
	        new byte[] { 0, 0, 0, 97 },     //Step_Forward
            new byte[] { 0, 0, 0, 118 },    //instant back
            new byte[] { 0, 0, 0, 117 },    //instant fwd
            new byte[] { 0, 0, 0, 16 },     //channel up
            new byte[] { 0, 0, 0, 17 },     //channel down
            new byte[] { 0, 0, 0, 12 }      // "-/--" dash_slash_dash_dash
        };
        #endregion

        public PS3Remote(int vendor, int product)
        {
            vendorId = vendor;
            productId = product;
            _hibernationEnabled = false;
            _btAddress = "";
            _hibernated = false;
            

            timerHibernation = new Timer();
            timerHibernation.Interval = _hibernationInterval;
            timerHibernation.Elapsed += new ElapsedEventHandler(timerHibernation_Elapsed);

            timerFindRemote = new Timer();
            timerFindRemote.Interval = 500;
            timerFindRemote.Elapsed += new ElapsedEventHandler(timerFindRemote_Elapsed);

            insertSound = "";
            removeSound = "";
        }

        public void connect()
        {
            timerFindRemote.Enabled = true;
        }

        public byte getBatteryLife
        {
            get { return _batteryLife; }
        }

        public bool isConnected
        {
            get { return timerFindRemote.Enabled; }
        }

        public bool hibernationEnabled
        {
            get { return _hibernationEnabled; }
            set
            {
                _hibernationEnabled = value;
                timerHibernation.Enabled = _hibernationEnabled && !isHibernated();
            }
        }
        public int hibernationInterval
        {
            get { return _hibernationInterval; }
            set { 
                _hibernationInterval = value;
                timerHibernation.Interval = _hibernationInterval;
                if (timerHibernation.Enabled)
                {
                    timerHibernation.Stop();
                    timerHibernation.Start();
                }
            }
        }

        public string btAddress
        {
            get { return _btAddress; }
            set { 
                _btAddress = value;
                _hibernated = isHibernated();
            }
        }

        private void readButtonData(HidDeviceData InData)
        {
            timerHibernation.Enabled = false;

            if ((InData.Status == HidDeviceData.ReadStatus.Success) && (InData.Data[0] == 1))
            {
                if (_hibernated && Awake != null)
                {
                    Awake(this, new EventArgs());
                    _hibernated = false;
                }
                timerFindRemote.Interval = 1500;
                
                if (DebugLog.isLogging) DebugLog.write("Read button data: " + String.Join(",", InData.Data));
                //Debug.Print("Read button data: " + String.Join(",", InData.Data));

                if ((InData.Data[10] == 0) || (InData.Data[4] == 255)) // button released
                {
                    if (ButtonReleased != null && isButtonDown) ButtonReleased(this, new ButtonData(lastButton));
                }
                else // button pressed
                {
                    byte[] bCode = { InData.Data[1], InData.Data[2], InData.Data[3], InData.Data[4] };

                    int i, j;

                    for (j = 0; j < 56; j++)
                    {
                        for (i = 0; i < 4; i++)
                        {
                            if (bCode[i] != buttonCodes[j][i]) break;
                        }

                        if (i == 4) break;
                    }

                    if (j != 56)
                    {
                        lastButton = (Button)j;
                        isButtonDown = true;

                        if (ButtonDown != null) ButtonDown(this, new ButtonData(lastButton));
                    }                   
                }

                byte batteryReading = (byte)(InData.Data[11] * 20);

                if (batteryReading != _batteryLife) //Check battery life reading.
                {
                    _batteryLife = batteryReading;

                    if (BatteryLifeChanged != null) BatteryLifeChanged(this, new EventArgs());
                }
                
                if (_hibernationEnabled) timerHibernation.Enabled = true;

                hidRemote.Read(readButtonData); //Read next button pressed.

                return;
            }

            if (DebugLog.isLogging) DebugLog.write("Read remote data: " + String.Join(",", InData.Data));

            if (Disconnected != null) Disconnected(this, new EventArgs());

            hidRemote.Dispose(); //Dispose of current remote.

            hidRemote = null;

            timerFindRemote.Enabled = true; //Try to reconnect.
            if (_hibernationEnabled) timerHibernation.Enabled = false;
        }

        private void timerFindRemote_Elapsed(object sender, ElapsedEventArgs e)
        {
            timerFindRemote.Stop();
            if (hidRemote == null)
            {
                if (DebugLog.isLogging) DebugLog.write("Searching for remote");

                IEnumerator<HidDevice> devices = HidDevices.Enumerate(vendorId, productId).GetEnumerator();
                
                if (devices.MoveNext()) hidRemote = devices.Current;

                if (hidRemote != null)
                {
                    if (DebugLog.isLogging) DebugLog.write("Remote found");

                    hidRemote.OpenDevice();

                    if (Connected != null) Connected(this, new EventArgs());
                    if (_hibernated && Hibernated != null) Hibernated(this, new EventArgs());

                    if (!_hibernated && hibernationEnabled) timerHibernation.Enabled = true;
                    if (_hibernated && hibernationEnabled) RestoreDevicesInsertionSounds();

                    hidRemote.Read(readButtonData);
                }
            }

            if (hidRemote == null) timerFindRemote.Start();
        }

        private void timerHibernation_Elapsed(object sender, ElapsedEventArgs e)
        {
            timerHibernation.Stop();
            if (DebugLog.isLogging) DebugLog.write("Attempting to hibernate remote");

            if (_btAddress.Length == 12)
            {
                try
                {
                    if (SaveDevicesInsertionSounds())
                    {
                        if (DebugLog.isLogging) DebugLog.write("Disabling device connect/disconnect sounds:");
                        Microsoft.Win32.Registry.SetValue(@"HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceConnect\.Current", "", "");
                        Microsoft.Win32.Registry.SetValue(@"HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceDisconnect\.Current", "", "");
                        // Values are restored in ReadButton Data, because, it seems that this method might end before the device is actually Reconnected
                    }

                    if (DebugLog.isLogging) DebugLog.write("Hibernating the remote with address " + _btAddress + ":");
                    BluetoothDeviceInfo device = new BluetoothDeviceInfo(BluetoothAddress.Parse(_btAddress));

                    if (DebugLog.isLogging) DebugLog.write("Disconnecting HID Service from the Remote...");
                    device.SetServiceState(BluetoothService.HumanInterfaceDevice, false);

                    if (DebugLog.isLogging) DebugLog.write("ReConnecting HID Service to the Remote...");
                    device.SetServiceState(BluetoothService.HumanInterfaceDevice, true);

                    _hibernated = true;
                    timerFindRemote.Interval = 200;

                    if (DebugLog.isLogging) DebugLog.write("Hibernating Done.");

                }
                catch (Exception ex)
                {
                    if (DebugLog.isLogging) DebugLog.write("Unable to hibernate remote:" + ex.Message);
                }

            }
            else
            {
                if (DebugLog.isLogging) DebugLog.write("Wrong format for BT Address");
            }
        }

        public bool isHibernated()
        {
            if (_btAddress.Length == 12 || _btAddress.Length == 17)
            {
                BluetoothDeviceInfo device;
                try { device = new BluetoothDeviceInfo(BluetoothAddress.Parse(_btAddress)); }
                catch { return false; } // The remote is considered awake

                try { ServiceRecord[] services = device.GetServiceRecords(InTheHand.Net.Bluetooth.BluetoothService.HumanInterfaceDevice); }
                catch {
                    if (!_hibernated && Hibernated != null) Hibernated(this, new EventArgs());
                    return true; 
                }
                return false;
            }
            else return false; // The remote is considered awake
        }

        private bool SaveDevicesInsertionSounds()
        {
            try
            {
                if (DebugLog.isLogging) DebugLog.write("Saving device connect/disconnect sounds:");
                insertSound = (string)Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceConnect\.Current", "", "");
                removeSound = (string)Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceDisconnect\.Current", "", "");
                return true;
            }
            catch
            {
                if (DebugLog.isLogging) DebugLog.write("Unable to save device connect/disconnect sounds.");
                return false;
            }
        }
        private void RestoreDevicesInsertionSounds()
        {
            try
            {
                if (DebugLog.isLogging) DebugLog.write("Restoring device connect/disconnect sounds:");
                if (insertSound.Length > 0) Microsoft.Win32.Registry.SetValue(@"HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceConnect\.Current", "", insertSound);
                if (removeSound.Length > 0) Microsoft.Win32.Registry.SetValue(@"HKEY_CURRENT_USER\AppEvents\Schemes\Apps\.Default\DeviceDisconnect\.Current", "", removeSound);
            }
            catch
            {
                if (DebugLog.isLogging) DebugLog.write("Unable to restore device connect/disconnect sounds.");
            }
        }

        public class ButtonData : EventArgs
        {
            public Button button;

            public ButtonData(Button btn)
            {
                button = btn;
            }
        }

        public enum Button
        {
            Eject,
            Audio,
            Angle,
            Subtitle,
            Clear,
            Time,
            NUM_1,
            NUM_2,
            NUM_3,
            NUM_4,
            NUM_5,
            NUM_6,
            NUM_7,
            NUM_8,
            NUM_9,
            NUM_0,
            Blue,
            Red,
            Green,
            Yellow,
            Display,
            Top_Menu,
            PopUp_Menu,
            Return,
            Triangle,
            Circle,
            Square,
            Cross,
            Arrow_Up,
            Arrow_Down,
            Arrow_Left,
            Arrow_Right,
            Enter,
            L1,
            L2,
            L3,
            R1,
            R2,
            R3,
            Playstation,
            Select,
            Start,
            Play,
            Stop,
            Pause,
            Scan_Back,
            Scan_Forward,
            Prev,
            Next,
            Step_Back,
            Step_Forward,
            Instant_Back,
            Instant_Forward,
            Channel_Up,
            Channel_Down,
            dash_slash_dash_dash
        }
    }
}
