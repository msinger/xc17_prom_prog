using System;
using System.IO;
using System.IO.Ports;

namespace xc17_prom_prog
{
	public class PromProgrammer
	{
		public const int BufferSize   = 4096;
		public const int OffsetStep   = 256;
		public const int TicksPerUsec = 12;

		public const int Timeout = 250;

		private const byte SOF            = 0x1b;
		private const byte ResultAck      = 0x06;
		private const byte ResultNack     = 0x15;
		private const byte ResultData     = 0x1a;
		private const byte ResultAsync    = 0x16;
		private const byte ResultBusy     = 0x07;
		private const byte CmdReadBuffer  = 0x01;
		private const byte CmdWriteBuffer = 0x81;
		private const byte CmdPoll        = 0x02;
		private const byte CmdTestEcho    = 0x03;
		private const byte CmdTestVoltage = 0x04;
		private const byte CmdConfigProm  = 0x05;
		private const byte CmdQueryInfo   = 0x06;
		private const byte CmdPwrOff      = 0x07;
		private const byte CmdPwrOnRead   = 0x08;
		private const byte CmdPwrOnVerify = 0x09;
		private const byte CmdPwrOnProg   = 0x0a;
		private const byte CmdProgInc     = 0x0b;
		private const byte CmdProgVerify  = 0x0c;

		private PromInfo prom;

		public void VerifyDeviceID()
		{
			if (prom.ClockToID == 0)
				throw new InvalidOperationException("Need to configure PROM first.");
			PowerOnProg();
			ProgIncrement(prom.ClockToID);
			byte[] buf = ProgVerify();
			PowerOff();
			log.WriteLine(buf[0].ToString("x2"));
			log.WriteLine(buf[1].ToString("x2"));
			log.WriteLine(buf[2].ToString("x2"));
			log.WriteLine(buf[3].ToString("x2"));
			if (prom.Device64Bit)
			{
				log.WriteLine(buf[4].ToString("x2"));
				log.WriteLine(buf[5].ToString("x2"));
				log.WriteLine(buf[6].ToString("x2"));
				log.WriteLine(buf[7].ToString("x2"));
			}
		}

		public PromInfo Prom
		{
			get { return prom; }

			set
			{
				prom = value;
				ConfigProm();
			}
		}

		private void ConfigProm()
		{
			byte arg0 = 0;
			byte arg1, arg2, arg3;
			int tpgm, tpgm1, tprst;

			if (prom.Device5V)     arg0 |= 0x01;
			if (prom.Device5VProg) arg0 |= 0x02;
			if (prom.Device64Bit)  arg0 |= 0x04;

			/* Convert microseconds to 12 MHz ticks and throw away five least significant bits. */
			tpgm  = (prom.ProgPulse      * TicksPerUsec) >> 5;
			tpgm1 = (prom.ProgRetryPulse * TicksPerUsec) >> 5;
			tprst = (prom.ProgResetPulse * TicksPerUsec) >> 5;

			arg0 |= (byte)((tpgm  & 0x01f) << 3);
			arg1  = (byte)((tpgm  & 0x1e0) >> 5);
			arg1 |= (byte)((tpgm1 & 0x00f) << 4);
			arg2  = (byte)((tpgm1 & 0x1f0) >> 4);
			arg2 |= (byte)((tprst & 0x007) << 5);
			arg3  = (byte)((tprst & 0x7f8) >> 3);

			SendCmdSynced(CmdConfigProm, arg0, arg1, arg2, arg3, true);
		}

		public void PowerOff()
		{
			SendCmdSynced(CmdPwrOff, 0, 0, 0, 0, true);
		}

		private void PowerOnRead(bool inv_reset = false)
		{
			SendCmdSynced(CmdPwrOnRead, (byte)(inv_reset ? 0x01 : 0x00), 0, 0, 0, true);
		}

		private void PowerOnVerify(bool inv_reset = false)
		{
			SendCmdSynced(CmdPwrOnVerify, (byte)(inv_reset ? 0x01 : 0x00), 0, 0, 0, true);
		}

