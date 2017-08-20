using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace HistoryContest.Server
{
    public static class Program
    {
        public static bool FromMain { get; set; } = false;
        public static string EnvironmentName { get; set; } = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        public static string ContentRootPath { get; set; } = EnvironmentName.ToLowerInvariant() == "development" ?
            Directory.GetParent(Directory.GetCurrentDirectory()).FullName : Directory.GetCurrentDirectory();

        public static void Main(string[] args)
        {
            FromMain = true;
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args)
        {
            ProcessArgs(args);
            return new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(ContentRootPath)
                .UseIISIntegration()
                .UseApplicationInsights()
                .UseStartup<Startup>()
                .Build();
        }

        internal static void ProcessArgs(string[] args)
        {
            for (int i = 0; i < args.Length; ++i)
            {
                var arg = args[i];
                switch (arg)
                {
                    case "-rb":
                    case "--runbrowser":
                        string url = @"http://localhost:5000";
                        Console.WriteLine(@"Starting " + url + " with default browser...");
                        System.Diagnostics.Process.Start("explorer", url);
                        break;
                    case "-env":
                    case "--environment":
                        ++i;
                        EnvironmentName = args[i];
                        break;
                    case "-h":
                    case "--help":
                        string[] messages = new string[]
                        {
                            "-h|--help                     ��ʾ����",
                            "-rb|--runbrowser              ��������������Ĭ�����������վ",
                            "-env|--environment <env>      ���ó������л�����Ĭ��Ϊ\"Production\"��"
                        };
                        foreach (var message in messages)
                        {
                            Console.WriteLine(message);
                        }
                        Environment.Exit(0);
                        break;
                }
            }

            switch(EnvironmentName.ToLowerInvariant())
            {
                case "development":
                    ContentRootPath = Directory.GetParent(Directory.GetCurrentDirectory()).FullName;
                    break;
                case "staging":
                case "production":
                    ContentRootPath = Directory.GetCurrentDirectory();
                    break;
                default:
                    throw new ArgumentException("Enviroment name provided invalid. Please choose one in \"Development\", \"Production\", or \"Staging\"");
            }
        }
    }
}
