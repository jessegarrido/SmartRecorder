﻿using System.Runtime.InteropServices;
using System.Text;
using System.Net.NetworkInformation;
using PortAudioSharp;
using NAudio.Wave;
using NAudio.Lame;
using Wifi.Linux;
using System.Device.Gpio;
using SimpleWifi;
using System.Data;
using Dropbox.Api.Files;
using Dropbox.Api;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using System.Diagnostics;


namespace SmartRecorder
{
    public interface IAudioRepository
    {
        public void AudioDeviceInitAndEnumerate(bool enumerate);
        public int QueryAudioDevice(int? configSelectedIndex);
        public Task RecordAudioAsync();
        public void ConvertWavToMP3(string waveFileName, string mp3FileName, int bitRate);
        public void LoadLameDLL();
        public Task NormalizeTakeAsync();
    }
    public interface INetworkRepository
    {
        public Task CheckNetworkAsync();
        public Task CheckAndConnectCloudAsync();
        public Task EstablishWifiAsync();
        public class DropBox();
    }
    public interface IUIRepository
    {
        public void ClearDailyTakesCount();
        public Task<int> AskKeepOrEraseFilesAsync();
        public Task<string> IdentifyOS();
        public Task MainMenuAsync();
        public void SetupLocalFileAndLocation();
        public int FindRemovableDrives(bool displayDetails);
        public int GetValidUserSelection(List<int> validOptions);
        public void LoadConfig();
        public Task<string> RunBashCatAsync(string command);

    }
    public interface IIORepository
    {
        public string GetUSBDeviceName(nint name);
        public Task LEDBlinkAsync(int pin, int duration);
        public Task LongBlinkLEDAsync(int pin, int duration, CancellationToken ct);
        public void BlinkLED(int pin, int duration);
        public Task TurnOffLEDAsync(int pin);
        public Task TurnOnLEDAsync(int pin);
        public Task BlinkAllLEDs(CancellationToken ct);
        public Task BlinkOneLED(int pin, int duration, CancellationToken ct);
    }

