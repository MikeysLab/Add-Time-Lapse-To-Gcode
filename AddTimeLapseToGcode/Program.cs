using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace AddTimeLapseToGcode
{
	class Program
	{
		static void PrintHelp()
		{
			Console.WriteLine("Add time lapse to gcode V0.1, Mikey's Lab.");
			Console.WriteLine("Usage: AddTimeLapseToGcode.exe <inputfile> <outputfile> [-W] [-V] [-B <value>] [-R <value>] [-P <value>] [-S <value>] [-X <value>] [-Y <value>]");
			Console.WriteLine("Required:");
			Console.WriteLine("\t <input File>: File name of gcode provided by the slicer.");
			Console.WriteLine("\t <outputfile>: File name to save the new gcode with time lapse modifcations.");
			Console.WriteLine("Optional:");
			Console.WriteLine("\t -W\tWait for camera focus and setup after first layer.");
			Console.WriteLine("\t -B <v>\tWait every v layers for a camera battery swap.");
			Console.WriteLine("\t -R <v>\tmm to retract before park on layer change.");
			Console.WriteLine("\t -P <v>\tmm to purge after before resuming the print.");
			Console.WriteLine("\t -S <v>\tMilliseconds to wait for camera shutter.");
			Console.WriteLine("\t -X <v>\tX axis location for print head park.");
			Console.WriteLine("\t -Y <v>\tY axis location for print head park.");
			Console.WriteLine("\t -V\tVerbose logging to console.");
			return;
		}

		static void Main(string[] args)
		{
			bool WaitForFocus = false;
			bool Verbose = false;

			int Retraction = 10;
			int Prime = 10;

			double E = 0;
			double Eoffset = 0;

			double F = 0;
			double X = 0;
			double Y = 0;
			double Z = 0;

			int ParkX = 210;
			int ParkY = 235;

			int BatterySwap = 0;
			int ShutterWait = 750;

			if (Verbose) Console.WriteLine("Check command line and verify input file exists");
			try
			{
				if (!File.Exists(args[0]))
				{
					Console.WriteLine("File not found");
					PrintHelp();
					return;
				}
			}
			catch(Exception Ex)
			{
				PrintHelp();
				return;
			}

			if(args.Length > 2)
			{
				for (int i=2; i<args.Length;i++)
				{
					switch(args[i])
					{
						case "-W":
						case "--wait":
							WaitForFocus = true;
							break;

						case "-V":
							Verbose = true;
							break;

						case "-R":
						case "-retraction":
							int.TryParse(args[i + 1], out Retraction);
							i++;
							break;

						case "-P":
						case "--prime":
							int.TryParse(args[i + 1], out Prime);
							i++;
							break;

						case "-B":
						case "--batterySwap":
							int.TryParse(args[i + 1], out BatterySwap);
							i++;
							break;

						case "-S":
						case "--shutterWait":
							int.TryParse(args[i + 1], out ShutterWait);
							i++;
							break;

						case "-X":
						case "--parkX":
							int.TryParse(args[i + 1], out ParkX);
							i++;
							break;

						case "-Y":
						case "--parkY":
							int.TryParse(args[i + 1], out ParkY);
							i++;
							break;

						default:
							PrintHelp();
							break;
					}

				}
			}
			else
			{
				PrintHelp();
				return;
			}

			using (StreamReader sr = new StreamReader(args[0]))
			{
				if (Verbose) Console.WriteLine("Opening input file: " + args[0] + " for reading.");
				using (StreamWriter sw = new StreamWriter(args[1]))
				{
					if (Verbose) Console.WriteLine("Opening output file: " + args[1] + " for writing.");
					int layerCount = 0;
					while(!sr.EndOfStream)
					{
						string line = sr.ReadLine();
						if (Verbose) Console.WriteLine("Processing line: " + line);

						if (line.StartsWith("G"))
						{
							if (Verbose) Console.WriteLine("Found movement command, processing.");

							if (line.Contains(";")) line = line.Substring(0, line.IndexOf(';'));

							if (Verbose) Console.WriteLine("Removed comments from line: " + line);

							string[] values = line.Split(' ');

							if (Verbose) Console.WriteLine("Line has " + values.Length.ToString() + " values.");
							
							foreach (string val in values)
							{
								if (Verbose) Console.WriteLine("Processing value: " + val);

								if (val.StartsWith("F"))
								{
									F = Double.Parse(val.Substring(1));
								}
								if (val.StartsWith("X"))
								{
									X = Double.Parse(val.Substring(1));
									if (X > ParkX)
									{
										if (Verbose) Console.WriteLine("X movement command is out of bounds, limit to " + ParkX);
										X = ParkX;
									}
								}
								if (val.StartsWith("Y"))
								{
									Y = Double.Parse(val.Substring(1));
									if (Y > ParkY)
									{
										if (Verbose) Console.WriteLine("Y movement command is out of bounds, limit to " + ParkY);
										Y = ParkY;
									}
								}
								if (val.StartsWith("Z"))
								{
									Z = Double.Parse(val.Substring(1));
								}
								if (val.StartsWith("E"))
								{
									E = Double.Parse(val.Substring(1)) + Eoffset;
									if (val == "E0")
									{
										if (Verbose) Console.WriteLine("Extruder position reset to 0");
										E = 0;
										Eoffset = 0;
									}
								}
							}

							string newCmd = values[0] + " ";
							if (line.Contains("F")) newCmd = newCmd + "F" + F + " ";
							if (line.Contains("X")) newCmd = newCmd + "X" + X + " ";
							if (line.Contains("Y")) newCmd = newCmd + "Y" + Y + " ";
							if (line.Contains("Z")) newCmd = newCmd + "Z" + Z + " ";
							if (line.Contains("E")) newCmd = newCmd + "E" + E + " ";

							sw.WriteLine(newCmd);
							Console.WriteLine(newCmd);
						}
						else if (line.StartsWith(";LAYER:"))
						{
							layerCount++;
							if (Verbose) Console.WriteLine("Layer: " + line.Substring(7) + " RETRACT == E: " + E.ToString());
							Eoffset += (Retraction * -1) + Prime;

							if (Verbose) Console.WriteLine("Retract filament");
							sw.WriteLine("G1 E" + (E - Retraction).ToString() + " F1800"); //reract 5mm

							if (Verbose) Console.WriteLine("Pre-park print head");
							sw.WriteLine("G1 F9000 X" + (ParkX-10) + " Y" + ParkY + "; prePark print head");
							sw.WriteLine("G4 P250; Pause for motion stop");

							if (Verbose) Console.WriteLine("Park printhead and trigger shutter");
							sw.WriteLine("G1 F9000 X" + ParkX + " Y" + ParkY +"; Park print head");
							sw.WriteLine("M400; Wait for moves to finish");

							if (int.Parse(line.Substring(7)) == 1 && WaitForFocus)
							{
								if (Verbose) Console.WriteLine("First layer done, camera wait selected, inserting Gcode.");
								sw.WriteLine("M0 Focus camera and click to continue");
								sw.WriteLine("G1 F9000 X" + (ParkX - 10) + " Y" + ParkY + "; PrePark print head");
								sw.WriteLine("G1 F9000 X" + ParkX + " Y" + ParkY + "; Park print head");
							}

							if (BatterySwap != 0 && (layerCount % BatterySwap) == 0)
							{
								if (Verbose) Console.WriteLine("Battery swap selected, at start of layer for swap, inserting GCode.");
								sw.WriteLine("G1 F9000 X" + (ParkX - 10) + " Y" + ParkY + "; Park print head");
								sw.WriteLine("M0 Swap Camera Battery");
							}

							if (Verbose) Console.WriteLine("Inserting GCode for shutter wait");
							sw.WriteLine("G4 P" + ShutterWait +" ; Wait for camera");
							sw.WriteLine("G1 F9000 X" + X.ToString() + " Y" + Y.ToString());
							sw.WriteLine("G1 E" + (E - Retraction + Prime).ToString() + " F1800"); //prime extruder
						}
						else
						{
							sw.WriteLine(line);
						}
					}
				}
			}
			Console.WriteLine("Done. " + args[1] + " is ready for the printer.");
		}
	}
}
