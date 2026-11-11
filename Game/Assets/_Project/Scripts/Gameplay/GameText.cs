using System.Globalization;

namespace Ward0.Gameplay
{
    /// <summary>Why a shift ended in failure. Picks the result-screen title, explanation and advice in <see cref="GameText"/>.</summary>
    public enum FailCause { Other, ClearedInfected, Patrol, Reactor, Mind }

    /// <summary>
    /// Every player-facing string in the game lives here (prompts, objective line, messages, hints, result screens).
    /// Tone: a quarantine checkpoint terminal - cold, short, procedural. Each message says what happened, why and the next action.
    /// Gameplay scripts never build text themselves: they call a member of this class. Parameterised strings use the
    /// <c>Fmt*</c> templates so a translation only has to replace this file.
    /// </summary>
    public static class GameText
    {
        private static string F(string template, params object[] args) { return string.Format(CultureInfo.InvariantCulture, template, args); }
        private static string Cap(string s) { return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }

        // ------------------------------------------------------------ HUD labels
        public const string ObjectiveCaption = "OBJECTIVE";
        public const string HintCaption = "HINT";
        public const string HintHideNote = "H  hide hints";
        public const string HintsOn = "Hints on. Press H to hide them.";
        public const string HintsOff = "Hints off. Press H to turn them back on.";
        public const string KeyInteract = "E";
        public const string KeyHold = "HOLD";
        public const string TorchLabel = "TORCH  [F]";
        public const string StaminaLabel = "STAMINA";
        public const string ControlsHint = "WASD move   Space jump   Shift sprint (loud)   Ctrl crouch (quiet)   E interact   F torch   V see yourself   H hints";
        public const string Restart = "Press R to restart the shift";
        public const string NextCaption = "NEXT";
        public const string WaypointElevator = "ELEVATOR";

        private const string FmtReactor = "REACTOR  {0}";
        private const string FmtWaypoint = "{0}  {1} m";
        /// <summary>Reactor countdown line, e.g. "REACTOR  9:35".</summary>
        public static string Reactor(string time) { return F(FmtReactor, time); }
        /// <summary>Waypoint label with the distance in whole metres.</summary>
        public static string Waypoint(string label, int metres) { return F(FmtWaypoint, label, metres); }

        // ------------------------------------------------------------ objective line (top-left)
        private const string FmtObjTriage = "Triage: {0} / {1} miners processed";
        private const string FmtObjMine = "Restore systems: {0} / {1}  (breakers and valves)";
        public const string ObjDescent = "Take the elevator down  (west side of the booth)";
        public const string ObjReturn = "Return to the elevator and go up";
        public const string ObjComplete = "Shift complete";
        public const string ObjFailed = "Shift failed";

        /// <summary>Objective line for the given phase and progress.</summary>
        public static string Objective(ShiftPhase phase, int minersDone, int minersTotal, int repairs, int repairsTotal)
        {
            switch (phase)
            {
                case ShiftPhase.Triage: return F(FmtObjTriage, minersDone, minersTotal);
                case ShiftPhase.Descent: return ObjDescent;
                case ShiftPhase.Mine: return F(FmtObjMine, repairs, repairsTotal);
                case ShiftPhase.Return: return ObjReturn;
                case ShiftPhase.Complete: return ObjComplete;
                default: return ObjFailed;
            }
        }

        // ------------------------------------------------------------ interaction prompts (the HUD draws the key cap)
        public const string PromptPowerOn = "Switch MAIN POWER on";
        public const string PromptPowerOff = "Switch MAIN POWER off";
        public const string PromptXRayOn = "Switch X-RAY on";
        public const string PromptXRayOff = "Switch X-RAY off";
        public const string PromptBioscanOn = "Switch BIOSCAN on";
        public const string PromptBioscanOff = "Switch BIOSCAN off";
        public const string PromptAnalysis = "Run ANALYSIS";
        public const string PromptLog = "Read LOG";
        public const string PromptClear = "CLEAR the miner  (certify healthy)";
        public const string PromptIncinerate = "Pull the red lever: INCINERATE the miner";
        public const string PromptElevatorCall = "Call elevator";
        public const string PromptDoorOpen = "Open door";
        public const string PromptDoorClose = "Close door";
        public const string PromptDoorLocked = "Door locked";
        private const string FmtPromptRepair = "Repair {0}";
        private const string FmtPromptRepaired = "{0} restored";
        private const string FmtPromptBattery = "Take battery  (+{0} s torch)";

