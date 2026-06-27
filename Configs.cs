using BepInEx;
using BepInEx.Configuration;
using Dawn;
using Dusk;
using ItemSCPs;
using PSCPLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static SCP999.Plugin;

namespace SCP999
{
    public static class Configs
    {
        // SCP-999
        public static float size { get; private set; }
        public static int playerHealAmount { get; private set; }
        public static int enemyHealAmount { get; private set; }
        public static float playerDetectionRange { get; private set; }
        public static float enemyDetectionRange { get; private set; }
        public static float followRange { get; private set; }
        public static float huggingRange { get; private set; }
        public static int maxCandy { get; private set; }
        public static float insanityDecreaseRate { get; private set; }

        // Containment Jar
        public static int jar999Value { get; private set; }
        public static int jarSlimeValue { get; private set; }
        public static bool slimeTaming { get; private set; }

        public static void Init()
        {
            // SCP-999
            size = Instance.Config.Bind("SCP-999 Options", "Size", 1f, "How big SCP-999 is").Value;
            playerHealAmount = Instance.Config.Bind("SCP-999 Options", "Player Heal Amount", 10, "How much SCP-999 heals the player per second").Value;
            enemyHealAmount = Instance.Config.Bind("SCP-999 Options", "Enemy Heal Amount", 1, "How much SCP-999 heals an enemy per second").Value;
            playerDetectionRange = Instance.Config.Bind("SCP-999 Options", "Player Detection Range", 50f, "How far SCP-999 can detect a player when they are hurt").Value;
            enemyDetectionRange = Instance.Config.Bind("SCP-999 Options", "Enemy Detection Range", 15f, "How far SCP-999 can detect an enemy when they get hurt").Value;
            followRange = Instance.Config.Bind("SCP-999 Options", "Follow Range", 5f, "The distance in which SCP-999 will follow a player or enemy when following them").Value;
            huggingRange = Instance.Config.Bind("SCP-999 Options", "Hugging Range", 2f, "The distance in which SCP-999 will begin hugging and healing a player or enemy").Value;
            maxCandy = Instance.Config.Bind("SCP-999 Options", "Max Candy", 2, "Maximum amount of candy SCP-999 can eat before becoming hyper").Value;
            insanityDecreaseRate = Instance.Config.Bind("SCP-999 Options", "Insanity Decrease Rate", 5f, "How much insanity the player loses per second while SCP-999 is hugging/healing them").Value;

            // Containment Jar
            jar999Value = Instance.Config.Bind("Containment Jar Options", "SCP-999 Value", 1, "Value of a Containment Jar with SCP-999 inside it").Value;
            jarSlimeValue = Instance.Config.Bind("Containment Jar Options", "Slime Value", 50, "Value of a Containment Jar with slime inside it").Value;
            slimeTaming = Instance.Config.Bind("Containment Jar Options", "Slime Taming", true, "When enabled, releasing SCP-999 from the Containment Jar tames it to the player who released it. It will only follow that player unless it becomes hyper.").Value;
        }
    }
}
