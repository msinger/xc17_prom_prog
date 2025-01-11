using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;

namespace xc17_prom_prog
{
	class MainClass
	{
		public static int Main(string[] args)
		{
			bool   parseOptions = true;
			bool   testEcho     = false;
			bool   testVoltage  = false;
			bool   detect       = false;
			bool   readReset    = false;
			bool   read         = false;
			bool   blankCheck   = false;
			string port         = null;
			string voltageStr   = null;
			string promType     = null;
			string outFile      = null;
			int    num          = 0;

			for (int i = 0; i < args.Length; i++)
			{
				if (parseOptions && args[i].StartsWith("--"))
				{
					string nextArg  = (i + 1 < args.Length) ? args[i + 1] : null;
					string nextArgL = nextArg != null ? nextArg.ToLower() : null;
					switch (args[i])
					{
						case "--test-echo":    testEcho     = true;                                num++; break;
						case "--test-voltage": testVoltage  = true;  voltageStr   = nextArgL; i++; num++; break;
						case "--prom":                               promType     = nextArgL; i++;        break;
						case "--detect":       detect       = true;                                num++; break;
						case "--read-reset":   readReset    = true;                                num++; break;
						case "--read":         read         = true;  outFile      = nextArg;  i++; num++; break;
						case "--blank":        blankCheck   = true;                                num++; break;
						case "--":             parseOptions = false;                                      break;
						default:
							if (args[i] != "--help")
								Console.Error.WriteLine("Invalid argument: " + args[i]);
							Usage(args[i] == "--help" ? Console.Out : Console.Error);
							return args[i] == "--help" ? 0 : 1;
					}
					continue;
				}

				if (port == null)
				{
					port = args[i];
					continue;
				}

				Console.Error.WriteLine("Too many arguments: " + args[i]);
				return 2;
			}

			if (port == null)
			{
				Usage(Console.Error);
				return 2;
			}

			PromInfo promInfo = new PromInfo();
			if (promType != null)
			{
				if (!proms.TryGetValue(promType, out promInfo))
				{
					Usage(Console.Error);
					return 2;
				}
			}

			if (num != 1)
			{
				Console.Error.WriteLine("Exactly one of these options must be given:");
				Console.Error.WriteLine("  --test-echo");
				Console.Error.WriteLine("  --test-voltage");
				Console.Error.WriteLine("  --detect");
				Console.Error.WriteLine("  --read-reset");
				Console.Error.WriteLine("  --read");
				Console.Error.WriteLine("  --blank");
				return 2;
			}

			if (detect || readReset || read)
			{
				if (promType == null)
				{
					Console.Error.WriteLine("Please specify PROM using --prom option.");
					return 2;
				}
			}

			PromProgrammer prog;
			try
			{
				prog = new PromProgrammer(port, Console.Error);
			}
			catch (IOException)
			{
				Console.Error.WriteLine("Can't open port " + port + ".");
				return 3;
			}

			PromProgrammer.ProgrammerInfo info = prog.QueryInfo();
			Console.Error.WriteLine("HDL ver.: " + info.HdlVersion + "  HW ver.: " + info.HwVersion);
			Console.Error.WriteLine("HW type: " + info.HwType);

			if (promType != null)
				Console.Error.WriteLine("PROM type: " + promInfo.Type);

			if (testEcho)
			{
				prog.TestEcho();
				return 0;
			}

			if (testVoltage)
			{
				int voltageArg;
				switch (voltageStr)
				{
					case "off":            voltageArg =  0; break;
					case "vcc-gnd":        voltageArg =  1; break;
					case "vcc-3v3":        voltageArg =  2; break;
					case "vcc-5v":         voltageArg =  3; break;
					case "vpp-gnd":        voltageArg =  4; break;
					case "vpp-gnd-weak":   voltageArg =  5; break;
					case "vpp-3v3":        voltageArg =  6; break;
					case "vpp-3v7":        voltageArg =  7; break;
					case "vpp-5v":         voltageArg =  8; break;
					case "vpp-5v4":        voltageArg =  9; break;
					case "vpp-12v25":      voltageArg = 10; break;
					case "vpp-12v25-weak": voltageArg = 11; break;
					default: Usage(Console.Error); return 2;
				}
				prog.TestVoltage(voltageArg);
				return 0;
			}

			try
			{
				prog.PowerOff();
				prog.Prom = promInfo;
				prog.VerifyDeviceID();
				bool invReset = prog.IsResetInverted();
				if (invReset)
					Console.Error.WriteLine("Reset inverted bit is programmed (inverted; active low).");
				else
					Console.Error.WriteLine("Reset inverted bit is not programmed (default; active high).");
				if (readReset)
					return invReset ? 0 : 1;
				if (read)
				{
					BinaryWriter w;
					if (outFile == null || outFile == "-")
						w = new BinaryWriter(Console.OpenStandardOutput());
					else
						w = new BinaryWriter(File.Open(outFile, FileMode.Create, FileAccess.Write, FileShare.Read));
					prog.Read(w, invReset);
				}
				if (blankCheck)
				{
					if (prog.IsBlank(invReset))
					{
						Console.Error.WriteLine("Device is blank.");
						return 0;
					}
					else
					{
						Console.Error.WriteLine("Device is not blank.");
						return 1;
					}
				}
			}
			catch (InvalidResponseException e)
			{
				Console.Error.WriteLine(e.Message);
				/* If we ran into an error, try to recover and switch off power to the PROM. */
				try
				{
					prog.ResetPort();
					prog.PowerOff();
				}
				catch (InvalidResponseException) { }
				throw;
			}

			return 0;
		}

