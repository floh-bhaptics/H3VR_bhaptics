using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

using UnityEngine;

using MyBhapticsTactsuit;

namespace H3VR_bhaptics
{
    [BepInPlugin("org.bepinex.plugins.H3VR_bhaptics", "H3VR bhaptics integration", "1.3")]
    public class Plugin : BaseUnityPlugin
    {
#pragma warning disable CS0109 // Remove unnecessary warning
        internal static new ManualLogSource Log;
#pragma warning restore CS0109
        public static TactsuitVR tactsuitVr;
        public static Vector3 playerPosition;
        // I couldn't find a way to read out the max health. So this is a global variable hack that
        // will just store the maximum health ever read.
        public static float maxHealth = 0f;


        private void Awake()
        {
            // Make my own logger so it can be accessed from the Tactsuit class
            Log = base.Logger;
            // Plugin startup logic
            Logger.LogMessage("Plugin H3VR_bhaptics is loaded!");
            tactsuitVr = new TactsuitVR();
            // one startup heartbeat so you know the vest works correctly
            tactsuitVr.PlaybackHaptics("HeartBeat");
            // patch all functions
            var harmony = new Harmony("bhaptics.patch.h3vr");
            harmony.PatchAll();
        }

        #region Weapon recoil
        private static KeyValuePair<float, float> getAngleAndShift(FistVR.FVRPlayerBody player, Vector3 hit)
        {
            // bhaptics starts in the front, then rotates to the left. 0° is front, 90° is left, 270° is right.
            // y is "up", z is "forward" in local coordinates
            Vector3 patternOrigin = new Vector3(0f, 0f, 1f);
            Vector3 hitPosition = hit - player.TorsoTransform.position;
            Quaternion PlayerRotation = player.TorsoTransform.rotation;
            Vector3 playerDir = PlayerRotation.eulerAngles;
            // get rid of the up/down component to analyze xz-rotation
            Vector3 flattenedHit = new Vector3(hitPosition.x, 0f, hitPosition.z);

            // get angle. .Net < 4.0 does not have a "SignedAngle" function...
            float earlyhitAngle = Vector3.Angle(flattenedHit, patternOrigin);
            // check if cross product points up or down, to make signed angle myself
            Vector3 earlycrossProduct = Vector3.Cross(flattenedHit, patternOrigin);
            if (earlycrossProduct.y > 0f) { earlyhitAngle *= -1f; }
            // relative to player direction
            float myRotation = earlyhitAngle - playerDir.y;
            // switch directions (bhaptics angles are in mathematically negative direction)
            myRotation *= -1f;
            // convert signed angle into [0, 360] rotation
            if (myRotation < 0f) { myRotation = 360f + myRotation; }

            // up/down shift is in y-direction
            float hitShift = hitPosition.y;
            // in H3VR, the TorsoTransform has y=0 at the neck,
            // and the torso ends at roughly -0.5 (that's in meters)
            // so cap the shift to [-0.5, 0]...
            if (hitShift > 0.0f) { hitShift = 0.5f; }
            else if (hitShift < -0.5f) { hitShift = -0.5f; }
            // ...and then spread/shift it to [-0.5, 0.5]
            else { hitShift = (hitShift + 0.5f) * 2.0f - 0.5f; }

            //tactsuitVr.LOG("Relative x-z-position: " + relativeHitDir.x.ToString() + " "  + relativeHitDir.z.ToString());
            //tactsuitVr.LOG("HitAngle: " + hitAngle.ToString());
            //tactsuitVr.LOG("HitShift: " + hitShift.ToString());

            // No tuple returns available in .NET < 4.0, so this is the easiest quickfix
            return new KeyValuePair<float, float>(myRotation, hitShift);
        }


