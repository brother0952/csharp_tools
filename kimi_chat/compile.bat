@echo off
setlocal

set MSBUILD="C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set CONFIG=Release

:: 还原 NuGet 包
echo 正在还原 NuGet 包...
dotnet restore KimiChat.csproj
if errorlevel 1 (
    echo NuGet 包还原失败！
    exit /b 1
)

:: 编译项目
echo 正在编译项目...
%MSBUILD% KimiChat.csproj /p:Configuration=%CONFIG%

if exist "bin\%CONFIG%\net471\KimiChat.exe" (
    :: 复制主程序和所有依赖项
    copy /Y "bin\%CONFIG%\net471\KimiChat.exe" "."
    copy /Y "bin\%CONFIG%\net471\Newtonsoft.Json.dll" "."
    copy /Y "bin\%CONFIG%\net471\*.dll" "."
    echo 编译成功！
) else (
    echo 编译失败！
)

rmdir /s /q bin obj

@REM pause 