		private static void Usage(TextWriter o)
		{
			o.WriteLine("XC17xxx PROM Programmer");
			o.WriteLine("USAGE: xc17_prom_prog [OPTIONS] [--] PORT");
			o.WriteLine("OPTIONS:");
			o.WriteLine("  --help                  Prints this text.");
			o.WriteLine("  --prom PROM             Configures programmer to handle the specific PROM chip.");
			o.WriteLine("  --test-echo             Tests communication and BRAM buffers on programmer.");
			o.WriteLine("  --test-voltage VOLTAGE  Don't use this option when a PROM is inserted! Switch on a single");
			o.WriteLine("                          supply VOLTAGE and switch off all others for testing. VOLTAGE can");
			o.WriteLine("                          be one of vcc-gnd, vcc-3v3, vcc-5v, vpp-gnd, vpp-gnd-weak, vpp-3v3,");
			o.WriteLine("                          vpp-3v7, vpp-5v, vpp-5v4, vpp-12v25, vpp-12v25-weak, off.");
			o.WriteLine("  --detect                Just detect the presence of the PROM chip by verifying device ID.");
			o.WriteLine("  --read-reset            Read reset polarity; returns 0 if inverted (active low), otherwise 1.");
			o.WriteLine("  --read OUTFILE          Read chip contents into OUTFILE.");
			o.WriteLine("  --blank                 Perform blank check; returns 0 if blank, otherwise 1.");
			o.WriteLine("Supported values for PROM:");
			o.Write(" ");
			bool first = true;
			int col = 0;
			foreach (string key in proms.Keys)
			{
				if(!first)
					o.Write(",");
				if (col >= 9)
				{
					o.WriteLine();
					o.Write(" ");
					col = 0;
				}
				o.Write(" ");
				o.Write(key);
				first = false;
				col++;
			}
			o.WriteLine();
		}

