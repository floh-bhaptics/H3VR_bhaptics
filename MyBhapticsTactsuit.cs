using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Reflection;
using System.Threading;
using Bhaptics.Tact;
using Bhaptics;
using BepInEx;
using H3VR_bhaptics;
using System.Runtime.InteropServices;

namespace MyBhapticsTactsuit
{

    public class TactsuitVR
    {
        public bool suitDisabled = true;
        public bool systemInitialized = false;
        private static ManualResetEvent HeartBeat_mrse = new ManualResetEvent(false);
        private static ManualResetEvent Tingle_mrse = new ManualResetEvent(false);
        private static ManualResetEvent Whip_mrse = new ManualResetEvent(false);
        public Dictionary<String, FileInfo> FeedbackMap = new Dictionary<String, FileInfo>();

#pragma warning disable CS0618 // Typ oder Element ist veraltet
        public HapticPlayer hapticPlayer;
#pragma warning restore CS0618 // Typ oder Element ist veraltet


        private static RotationOption defaultRotationOption = new RotationOption(0.0f, 0.0f);

        public void HeartBeatFunc()
        {
            while (true)
            {
                HeartBeat_mrse.WaitOne();
                PlaybackHaptics("HeartBeat");
                Thread.Sleep(1000);
            }
        }

        public void TingleFunc()
        {
            while (true)
            {
                Tingle_mrse.WaitOne();
                PlaybackHaptics("NeckTingleShort");
                Thread.Sleep(2050);
            }
        }

        public void WhipFunc()
        {
            while (true)
            {
                Whip_mrse.WaitOne();
                PlaybackHaptics("Whip_R");
                Thread.Sleep(1050);
            }
        }

        public TactsuitVR()
        {

            LOG("Initializing suit");
            try
            {
#pragma warning disable CS0618 // Typ oder Element ist veraltet
                hapticPlayer = new HapticPlayer("H3VR_bhaptics", "H3VR_bhaptics");
#pragma warning restore CS0618 // Typ oder Element ist veraltet
                suitDisabled = false;
            }
            catch { LOG("Suit initialization failed!"); }
            RegisterAllTactFiles();
            LOG("Starting HeartBeat and NeckTingle thread...");
            Thread HeartBeatThread = new Thread(HeartBeatFunc);
            HeartBeatThread.Start();
            Thread TingleThread = new Thread(TingleFunc);
            TingleThread.Start();
            Thread WhipThread = new Thread(WhipFunc);
            WhipThread.Start();
        }

        public void LOG(string logStr)
        {
            Plugin.Log.LogMessage(logStr);
        }


        void RegisterAllTactFiles()
        {
            if (suitDisabled) { return; }
            string assemblyFile = Assembly.GetExecutingAssembly().Location;
            string myPath = Path.GetDirectoryName(assemblyFile);
            LOG("Assembly path: " + myPath);
            string configPath = myPath + "\\bHaptics";
            DirectoryInfo d = new DirectoryInfo(configPath);
            FileInfo[] Files = d.GetFiles("*.tact", SearchOption.AllDirectories);
            for (int i = 0; i < Files.Length; i++)
            {
                string filename = Files[i].Name;
                string fullName = Files[i].FullName;
                string prefix = Path.GetFileNameWithoutExtension(filename);
                // LOG("Trying to register: " + prefix + " " + fullName);
                if (filename == "." || filename == "..")
                    continue;
                string tactFileStr = File.ReadAllText(fullName);
                try
                {
                    hapticPlayer.RegisterTactFileStr(prefix, tactFileStr);
                    LOG("Pattern registered: " + prefix);
                }
                catch (Exception e) { LOG(e.ToString()); }

                FeedbackMap.Add(prefix, Files[i]);
                /*
                if (prefix.EndsWith("_R"))
                {
                    string otherPrefix = prefix.Remove(prefix.Length - 2) + "_L";
                    try
                    {
                        hapticPlayer.RegisterTactFileStrReflected(otherPrefix, tactFileStr);
                        LOG("Pattern registered: " + otherPrefix);
                    }
                    catch (Exception e) { LOG(e.ToString()); }
                    
                    FeedbackMap.Add(otherPrefix, Files[i]);
                }
                */
            }
            systemInitialized = true;
            //PlaybackHaptics("HeartBeat");
        }

        public void PlaybackHaptics(String key, float intensity = 1.0f, float duration = 1.0f)
        {
            if (suitDisabled) { return; }
            if (FeedbackMap.ContainsKey(key))
            {
                ScaleOption scaleOption = new ScaleOption(intensity, duration);
                hapticPlayer.SubmitRegisteredVestRotation(key, key, defaultRotationOption, scaleOption);
                // LOG("Playing back: " + key);
            }
            else
            {
                LOG("Feedback not registered: " + key);
            }
        }