        [HarmonyPatch(typeof(FistVR.FVRFireArm), "Recoil")]
        public class bhaptics_RecoilGun
        {
            [HarmonyPostfix]
            public static void Postfix(FistVR.FVRFireArm __instance, bool twoHandStabilized, bool foregripStabilized, bool shoulderStabilized)
            {
                string gunName = "";
                string recoilPrefix;
                bool fatalError = false;
                bool hasStock = false;
                bool twoHanded = false;
                bool inWater = false;
                bool isRightHand = true;
                float intensity;
                FistVR.FVRFireArmRecoilProfile myRecoil;
                FistVR.FireArmRoundType myBulletType;

                // Not all guns, especially mods, have all properties. So check individually, 
                // log, and if crucial information can't be read, give up...
                try { gunName = __instance.name; }
                catch { tactsuitVr.LOG("Gun name not found."); }
                try { hasStock = __instance.HasActiveShoulderStock; }
                catch { tactsuitVr.LOG("Gun stock info not found."); }
                try { twoHanded = __instance.Foregrip.activeSelf; }
                catch { tactsuitVr.LOG("Gun foregrip info not found."); }
                try { inWater = __instance.IsInWater; }
                catch { tactsuitVr.LOG("In water not found."); }
                try { isRightHand = __instance.m_hand.IsThisTheRightHand; }
                catch { tactsuitVr.LOG("Hand holding gun not found!"); return; }
                try { myRecoil = __instance.RecoilProfile; }
                catch { tactsuitVr.LOG("Recoil profile not found."); fatalError = true; myRecoil = new FistVR.FVRFireArmRecoilProfile(); }
                try { myBulletType = __instance.RoundType; }
                catch { tactsuitVr.LOG("Round type not found."); fatalError = true; myBulletType = new FistVR.FireArmRoundType(); }

                // If I can't find the recoil strength or round, playing back nothing is better than something really wrong.
                if (fatalError)
                {
                    tactsuitVr.GunRecoil(isRightHand, "Pistol", 1.0f, (foregripStabilized | twoHandStabilized), shoulderStabilized);
                    return;
                }

                // Logging from when I was figuring things out
                /*
                tactsuitVr.LOG("Gun name: " + gunName);
                tactsuitVr.LOG("has stock: " + hasStock.ToString());
                tactsuitVr.LOG("twohanded: " + twoHanded.ToString());
                tactsuitVr.LOG("Recoil in XY: " + myRecoil.XYLinearPerShot.ToString());
                tactsuitVr.LOG("Recoil in Z: " + myRecoil.ZLinearPerShot.ToString());
                tactsuitVr.LOG("Gun class: " + GunClasses.recoilPrefixFromRoundType(myBulletType, hasStock, twoHanded));
                tactsuitVr.LOG("ShoulderStock: " + shoulderStabilized.ToString());
                tactsuitVr.LOG("foreGripStabilized: " + foregripStabilized.ToString());
                tactsuitVr.LOG("twoHandStabilized: " + twoHandStabilized.ToString());
                tactsuitVr.LOG("RoundType: " + myBulletType.ToString());
                tactsuitVr.LOG(" ");
                */

                recoilPrefix = GunClasses.recoilPrefixFromRoundType(myBulletType, hasStock, twoHanded);
                // Most XY-recoil is in [0, 0.2], but scaled in a way where most guns are very close
                // to 0 and very few extreme guns are at 0.2, so I am doing the sqrt to spread that
                // out more evenly, and then stretch the interval to [0.55, 1.0], which seems like
                // a good intensity area.
                float scaledRecoil = (float)Math.Sqrt((double)myRecoil.XYLinearPerShot) + 0.55f;
                // Make sure it's not above 1.0
                intensity = Math.Min(scaledRecoil, 1.0f);

                // If we couldn't determine the gun type, go for Pistol or Rifle, depending on
                // stock or foregrip
                if (recoilPrefix == "Default")
                {
                    if ((hasStock) | (twoHanded)) { recoilPrefix = "Rifle"; }
                    else { recoilPrefix = "Pistol"; }
                }

                // Special case for "The OG" shotgun
                if (gunName.Contains("BreakActionShotgunTheOG")) { recoilPrefix = "HolyMoly"; intensity = 1.0f; }

                // Finally call recoil playback with all gathered parameters
                tactsuitVr.GunRecoil(isRightHand, recoilPrefix, intensity, (foregripStabilized | twoHandStabilized), shoulderStabilized);
            }
        }