		private void PowerOnProg()
		{
			SendCmdSynced(CmdPwrOnProg, 0, 0, 0, 0, true);
		}

		private void ProgIncrement(int count = 1, bool sense_reset = false)
		{
			SendCmdSynced(CmdProgInc, (byte)(count & 0xff),
			                          (byte)((count >> 8) & 0xff),
			                          (byte)((count >> 16) & 0x01),
			                          (byte)(sense_reset ? 0x01 : 0x00),
			                          true);
		}

		private byte[] ProgVerify(bool sense_reset = false)
		{
			SendCmdSynced(CmdProgVerify, 0, 0, 0, (byte)(sense_reset ? 0x01 : 0x00), true);
			return ReadBuffer(0, prom.Device64Bit ? 8 : 4);
		}

		public void TestEcho()
		{
			byte[] sbuf = new byte[256];
			for (int i = 0; i < 256; i++) sbuf[i] = (byte)i;
			WriteBuffer(0, sbuf);
			SendCmdSynced(CmdTestEcho, 0, 0, 0, 0, true);
			byte[] rbuf = ReadBuffer(0, 256);
			for (int i = 0; i < 256; i++)
				if (rbuf[i] != (byte)i)
					throw new InvalidResponseException("Data mismatch");
		}

		public void TestVoltage(int arg)
		{
			if (arg < 0 || arg > 11)
				throw new ArgumentException("", "arg");
			SendCmdSynced(CmdTestVoltage, (byte)arg, 0, 0, 0, true);
		}

		public struct ProgrammerInfo
		{
			public int    HdlVersion;
			public int    HwVersion;
			public string HwType;
		}

		public ProgrammerInfo QueryInfo()
		{
			SendCmdSynced(CmdQueryInfo, 0, 0, 0, 0, true);
			byte[] buf = ReadBuffer(0, 4);
			ProgrammerInfo info = new ProgrammerInfo();
			info.HdlVersion = -1;
			info.HwVersion  = -1;
			info.HwType     = "unknown";
			if (buf[0] >= 2)
				info.HdlVersion = buf[1];
			if (buf[0] >= 3)
				info.HwVersion = buf[2];
			if (buf[0] >= 4)
			{
				switch (buf[3])
				{
				case 1:
					info.HwType = "XC17xxx PROM Programmer";
					break;
				case 2:
					info.HwType = "XC17xxx PROM Programmer with HX8K Breakout Board";
					break;
				}
			}
			return info;
		}

		private void WriteBuffer(int offset, byte[] data)
		{
			if (offset < 0 || offset >= BufferSize / OffsetStep)
				throw new ArgumentException("", "offset");
			if (data == null || data.Length == 0 || data.Length > BufferSize)
				throw new ArgumentException("", "data");
			int offsetInByte = offset * OffsetStep;
			if (data.Length > BufferSize - offsetInByte)
				throw new ArgumentException();
			SendData(CmdWriteBuffer, 0, (byte)offset, data, true);
		}

		private byte[] ReadBuffer(int offset, int length)
		{
			if (offset < 0 || offset >= BufferSize / OffsetStep)
				throw new ArgumentException("", "offset");
			if (length <= 0 || length > BufferSize)
				throw new ArgumentException("", "length");
			int offsetInByte = offset * OffsetStep;
			if (length > BufferSize - offsetInByte)
				throw new ArgumentException();
			byte result = SendCmd(CmdReadBuffer, 0, (byte)offset, (byte)length, (byte)(length >> 8));
			if (result != ResultData)
				throw new InvalidResponseException("No data");
			return Receive(length);
		}

		private byte Poll(bool wantAck = false)
		{
			var timer = new System.Diagnostics.Stopwatch();
			timer.Start();
			while (true)
			{
				byte result = SendCmd(CmdPoll, 0, 0, 0, 0);
				if (result == ResultBusy)
				{
					if (timer.ElapsedMilliseconds > Timeout)
						throw new InvalidResponseException("Timeout");
					continue;
				}
				if (wantAck && result != ResultAck)
					throw new InvalidResponseException("No acknowledge");
				return result;
			}
		}

