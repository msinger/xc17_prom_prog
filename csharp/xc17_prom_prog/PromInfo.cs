using System;

namespace xc17_prom_prog
{
	public struct PromInfo
	{
		public string Type;
		public byte   Code;
		public bool   Device5V;
		public bool   Device5VProg;
		public bool   Device64Bit;
		public int    Density;
		public int    ClockToID;
		public int    ClockToReset;
		public int    ProgPulse;
		public int    ProgRetryPulse;
		public int    ProgResetPulse;

		public PromInfo(string type,
		                byte   code,
		                bool   dev5v,
		                bool   dev5v_prog,
		                bool   dev64bit,
		                int    density,
		                int    nidclk,
		                int    nrstclk,
		                int    tpgm,
		                int    tpgm1,
		                int    tprst)
		{
			Type           = type;
			Code           = code;
			Device5V       = dev5v;
			Device5VProg   = dev5v_prog;
			Device64Bit    = dev64bit;
			Density        = density;
			ClockToID      = nidclk;
			ClockToReset   = nrstclk;
			ProgPulse      = tpgm;
			ProgRetryPulse = tpgm1;
			ProgResetPulse = tprst;
		}
	}
}
