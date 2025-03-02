@echo off
chcp 65001 > nul
setlocal

:: 处理 clean 参数
if "%1"=="clean" (
    echo 清理项目...
    rmdir /s /q bin obj > nul 2>&1
    del /f /q ScreenshotTool.exe > nul 2>&1
    del /f /q *.dll > nul 2>&1
    echo 清理完成！
    exit /b 0
)

:: 查找 MSBuild 路径
for /f "tokens=1,2*" %%a in ('reg query "HKLM\SOFTWARE\Microsoft\MSBuild\ToolsVersions\4.0" /v MSBuildToolsPath') do (
    if "%%a"=="MSBuildToolsPath" (
        set "MSBUILD_PATH=%%c"
    )
)

if not defined MSBUILD_PATH (
    echo 未找到 MSBuild，尝试使用 Visual Studio 2022 路径...
    set "MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
) else (
    set "MSBUILD=%MSBUILD_PATH%MSBuild.exe"
)

if not exist "%MSBUILD%" (
    echo 未找到 MSBuild，尝试使用 BuildTools 路径...
    set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
)

if not exist "%MSBUILD%" (
    echo 错误：未找到 MSBuild！
    echo 请确保已安装 Visual Studio 2022 或 Build Tools 2022
    exit /b 1
)

set CONFIG=Release

:: 还原 NuGet 包
echo 正在还原 NuGet 包...
dotnet restore ScreenshotTool.csproj
if errorlevel 1 (
    echo NuGet 包还原失败！
    exit /b 1
)

:: 编译项目
echo 正在编译项目...
"%MSBUILD%" ScreenshotTool.csproj /p:Configuration=%CONFIG% /v:n

if exist "bin\%CONFIG%\ScreenshotTool.exe" (
    :: 复制主程序和所有依赖项
    copy /Y "bin\%CONFIG%\ScreenshotTool.exe" "." > nul
    copy /Y "bin\%CONFIG%\*.dll" "." > nul
    echo 编译成功！
) else (
    echo 编译失败！
    echo 请检查输出目录：bin\%CONFIG%
    dir bin\%CONFIG%
    exit /b 1
)

rmdir /s /q bin obj > nul 2>&1

@REM pause 