		private static Dictionary<string, PromInfo> proms = new Dictionary<string, PromInfo>()
		{
			/*                            PROM type                  ID       5V 5Vprog  64bit  density Nidclk Nrstclk Tpgm Tpgm1 Tprst*/
			["xc1736e"]    = new PromInfo("XC1736E",                0xed,  true,  true, false,   36288,  2056,  2048, 1000,    0, 5000),
			["xc1765e"]    = new PromInfo("XC1765E",                0xfd,  true,  true, false,   65536,  2056,  2048, 1000,    0, 5000),
			["xc1765x"]    = new PromInfo("XC1765X aka XC1765EL",   0xfc, false,  true, false,   65536,  2056,  2048, 1000,    0, 5000),
			["xc1765el"]   = new PromInfo("XC1765X aka XC1765EL",   0xfc, false,  true, false,   65536,  2056,  2048, 1000,    0, 5000),
			["xc17128e"]   = new PromInfo("XC17128E",               0x8d,  true,  true,  true,  131072,  4600,  4104, 1000,    0, 5000),
			["xc17128x"]   = new PromInfo("XC17128X aka XC17128EL", 0x8c, false,  true,  true,  131072,  4600,  4104, 1000,    0, 5000),
			["xc17128el"]  = new PromInfo("XC17128X aka XC17128EL", 0x8c, false,  true,  true,  131072,  4600,  4104, 1000,    0, 5000),
			["xc17256e"]   = new PromInfo("XC17256E",               0xad,  true,  true,  true,  262144,  4600,  4104, 1000,    0, 5000),
			["xc17256x"]   = new PromInfo("XC17256X aka XC17256EL", 0xac, false,  true,  true,  262144,  4600,  4104, 1000,    0, 5000),
			["xc17256el"]  = new PromInfo("XC17256X aka XC17256EL", 0xac, false,  true,  true,  262144,  4600,  4104, 1000,    0, 5000),
			["xc17s05"]    = new PromInfo("XC17S05",                0xf8,  true,  true, false,   65536,  2056,  2048,  102,  502, 5000),
			["xc17s05xl"]  = new PromInfo("XC17S05XL",              0x87, false,  true,  true,  131072,  4600,  4104,  102,  502, 5000),
			["xc17s10"]    = new PromInfo("XC17S10",                0x88,  true,  true,  true,  131072,  4600,  4104,  102,  502, 5000),
			["xc17s10xl"]  = new PromInfo("XC17S10XL",              0x89, false,  true,  true,  131072,  4600,  4104,  102,  502, 5000),
			["xc17s20"]    = new PromInfo("XC17S20",                0xa8,  true,  true,  true,  262144,  4600,  4104,  102,  502, 5000),
			["xc17s20xl"]  = new PromInfo("XC17S20XL",              0xa9, false,  true,  true,  262144,  4600,  4104,  102,  502, 5000),
			["xc17s30"]    = new PromInfo("XC17S30",                0xa6,  true,  true,  true,  262144,  4600,  4104,  102,  502, 5000),
			["xc17s30xl"]  = new PromInfo("XC17S30XL",              0xa7, false,  true,  true,  262144,  4600,  4104,  102,  502, 5000),
			["xc17s40"]    = new PromInfo("XC17S40",                0x98,  true,  true,  true,  524288, 19791, 16384,  102,  502, 5000),
			["xc17s40xl"]  = new PromInfo("XC17S40XL",              0x99, false,  true,  true,  524288, 19791, 16384,  102,  502, 5000),
			["xc17s50xl"]  = new PromInfo("XC17S50XL",              0xd6, false,  true,  true, 1048576, 19791, 16384,  102,  502, 5000),
			["xc17s100xl"] = new PromInfo("XC17S100XL",             0xd7, false,  true,  true, 1048576, 19791, 16384,  102,  502, 5000),
			["xc17s150xl"] = new PromInfo("XC17S150XL",             0xd9, false,  true,  true, 1048576, 19791, 16384,  102,  502, 5000),
			["xc17512l"]   = new PromInfo("XC17512L",               0x9b, false,  true,  true,  524288, 19791, 16384,  102,  502, 5000),
			["xc1701"]     = new PromInfo("XC1701",                 0xda,  true,  true,  true, 1048576, 19791, 16384,  102,  502, 5000),
			["xc1701l"]    = new PromInfo("XC1701L",                0xdb, false,  true,  true, 1048576, 19791, 16384,  102,  502, 5000),
			["xc1702l"]    = new PromInfo("XC1702L",                0x3b, false, false,  true, 2097152, 65632, 65536,  102,  200,  400),
			["xc1704l"]    = new PromInfo("XC1704L",                0xbb, false, false,  true, 4194304, 65632, 65536,  102,  200,  400)
		};
	}
}
