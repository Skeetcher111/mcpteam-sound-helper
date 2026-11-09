// MCPteam Sound Helper (MCPteam Звуковой помощник): even system volume for Windows. No driver, zero added audio latency.
// Measures loudness of everything that plays (WASAPI loopback, BS.1770 K-weighting)
// and rides the per-app volumes (Volume Mixer): quiet content up, loud content down, fast guard against bangs.
// The Windows master volume is never touched: it stays exactly where the user put it.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("MCPteam Звуковой помощник — ровная громкость Windows")]
[assembly: System.Reflection.AssemblyProduct("MCPteam Звуковой помощник")]
[assembly: System.Reflection.AssemblyCompany("MCPteam")]
[assembly: System.Reflection.AssemblyCopyright("MIT, MCPteam 2026")]
[assembly: System.Reflection.AssemblyVersion("1.2.4.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.2.4.0")]

namespace SoundHelper
{
    // ---------- Core Audio COM ----------
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumeratorCo { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int mask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object o);
        [PreserveSig] int OpenPropertyStore(int access, out IntPtr store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, int flags, long bufferDuration, long periodicity, IntPtr format, IntPtr session);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint padding);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long def, out long min);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr h);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object o);
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out ulong devPos, out ulong qpcPos);
        [PreserveSig] int ReleaseBuffer(uint frames);
        [PreserveSig] int GetNextPacketSize(out uint frames);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr n);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr n);
        [PreserveSig] int GetChannelCount(out uint c);
        [PreserveSig] int SetMasterVolumeLevel(float db, ref Guid ctx);
        [PreserveSig] int SetMasterVolumeLevelScalar(float v, ref Guid ctx);
        [PreserveSig] int GetMasterVolumeLevel(out float db);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float v);
        [PreserveSig] int SetChannelVolumeLevel(uint ch, float db, ref Guid ctx);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint ch, float v, ref Guid ctx);
        [PreserveSig] int GetChannelVolumeLevel(uint ch, out float db);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint ch, out float v);
        [PreserveSig] int SetMute(bool mute, ref Guid ctx);
        [PreserveSig] int GetMute(out bool mute);
        [PreserveSig] int GetVolumeStepInfo(out uint step, out uint count);
        [PreserveSig] int VolumeStepUp(ref Guid ctx);
        [PreserveSig] int VolumeStepDown(ref Guid ctx);
        [PreserveSig] int QueryHardwareSupport(out uint mask);
        [PreserveSig] int GetVolumeRange(out float min, out float max, out float inc);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr guid, int flags, out IntPtr ctl);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr guid, int flags, out IntPtr vol);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator e);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int n);
        [PreserveSig] int GetSession(int i, out IAudioSessionControl2 s);
    }

    [ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName(out IntPtr name);
        [PreserveSig] int SetDisplayName(string v, ref Guid ctx);
        [PreserveSig] int GetIconPath(out IntPtr path);
        [PreserveSig] int SetIconPath(string v, ref Guid ctx);
        [PreserveSig] int GetGroupingParam(out Guid g);
        [PreserveSig] int SetGroupingParam(ref Guid g, ref Guid ctx);
        [PreserveSig] int RegisterAudioSessionNotification(IAudioSessionEvents n);
        [PreserveSig] int UnregisterAudioSessionNotification(IAudioSessionEvents n);
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetProcessId(out uint pid);
    }

    [ComImport, Guid("24918ACC-64B3-37C1-8CA9-74A66E9957A8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEvents
    {
        [PreserveSig] int OnDisplayNameChanged([MarshalAs(UnmanagedType.LPWStr)] string v, ref Guid ctx);
        [PreserveSig] int OnIconPathChanged([MarshalAs(UnmanagedType.LPWStr)] string v, ref Guid ctx);
        [PreserveSig] int OnSimpleVolumeChanged(float v, int mute, ref Guid ctx);
        [PreserveSig] int OnChannelVolumeChanged(uint n, IntPtr v, uint ch, ref Guid ctx);
        [PreserveSig] int OnGroupingParamChanged(ref Guid g, ref Guid ctx);
        [PreserveSig] int OnStateChanged(int state);
        [PreserveSig] int OnSessionDisconnected(int reason);
    }

    // Windows queues every volume change of a session to each process holding it and frees the queue only when a listener
    // drains it: without a listener the kernel pool (EtwD/Etwr) grew by hundreds of MB a minute. This one just drains.
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    class Drain : IAudioSessionEvents
    {
        public int OnDisplayNameChanged(string v, ref Guid ctx) { return 0; }
        public int OnIconPathChanged(string v, ref Guid ctx) { return 0; }
        public int OnSimpleVolumeChanged(float v, int mute, ref Guid ctx) { return 0; }
        public int OnChannelVolumeChanged(uint n, IntPtr v, uint ch, ref Guid ctx) { return 0; }
        public int OnGroupingParamChanged(ref Guid g, ref Guid ctx) { return 0; }
        public int OnStateChanged(int state) { return 0; }
        public int OnSessionDisconnected(int reason) { return 0; }
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float v, ref Guid ctx);
        [PreserveSig] int GetMasterVolume(out float v);
    }

    // ---------- Settings ----------
    class Settings
    {
        public bool Enabled = true;
        public double Target = -20;     // LUFS of the mix that should sound like "your normal volume"
        public double Strength = 0.8;   // 0..1, how much of the difference is corrected
        public double MaxBoost = 0;     // dB: apps play this much under their mixer volume, so quiet content can be lifted (0: only cut, never drop the normal level)
        public double MaxCut = 24;      // dB
        public double Guard = 5;
        public bool PostVolume = true; // loopback is taken after the master volume (measured on Win10 HDA); loopback=pre to switch off        // dB above target allowed for 50 ms peaks before the guard cuts

        static string Path { get { return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MCPteam Sound Helper", "settings.ini"); } }

        public static string Dir { get { return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MCPteam Sound Helper"); } }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                foreach (var line in File.ReadAllLines(Path))
                {
                    var kv = line.Split('=');
                    if (kv.Length != 2) continue;
                    double v; double.TryParse(kv[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
                    switch (kv[0].Trim())
                    {
                        case "enabled": s.Enabled = v != 0; break;
                        case "target": s.Target = Clamp(v, -35, -8); break;
                        case "strength": s.Strength = Clamp(v, 0, 1); break;
                        case "reserve": s.MaxBoost = Clamp(v, 0, 20); break;
                        case "maxcut": s.MaxCut = Clamp(v, 0, 40); break;
                        case "guard": s.Guard = Clamp(v, 1, 15); break;
                        case "loopback": s.PostVolume = kv[1].Trim() != "pre"; break;
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                File.WriteAllText(Path, string.Format(CultureInfo.InvariantCulture,
                    "enabled={0}\ntarget={1}\nstrength={2}\nreserve={3}\nmaxcut={4}\nguard={5}\n",
                    Enabled ? 1 : 0, Target, Strength, MaxBoost, MaxCut, Guard));
            }
            catch { }
        }

        public static double Clamp(double v, double lo, double hi) { return v < lo ? lo : v > hi ? hi : v; }
    }

    // ---------- Loudness meter + gain controller (shared by live mode and --sim) ----------
    class Biquad
    {
        double b0, b1, b2, a1, a2, z1, z2;
        public Biquad(double b0, double b1, double b2, double a1, double a2) { this.b0 = b0; this.b1 = b1; this.b2 = b2; this.a1 = a1; this.a2 = a2; }
        public double Run(double x) { double y = b0 * x + z1; z1 = b1 * x - a1 * y + z2; z2 = b2 * x - a2 * y; return y; }
    }

    class Controller
    {
        readonly int channels; readonly double fs;
        readonly Biquad[] shelf, hp;
        double eFast = 1e-10, eMom = 1e-10, eSlow = 1e-10, blockE; int blockN;
        public double Gain;              // dB, current correction
        public double LMom = -70, LSlow = -70;
        public Settings S;
        public double MinGain = -24, MaxGain = 12; // set by headroom of the volume control

        public Controller(int channels, double fs, Settings s)
        {
            this.channels = channels; this.fs = fs; S = s;
            shelf = new Biquad[channels]; hp = new Biquad[channels];
            // ITU-R BS.1770 K-weighting for any sample rate (same formulas as libebur128)
            double f0 = 1681.974450955533, G = 3.999843853973347, Q = 0.7071752369554196;
            double K = Math.Tan(Math.PI * f0 / fs), Vh = Math.Pow(10, G / 20), Vb = Math.Pow(Vh, 0.4996667741545416);
            double a0 = 1 + K / Q + K * K;
            double sb0 = (Vh + Vb * K / Q + K * K) / a0, sb1 = 2 * (K * K - Vh) / a0, sb2 = (Vh - Vb * K / Q + K * K) / a0;
            double sa1 = 2 * (K * K - 1) / a0, sa2 = (1 - K / Q + K * K) / a0;
            f0 = 38.13547087602444; Q = 0.5003270373238773; K = Math.Tan(Math.PI * f0 / fs);
            double ha1 = 2 * (K * K - 1) / (1 + K / Q + K * K), ha2 = (1 - K / Q + K * K) / (1 + K / Q + K * K);
            for (int c = 0; c < channels; c++) { shelf[c] = new Biquad(sb0, sb1, sb2, sa1, sa2); hp[c] = new Biquad(1, -2, 1, ha1, ha2); }
        }

        static double L(double e) { return -0.691 + 10 * Math.Log10(e + 1e-12); }

        // interleaved float samples; calls Step() every 10 ms of audio
        public void Feed(float[] buf, int frames)
        {
            int block = (int)(fs / 100);
            for (int i = 0; i < frames; i++)
            {
                double sum = 0;
                for (int c = 0; c < channels && c < 2; c++) // L/R carry the loudness, LFE/surround ignored for simplicity
                {
                    double y = hp[c].Run(shelf[c].Run(buf[i * channels + c]));
                    sum += y * y;
                }
                blockE += sum; blockN++;
                if (blockN >= block) { Step(blockE / blockN, blockN / fs); blockE = 0; blockN = 0; }
            }
        }

        // silence: no packets from loopback -> hold the gain
        public void Silence(double dt) { LMom = -70; }

        void Step(double e, double dt)
        {
            eFast += (e - eFast) * (1 - Math.Exp(-dt / 0.05));
            eMom += (e - eMom) * (1 - Math.Exp(-dt / 0.4));
            LMom = L(eMom);
            if (LMom > -50) eSlow += (e - eSlow) * (1 - Math.Exp(-dt / 2.5)); // gated: pauses don't pull the level down
            LSlow = L(eSlow);
            if (LMom < -50) return; // silence or near-silence: don't pump noise up

            double desired = Settings.Clamp(S.Strength * (S.Target - LSlow), -S.MaxCut, S.MaxBoost);
            desired = Settings.Clamp(desired, MinGain, MaxGain);
            if (desired < Gain) Gain = Math.Max(desired, Gain - 3 * dt);    // going down: 3 dB/s, no audible drop (bangs are the guard's job)
            else Gain = Math.Min(desired, Gain + 3 * dt);                     // going up: 3 dB/s, smooth recovery

            // guard: a bang (explosion, ad, notification) louder than target+Guard is cut at once
            double ceiling = S.Target + S.Guard;
            double lf = L(eFast);
            if (lf + Gain > ceiling) Gain = Math.Max(MinGain, ceiling - lf);
        }
    }

    // ---------- Live engine: loopback in, per-app volumes out (the master volume is only read) ----------
    class Engine
    {
        public Settings S;
        public volatile string Status = "Запуск…";
        public volatile bool Stop;
        public Controller C;
        Thread th;
        Guid ctx = new Guid("6f2b1a0e-5c1d-4b7a-9e2f-524f564e4f31");

        class AppVol { public ISimpleAudioVolume V; public IAudioSessionControl2 C; public string Id; public float Last = -1; }
        readonly Drain drain = new Drain();
        readonly Dictionary<string, AppVol> apps = new Dictionary<string, AppVol>();  // by session instance
        readonly Dictionary<string, float> user = new Dictionary<string, float>();    // mixer volume the user chose, by app
        float applied = 1f; // linear factor currently put on every app
        static string StateFile { get { return Path.Combine(Settings.Dir, "apps.txt"); } }

        public Engine(Settings s) { S = s; LoadState(); }
        public void Start() { th = new Thread(Run) { IsBackground = true, Name = "soundhelper" }; th.SetApartmentState(ApartmentState.MTA); th.Start(); }
        public void Shutdown() { Stop = true; if (th != null) th.Join(1500); }

        // apps.txt keeps the volume the user chose per app: survives a crash and apps that closed while turned down
        void LoadState()
        {
            try { foreach (var l in File.ReadAllLines(StateFile)) { var kv = l.Split('\t'); float v; if (kv.Length == 2 && float.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) user[kv[0]] = v; } } catch { }
        }
        void SaveState()
        {
            try { Directory.CreateDirectory(Settings.Dir); var w = new List<string>(); foreach (var kv in user) w.Add(kv.Key + "\t" + kv.Value.ToString(CultureInfo.InvariantCulture)); File.WriteAllLines(StateFile, w.ToArray()); } catch { }
        }

        void Run()
        {
            while (!Stop)
            {
                try { Session(); }
                catch (Exception ex) { Status = "Нет звукового устройства, жду… (" + ex.Message + ")"; }
                RestoreUser();
                for (int i = 0; i < 20 && !Stop; i++) Thread.Sleep(100);
            }
        }

        void RestoreUser()
        {
            foreach (var a in apps.Values) { float u; if (user.TryGetValue(a.Id, out u)) try { a.V.SetMasterVolume(u, ref ctx); } catch { } try { a.C.UnregisterAudioSessionNotification(drain); } catch { } }
            apps.Clear(); applied = 1f;
            // the file stays: an app closed in the middle of a loud scene keeps the cut volume in Windows,
            // next time it starts we give it back the volume the user chose
        }

        void Scan(IAudioSessionManager2 sm)
        {
            IAudioSessionEnumerator e; if (sm.GetSessionEnumerator(out e) != 0) return;
            int n; e.GetCount(out n);
            var seen = new HashSet<string>(); bool dirty = false; uint me = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            for (int i = 0; i < n; i++)
            {
                IAudioSessionControl2 sc; if (e.GetSession(i, out sc) != 0) continue;
                string inst, id; uint pid;
                if (sc.GetSessionInstanceIdentifier(out inst) != 0 || sc.GetSessionIdentifier(out id) != 0) continue;
                sc.GetProcessId(out pid); if (pid == me) continue;
                seen.Add(inst);
                if (apps.ContainsKey(inst)) continue;
                var v = sc as ISimpleAudioVolume; if (v == null) continue; float cur; v.GetMasterVolume(out cur);
                if (!user.ContainsKey(id)) { user[id] = cur; dirty = true; }
                sc.RegisterAudioSessionNotification(drain);
                apps[inst] = new AppVol { V = v, C = sc, Id = id };
            }
            foreach (var k in new List<string>(apps.Keys)) if (!seen.Contains(k)) { try { apps[k].C.UnregisterAudioSessionNotification(drain); } catch { } apps.Remove(k); }
            if (dirty) SaveState();
        }

        void Apply(float f)
        {
            bool dirty = false;
            foreach (var a in apps.Values)
            {
                // a session that is not playing has nothing to level: leave it, every call costs a Windows event per listener
                int st; if (a.Last >= 0 && (a.C.GetState(out st) != 0 || st != 1)) continue;
                float cur; if (a.V.GetMasterVolume(out cur) != 0) continue;
                float u = user[a.Id];
                // the user moved this app in the Volume Mixer -> that is its new normal
                if (a.Last >= 0 && Math.Abs(cur - a.Last) > 0.02f) { u = Math.Min(1f, cur / Math.Max(applied, 0.01f)); user[a.Id] = u; dirty = true; }
                float want = Math.Min(1f, u * f);
                // every SetMasterVolume makes Windows fire an ETW event that the kernel keeps until reboot (1.2.0 ate ~25 GB/h
                // of pool with dozens of Chrome tabs): only real changes, and Apply itself runs at most every 250 ms
                // step in dB, not linear: 0.02 at 10 % volume was a 1.7 dB jump; 1 dB per 250 ms is still smooth to the ear
                if (a.Last < 0 || Math.Abs(20 * Math.Log10((want + 1e-3) / (a.Last + 1e-3))) >= 1) { a.V.SetMasterVolume(want, ref ctx); a.Last = want; }
            }
            applied = f;
            if (dirty) SaveState();
        }

        void Session()
        {
            var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCo();
            IMMDevice dev; Check(en.GetDefaultAudioEndpoint(0, 1, out dev)); // eRender, eMultimedia
            string devId; dev.GetId(out devId);
            object o;
            Guid iidVol = typeof(IAudioEndpointVolume).GUID; Check(dev.Activate(ref iidVol, 23, IntPtr.Zero, out o)); var vol = (IAudioEndpointVolume)o;
            Guid iidSm = typeof(IAudioSessionManager2).GUID; Check(dev.Activate(ref iidSm, 23, IntPtr.Zero, out o)); var sm = (IAudioSessionManager2)o;
            Guid iidAc = typeof(IAudioClient).GUID; Check(dev.Activate(ref iidAc, 23, IntPtr.Zero, out o)); var ac = (IAudioClient)o;
            IntPtr fmt; Check(ac.GetMixFormat(out fmt));
            int tag = Marshal.ReadInt16(fmt, 0) & 0xffff, ch = Marshal.ReadInt16(fmt, 2), rate = Marshal.ReadInt32(fmt, 4), bits = Marshal.ReadInt16(fmt, 14);
            bool isFloat = tag == 3 || (tag == 0xFFFE && Marshal.ReadInt32(fmt, 24) == 3);
            Check(ac.Initialize(0, 0x00020000, 1000000, 0, fmt, IntPtr.Zero)); // shared, LOOPBACK, 100 ms
            Marshal.FreeCoTaskMem(fmt);
            Guid iidCap = typeof(IAudioCaptureClient).GUID; Check(ac.GetService(ref iidCap, out o)); var cap = (IAudioCaptureClient)o;

            float minDb, maxDb, inc; vol.GetVolumeRange(out minDb, out maxDb, out inc);
            var c = new Controller(ch, rate, S) { MinGain = -S.MaxCut, MaxGain = S.MaxBoost }; C = c;
            Scan(sm);
            Check(ac.Start());
            var buf = new float[rate * ch];
            DateTime lastData = DateTime.UtcNow, lastScan = DateTime.UtcNow, lastApply = DateTime.MinValue;
            try
            {
                while (!Stop)
                {
                    Thread.Sleep(10);
                    c.MaxGain = S.MaxBoost;
                    float cur; vol.GetMasterVolumeLevel(out cur);
                    // loopback comes after the master volume and after our per-app factor: undo both, so we measure the content itself
                    float comp = (S.PostVolume ? (float)Math.Pow(10, -(cur - maxDb) / 20.0) : 1f) / applied;
                    uint n; bool got = false;
                    while (cap.GetNextPacketSize(out n) == 0 && n > 0)
                    {
                        IntPtr data; uint frames, flags; ulong p1, p2;
                        if (cap.GetBuffer(out data, out frames, out flags, out p1, out p2) != 0) break;
                        int count = (int)frames * ch;
                        if (buf.Length < count) buf = new float[count];
                        if ((flags & 2) != 0) Array.Clear(buf, 0, count);
                        else if (isFloat && bits == 32) Marshal.Copy(data, buf, 0, count);
                        else if (bits == 16) { var t = new short[count]; Marshal.Copy(data, t, 0, count); for (int i = 0; i < count; i++) buf[i] = t[i] / 32768f; }
                        else Array.Clear(buf, 0, count);
                        cap.ReleaseBuffer(frames);
                        if (comp != 1f) for (int i = 0; i < count; i++) buf[i] *= comp;
                        c.Feed(buf, (int)frames); got = true;
                    }
                    if (got) lastData = DateTime.UtcNow; else if ((DateTime.UtcNow - lastData).TotalMilliseconds > 300) c.Silence(0.01);

                    // normal content plays MaxBoost dB under the app's mixer volume; quiet content gets that room back
                    float f = S.Enabled ? (float)Math.Pow(10, (c.Gain - S.MaxBoost) / 20.0) : 1f;
                    bool rescan = (DateTime.UtcNow - lastScan).TotalSeconds > 1;
                    if (rescan) { lastScan = DateTime.UtcNow; Scan(sm); }
                    double step = 20 * Math.Log10(f / applied), since = (DateTime.UtcNow - lastApply).TotalMilliseconds;
                    // a bang (drop of 3 dB+) is cut within 50 ms, everything else rides at most every 250 ms
                    if (rescan || (since >= 250 && Math.Abs(step) >= 0.25) || (since >= 50 && step <= -3))
                    { Apply(f); lastApply = DateTime.UtcNow; }

                    Status = !S.Enabled ? string.Format("Выключено (звук {0:0.0} LUFS)", c.LMom) : c.LMom < -50 ? "Тишина, громкость держу"
                        : string.Format("Звук {0:0} LUFS, поправка {1:+0;-0;0} дБ, программ: {2}", c.LSlow, c.Gain - S.MaxBoost, apps.Count);

                    if (rescan)
                    {
                        IMMDevice d2; string id2 = null;
                        if (en.GetDefaultAudioEndpoint(0, 1, out d2) == 0) d2.GetId(out id2);
                        if (id2 != devId) break; // headphones plugged / output switched
                    }
                }
            }
            finally { try { ac.Stop(); } catch { } }
        }

        static void Check(int hr) { if (hr != 0) Marshal.ThrowExceptionForHR(hr); }
    }

    // ---------- Tray UI ----------
    class App : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "MCPteam Sound Helper";
        readonly NotifyIcon tray; readonly Engine eng; readonly Settings S;
        readonly ToolStripMenuItem miOn, miAuto;
        readonly Icon icOn, icOff; Form panel;
        readonly System.Windows.Forms.Timer timer;

        public App()
        {
            S = Settings.Load();
            eng = new Engine(S); eng.Start();
            icOn = MakeIcon(Color.FromArgb(255, 122, 0)); icOff = MakeIcon(Color.Gray);
            var menu = new ContextMenuStrip();
            miOn = new ToolStripMenuItem("Включено", null, delegate { Toggle(); }) { Checked = S.Enabled };
            miAuto = new ToolStripMenuItem("Запускать вместе с Windows", null, delegate { SetAuto(!IsAuto()); miAuto.Checked = IsAuto(); }) { Checked = IsAuto() };
            menu.Items.Add(miOn);
            menu.Items.Add("Настройки…", null, delegate { ShowPanel(); });
            menu.Items.Add(miAuto);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("О программе (MCPteam)", null, delegate { try { System.Diagnostics.Process.Start("https://github.com/Skeetcher111/mcpteam-sound-helper"); } catch { } });
            menu.Items.Add("Выход", null, delegate { Quit(); });
            tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true, Icon = S.Enabled ? icOn : icOff, Text = "MCPteam Звуковой помощник" };
            tray.MouseClick += delegate (object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowPanel(); };
            timer = new System.Windows.Forms.Timer { Interval = 500 };
            timer.Tick += delegate { string t = "Звуковой помощник: " + eng.Status; tray.Text = t.Length > 63 ? t.Substring(0, 63) : t; };
            timer.Start();
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) if (k != null && k.GetValue("Rovno") != null) { SetAuto(true); miAuto.Checked = true; } // autostart of 1.0-1.1
            if (!File.Exists(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MCPteam Sound Helper", "settings.ini")))
            {
                S.Save(); SetAuto(true); miAuto.Checked = true;
                tray.ShowBalloonTip(5000, "Звуковой помощник работает", "Громкость всех программ теперь держится ровно. Настройки — по клику на значок.", ToolTipIcon.Info);
            }
        }

        void Toggle() { S.Enabled = !S.Enabled; miOn.Checked = S.Enabled; tray.Icon = S.Enabled ? icOn : icOff; S.Save(); }

        void Quit() { timer.Stop(); eng.Shutdown(); tray.Visible = false; S.Save(); ExitThread(); }

        static bool IsAuto() { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue(RunName) != null; }
        static void SetAuto(bool on)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            { k.DeleteValue("Rovno", false); if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\""); else k.DeleteValue(RunName, false); }
        }

        void ShowPanel()
        {
            if (panel != null && !panel.IsDisposed) { panel.Activate(); return; }
            panel = BuildPanel(S, icOn, Toggle, delegate { return eng.Status; }); panel.Show();
        }

        public static Form BuildPanel(Settings S, Icon icon, Action toggle, Func<string> status)
        {
            var f = new Form { Text = "MCPteam Звуковой помощник", FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                ClientSize = new Size(380, 250), StartPosition = FormStartPosition.Manual, Icon = icon, Font = new Font("Segoe UI", 9.5f), TopMost = true };
            var wa = Screen.PrimaryScreen.WorkingArea; f.Location = new Point(wa.Right - f.Width - 12, wa.Bottom - f.Height - 12);
            var on = new CheckBox { Text = "Выравнивать громкость", Checked = S.Enabled, Left = 16, Top = 12, Width = 300 };
            on.CheckedChanged += delegate { if (on.Checked != S.Enabled) toggle(); };
            var lt = new Label { Left = 16, Top = 44, Width = 350 };
            var tt = new TrackBar { Left = 10, Top = 64, Width = 360, Minimum = -35, Maximum = -8, TickFrequency = 3, Value = (int)S.Target };
            var ls = new Label { Left = 16, Top = 112, Width = 350 };
            var ts = new TrackBar { Left = 10, Top = 132, Width = 360, Minimum = 0, Maximum = 100, TickFrequency = 10, Value = (int)(S.Strength * 100) };
            var st = new Label { Left = 16, Top = 186, Width = 350, ForeColor = Color.DimGray };
            var hint = new Label { Left = 16, Top = 212, Width = 350, Height = 34, ForeColor = Color.DimGray, Text = "Громкость Windows не трогаю: выравниваю звук программ в микшере." };
            Action upd = delegate
            {
                lt.Text = string.Format("Целевая громкость: {0} LUFS{1}", tt.Value, tt.Value >= -16 ? " (громче)" : tt.Value <= -26 ? " (тише)" : "");
                ls.Text = string.Format("Сила выравнивания: {0}%", ts.Value);
            };
            tt.ValueChanged += delegate { S.Target = tt.Value; upd(); S.Save(); };
            ts.ValueChanged += delegate { S.Strength = ts.Value / 100.0; upd(); S.Save(); };
            var t = new System.Windows.Forms.Timer { Interval = 250 }; t.Tick += delegate { st.Text = status(); }; st.Text = status(); t.Start();
            f.FormClosed += delegate { t.Dispose(); };
            upd();
            f.Controls.AddRange(new Control[] { on, lt, tt, ls, ts, st, hint });
            return f;
        }

        public static Icon MakeIcon(Color c)
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(c)) g.FillEllipse(b, 1, 1, 30, 30);
                int[] h = { 8, 14, 20, 14, 8 }; // bars squeezed to one level
                using (var w = new SolidBrush(Color.White))
                    for (int i = 0; i < 5; i++) g.FillRectangle(w, 7 + i * 4, 16 - h[i] / 2, 2, h[i]);
                using (var p = new Pen(Color.White, 2)) g.DrawLine(p, 6, 16, 26, 16);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }
    }

    // ---------- Offline simulation: same controller on a WAV, models 20 ms control latency ----------
    static class Sim
    {
        public static int Run(string inPath, string outPath, double userHeadroomDb)
        {
            var r = new BinaryReader(File.OpenRead(inPath));
            if (new string(r.ReadChars(4)) != "RIFF") throw new Exception("not wav");
            r.ReadInt32(); r.ReadChars(4);
            int ch = 0, rate = 0, bits = 0, tag = 0; byte[] data = null;
            while (r.BaseStream.Position < r.BaseStream.Length - 8)
            {
                string id = new string(r.ReadChars(4)); int len = r.ReadInt32();
                if (id == "fmt ") { tag = r.ReadInt16(); ch = r.ReadInt16(); rate = r.ReadInt32(); r.ReadInt32(); r.ReadInt16(); bits = r.ReadInt16(); r.ReadBytes(len - 16); }
                else if (id == "data") { data = r.ReadBytes(len); break; }
                else r.ReadBytes(len + (len & 1));
            }
            int frames = data.Length / (ch * bits / 8);
            var x = new float[frames * ch];
            for (int i = 0; i < x.Length; i++) x[i] = bits == 16 ? BitConverter.ToInt16(data, i * 2) / 32768f : BitConverter.ToSingle(data, i * 4);
            var s = new Settings(); var c = new Controller(ch, rate, s) { MinGain = -s.MaxCut, MaxGain = Math.Min(s.MaxBoost, userHeadroomDb) };
            int block = rate / 100; var gains = new List<double>();
            var y = new float[x.Length]; var tmp = new float[block * ch];
            double applied = 0; var log = new StreamWriter(outPath + ".gain.tsv");
            for (int f0 = 0; f0 < frames; f0 += block)
            {
                int n = Math.Min(block, frames - f0);
                gains.Add(c.Gain);
                double g = gains.Count > 2 ? gains[gains.Count - 3] : 0; // what the volume control applies 20 ms later
                for (int i = 0; i < n; i++)
                {
                    double a = applied + (g - applied) * (i + 1) / n; // engine ramps volume inside a buffer
                    double lin = Math.Pow(10, a / 20);
                    for (int k = 0; k < ch; k++) y[(f0 + i) * ch + k] = (float)(x[(f0 + i) * ch + k] * lin);
                }
                applied = g;
                Array.Copy(x, f0 * ch, tmp, 0, n * ch);
                c.Feed(tmp, n);
                if ((f0 / block) % 10 == 0) log.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0:0.00}\t{1:0.0}\t{2:0.0}", (double)f0 / rate, c.LSlow, applied));
            }
            log.Close();
            using (var w = new BinaryWriter(File.Create(outPath)))
            {
                w.Write("RIFF".ToCharArray()); w.Write(36 + y.Length * 4); w.Write("WAVEfmt ".ToCharArray());
                w.Write(16); w.Write((short)3); w.Write((short)ch); w.Write(rate); w.Write(rate * ch * 4); w.Write((short)(ch * 4)); w.Write((short)32);
                w.Write("data".ToCharArray()); w.Write(y.Length * 4);
                foreach (var v in y) w.Write(v);
            }
            return 0;
        }
    }

    static class Program
    {
        static float Master()
        {
            IMMDevice d; ((IMMDeviceEnumerator)new MMDeviceEnumeratorCo()).GetDefaultAudioEndpoint(0, 1, out d);
            object o; Guid g = typeof(IAudioEndpointVolume).GUID; d.Activate(ref g, 23, IntPtr.Zero, out o);
            float v; ((IAudioEndpointVolume)o).GetMasterVolumeLevelScalar(out v); return v;
        }

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length >= 3 && args[0] == "--sim")
                return Sim.Run(args[1], args[2], args.Length > 3 ? double.Parse(args[3], CultureInfo.InvariantCulture) : 12);
            if (args.Length >= 1 && args[0] == "--probe")
            {   // diagnostics: run the engine for N seconds, print status and the Windows master volume (must not change)
                var so = new StreamWriter(Console.OpenStandardOutput(), new System.Text.UTF8Encoding(false)) { AutoFlush = true }; var st = Settings.Load(); st.Enabled = !(args.Length > 1 && args[1] == "off");
                var e = new Engine(st); e.Start();
                int total = args.Length > 2 ? int.Parse(args[2]) * 2 : 24;
                for (int i = 0; i < total; i++) { Thread.Sleep(500); so.WriteLine("{0,4:0.0}s  {1}  | Windows {2:0.00}", (i + 1) / 2.0, e.Status, Master()); }
                e.Shutdown(); so.WriteLine("exit  | Windows {0:0.00}", Master()); return 0;
            }
            if (args.Length >= 2 && args[0] == "--shot")
            {   // README screenshot of the settings window, rendered without showing it
                Application.EnableVisualStyles();
                var f = App.BuildPanel(new Settings(), App.MakeIcon(Color.FromArgb(255, 122, 0)), delegate { }, delegate { return "Звук -27 LUFS, поправка +6 дБ"; });
                var cs = f.ClientSize; var hh = f.Handle;
                using (var bmp = new Bitmap(cs.Width, cs.Height))
                {
                    using (var g = Graphics.FromImage(bmp)) g.Clear(SystemColors.Control);
                    foreach (Control c in f.Controls) { var h2 = c.Handle; c.DrawToBitmap(bmp, c.Bounds); }
                    bmp.Save(args[1]);
                }
                return 0;
            }
            try { string ad = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), od = System.IO.Path.Combine(ad, "Rovno"); // settings of 1.0-1.1
                  if (Directory.Exists(od) && !Directory.Exists(Settings.Dir)) Directory.Move(od, Settings.Dir); } catch { }
            bool created;
            using (var m = new Mutex(true, "Rovno.MCPteam.single", out created))
            {
                if (!created) return 0;
                Application.EnableVisualStyles();
                Application.Run(new App());
            }
            return 0;
        }
    }
}