        /// <summary>Prompt for one of the six desk switches in its current state.</summary>
        public static string PromptDesk(DeskRole role, bool on)
        {
            switch (role)
            {
                case DeskRole.Power: return on ? PromptPowerOff : PromptPowerOn;
                case DeskRole.XRay: return on ? PromptXRayOff : PromptXRayOn;
                case DeskRole.Bioscan: return on ? PromptBioscanOff : PromptBioscanOn;
                case DeskRole.Analysis: return PromptAnalysis;
                case DeskRole.Log: return PromptLog;
                default: return PromptClear;
            }
        }
        /// <summary>Hold prompt for an unrepaired station ("Repair breaker"). Cache the result.</summary>
        public static string PromptRepair(string station) { return F(FmtPromptRepair, station); }
        /// <summary>Prompt for a finished station ("Breaker restored"). Cache the result.</summary>
        public static string PromptRepaired(string station) { return F(FmtPromptRepaired, Cap(station)); }
        /// <summary>Battery pickup prompt. Cache the result.</summary>
        public static string PromptBattery(int seconds) { return F(FmtPromptBattery, seconds); }

        // ------------------------------------------------------------ triage messages
        private const string FmtMinerArrives = "Miner {0} of {1} is in the cell. Scan them, then decide.";
        public const string Intro = "SHIFT 07  |  Quarantine checkpoint. Scan each miner at the desk, then CLEAR the healthy or INCINERATE the infected.";
        public const string NoMinerInCell = "No miner in the cell. Wait for the next one.";
        public const string MinerNotReady = "The miner is still being brought in. Wait a moment.";
        public const string TriageDone = "Triage is complete. Nothing left to process.";
        public const string DeskNoPower = "The desk has no power. Switch MAIN POWER on first.";
        public const string AnalysisNoSubject = "Nothing to analyse. Wait for the next miner.";
        public const string AnalysisRunning = "Analysis running. Wait for the readout.";
        public const string AnalysisDone = "Analysis complete. Read the three gauges.";
        public const string LogNoSubject = "LOG: no subject in the cell.";
        public const string LogPending = "LOG: subject present. Not yet analysed.";
        public const string LogAnalysed = "LOG: analysis complete. Needles in the red mean infected.";

        /// <summary>"Miner 2 of 3 is in the cell..." (index is 1-based).</summary>
        public static string MinerArrives(int index, int total) { return F(FmtMinerArrives, index, total); }

        // ------------------------------------------------------------ verdict feedback
        public const string VerdictClear = "Verdict: CLEAR. Releasing the miner.";
        public const string ClearHealthy = "Miner cleared and escorted out.";
        public const string VerdictIncinerate = "Verdict: INCINERATE.";
        public const string IncinerateHealthy = "That miner was healthy. Your sanity baseline is permanently lower.";
        public const string IncinerateInfected = "Infection confirmed. Threat removed.";

        // ------------------------------------------------------------ breakdown, elevator
        private const string FmtBreakdown = "FACILITY BREAKDOWN. Triage complete. Go to the ELEVATOR on the west side of the booth, descend and restore {0} systems.";
        private const string FmtElevatorTriageOne = "ELEVATOR LOCKED. Finish triage first: {0} miner left to process at the desk.";
        private const string FmtElevatorTriageMany = "ELEVATOR LOCKED. Finish triage first: {0} miners left to process at the desk.";
        private const string FmtElevatorRepairsOne = "ELEVATOR LOCKED. Restore {0} more system first (breakers and valves).";
        private const string FmtElevatorRepairsMany = "ELEVATOR LOCKED. Restore {0} more systems first (breakers and valves).";
        public const string ElevatorNotNeeded = "No descent required. The elevator is idle.";

        /// <summary>Alarm message when the last miner has been processed. States the whole objective.</summary>
        public static string Breakdown(int repairsNeeded) { return F(FmtBreakdown, repairsNeeded); }
        /// <summary>Refusal when the upper elevator is called before triage is finished; says how many miners remain.</summary>
        public static string ElevatorLockedTriage(int minersLeft) { return F(minersLeft == 1 ? FmtElevatorTriageOne : FmtElevatorTriageMany, minersLeft); }
        /// <summary>Refusal when the lower elevator is called before the repairs are done; says how many remain.</summary>
        public static string ElevatorLockedRepairs(int repairsLeft) { return F(repairsLeft == 1 ? FmtElevatorRepairsOne : FmtElevatorRepairsMany, repairsLeft); }

        // ------------------------------------------------------------ mine messages
        private const string FmtMineArrive = "Mine level. Restore {0} systems before the reactor fails. Hold E at breakers and valves. Stay quiet, stay out of sight.";
        private const string FmtRepairDoneOne = "{0} restored. {1} system left. The patrols are more active.";
        private const string FmtRepairDoneMany = "{0} restored. {1} systems left. The patrols are more active.";
        private const string FmtBatteryTaken = "Battery taken. Torch +{0} s.";
        public const string AllRepaired = "All systems restored. Return to the elevator and go up before the reactor fails.";
        public const string CorridorCollapsed = "The tunnel behind you has collapsed. Find another way back.";
        public const string DoorLocked = "The door is locked.";

        /// <summary>Message shown on arrival in the mine.</summary>
        public static string MineArrive(int repairsNeeded) { return F(FmtMineArrive, repairsNeeded); }
        /// <summary>Message after one repair; says how many systems are left.</summary>
        public static string RepairDone(string station, int left) { return F(left == 1 ? FmtRepairDoneOne : FmtRepairDoneMany, Cap(station), left); }
        /// <summary>Message after picking up a spare torch battery.</summary>
        public static string BatteryTaken(int seconds) { return F(FmtBatteryTaken, seconds); }

