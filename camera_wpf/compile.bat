@echo off
chcp 65001 > nul
setlocal

:: 处理 clean 参数
if "%1"=="clean" (
    echo 清理项目...
    rmdir /s /q bin obj > nul 2>&1
    del /f /q CameraTool.exe > nul 2>&1
    del /f /q *.dll > nul 2>&1
    echo 清理完成！
    exit /b 0
)

set MSBUILD="C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set CONFIG=Release

:: 还原 NuGet 包
echo 正在还原 NuGet 包...
dotnet restore CameraTool.csproj
if errorlevel 1 (
    echo NuGet 包还原失败！
    exit /b 1
)

:: 编译项目
echo 正在编译项目...
%MSBUILD% CameraTool.csproj /p:Configuration=%CONFIG% /v:n

if exist "bin\%CONFIG%\net47\CameraTool.exe" (
    :: 复制主程序和所有依赖项
    copy /Y "bin\%CONFIG%\net47\CameraTool.exe" "." > nul
    copy /Y "bin\%CONFIG%\net47\*.dll" "." > nul
    echo 编译成功！
) else (
    echo 编译失败！
    echo 请检查输出目录：bin\%CONFIG%
    dir bin\%CONFIG%
    exit /b 1
)

rmdir /s /q bin obj > nul 2>&1

@REM pause 