        [HarmonyPatch(typeof(FistVR.FVRPhysicalObject))]
        [HarmonyPatch("OnCollisionEnter")]
        [HarmonyPatch(new Type[] { typeof(Collision) })]
        public class bhaptics_MeleeCollider
        {
            [HarmonyPostfix]
            public static void Postfix(FistVR.FVRPhysicalObject __instance, Collision col)
            {
                if (!__instance.IsHeld) { return; }
                if (!__instance.MP.IsMeleeWeapon) { return; }
                string collideWith = col.collider.name;
                // Collision with shells or mags shouldn't trigger feedback. Guns are "melee" as well.
                if (collideWith.Contains("Capsule") | collideWith.Contains("Mag")) { return; }
                bool twohanded = __instance.IsAltHeld;
                bool isRightHand = __instance.m_hand.IsThisTheRightHand;
                float speed = col.relativeVelocity.magnitude;
                // Also ignore very light bumps 
                if (speed <= 1.0f) { return; }
                // Scale feedback with the speed of the collision
                float intensity = Math.Min(0.2f + speed / 5.0f, 1.0f);
                tactsuitVr.SwordRecoil(isRightHand, intensity);
            }
        }

        #endregion

        #region Player Body

        // Tried to find the holster function. Did not yet succeed.
        [HarmonyPatch(typeof(FistVR.FVRFireArmBeltSegment), "EndInteraction", new Type[] { typeof(FistVR.FVRViveHand) })]
        public class bhaptics_HolsterSomething
        {
            [HarmonyPostfix]
            public static void Postfix(FistVR.FVRViveHand hand)
            {
                if (hand.IsThisTheRightHand) { tactsuitVr.PlaybackHaptics("Holster_R"); }
                else { tactsuitVr.PlaybackHaptics("Holster_L"); }
            }
        }

        #endregion

        #region Player damage

        [HarmonyPatch(typeof(FistVR.FVRPlayerHitbox))]
        [HarmonyPatch("Damage")]
        [HarmonyPatch(new Type[] { typeof(FistVR.Damage) })]
        public class bhaptics_DamageDealtHitbox
        {
            [HarmonyPostfix]
            public static void Postfix(FistVR.FVRPlayerHitbox __instance, FistVR.Damage d)
            {
                // Get XZ-angle and y-shift of hit
                FistVR.FVRPlayerBody myBody = __instance.Body;
                var angleShift = getAngleAndShift(myBody, d.point);

                // Different hit patterns for different damage classes
                string feedbackKey = "BulletHit";
                switch (d.Class)
                {
                    case FistVR.Damage.DamageClass.Projectile:
                        feedbackKey = "BulletHit";
                        break;
                    case FistVR.Damage.DamageClass.Melee:
                        feedbackKey = "BladeHit";
                        break;
                    case FistVR.Damage.DamageClass.Explosive:
                        feedbackKey = "Impact";
                        break;
                    case FistVR.Damage.DamageClass.Environment:
                        feedbackKey = "Impact";
                        break;
                    case FistVR.Damage.DamageClass.Abstract:
                        feedbackKey = "BulletHit";
                        break;
                    default:
                        break;
                }

                // If it's at the very top, play back a headshot
                if (angleShift.Value == 0.5f) { tactsuitVr.HeadShot(angleShift.Key); }
                else { tactsuitVr.PlayBackHit(feedbackKey, angleShift.Key, angleShift.Value); }

                // Logging from when I tried to figure things out
                //tactsuitVr.LOG("Dealt Body position: " + myBody.TorsoTransform.position.x.ToString() + " " + myBody.TorsoTransform.position.y.ToString() + " " + myBody.TorsoTransform.position.z.ToString());
                //tactsuitVr.LOG("Dealt Hitpoint: " + d.point.x.ToString() + " " + d.point.y.ToString() + " " + d.point.z.ToString());
                //tactsuitVr.LOG("Dealt StrikeDir: " + d.strikeDir.x.ToString() + " " + d.strikeDir.y.ToString() + " " + d.strikeDir.z.ToString());
            }
        }

        #endregion

        #region World interaction

