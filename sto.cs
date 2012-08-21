using System;
using System.Reflection;
using Advanced_Combat_Tracker;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Text;
using System.Globalization;
using System.Collections;
using System.Collections.Specialized;

[assembly: AssemblyTitle("STO Parsing Plugin")]
[assembly: AssemblyDescription("A basic parser that reads the combat logs in Star Trek Online")]
[assembly: AssemblyCopyright("Aria@Abydos1")]
[assembly: AssemblyVersion("0.9.1.0")]

namespace Parsing_Plugin
{

    public class STO_Parser : IActPluginV1
    {

        private static string[] separatorLog = new string[] { "::", "," };
        private static CultureInfo cultureLog = new CultureInfo("en-US");
        private static CultureInfo cultureDisplay = new CultureInfo("de-DE");

        private static StringDictionary petOwners = new StringDictionary();

        private static string[] petNames = new string[] { "Peregrine Fighter", "Type 8 Shuttle", "Type 10 Shuttle", "Danube Runabout", "Delta Flyer", "Stalker Fighter", "Shield Repair Unit", "To'Duj Fighter", "S'Kul Fighter", "Orion Interceptor", "Marauding Force", "Orion Slaver", "Advanced Slaver", "Power Siphon Drone", "Tachyon Drone", "Bird-of-Prey", "Fer'Jai Frigate", "Tholian Widow Fighter", "Mine", "Tyken's Rift", "Boarding Party", "Repair Drone", "Assimilated Fighter", "Torpedo", "Aceton Assimilator", "Tholian Web", "Turret", "Bio-neural Warhead" };
        private static string[] weaponTypes = new string[] { "Turret", "Cannon", "Array", "Banks", "Torpedo" };
        private static string[] weaponExclusion = new string[] { "Point Defense", " - " };

        private class DamageEvent
        {
            public DateTime timestamp;
            public int sortId;
            public LogLineEventArgs logArgs;

            public string ownerDisplay;
            public string ownerInternal;
            public string sourceDisplay;
            public string sourceInternal;
            public string targetDisplay;
            public string targetInternal;
            public string eventDisplay;
            public string eventInternal;

            public string type;
            public string flags;

            public float magnitude;
            public float magnitudeBase;

            public bool critical = false;
            public int swingType = (int)SwingTypeEnum.Melee;

            public DamageEvent(LogLineEventArgs logInfo)
            {
                string[] split;
                split = logInfo.logLine.Split(separatorLog, StringSplitOptions.None);

                DateTime.TryParseExact(split[0], "yy:MM:dd:HH:mm:ss.f::", cultureDisplay, DateTimeStyles.AssumeLocal, out timestamp);

                ownerDisplay = split[1];
                ownerInternal = split[2];
                sourceDisplay = split[3];
                sourceInternal = split[4];
                targetDisplay = split[5];
                targetInternal = split[6];
                eventDisplay = split[7];
                eventInternal = split[8];
                type = split[9];
                flags = split[10];
                magnitude = float.Parse(split[11], cultureLog);
                magnitudeBase = float.Parse(split[12], cultureLog);

                if (ownerDisplay == "" || ownerDisplay == "*") { ownerDisplay = sourceDisplay; ownerInternal = sourceInternal; }
                if (sourceDisplay == "" || sourceDisplay == "*") { sourceDisplay = ownerDisplay; sourceInternal = ownerInternal; }
                if (targetDisplay == "" || targetDisplay == "*") { targetDisplay = sourceDisplay; targetInternal = sourceInternal; }

                if (internalIsPlayer(ownerInternal)) ownerDisplay = getAccountName(ownerInternal);
                if (internalIsPlayer(sourceInternal)) sourceDisplay = getAccountName(sourceInternal);
                if (internalIsPlayer(targetInternal)) targetDisplay = getAccountName(targetInternal);

                if (ownerDisplay == "")
                    ownerDisplay = "UNKNOWN";

                logArgs = logInfo;
            }

            static public string getAccountName(string player)
            {
                int i = player.LastIndexOf('@');
                if (i == -1)
                    return player;
                return player.Substring(i, player.Length - i - 1);
            }

            static public bool internalIsPlayer(string s)
            {
                return (s != "" && s[0] == 'P');
            }