		private string port;
		private SerialPort p;

		private byte Send(byte[] buf)
		{
			byte[] rbuf = new byte[1];
			p.Write(buf, 0, buf.Length);
			try { if (p.Read(rbuf, 0, 1) != 1) throw new InvalidResponseException(); }
			catch (TimeoutException) { throw new InvalidResponseException("Timeout"); }
			return rbuf[0];
		}

		private byte[] Receive(int len)
		{
			byte[] rbuf = new byte[len];
			int offset = 0;
			while (len > 0)
			{
				int received;
				try { received = p.Read(rbuf, offset, len); }
				catch (TimeoutException) { throw new InvalidResponseException("Timeout"); }
				if (received <= 0) throw new InvalidResponseException("Timeout");
				if (received > len) throw new InvalidResponseException();
				offset += received;
				len -= received;
			}
			return rbuf;
		}

		private byte SendCmdSynced(byte cmd, byte arg0, byte arg1, byte arg2, byte arg3, bool wantAck = false)
		{
			byte result = SendCmd(cmd, arg0, arg1, arg2, arg3);
			if (result != ResultAsync) throw new InvalidResponseException();
			return Poll(wantAck);
		}

		private byte SendCmd(byte cmd, byte arg0, byte arg1, byte arg2, byte arg3, bool wantAck = false)
		{
			byte[] sbuf = new byte[6];
			sbuf[0] = SOF;
			sbuf[1] = cmd;
			sbuf[2] = arg0;
			sbuf[3] = arg1;
			sbuf[4] = arg2;
			sbuf[5] = arg3;
			byte result = Send(sbuf);
			if (wantAck && result != ResultAck)
				throw new InvalidResponseException("No acknowledge");
			return result;
		}

		private byte SendData(byte cmd, byte arg0, byte arg1, byte[] data, bool wantAck = false)
		{
			if (data == null || data.Length == 0 || data.Length > BufferSize)
				throw new ArgumentException("", "data");
			byte[] sbuf = new byte[6 + data.Length];
			sbuf[0] = SOF;
			sbuf[1] = cmd;
			sbuf[2] = arg0;
			sbuf[3] = arg1;
			sbuf[4] = (byte)data.Length;
			sbuf[5] = (byte)(data.Length >> 8);
			for (int i = 0; i < data.Length; i++)
				sbuf[6 + i] = data[i];
			byte result = Send(sbuf);
			if (wantAck && result != ResultAck)
				throw new InvalidResponseException("No acknowledge");
			return result;
		}

		private void OpenPort()
		{
			if (p != null)
			{
				p.Close();
			}

			p = new SerialPort(port, 1000000, Parity.None, 8, StopBits.One);
			p.Handshake = Handshake.RequestToSend;
			p.DtrEnable = false;
			p.ReadBufferSize  = 512;
			p.WriteBufferSize = 512;
			p.WriteTimeout = Timeout;
			p.ReadTimeout = Timeout;
			try { p.DiscardNull = false; } catch { }
			try { p.ReceivedBytesThreshold = 1; } catch { }
			p.Open();
			ResetPort();
		}

		private readonly TextWriter log;
		private readonly TextWriter err;

		public PromProgrammer(string port, TextWriter log, TextWriter err)
		{
			this.prom = new PromInfo();
			this.port = port;
			this.log = (log != null) ? log : TextWriter.Null;
			this.err = (err != null) ? err : TextWriter.Null;
			OpenPort();
		}

		public void ResetPort()
		{
			p.BreakState = true;
			System.Threading.Thread.Sleep(10);
			p.DiscardOutBuffer();
			p.DiscardInBuffer();
			p.BreakState = false;
			System.Threading.Thread.Sleep(10);
			p.DiscardOutBuffer();
			p.DiscardInBuffer();
			Poll(false);
		}
	}
}
