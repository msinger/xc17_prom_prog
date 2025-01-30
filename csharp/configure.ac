AC_PREREQ(2.63)
AC_INIT(xc17_prom_prog, 1.1)
AC_CONFIG_SRCDIR([xc17_prom_prog/Program.cs])
AC_CONFIG_MACRO_DIR([m4])

AM_INIT_AUTOMAKE([foreign dist-bzip2 filename-length-max=99 no-dependencies tar-ustar])
AM_MAINTAINER_MODE

MY_CHECK_WS

# pkg-config
AC_PATH_PROG(PKG_CONFIG, pkg-config, no)
if test "x$PKG_CONFIG" = "xno"; then
	AC_MSG_ERROR([You need to install pkg-config])
fi

# tr
AC_PATH_PROG(TR, tr, no)
if test "x$TR" = "xno"; then
	AC_MSG_ERROR([You need to install tr (coreutils)])
fi

AC_PROG_INSTALL

# csc/gmcs/mcs C# compiler
MY_PROG_CSC

# check if we are using mono >= 2.0.1
if test "x$CSC_RUNTIME" = "xmono"; then
	AC_MSG_CHECKING([for mono $(basename $CSC) >= 2.0.1])
	csc_ver="$($CSC --version | sed -e 's/.*@<:@vV@:>@ersion \(@<:@0-9@:>@*\.@<:@0-9@:>@*\(\.@<:@0-9@:>@*\)\?\).*/\1/g')"
	csc_ver_maj=$(echo $csc_ver | cut -d . -f 1)
	csc_ver_min=$(echo $csc_ver | cut -d . -f 2)
	csc_ver_rev=$(echo $csc_ver | cut -d . -f 3)
	if test "$csc_ver_maj" -lt 2 ||
	   test "x$csc_ver_maj" = "x2" -a "x$csc_ver_min" = "x0" -a "$csc_ver_rev" -lt 1; then
		AC_MSG_RESULT([no ($csc_ver)])
		AC_MSG_ERROR([mono < 2.0.1 is not supported])
	else
		AC_MSG_RESULT([yes ($csc_ver)])
	fi
fi


AM_CONDITIONAL(ENABLE_DOC, [false])
AM_CONDITIONAL(ENABLE_UPDATE_DOCSRC, [false])


MY_ARG_ENABLE(
	debug,
	no,
	[Compile with debugging flags.]
)

AM_CONDITIONAL(ENABLE_DEBUG, [test "x$enable_debug" != "xno"])
AM_CONDITIONAL(ENABLE_RELEASE, [test "x$enable_debug" = "xno"])

MY_CSC_CHECK_ARGS([$enable_debug])


MY_ARG_ENABLE(
	gac,
	no,
	[Install assemblies into global assembly cache.]
)

AM_CONDITIONAL(ENABLE_GAC, [test "x$enable_gac" != "xno"])

# gactuil
need_gacutil=""
if test "x$enable_gac" != "xno"; then
	need_gacutil=yes
fi

MY_PROG_GAC_UTIL($need_gacutil)


MY_ARG_WITH(
	snk,
	[${srcdir}/snk/xc17_prom_prog.snk],
	[Sign the assemblies with given key.],
	[path to snk file]
)

if test "x$with_snk" != "xno" && ! test -f "$with_snk"; then
	AC_MSG_ERROR([$with_snk not found])
fi

AC_SUBST(SNK_FILE, $(test "x$with_snk" != "xno" && cd $(dirname $with_snk) && echo $(pwd)/$(basename $with_snk)))
AC_SUBST(SNK_SIGN_BOOL, $(test "x$with_snk" != "xno" && echo true || echo false))

if test "x$enable_gac" != "xno" && test "x$with_snk" = "xno"; then
	AC_MSG_ERROR([assemblies need a strongname to be installed into gac. (do not use --enable-gac and --without-snk at the same time.)])
fi


AC_SUBST(XC17_PROM_PROG_VERSION, $VERSION.0.0)


AC_CONFIG_FILES([
xc17_prom_prog/Properties/AssemblyInfo.cs
xc17_prom_prog/xc17_prom_prog
xc17_prom_prog/Makefile
Makefile
])

AC_OUTPUT

echo
echo
echo "=== CONFIGURATION SUMMARY ==="
echo
echo "C# compiler:                        $CSC"
echo "Gacutil:                            $GAC_UTIL"
echo "C# compiler arguments:              $csc_args_eval"
echo "Debugging enabled:                  $enable_debug"
echo "Sign assemblies with snk:           $with_snk"
echo "Install into GAC:                   $enable_gac"
