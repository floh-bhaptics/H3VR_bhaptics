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
    [BepInPlugin("org.bepinex.plugins.H3VR_bhaptics", "H3VR bhaptics integration", "1.0")]
    public class Plugin : BaseUnityPlugin
    {
#pragma warning disable CS0109 // Element blendet kein vererbtes Element aus; neues Schlüsselwort erforderlich
        internal static new ManualLogSource Log;
#pragma warning restore CS0109 // Element blendet kein vererbtes Element aus; neues Schlüsselwort erforderlich
        public static TactsuitVR tactsuitVr;
        public static Vector3 playerPosition;
        public static float maxHealth = 0f;


        private void Awake()
        {
            Log = base.Logger;
            // Plugin startup logic
            Logger.LogMessage("Plugin H3VR_bhaptics is loaded!");
            tactsuitVr = new TactsuitVR();
            tactsuitVr.PlaybackHaptics("HeartBeat");
            var harmony = new Harmony("bhaptics.patch.h3vr");
            harmony.PatchAll();
        }

        #region Weapon recoil
        private static KeyValuePair<float, float> getAngleAndShift(FistVR.FVRPlayerBody player, Vector3 hit)
        {
            // bhaptics starts in the front, then rotates to the left. 0° is front, 90° is left, 270° is right.
            Vector3 patternOrigin = new Vector3(0f, 0f, 1f);
            // y is "up", z is "forward" in local coordinates
            Vector3 hitPosition = hit - player.TorsoTransform.position;
            Quaternion PlayerRotation = player.TorsoTransform.rotation;
            Vector3 playerDir = PlayerRotation.eulerAngles;
            Vector3 flattenedHit = new Vector3(hitPosition.x, 0f, hitPosition.z);
            float earlyhitAngle = Vector3.Angle(flattenedHit, patternOrigin);
            Vector3 earlycrossProduct = Vector3.Cross(flattenedHit, patternOrigin);
            if (earlycrossProduct.y > 0f) { earlyhitAngle *= -1f; }
            //tactsuitVr.LOG("EarlyHitAngle: " + earlyhitAngle.ToString());
            float myRotation = earlyhitAngle - playerDir.y;
            myRotation *= -1f;
            if (myRotation < 0f) { myRotation = 360f + myRotation; }
            //tactsuitVr.LOG("mHitAngle: " + myRotation.ToString());


            float hitShift = hitPosition.y;
            //tactsuitVr.LOG("HitShift: " + hitShift.ToString());
            if (hitShift > 0.0f) { hitShift = 0.5f; }
            else if (hitShift < -0.5f) { hitShift = -0.5f; }
            else { hitShift = (hitShift + 0.5f) * 2.0f - 0.5f; }
            //tactsuitVr.LOG("HitShift: " + hitShift.ToString());
            //tactsuitVr.LOG(" ");

            //tactsuitVr.LOG("Relative x-z-position: " + relativeHitDir.x.ToString() + " "  + relativeHitDir.z.ToString());
            //tactsuitVr.LOG("HitAngle: " + hitAngle.ToString());
            //tactsuitVr.LOG("HitShift: " + hitShift.ToString());

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

                if (fatalError)
                {
                    tactsuitVr.GunRecoil(isRightHand, "Pistol", 1.0f, (foregripStabilized | twoHandStabilized), shoulderStabilized);
                    return;
                }
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
                float scaledRecoil = (float)Math.Sqrt((double)myRecoil.XYLinearPerShot) + 0.55f;
                intensity = Math.Min(scaledRecoil, 1.0f);

                if (recoilPrefix == "Default")
                {
                    if ((hasStock) | (twoHanded)) { recoilPrefix = "Rifle"; }
                    else { recoilPrefix = "Pistol"; }
                }

                if (gunName.Contains("BreakActionShotgunTheOG")) { recoilPrefix = "HolyMoly"; intensity = 1.0f; }

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
                if (collideWith.Contains("Capsule") | collideWith.Contains("Mag")) { return; }
                //tactsuitVr.LOG("Colliding with: " + col.collider.name);
                bool twohanded = __instance.IsAltHeld;
                bool isRightHand = __instance.m_hand.IsThisTheRightHand;
                float speed = col.relativeVelocity.magnitude;
                if (speed <= 1.0f) { return; }
                float intensity = Math.Min(0.2f + speed / 5.0f, 1.0f);
                // tactsuitVr.LOG("Intensity: " + intensity.ToString());
                tactsuitVr.SwordRecoil(isRightHand, intensity);

            }
        }

        #endregion

        #region Player Body
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
                // d.point;
                // d.strikeDir
                FistVR.FVRPlayerBody myBody = __instance.Body;
                var angleShift = getAngleAndShift(myBody, d.point);
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
                if (angleShift.Value == 0.5f) { tactsuitVr.HeadShot(angleShift.Key); }
                else { tactsuitVr.PlayBackHit(feedbackKey, angleShift.Key, angleShift.Value); }

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
                //FistVR.PowerupType.
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
                tactsuitVr.LOG("Player killed.");
                tactsuitVr.StopThreads();
            }
        }

        [HarmonyPatch(typeof(FistVR.FVRPlayerBody), "KillPlayer", new Type[] { typeof(bool) })]
        public class bhaptics_PlayerKilled
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                tactsuitVr.LOG("Player killed.");
                maxHealth = 0;
                tactsuitVr.StopThreads();
            }
        }

        [HarmonyPatch(typeof(FistVR.FVRPlayerBody), "Update")]
        public class bhaptics_PlayerBodyUpdate
        {
            [HarmonyPostfix]
            public static void Postfix(FistVR.FVRPlayerBody __instance)
            {
                playerPosition = __instance.transform.position;
                float health = __instance.Health;
                if (health > maxHealth) { maxHealth = health; }
                if (health < maxHealth/3.0f) { tactsuitVr.StartHeartBeat(); }
                else { tactsuitVr.StopHeartBeat(); }
                //tactsuitVr.StopThreads();
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
                //tactsuitVr.LOG("Grenade distance: " + distance.ToString());
                float intensity = Math.Max(((40.0f - distance) / 40.0f), 0.0f);
                tactsuitVr.PlaybackHaptics("ExplosionBelly", intensity);
            }
        }

        #endregion

    }
}

