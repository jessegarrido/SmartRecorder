using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace SmartRecorder
{
    internal class Program
    {

        public static IServiceProvider CreateServiceCollection() 
        {
            var services = new ServiceCollection();
            services.AddTransient<IAudioRepository, AudioRepository>();
            services.AddTransient<INetworkRepository, NetworkRepository>();
            services.AddTransient<IUIRepository, UIRepository>();
            services.AddTransient<IIORepository, IORepository>();
            return services.BuildServiceProvider();
        }

        static async Task Main(string[] args)
        {
            IServiceProvider services = CreateServiceCollection();

            UIRepository uiRepository = new();
            AudioRepository audioRepository = new();
            NetworkRepository networkRepository = new();
           // IORepository ioRepository = new();
            NetworkRepository.DropBox db = new();
            // db.DropBoxAuthResetAsync();
            uiRepository.ClearDailyTakesCount();
            uiRepository.LoadConfig();
            Console.WriteLine("Welcome to SmartaCam");
            Global.OS = await uiRepository.IdentifyOS();
            Console.WriteLine($"Platform: {Global.OS}");
            Console.WriteLine($"Session Name: {Global.SessionName}");
            Console.WriteLine($"User's Home Folder: {Global.Home}");
            await uiRepository.AskKeepOrEraseFilesAsync();
            networkRepository.CheckNetworkAsync();
            Console.WriteLine($"Network Connected Status: {Global.NetworkStatus}");
            audioRepository.AudioDeviceInitAndEnumerate(false);
            Config.SelectedAudioDevice = Global.OS.Contains("Raspberry") ? 2 : 0;
            uiRepository.FindRemovableDrives(true);
            Global.MyState = 1;
            do
            {
                await uiRepository.MainMenuAsync();
            }
            while (true);
        }

    }
}
