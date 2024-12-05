AC_DEFUN([MY_CHECK_WS],
[dnl
if expr index "x$PWD" ' ' >/dev/null 2>&1; then
	AC_MSG_ERROR([whitespaces in builddir are not supported])
fi
if expr index "x$[]0" ' ' >/dev/null 2>&1; then
	AC_MSG_ERROR([whitespaces in srcdir are not supported])
fi
])dnl
dnl
AC_DEFUN([MY_PROG_CSC],
[dnl
# csc/gmcs/mcs C# compiler
AC_ARG_VAR(CSC, [CSharp compiler (overrides auto detection)])
if test -z "$CSC"; then
	AC_PATH_PROG(CSC, csc, [])
	if test -z "$CSC"; then
		AC_PATH_PROG(CSC, gmcs, [])
		if test -z "$CSC"; then
			AC_PATH_PROG(CSC, mcs, [])
			if test -z "$CSC"; then
				AC_MSG_ERROR([csc/gmcs/mcs not found])
			fi
		fi
	fi
fi
AC_MSG_CHECKING([whether the CSharp compiler works])
cat >test_in.cs <<EOF
public static class Program { public static void Main() { } }
EOF
$CSC -target:exe -out:test_out.exe test_in.cs >/dev/null 2>&1
csc_ret=${?}
if test "x$csc_ret" = "x0"; then
	AC_MSG_RESULT(yes)
else
	AC_MSG_RESULT(no)
	rm -f test_out.exe{,.mdb} test_in.cs
	AC_MSG_ERROR(['$CSC -target:exe -out:test_out.exe test_in.cs' failed with exit code $csc_ret])
fi
AC_ARG_VAR(CSFLAGS, [Additional arguments passed to CSharp compiler])

# csc/gmcs/mcs options

# -noconfig option
AC_MSG_CHECKING([whether $CSC accepts -noconfig{+|-}])
$CSC -target:exe -out:test_out.exe -noconfig+ test_in.cs >/dev/null 2>&1
if test "x${?}" = "x0"; then
	AC_MSG_RESULT(yes)
	AC_SUBST(CSC_NO_IMPLICIT_REFS, -noconfig+)dnl
	AC_SUBST(CSC_IMPLICIT_REFS, -noconfig-)dnl
else
	$CSC -target:exe -out:test_out.exe -noconfig test_in.cs >/dev/null 2>&1
	if test "x${?}" = "x0"; then
		AC_MSG_RESULT([no (only -noconfig without + or -)])
		AC_SUBST(CSC_NO_IMPLICIT_REFS, -noconfig)dnl
		AC_SUBST(CSC_IMPLICIT_REFS, [])dnl
	else
		AC_MSG_RESULT(no)
		AC_SUBST(CSC_NO_IMPLICIT_REFS, [])dnl
		AC_SUBST(CSC_IMPLICIT_REFS, [])dnl
	fi
fi
AC_SUBST(CSC_IMPLICIT_REFS_ARG, \$\{CSC_NO_IMPLICIT_REFS\})dnl

# -langversion option
AC_MSG_CHECKING([whether $CSC accepts -langversion:8])
cat >test_in.cs <<EOF
public static class Program { public static void Main() { using var r = new System.BinaryReader(System.Console.In); } }
EOF
$CSC -target:exe -out:test_out.exe -langversion:8 test_in.cs >/dev/null 2>&1
if test "x${?}" = "x0"; then
	AC_MSG_RESULT(yes)
	AC_SUBST(CSC_LANG_ARG, -langversion:8)dnl
else
	$CSC -target:exe -out:test_out.exe -langversion:Default test_in.cs >/dev/null 2>&1
	if test "x${?}" = "x0"; then
		AC_MSG_RESULT([no (trying with -langversion:Default)])
		AC_SUBST(CSC_LANG_ARG, -langversion:Default)dnl
	else
		AC_MSG_RESULT([no (trying without)])
		AC_SUBST(CSC_LANG_ARG, [])dnl
	fi
fi

# -codepage option
AC_MSG_CHECKING([whether $CSC accepts -codepage:utf8])
cat >test_in.cs <<EOF
public static class Program { public static void Main() { } }
EOF
$CSC -target:exe -out:test_out.exe -codepage:utf8 test_in.cs >/dev/null 2>&1
if test "x${?}" = "x0"; then
	AC_MSG_RESULT(yes)
	AC_SUBST(CSC_CODEPAGE_UTF8, -codepage:utf8)dnl
else
	$CSC -target:exe -out:test_out.exe -codepage:65001 test_in.cs >/dev/null 2>&1
	if test "x${?}" = "x0"; then
		AC_MSG_RESULT([no (but -codepage:65001 works)])
		AC_SUBST(CSC_CODEPAGE_UTF8, -codepage:65001)dnl
	else
		AC_MSG_RESULT(no)
		rm -f test_out.exe{,.mdb} test_in.cs
		AC_MSG_ERROR([neither -codepage:utf8 nor -codepage:65001 works])
	fi
fi
AC_SUBST(CSC_CODEPAGE_ARG, \$\{CSC_CODEPAGE_UTF8\})dnl
rm -f test_out.exe{,.mdb} test_in.cs

# -debug option
AC_SUBST(CSC_DEBUG, "-debug+ -define:DEBUG")dnl
AC_SUBST(CSC_NO_DEBUG, -debug-)dnl

# -optimize option
AC_SUBST(CSC_OPT, -optimize+)dnl
AC_SUBST(CSC_NO_OPT, -optimize-)dnl

# -checked option
AC_SUBST(CSC_CHK, -checked+)dnl
AC_SUBST(CSC_NO_CHK, -checked-)dnl

# -unsafe option
AC_SUBST(CSC_UNSAFE, -unsafe+)dnl
AC_SUBST(CSC_NO_UNSAFE, -unsafe-)dnl

# -delaysign option
AC_SUBST(CSC_DELAYSIGN, -delaysign+)dnl
AC_SUBST(CSC_NO_DELAYSIGN, -delaysign-)dnl

# -target option
AC_SUBST(CSC_TARGET_PREFIX, -target:)dnl
AC_SUBST(CSC_CONSOLE_TARGET, exe)dnl
AC_SUBST(CSC_WINDOWS_TARGET, winexe)dnl
AC_SUBST(CSC_LIBRARY_TARGET, library)dnl

# other options
AC_SUBST(CSC_WARN_ARG, -warn:4)dnl
AC_SUBST(gen_CSC_OUT_ARG, -out:\'\$[]1\')dnl
AC_SUBST(gen_CSC_SNK_ARG, -keyfile:\'\$[]1\')dnl

csc_version="$($CSC --version 2>/dev/null)"
csc_version_ret=${?}
if test "x$csc_version_ret" = "x0" &&
   echo $csc_version | grep -q Mono; then
	AC_SUBST(CSC_RUNTIME, mono)dnl
else
	if $CSC 2>&1 | grep -q Microsoft; then
		AC_SUBST(CSC_RUNTIME, msnet)dnl
	else
		AC_SUBST(CSC_RUNTIME, unknown)dnl
	fi
fi
])dnl
dnl
AC_DEFUN([MY_CSC_CHECK_ARGS],
[dnl
if test "x$1" = "xno"; then
	AC_SUBST(CSC_DEBUG_ARG, \$\{CSC_NO_DEBUG\})dnl
	AC_SUBST(CSC_OPT_ARG, \$\{CSC_OPT\})dnl
else
	AC_SUBST(CSC_DEBUG_ARG, \$\{CSC_DEBUG\})dnl
	AC_SUBST(CSC_OPT_ARG, \$\{CSC_NO_OPT\})dnl
fi

AC_SUBST(CSC_COMMON_ARGS,
"\${CSC_WARN_ARG}dnl
 \${CSC_LANG_ARG}dnl
 \${CSC_CODEPAGE_ARG}dnl
 \${CSC_IMPLICIT_REFS_ARG}dnl
 \${CSC_DEBUG_ARG}dnl
 \${CSC_OPT_ARG}")dnl
AC_SUBST(CSC_ALL_ARGS,
"\${CSFLAGS}dnl
 \${CSC_COMMON_ARGS}dnl
 \${CSC_ARGS}")dnl
csc_args_eval="$CSFLAGS $(eval eval echo $CSC_COMMON_ARGS)"

AC_MSG_CHECKING([whether $CSC accepts the common arguments])
cat >test_in.cs <<EOF
public static class Program { public static void Main() { } }
EOF
$CSC -target:exe -out:test_out.exe $csc_args_eval test_in.cs >/dev/null 2>&1
csc_ret=${?}
rm -f test_out.exe{,.mdb} test_in.cs
if test "x$csc_ret" = "x0"; then
	AC_MSG_RESULT(yes)
else
	AC_MSG_RESULT(no)
	AC_MSG_ERROR(['$CSC -target:exe -out:test_out.exe $csc_args_eval test_in.cs' failed with exit code $csc_ret])
fi
])dnl
dnl
AC_DEFUN([MY_PROG_NCC],
[dnl
# Nemerle compiler
AC_ARG_VAR(NCC, [Nemerle compiler (overrides auto detection)])
if test -z "$NCC"; then
	AC_PATH_PROG(NCC, ncc, [])
	if test -z "$NCC"; then
		AC_MSG_ERROR([ncc not found])
	fi
fi
AC_MSG_CHECKING([whether the Nemerle compiler works])
cat >test_in.n <<EOF
public module Program { public static Main() : void { } }
EOF
$NCC -target:exe -out:test_out.exe test_in.n >/dev/null 2>&1
ncc_ret=${?}
if test "x$ncc_ret" = "x0"; then
	AC_MSG_RESULT(yes)
else
	AC_MSG_RESULT(no)
dnl	rm -f test_out.exe{,.mdb} test_in.n
	AC_MSG_ERROR(['$NCC -target:exe -out:test_out.exe test_in.n' failed with exit code $ncc_ret])
fi
AC_ARG_VAR(NFLAGS, [Additional arguments passed to Nemerle compiler])

# ncc options

# -progress-bar option
AC_MSG_CHECKING([whether $NCC accepts -progress-bar{+|-}])
$NCC -target:exe -out:test_out.exe -progress-bar+ test_in.n >/dev/null 2>&1
if test "x${?}" = "x0"; then
	AC_MSG_RESULT(yes)
	AC_SUBST(NCC_BAR, -progress-bar+)dnl
	AC_SUBST(NCC_NO_BAR, -progress-bar-)dnl
else
	$NCC -target:exe -out:test_out.exe -bar test_in.n >/dev/null 2>&1
	if test "x${?}" = "x0"; then
		AC_MSG_RESULT([no (using -bar instead)])
		AC_SUBST(NCC_BAR, -bar)dnl
		AC_SUBST(NCC_NO_BAR, [])dnl
	else
		AC_MSG_RESULT(no)
		AC_SUBST(NCC_BAR, [])dnl
		AC_SUBST(NCC_NO_BAR, [])dnl
	fi
fi
rm -f test_out.exe{,.mdb} test_in.n

# -debug option
AC_SUBST(NCC_DEBUG, "-debug+ -define:DEBUG")dnl
AC_SUBST(NCC_NO_DEBUG, -debug-)dnl

# -optimize option
AC_SUBST(NCC_OPT, -optimize)dnl
AC_SUBST(NCC_NO_OPT, [])dnl

# -checked option
AC_SUBST(NCC_CHK, -checked+)dnl
AC_SUBST(NCC_NO_CHK, -checked-)dnl

# -target option
AC_SUBST(NCC_TARGET_PREFIX, -target:)dnl
AC_SUBST(NCC_CONSOLE_TARGET, exe)dnl
AC_SUBST(NCC_WINDOWS_TARGET, winexe)dnl
AC_SUBST(NCC_LIBRARY_TARGET, library)dnl

# other options
AC_SUBST(NCC_WARN_ARG, -warn:5)dnl
AC_SUBST(gen_NCC_OUT_ARG, -out:\'\$[]1\')dnl
])dnl
dnl
AC_DEFUN([MY_NCC_CHECK_ARGS],
[dnl
if test "x$1" = "xno"; then
	AC_SUBST(NCC_DEBUG_ARG, \$\{NCC_NO_DEBUG\})dnl
	AC_SUBST(NCC_OPT_ARG, \$\{NCC_OPT\})dnl
else
	AC_SUBST(NCC_DEBUG_ARG, \$\{NCC_DEBUG\})dnl
	AC_SUBST(NCC_OPT_ARG, \$\{NCC_NO_OPT\})dnl
fi
if test "x$2" = "xno"; then
	AC_SUBST(NCC_BAR_ARG, \$\{NCC_NO_BAR\})dnl
else
	AC_SUBST(NCC_BAR_ARG, \$\{NCC_BAR\})dnl
fi

AC_SUBST(NCC_COMMON_ARGS,
"\${NCC_WARN_ARG}dnl
 \${NCC_DEBUG_ARG}dnl
 \${NCC_OPT_ARG}dnl
 \${NCC_BAR_ARG}")dnl
AC_SUBST(NCC_ALL_ARGS,
"\${NFLAGS}dnl
 \${NCC_COMMON_ARGS}dnl
 \${NCC_ARGS}")dnl
ncc_args_eval="$NFLAGS $(eval eval echo $NCC_COMMON_ARGS)"

AC_MSG_CHECKING([whether $NCC accepts the common arguments])
cat >test_in.n <<EOF
public module Program { public static Main() : void { } }
EOF
$NCC -target:exe -out:test_out.exe $ncc_args_eval test_in.n >/dev/null 2>&1
ncc_ret=${?}
rm -f test_out.exe{,.mdb} test_in.n
if test "x$ncc_ret" = "x0"; then
	AC_MSG_RESULT(yes)
else
	AC_MSG_RESULT(no)
	AC_MSG_ERROR(['$NCC -target:exe -out:test_out.exe $ncc_args_eval test_in.n' failed with exit code $ncc_ret])
fi
])dnl
dnl
AC_DEFUN([MY_ARG_ENABLE],
[dnl
m4_do(
	[AC_ARG_ENABLE([$1],
		[AS_HELP_STRING(
			[--m4_if([$2<m4_unquote(m4_normalize([$4]))>], yes<>, dis, en)able-$1[]m4_case(
				m4_join(|, m4_unquote(m4_normalize([$4]))),
				yes|no, [],
				no|yes, [],
				yes,    [],
				no,     [],
				[],     [],
				        [@<:@=ARG@:>@]
			)],
			[$3 ]m4_case(
				m4_join(|, m4_unquote(m4_normalize([$4]))),
				yes|no, [[ ]],
				no|yes, [[ ]],
				yes,    [[ ]],
				no,     [[ ]],
				[],     [[ ]],
				        [[ARG is one of: ]m4_normalize([$4])[ ]]
			)m4_if(
				[$2<m4_unquote(m4_normalize([$4]))>],
				yes<>,
				[(enabled by default)],
				[(default is $2)]
			))],
		[enable_[]m4_translit([$1], -, _)_expl=yes],
		[enable_[]m4_translit([$1], -, _)=$2 enable_[]m4_translit([$1], -, _)_expl=""])],
	[AS_CASE(
		["$enable_[]m4_translit([$1], -, _)"],
		[m4_join(|, m4_ifval(m4_normalize([$4]), m4_normalize([$4]), [yes, no]))],
		[# ok]m4_newline,
		[AC_MSG_ERROR([the value '$enable_[]m4_translit([$1], -, _)' is invalid for --enable-$1])]
	)])]
)dnl
dnl
AC_DEFUN([MY_ARG_WITH],
[dnl
m4_do(
	[AC_ARG_WITH([$1],
		[AS_HELP_STRING(
			[--with[]m4_if([$2], yes, out, [])-$1[]m4_case(
				m4_unquote(m4_normalize([$4])),
				[],     [],
				        [@<:@=ARG@:>@]
			)],
			[$3 ]m4_case(
				m4_unquote(m4_normalize([$4])),
				[],     [[ ]],
				        [[ARG is ]m4_normalize([$4])[ ]]
			)[(default is $2)])],
		[with_[]m4_translit([$1], -, _)_expl=yes],
		[with_[]m4_translit([$1], -, _)="$2" with_[]m4_translit([$1], -, _)_expl=""])],
	[m4_if(dnl  Replace "yes" with the default value, but only if default is not "no":
		[$2],
		no,
		[],
		[AS_CASE(
			["$with_[]m4_translit([$1], -, _)"],
			yes,
			[with_[]m4_translit([$1], -, _)="$2"]
		)]
	)])]
)dnl
dnl
AC_DEFUN([MY_PROG_MDOC],
[dnl
# mdoc
AC_ARG_VAR(MDOC, [Mono documentation management tool (overrides auto detection)])
if test -z "$MDOC"; then
	AC_PATH_PROG(MDOC, mdoc, [])
	var_mdoc_expl=""
else
	var_mdoc_expl=yes
fi
if test -n "$MDOC"; then
	AC_MSG_CHECKING([whether mdoc works])
	$MDOC help >/dev/null 2>&1
	mdoc_ret=${?}
	if test "x$mdoc_ret" = "x0"; then
		AC_MSG_RESULT(yes)
	else
		AC_MSG_RESULT(no)
		if test -n "$var_mdoc_expl" || test -n "$1"; then
			AC_MSG_ERROR(['$MDOC help' failed with exit code $mdoc_ret])
		else
			AC_MSG_WARN(['$MDOC help' failed with exit code $mdoc_ret])
			MDOC=""
		fi
	fi
fi
])dnl
AC_DEFUN([MY_PROG_MONODOC],
[dnl
# monodoc
AC_ARG_VAR(MONODOC_SOURCES_DIR, [Monodoc sources directory (overrides auto detection)])
if test -z "$MONODOC_SOURCES_DIR"; then
	AC_MSG_CHECKING([for monodoc sources directory])
	MONODOC_SOURCES_DIR="$("$PKG_CONFIG" --variable=sourcesdir monodoc)"
	pkgcfg_ret=${?}
	if test "x$pkgcfg_ret" = "x0" && test -n "$MONODOC_SOURCES_DIR"; then
		AC_MSG_RESULT($MONODOC_SOURCES_DIR)
	else
		AC_MSG_RESULT([not found])
	fi
	var_monodoc_sources_dir_expl=""
else
	var_monodoc_sources_dir_expl=yes
fi
])dnl
AC_DEFUN([MY_PROG_GAC_UTIL],
[dnl
# gacutil
AC_ARG_VAR(GAC_UTIL, [Global Assembly Cache management utility (overrides auto detection)])
if test -z "$GAC_UTIL"; then
	AC_PATH_PROG(GAC_UTIL, gacutil, [])
	var_gacutil_expl=""
else
	var_gacutil_expl=yes
fi
if test -n "$GAC_UTIL"; then
	AC_MSG_CHECKING([whether gacutil works])
	$GAC_UTIL -l no_result >/dev/null 2>&1
	gacutil_ret=${?}
	if test "x$gacutil_ret" = "x0"; then
		AC_MSG_RESULT(yes)
	else
		AC_MSG_RESULT(no)
		if test -n "$var_gacutil_expl" || test -n "$1"; then
			AC_MSG_ERROR(['$GAC_UTIL -l no_result' failed with exit code $gacutil_ret])
		else
			AC_MSG_WARN(['$GAC_UTIL -l no_result' failed with exit code $gacutil_ret])
			GAC_UTIL=""
		fi
	fi
fi
])dnl
