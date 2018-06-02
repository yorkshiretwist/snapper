using snapper.core;
using System;
using System.Configuration;
using System.Threading.Tasks;

namespace snapper.testharness
{
    class Program
    {
        static void Main(string[] args)
        {
            string userName = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            Console.WriteLine($"Username: {userName}");
            Console.WriteLine("Creating config...");
            var config = new ProcessorConfig
            {
                PauseSeconds = int.Parse(ConfigurationManager.AppSettings["PauseSeconds"]),
                RootFolderPath = ConfigurationManager.AppSettings["RootFolderPath"],
                UsernamePattern = ConfigurationManager.AppSettings["UsernamePattern"],
                KeystrokeDelayMilliseconds = int.Parse(ConfigurationManager.AppSettings["KeystrokeDelayMilliseconds"]),
                Debug = true
            };
            Console.WriteLine("Constructing processor...");
            var processor = new Processor();
            Console.WriteLine("Starting processor...");
            Task.Run(() => processor.Start(config));
            Console.WriteLine("Waiting");
            Console.ReadKey();
        }
    }
}