        [HarmonyPatch(typeof(FistVR.FVRSceneSettings), "OnPowerupUse", new Type[] { typeof(FistVR.PowerupType) })]
        public class bhaptics_PowerUpUse
        {
            [HarmonyPostfix]
            public static void Postfix(FistVR.PowerupType type)
            {
                // Powerup special effects in Take & Hold mode
                switch (type)
                {
                    case FistVR.PowerupType.Health:
                        tactsuitVr.PlaybackHaptics("Healing");
                        break;
                    case FistVR.PowerupType.Explosive:
                        tactsuitVr.PlaybackHaptics("ExplosionFace");
                        break;
                    case FistVR.PowerupType.InfiniteAmmo:
                        tactsuitVr.PlaybackHaptics("InfiniteAmmo");
                        break;
                    case FistVR.PowerupType.Invincibility:
                        tactsuitVr.PlaybackHaptics("Invincibility");
                        break;
                    case FistVR.PowerupType.QuadDamage:
                        tactsuitVr.PlaybackHaptics("QuadDamage");
                        break;
                    case FistVR.PowerupType.SpeedUp:
                        tactsuitVr.PlaybackHaptics("HeartBeatFast");
                        break;
                    case FistVR.PowerupType.Regen:
                        tactsuitVr.PlaybackHaptics("Healing");
                        break;
                    case FistVR.PowerupType.MuscleMeat:
                        tactsuitVr.PlaybackHaptics("MuscleMeat");
                        break;
                    case FistVR.PowerupType.Ghosted:
                        tactsuitVr.PlaybackHaptics("Ghosted");
                        break;
                    case FistVR.PowerupType.Cyclops:
                        tactsuitVr.PlaybackHaptics("Cyclops");
                        break;
                    default:
                        break;
                }
            }
        }

        [HarmonyPatch(typeof(FistVR.FVRPlayerBody), "KillPlayer", new Type[] { typeof(bool) })]
        public class bhaptics_PlayerKilled
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                tactsuitVr.LOG("Player killed.");
                // Reset in case there is a new maximum health setting in the next round
                maxHealth = 0;
                // Stop heart beat if player is killed.
                tactsuitVr.StopThreads();
            }
        }

        [HarmonyPatch(typeof(FistVR.FVRPlayerBody), "Update")]
        public class bhaptics_PlayerBodyUpdate
        {
            [HarmonyPostfix]
            public static void Postfix(FistVR.FVRPlayerBody __instance)
            {
                // I can't get to the world player position in the grenade explosion
                // function, so just store it globally on update
                playerPosition = __instance.transform.position;

                float health = __instance.Health;
                // Set max health
                if (health > maxHealth) { maxHealth = health; }
                // if health drops below 1/3 of max, start heart beat
                if (health < maxHealth/3.0f) { tactsuitVr.StartHeartBeat(); }
                else { tactsuitVr.StopHeartBeat(); }
            }
        }

        [HarmonyPatch(typeof(FistVR.FVRMovementManager), "RocketJump")]
        public class bhaptics_RocketJump
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                tactsuitVr.PlaybackHaptics("RocketJump");
            }
        }

        [HarmonyPatch(typeof(FistVR.ZosigGameManager), "VomitObject", new Type[] { typeof(FistVR.FVRObject) })]
        public class bhaptics_VomitObject
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                //tactsuitVr.LOG("Vomit");
                tactsuitVr.PlaybackHaptics("Vomit");
            }
        }

        [HarmonyPatch(typeof(FistVR.ZosigGameManager), "EatBangerJunk")]
        public class bhaptics_EatBangerJunk
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                tactsuitVr.PlaybackHaptics("Eating");
            }
        }

        [HarmonyPatch(typeof(FistVR.ZosigGameManager), "EatHerb")]
        public class bhaptics_EatHerb
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                tactsuitVr.PlaybackHaptics("Eating");
            }
        }

        [HarmonyPatch(typeof(FistVR.ZosigGameManager), "EatMeatCore")]
        public class bhaptics_EatMeatCore
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                tactsuitVr.PlaybackHaptics("Eating");
            }
        }

        [HarmonyPatch(typeof(FistVR.GrenadeExplosion), "Explode")]
        public class bhaptics_GrenadeExplosion
        {
            [HarmonyPostfix]
            public static void Postfix(FistVR.GrenadeExplosion __instance)
            {
                Vector3 grenadePosition = __instance.transform.position;
                float distance = (grenadePosition - playerPosition).magnitude;
                // if grenade is more than 40 meters away, ignore explosion.
                // otherwise scale feedback. If close enough, this is in *addition*
                // to the explosion damage feedback
                float intensity = Math.Max(((40.0f - distance) / 40.0f), 0.0f);
                tactsuitVr.PlaybackHaptics("ExplosionBelly", intensity);
            }
        }

        #endregion

    }
}

