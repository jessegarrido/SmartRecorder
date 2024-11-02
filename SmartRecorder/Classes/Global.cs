using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using Dropbox.Api;
using Microsoft.Extensions.Configuration;
using PortAudioSharp;

namespace SmartRecorder
{
    public static class Global
    {
        public static int MyState { get; set; } = 0;
        public static string SessionName { get; set; } = DateTime.Today == null ? "UNKNOWN" : DateTime.Today.ToString("yyyy-MM-dd");
        public static string Home { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        public static string LocalRecordingsFolder { get; set; } = Path.Combine(Home, "SmartaCam", "Recordings");
        public static string wavPathAndName { get; set; }
        public static string mp3PathAndName { get; set; }
        public static string lastWavPathAndName { get; set; }
        public static string OS { get; set; }
        public static bool NetworkStatus { get; set; } = true;
        public static bool OAuthStatus { get; set; } = false;
        public static string? RemovableDrivePath { get; set; } 
        public static int RemovableDriveCount { get; set; } = 0;
        public static string DropBoxCodeDotTxtContains { get; set; }
        // public static int Takes { get; set; }
        // public static DateOnly LastTakeDate { get; set; }
        public static List<FileInfo> FilesInDirectory { get; set; } = new DirectoryInfo(LocalRecordingsFolder).GetFiles()
                                                                      .OrderBy(f => f.LastWriteTime)
                                                                     .ToList();
     //   public static DropboxClientConfig dbConfig { get; set; }
    
    }
    public class Config
    {
        //load from App.config
        public static string SSID { get; set; }
        public static string SSIDpw { get; set; }
        public static string DbCode { get; set; }
        public static string DbApiKey { get; set; }
        public static string DbApiSecret { get; set; }
        public static int SampleRate { get; set; }
        public static int SelectedAudioDevice { get; set; }
        public static int RedLED { get; set; } = 14;
        public static int GreenLED { get; set; } = 3;
        public static int YellowLED { get; set; } = 4;
        public static List<string> PingIPList { get; set; } = ["1.1.1.1", "8.8.8.8", "208.67.222.222"]; 

        public static StreamParameters SetAudioParameters()
        {
            StreamParameters param = new StreamParameters();
            DeviceInfo info = PortAudio.GetDeviceInfo(Config.SelectedAudioDevice);
            param.device = Config.SelectedAudioDevice;
            param.channelCount = 2;
            param.sampleFormat = SampleFormat.Float32;
            //param.suggestedLatency = info.defaultLowInputLatency;
            param.suggestedLatency = .9;
            param.hostApiSpecificStreamInfo = nint.Zero;
            return param;
        }
    }
}
