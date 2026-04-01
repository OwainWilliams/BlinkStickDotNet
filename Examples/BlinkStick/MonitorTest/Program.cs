using System;
using BlinkStickDotNet;
using System.Collections.Generic;
using System.Threading;

namespace MonitorTest
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Console.WriteLine ("Monitor BlinkSticks inserted and removed");

			UsbMonitor monitor = new UsbMonitor();

			//Attach to connected event
			monitor.BlinkStickConnected += (object sender, DeviceModifiedArgs e) => {
				Console.WriteLine("BlinkStick " + e.Device.Serial + " connected!");
			};

			//Attach to disconnected event
			monitor.BlinkStickDisconnected += (object sender, DeviceModifiedArgs e) => {
				Console.WriteLine("BlinkStick " + e.Device.Serial + " disconnected...");
			};

			List<BlinkStick> devices = new List<BlinkStick> (BlinkStick.FindAll());

			//List BlinkSticks already connected
			foreach (BlinkStick device in devices)
			{
				Console.WriteLine("BlinkStick " + device.Serial + " already connected");
			}

			//Start monitoring
			monitor.Start ();

			Console.WriteLine ("Monitoring for BlinkStick devices... Press any key to exit.");

			while (true) {
				Thread.Sleep(50);

				//Exit if key is pressed
				if (Console.KeyAvailable)
					break;
			}

			//Stop monitoring
			monitor.Stop ();
		}
	}
}
