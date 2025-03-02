@echo off
setlocal

set MSBUILD="C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set CONFIG=Release

:: 创建临时项目文件
echo ^<?xml version="1.0" encoding="utf-8"?^> > BluetoothSwitch.csproj
echo ^<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003"^> >> BluetoothSwitch.csproj
echo   ^<PropertyGroup^> >> BluetoothSwitch.csproj
echo     ^<OutputType^>Exe^</OutputType^> >> BluetoothSwitch.csproj
echo     ^<TargetFramework^>net472^</TargetFramework^> >> BluetoothSwitch.csproj
echo     ^<OutputPath^>bin\$(Configuration)\^</OutputPath^> >> BluetoothSwitch.csproj
echo     ^<AppendTargetFrameworkToOutputPath^>false^</AppendTargetFrameworkToOutputPath^> >> BluetoothSwitch.csproj
echo   ^</PropertyGroup^> >> BluetoothSwitch.csproj
echo   ^<ItemGroup^> >> BluetoothSwitch.csproj
echo     ^<Reference Include="System" /^> >> BluetoothSwitch.csproj
echo     ^<Reference Include="System.Core" /^> >> BluetoothSwitch.csproj
echo     ^<Reference Include="System.Management" /^> >> BluetoothSwitch.csproj
echo     ^<Reference Include="System.ServiceProcess" /^> >> BluetoothSwitch.csproj
echo     ^<Compile Include="BluetoothSwitch.cs" /^> >> BluetoothSwitch.csproj
echo   ^</ItemGroup^> >> BluetoothSwitch.csproj
echo   ^<Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" /^> >> BluetoothSwitch.csproj
echo ^</Project^> >> BluetoothSwitch.csproj

:: 编译项目
%MSBUILD% BluetoothSwitch.csproj /p:Configuration=%CONFIG%

:: 检查编译结果
if exist "bin\%CONFIG%\BluetoothSwitch.exe" (
    copy /Y "bin\%CONFIG%\BluetoothSwitch.exe" "."
    echo 编译成功！
) else (
    echo 编译失败！
)

:: 清理临时文件
del BluetoothSwitch.csproj
rmdir /s /q bin obj

@REM pause 