            public bool isPetEvent()
            {
                if (ownerInternal != "" && ownerInternal[0] == 'P' && sourceInternal != "" && sourceInternal[0] == 'C' && sourceInternal != targetInternal)
                {
                    foreach (string s in petNames)
                    {
                        if (sourceDisplay.Contains(s))
                            return true;
                    }
                }
                return false;
            }

            public bool isMelee()
            {
                foreach (string s in weaponTypes)
                {
                    if (eventDisplay.Contains(s))
                    {
                        foreach (string e in weaponExclusion)
                        {
                            if (eventDisplay.Contains(e))
                                return false;
                        }
                        return true;
                    }
                }
                return false;
            }

            public bool isOwnerPetType()
            {
                foreach (string s in petNames)
                {
                    if (ownerDisplay.Contains(s))
                        return true;
                }
                return false;
            }

            public void setPetOwner(string owner)
            {
                eventDisplay = ownerDisplay + " - " + eventDisplay;
                ownerDisplay = owner;
            }

            public void processData()
            {
                if (eventDisplay == "Chaotic Particle Stream" && magnitudeBase == 0)
                    magnitudeBase = magnitude;

                if (flags.Contains("Critical"))
                    critical = true;

                if (!isMelee())
                    swingType = (int)SwingTypeEnum.NonMelee;
            }

            public bool isPlayer()
            {
                return ((type == "Shield" && ownerInternal != "" && ownerInternal[0] == 'P') || (type != "Shield" && sourceInternal != "" && sourceInternal[0] == 'P'));
            }

            public bool ownerIsPlayer()
            {
                return (ownerInternal != "" && ownerInternal[0] == 'P');
            }

            public void addEvent()
            {
                if (eventDisplay == "")
                    eventDisplay = "Unknown";

                if (!ownerIsPlayer() && isOwnerPetType())
                    ownerDisplay = ownerInternal;

                if (flags.Contains("Miss"))
                {
                    if (ActGlobals.oFormActMain.SetEncounter(logArgs.detectedTime, ownerDisplay, targetDisplay))
                    {
                        ActGlobals.oFormActMain.AddCombatAction(swingType, critical, "None", ownerDisplay, eventDisplay, new Dnum(Dnum.Miss, "miss"), logArgs.detectedTime, ActGlobals.oFormActMain.GlobalTimeSorter, targetDisplay, type);
                    }
                }
                else if (magnitude < 0 && magnitudeBase == 0)
                {
                    if (ActGlobals.oFormActMain.SetEncounter(logArgs.detectedTime, ownerDisplay, targetDisplay))
                    {
                        ActGlobals.oFormActMain.AddCombatAction((int)SwingTypeEnum.Healing, critical, "None", ownerDisplay, eventDisplay, -(int)magnitude, logArgs.detectedTime, ActGlobals.oFormActMain.GlobalTimeSorter, targetDisplay, type);
                    }
                }
                else if (type == "Shield")
                {
                    if (ActGlobals.oFormActMain.SetEncounter(logArgs.detectedTime, ownerDisplay, targetDisplay))
                    {
                        ActGlobals.oFormActMain.AddCombatAction(swingType, critical, "None", ownerDisplay, eventDisplay, -(int)magnitude, logArgs.detectedTime, ActGlobals.oFormActMain.GlobalTimeSorter, targetDisplay, type);
                    }
                }
                else if (magnitude >= 0)
                {
                    if (ActGlobals.oFormActMain.SetEncounter(logArgs.detectedTime, ownerDisplay, targetDisplay))
                    {
                        ActGlobals.oFormActMain.AddCombatAction(swingType, critical, "None", ownerDisplay, eventDisplay, (int)magnitude, logArgs.detectedTime, ActGlobals.oFormActMain.GlobalTimeSorter, targetDisplay, type);
                    }
                }
            }
        };

        Label lblStatus = null;

        private static ArrayList pendingPetEvents = new ArrayList();

        private static int pendingPetEventsCounter = 0;

        public string getPetOwner(string pet)
        {
            if (!petOwners.ContainsKey(pet))
            {
                return null;
            }
            return petOwners[pet];
        }

        public void registerPet(string pet, string owner)
        {
            if (!petOwners.ContainsKey(pet))
            {
                petOwners.Add(pet, owner);
            }
        }

