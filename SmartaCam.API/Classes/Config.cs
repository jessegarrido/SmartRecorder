﻿using Dropbox.Api;
using Microsoft.Extensions.Configuration;
using PortAudioSharp;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartaCam
{
    public class Config
    {
        //load from App.config
        public static string SSID { get; set; }
        public static string SSIDpw { get; set; }
        public static string DbCode { get; set; }
        public static string DbApiKey { get; set; }
        public static string DbApiSecret { get; set; }
        public static int SampleRate { get; set; }
        public static int SelectedAudioDevice { get; set; } = 0;
        public static bool Normalize { get; set; } = true;
        public static bool PushToCloud { get; set; } = false;
        public static bool CopyToUsb { get; set; } = false;
        public static int Mp3BitRate { get; set; } = 192;
        public static string LocalRecordingsFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),"SmartaCam","Recordings");

        public static int RedLED { get; set; } = 4; // purple - pin 7
        public static int GreenLED { get; set; } = 2; //orange - pin 3
        public static int YellowLED { get; set; } = 3; // green - pin 5 
        public static int PlayButton { get; set; } = 14; // grey - pin 8
        public static int RecordButton { get; set; } = 15; // black - pin 10      
        public static int BackButton { get; set; } = 18; // red - pin 12
        public static int ForwardButton { get; set; } = 27; // orange - pin 13
        public static int StopButton { get; set; } = 17;// = brown - pin 11
        public static int FootPedal { get; set; } = 22; // Green, Pin 15
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