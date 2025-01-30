AC_PREREQ([2.63])
AC_INIT([xc17_prom_prog], [1.0])
AC_CONFIG_SRCDIR([src/top.sv])
AC_CONFIG_MACRO_DIR([m4])

AM_INIT_AUTOMAKE([foreign dist-bzip2 filename-length-max=99 no-dependencies tar-ustar])
AM_MAINTAINER_MODE

MY_CHECK_WS

MY_PROG_YOSYS
MY_PROG_NEXTPNR
MY_PROG_ICEPACK
MY_PROG_ICEPROG

MY_ARG_ENABLE(
	board,
	xc17_prom_prog,
	[Select board to build for.],
	[xc17_prom_prog, hx8k_brk]
)

FPGA_CORE=
FPGA_PACKAGE=
NUM_LEDS=0
HW_VERSION=
HW_TYPE=
if test "x$enable_board" = "xxc17_prom_prog"; then
	FPGA_CORE=hx4k
	FPGA_PACKAGE=tq144
	NUM_LEDS=1
	HW_VERSION=3
	HW_TYPE=1
fi
if test "x$enable_board" = "xhx8k_brk"; then
	FPGA_CORE=hx8k
	FPGA_PACKAGE=ct256
	NUM_LEDS=9
	HW_VERSION=3
	HW_TYPE=2
fi

AC_SUBST(BOARD, $enable_board)
AC_SUBST(FPGA_CORE)
AC_SUBST(FPGA_PACKAGE)
AC_SUBST(NUM_LEDS)
AC_SUBST(HDL_VERSION, ${VERSION%.*})
AC_SUBST(HW_VERSION)
AC_SUBST(HW_TYPE)

AC_CONFIG_FILES([
src/config.vh
src/Makefile
Makefile
])

AC_OUTPUT

echo
echo
echo "=== CONFIGURATION SUMMARY ==="
echo
echo "--- Tools ---"
echo "Yosys:                              $YOSYS"
echo "nextpnr:                            $NEXTPNR"
echo "IcePack:                            $ICEPACK"
echo "IceProg:                            $ICEPROG"
echo
echo "--- Hardware ---"
echo "Board:                              $enable_board"
echo "FPGA Core:                          $FPGA_CORE"
echo "FPGA Package:                       $FPGA_PACKAGE"
