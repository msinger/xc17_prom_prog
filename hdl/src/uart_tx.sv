`default_nettype none

(* nolatches *)
module uart_tx #(
		parameter int DATABITS = 8,
		parameter int BAUDDIV  = 12
	) (
		input  logic                clk,
		input  logic                reset,

		input  logic [DATABITS-1:0] data,
		input  logic                seq,  /* toggling triggers transfer of data byte */
		output logic                ack,  /* gets set to seq when next byte can be written */

		output logic                tx
	);

	typedef logic [1:0]                  state_t;
	typedef logic [DATABITS-1:0]         word_t;
	typedef logic [$clog2(DATABITS)-1:0] bitnum_t;
	typedef logic [$clog2(BAUDDIV)-1:0]  div_t;

	localparam state_t state_idle  = 0;
	localparam state_t state_start = 1;
	localparam state_t state_data  = 2;
	localparam state_t state_stop  = 3;

	state_t  state;
	bitnum_t cur_bit;
	div_t    sub_count;
	word_t   shift;

	initial ack = 0;
	initial tx  = 1;

	initial state = state_idle;

	always_ff @(posedge clk) begin
		unique case (state)
		state_idle:
			if (seq != ack) begin
				state      = state_start;
				sub_count  = 0;
				shift      = data;
				tx        <= 0;
			end
		state_start:
			if (sub_count == BAUDDIV - 1) begin
				state      = state_data;
				sub_count  = 0;
				cur_bit    = 0;
				tx        <= shift[0];
				shift      = { 1'bx, shift[DATABITS-1:1] };
			end else
				sub_count++;
		state_data:
			if (sub_count == BAUDDIV - 1) begin
				sub_count = 0;
				if (cur_bit == DATABITS - 1) begin
					state    = state_stop;
					tx      <= 1;
				end else begin
					cur_bit++;
					tx      <= shift[0];
					shift    = { 1'bx, shift[DATABITS-1:1] };
				end
			end else
				sub_count++;
		state_stop:
			if (sub_count == BAUDDIV - 1) begin
				state      = state_idle;
				ack       <= seq;
			end else
				sub_count++;
		endcase

		if (reset) begin
			state  = state_idle;
			tx    <= 1;
			ack   <= seq;
		end
	end
endmodule
