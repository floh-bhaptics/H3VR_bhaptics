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
        // Event to start and stop the heartbeat thread
        private static ManualResetEvent HeartBeat_mrse = new ManualResetEvent(false);
        // dictionary of all feedback patterns found in the bHaptics directory
        public Dictionary<String, FileInfo> FeedbackMap = new Dictionary<String, FileInfo>();

#pragma warning disable CS0618 // remove warning that the C# library is deprecated
        public HapticPlayer hapticPlayer;
#pragma warning restore CS0618 

        private static RotationOption defaultRotationOption = new RotationOption(0.0f, 0.0f);

        public void HeartBeatFunc()
        {
            while (true)
            {
                // Check if reset event is active
                HeartBeat_mrse.WaitOne();
                PlaybackHaptics("HeartBeat");
                Thread.Sleep(1000);
            }
        }

        public TactsuitVR()
        {

            LOG("Initializing suit");
            try
            {
#pragma warning disable CS0618 // remove warning that the C# library is deprecated
                hapticPlayer = new HapticPlayer("H3VR_bhaptics", "H3VR_bhaptics");
#pragma warning restore CS0618
                suitDisabled = false;
            }
            catch { LOG("Suit initialization failed!"); }
            RegisterAllTactFiles();
            LOG("Starting HeartBeat thread...");
            Thread HeartBeatThread = new Thread(HeartBeatFunc);
            HeartBeatThread.Start();
        }

        public void LOG(string logStr)
        {
            Plugin.Log.LogMessage(logStr);
        }


        void RegisterAllTactFiles()
        {
            if (suitDisabled) { return; }
            // Get location of the compiled assembly and search through "bHaptics" directory and contained patterns
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

                // tried to only save right side pattern and submit reflected version for left side,
                // but it somehow didn't work.
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
        }

        public void PlaybackHaptics(String key, float intensity = 1.0f, float duration = 1.0f)
        {
            if (suitDisabled) { return; }
            if (FeedbackMap.ContainsKey(key))
            {
                ScaleOption scaleOption = new ScaleOption(intensity, duration);
                hapticPlayer.SubmitRegisteredVestRotation(key, key, defaultRotationOption, scaleOption);
            }
            else
            {
                LOG("Feedback not registered: " + key);
            }
        }

        public void PlayBackHit(String key, float xzAngle, float yShift)
        {
            // two parameters can be given to the pattern to move it on the vest:
            // 1. An angle in degrees [0, 360] to turn the pattern to the left
            // 2. A shift [-0.5, 0.5] in y-direction (up and down) to move it up or down
            if (suitDisabled) { return; }
            ScaleOption scaleOption = new ScaleOption(1f, 1f);
            RotationOption rotationOption = new RotationOption(xzAngle, yShift);
            hapticPlayer.SubmitRegisteredVestRotation(key, key, rotationOption, scaleOption);
        }

        public void GunRecoil(bool isRightHand, string recoilPrefix, float intensity = 1.0f, bool twoHanded=false, bool shoulderStock=false )
        {
            if (suitDisabled) { return; }
            float duration = 1.0f;
            var scaleOption = new ScaleOption(intensity, duration);
            var rotationFront = new RotationOption(0f, 0f);

            // assemble the name of the feedback pattern, first left and right
            string prefix = "Recoil";
            string postfix = "_L";
            string otherPostfix = "_R";
            if (isRightHand) { postfix = "_R"; otherPostfix = "_L"; }
            // add gun type to the pattern name
            prefix += recoilPrefix;
            // hands and arms patterns are the same, no matter if stock pressed against the shoulder
            string keyHand = prefix + "Hands" + postfix;
            string keyArm = prefix + "Arms" + postfix;
            string keyOtherArm = prefix + "Arms" + otherPostfix;
            string keyOtherHand = prefix + "Hands" + otherPostfix;
            // change vest pattern if stock is against shoulder
            if (shoulderStock) { prefix += "Shoulder"; }
            string keyVest = prefix + "Vest" + postfix;
            // always play back dominant arm and hand patterns
            hapticPlayer.SubmitRegisteredVestRotation(keyArm, keyArm, rotationFront, scaleOption);
            hapticPlayer.SubmitRegisteredVestRotation(keyHand, keyHand, rotationFront, scaleOption);
            // second hand/arm only if it grabs the gun
            if (twoHanded)
            {
                hapticPlayer.SubmitRegisteredVestRotation(keyOtherArm, keyOtherArm, rotationFront, scaleOption);
                hapticPlayer.SubmitRegisteredVestRotation(keyOtherHand, keyOtherHand, rotationFront, scaleOption);
            }
            // play back vest pattern
            hapticPlayer.SubmitRegisteredVestRotation(keyVest, keyVest, rotationFront, scaleOption);
        }

        public void SwordRecoil(bool isRightHand, float intensity = 1.0f)
        {
            // Melee feedback pattern
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

        public void HeadShot(float hitAngle)
        {
            // extra function for headshots to include Tactal interface
            if (suitDisabled) { return; }
            // just separate 4 hit directions
            if ((hitAngle<45f)|(hitAngle>315f)) { PlaybackHaptics("Headshot_F"); }
            if ((hitAngle > 45f) && (hitAngle < 135f)) { PlaybackHaptics("Headshot_L"); }
            if ((hitAngle > 135f) && (hitAngle < 225f)) { PlaybackHaptics("Headshot_B"); }
            if ((hitAngle > 225f) && (hitAngle < 315f)) { PlaybackHaptics("Headshot_R"); }
            // also play it back on the vest, at the very top.
            // This older C# bhaptics library cannot check if Tactal is connected
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
        }


    }
}