        public void PlayBackHit(String key, float xzAngle, float yShift)
        {
            if (suitDisabled) { return; }
            ScaleOption scaleOption = new ScaleOption(1f, 1f);
            RotationOption rotationOption = new RotationOption(xzAngle, yShift);
            hapticPlayer.SubmitRegisteredVestRotation(key, key, rotationOption, scaleOption);
        }

        public void GunRecoil(bool isRightHand, string recoilPrefix, float intensity = 1.0f, bool twoHanded=false, bool shoulderStock=false )
        {
            string prefix = "Recoil";
            if (suitDisabled) { return; }
            float duration = 1.0f;
            var scaleOption = new ScaleOption(intensity, duration);
            var rotationFront = new RotationOption(0f, 0f);
            string postfix = "_L";
            string otherPostfix = "_R";
            if (isRightHand) { postfix = "_R"; otherPostfix = "_L"; }
            prefix += recoilPrefix;
            string keyHand = prefix + "Hands" + postfix;
            string keyArm = prefix + "Arms" + postfix;
            string keyOtherArm = prefix + "Arms" + otherPostfix;
            string keyOtherHand = prefix + "Hands" + otherPostfix;
            if (shoulderStock) { prefix += "Shoulder"; }
            string keyVest = prefix + "Vest" + postfix;
            //LOG("Gunrecoil: " + keyArm + " " + keyVest + " " + keyOtherArm + " " + intensity.ToString());
            hapticPlayer.SubmitRegisteredVestRotation(keyArm, keyArm, rotationFront, scaleOption);
            hapticPlayer.SubmitRegisteredVestRotation(keyHand, keyHand, rotationFront, scaleOption);
            if (twoHanded)
            {
                hapticPlayer.SubmitRegisteredVestRotation(keyOtherArm, keyOtherArm, rotationFront, scaleOption);
                hapticPlayer.SubmitRegisteredVestRotation(keyOtherHand, keyOtherHand, rotationFront, scaleOption);
            }
            hapticPlayer.SubmitRegisteredVestRotation(keyVest, keyVest, rotationFront, scaleOption);
        }
        public void SwordRecoil(bool isRightHand, float intensity = 1.0f)
        {
            if (suitDisabled) { return; }
            float duration = 1.0f;
            var scaleOption = new ScaleOption(intensity, duration);
            var rotationFront = new RotationOption(0f, 0f);
            string postfix = "_L";
            if (isRightHand) { postfix = "_R"; }
            string keyArm = "Sword" + postfix;
            string keyVest = "SwordVest" + postfix;
            hapticPlayer.SubmitRegisteredVestRotation(keyArm, keyArm, rotationFront, scaleOption);
            hapticPlayer.SubmitRegisteredVestRotation(keyVest, keyVest, rotationFront, scaleOption);
        }
        public void WhipPull(bool isRightHand, float intensity = 1.0f)
        {
            if (suitDisabled) { return; }
            float duration = 1.0f;
            var scaleOption = new ScaleOption(intensity, duration);
            var rotationFront = new RotationOption(0f, 0f);
            string postfix = "_L";
            if (isRightHand) { postfix = "_R"; }
            string keyArm = "WhipPull" + postfix;
            string keyVest = "WhipPullVest" + postfix;
            hapticPlayer.SubmitRegisteredVestRotation(keyArm, keyArm, rotationFront, scaleOption);
            hapticPlayer.SubmitRegisteredVestRotation(keyVest, keyVest, rotationFront, scaleOption);
        }

        public void HeadShot(float hitAngle)
        {
            if (suitDisabled) { return; }
            if ((hitAngle<45f)|(hitAngle>315f)) { PlaybackHaptics("Headshot_F"); }
            if ((hitAngle > 45f) && (hitAngle < 135f)) { PlaybackHaptics("Headshot_L"); }
            if ((hitAngle > 135f) && (hitAngle < 225f)) { PlaybackHaptics("Headshot_B"); }
            if ((hitAngle > 225f) && (hitAngle < 315f)) { PlaybackHaptics("Headshot_R"); }
            PlayBackHit("BulletHit", hitAngle, 0.5f);
        }

        public void StartHeartBeat()
        {
            HeartBeat_mrse.Set();
        }

        public void StopHeartBeat()
        {
            HeartBeat_mrse.Reset();
        }

        public void StartTingle()
        {
            Tingle_mrse.Set();
        }

        public void StopTingle()
        {
            Tingle_mrse.Reset();
        }
        public void StartWhip(bool isRightHand)
        {
            Whip_mrse.Set();
        }

        public void StopWhip()
        {
            Whip_mrse.Reset();
        }


        public bool IsPlaying(String effect)
        {
            return hapticPlayer.IsPlaying(effect);
        }

        public void StopHapticFeedback(String effect)
        {
            hapticPlayer.TurnOff(effect);
        }

        public void StopAllHapticFeedback()
        {
            StopThreads();
            foreach (String key in FeedbackMap.Keys)
            {
                hapticPlayer.TurnOff(key);
            }
        }

        public void StopThreads()
        {
            StopHeartBeat();
            StopTingle();
            StopWhip();
        }


    }
}
