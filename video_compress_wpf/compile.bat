@echo off
chcp 65001 > nul
setlocal

:: 处理 clean 参数
if "%1"=="clean" (
    echo 清理项目...
    rmdir /s /q bin obj > nul 2>&1
    del /f /q video_compress.exe > nul 2>&1
    del /f /q *.dll > nul 2>&1
    echo 清理完成！
    exit /b 0
)

set MSBUILD="C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set CONFIG=Release

:: 还原 NuGet 包
echo 正在还原 NuGet 包...
dotnet restore video_compress.csproj
if errorlevel 1 (
    echo NuGet 包还原失败！
    exit /b 1
)

:: 编译项目
echo 正在编译项目...
%MSBUILD% video_compress.csproj /p:Configuration=%CONFIG% /v:m

if exist "bin\%CONFIG%\net47\video_compress.exe" (
    :: 复制主程序和所有依赖项
    copy /Y "bin\%CONFIG%\net47\video_compress.exe" "." > nul
    copy /Y "bin\%CONFIG%\net47\*.dll" "." > nul
    echo 编译成功！
    
    :: 创建release目录（如果不存在）
    if not exist "release" mkdir release
    
    :: 复制到release目录
    echo 正在复制文件到release目录...
    copy /Y "video_compress.exe" "release\" > nul
    copy /Y "*.dll" "release\" > nul
    echo 复制完成！
) else (
    echo 编译失败！
    echo 请检查输出目录：bin\%CONFIG%\net47
    dir "bin\%CONFIG%\net47"
    exit /b 1
)

@REM rmdir /s /q bin obj > nul 2>&1

@REM pause 