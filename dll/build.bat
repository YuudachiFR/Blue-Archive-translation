@echo off
setlocal
cd /d "%~dp0"
call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat" >nul || exit /b 1
if not exist build mkdir build
cl /nologo /LD /O2 /MT /EHa /std:c++17 /utf-8 /W3 ^
   /I minhook\include ^
   batrad.cpp minhook\src\buffer.c minhook\src\hook.c minhook\src\trampoline.c minhook\src\hde\hde64.c ^
   /Fo:build\ /Fe:build\XInput1_3.dll ^
   /link /DEF:xinput.def winhttp.lib user32.lib
exit /b %errorlevel%
