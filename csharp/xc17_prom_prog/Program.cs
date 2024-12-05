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
			bool   parse_opts   = true;
			bool   test_echo    = false;
			bool   detect       = false;
			string port         = null;
			string test_voltage = null;
			string prom_type    = null;
			int    num_instr    = 0;

			for (int i = 0; i < args.Length; i++)
			{
				if (args[i][0] == '-' && parse_opts)
				{
					switch (args[i])
					{
					case "--":
						parse_opts = false;
						break;
					case "--help":
						Usage(Console.Out);
						return 0;
					case "--test-echo":
						test_echo = true;
						num_instr++;
						break;
					case "--test-voltage":
						if (i + 1 >= args.Length)
						{
							Usage(Console.Error);
							return 2;
						}
						test_voltage = args[++i].ToLower();
						num_instr++;
						break;
					case "--prom":
						if (i + 1 >= args.Length)
						{
							Usage(Console.Error);
							return 2;
						}
						prom_type = args[++i].ToLower();
						break;
					case "--detect":
						detect = true;
						num_instr++;
						break;
					default:
						Console.Error.WriteLine("Invalid option: " + args[i]);
						return 2;
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

			PromInfo prom_info = new PromInfo();
			if (prom_type != null)
			{
				if (!proms.TryGetValue(prom_type, out prom_info))
				{
					Usage(Console.Error);
					return 2;
				}
			}

			if (num_instr != 1)
			{
				Console.Error.WriteLine("Exactly one of these options must be given:");
				Console.Error.WriteLine("  --test-echo");
				Console.Error.WriteLine("  --test-voltage");
				Console.Error.WriteLine("  --detect");
				return 2;
			}

			if (detect)
			{
				if (prom_type == null)
				{
					Console.Error.WriteLine("Please specify PROM using --prom option.");
					return 2;
				}
			}

			PromProgrammer prog;
			try
			{
				prog = new PromProgrammer(port, Console.Out, Console.Error);
			}
			catch (IOException)
			{
				Console.Error.WriteLine("Can't open port " + port + ".");
				return 3;
			}

			PromProgrammer.ProgrammerInfo info = prog.QueryInfo();
			Console.Out.WriteLine("HDL ver.: " + info.HdlVersion + "  HW ver.: " + info.HwVersion);
			Console.Out.WriteLine("HW type: " + info.HwType);

			if (prom_type != null)
				Console.Out.WriteLine("PROM type: " + prom_info.Type);

			if (test_echo)
			{
				prog.TestEcho();
				return 0;
			}

			if (test_voltage != null)
			{
				int voltage_arg;
				switch (test_voltage)
				{
					case "off":       voltage_arg = 0; break;
					case "vcc-3v3":   voltage_arg = 1; break;
					case "vcc-5v":    voltage_arg = 2; break;
					case "vpp-3v3":   voltage_arg = 3; break;
					case "vpp-3v7":   voltage_arg = 4; break;
					case "vpp-5v":    voltage_arg = 5; break;
					case "vpp-5v4":   voltage_arg = 6; break;
					case "vpp-12v25": voltage_arg = 7; break;
					case "gnd":       voltage_arg = 8; break;
					default: Usage(Console.Error);     return 2;
				}
				prog.TestVoltage(voltage_arg);
				return 0;
			}

			try
			{
				prog.PowerOff();
				prog.Prom = prom_info;
				prog.VerifyDeviceID();
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
			o.WriteLine("                          be one of vcc-3v3, vcc-5v, vpp-3v3, vpp-3v7, vpp-5v, vpp-5v4,");
			o.WriteLine("                          vpp-12v25, gnd, off.");
			o.WriteLine("  --detect                Just detect the presence of the PROM chip.");
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
			["xc1736e"]    = new PromInfo("XC1736E",                 0xed,  true,  true, false,   36288,  2056,  2048, 1000,    0, 5000),
			["xc1765e"]    = new PromInfo("XC1765E",                 0xfd,  true,  true, false,   65536,  2056,  2048, 1000,    0, 5000),
			["xc1765x"]    = new PromInfo("XC1765X aka. XC1765EL",   0xfc, false,  true, false,   65536,  2056,  2048, 1000,    0, 5000),
			["xc1765el"]   = new PromInfo("XC1765X aka. XC1765EL",   0xfc, false,  true, false,   65536,  2056,  2048, 1000,    0, 5000),
			["xc17128e"]   = new PromInfo("XC17128E",                0x8d,  true,  true,  true,  131072,  4600,  4104, 1000,    0, 5000),
			["xc17128x"]   = new PromInfo("XC17128X aka. XC17128EL", 0x8c, false,  true,  true,  131072,  4600,  4104, 1000,    0, 5000),
			["xc17128el"]  = new PromInfo("XC17128X aka. XC17128EL", 0x8c, false,  true,  true,  131072,  4600,  4104, 1000,    0, 5000),
			["xc17256e"]   = new PromInfo("XC17256E",                0xad,  true,  true,  true,  262144,  4600,  4104, 1000,    0, 5000),
			["xc17256x"]   = new PromInfo("XC17256X aka. XC17256EL", 0xac, false,  true,  true,  262144,  4600,  4104, 1000,    0, 5000),
			["xc17256el"]  = new PromInfo("XC17256X aka. XC17256EL", 0xac, false,  true,  true,  262144,  4600,  4104, 1000,    0, 5000),
			["xc17s05"]    = new PromInfo("XC17S05",                 0xf8,  true,  true, false,   65536,  2056,  2048,  102,  502, 5000),
			["xc17s05xl"]  = new PromInfo("XC17S05XL",               0x87, false,  true,  true,  131072,  4600,  4104,  102,  502, 5000),
			["xc17s10"]    = new PromInfo("XC17S10",                 0x88,  true,  true,  true,  131072,  4600,  4104,  102,  502, 5000),
			["xc17s10xl"]  = new PromInfo("XC17S10XL",               0x89, false,  true,  true,  131072,  4600,  4104,  102,  502, 5000),
			["xc17s20"]    = new PromInfo("XC17S20",                 0xa8,  true,  true,  true,  262144,  4600,  4104,  102,  502, 5000),
			["xc17s20xl"]  = new PromInfo("XC17S20XL",               0xa9, false,  true,  true,  262144,  4600,  4104,  102,  502, 5000),
			["xc17s30"]    = new PromInfo("XC17S30",                 0xa6,  true,  true,  true,  262144,  4600,  4104,  102,  502, 5000),
			["xc17s30xl"]  = new PromInfo("XC17S30XL",               0xa7, false,  true,  true,  262144,  4600,  4104,  102,  502, 5000),
			["xc17s40"]    = new PromInfo("XC17S40",                 0x98,  true,  true,  true,  524288, 19791, 16384,  102,  502, 5000),
			["xc17s40xl"]  = new PromInfo("XC17S40XL",               0x99, false,  true,  true,  524288, 19791, 16384,  102,  502, 5000),
			["xc17s50xl"]  = new PromInfo("XC17S50XL",               0xd6, false,  true,  true, 1048576, 19791, 16384,  102,  502, 5000),
			["xc17s100xl"] = new PromInfo("XC17S100XL",              0xd7, false,  true,  true, 1048576, 19791, 16384,  102,  502, 5000),
			["xc17s150xl"] = new PromInfo("XC17S150XL",              0xd9, false,  true,  true, 1048576, 19791, 16384,  102,  502, 5000),
			["xc17512l"]   = new PromInfo("XC17512L",                0x9b, false,  true,  true,  524288, 19791, 16384,  102,  502, 5000),
			["xc1701"]     = new PromInfo("XC1701",                  0xda,  true,  true,  true, 1048576, 19791, 16384,  102,  502, 5000),
			["xc1701l"]    = new PromInfo("XC1701L",                 0xdb, false,  true,  true, 1048576, 19791, 16384,  102,  502, 5000),
			["xc1702l"]    = new PromInfo("XC1702L",                 0x3b, false, false,  true, 2097152, 65632, 65536,  102,  200,  400),
			["xc1704l"]    = new PromInfo("XC1704L",                 0xbb, false, false,  true, 4194304, 65632, 65536,  102,  200,  400)
		};
	}
}
