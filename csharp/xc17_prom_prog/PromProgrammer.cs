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

		public const int Timeout = 1000;

		private const byte SOF            = 0x1b;
		private const byte ResultAck      = 0x06;
		private const byte ResultNack     = 0x15;
		private const byte ResultData     = 0x1a;
		private const byte ResultAsync    = 0x16;
		private const byte ResultBusy     = 0x07;
		private const byte ResultCeo      = 0x04;
		private const byte ResultEarlyCeo = 0x14;
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
		private const byte CmdRead        = 0x0b;
		private const byte CmdProgInc     = 0x0c;
		private const byte CmdProgVerify  = 0x0d;
		private const byte CmdProgStart   = 0x0e;

		private PromInfo prom;

		public void Program(BinaryReader r, bool contOnError = false)
		{
			if (prom.Density == 0)
				throw new InvalidOperationException("Need to configure PROM first.");
			int   length     = (prom.Density + 7) / 8;
			bool  odd        = false;
			int   blockSize  = BufferSize / 2;
			int[] offset     = new int[] { 0, blockSize / OffsetStep };
			bool  first      = true;
			bool  failed     = false;
			PowerOnProg();
			while (true)
			{
				int curLength = (length > blockSize) ? blockSize : length;
				if (length > 0)
				{
					byte[] buf = r.ReadBytes(curLength);
					if (buf.Length > curLength)
						throw new IOException("BinaryReader.ReadBytes() returned too big array.");
					if (buf.Length < curLength) /* EOF? */
					{
						byte[] buf2 = new byte[curLength];
						Array.Fill<byte>(buf2, 0xff);
						buf.CopyTo(buf2, 0);
						buf = buf2;
					}
					WriteBuffer(offset[odd ? 1 : 0], buf);
				}
				if (!first)
				{
					switch (Poll())
					{
					case ResultAck:
						break;
					default:
						failed = true;
						if (!contOnError)
							throw new InvalidResponseException("No acknowledge");
						break;
					}
				}
				if (length > 0)
					AsyncProgramFromBuffer(offset[odd ? 1 : 0], curLength, contOnError);
				if (length <= 0)
					break;
				first = false;
				length -= blockSize;
				odd = !odd;
			}
			PowerOff();
			if (failed)
				throw new InvalidResponseException("Failed");
		}

		public void ProgramResetPolarity()
		{
			if (prom.ClockToReset == 0)
				throw new InvalidOperationException("Need to configure PROM first.");
			PowerOnProg();
			WriteBuffer(0, new byte[8] { 0, 0, 0, 0, 0, 0, 0, 0 });
			ProgIncrement(prom.ClockToReset);
			SendCmdSynced(CmdProgStart, 1, 0, 0, 1, true);
			PowerOff();
		}

		public bool IsBlank(bool invReset)
		{
			return ReadOrBlankCheck(BinaryWriter.Null, invReset, false);
		}

		public void Read(BinaryWriter w, bool invReset, bool marginVoltage)
		{
			ReadOrBlankCheck(w, invReset, marginVoltage);
		}

		public bool Verify(BinaryReader r, bool invReset, bool marginVoltage)
		{
			MemoryStream m = new MemoryStream();
			BinaryWriter w = new BinaryWriter(m);
			ReadOrBlankCheck(w, invReset, marginVoltage);
			m.Seek(0, SeekOrigin.Begin);
			bool eof_a = false, eof_b = false;
			while (!eof_a || !eof_b)
			{
				int a = -1, b = -1;
				if (!eof_a)
					a = m.ReadByte();
				if (a == -1)
					eof_a = true;
				if (!eof_b)
				{
					try
					{
						b = r.ReadByte();
					}
					catch (EndOfStreamException)
					{
						eof_b = true;
					}
				}
				if (eof_a == eof_b)
				{
					if (a != b)
						return false;
				}
				else if (eof_a)
				{
					if (b != 0xff)
						return false;
				}
				else if (eof_b)
				{
					if (a != 0xff)
						return false;
				}
			}
			return true;
		}

		private bool ReadOrBlankCheck(BinaryWriter w, bool invReset, bool marginVoltage)
		{
			if (prom.Density == 0)
				throw new InvalidOperationException("Need to configure PROM first.");
			int   length     = (prom.Density + 7) / 8;
			int   prevLength = 0;
			bool  odd        = false;
			int   blockSize  = BufferSize / 2;
			int[] offset     = new int[] { 0, blockSize / OffsetStep };
			bool  first      = true;
			bool  earlyCeo   = false;
			bool  ceo        = false;
			bool  blank      = true;
			if (marginVoltage)
				PowerOnVerify(invReset);
			else
				PowerOnRead(invReset);
			while (true)
			{
				if (!first)
				{
					switch (Poll())
					{
					case ResultAck:
						break;
					case ResultCeo:
						if (length > 0)
							earlyCeo = true;
						else
							ceo = true;
						break;
					case ResultEarlyCeo:
						earlyCeo = true;
						break;
					default:
						throw new InvalidResponseException("No acknowledge");
					}
				}
				if (length > 0)
					AsyncReadToBuffer(offset[odd ? 1 : 0], (length > blockSize) ? blockSize : length);
				if (!first)
				{
					byte[] buf = ReadBuffer(offset[odd ? 0 : 1], prevLength);
					w.Write(buf);
					for (int i = 0; i < prevLength; i++)
						if (buf[i] != 0xff)
							blank = false;
				}
				if (length <= 0)
					break;
				first = false;
				prevLength = (length > blockSize) ? blockSize : length;
				length -= blockSize;
				odd = !odd;
			}
			PowerOff();
			w.Flush();
			if (earlyCeo)
				throw new InvalidResponseException("Early CEO");
			if (!ceo)
				throw new InvalidResponseException("No CEO");
			return blank;
		}

		public void VerifyDeviceID()
		{
			if (prom.ClockToID == 0)
				throw new InvalidOperationException("Need to configure PROM first.");
			PowerOnProg();
			ProgIncrement(prom.ClockToID);
			byte[] buf = ProgVerify();
			buf[0] = (byte)((buf[0] & 0x55) << 1 | (buf[0] & 0xaa) >> 1);
			buf[0] = (byte)((buf[0] & 0x33) << 2 | (buf[0] & 0xcc) >> 2);
			buf[0] = (byte)((buf[0] & 0x0f) << 4 | (buf[0] & 0xf0) >> 4);
			buf[1] = (byte)((buf[1] & 0x55) << 1 | (buf[1] & 0xaa) >> 1);
			buf[1] = (byte)((buf[1] & 0x33) << 2 | (buf[1] & 0xcc) >> 2);
			buf[1] = (byte)((buf[1] & 0x0f) << 4 | (buf[1] & 0xf0) >> 4);
			PowerOff();
			if (buf[0] != 0xc9)
				throw new InvalidResponseException("Read manufacturer ID (0x" + buf[0].ToString("x2") +
				                                   ") does not match Xilinx ID (0xc9).");
			if (buf[1] != prom.Code)
				throw new InvalidResponseException("Read device ID (0x" + buf[1].ToString("x2") +
				                                   ") does not match " + prom.Type + " (0x" +
				                                   prom.Code.ToString("x2") + ").");
		}

		public bool IsResetInverted()
		{
			if (prom.ClockToReset == 0)
				throw new InvalidOperationException("Need to configure PROM first.");
			PowerOnProg();
			ProgIncrement(prom.ClockToReset, true);
			byte[] buf = ProgVerify(true);
			PowerOff();
			if ((buf[0] != 0 && buf[0] != 0xff) || buf[0] != buf[1] || buf[0] != buf[2] || buf[0] != buf[3])
				throw new InvalidResponseException("Inconsistent reset polarity.");
			return buf[0] == 0;
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

		private void PowerOnRead(bool invReset = false)
		{
			SendCmdSynced(CmdPwrOnRead, (byte)(invReset ? 0x01 : 0x00), 0, 0, 0, true);
		}

		private void PowerOnVerify(bool invReset = false)
		{
			SendCmdSynced(CmdPwrOnVerify, (byte)(invReset ? 0x01 : 0x00), 0, 0, 0, true);
		}

		private void PowerOnProg()
		{
			SendCmdSynced(CmdPwrOnProg, 0, 0, 0, 0, true);
		}

		private void AsyncProgramFromBuffer(int offset, int length, bool contOnError = false)
		{
			if (offset < 0 || offset >= BufferSize / OffsetStep)
				throw new ArgumentException("", "offset");
			if (length <= 0 || length > BufferSize)
				throw new ArgumentException("", "length");
			int wordCount = length / (prom.Device64Bit ? 8 : 4);
			if (wordCount * (prom.Device64Bit ? 8 : 4) != length)
				throw new ArgumentException("", "length");
			int offsetInByte = offset * OffsetStep;
			if (length > BufferSize - offsetInByte)
				throw new ArgumentException();
			byte arg0, arg1;
			arg0  = (byte)(wordCount & 0xff);
			arg1  = (byte)((wordCount >> 8) & 0x03);
			arg1 |= (byte)((offset & 0x0f) << 2);
			byte result = SendCmd(CmdProgStart, arg0, arg1, 0, (byte)(contOnError ? 0x02 : 0x00));
			if (result != ResultAsync)
				throw new InvalidResponseException();
		}

		private void AsyncReadToBuffer(int offset, int length)
		{
			if (offset < 0 || offset >= BufferSize / OffsetStep)
				throw new ArgumentException("", "offset");
			if (length <= 0 || length > BufferSize)
				throw new ArgumentException("", "length");
			int wordCount = length / (prom.Device64Bit ? 8 : 4);
			if (wordCount * (prom.Device64Bit ? 8 : 4) != length)
				throw new ArgumentException("", "length");
			int offsetInByte = offset * OffsetStep;
			if (length > BufferSize - offsetInByte)
				throw new ArgumentException();
			byte arg0, arg1;
			arg0  = (byte)(wordCount & 0xff);
			arg1  = (byte)((wordCount >> 8) & 0x03);
			arg1 |= (byte)((offset & 0x0f) << 2);
			byte result = SendCmd(CmdRead, arg0, arg1, 0, 0);
			if (result != ResultAsync)
				throw new InvalidResponseException();
		}

		private void ProgIncrement(int count = 1, bool senseReset = false)
		{
			SendCmdSynced(CmdProgInc, (byte)(count & 0xff),
			                          (byte)((count >> 8) & 0xff),
			                          (byte)((count >> 16) & 0x01),
			                          (byte)(senseReset ? 0x01 : 0x00),
			                          true);
		}

		private byte[] ProgVerify(bool senseReset = false)
		{
			SendCmdSynced(CmdProgVerify, 0, 0, 0, (byte)(senseReset ? 0x01 : 0x00), true);
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

		public PromProgrammer(string port, TextWriter log)
		{
			this.prom = new PromInfo();
			this.port = port;
			this.log = (log != null) ? log : TextWriter.Null;
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
