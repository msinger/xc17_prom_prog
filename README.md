XC17xxx PROM Programmer
=======================

Tool for programming Xilinx XC17xxx families of PROM chips that are used for storing the configuration
bitstream of the Spartan FPGA.

I developed this programmer to be able to program the PROM chips used in the Wide-Boy64. The Wide-Boy64
CGB uses the XC1701L, and the [Wide-Boy64 AGB](http://iceboy.a-singer.de/doc/wide_boy.html) uses the XC17S20XL.

I originally thought that the Wide-Boy64 CGB uses the XC1701 chip (without the L at the end), because that's
how those chips are labeled on their package. After buying some of those XC1701 chips and reading out their
device ID, I realized that they actually are XC1701L chips. This makes sense by the way, since the Spartan
XC4010XL FPGA inside the Wide-Boy64 CGB has 3.3&nbsp;V IO voltage, which matches the XC1701L PROM. The XC1701
is a 5&nbsp;V device.

You can still buy the XC17S20XL chips here:
- https://de.aliexpress.com/item/1005006854844265.html
- https://www.win-source.net/products/detail/xilinx-inc/xc17s20lpc.html
- https://www.win-source.net/products/detail/xilinx-inc/xc17s20xlpd8c.html

You can still buy a very limited supply of the XC1701L chips here:
- https://www.win-source.net/products/detail/xilinx-inc/xc1701pd8c.html

The Xilinx datasheets recommend the XC17512L to be used with the Spartan XC4010XL (which is the FPGA used in
the Wide-Boy64 CGB). So, the XC17512L could be used instead of the XC1701L.


Files in this repo
------------------

| File/Folder                                 | Description                                                            |
| ------------------------------------------- | ---------------------------------------------------------------------- |
| ./pcb/xc17\_prom\_prog/                     | KiCad project.                                                         |
| ./pcb/xc17\_prom\_prog/bom.ods              | Bill of materials (BOM) needed to populate the PCB.                    |
| ./pcb/lib/                                  | KiCad symbol libraries used in KiCad project.                          |
| ./ftdi/xc17\_prom\_prog\_ftdi\_template.xml | Template for configuring the FTDI chip on the PCB.                     |
| ./hdl/                                      | Verilog project that generates the configuration for the iCE40 FPGA.   |
| ./csharp/                                   | C# project that builds the command line tool for using the programmer. |


List of supported chips
-----------------------

I designed the programmer to be able to program the chips listed in the table below. Those are the ones I was able
to find the "Programmer Qualification Specification" datasheets for. Maybe it can be modified to work for other
variants of those PROM chips when more datasheets are found.

| PROM type              | ID   | Voltage group | Word size (bit) | Density (bit) | N<sub>IDCLK</sub> | N<sub>RSTCLK</sub> | T<sub>PGM</sub> (&micro;s) | T<sub>PGM1</sub> (&micro;s) | T<sub>PRST</sub> (&micro;s) |
| :--------------------- | :--- | :-----------: | --------------: | ------------: | ----------------: | -----------------: | -------------------------: | --------------------------: | --------------------------: |
| XC1736E                | 0xED | A             |              32 |         36288 |              2056 |               2048 |                       1000 |                         N/A |                        5000 |
| XC1765E                | 0xFD | A             |              32 |         65536 |              2056 |               2048 |                       1000 |                         N/A |                        5000 |
| XC1765X aka XC1765EL   | 0xFC | B             |              32 |         65536 |              2056 |               2048 |                       1000 |                         N/A |                        5000 |
| XC17128E               | 0x8D | A             |              64 |        131072 |              4600 |               4104 |                       1000 |                         N/A |                        5000 |
| XC17128X aka XC17128EL | 0x8C | B             |              64 |        131072 |              4600 |               4104 |                       1000 |                         N/A |                        5000 |
| XC17256E               | 0xAD | A             |              64 |        262144 |              4600 |               4104 |                       1000 |                         N/A |                        5000 |
| XC17256X aka XC17256EL | 0xAC | B             |              64 |        262144 |              4600 |               4104 |                       1000 |                         N/A |                        5000 |
| XC17S05                | 0xF8 | A             |              32 |         65536 |              2056 |               2048 |                        100 |                         500 |                        5000 |
| XC17S05XL              | 0x87 | B             |              64 |        131072 |              4600 |               4104 |                        100 |                         500 |                        5000 |
| XC17S10                | 0x88 | A             |              64 |        131072 |              4600 |               4104 |                        100 |                         500 |                        5000 |
| XC17S10XL              | 0x89 | B             |              64 |        131072 |              4600 |               4104 |                        100 |                         500 |                        5000 |
| XC17S20                | 0xA8 | A             |              64 |        262144 |              4600 |               4104 |                        100 |                         500 |                        5000 |
| XC17S20XL              | 0xA9 | B             |              64 |        262144 |              4600 |               4104 |                        100 |                         500 |                        5000 |
| XC17S30                | 0xA6 | A             |              64 |        262144 |              4600 |               4104 |                        100 |                         500 |                        5000 |
| XC17S30XL              | 0xA7 | B             |              64 |        262144 |              4600 |               4104 |                        100 |                         500 |                        5000 |
| XC17S40                | 0x98 | A             |              64 |        524288 |             19791 |              16384 |                        100 |                         500 |                        5000 |
| XC17S40XL              | 0x99 | B             |              64 |        524288 |             19791 |              16384 |                        100 |                         500 |                        5000 |
| XC17S50XL              | 0xD6 | B             |              64 |       1048576 |             19791 |              16384 |                        100 |                         500 |                        5000 |
| XC17S100XL             | 0xD7 | B             |              64 |       1048576 |             19791 |              16384 |                        100 |                         500 |                        5000 |
| XC17S150XL             | 0xD9 | B             |              64 |       1048576 |             19791 |              16384 |                        100 |                         500 |                        5000 |
| XC17512L               | 0x9B | B             |              64 |        524288 |             19791 |              16384 |                        100 |                         500 |                        5000 |
| XC1701                 | 0xDA | A             |              64 |       1048576 |             19791 |              16384 |                        100 |                         500 |                        5000 |
| XC1701L                | 0xDB | B             |              64 |       1048576 |             19791 |              16384 |                        100 |                         500 |                        5000 |
| XC1702L                | 0x3B | C             |              64 |       2097152 |             65632 |              65536 |                        100 |                         200 |                         400 |
| XC1704L                | 0xBB | C             |              64 |       4194304 |             65632 |              65536 |                        100 |                         200 |                         400 |

The table also contains the properties that are different between the PROM types. I listed them here to have an overview;
makes it easier than reading through the datasheets over and over.

The ID column contains the device ID, which has the "density code" in its high nibble and the "algorithm code" in the
low nibble.

N<sub>IDCLK</sub> is the number of times the word address needs to be incremented to reach the manufacturer and device ID
page. N<sub>RSTCLK</sub> is the number of times the word address needs to be incremented to reach the page that stores
the reset polarity.

T<sub>PGM</sub> is the time for how long V<sub>PP1</sub> needs to be applied to program a word. T<sub>PGM1</sub> is the
time that V<sub>PP1</sub> needs to be applied for retries when programming failed. Not all PROM types support alternating
programming and verifying on a word for word basis, so not all of them can do retries. T<sub>PRST</sub> is the time for
how long V<sub>PP1</sub> needs to be applied to program the reset polarity.

The PROMs can be divided into three different "voltage groups", which define what voltages need to be applied to the
VCC and VPP pins:

| Group | V<sub>CC</sub>, V<sub>PP</sub>, V<sub>CCVFY</sub> | V<sub>CCP</sub>, V<sub>CCNOM</sub>, V<sub>PPNOM</sub> | V<sub>PP1</sub> | V<sub>PP2</sub> | V<sub>PPVFY</sub> |
| :---: | ------------------------------------------------: | ----------------------------------------------------: | --------------: | --------------: | ----------------: |
| A     |                                          5&nbsp;V |                                              5&nbsp;V |    12.25&nbsp;V |      5.4&nbsp;V |        5.4&nbsp;V |
| B     |                                        3.3&nbsp;V |                                              5&nbsp;V |    12.25&nbsp;V |      5.4&nbsp;V |        3.7&nbsp;V |
| C     |                                        3.3&nbsp;V |                                            3.3&nbsp;V |    12.25&nbsp;V |      3.7&nbsp;V |        3.7&nbsp;V |


PCB Specification
-----------------

I would recommend using the ZIP file from the latest github release, which contains the Gerber and drill files. Use these
parameters for ordering the PCB:

| Parameter         | Value                      |
| ----------------- | -------------------------- |
| Size              | 123&nbsp;x&nbsp;95&nbsp;mm |
| Layers            | 2                          |
| Min hole size     | 0.3&nbsp;mm                |
| Min track/spacing | 6/6&nbsp;mil               |
| Surface finish    | ENIG                       |

The PCB doesn't contain any vias in pads.


PCB population
--------------

There are two ways to populate the PCB:
* Fully populated, including FPGA and FTDI.
  - Programmer doesn't require any additional hardware.
* Partially populated, leaving out the parts inside the dotted area.
  - Programmer needs to be plugged into an
    [iCE40-HX8K Breakout Board](https://www.latticesemi.com/Products/DevelopmentBoardsAndKits/iCE40HX8KBreakoutBoard.aspx).
  - The HX8K Breakout Board needs to be connected via USB to the PC.
  - The programmer PCB needs power over the USB port, but no data connection.

There are some parts that aren't easy to solder by hand, so if you want to safe yourself some trouble, you can get
an HX8K Breakout Board. You don't need a cable to connect the two. Just solder a pin header (J3) at the bottom side
of the programmer PCB, you can plug it directly into the already soldered pin sockets of the HX8K Breakout Board (J2).

IF YOU FULLY POPULATED THE PCB, DO _**NOT**_ CONNECT IT TO AN HX8K BREAKOUT BOARD.

The [BOM](pcb/xc17_prom_prog/bom.ods) contains the parts required to fully populate the PCB. If you plan to only
partially populate it, make sure you don't order the non-required expensive parts like FPGA, FTDI and the LT3030
regulator.


FTDI configuration
------------------

The FTDI chip needs to be configured correctly, otherwise the FPGA can't be programmed. The FTDI configuration is held
in the small EEPROM chip (U4). It is programmed via USB using the FT\_PROG utility that you can download from the
[FTDI website](https://ftdichip.com/utilities/). FT\_PROG requires Windows. It also works from inside a Windows 10 VM
running on VirtualBox.

Program the [template file](ftdi/xc17_prom_prog_ftdi_template.xml) from this repository into the EEPROM. Make sure the
EEPROM is erased first.


Build FPGA bitstream
--------------------

To build the bitstream for the FPGA, you need the following tools:
* [yosys](https://github.com/YosysHQ/yosys)
* [nextpnr](https://github.com/YosysHQ/nextpnr)
* [icestorm](https://github.com/YosysHQ/icestorm)
* Plus all the standard development tools like gnumake, autoconf, automake, m4

On Fedora, you can use the following command line to install everything:

```
dnf install yosys nextpnr icestorm make autoconf automake m4
```

Inside the `hdl` directory of the repository, run the following commands to build the bitstream for a fully populated
PCB:

```
autoreconf -i
./configure --enable-board=xc17_prom_prog
make
```

If you've partially populated the PCB and connected it to the HX8K Breakout Board, then use the following commands
instead:

```
autoreconf -i
./configure --enable-board=hx8k_brk
make
```


Program FPGA bitstream into Flash
---------------------------------

The FPGA bitstream needs to be programmed into the flash chip on the PCB or HX8K Breakout Board via USB.

If you've built the FPGA bitstream yourself, you can run the following command inside the `hdl` directory to program
the bitstream:

```
make prog
```

If you want to program a prebuilt release that you've downloaded from github, use the following command:

```
iceprog xc17_prom_prog.bin
```

I haven't tried it, but it should also be possible to use the
[Lattice iCEcube2 Software](https://www.latticesemi.com/en/Products/DesignSoftwareAndIP/FPGAandLDS/iCEcube2)
to program either the `asc` file or the `bin` file.


Build the command line tool
---------------------------

For building the C# command line tool on Linux, you need the Mono framework and the standard tools gnumake, autoconf,
automake and m4.

For building it on Windows, you need the .NET Framework and some POSIX-like environment for building: Either Cygwin or
Msys2. Gnumake, autoconf, automake and m4 need to be installed in that environment. The C# compiler executable (csc.exe)
must also be callable in your build environment. Make sure it is in `$PATH`.

Run the following commands inside the `csharp` directory of the repository to build the command line tool:

```
autoreconf -i
./configure
make
```

If you want, you can install it with `make install`.

