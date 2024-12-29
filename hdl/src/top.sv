`default_nettype none
`include "config.vh"

(* nolatches *)
(* top *)
module top(
		input  logic clk12m,

		input  logic rx,
		output logic tx,
		input  logic n_rts,
		output logic n_cts,
		input  logic n_dtr,
		output logic n_dsr = 0,
		output logic n_dcd = 0,

		output logic [`NUM_LEDS-1:0] led,

		output logic vcc_gnd,
		output logic vcc_3v3,
		output logic vcc_5v,
		output logic vpp_gnd,
		output logic vpp_gnd_weak,
		output logic vpp_3v3,
		output logic vpp_3v7,
		output logic vpp_5v,
		output logic vpp_5v4,
		output logic vpp_12v25,
		output logic vpp_12v25_weak,

		output logic dir,
		output logic n_oe,
		output logic n_oe_ceo = 0,
		output logic n_oe_ctrl,

		inout  logic data,
		output logic clk,
		output logic n_ce,
		input  logic n_ceo,
		output logic reset_oe
	);

	localparam int us = 12; /* ticks per microsecond */

	logic [20:0] led_delay;
	initial led       = 0;
	initial led_delay = 0;
	always_ff @(posedge clk12m) begin
		if (&led_delay)
			led[0] <= 0;
		else
			led_delay++;
		if (vcc_gnd || vcc_3v3 || vcc_5v ||
		    vpp_gnd || vpp_gnd_weak || vpp_3v3 || vpp_3v7 || vpp_5v || vpp_5v4 || vpp_12v25 || vpp_12v25_weak) begin
			led[0]    <= 1;
			led_delay  = 0;
		end
	end

	logic data_ena, data_out, data_in, data_s;
	cdc #(1) data_cdc (clk12m, data_in, data_s);
	SB_IO #(
		.PIN_TYPE('b 00_1010),
		.PULLUP(1)
	) data_io (
		.PACKAGE_PIN(data),
		.INPUT_CLK(clk12m),
		.D_IN_0(data_in),
		.D_OUT_0(data_out),
		.OUTPUT_ENABLE(data_ena)
	);

	logic n_ceo_in, n_ceo_s;
	cdc #(1) n_ceo_cdc (clk12m, n_ceo_in, n_ceo_s);
	SB_IO #(
		.PIN_TYPE('b 00_0000),
		.PULLUP(1)
	) n_ceo_io (
		.PACKAGE_PIN(n_ceo),
		.INPUT_CLK(clk12m),
		.D_IN_0(n_ceo_in)
	);

	initial dir       = 0;
	initial n_oe      = 1;
	initial n_oe_ctrl = 1;
	initial clk       = 0;
	initial n_ce      = 0;
	initial reset_oe  = 0;
	initial data_out  = 0;
	initial data_ena  = 0;

	initial vcc_gnd        = 0;
	initial vcc_3v3        = 0;
	initial vcc_5v         = 0;
	initial vpp_gnd        = 0;
	initial vpp_gnd_weak   = 0;
	initial vpp_3v3        = 0;
	initial vpp_3v7        = 0;
	initial vpp_5v         = 0;
	initial vpp_5v4        = 0;
	initial vpp_12v25      = 0;
	initial vpp_12v25_weak = 0;

	typedef logic [1:0]  pstate_t;
	typedef logic [1:0]  pmode_t;
	typedef logic [3:0]  pcmd_t;
	typedef logic [31:0] parg_t;
	typedef logic [16:0] pstep_t;

	localparam pstate_t pstate_idle        = 0;
	localparam pstate_t pstate_proc        = 1;
	localparam pstate_t pstate_prog_inc    = 2;
	localparam pstate_t pstate_prog_verify = 3;

	localparam pmode_t pmode_off       = 0;
	localparam pmode_t pmode_read      = 1;
	localparam pmode_t pmode_verify    = 2;
	localparam pmode_t pmode_prog      = 3;

	localparam pcmd_t pcmd_test_echo    = 0;
	localparam pcmd_t pcmd_test_voltage = 1;
	localparam pcmd_t pcmd_config_prom  = 2;
	localparam pcmd_t pcmd_query_info   = 3;
	localparam pcmd_t pcmd_pwroff       = 4;
	localparam pcmd_t pcmd_pwron_read   = 5;
	localparam pcmd_t pcmd_pwron_verify = 6;
	localparam pcmd_t pcmd_pwron_prog   = 7;
	localparam pcmd_t pcmd_prog_inc     = 8;
	localparam pcmd_t pcmd_prog_verify  = 9;

	pstate_t pstate, next_pstate;
	pmode_t  pmode;
	pcmd_t   pcmd;
	parg_t   parg;
	logic    p_seq, p_ack, p_result, p_match;
	pstep_t  pstep;

	/*
	 * 5 volt device?
	 * If 1, the following voltages are being used:
	 *   - VCCread   = 5V
	 *   - VPPread   = 5V
	 *   - VCCverify = 5V
	 *   - VPPverify = 5.4V
	 * If 0, the following voltages are being used:
	 *   - VCCread   = 3.3V
	 *   - VPPread   = 3.3V
	 *   - VCCverify = 3.3V
	 *   - VPPverify = 3.7V
	 */
	logic dev5v;

	/*
	 * Device that uses 5 volt for programming?
	 * If 1, the following voltages are being used:
	 *   - VCCprog   = 5V
	 *   - VPP2      = 5.4V
	 *   - VCCnom    = 5V
	 *   - VPPnom    = 5V
	 * If 0, the following voltages are being used:
	 *   - VCCprog   = 3.3V
	 *   - VPP2      = 3.7V
	 *   - VCCnom    = 3.3V
	 *   - VPPnom    = 3.3V
	 */
	logic dev5v_prog;

	/* 1: Device uses 64 bit words.  0: Device used 32 bit words. */
	logic dev64bit;

	/* 1: Device is configured for active low reset.  0: Device is configured for active high reset. */
	logic inv_reset;

	/* Program pulse time and retry program pulse time in ticks. */
	logic [13:0] tpgm, tpgm1;

	/* Reset program pulse time in ticks. */
	logic [15:0] tprst;

	initial pstate     = pstate_idle;
	initial pmode      = pmode_off;
	initial p_seq      = 0;
	initial p_ack      = 0;
	initial p_result   = 0;
	initial dev5v      = 0;
	initial dev5v_prog = 0;
	initial dev64bit   = 0;
	initial inv_reset  = 0;

	task automatic disable_vcc();
		vcc_gnd <= 0;
		vcc_3v3 <= 0;
		vcc_5v  <= 0;
	endtask

	task automatic disable_vpp();
		vpp_gnd        <= 0;
		vpp_gnd_weak   <= 0;
		vpp_3v3        <= 0;
		vpp_3v7        <= 0;
		vpp_5v         <= 0;
		vpp_5v4        <= 0;
		vpp_12v25      <= 0;
		vpp_12v25_weak <= 0;
	endtask

	task automatic disable_all();
		disable_vcc;
		disable_vpp;
		pmode = pmode_off;
	endtask

	task automatic pwr_read();
		if (dev5v) begin
			vcc_5v  <= 1;
			vpp_5v  <= 1;
		end else begin
			vcc_3v3 <= 1;
			vpp_3v3 <= 1;
		end
		pmode = pmode_read;
	endtask

	task automatic pwr_verify();
		if (dev5v) begin
			vcc_5v  <= 1;
			vpp_5v4 <= 1;
		end else begin
			vcc_3v3 <= 1;
			vpp_3v7 <= 1;
		end
		pmode = pmode_verify;
	endtask

	task automatic pwr_prog();
		if (dev5v_prog) begin
			vcc_5v  <= 1;
			vpp_5v  <= 1;
		end else begin
			vcc_3v3 <= 1;
			vpp_3v3 <= 1;
		end
		pmode = pmode_prog;
	endtask

	task automatic pwr_vpp_nominal();
		if (dev5v_prog)
			vpp_5v  <= 1;
		else
			vpp_3v3 <= 1;
	endtask

	task automatic pwr_vpp2();
		if (dev5v_prog)
			vpp_5v4 <= 1;
		else
			vpp_3v7 <= 1;
	endtask

	task automatic reset_prom_pins();
		dir       <= 0;
		n_oe      <= 1;
		n_oe_ctrl <= 1;
		clk       <= 0;
		n_ce      <= 0;
		reset_oe  <= 0;
		data_out  <= 0;
		data_ena  <= 0;
	endtask

	task automatic reset_prom(input logic reset);
		if (inv_reset)
			reset_oe <= !reset;
		else
			reset_oe <= reset;
	endtask

	logic [5:0]  bit_pos;
	logic [11:0] byte_pos;
	logic [9:0]  word_count;
	logic [16:0] inc_count;
	logic [63:0] prog_word, verify_word;
	logic        sense_reset;

	always_ff @(posedge clk12m) begin
		logic ack;
		ack = 0;

		case (pstate)
		pstate_idle:
			if (p_seq != p_ack) begin
				pstate   = pstate_proc;
				pstep    = 0;
				byte_pos = 0;
			end
		pstate_proc:
			begin
				unique case (pcmd)
				pcmd_test_echo:
					begin
						rbuf[byte_pos] <= wbuf[byte_pos];
						if (byte_pos == 4095) begin
							pstate    = pstate_idle;
							ack       = 1;
							p_result <= 1;
						end
						byte_pos++;
					end
				pcmd_test_voltage:
					begin
						pstate     = pstate_idle;
						ack        = 1;
						p_result  <= 1;
						disable_all;
						unique0 case (parg)
							1:  vcc_gnd        <= 1;
							2:  vcc_3v3        <= 1;
							3:  vcc_5v         <= 1;
							4:  vpp_gnd        <= 1;
							5:  vpp_gnd_weak   <= 1;
							6:  vpp_3v3        <= 1;
							7:  vpp_3v7        <= 1;
							8:  vpp_5v         <= 1;
							9:  vpp_5v4        <= 1;
							10: vpp_12v25      <= 1;
							11: vpp_12v25_weak <= 1;
						endcase
					end
				pcmd_config_prom:
					begin
						pstate      = pstate_idle;
						ack         = 1;
						p_result   <= 1;
						dev5v      <= parg[0];
						dev5v_prog <= parg[1];
						dev64bit   <= parg[2];
						tpgm       <= parg[11:3]  << 5;
						tpgm1      <= parg[20:12] << 5;
						tprst      <= parg[31:21] << 5;
					end
				pcmd_query_info:
					begin
						unique case (byte_pos)
						0:
							rbuf[byte_pos] <= 4; /* length */
						1:
							rbuf[byte_pos] <= `HDL_VERSION;
						2:
							rbuf[byte_pos] <= `HW_VERSION;
						3:
							begin
								rbuf[byte_pos] <= `HW_TYPE;
								pstate          = pstate_idle;
								ack             = 1;
								p_result       <= 1;
							end
						endcase
						byte_pos++;
					end
				pcmd_pwroff:
					case (pstep)
					0*us:
						begin
							/* We need to set these three to GND one msec before cutting off
							 * power when exiting programming mode. */
							n_ce     <= 0;
							reset_oe <= 0;
							clk      <= 0;
						end
					1000*us:
						disable_all;
					1005*us:
						begin
							vcc_gnd <= 1;
							vpp_gnd <= 1;
						end
					2000*us:
						begin
							vcc_gnd <= 0;
							vpp_gnd <= 0;
						end
					2005*us:
						begin
							pstate    = pstate_idle;
							ack       = 1;
							p_result <= 1;
							reset_prom_pins;
						end
					endcase
				pcmd_pwron_read:
					case (pstep)
					0*us:
						if (pmode == pmode_off) begin
							reset_prom_pins;
							n_oe      <= 0;
							n_oe_ctrl <= 0;
							inv_reset <= parg[0];
						end else begin
							pstate     = pstate_idle;
							ack        = 1;
							p_result  <= 0;
						end
					4*us:
						pwr_read;
					1000*us:
						reset_prom(1);
					6000*us:
						begin
							pstate    = pstate_idle;
							ack       = 1;
							p_result <= 1;
							reset_prom(0);
						end
					endcase
				pcmd_pwron_verify:
					case (pstep)
					0*us:
						if (pmode == pmode_off) begin
							reset_prom_pins;
							n_oe      <= 0;
							n_oe_ctrl <= 0;
							inv_reset <= parg[0];
						end else begin
							pstate     = pstate_idle;
							ack        = 1;
							p_result  <= 0;
						end
					4*us:
						pwr_verify;
					1000*us:
						reset_prom(1);
					6000*us:
						begin
							pstate    = pstate_idle;
							ack       = 1;
							p_result <= 1;
							reset_prom(0);
						end
					endcase
				pcmd_pwron_prog:
					case (pstep)
					0*us:
						if (pmode == pmode_off) begin
							reset_prom_pins;
							n_oe      <= 0;
							n_oe_ctrl <= 0;
							n_ce      <= 1;
							reset_oe  <= 1;
						end else begin
							pstate     = pstate_idle;
							ack        = 1;
							p_result  <= 0;
						end
					4*us:
						pwr_prog;
					1000*us:
						disable_vpp;
					1004*us:
						vpp_12v25_weak <= 1;
					1010*us:
						clk <= 1;
					1011*us:
						clk <= 0;
					1012*us:
						clk <= 1;
					1013*us:
						disable_vpp;
					1017*us:
						begin
							vpp_gnd_weak <= 1;
							vpp_gnd      <= 1;
						end
					1019*us:
						vpp_gnd <= 0;
					1022*us:
						pwr_vpp_nominal;
					1024*us:
						if (dev5v_prog)
							vpp_gnd_weak <= 0;
					1028*us:
						if (!dev5v_prog)
							vpp_gnd_weak <= 0;
					1100*us:
						clk <= 0;
					1101*us:
						clk <= 1;
					6000*us:
						begin
							pstate    = pstate_idle;
							ack       = 1;
							p_result <= 1;
						end
					endcase
				pcmd_prog_inc:
					case (pstep)
					0*us:
						if (pmode == pmode_prog) begin
							reset_oe   <= 0;
							inc_count   = parg[16:0];
							sense_reset = parg[24];
							/* When reset is being read, DATA needs to be driven high
							 * before incrementing address counter. (data_ena is set
							 * one tick later.) */
							if (sense_reset) begin
								dir      <= 1;
								data_out <= 1;
							end
						end else begin
							pstate      = pstate_idle;
							ack         = 1;
							p_result   <= 0;
						end
					0*us+1:
						if (sense_reset)
							data_ena <= 1;
					4*us-1:
						begin
							pstate      = pstate_prog_inc;
							next_pstate = pstate_idle;
							pstep       = '1; /* gets incremented to 0 down below */
						end
					endcase
				pcmd_prog_verify:
					if (pmode == pmode_prog) begin
						byte_pos    = parg[13:10] << 8;
						sense_reset = parg[24];
						pstate      = pstate_prog_verify;
						next_pstate = pstate_idle;
						pstep       = '1; /* gets incremented to 0 down below */
					end else begin
						pstate      = pstate_idle;
						ack         = 1;
						p_result   <= 0;
					end
				endcase
				pstep++;
			end
		pstate_prog_inc:
			case (pstep)
			0*us:
				begin
					clk <= 0;
					pstep++;
					inc_count--;
				end
			1*us:
				begin
					clk <= 1;
					pstep++;
				end
			2*us-1:
				if (inc_count)
					pstep = 0;
				else
					pstep++;
			6*us:
				begin
					reset_oe <= 1;
					pstep++;
				end
			10*us:
				if (next_pstate == pstate_idle) begin
					pstate    = pstate_idle;
					ack       = 1;
					p_result <= 1;
				end else
					pstate    = next_pstate;
			default:
				pstep++;
			endcase
		pstate_prog_verify:
			case (pstep)
			0*us:
				begin
					n_ce    <= 0;
					bit_pos  = 0;
					pstep++;
				end
			4*us:
				begin
					verify_word <<= 1;
					if (sense_reset) /* Reset polarity is read from /CEO and its state is inverted. */
						verify_word[0] = !n_ceo_s;
					else begin
						verify_word[0] = data_s;
						if (!(dev64bit && bit_pos == 63) && !(!dev64bit && bit_pos == 31))
							clk <= 0; /* Clock only if we are not reading reset polarity. */
					end
					pstep++;
				end
			5*us:
				begin
					clk <= 1;
					if (next_pstate == pstate_idle && &bit_pos[2:0]) begin
						rbuf[byte_pos] <= { verify_word[0], verify_word[1],
						                    verify_word[2], verify_word[3],
						                    verify_word[4], verify_word[5],
						                    verify_word[6], verify_word[7] };
						byte_pos++;
					end
					pstep++;
					if (!(dev64bit && bit_pos == 63) && !(!dev64bit && bit_pos == 31))
						pstep = 3*us;
					bit_pos++;
				end
			9*us-1:
				begin
					/* In case of reading reset polarity, restore data to be an input. */
					data_ena <= 0;
					pstep++;
				end
			9*us:
				begin
					n_ce <= 1;
					dir  <= 0;
					if (next_pstate == pstate_idle) begin
						pstate    = pstate_idle;
						ack       = 1;
						p_result <= 1;
					end else begin
						if ((dev64bit && verify_word != prog_word) ||
						    (!dev64bit && verify_word[31:0] != prog_word[31:0])) begin
							pstate    = pstate_idle;
							ack       = 1;
							p_result <= 0;
							p_match  <= 0;
						end else
							pstate    = next_pstate;
					end
				end
			default:
				pstep++;
			endcase
		endcase

		if (reset) begin
			pstate      = pstate_idle;
			pmode       = pmode_off;
			ack         = 1;
			p_result   <= 0;
			p_match    <= 0;
			dev5v      <= 0;
			dev5v_prog <= 0;
			dev64bit   <= 0;
			inv_reset  <= 0;
			tpgm       <= 0;
			tpgm1      <= 0;
			tprst      <= 0;
			disable_all;
			reset_prom_pins;
		end

		if (ack)
			p_ack <= p_seq;
	end

	logic reset;
	logic [3:0] reset_count;
	initial reset       = 1;
	initial reset_count = 0;
	always_ff @(posedge clk12m) begin
		if (!&reset_count)
			reset_count++;
		if (reset && &reset_count)
			reset <= 0;
	end

	logic rx_seq, rx_ack, rx_valid;
	byte  rx_data;
	initial rx_ack = 0;
	uart_rx #(
		.BAUDDIV(12)
	) uart_rx (
		.clk(clk12m),
		.reset,

		.valid(rx_valid),
		.data(rx_data),
		.seq(rx_seq),
		.ack(rx_ack),

		.rx(rx_s),
		.n_cts
	);

	logic rx_in, rx_s;
	cdc #(1) rx_cdc (clk12m, rx_in, rx_s);
	SB_IO #(
		.PIN_TYPE('b 00_0000),
		.PULLUP(1)
	) rx_io (
		.PACKAGE_PIN(rx),
		.INPUT_CLK(clk12m),
		.D_IN_0(rx_in)
	);

	logic tx_seq, tx_ack;
	byte  tx_data;
	initial tx_seq = 0;
	uart_tx #(
		.BAUDDIV(12)
	) uart_tx (
		.clk(clk12m),
		.reset,

		.data(tx_data),
		.seq(tx_seq),
		.ack(tx_ack),

		.tx
	);

	byte rbuf[4096];
	byte wbuf[4096];

	typedef logic [2:0]  cstate_t;
	typedef logic [1:0]  argidx_t;
	typedef logic [11:0] size_t;

	localparam cstate_t cstate_idle          = 0;
	localparam cstate_t cstate_rx_cmd        = 1;
	localparam cstate_t cstate_rx_arg        = 2;
	localparam cstate_t cstate_rx_data       = 3;
	localparam cstate_t cstate_proc          = 4;
	localparam cstate_t cstate_tx_result     = 5;
	localparam cstate_t cstate_tx_data_start = 6;
	localparam cstate_t cstate_tx_data       = 7;

	cstate_t           cstate;
	byte               ccmd, result;
	(* mem2reg *) byte arg[4];
	argidx_t           argi;
	size_t             bufi;

	initial cstate = cstate_idle;

	localparam byte sof               = 'h1b;
	localparam byte result_ack        = 'h06;
	localparam byte result_nack       = 'h15;
	localparam byte result_data       = 'h1a;
	localparam byte result_async      = 'h16;
	localparam byte result_busy       = 'h07;
	localparam byte ccmd_read_buffer  = 'h01;
	localparam byte ccmd_write_buffer = 'h81;
	localparam byte ccmd_poll         = 'h02;
	localparam byte ccmd_test_echo    = 'h03;
	localparam byte ccmd_test_voltage = 'h04;
	localparam byte ccmd_config_prom  = 'h05;
	localparam byte ccmd_query_info   = 'h06;
	localparam byte ccmd_pwroff       = 'h07;
	localparam byte ccmd_pwron_read   = 'h08;
	localparam byte ccmd_pwron_verify = 'h09;
	localparam byte ccmd_pwron_prog   = 'h0a;
	localparam byte ccmd_prog_inc     = 'h0b;
	localparam byte ccmd_prog_verify  = 'h0c;

	always_ff @(posedge clk12m) begin
		logic  ack;
		size_t size, rw_start, rw_end;
		ack      = 0;
		size     = arg[2] | (arg[3] << 8);
		rw_start = arg[1] << 8;
		rw_end   = rw_start + size;

		if (rx_valid) unique case (cstate)
		cstate_idle:
			if (rx_seq != rx_ack) begin
				if (rx_data == sof)
					cstate = cstate_rx_cmd;
				ack = 1;
			end
		cstate_rx_cmd:
			if (rx_seq != rx_ack) begin
				ccmd   = rx_data;
				cstate = cstate_rx_arg;
				argi   = 0;
				ack    = 1;
			end
		cstate_rx_arg:
			if (rx_seq != rx_ack) begin
				arg[argi] = rx_data;
				if (argi == 3) begin
					cstate = ccmd[7] ? cstate_rx_data : cstate_proc;
					ack    = ccmd[7];
					bufi   = rw_start;
				end else
					ack    = 1;
				argi++;
			end
		cstate_rx_data:
			if (rx_seq != rx_ack) begin
				if (ccmd == ccmd_write_buffer)
					wbuf[bufi] <= rx_data;
				bufi++;
				if (bufi == rw_end)
					cstate = cstate_proc;
				else
					ack = 1;
			end
		cstate_proc:
			unique case (ccmd)
			ccmd_read_buffer:
				begin
					cstate = cstate_tx_data_start;
					ack    = 1;
				end
			ccmd_write_buffer:
				begin
					cstate = cstate_tx_result;
					result = result_ack;
					ack    = 1;
				end
			ccmd_poll:
				if (p_seq == p_ack) begin
					cstate  = cstate_tx_result;
					result  = p_result ? result_ack : result_nack;
					ack     = 1;
				end else begin
					cstate  = cstate_tx_result;
					result  = result_busy;
					ack     = 1;
				end
			ccmd_test_echo:
				if (p_seq == p_ack) begin
					cstate  = cstate_tx_result;
					result  = result_async;
					ack     = 1;
					pcmd   <= pcmd_test_echo;
					parg   <= { arg[3], arg[2], arg[1], arg[0] };
					p_seq  <= !p_seq;
				end else begin
					cstate  = cstate_tx_result;
					result  = result_busy;
					ack     = 1;
				end
			ccmd_test_voltage:
				if (p_seq == p_ack) begin
					cstate  = cstate_tx_result;
					result  = result_async;
					ack     = 1;
					pcmd   <= pcmd_test_voltage;
					parg   <= { arg[3], arg[2], arg[1], arg[0] };
					p_seq  <= !p_seq;
				end else begin
					cstate  = cstate_tx_result;
					result  = result_busy;
					ack     = 1;
				end
			ccmd_config_prom:
				if (p_seq == p_ack) begin
					cstate  = cstate_tx_result;
					result  = result_async;
					ack     = 1;
					pcmd   <= pcmd_config_prom;
					parg   <= { arg[3], arg[2], arg[1], arg[0] };
					p_seq  <= !p_seq;
				end else begin
					cstate  = cstate_tx_result;
					result  = result_busy;
					ack     = 1;
				end
			ccmd_query_info:
				if (p_seq == p_ack) begin
					cstate  = cstate_tx_result;
					result  = result_async;
					ack     = 1;
					pcmd   <= pcmd_query_info;
					parg   <= { arg[3], arg[2], arg[1], arg[0] };
					p_seq  <= !p_seq;
				end else begin
					cstate  = cstate_tx_result;
					result  = result_busy;
					ack     = 1;
				end
			ccmd_pwroff:
				if (p_seq == p_ack) begin
					cstate  = cstate_tx_result;
					result  = result_async;
					ack     = 1;
					pcmd   <= pcmd_pwroff;
					parg   <= { arg[3], arg[2], arg[1], arg[0] };
					p_seq  <= !p_seq;
				end else begin
					cstate  = cstate_tx_result;
					result  = result_busy;
					ack     = 1;
				end
			ccmd_pwron_read:
				if (p_seq == p_ack) begin
					cstate  = cstate_tx_result;
					result  = result_async;
					ack     = 1;
					pcmd   <= pcmd_pwron_read;
					parg   <= { arg[3], arg[2], arg[1], arg[0] };
					p_seq  <= !p_seq;
				end else begin
					cstate  = cstate_tx_result;
					result  = result_busy;
					ack     = 1;
				end
			ccmd_pwron_verify:
				if (p_seq == p_ack) begin
					cstate  = cstate_tx_result;
					result  = result_async;
					ack     = 1;
					pcmd   <= pcmd_pwron_verify;
					parg   <= { arg[3], arg[2], arg[1], arg[0] };
					p_seq  <= !p_seq;
				end else begin
					cstate  = cstate_tx_result;
					result  = result_busy;
					ack     = 1;
				end
			ccmd_pwron_prog:
				if (p_seq == p_ack) begin
					cstate  = cstate_tx_result;
					result  = result_async;
					ack     = 1;
					pcmd   <= pcmd_pwron_prog;
					parg   <= { arg[3], arg[2], arg[1], arg[0] };
					p_seq  <= !p_seq;
				end else begin
					cstate  = cstate_tx_result;
					result  = result_busy;
					ack     = 1;
				end
			ccmd_prog_inc:
				if (p_seq == p_ack) begin
					cstate  = cstate_tx_result;
					result  = result_async;
					ack     = 1;
					pcmd   <= pcmd_prog_inc;
					parg   <= { arg[3], arg[2], arg[1], arg[0] };
					p_seq  <= !p_seq;
				end else begin
					cstate  = cstate_tx_result;
					result  = result_busy;
					ack     = 1;
				end
			ccmd_prog_verify:
				if (p_seq == p_ack) begin
					cstate  = cstate_tx_result;
					result  = result_async;
					ack     = 1;
					pcmd   <= pcmd_prog_verify;
					parg   <= { arg[3], arg[2], arg[1], arg[0] };
					p_seq  <= !p_seq;
				end else begin
					cstate  = cstate_tx_result;
					result  = result_busy;
					ack     = 1;
				end
			default:
				begin
					cstate = cstate_tx_result;
					result = result_nack;
					ack    = 1;
				end
			endcase
		cstate_tx_result:
			if (tx_seq == tx_ack) begin
				cstate   = cstate_idle;
				tx_data <= result;
				tx_seq  <= !tx_seq;
			end
		cstate_tx_data_start:
			if (tx_seq == tx_ack) begin
				cstate   = cstate_tx_data;
				bufi     = rw_start;
				tx_data <= result_data;
				tx_seq  <= !tx_seq;
			end
		cstate_tx_data:
			if (tx_seq == tx_ack) begin
				if (ccmd == ccmd_read_buffer)
					tx_data <= rbuf[bufi];
				bufi++;
				tx_seq <= !tx_seq;
				if (bufi == rw_end)
					cstate = cstate_idle;
			end
		endcase

		if (reset || !rx_valid) begin
			cstate = cstate_idle;
			ack    = 1;
		end

		if (ack)
			rx_ack <= rx_seq;
	end

endmodule
