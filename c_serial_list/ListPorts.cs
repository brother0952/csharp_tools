using System;
using System.Management;

class Program
{
    static void Main()
    {
        Console.WriteLine("可用的串口列表：");
        Console.WriteLine("----------------");
        
        try
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");
            foreach (ManagementObject port in searcher.Get())
            {
                string name = port["Name"].ToString();
                Console.WriteLine(name);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("获取串口信息时出错：" + ex.Message);
        }
        
        Console.WriteLine("\n按任意键退出...");
        Console.ReadKey();
    }
} 