        public void processPendingPetEvents()
        {
            for (int i = 0; i < pendingPetEvents.Count; ++i)
            {
                DamageEvent e = (DamageEvent)pendingPetEvents[i];
                string owner = getPetOwner(e.ownerInternal);
                if (owner != null)
                {
                    e.setPetOwner(owner);
                    e.addEvent();
                    pendingPetEvents.RemoveAt(i);
                }

            }
        }

        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            // Setting this Regex will allow ACT to extract the character's name from the file name as the first capture group
            // when opening new log files. We'll say the log file name may look like "20080706-Player.log"
            ActGlobals.oFormActMain.LogPathHasCharName = false;

            // A windows file system filter to search updated log files with.
            ActGlobals.oFormActMain.LogFileFilter = "Combat*.log";

            // If all log files are in a single folder, this isn't an issue. If log files are split into different folders,
            // enter the parent folder name here. This way ACT will monitor that base folder and all sub-folders for updated files.
            ActGlobals.oFormActMain.LogFileParentFolderName = "GameClient";

            // Then to apply the settings and restart the log checking thread
            ActGlobals.oFormActMain.ResetCheckLogs();

            // This is the absolute path of where you wish ACT generated macro exports to be put. I'll leave it up to you
            // to determine this path programatically. If left blank, ACT will attempt to find EQ2 by registry or log file parents.
            // ActGlobals.oFormActMain.GameMacroFolder = @"C:\Program Files\Game Company\Game Folder";
            // Lets say that the log file time stamp is like: "[13:42:57]"
            // ACT needs to know the length of the timestamp and spacing at the beginning of the log line
            ActGlobals.oFormActMain.TimeStampLen = 19; // Remember to include spaces after the time stamp

            // Replace ACT's default DateTime parser with your own implementation matching your format
            ActGlobals.oFormActMain.GetDateTimeFromLog = new FormActMain.DateTimeLogParser(ParseDateTime);

            // This Regex is only used by a quick parsing method to find the current zone name based on a file position
            // If you do not define this correctly, the quick parser will fail and take a while to do so.
            // You still need to implement a zone change parser in your engine regardless of this
            // ActGlobals.oFormActMain.ZoneChangeRegex = new Regex(@"You have entered: (.+)\.", RegexOptions.Compiled);
            // All of your parsing engine will be based off of this event
            // You should not use Before/AfterCombatAction as you may enter infinite loops. AfterLogLineRead is okay, but not recommended
            ActGlobals.oFormActMain.BeforeLogLineRead += new LogLineEventDelegate(oFormActMain_BeforeLogLineRead);


            lblStatus = pluginStatusText;
            lblStatus.Text = "STO ACT plugin loaded";
        }

        void oFormActMain_BeforeLogLineRead(bool isImport, LogLineEventArgs logInfo)
        {
            // Vorhandensein von :: als Zeichen für ein STO-Log nehmen
            if (!logInfo.logLine.Contains("::")) return;


            DamageEvent eventData = new DamageEvent(logInfo);
            eventData.sortId = ++ActGlobals.oFormActMain.GlobalTimeSorter;
            eventData.processData();

            if (eventData.isPetEvent())
            {
                registerPet(eventData.sourceInternal, eventData.ownerDisplay);
            }

            if (!eventData.ownerIsPlayer() && eventData.isOwnerPetType())
            {
                string owner = getPetOwner(eventData.ownerInternal);
                if (owner == null)
                {
                    pendingPetEvents.Add(eventData);
                }
                else
                {
                    eventData.setPetOwner(owner);
                    eventData.addEvent();
                }
            }
            else
            {
                eventData.addEvent();
            }

            if (++pendingPetEventsCounter > 20)
            {
                processPendingPetEvents();
            }
        }

        private DateTime ParseDateTime(string FullLogLine)
        {
            return DateTime.ParseExact(FullLogLine.Substring(0, 19), "yy:MM:dd:HH:mm:ss.f", System.Globalization.CultureInfo.InvariantCulture);
        }

        public void DeInitPlugin()
        {
            ActGlobals.oFormActMain.BeforeLogLineRead -= oFormActMain_BeforeLogLineRead;
            lblStatus.Text = "STO ACT plugin unloaded";
        }
    }
}