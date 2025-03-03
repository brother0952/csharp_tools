@echo off
chcp 65001 > nul
setlocal


set MSBUILD="C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set CONFIG=Release

:: 创建临时项目文件
echo ^<?xml version="1.0" encoding="utf-8"?^> > ListPorts.csproj
echo ^<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003"^> >> ListPorts.csproj
echo   ^<PropertyGroup^> >> ListPorts.csproj
echo     ^<OutputType^>Exe^</OutputType^> >> ListPorts.csproj
echo     ^<TargetFramework^>net472^</TargetFramework^> >> ListPorts.csproj
echo     ^<OutputPath^>bin\$(Configuration)\^</OutputPath^> >> ListPorts.csproj
echo     ^<AppendTargetFrameworkToOutputPath^>false^</AppendTargetFrameworkToOutputPath^> >> ListPorts.csproj
echo   ^</PropertyGroup^> >> ListPorts.csproj
echo   ^<ItemGroup^> >> ListPorts.csproj
echo     ^<Reference Include="System" /^> >> ListPorts.csproj
echo     ^<Reference Include="System.Core" /^> >> ListPorts.csproj
echo     ^<Reference Include="System.Management" /^> >> ListPorts.csproj
echo     ^<Compile Include="ListPorts.cs" /^> >> ListPorts.csproj
echo   ^</ItemGroup^> >> ListPorts.csproj
echo   ^<Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" /^> >> ListPorts.csproj
echo ^</Project^> >> ListPorts.csproj

:: 编译项目
%MSBUILD% ListPorts.csproj /p:Configuration=%CONFIG%

:: 检查编译结果
if exist "bin\%CONFIG%\ListPorts.exe" (
    copy /Y "bin\%CONFIG%\ListPorts.exe" "."
    echo 编译成功！
    @REM :: 创建release目录（如果不存在）
    if not exist "release" mkdir release
    
    :: 复制到release目录
    echo 正在复制文件到release目录...
    copy /Y "ListPorts.exe" "release\" > nul
    echo 复制完成！

) else (
    echo 编译失败！
)

:: 清理临时文件
@REM del ListPorts.csproj
@REM rmdir /s /q bin obj

@REM pause 