        // ------------------------------------------------------------ result screens
        public const string WinTitle = "SHIFT COMPLETE";
        public const string WinNext = "Shift logged. Press R to run it again.";
        public const string FailTitleDeath = "YOU DIED";
        public const string FailTitleReactor = "REACTOR FAILURE";
        public const string FailTitleMind = "MIND LOST";
        public const string FailTitleOther = "SHIFT FAILED";
        private const string FmtWinBody = "You made it back up and the reactor is stable.\nTime in the mine: {0}\nSanity baseline: {1}%\nMiners processed: {2}";

        /// <summary>Victory summary body.</summary>
        public static string WinBody(string mineTime, int sanityPercent, int miners) { return F(FmtWinBody, mineTime, sanityPercent, miners); }

        /// <summary>Large title of the failure screen.</summary>
        public static string FailTitle(FailCause c)
        {
            switch (c)
            {
                case FailCause.ClearedInfected:
                case FailCause.Patrol: return FailTitleDeath;
                case FailCause.Reactor: return FailTitleReactor;
                case FailCause.Mind: return FailTitleMind;
                default: return FailTitleOther;
            }
        }

        /// <summary>What happened, in one or two sentences.</summary>
        public static string FailBody(FailCause c)
        {
            switch (c)
            {
                case FailCause.ClearedInfected: return "You cleared an infected miner. It broke out of the cell.";
                case FailCause.Patrol: return "A patrol found you.";
                case FailCause.Reactor: return "The reactor failed before the systems were restored. The mine is lost.";
                case FailCause.Mind: return "Your mind broke in the dark.";
                default: return "The shift ended.";
            }
        }

        /// <summary>What to do differently next time.</summary>
        public static string FailNext(FailCause c)
        {
            switch (c)
            {
                case FailCause.ClearedInfected: return "Run ANALYSIS first. Infected miners show anomalies and red needles: INCINERATE those, CLEAR only the healthy.";
                case FailCause.Patrol: return "Crouch (Ctrl) behind a minecart when a patrol passes. Sprinting and repairs are loud.";
                case FailCause.Reactor: return "Move faster: skip the dead ends and start each repair as soon as you reach a station.";
                case FailCause.Mind: return "Avoid the gas pockets and never incinerate a healthy miner. Both wear down your sanity.";
                default: return "Check the objective in the top-left corner and try again.";
            }
        }

        // ------------------------------------------------------------ hints (see HintDirector)
        private static readonly string[] StepLabels = { "", "1/4", "2/4", "3/4", "4/4" };

        public const string HintPower = "Switch the desk MAIN POWER on. It is the POWER switch in the row of six on the desk.";
        public const string HintScan = "Turn on X-RAY and BIOSCAN. The two screens show the miner's scan: look for anomalies.";
        public const string HintAnalyse = "Press ANALYSIS, then wait for the readout (about 3 seconds).";
        public const string HintAnalysing = "Analysing. Wait for the readout.";
        public const string HintDecide = "Anomalies on the screens and needles in the red = infected. Healthy: flip CLEAR. Infected: pull the red lever to INCINERATE.";
        public const string HintPowerShort = "Switch MAIN POWER on.";
        public const string HintScanShort = "Turn on X-RAY and BIOSCAN.";
        public const string HintAnalyseShort = "Press ANALYSIS.";
        public const string HintDecideShort = "Needles in the red = infected. CLEAR the healthy, INCINERATE the infected.";

        public const string HintDescent = "Go to the elevator on the west side of the booth. Open its door, step inside and press CALL on the wall.";
        public const string HintReturn = "Return to the elevator on the landing. Step inside and press CALL to go up.";
        public const string HintMineFirst = "Find a breaker or valve and HOLD E until the bar fills. Repairs are loud: stay low (Ctrl) and hide behind minecarts.";
        private const string FmtHintMineNextOne = "{0} system left. Find the next breaker or valve and hold E.";
        private const string FmtHintMineNextMany = "{0} systems left. Find the next breaker or valve and hold E.";
        public const string HintRepairing = "Keep holding E. Repairing is loud and patrols may hear it.";
        public const string HintDetected = "A patrol sees you. Break line of sight: crouch (Ctrl) behind a minecart.";
        public const string HintHidden = "You are hidden. Stay crouched until the patrol moves away.";
        public const string HintGas = "Toxic gas. Leave the cloud: it drains your sanity.";

        /// <summary>Step badge for the scanner flow ("1/4"); step is 1-4, 0 gives an empty label.</summary>
        public static string StepLabel(int step) { return StepLabels[step < 0 || step >= StepLabels.Length ? 0 : step]; }
        /// <summary>Reminder for the next system to restore.</summary>
        public static string HintMineNext(int left) { return F(left == 1 ? FmtHintMineNextOne : FmtHintMineNextMany, left); }
    }
}
