SRC = \
cdc.sv \
top.sv \
uart_rx.sv \
uart_tx.sv

NODIST_SRC =

HDR =

NODIST_HDR = \
config.vh

PCF_FILES = \
xc17_prom_prog.pcf \
hx8k_brk.pcf

CONSTR_FILES = \
xc17_prom_prog-constraints.py

EXTRA_DIST = $(SRC) $(HDR) $(PCF_FILES) $(CONSTR_FILES)

CLEANFILES = \
$(NODIST_SRC) \
xc17_prom_prog.asc \
xc17_prom_prog.bin \
xc17_prom_prog.json

DISTCLEANFILES = \
$(NODIST_HDR)

USED_PCF_FILE = \
boards/@BOARD@.pcf

all: xc17_prom_prog.bin

prog: xc17_prom_prog.bin
	$(ICEPROG) $<

run: xc17_prom_prog.bin
	$(ICEPROG) -S $<

.PHONY: all prog run json

xc17_prom_prog.json: Makefile $(addprefix $(srcdir)/,$(SRC) $(HDR)) $(NODIST_SRC) $(NODIST_HDR)
	$(YOSYS) $(YOSYSFLAGS) -q -p "synth_ice40 -abc2 -json $@" $(addprefix $(srcdir)/,$(SRC)) $(NODIST_SRC)

xc17_prom_prog.asc: xc17_prom_prog.json $(srcdir)/$(USED_PCF_FILE) Makefile
	$(NEXTPNR) $(NEXTPNRFLAGS) --@FPGA_CORE@ --package @FPGA_PACKAGE@ --pcf $(srcdir)/$(USED_PCF_FILE) --pre-pack $(srcdir)/xc17_prom_prog-constraints.py --json $< --asc $@

xc17_prom_prog.bin: xc17_prom_prog.asc Makefile
	$(ICEPACK) $(ICEPACKFLAGS) $< $@
