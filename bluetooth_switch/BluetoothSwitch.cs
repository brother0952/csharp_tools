using System;
using System.Management;
using System.Diagnostics;
using System.Threading;
using System.Security.Principal;
using System.Linq;

class Program
{
    static void Main()
    {
        // 检查管理员权限
        if (!IsAdministrator())
        {
            // 以管理员权限重启程序
            var startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = true;
            startInfo.WorkingDirectory = Environment.CurrentDirectory;
            startInfo.FileName = Process.GetCurrentProcess().MainModule.FileName;
            startInfo.Verb = "runas";

            try
            {
                Process.Start(startInfo);
                return;
            }
            catch
            {
                Console.WriteLine("需要管理员权限才能操作蓝牙设备");
                Console.WriteLine("\n按任意键退出...");
                Console.ReadKey();
                return;
            }
        }

        try
        {
            // 获取蓝牙服务名称
            string btServiceName = GetBluetoothServiceName();
            if (string.IsNullOrEmpty(btServiceName))
            {
                Console.WriteLine("未找到蓝牙服务");
                return;
            }

            // 获取蓝牙状态
            bool isEnabled = IsBluetoothEnabled();
            Console.WriteLine($"当前蓝牙状态: {(isEnabled ? "已启用" : "已禁用")}");

            try
            {
                if (isEnabled)
                {
                    // 停止蓝牙服务
                    RunCommand("sc", $"stop {btServiceName}");
                    Thread.Sleep(1000);
                    RunCommand("sc", "stop bthserv");
                    Console.WriteLine("蓝牙已禁用");
                }
                else
                {
                    // 启动蓝牙服务
                    RunCommand("sc", "start bthserv");
                    Thread.Sleep(1000);
                    RunCommand("sc", $"start {btServiceName}");
                    Console.WriteLine("蓝牙已启用");
                }

                // 等待状态变化
                Thread.Sleep(1000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"切换状态失败: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"操作蓝牙时出错: {ex.Message}");
        }

        Console.WriteLine("\n按任意键退出...");
        Console.ReadKey();
    }

    static string GetBluetoothServiceName()
    {
        var process = new Process();
        process.StartInfo.FileName = "sc";
        process.StartInfo.Arguments = "query type= service state= all";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.CreateNoWindow = true;
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        // 查找蓝牙用户支持服务
        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.Contains("Bluetooth") && line.Contains("User"))
            {
                return line.Split(':')[1].Trim();
            }
        }

        return null;
    }

    static void RunCommand(string command, string arguments)
    {
        var process = new Process();
        process.StartInfo.FileName = command;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;
        process.Start();

        if (!process.WaitForExit(5000)) // 5秒超时
        {
            process.Kill();
            throw new Exception("命令执行超时");
        }

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        if (!string.IsNullOrEmpty(error))
        {
            throw new Exception(error);
        }

        if (output.Contains("FAILED"))
        {
            throw new Exception(output);
        }
    }

    static bool IsAdministrator()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    static bool IsBluetoothEnabled()
    {
        try
        {
            var process = new Process();
            process.StartInfo.FileName = "sc";
            process.StartInfo.Arguments = "query bthserv";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output.Contains("RUNNING");
        }
        catch
        {
            Console.WriteLine("无法获取蓝牙状态，假设已启用");
            return true;
        }
    }
} 