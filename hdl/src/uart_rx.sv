`default_nettype none

(* nolatches *)
module uart_rx #(
		parameter int DATABITS = 8,
		parameter int BAUDDIV  = 12
	) (
		input  logic                clk,
		input  logic                reset,

		output logic                valid, /* true, when STOPBIT was set */
		output logic [DATABITS-1:0] data,
		output logic                seq,   /* toggles when next data byte is ready */
		input  logic                ack,   /* needs to be set to seq to restart receiver after each byte */

		input  logic                rx,
		output logic                n_cts
	);

	typedef logic [2:0]                  state_t;
	typedef logic [DATABITS-1:0]         word_t;
	typedef logic [$clog2(DATABITS)-1:0] bitnum_t;
	typedef logic [$clog2(BAUDDIV)-1:0]  div_t;

	localparam state_t state_wait_idle = 0;
	localparam state_t state_idle      = 1;
	localparam state_t state_start     = 2;
	localparam state_t state_data      = 3;
	localparam state_t state_stop      = 4;
	localparam state_t state_wait_ack  = 5;

	state_t  state;
	bitnum_t cur_bit;
	div_t    sub_count;
	word_t   shift;

	initial seq   = 0;
	initial n_cts = 1;

	initial state = state_wait_idle;

	always_ff @(posedge clk) begin
		unique case (state)
		state_wait_idle:
			if (rx) begin
				state      = state_idle;
				n_cts     <= 0;
			end
		state_idle:
			if (!rx) begin
				state      = state_start;
				sub_count  = BAUDDIV / 2;
				cur_bit    = 0;
			end
		state_start:
			if (sub_count == BAUDDIV - 1) begin
				state      = !rx ? state_data : state_idle;
				sub_count  = 0;
				n_cts     <= !rx;
			end else
				sub_count++;
		state_data:
			if (sub_count == BAUDDIV - 1) begin
				shift = { rx, shift[DATABITS-1:1] };
				if (cur_bit == DATABITS - 1)
					state = state_stop;
				else
					cur_bit++;
				sub_count = 0;
			end else
				sub_count++;
		state_stop:
			if (sub_count == BAUDDIV - 1) begin
				state      = state_wait_ack;
				valid     <= rx;
				data      <= shift;
				seq       <= !seq;
			end else
				sub_count++;
		state_wait_ack:
			if (seq == ack)
				state      = state_wait_idle;
		endcase

		if (reset) begin
			state  = state_wait_idle;
			n_cts <= 1;
		end
	end
endmodule
