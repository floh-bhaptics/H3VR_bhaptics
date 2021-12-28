using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace H3VR_bhaptics
{
    public class GunClasses
    {
        public static string recoilPrefixFromRoundType(FistVR.FireArmRoundType roundType, bool hasStock, bool twoHanded)
        {
            // Determining gun types by their rounds. This makes it possible to account for guns added via
            // mods, since the ammo types are compiled into the code.
            string prefix = "Default";
            string[] pistolRounds = { "a9_19_Parabellum", "a50ActionExpress", "a763x25mmMauser", "a8mmBergmann", "a765x25mmBorchardt", "a45_ACP",
                "a32ACP", "a45_ACP", "a50_Imaginary", "a357_Magnum", "a44_Magnum", "a22_LR", "a57x28mm", "a40_SW", "a25_ACP", "a9mmSteyr", "a9_18_Makarov",
                "a22WinchesterMagnum", "a38ACP", "a380_ACP", "a556_45_Nato", "a762_25_Tokarev", "a455WebleyAuto", "a792x57mmMauser", "a600MagnumBolt",
                "a45_70_Govt", "a38Special", "a454Casull", "a38Rimfire", "a357_Magnum", "a38Tround", "a106_25mmR", "a762x38mmR", "a762x42mm", "a22WinchesterMagnum",
                "a44_40Winchester", "a45_Colt", "a500SW", "a44_Magnum", "a455Webley", "a9mmSteyr", "a46x30mm", "a9_18_Makarov", "aCpbp", "a40_SW", "a25_ACP", "a380_ACP",
                "a762_54_Mosin", "a308_Winchester", "a41_Short"};
            string[] shotgunRounds = { "a12g_Shotgun", "a20g_Shotgun", "a3gauge", "a12gaugeShort", "a12GaugeBelted", "a40_46_Grenade" };
            string[] rocketRounds = { "aRPG7Rocket", "aM1A1Rocket", "aPanzerSchreckRocket" };
            string[] rifleRounds = { "a50_BMG", "a20x82mm", "a13_2mmTuF", "a408Cheytac", "a50_Remington_BP", "a50mmPotato" };
            string[] battleRifleRounds = { "a762_51_Nato", "a762_54_Mosin", "a3006_Springfield", "a75x54mmFrench", "a762_54_Mosin", "a300_Winchester_Magnum", "a338Lapua" };
            string[] assaultRifleRounds = { "a556_45_Nato", "a545_39_Soviet", "a762_39_Soviet", "a280British", "a58x42mm", "a792x33mmKurz", "a762_39_Soviet" };

            if (rocketRounds.Any(roundType.ToString().Contains)) { prefix = "Rocket"; }
            if (shotgunRounds.Any(roundType.ToString().Contains)) { prefix = "Shotgun"; }
            if (rifleRounds.Any(roundType.ToString().Contains)) { prefix = "BigRifle"; }
            if (battleRifleRounds.Any(roundType.ToString().Contains)) { prefix = "Rifle"; }
            if (assaultRifleRounds.Any(roundType.ToString().Contains)) { prefix = "Rifle"; }
            if ( (hasStock) ) { return prefix; }
            if (pistolRounds.Any(roundType.ToString().Contains)) { prefix = "Pistol"; }

            return prefix;
        }
    }
}