    public class AudioRepository : IAudioRepository
    {
        public void AudioDeviceInitAndEnumerate(bool enumerate)
        {
            PortAudio.LoadNativeLibrary();
            PortAudio.Initialize();
            Console.WriteLine(PortAudio.VersionInfo.versionText);
            Console.WriteLine($"Number of audio devices: {PortAudio.DeviceCount}");
            if (enumerate == true)
            {
                for (int i = 0; i != PortAudio.DeviceCount; ++i)
                {
                    Console.WriteLine($" Device {i}");
                    DeviceInfo deviceInfo = PortAudio.GetDeviceInfo(i);
                    Console.WriteLine($"   Name: {deviceInfo.name}");
                    Console.WriteLine($"   Max input channels: {deviceInfo.maxInputChannels}");
                    Console.WriteLine($"   Default sample rate: {deviceInfo.defaultSampleRate}");
                }
            }
        }
        public int QueryAudioDevice(int? configSelectedIndex)
        {
            int deviceIndex;
            deviceIndex = configSelectedIndex == null ? PortAudio.DefaultInputDevice : Config.SelectedAudioDevice;

            if (deviceIndex == PortAudio.NoDevice)
            {
                Console.WriteLine("No default input device found");
                Environment.Exit(1);
            }
            DeviceInfo info = PortAudio.GetDeviceInfo(deviceIndex);
            Console.WriteLine();
            Console.WriteLine($"Initializing audio device {deviceIndex}: ({info.name})");
            Config.SelectedAudioDevice = deviceIndex;
            return deviceIndex;
        }
        public async Task RecordAudioAsync()
        {
            // using var tokenSource = new CancellationTokenSource();
            // var token = tokenSource.Token;
            //  IORepository ioRepository = new();
            UIRepository uiRepository = new();
            IORepository ioRepository = new();
            if (Global.OS == "Raspberry Pi") { ioRepository.TurnOnLEDAsync(Config.RedLED); };
            Global.MyState = 2; // TODO generic update curent state function
            uiRepository.SetupLocalFileAndLocation();
            DeviceInfo info = PortAudio.GetDeviceInfo(Config.SelectedAudioDevice);
            Console.WriteLine();
            Console.WriteLine($"Use default device {Config.SelectedAudioDevice} ({info.name})");
            StreamParameters param = Config.SetAudioParameters();
            int numChannels = param.channelCount;

            //FileStream f = new FileStream(wavPathAndName, FileMode.Create);
            // using (Float32WavWriter wr = new Float32WavWriter(Global.wavPathAndName, Config.SampleRate, numChannels))
            WaveFormat wavformat = new WaveFormat(Config.SampleRate, 2);
            using (WaveFileWriter wr = new WaveFileWriter(Global.wavPathAndName, wavformat))
            {
                PortAudioSharp.Stream.Callback callback = (nint input, nint output,
                    uint frameCount,
                    ref StreamCallbackTimeInfo timeInfo,
                    StreamCallbackFlags statusFlags,
                    nint userData
                    ) =>
                {
                    frameCount = frameCount * (uint)numChannels;
                    float[] samples = new float[frameCount];
                    Marshal.Copy(input, samples, 0, (int)frameCount);
                    wr.WriteSamples(samples, 0, (int)frameCount);
                    return StreamCallbackResult.Continue;
                };

                Console.WriteLine(param);
                Console.WriteLine(Config.SampleRate);
                Console.WriteLine("Now Recording");

                PortAudioSharp.Stream stream = new PortAudioSharp.Stream(inParams: param, outParams: null, sampleRate: Config.SampleRate,
                    framesPerBuffer: 0,
                    streamFlags: StreamFlags.ClipOff,
                    callback: callback,
                    userData: nint.Zero
                    );
                {
                    stream.Start();
                    do
                    {
                        Thread.Sleep(500);
                    } while (Global.MyState == 2);
                    stream.Stop();
                    if (Global.OS == "Raspberry Pi") { ioRepository.TurnOffLEDAsync(Config.RedLED); };
                    Console.WriteLine("Recording Stopped.");
                    Settings.Default.Takes++;
                    Settings.Default.LastTakeDate = DateTime.Today;
                    Settings.Default.Save();
                    //Global.FilesInDirectory = new DirectoryInfo(Global.LocalRecordingsFolder).GetFiles()
                    //                                                  .OrderBy(f => f.LastWriteTime)
                    //                                                 .ToList();
                };
            }
        }
        public void ConvertWavToMP3(string waveFileName, string mp3FileName, int bitRate = 192)
        {
            Console.WriteLine($"Converting {Global.wavPathAndName} to mp3 file");
            LoadLameDLL();
            Thread.Sleep(1000);
            using (var reader = new AudioFileReader(waveFileName))
            using (var writer = new LameMP3FileWriter(mp3FileName, reader.WaveFormat, bitRate))
                reader.CopyTo(writer);
            Console.WriteLine($"{Global.mp3PathAndName} was created.");
        }
        public void LoadLameDLL()
        {
            LameDLL.LoadNativeDLL(Path.Combine(AppDomain.CurrentDomain.BaseDirectory));
        }
        public async Task NormalizeTakeAsync()
        // from https://markheath.net/post/normalize-audio-naudio
        {
            UIRepository uiRepository = new();
            Console.WriteLine("Normalizing");
            var inPath = Global.wavPathAndName;
            var outPath = $"{Global.wavPathAndName}_normalized.wav";
            float max = 0;

            while (!inPath.IsFileReady())
                {
                Task.Delay(1000);
            }
            using (var reader = new AudioFileReader(inPath))
            {
                // find the max peak
                float[] buffer = new float[reader.WaveFormat.SampleRate];
                int read;
                do
                {
                    read = reader.Read(buffer, 0, buffer.Length);
                    for (int n = 0; n < read; n++)
                    {
                        var abs = Math.Abs(buffer[n]);
                        if (abs > max) max = abs;
                    }
                } while (read > 0);
                Console.WriteLine($"Max sample value: {max}");

                if (max == 0 || max > 1.0f)
                    throw new InvalidOperationException("File cannot be normalized");

                // rewind and amplify
                reader.Position = 0;
                reader.Volume = 1.0f / max;

                // write out to a new WAV file
                //  WaveFileWriter.CreateWaveFile16(outPath, reader);
                WaveFileWriter.CreateWaveFile16(outPath, reader);

            }
            File.Move($"{Global.wavPathAndName}_normalized.wav", Global.wavPathAndName, true);

        }
        public async Task PlaybackAudioAsync()
        {
            //playback doesn't work yet, add database first
            using var tokenSource = new CancellationTokenSource();
            var pauseToken = tokenSource.Token;
            IORepository ioRepository = new();
            if (Global.OS == "Linux") { ioRepository.TurnOnLEDAsync(Config.GreenLED); };
            WaveStream mainOutputStream = new WaveFileReader(Global.lastWavPathAndName);
            WaveChannel32 volumeStream = new WaveChannel32(mainOutputStream);
            WaveOutEvent player = new WaveOutEvent();
            player.Init(volumeStream);
            player.Play();
            if (pauseToken.IsCancellationRequested) { player.Pause(); }
            if (Global.OS == "Raspberry Pi") { ioRepository.TurnOffLEDAsync(Config.GreenLED); };
        }
    }
    public class NetworkRepository : INetworkRepository
    {
        public async Task CheckNetworkAsync()
        {
            IORepository ioRepository = new();
            var pingTask = Task.Run(async () =>
            {
                Ping pingSender = new();
                var pingSubTasks = Config.PingIPList.Select(ip => new Ping().SendPingAsync(ip, 5000));
                try
                {
                    var results = await Task.WhenAll(pingSubTasks);
                    int succesfulPingCount = 0;
                    foreach (var result in results)
                    {
                        if (result.Status == IPStatus.Success)
                        {
                            succesfulPingCount++;
                        }
                    }
                    Global.NetworkStatus = succesfulPingCount > 0 ? true : false;
                }
                catch (PingException ex)
                {
                    // ConnectionStatus = "Disconnected-Exception";
                }
                if (Global.OS == "Raspberry Pi")
                {
                    if (Global.NetworkStatus)
                    {
                        await ioRepository.TurnOnLEDAsync(Config.YellowLED);
                    }
                    else
                    {
                        await ioRepository.TurnOffLEDAsync(Config.YellowLED);
                    }
                }

                if (!Global.NetworkStatus) { await EstablishWifiAsync(); }
            });
            await pingTask;
            CheckAndConnectCloudAsync();
            int pingWaitTime = Global.NetworkStatus ? 300000 : 60000;
            await Task.Delay(pingWaitTime);
            CheckNetworkAsync();
        }
        public async Task CheckAndConnectCloudAsync()
        {
            IORepository ioRepository = new();
            if (Global.NetworkStatus && !Global.OAuthStatus)
            {
                using var tokenSource = new CancellationTokenSource();
                var LEDcanceltoken = tokenSource.Token;
                DropBox db = new DropBox();
                Global.OAuthStatus = await db.TestDropBoxAuthAsync();
                Console.WriteLine($"DBAUth Status: {Global.OAuthStatus}");
                if (!Global.OAuthStatus)// && outerAuthTask.Status.Equals(null))
                {
                    if (Global.OS == "Raspberry Pi") { ioRepository.LongBlinkLEDAsync(Config.YellowLED, 10000, LEDcanceltoken); };
                    var dbAuth = await db.DropBoxAuth();
                    if (dbAuth) { if (Global.OS == "Raspberry Pi") { tokenSource.Cancel(); }; };
                }
            }
        }
        public async Task EstablishWifiAsync()
        {
            switch (Global.OS)
            {
                case "Raspberry Pi":
                    // Doesn't seem to work - Use D-bus, or send command to shell? 
                    Wpa wpa = new Wpa("wlan0");
                    if (await wpa.SetAsync(Config.SSID))
                    {
                        // Try enable using provided password
                        if (await wpa.TryEnableAsync(Config.SSIDpw))
                        {
                            // Save config and reboot host
                            await wpa.SaveAndRebootAsync();
                        }
                    }
                    break;
                case "Windows":
                    SimpleWifi.Wifi wifi = new();
                    var accessPoints = wifi.GetAccessPoints();
                    List<string> accessPointNames = new List<string>();
                    foreach (AccessPoint ap in accessPoints)
                    {
                        accessPointNames.Add(ap.Name);
                        if (ap.Name == Config.SSID)
                        {
                            AuthRequest authRequest = new AuthRequest(ap)
                            {
                                Password = Config.SSIDpw
                            };
                            if (ap.Connect(authRequest))
                                Console.WriteLine("connected");
                            else
                                Console.WriteLine("disconnected");
                        }

                    }
                break;
            }
        }
        public class DropBox
        {
            private string _dbauthcode { get; set; } = string.Empty;
            IORepository ioRepository = new();
            public async Task PushToDropBoxAsync()
            {
                using var tokenSource = new CancellationTokenSource();
                var LEDcanceltoken = tokenSource.Token;
                if (Global.OS == "Raspberry Pi") { ioRepository.BlinkOneLED(Config.YellowLED, 1000, LEDcanceltoken); };
                var client = new DropboxClient(Settings.Default.RefreshToken, Config.DbApiKey);
                string folder = $"/{Global.SessionName}";
                // var client = new DropboxClient(Settings.Default.RefreshToken, ApiKey, config);
                Console.WriteLine("Push To Remote");
                Console.WriteLine(client);
                Console.WriteLine(folder);
                Console.WriteLine(Global.mp3PathAndName);

                try
                {
                    var createFolderTask = CreateFolder(client, folder);
                    createFolderTask.Wait();
                    string file = Global.mp3PathAndName;
                    var uploadTask = ChunkUpload(client, folder, file);//    Task.Run((Func<Task<int>>)instance.Run);
                    await uploadTask;
                    // return;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    // throw e;
                }
                if (Global.OS == "Raspberry Pi") { tokenSource.Cancel(); };
                return;

                async Task<FolderMetadata> CreateFolder(DropboxClient client, string path)
                {
                    Console.WriteLine("--- Creating Folder ---");
                    Console.WriteLine(path);

                    var folderArg = new CreateFolderArg(path);
                    Console.WriteLine(folderArg);
                    try
                    {
                        var folder = await client.Files.CreateFolderV2Async(folderArg);

                        Console.WriteLine("Folder: " + path + " created!");

                        return folder.Metadata;
                    }
                    catch (ApiException<CreateFolderError> e)
                    {
                        if (e.Message.StartsWith("path/conflict/folder"))
                        {
                            Console.WriteLine("Folder already exists... Skipping create");
                            return null;
                        }
                        else
                        {
                            throw e;
                        }
                    }
                }

                async Task ChunkUpload(DropboxClient client, string folder, string fileName)
                {
                    Console.WriteLine("Chunk upload file...");
                    // Chunk size is 128KB.
                    const int chunkSize = 128 * 1024;
                    byte[] myByteArray = File.ReadAllBytes(fileName);
                    using (var stream = new MemoryStream(myByteArray))
                    {
                        int numChunks = (int)Math.Ceiling((double)stream.Length / chunkSize);

                        byte[] buffer = new byte[chunkSize];
                        string sessionId = null;

                        for (var idx = 0; idx < numChunks; idx++)
                        {
                            Console.WriteLine("Start uploading chunk {0}", idx);
                            var byteRead = stream.Read(buffer, 0, chunkSize);

                            using (MemoryStream memStream = new MemoryStream(buffer, 0, byteRead))
                            {
                                if (idx == 0)
                                {
                                    var result = await client.Files.UploadSessionStartAsync(body: memStream);
                                    sessionId = result.SessionId;
                                }

                                else
                                {
                                    UploadSessionCursor cursor = new UploadSessionCursor(sessionId, (ulong)(chunkSize * idx));

                                    if (idx == numChunks - 1)
                                    {
                                        var name = Path.GetFileName(fileName);
                                        await client.Files.UploadSessionFinishAsync(cursor, new CommitInfo(folder + "/" + name), body: memStream);
                                    }

                                    else
                                    {
                                        await client.Files.UploadSessionAppendV2Async(cursor, body: memStream);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            //public void DBAuth()
            //{
            //    try
            //    {
            //        var task = Run();//    Task.Run((Func<Task<int>>)instance.Run);
            //        task.Wait();
            //        return;
            //    }
            //    catch (Exception e)
            //    {
            //        Console.WriteLine(e);
            //        throw e;
            //    }
            //}
            async Task<bool> GetCurrentAccount(DropboxClient client)
            {
                bool authSuccess = false;
                try
                {
                    Console.WriteLine("Current Account:");
                    var full = await client.Users.GetCurrentAccountAsync();

                    Console.WriteLine("Account id    : {0}", full.AccountId);
                    Console.WriteLine("Country       : {0}", full.Country);
                    Console.WriteLine("Email         : {0}", full.Email);
                    Console.WriteLine("Is paired     : {0}", full.IsPaired ? "Yes" : "No");
                    Console.WriteLine("Locale        : {0}", full.Locale);
                    Console.WriteLine("Name");
                    Console.WriteLine("  Display  : {0}", full.Name.DisplayName);
                    Console.WriteLine("  Familiar : {0}", full.Name.FamiliarName);
                    Console.WriteLine("  Given    : {0}", full.Name.GivenName);
                    Console.WriteLine("  Surname  : {0}", full.Name.Surname);
                    Console.WriteLine("Referral link : {0}", full.ReferralLink);

                    if (full.Team != null)
                    {
                        Console.WriteLine("Team");
                        Console.WriteLine("  Id   : {0}", full.Team.Id);
                        Console.WriteLine("  Name : {0}", full.Team.Name);
                    }
                    else
                    {
                        Console.WriteLine("Team - None");
                    }
                    authSuccess = full.AccountId == null ? false : true;
                }
                catch (Exception e)
                {
                    authSuccess = false;
                    //.WriteLine("Oath Failed!!!");
                    //throw e;
                }
                return authSuccess;
            }
            public async Task DropBoxAuthResetAsync()
            {
                Settings.Default.AccessToken = null;
                Settings.Default.RefreshToken = null;
                Settings.Default.Save();
                Global.OAuthStatus = false;
            }
            public async Task<bool> TestDropBoxAuthAsync()
            {
                bool authSuccess = false;
                //    var dbAuthTask = Task.Run(async () =>
                //    {
                var httpClient = new HttpClient(new HttpClientHandler { });
                try
                {
                    var config = new DropboxClientConfig("SmartaCam")
                    {
                        HttpClient = httpClient
                    };
                    var client = new DropboxClient(Settings.Default.RefreshToken, Config.DbApiKey, config);
                    var dbGetAccountTask = GetCurrentAccount(client);
                    // authSuccess = true;
                    authSuccess = await dbGetAccountTask;
                }
                catch (HttpException e)
                {
                    Console.WriteLine("Exception reported from RPC layer");
                    Console.WriteLine("    Status code: {0}", e.StatusCode);
                    Console.WriteLine("    Message    : {0}", e.Message);
                    if (e.RequestUri != null)
                    {
                        Console.WriteLine("    Request uri: {0}", e.RequestUri);
                    }
                    authSuccess = false;
                }
                //  });
                //  await dbAuthTask;
                return authSuccess;
            }
            public async Task<bool> DropBoxAuth()
            {
                bool authSuccess = false;
                var dbAuthTask = Task.Run(async () =>
                {
                    Console.WriteLine("DropBox Authorization Starting");
                    await AcquireAccessToken(null, IncludeGrantedScopes.None);
                    var httpClient = new HttpClient(new HttpClientHandler { });
                    try
                    {
                        var config = new DropboxClientConfig("SmartaCam")
                        {
                            HttpClient = httpClient
                        };

                        var client = new DropboxClient(Settings.Default.RefreshToken, Config.DbApiKey, config);

                        // This call should succeed since the correct scope has been acquired

                        var dbGetAccountTask = GetCurrentAccount(client);
                        await dbGetAccountTask;
                        authSuccess = true;
                    }
                    catch (HttpException e)
                    {
                        Console.WriteLine("Exception reported from RPC layer");
                        Console.WriteLine("    Status code: {0}", e.StatusCode);
                        Console.WriteLine("    Message    : {0}", e.Message);
                        if (e.RequestUri != null)
                        {
                            Console.WriteLine("    Request uri: {0}", e.RequestUri);
                        }
                        authSuccess = false;
                    }
                });
                await dbAuthTask;
                return authSuccess;
            }
            async Task<string> AcquireAccessToken(string[] scopeList, IncludeGrantedScopes includeGrantedScopes)
            {

                Console.Write("Resetting auth keys ");
                //if (Console.ReadKey().Key == ConsoleKey.Y)
                //{
                //    /*Settings.Default.Reset();*/
                //}
                Console.WriteLine();
                Settings.Default.Reset();
                var accessToken = Settings.Default.AccessToken;
                var refreshToken = Settings.Default.RefreshToken;

                if (string.IsNullOrEmpty(accessToken))
                {
                    try
                    {
                        // Console.WriteLine("Waiting for credentials.");
                        var OAuthFlow = new PKCEOAuthFlow();
                        var authorizeUri = OAuthFlow.GetAuthorizeUri(OAuthResponseType.Code, Config.DbApiKey, state: "N", tokenAccessType: TokenAccessType.Offline, scopeList: scopeList, includeGrantedScopes: includeGrantedScopes);

                        Console.WriteLine("Visit this webpage and get credentials:");
                        //Console.WriteLine(authorizeUri);
                        //WAIT FOR USB INSERT SOMEHOW
                        // Create a file to write to.
                        Global.DropBoxCodeDotTxtContains = authorizeUri + Environment.NewLine;
                        Console.WriteLine($"DropBox Code Path: {Global.RemovableDrivePath}");
                        File.WriteAllText(Path.Combine(Global.RemovableDrivePath, "DropBoxCode.txt"), Global.DropBoxCodeDotTxtContains);

                        Console.WriteLine("Waiting For DropBox Authorization Code");
                        while (Global.DropBoxCodeDotTxtContains.StartsWith("https:")) // ADD condition for not already authorized
                        {
                            WatchDropBoxCodeFile().GetAwaiter().GetResult();
                            //await Task.Delay(1000);
                        }

                        //string accessCodenil = Console.ReadLine();
                        //var 
                        //Settings.Default.AccessToken = accessToken;
                        // Summary:
                        //     Processes the second half of the OAuth 2.0 code flow. Uses the codeVerifier created
                        //     in this class to execute the second half.
                        //
                        // Parameters:
                        //   code:
                        //     The code acquired in the query parameters of the redirect from the initial authorize
                        //     url.
                        //
                        //   appKey:
                        //     The application key, found in the App Console.
                        //
                        //   redirectUri:
                        //     The redirect URI that was provided in the initial authorize URI, this is only
                        //     used to validate that it matches the original request, it is not used to redirect
                        //     again.
                        //
                        //   client:
                        //     An optional http client instance used to make requests.
                        //
                        // Returns:
                        //     The authorization response, containing the access token and uid of the authorized
                        //     user.
                        var accessCode = Global.DropBoxCodeDotTxtContains;
                        Console.WriteLine("Exchanging code for token");
                        // tokenResult.DefaultIkenizedUri = OAuthFlow.ProcessCodeFlowAsync(accessCode, Global.DbApiKey);

                        var tokenResult = await OAuthFlow.ProcessCodeFlowAsync(accessCode, Config.DbApiKey);//, RedirectUri.ToString(), state);
                        Console.WriteLine("Finished Exchanging Code for Token");
                        // Bring console window to the front.
                        // SetForegroundWindow(GetConsoleWindow());
                        accessToken = tokenResult.AccessToken;
                        refreshToken = tokenResult.RefreshToken;
                        var uid = tokenResult.Uid;
                        Console.WriteLine("Uid: {0}", uid);
                        Console.WriteLine("AccessToken: {0}", accessToken);
                        if (tokenResult.RefreshToken != null)
                        {
                            Console.WriteLine("RefreshToken: {0}", refreshToken);
                            Settings.Default.RefreshToken = refreshToken;
                        }
                        if (tokenResult.ExpiresAt != null)
                        {
                            Console.WriteLine("ExpiresAt: {0}", tokenResult.ExpiresAt);
                        }
                        if (tokenResult.ScopeList != null)
                        {
                            Console.WriteLine("Scopes: {0}", String.Join(" ", tokenResult.ScopeList));
                        }
                        Settings.Default.AccessToken = accessToken;
                        Settings.Default.Uid = uid;
                        Settings.Default.Save();
                        /*
                                            var dbClient = new RestClient("https://api.dropbox.com/oauth2/token");
                                            RestRequest request = new RestRequest("Smart", Method.Post);
                                            request.AddParameter("grant_type", "refresh_token");
                                            request.AddParameter("client_id", Global.DbApiKey);
                                            request.AddParameter("client_secret", Global.DbApiSecret);

                                            var response = dbClient.Post(request);
                                            var content = response.Content;
                                            Console.WriteLine(content);
                                            var tokenResult = Settings.Default.RefreshToken;
                                            */
                        //  http.Stop();
                        return "Recorder";// uid;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error: {0}", e.Message);
                        return null;
                    }
                }
                return null;
            }
            public async Task WatchDropBoxCodeFile()
            {
                Console.WriteLine(Global.Home);
                //using var provider = new PhysicalFileProvider(Global.Home);

                using var provider = new PhysicalFileProvider(Global.RemovableDrivePath);
                Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "true");
                provider.UsePollingFileWatcher = true;
                provider.UseActivePolling = true;
                var contents = provider.GetDirectoryContents(string.Empty);
                //foreach (PhysicalDirectoryInfo fileInfo in contents)
                //{
                //    Console.WriteLine(fileInfo.PhysicalPath);
                //}
                IChangeToken changeToken = provider.Watch("DropBoxCode.txt");
                var tcs = new TaskCompletionSource<object>();
                changeToken.RegisterChangeCallback(state =>
                ((TaskCompletionSource<object>)state).TrySetResult(null), tcs);
                await tcs.Task.ConfigureAwait(true);
                Console.WriteLine("USB event detected");
                await Task.Delay(1000);
                Global.DropBoxCodeDotTxtContains = GetDropBoxCodeFromUSB();
                Console.WriteLine(Global.DropBoxCodeDotTxtContains);
            }
            //public void USBDetected()
            //{
            //    Console.WriteLine("USB event detected");
            //    //Global.DropBoxCodeDotTxtContains = GetDropBoxCodeFromUSB();
            //    Task.Delay(1000);
            //    Global.DropBoxCodeDotTxtContains = GetDropBoxCodeFromUSB();
            //}
            //public void watcher_USBDetected(object sender, EventArrivedEventArgs e)
            //{
            //    Console.WriteLine("USB event detected");
            //    //Global.DropBoxCodeDotTxtContains = GetDropBoxCodeFromUSB();
            //    Task.Delay(1000);
            //    Global.DropBoxCodeDotTxtContains = GetDropBoxCodeFromUSB();
            //}
            public string GetDropBoxCodeFromUSB()
            {
                UIRepository uiRepository = new();
                string path = Path.Combine(Global.RemovableDrivePath.ToString(), "DropBoxCode.txt");
                UtilClass gc = new();
                while (!path.IsFileReady())
                {
                    Task.Delay(1000);
                }
                Console.WriteLine(path);
                using (System.IO.StreamReader sr = new System.IO.StreamReader(path))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        string[] split = line.Split(',');
                        foreach (string word in split)
                        {
                            _dbauthcode = word;
                        }
                    }
                }
                return _dbauthcode;
            }

        }
    }
    public class UIRepository : IUIRepository
    {
        public void ClearDailyTakesCount()
        {
            DateTime today = DateTime.Today;
            if (today != Settings.Default.LastTakeDate)
            {
                Settings.Default.Takes = 0;
                Settings.Default.Save();
            }
        }
        public async Task<int> AskKeepOrEraseFilesAsync()
        {
            IORepository ioRepository = new();
            using var tokenSource = new CancellationTokenSource();
            var LEDcanceltoken = tokenSource.Token;
            // var tasks = new ConcurrentBag<Task>();

            string[] allfiles = Directory.GetFiles(Global.LocalRecordingsFolder, "*.*", SearchOption.AllDirectories);

            if (allfiles.Length > 0)
            {
                if (Global.OS == "Raspberry Pi") { ioRepository.BlinkAllLEDs(LEDcanceltoken); };
                Console.Write("Press 'record' to delete all saved recordings on SD & USB, and clear upload and play queues, or press 'play' to keep files ");
                int erased = GetValidUserSelection(new List<int> { 0, 1, 2 });
                if (Global.OS == "Raspberry Pi") { tokenSource.Cancel(); };
                if (erased == 1)
                {
                    Console.WriteLine($"Erasing {allfiles.Length} files in Recordings Folder.");
                    DirectoryInfo di = new DirectoryInfo(Global.LocalRecordingsFolder);

                    foreach (FileInfo file in di.GetFiles("*.*", SearchOption.AllDirectories))
                    {
                        file.Delete();
                    }
                    foreach (DirectoryInfo dir in di.GetDirectories())
                    {
                        dir.Delete(true);
                    }
                }
                else if (erased == 0)
                {
                    //reboot the pi
                }
                return erased;
            }
            else return 0;
        }
        public async Task<string> IdentifyOS()
        {
            string os;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                os = "Windows";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // os = "Linux";
                string piCatQuery = "/sys/firmware/devicetree/base/model";
                var piCatQueryReturn = await RunBashCatAsync(piCatQuery);
                os = piCatQueryReturn.Contains("Raspberry") ? "Raspberry Pi" : "Linux"; 
            }
            else { os = "unknown"; }
            return os;
        }
        public async Task MainMenuAsync()
        {
            AudioRepository audioRepository = new();
            Console.WriteLine("1 . Record/Pause\r\n2 . Play/Pause\r\n3 . Skip Back\r\n4 . Skip Forward\r\n0 . Reboot");
            var selection = GetValidUserSelection(new List<int> { 0, 1, 2, 3, 4 }); // 0=reboot,1=record,2=play,3=skipforward,4skipback
            switch (selection)
            {
                case 1:
                    if (Global.MyState == 2)
                    {
                        Global.MyState = 1;
                        await audioRepository.NormalizeTakeAsync();
                        audioRepository.ConvertWavToMP3(Global.wavPathAndName, Global.mp3PathAndName);
                        NetworkRepository.DropBox db = new();
                        db.PushToDropBoxAsync();
                    }
                    else
                    {
                        _ = Task.Run(() => { audioRepository.RecordAudioAsync(); });
                       // audioRepository.RecordAudioAsync();
                    }
                    break;
                case 2:
                    await audioRepository.PlaybackAudioAsync();
                    break;
            }
            return;
        }
        public void SetupLocalFileAndLocation()
        {
            string newWavPath = Path.Combine(Global.LocalRecordingsFolder, Global.SessionName);
            string newMp3Path = Path.Combine(newWavPath, "mp3");
            List<string> songfilepaths = new List<string> { newWavPath, newMp3Path };
            foreach (string path in songfilepaths)
            {
                if (Directory.Exists(path))
                {
                    Console.WriteLine($"Path {path} exists");
                }
                else
                {
                    DirectoryInfo di = Directory.CreateDirectory(path);
                    Console.WriteLine($"The directory {path} created at {Directory.GetCreationTime(newWavPath)}.");
                }
            }
            int wavCount = Directory.GetFiles(Path.GetDirectoryName(newWavPath), "*", SearchOption.TopDirectoryOnly).Length;
            int mp3Count = Directory.GetFiles(Path.GetDirectoryName(newMp3Path), "*", SearchOption.TopDirectoryOnly).Length;
            //int take = Math.Max(wavCount, mp3Count) + 1;
            //Settings.Default.Takes++;
            int take = Settings.Default.Takes + 1;
            Global.lastWavPathAndName = Global.wavPathAndName;
            Global.wavPathAndName = Path.Combine(newWavPath, $"{Global.SessionName}_take-{take}.wav");
            Global.mp3PathAndName = Path.Combine(newWavPath, "mp3", $"{Global.SessionName}_take-{take}.mp3");

            //  return wavPathAndName;
        }
        public int FindRemovableDrives(bool displayDetails)
        {
            //   DriveInfo[] allDrives = DriveInfo.GetDrives();
            Console.WriteLine("Inspect Removable Drives");
            var drives = DriveInfo.GetDrives()
              //   .Where(drive => drive.IsReady && drive.DriveType == DriveType.Removable);
              .Where(drive => drive.IsReady && (drive.DriveType == DriveType.Removable || (drive.DriveType == DriveType.Fixed & Global.OS != "Windows")));

            Console.WriteLine($"Number of Drives found: {drives.Count()}");
            if (drives.Count() > 0 && displayDetails == true)
            {
                foreach (DriveInfo d in drives)
                {
                    Console.WriteLine("Drive {0}", d.Name);
                    Console.WriteLine("  Drive type: {0}", d.DriveType);
                    if (d.IsReady == true)
                    {
                        Console.WriteLine("  Volume label: {0}", d.VolumeLabel);
                        Console.WriteLine("  File system: {0}", d.DriveFormat);
                        Console.WriteLine(
                            "  Available space to current user:{0, 15} bytes",
                            d.AvailableFreeSpace);

                        Console.WriteLine(
                            "  Total available space:          {0, 15} bytes",
                            d.TotalFreeSpace);

                        Console.WriteLine(
                            "  Total size of drive:            {0, 15} bytes ",
                            d.TotalSize);
                    }
                    Global.RemovableDrivePath = d.RootDirectory.ToString();
                    Console.WriteLine(Global.RemovableDrivePath);
                }

            }
            return drives.Count();
        }
        public int GetValidUserSelection(List<int> validOptions)
        {
            string input;
            int? validSelection = null;
            do
            {
                input = Console.ReadLine();
                //if (input.ToLower() == "exit") { return 0; }
                int.TryParse(input, out int userVal);
                validSelection = userVal;
            } while (!validOptions.Contains(validSelection ?? -1));
            return validSelection ?? -1;
        }
        public void LoadConfig()
        {
            Config.DbApiKey = Settings.Default.DbApiKey;
            Config.DbApiSecret = Settings.Default.DbApiSecret;
            Config.SSID = Settings.Default.SSID;
            Config.SSIDpw = Settings.Default.SSIDpw;
            Config.DbCode = Settings.Default.DbCode;
            Config.SelectedAudioDevice = Settings.Default.SelectedAudioDevice;
            Config.SampleRate = Settings.Default.SampleRate;
        }
        public async Task<string> RunBashCatAsync(string command)
        {
            var bashTask = Task.Run(() =>
            {
                var psi = new ProcessStartInfo();
              //  psi.FileName = "/bin/bash";
                psi.FileName = "/usr/bin/cat";
                psi.Arguments = command;
                psi.RedirectStandardOutput = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                using var process = Process.Start(psi);
                process.WaitForExit();
                return process.StandardOutput.ReadToEnd();
            });
            return await bashTask;
        }

    }
    public class IORepository : IIORepository
    {
        public string GetUSBDeviceName(nint name)
        {
            string s = "";
            if (name != nint.Zero)
            {
                int length = 0;
                unsafe
                {
                    byte* b = (byte*)name;
                    if (b != null)
                    {
                        while (*b != 0)
                        {
                            ++b;
                            length += 1;
                        }
                    }
                }

                if (length > 0)
                {
                    byte[] stringBuffer = new byte[length];
                    Marshal.Copy(name, stringBuffer, 0, length);
                    s = Encoding.UTF8.GetString(stringBuffer);
                }
            }
            return s;
        }
        public async Task LEDBlinkAsync(int pin, int duration)
        {
            int sleepS = duration;
            using var controller = new GpioController();
            Task LEDtask = Task.Run(() =>
            {
                controller.OpenPin(pin, PinMode.Output);
                controller.Write(pin, true ? PinValue.High : PinValue.Low);
                Thread.Sleep(sleepS);
                controller.Write(pin, false ? PinValue.High : PinValue.Low);
                Thread.Sleep(sleepS);
            });
            await LEDtask;
            controller.ClosePin(pin);
        }
        public async Task LongBlinkLEDAsync(int pin, int duration, CancellationToken ct)
        {
            int sleepS = duration;
            using var controller = new GpioController();
            Task LEDtask = Task.Run(() =>
            {
                controller.OpenPin(pin, PinMode.Output);
                while (!ct.IsCancellationRequested)
                {
                    controller.Write(pin, false); //? PinValue.High : PinValue.Low);
                    Thread.Sleep(1000);
                    controller.Write(pin, true); //? PinValue.High : PinValue.Low);
                    Thread.Sleep(sleepS);
                }
            });
            await LEDtask;
            controller.Write(pin, true);
            controller.ClosePin(pin);
        }
        public void BlinkLED(int pin, int duration)
        {
            int sleepS = duration;
            using var controller = new GpioController();
            controller.OpenPin(pin, PinMode.Output);
            controller.Write(pin, true ? PinValue.High : PinValue.Low);
            Thread.Sleep(sleepS);
            controller.Write(pin, false ? PinValue.High : PinValue.Low);
            controller.ClosePin(pin);
            Thread.Sleep(sleepS);
        }
        public async Task TurnOffLEDAsync(int pin)
        {
            using var controller = new GpioController();
            var LEDtask = Task.Run(() =>
            {
                controller.OpenPin(pin, PinMode.Output);
                controller.Write(pin, false);
            });
            await LEDtask;
            controller.ClosePin(pin);
        }
        public async Task TurnOnLEDAsync(int pin)
        {
            using var controller = new GpioController();
            var LEDtask = Task.Run(() =>
            {
                controller.OpenPin(pin, PinMode.Output);
                controller.Write(pin, true);
            });
            await LEDtask;
            controller.ClosePin(pin);
        }
        public async Task BlinkAllLEDs(CancellationToken ct)
        {
            var blinkTask = Task.Run(() =>
            {
                while (!ct.IsCancellationRequested)
                {
                    BlinkLED(Config.YellowLED, 55);
                    BlinkLED(Config.RedLED, 55);
                    BlinkLED(Config.YellowLED, 55);
                    BlinkLED(Config.GreenLED, 55);
                }
                TurnOffLEDAsync(Config.YellowLED);
                TurnOffLEDAsync(Config.RedLED);
                TurnOffLEDAsync(Config.GreenLED);
            });
            await blinkTask;
        }
        public async Task BlinkOneLED(int pin, int duration, CancellationToken ct)
        {
            var blinkTask = Task.Run(() =>
            {
                while (!ct.IsCancellationRequested)
                {
                    BlinkLED(pin, duration);
                }
                TurnOnLEDAsync(pin);
            });
            await blinkTask;
        }
    }
}