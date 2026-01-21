using System;
using System.Collections.Generic;
using System.Linq;
using GearPlanner.Models;

namespace GearPlanner.Helpers;

public class LootDistributionCalculator
{
    public class GearSlotInfo
    {
        public string SlotName { get; set; } = string.Empty;
        public int Floor { get; set; }
        public int BookCost { get; set; }
    }

    public class MemberJobState
    {
        public string JobName { get; set; } = string.Empty;
        public bool IsMainJob { get; set; }
        public int JobPriority { get; set; } // 0 = main, 1 = alt 1, 2 = alt 2, etc.
        public Dictionary<string, GearState> GearNeeds { get; set; } = new();
        public int GlazesNeeded { get; set; } = 0;
        public int TwinesNeeded { get; set; } = 0;
        
        public bool IsFullyGeared()
        {
            // Check all gear pieces are obtained and upgraded as desired
            var allGearReady = GearNeeds.Values.All(g => 
            {
                if (g.DesiredSource == Models.GearSource.Savage)
                    return g.HasSavage;
                else if (g.DesiredSource == Models.GearSource.TomeUp)
                    return g.HasSavage && !g.NeedsUpgrade;
                else
                    return true; // No desired source or other sources don't need to be checked
            });
            
            return allGearReady && GlazesNeeded == 0 && TwinesNeeded == 0;
        }
    }

    public class GearState
    {
        public string SlotName { get; set; } = string.Empty;
        public bool HasSavage { get; set; }
        public bool NeedsUpgrade { get; set; } // Needs Glaze/Twine upgrade
        public Models.GearSource DesiredSource { get; set; } // What they want for this piece
        
        public string GetStatus()
        {
            if (HasSavage) return "Savage";
            if (NeedsUpgrade) return "NeedsUpgrade";
            return "Missing";
        }
    }

    public class MemberState
    {
        public string MemberName { get; set; } = string.Empty;
        public List<MemberJobState> Jobs { get; set; } = new();
        public int Floor1Books { get; set; }
        public int Floor2Books { get; set; }
        public int Floor3Books { get; set; }
        public int Floor4Books { get; set; }
        public bool HasMainWeapon { get; set; }
        public List<bool> HasAltWeapons { get; set; } = new();
        
        public bool IsFullyGeared()
        {
            return Jobs.All(j => j.IsFullyGeared()) && HasMainWeapon && HasAltWeapons.All(w => w);
        }
    }

    public class WeeklyPlan
    {
        public int WeekNumber { get; set; }
        public List<int> FloorsToRun { get; set; } = new();
        public List<string> Allocations { get; set; } = new(); // Human-readable allocations
    }

    public class DistributionPlan
    {
        public List<string> StartingState { get; set; } = new(); // Starting state summary
        public List<WeeklyPlan> Weeks { get; set; } = new();
        public int TotalWeeks { get; set; }
    }

    private static readonly Dictionary<string, GearSlotInfo> GearSlotMap = new()
    {
        { "MainHand", new GearSlotInfo { SlotName = "MainHand", Floor = 4, BookCost = 8 } },
        { "Head", new GearSlotInfo { SlotName = "Head", Floor = 2, BookCost = 4 } },
        { "Body", new GearSlotInfo { SlotName = "Body", Floor = 3, BookCost = 6 } },
        { "Hands", new GearSlotInfo { SlotName = "Hands", Floor = 2, BookCost = 4 } },
        { "Legs", new GearSlotInfo { SlotName = "Legs", Floor = 3, BookCost = 6 } },
        { "Feet", new GearSlotInfo { SlotName = "Feet", Floor = 2, BookCost = 4 } },
        { "Ears", new GearSlotInfo { SlotName = "Ears", Floor = 1, BookCost = 3 } },
        { "Neck", new GearSlotInfo { SlotName = "Neck", Floor = 1, BookCost = 3 } },
        { "Wrists", new GearSlotInfo { SlotName = "Wrists", Floor = 1, BookCost = 3 } },
        { "Ring1", new GearSlotInfo { SlotName = "Ring1", Floor = 1, BookCost = 3 } },
        { "Ring2", new GearSlotInfo { SlotName = "Ring2", Floor = 1, BookCost = 3 } }
    };

    public static DistributionPlan CalculateDistribution(RaidTeam team, bool useCurrentState)
    {
        // First pass: Calculate with conservative Floor 4 book reservations
        var memberStates = BuildMemberStates(team, useCurrentState);
        var initialPlan = SimulateLootDistribution(memberStates, team, allowFloor4Trading: false);
        
        // Check for excess Floor 4 books at the end
        var maxExcessFloor4 = memberStates.Max(m => m.Floor4Books);
        
        if (maxExcessFloor4 > 0)
        {
            // Second pass: Go back a few weeks and allow Floor 4 trading
            var weeksToBacktrack = Math.Min(maxExcessFloor4, 3); // Go back up to 3 weeks
            var backtrackWeek = Math.Max(1, initialPlan.TotalWeeks - weeksToBacktrack);
            
            // Reset member states and replay from the beginning to backtrack point
            memberStates = BuildMemberStates(team, useCurrentState);
            var revisedPlan = SimulateLootDistributionWithBacktrack(memberStates, team, backtrackWeek, allowFloor4Trading: true);
            return revisedPlan;
        }
        
        return initialPlan;
    }

    private static List<MemberState> BuildMemberStates(RaidTeam team, bool useCurrentState)
    {
        // Build member states from team data
        var memberStates = new List<MemberState>();
        
        foreach (var member in team.Members)
        {
            var state = new MemberState { MemberName = member.Name };
            
            // Main job
            if (team.Sheets.Count > 0)
            {
                var mainSheet = team.Sheets[0];
                var sheetMemberIdx = team.Members.IndexOf(member);
                if (sheetMemberIdx >= 0 && sheetMemberIdx < mainSheet.Members.Count)
                {
                    var sheetMember = mainSheet.Members[sheetMemberIdx];
                    var jobState = BuildJobState(sheetMember, member.Job, true, useCurrentState, 0);
                    state.Jobs.Add(jobState);
                    state.HasMainWeapon = sheetMember.Gear.TryGetValue("MainHand", out var mh) && 
                                        mh.Source == Models.GearSource.Savage;
                }
            }

            // Alt jobs
            for (int sheetIdx = 1; sheetIdx < team.Sheets.Count; sheetIdx++)
            {
                var sheet = team.Sheets[sheetIdx];
                var sheetMemberIdx = team.Members.IndexOf(member);
                if (sheetMemberIdx >= 0 && sheetMemberIdx < sheet.Members.Count)
                {
                    var sheetMember = sheet.Members[sheetMemberIdx];
                    
                    // Check if this alt job has any desired gear (skip if completely empty)
                    var hasDesiredGear = sheetMember.Gear.Values.Any(g => g.DesiredSource != Models.GearSource.None);
                    if (!hasDesiredGear)
                        continue; // Skip this alt job if member doesn't want it
                    
                    var jobName = sheet.Name; // or extract job name from sheet
                    var jobState = BuildJobState(sheetMember, jobName, false, useCurrentState, sheetIdx);
                    state.Jobs.Add(jobState);
                    state.HasAltWeapons.Add(sheetMember.Gear.TryGetValue("MainHand", out var mh) && 
                                           mh.Source == Models.GearSource.Savage);
                }
            }

            // Set starting books from configuration (Books row = pagesFromClears + BookAdjustments - SpentBooks)
            if (useCurrentState)
            {
                // Get books from team-wide clears
                var booksFromClears1 = team.Floor1Clears;
                var booksFromClears2 = team.Floor2Clears;
                var booksFromClears3 = team.Floor3Clears;
                var booksFromClears4 = team.Floor4Clears;
                
                // Total available = clears + adjustments
                var totalAvailable1 = booksFromClears1 + (member.BookAdjustments.ContainsKey(1) ? member.BookAdjustments[1] : 0);
                var totalAvailable2 = booksFromClears2 + (member.BookAdjustments.ContainsKey(2) ? member.BookAdjustments[2] : 0);
                var totalAvailable3 = booksFromClears3 + (member.BookAdjustments.ContainsKey(3) ? member.BookAdjustments[3] : 0);
                var totalAvailable4 = booksFromClears4 + (member.BookAdjustments.ContainsKey(4) ? member.BookAdjustments[4] : 0);
                
                // Current books = total available - spent
                state.Floor1Books = totalAvailable1 - (member.SpentBooks.ContainsKey(1) ? member.SpentBooks[1] : 0);
                state.Floor2Books = totalAvailable2 - (member.SpentBooks.ContainsKey(2) ? member.SpentBooks[2] : 0);
                state.Floor3Books = totalAvailable3 - (member.SpentBooks.ContainsKey(3) ? member.SpentBooks[3] : 0);
                state.Floor4Books = totalAvailable4 - (member.SpentBooks.ContainsKey(4) ? member.SpentBooks[4] : 0);
            }

            memberStates.Add(state);
        }

        return memberStates;
    }

    private static MemberJobState BuildJobState(RaidMember sheetMember, string jobName, bool isMainJob, bool useCurrentState, int jobPriority)
    {
        var jobState = new MemberJobState
        {
            JobName = jobName,
            IsMainJob = isMainJob,
            JobPriority = jobPriority
        };

        // Build gear needs
        foreach (var (slotName, gearPiece) in sheetMember.Gear)
        {
            var hasDesired = gearPiece.Source == gearPiece.DesiredSource;
            var needsUpgrade = gearPiece.DesiredSource == Models.GearSource.TomeUp && 
                             gearPiece.Source != Models.GearSource.TomeUp;

            jobState.GearNeeds[slotName] = new GearState
            {
                SlotName = slotName,
                // For TomeUp desired: assume Tome gear is available, so HasSavage=true even before dropping
                HasSavage = gearPiece.Source == Models.GearSource.Savage || hasDesired || gearPiece.DesiredSource == Models.GearSource.TomeUp,
                NeedsUpgrade = needsUpgrade,
                DesiredSource = gearPiece.DesiredSource
            };
        }

        // Calculate glazes/twines needed for this job
        var glazeSlots = new[] { "Ears", "Neck", "Wrists", "Ring1", "Ring2" };
        foreach (var slot in glazeSlots)
        {
            if (jobState.GearNeeds.TryGetValue(slot, out var gear) && gear.NeedsUpgrade)
            {
                jobState.GlazesNeeded++;
            }
        }

        var twineSlots = new[] { "Head", "Body", "Hands", "Legs", "Feet" };
        foreach (var slot in twineSlots)
        {
            if (jobState.GearNeeds.TryGetValue(slot, out var gear) && gear.NeedsUpgrade)
            {
                jobState.TwinesNeeded++;
            }
        }

        return jobState;
    }

    private static DistributionPlan SimulateLootDistribution(List<MemberState> members, RaidTeam team, bool allowFloor4Trading = false)
    {
        var plan = new DistributionPlan();
        
        // Add starting state summary
        plan.StartingState.Add("=== STARTING TEAM STATE ===\n");
        foreach (var member in members)
        {
            foreach (var job in member.Jobs)
            {
                var savagePieces = job.GearNeeds.Count(g => g.Value.DesiredSource == Models.GearSource.Savage && !g.Value.HasSavage);
                var glazesNeeded = job.GlazesNeeded;
                var twinesNeeded = job.TwinesNeeded;
                
                var jobTypeLabel = job.IsMainJob ? "Main" : $"Alt Job {job.JobPriority}";
                plan.StartingState.Add($"{member.MemberName} ({job.JobName}) - {jobTypeLabel}:");
                plan.StartingState.Add($"  Savage Pieces Needed: {savagePieces}");
                plan.StartingState.Add($"  Glazes Needed: {glazesNeeded}");
                plan.StartingState.Add($"  Twines Needed: {twinesNeeded}");
                plan.StartingState.Add("");
            }
        }
        plan.StartingState.Add("=== WEEK-BY-WEEK DISTRIBUTION PLAN ===\n");
        
        var week = 0;
        var maxWeeks = 52; // Safety limit

        // Simulate week by week until everyone is fully geared
        while (week < maxWeeks && members.Any(m => !m.IsFullyGeared()))
        {
            week++;
            var weekPlan = new WeeklyPlan { WeekNumber = week };

            // Determine which floors to run based on what's needed
            var floorsToRun = DetermineFloresToRunOptimized(members, mainJobOnly: false);
            weekPlan.FloorsToRun = floorsToRun;

            if (floorsToRun.Count == 0)
                break;

            // Allocate loot and materials (awards books first, then allocates loot)
            // Enable Floor 4 trading to convert excess Floor 4 books to other floors
            AllocateLootForWeek(members, floorsToRun, weekPlan, mainJobOnly: false, allowFloor4Trading: true);

            plan.Weeks.Add(weekPlan);
        }

        plan.TotalWeeks = week;
        return plan;
    }

    private static DistributionPlan SimulateLootDistributionWithBacktrack(List<MemberState> members, RaidTeam team, int backtrackToWeek, bool allowFloor4Trading)
    {
        // Run normally up to backtrack point, then allow trading
        var plan = new DistributionPlan();
        
        plan.StartingState.Add("=== STARTING TEAM STATE ===\n");
        foreach (var member in members)
        {
            foreach (var job in member.Jobs)
            {
                var savagePieces = job.GearNeeds.Count(g => g.Value.DesiredSource == Models.GearSource.Savage && !g.Value.HasSavage);
                var glazesNeeded = job.GlazesNeeded;
                var twinesNeeded = job.TwinesNeeded;
                
                var jobTypeLabel = job.IsMainJob ? "Main" : $"Alt Job {job.JobPriority}";
                plan.StartingState.Add($"{member.MemberName} ({job.JobName}) - {jobTypeLabel}:");
                plan.StartingState.Add($"  Savage Pieces Needed: {savagePieces}");
                plan.StartingState.Add($"  Glazes Needed: {glazesNeeded}");
                plan.StartingState.Add($"  Twines Needed: {twinesNeeded}");
                plan.StartingState.Add("");
            }
        }
        plan.StartingState.Add("=== WEEK-BY-WEEK DISTRIBUTION PLAN ===\n");
        
        var week = 0;
        var maxWeeks = 52;
        
        while (week < maxWeeks && members.Any(m => !m.IsFullyGeared()))
        {
            week++;
            var weekPlan = new WeeklyPlan { WeekNumber = week };
            
            if (week >= 1)
            {
                // Debug output after allocations from previous week
                var allFullyGeared = members.All(m => m.IsFullyGeared());
                weekPlan.Allocations.Add($"\n[DEBUG WEEK {week}] All members fully geared: {allFullyGeared}");
                if (!allFullyGeared)
                {
                    var notGeared = members.Where(m => !m.IsFullyGeared()).FirstOrDefault();
                    if (notGeared != null)
                    {
                        weekPlan.Allocations.Add($"[DEBUG] First not fully geared: {notGeared.MemberName}");
                        var notGearedJob = notGeared.Jobs.FirstOrDefault(j => !j.IsFullyGeared());
                        if (notGearedJob != null)
                        {
                            var gearReady = notGearedJob.GearNeeds.Values.All(g => 
                            {
                                if (g.DesiredSource == Models.GearSource.Savage) return g.HasSavage;
                                else if (g.DesiredSource == Models.GearSource.TomeUp) return g.HasSavage && !g.NeedsUpgrade;
                                else return true;
                            });
                            weekPlan.Allocations.Add($"[DEBUG] Job: {notGearedJob.JobName}, GearReady={gearReady}, Glazes={notGearedJob.GlazesNeeded}, Twines={notGearedJob.TwinesNeeded}");
                            weekPlan.Allocations.Add($"[DEBUG] HasMainWeapon={notGeared.HasMainWeapon}, AltWeapons=[{string.Join(",", notGeared.HasAltWeapons)}]");
                            
                            // Show which gear pieces are failing
                            var failingGear = notGearedJob.GearNeeds.Where(g => 
                            {
                                if (g.Value.DesiredSource == Models.GearSource.Savage) return !g.Value.HasSavage;
                                else if (g.Value.DesiredSource == Models.GearSource.TomeUp) return !(g.Value.HasSavage && !g.Value.NeedsUpgrade);
                                else return false;
                            }).ToList();
                            
                            if (failingGear.Any())
                            {
                                weekPlan.Allocations.Add($"[DEBUG] Failing gear slots:");
                                foreach (var (slot, gear) in failingGear)
                                {
                                    weekPlan.Allocations.Add($"[DEBUG]   {slot}: Desired={gear.DesiredSource}, HasSavage={gear.HasSavage}, NeedsUpgrade={gear.NeedsUpgrade}");
                                }
                            }
                        }
                    }
                }
                
                // Detailed remaining gear for this week
                weekPlan.Allocations.Add("");
                weekPlan.Allocations.Add("--- DETAILED REMAINING GEAR (WEEK " + week + ") ---");
                foreach (var member in members)
                {
                    var memberHasNeeds = false;
                    foreach (var job in member.Jobs)
                    {
                        var remainingGear = job.GearNeeds
                            .Where(g => (!g.Value.HasSavage && g.Value.DesiredSource == Models.GearSource.Savage) ||
                                       (g.Value.HasSavage && g.Value.NeedsUpgrade && g.Value.DesiredSource == Models.GearSource.TomeUp))
                            .ToList();
                        
                        if (remainingGear.Count > 0 || job.GlazesNeeded > 0 || job.TwinesNeeded > 0)
                        {
                            if (!memberHasNeeds)
                            {
                                weekPlan.Allocations.Add($"{member.MemberName}:");
                                memberHasNeeds = true;
                            }
                            
                            weekPlan.Allocations.Add($"  {job.JobName}:");
                            
                            foreach (var (slotName, gearState) in remainingGear)
                            {
                                if (!gearState.HasSavage && gearState.DesiredSource == Models.GearSource.Savage)
                                    weekPlan.Allocations.Add($"    - {slotName} (Savage)");
                                else if (gearState.HasSavage && gearState.NeedsUpgrade)
                                    weekPlan.Allocations.Add($"    - {slotName} (TomeUp)");
                            }
                            
                            if (job.GlazesNeeded > 0)
                                weekPlan.Allocations.Add($"    - {job.GlazesNeeded}x Glaze");
                            if (job.TwinesNeeded > 0)
                                weekPlan.Allocations.Add($"    - {job.TwinesNeeded}x Twine");
                        }
                    }
                }
            }
            
            var floorsToRun = DetermineFloresToRunOptimized(members, mainJobOnly: false);
            weekPlan.FloorsToRun = floorsToRun;
            
            if (floorsToRun.Count == 0)
                break;
            
            // Use allowFloor4Trading flag only from backtrackToWeek onwards
            var allowTrading = week >= backtrackToWeek && allowFloor4Trading;
            AllocateLootForWeek(members, floorsToRun, weekPlan, mainJobOnly: false, allowTrading);
            
            plan.Weeks.Add(weekPlan);
        }
        
        plan.TotalWeeks = week;
        return plan;
    }

    private static void AwardBooksFromFloors(List<MemberState> members, List<int> floorsToRun)
    {
        foreach (var member in members)
        {
            if (floorsToRun.Contains(1)) member.Floor1Books += 1;
            if (floorsToRun.Contains(2)) member.Floor2Books += 1;
            if (floorsToRun.Contains(3)) member.Floor3Books += 1;
            if (floorsToRun.Contains(4)) member.Floor4Books += 1;
        }
    }

    private static List<int> DetermineFloresToRunOptimized(List<MemberState> members, bool mainJobOnly)
    {
        // If everyone is fully geared, return no floors
        bool allFullyGeared = members.All(m => m.IsFullyGeared());
        if (allFullyGeared)
        {
            return new List<int>();
        }

        var floors = new List<int>();
        var jobsToCheck = members.SelectMany(m => m.Jobs).ToList();
        
        // Calculate book needs per member for remaining gear
        var floor1NeedsPerMember = new Dictionary<MemberState, int>();
        var floor2NeedsPerMember = new Dictionary<MemberState, int>();
        var floor3NeedsPerMember = new Dictionary<MemberState, int>();
        
        foreach (var member in members)
        {
            int floor1Need = 0, floor2Need = 0, floor3Need = 0;
            
            foreach (var job in member.Jobs)
            {
                floor2Need += job.GlazesNeeded * 3;
                floor3Need += job.TwinesNeeded * 4;
                
                foreach (var (slotName, gearState) in job.GearNeeds)
                {
                    if (!gearState.HasSavage && GearSlotMap.TryGetValue(slotName, out var info))
                    {
                        switch (info.Floor)
                        {
                            case 1: floor1Need += info.BookCost; break;
                            case 2: floor2Need += info.BookCost; break;
                            case 3: floor3Need += info.BookCost; break;
                        }
                    }
                }
            }
            
            floor1NeedsPerMember[member] = floor1Need;
            floor2NeedsPerMember[member] = floor2Need;
            floor3NeedsPerMember[member] = floor3Need;
        }

        // Floor 1: Run if anyone needs Floor 1 gear OR doesn't have enough Floor 1 books to buy what they need
        if (jobsToCheck.Any(j => !j.IsFullyGeared() &&
            j.GearNeeds.Any(gn => new[] { "Ears", "Neck", "Wrists", "Ring1", "Ring2" }.Contains(gn.Key) && !gn.Value.HasSavage)) ||
            members.Any(m => floor1NeedsPerMember[m] > 0 && m.Floor1Books < floor1NeedsPerMember[m]))
        {
            floors.Add(1);
        }

        // Floor 2: Run if anyone needs Floor 2 gear/glazes OR doesn't have enough Floor 2 books to buy what they need
        // Note: Glazes drop from Floor 2 and cost Floor 2 books, so include GlazesNeeded here
        if (jobsToCheck.Any(j => !j.IsFullyGeared() && (
            j.GearNeeds.Any(gn => new[] { "Head", "Hands", "Feet" }.Contains(gn.Key) && !gn.Value.HasSavage) ||
            j.GlazesNeeded > 0)) ||
            members.Any(m => floor2NeedsPerMember[m] > 0 && m.Floor2Books < floor2NeedsPerMember[m]))
        {
            floors.Add(2);
        }

        // Floor 3: Run if anyone needs Floor 3 gear/twines OR doesn't have enough Floor 3 books to buy what they need
        // Note: Twines drop from Floor 3 and cost Floor 3 books, so include TwinesNeeded here
        if (jobsToCheck.Any(j => !j.IsFullyGeared() && (
            j.GearNeeds.Any(gn => new[] { "Body", "Legs" }.Contains(gn.Key) && !gn.Value.HasSavage) ||
            j.TwinesNeeded > 0)) ||
            members.Any(m => floor3NeedsPerMember[m] > 0 && m.Floor3Books < floor3NeedsPerMember[m]))
        {
            floors.Add(3);
        }

        // Floor 4: Run if anyone needs a weapon OR if there are member deficits that could benefit from trading
        bool needsWeapon = members.Any(m => !m.HasMainWeapon || m.HasAltWeapons.Any(w => !w));
        
        // Check if any member has a Floor 2 or Floor 3 deficit that could be helped by trading Floor 4 books
        bool hasFloor2or3Deficit = false;
        foreach (var member in members)
        {
            int memberFloor2Need = 0;
            int memberFloor3Need = 0;
            
            foreach (var job in member.Jobs)
            {
                memberFloor2Need += job.GlazesNeeded * 3;
                memberFloor3Need += job.TwinesNeeded * 4;
                
                foreach (var (slotName, gearState) in job.GearNeeds)
                {
                    if (gearState.HasSavage && gearState.NeedsUpgrade)
                    {
                        if (new[] { "Ears", "Neck", "Wrists", "Ring1", "Ring2" }.Contains(slotName))
                            memberFloor2Need += 3;
                        else if (new[] { "Head", "Body", "Hands", "Legs", "Feet" }.Contains(slotName))
                            memberFloor3Need += 4;
                    }
                    else if (!gearState.HasSavage && GearSlotMap.TryGetValue(slotName, out var info))
                    {
                        if (info.Floor == 2) memberFloor2Need += info.BookCost;
                        if (info.Floor == 3) memberFloor3Need += info.BookCost;
                    }
                }
            }
            
            if ((memberFloor2Need > member.Floor2Books) || (memberFloor3Need > member.Floor3Books))
            {
                hasFloor2or3Deficit = true;
                break;
            }
        }
        
        if (needsWeapon || hasFloor2or3Deficit)
        {
            floors.Add(4);
        }

        return floors;
    }

    private static void AllocateLootForWeek(List<MemberState> members, List<int> floorsToRun, WeeklyPlan weekPlan, bool mainJobOnly = false, bool allowFloor4Trading = false)
    {
        // Collect all allocations for this week
        var allocations = new List<string>();
        
        // Add book state debug info BEFORE floor earnings
        allocations.Add("--- BOOK STATE (BEFORE EARNINGS) ---");
        foreach (var member in members)
        {
            allocations.Add($"{member.MemberName}: Floor1={member.Floor1Books}, Floor2={member.Floor2Books}, Floor3={member.Floor3Books}, Floor4={member.Floor4Books}");
        }
        allocations.Add("");
        
        // Show floor earnings for this week
        allocations.Add("--- FLOOR EARNINGS THIS WEEK ---");
        allocations.Add($"Running Floors: {string.Join(", ", floorsToRun)}");
        allocations.Add("Each member earns +1 book per floor cleared");
        allocations.Add("");
        
        // Award books from each floor FIRST (so allocation can spend them)
        foreach (var member in members)
        {
            if (floorsToRun.Contains(1)) member.Floor1Books += 1;
            if (floorsToRun.Contains(2)) member.Floor2Books += 1;
            if (floorsToRun.Contains(3)) member.Floor3Books += 1;
            if (floorsToRun.Contains(4)) member.Floor4Books += 1;
        }
        
        // Collect slots by floor to allocate (one per floor per slot)
        var floorSlots = new Dictionary<int, List<string>>
        {
            { 1, new List<string> { "Ears", "Neck", "Wrists", "Ring1" } },
            { 2, new List<string> { "Head", "Hands", "Feet" } },
            { 3, new List<string> { "Body", "Legs" } }
        };
        
        // Calculate the maximum job priority level needed
        var allJobs = members.SelectMany(m => m.Jobs).ToList();
        int maxPriority = allJobs.Any() ? allJobs.Max(j => j.JobPriority) + 1 : 1;
        
        // For each floor that runs, allocate its slots by job priority
        foreach (var floor in floorsToRun)
        {
            if (floor == 4)
                continue; // Handle weapons separately
            
            if (floorSlots.ContainsKey(floor))
            {
                foreach (var slot in floorSlots[floor])
                {
                    // Find the highest priority member/job that needs this slot
                    (MemberState Member, MemberJobState Job)? bestMatch = null;
                    int bestPriority = int.MaxValue;
                    
                    foreach (var member in members)
                    {
                        foreach (var job in member.Jobs)
                        {
                            if (job.GearNeeds.TryGetValue(slot, out var gearState) && 
                                !gearState.HasSavage && 
                                gearState.DesiredSource == Models.GearSource.Savage)
                            {
                                // Found a member who needs this slot
                                if (job.JobPriority < bestPriority)
                                {
                                    bestPriority = job.JobPriority;
                                    bestMatch = (member, job);
                                }
                            }
                        }
                    }
                    
                    // Assign this slot to the best match found
                    if (bestMatch != null)
                    {
                        allocations.Add($"- {bestMatch.Value.Member.MemberName} ({bestMatch.Value.Job.JobName}): RECEIVES {slot} (Savage) from direct drop");
                        bestMatch.Value.Job.GearNeeds[slot].HasSavage = true;
                    }
                }
            }
        }

        // Handle material drops (Glazes from Floor 2, Twines from Floor 3)
        // ONE Glaze per week from Floor 2
        if (floorsToRun.Contains(2))
        {
            // Find highest priority member/job that needs a glaze
            for (int jobPriority = 0; jobPriority < maxPriority; jobPriority++)
            {
                var matchingMember = members.FirstOrDefault(m => 
                    m.Jobs.Any(j => j.JobPriority == jobPriority && j.GlazesNeeded > 0));
                
                if (matchingMember != null)
                {
                    var job = matchingMember.Jobs.First(j => j.JobPriority == jobPriority && j.GlazesNeeded > 0);
                    job.GlazesNeeded--;
                    
                    // Mark a Glaze-upgradeable piece as upgraded
                    var glazeSlot = job.GearNeeds.FirstOrDefault(g => 
                        new[] { "Ears", "Neck", "Wrists", "Ring1", "Ring2" }.Contains(g.Key) && 
                        g.Value.NeedsUpgrade).Key;
                    if (glazeSlot != null)
                    {
                        job.GearNeeds[glazeSlot].NeedsUpgrade = false;
                    }
                    
                    allocations.Add($"- {matchingMember.MemberName} ({job.JobName}): RECEIVES Glaze from direct drop");
                    break; // Only one member gets glaze this week
                }
            }
        }

        // ONE Twine per week from Floor 3
        if (floorsToRun.Contains(3))
        {
            // Find highest priority member/job that needs a twine
            for (int jobPriority = 0; jobPriority < maxPriority; jobPriority++)
            {
                var matchingMember = members.FirstOrDefault(m => 
                    m.Jobs.Any(j => j.JobPriority == jobPriority && j.TwinesNeeded > 0));
                
                if (matchingMember != null)
                {
                    var job = matchingMember.Jobs.First(j => j.JobPriority == jobPriority && j.TwinesNeeded > 0);
                    job.TwinesNeeded--;
                    
                    // Mark a Twine-upgradeable piece as upgraded
                    var twineSlot = job.GearNeeds.FirstOrDefault(g => 
                        new[] { "Head", "Body", "Hands", "Legs", "Feet" }.Contains(g.Key) && 
                        g.Value.NeedsUpgrade).Key;
                    if (twineSlot != null)
                    {
                        job.GearNeeds[twineSlot].NeedsUpgrade = false;
                    }
                    
                    allocations.Add($"- {matchingMember.MemberName} ({job.JobName}): RECEIVES Twine from direct drop");
                    break; // Only one member gets twine this week
                }
            }
        }

        // Handle weapons from floor 4
        if (floorsToRun.Contains(4))
        {
            // Floor 4 gives ONE main hand weapon per week
            // First priority: member without main weapon (for main job)
            var memberNeedingWeapon = members.FirstOrDefault(m => !m.HasMainWeapon);
            if (memberNeedingWeapon != null)
            {
                var mainJob = memberNeedingWeapon.Jobs.FirstOrDefault(j => j.IsMainJob);
                if (mainJob != null)
                {
                    allocations.Add($"- {memberNeedingWeapon.MemberName} ({mainJob.JobName}): RECEIVES Main Hand (Savage) from direct drop");
                    // Update GearNeeds to mark MainHand as obtained
                    if (mainJob.GearNeeds.TryGetValue("MainHand", out var mhGear))
                    {
                        mhGear.HasSavage = true;
                    }
                }
                // Mark weapon as obtained for main job
                memberNeedingWeapon.HasMainWeapon = true;
            }
            else
            {
                // All main jobs have weapons. Allocate to next alt job needing one.
                for (int memberIdx = 0; memberIdx < members.Count; memberIdx++)
                {
                    var member = members[memberIdx];
                    for (int altIdx = 0; altIdx < member.HasAltWeapons.Count; altIdx++)
                    {
                        if (!member.HasAltWeapons[altIdx])
                        {
                            // Mark this alt weapon as obtained
                            member.HasAltWeapons[altIdx] = true;
                            // Get the alt job at the correct index (skip main job which is at index 0)
                            var altJob = member.Jobs.ElementAtOrDefault(altIdx + 1);
                            if (altJob != null)
                            {
                                allocations.Add($"- {member.MemberName} ({altJob.JobName}): RECEIVES Main Hand (Savage) from direct drop");
                                // Update GearNeeds to mark MainHand as obtained
                                if (altJob.GearNeeds.TryGetValue("MainHand", out var mhGear))
                                {
                                    mhGear.HasSavage = true;
                                }
                            }
                            // Exit both loops after allocating one weapon
                            goto WeaponAllocated;
                        }
                    }
                }
                WeaponAllocated:;
            }
        }

        // Add all allocations to the week plan
        foreach (var allocation in allocations)
        {
            weekPlan.Allocations.Add(allocation);
        }

        // Add book spending section
        if (allocations.Count > 0)
        {
            weekPlan.Allocations.Add("");
        }
        weekPlan.Allocations.Add("--- BOOK SPENDING ---");

        // Use books to buy gear/materials
        AllocateBooks(members, weekPlan, mainJobOnly, allowFloor4Trading);
        
        // Add book accounting summary
        weekPlan.Allocations.Add("");
        weekPlan.Allocations.Add("--- WEEK SUMMARY ---");
        // Count gear pieces that either: don't have Savage but want it, OR need upgrade to TomeUp
        int totalGearNeeded = members.Sum(m => m.Jobs.Sum(j => 
            j.GearNeeds.Count(g => 
                (!g.Value.HasSavage && g.Value.DesiredSource == Models.GearSource.Savage) ||
                (g.Value.HasSavage && g.Value.NeedsUpgrade && g.Value.DesiredSource == Models.GearSource.TomeUp)
            )
        ));
        int totalGlazesNeeded = members.Sum(m => m.Jobs.Sum(j => j.GlazesNeeded));
        int totalTwinesNeeded = members.Sum(m => m.Jobs.Sum(j => j.TwinesNeeded));
        weekPlan.Allocations.Add($"Remaining Gear Pieces Needed: {totalGearNeeded}");
        weekPlan.Allocations.Add($"Remaining Glazes Needed: {totalGlazesNeeded}");
        weekPlan.Allocations.Add($"Remaining Twines Needed: {totalTwinesNeeded}");
        
        // After week 14, show detailed remaining gear
        if (weekPlan.WeekNumber >= 14)
        {
            weekPlan.Allocations.Add("");
            weekPlan.Allocations.Add("--- DETAILED REMAINING GEAR ---");
            foreach (var member in members)
            {
                var memberHasNeeds = false;
                foreach (var job in member.Jobs)
                {
                    var remainingGear = job.GearNeeds
                        .Where(g => (!g.Value.HasSavage && g.Value.DesiredSource == Models.GearSource.Savage) ||
                                   (g.Value.HasSavage && g.Value.NeedsUpgrade && g.Value.DesiredSource == Models.GearSource.TomeUp))
                        .ToList();
                    
                    if (remainingGear.Count > 0 || job.GlazesNeeded > 0 || job.TwinesNeeded > 0)
                    {
                        if (!memberHasNeeds)
                        {
                            weekPlan.Allocations.Add($"{member.MemberName}:");
                            memberHasNeeds = true;
                        }
                        
                        weekPlan.Allocations.Add($"  {job.JobName}:");
                        
                        foreach (var (slotName, gearState) in remainingGear)
                        {
                            if (!gearState.HasSavage && gearState.DesiredSource == Models.GearSource.Savage)
                                weekPlan.Allocations.Add($"    - {slotName} (Savage)");
                            else if (gearState.HasSavage && gearState.NeedsUpgrade)
                                weekPlan.Allocations.Add($"    - {slotName} (TomeUp)");
                        }
                        
                        if (job.GlazesNeeded > 0)
                            weekPlan.Allocations.Add($"    - {job.GlazesNeeded}x Glaze");
                        if (job.TwinesNeeded > 0)
                            weekPlan.Allocations.Add($"    - {job.TwinesNeeded}x Twine");
                    }
                }
            }
        }
    }

    private static (MemberState Member, MemberJobState Job)? FindMemberAndJobNeedingSlot(List<MemberState> members, string slotName, int jobPriority)
    {
        // Find member and job at the specified priority level that needs this slot
        // Only assign if they want Savage for this slot (floors only drop Savage)
        foreach (var member in members)
        {
            var targetJob = member.Jobs.FirstOrDefault(j => j.JobPriority == jobPriority);
            if (targetJob == null) continue;

            if (targetJob.GearNeeds.TryGetValue(slotName, out var gearState) && 
                !gearState.HasSavage && 
                gearState.DesiredSource == Models.GearSource.Savage)
            {
                return (member, targetJob);
            }
        }
        
        return null;
    }

    private static void AllocateBooks(List<MemberState> members, WeeklyPlan weekPlan, bool mainJobOnly = false, bool allowFloor4Trading = false)
    {
        // Strategy: Reserve Floor 4 books for weapons FIRST, then trade excess to other floors
        // UNLESS allowFloor4Trading is false, in which case only reserve for weapons
        
        // Calculate how many Floor 4 books we need for weapons
        int mainWeaponsNeeded = members.Count(m => !m.HasMainWeapon);
        int altWeaponsNeeded = 0;
        foreach (var member in members)
        {
            for (int i = 0; i < member.HasAltWeapons.Count; i++)
            {
                if (!member.HasAltWeapons[i] && member.HasMainWeapon)
                {
                    altWeaponsNeeded++;
                }
            }
        }
        int totalWeaponsNeeded = (mainWeaponsNeeded + altWeaponsNeeded) * 8;
        
        // Reserve Floor 4 books for weapons (don't trade these away yet)
        var totalFloor4Available = members.Sum(m => m.Floor4Books);
        var floor4Reserved = Math.Min(totalFloor4Available, totalWeaponsNeeded);
        var floor4AvailableForTrade = totalFloor4Available - floor4Reserved;

        // Only allow trading if a member has all weapons AND flag is set
        if (!allowFloor4Trading)
        {
            floor4AvailableForTrade = 0; // Don't trade any Floor 4 books if not allowed
        }

        // Calculate book needs for the remaining gear, glazes, and twines
        var floor1Need = 0;
        var floor2Need = 0;
        var floor3Need = 0;
        
        var deficitDebug = new List<string>();
        
        foreach (var member in members)
        {
            foreach (var job in member.Jobs)
            {
                // Glazes need Floor 2 books
                if (job.GlazesNeeded > 0)
                {
                    floor2Need += job.GlazesNeeded * 3;
                    deficitDebug.Add($"  {member.MemberName} {job.JobName}: +{job.GlazesNeeded * 3} Floor2 for {job.GlazesNeeded} Glazes");
                }
                
                // Twines need Floor 3 books
                if (job.TwinesNeeded > 0)
                {
                    floor3Need += job.TwinesNeeded * 4;
                    deficitDebug.Add($"  {member.MemberName} {job.JobName}: +{job.TwinesNeeded * 4} Floor3 for {job.TwinesNeeded} Twines");
                }
                
                // Gear pieces need their respective floors
                foreach (var (slotName, gearState) in job.GearNeeds)
                {
                    if (!gearState.HasSavage && GearSlotMap.TryGetValue(slotName, out var info))
                    {
                        // Gear piece not yet obtained - needs book cost
                        switch (info.Floor)
                        {
                            case 1: floor1Need += info.BookCost; break;
                            case 2: floor2Need += info.BookCost; break;
                            case 3: floor3Need += info.BookCost; break;
                        }
                        deficitDebug.Add($"  {member.MemberName} {job.JobName}: +{info.BookCost} Floor{info.Floor} for {slotName} (Savage missing)");
                    }
                    else if (gearState.HasSavage && gearState.NeedsUpgrade)
                    {
                        // TomeUp piece obtained but needs upgrade - count as twine/glaze need
                        // Glazes upgrade: Ears, Neck, Wrists, Ring1, Ring2
                        // Twines upgrade: Head, Body, Hands, Legs, Feet
                        if (new[] { "Ears", "Neck", "Wrists", "Ring1", "Ring2" }.Contains(slotName))
                        {
                            floor2Need += 3; // Each glaze costs 3 Floor 2 books
                            deficitDebug.Add($"  {member.MemberName} {job.JobName}: +3 Floor2 for {slotName} (TomeUp upgrade)");
                        }
                        else if (new[] { "Head", "Body", "Hands", "Legs", "Feet" }.Contains(slotName))
                        {
                            floor3Need += 4; // Each twine costs 4 Floor 3 books
                            deficitDebug.Add($"  {member.MemberName} {job.JobName}: +4 Floor3 for {slotName} (TomeUp upgrade)");
                        }
                    }
                }
            }
        }
        
        if (deficitDebug.Count > 0)
        {
            foreach (var line in deficitDebug)
            {
                weekPlan.Allocations.Add(line);
            }
        }

        // Calculate per-member deficits (members can only spend their own books)
        // A member has a deficit if they need books but don't have enough individually
        int floor1MemberDeficit = 0;
        int floor2MemberDeficit = 0;
        int floor3MemberDeficit = 0;
        
        foreach (var member in members)
        {
            foreach (var job in member.Jobs)
            {
                // Check how many books this job needs
                int jobFloor1Need = 0;
                int jobFloor2Need = 0;
                int jobFloor3Need = 0;
                
                // Count glazes and twines
                jobFloor2Need += job.GlazesNeeded * 3;
                jobFloor3Need += job.TwinesNeeded * 4;
                
                // Count gear pieces
                foreach (var (slotName, gearState) in job.GearNeeds)
                {
                    if (!gearState.HasSavage && GearSlotMap.TryGetValue(slotName, out var info))
                    {
                        switch (info.Floor)
                        {
                            case 1: jobFloor1Need += info.BookCost; break;
                            case 2: jobFloor2Need += info.BookCost; break;
                            case 3: jobFloor3Need += info.BookCost; break;
                        }
                    }
                    else if (gearState.HasSavage && gearState.NeedsUpgrade)
                    {
                        if (new[] { "Ears", "Neck", "Wrists", "Ring1", "Ring2" }.Contains(slotName))
                        {
                            jobFloor2Need += 3;
                        }
                        else if (new[] { "Head", "Body", "Hands", "Legs", "Feet" }.Contains(slotName))
                        {
                            jobFloor3Need += 4;
                        }
                    }
                }
                
                // Add individual member deficits
                if (jobFloor1Need > member.Floor1Books)
                    floor1MemberDeficit += (jobFloor1Need - member.Floor1Books);
                if (jobFloor2Need > member.Floor2Books)
                    floor2MemberDeficit += (jobFloor2Need - member.Floor2Books);
                if (jobFloor3Need > member.Floor3Books)
                    floor3MemberDeficit += (jobFloor3Need - member.Floor3Books);
            }
        }
        
        // Use the per-member deficits as-is - they represent what each member needs vs. their current holdings
        // Cap to the total NEED (not the member deficit which represents member-by-member constraints)
        var floor1Deficit = Math.Min(floor1MemberDeficit, floor1Need);
        var floor2Deficit = Math.Min(floor2MemberDeficit, floor2Need);
        var floor3Deficit = Math.Min(floor3MemberDeficit, floor3Need);
        
        // Also calculate shortfall (books needed - books available) to ensure complete allocation
        // This ensures we trade enough Floor 4 books to cover all needs
        int totalFloor1Available = members.Sum(m => m.Floor1Books);
        int totalFloor2Available = members.Sum(m => m.Floor2Books);
        int totalFloor3Available = members.Sum(m => m.Floor3Books);
        
        int floor1Shortfall = Math.Max(0, floor1Need - totalFloor1Available);
        int floor2Shortfall = Math.Max(0, floor2Need - totalFloor2Available);
        int floor3Shortfall = Math.Max(0, floor3Need - totalFloor3Available);
        
        // Use the GREATER of deficit or shortfall to ensure complete coverage
        var floor1ToTrade = Math.Max(floor1Deficit, floor1Shortfall);
        var floor2ToTrade = Math.Max(floor2Deficit, floor2Shortfall);
        var floor3ToTrade = Math.Max(floor3Deficit, floor3Shortfall);
        
        // Debug: Show trading details
        if (floor4AvailableForTrade > 0 || floor1ToTrade > 0 || floor2ToTrade > 0 || floor3ToTrade > 0)
        {
            weekPlan.Allocations.Add($"[DEBUG TRADING] Floor4Available={floor4AvailableForTrade}, Floor1ToTrade={floor1ToTrade}, Floor2ToTrade={floor2ToTrade}, Floor3ToTrade={floor3ToTrade}");
        }
        
        // Trade only the excess Floor 4 books (those not reserved for weapons)
        // Prioritize: Floor 2 (glazes) > Floor 3 (twines) > Floor 1 (gear)
        if (floor2ToTrade > 0 && floor4AvailableForTrade > 0)
        {
            var booksToTrade = Math.Min(floor4AvailableForTrade, floor2ToTrade);
            TradeFloor4BooksToOtherFloor(members, 2, booksToTrade, weekPlan);
            floor4AvailableForTrade -= booksToTrade;
        }
        
        if (floor3ToTrade > 0 && floor4AvailableForTrade > 0)
        {
            var booksToTrade = Math.Min(floor4AvailableForTrade, floor3ToTrade);
            TradeFloor4BooksToOtherFloor(members, 3, booksToTrade, weekPlan);
            floor4AvailableForTrade -= booksToTrade;
        }
        
        if (floor1ToTrade > 0 && floor4AvailableForTrade > 0)
        {
            var booksToTrade = Math.Min(floor4AvailableForTrade, floor1ToTrade);
            TradeFloor4BooksToOtherFloor(members, 1, booksToTrade, weekPlan);
        }

        // Now allocate weapons (if Floor 4 books available and no weapon yet)
        // Prioritize main job weapons
        foreach (var member in members)
        {
            if (!member.HasMainWeapon && member.Floor4Books >= 8)
            {
                member.Floor4Books -= 8;
                member.HasMainWeapon = true;
                var mainJob = member.Jobs.FirstOrDefault(j => j.IsMainJob);
                var jobName = mainJob?.JobName ?? "Main";
                weekPlan.Allocations.Add($"{member.MemberName} ({jobName}): BUYS Main Hand (Savage) - SPENDS 8 Floor 4 Books");
                // Update GearNeeds to mark MainHand as obtained
                if (mainJob != null && mainJob.GearNeeds.TryGetValue("MainHand", out var mhGear))
                {
                    mhGear.HasSavage = true;
                }
            }
        }

        // Then allocate alt job weapons only after all main job weapons are allocated
        for (int memberIdx = 0; memberIdx < members.Count; memberIdx++)
        {
            var member = members[memberIdx];
            for (int altIdx = 0; altIdx < member.HasAltWeapons.Count; altIdx++)
            {
                if (!member.HasAltWeapons[altIdx] && member.Floor4Books >= 8 && member.HasMainWeapon)
                {
                    member.Floor4Books -= 8;
                    member.HasAltWeapons[altIdx] = true;
                    // Get the alt job at the correct index (skip main job which is at index 0)
                    var altJob = member.Jobs.ElementAtOrDefault(altIdx + 1);
                    if (altJob != null)
                    {
                        weekPlan.Allocations.Add($"    - {member.MemberName} ({altJob.JobName}): BUYS Main Hand (Savage) - SPENDS 8 Floor 4 Books");
                        // Update GearNeeds to mark MainHand as obtained
                        if (altJob.GearNeeds.TryGetValue("MainHand", out var mhGear))
                        {
                            mhGear.HasSavage = true;
                        }
                    }
                }
            }
        }

        // Prioritize upgrades (Glazes/Twines) for all jobs
        foreach (var member in members)
        {
            foreach (var job in member.Jobs)
            {
                // Buy Glazes first (more efficient)
                while (job.GlazesNeeded > 0 && member.Floor2Books >= 3)
                {
                    member.Floor2Books -= 3;
                    job.GlazesNeeded--;
                    
                    // Mark a Glaze-upgradeable piece as upgraded
                    var glazeSlot = job.GearNeeds.FirstOrDefault(g => 
                        new[] { "Ears", "Neck", "Wrists", "Ring1", "Ring2" }.Contains(g.Key) && 
                        g.Value.NeedsUpgrade).Key;
                    if (glazeSlot != null)
                    {
                        job.GearNeeds[glazeSlot].NeedsUpgrade = false;
                    }
                    
                    weekPlan.Allocations.Add($"    - {member.MemberName} ({job.JobName}): BUYS Glaze - SPENDS 3 Floor 2 Books");
                }

                // Buy Twines
                while (job.TwinesNeeded > 0 && member.Floor3Books >= 4)
                {
                    member.Floor3Books -= 4;
                    job.TwinesNeeded--;
                    
                    // Mark a Twine-upgradeable piece as upgraded
                    var twineSlot = job.GearNeeds.FirstOrDefault(g => 
                        new[] { "Head", "Body", "Hands", "Legs", "Feet" }.Contains(g.Key) && 
                        g.Value.NeedsUpgrade).Key;
                    if (twineSlot != null)
                    {
                        job.GearNeeds[twineSlot].NeedsUpgrade = false;
                    }
                    
                    weekPlan.Allocations.Add($"    - {member.MemberName} ({job.JobName}): BUYS Twine - SPENDS 4 Floor 3 Books");
                }
            }
        }

        // Then allocate gear to all jobs
        AllocateGearToJobs(members, false, weekPlan);
    }

    private static void TradeFloor4BooksToOtherFloor(List<MemberState> members, int targetFloor, int booksToTrade, WeeklyPlan weekPlan)
    {
        var remaining = booksToTrade;
        var targetFloorName = targetFloor switch
        {
            1 => "Floor1",
            2 => "Floor2",
            3 => "Floor3",
            _ => "Unknown"
        };
        
        // Priority 1: Members who need this floor and have Floor4 books (they can trade for their own needs)
        var needingMembers = members
            .Where(m => !m.IsFullyGeared() && m.Floor4Books > 0)
            .OrderBy(m => m.MemberName)
            .ToList();
        
        foreach (var member in needingMembers)
        {
            if (remaining <= 0) break;
            
            // Check if this member needs books on the target floor
            int memberNeedOnFloor = 0;
            foreach (var job in member.Jobs)
            {
                if (targetFloor == 2)
                    memberNeedOnFloor += job.GlazesNeeded * 3;
                else if (targetFloor == 3)
                    memberNeedOnFloor += job.TwinesNeeded * 4;
                
                foreach (var (slotName, gearState) in job.GearNeeds)
                {
                    if (!gearState.HasSavage && GearSlotMap.TryGetValue(slotName, out var info) && info.Floor == targetFloor)
                    {
                        memberNeedOnFloor += info.BookCost;
                    }
                    else if (gearState.HasSavage && gearState.NeedsUpgrade)
                    {
                        bool isGlaze = new[] { "Ears", "Neck", "Wrists", "Ring1", "Ring2" }.Contains(slotName);
                        bool isTwine = new[] { "Head", "Body", "Hands", "Legs", "Feet" }.Contains(slotName);
                        if ((isGlaze && targetFloor == 2) || (isTwine && targetFloor == 3))
                            memberNeedOnFloor += (isGlaze ? 3 : 4);
                    }
                }
            }
            
            // Get current books on this floor
            int currentBooks = targetFloor switch
            {
                1 => member.Floor1Books,
                2 => member.Floor2Books,
                3 => member.Floor3Books,
                _ => 0
            };
            
            // Trade if they need more books on this floor
            while (member.Floor4Books > 0 && remaining > 0 && currentBooks < memberNeedOnFloor)
            {
                member.Floor4Books--;
                currentBooks++;
                
                switch (targetFloor)
                {
                    case 1: member.Floor1Books++; break;
                    case 2: member.Floor2Books++; break;
                    case 3: member.Floor3Books++; break;
                }
                
                weekPlan.Allocations.Add($"    - {member.MemberName}: TRADES 1 Floor 4 Book → {targetFloorName}");
                remaining--;
            }
        }
        
    }

    private static void AllocateGearToJobs(List<MemberState> members, bool mainJobsOnly, WeeklyPlan weekPlan)
    {
        // Keep trying to allocate gear until no more purchases are possible
        bool madeProgress = true;
        while (madeProgress)
        {
            madeProgress = false;
            
            foreach (var member in members)
            {
                // First allocate to main jobs (priority), then alt jobs
                var allJobs = member.Jobs.OrderBy(j => j.JobPriority).ToList();

                foreach (var job in allJobs)
                {
                    // Process gear slots in order of cost (cheapest first) to maximize utilization
                    var gearBySlot = job.GearNeeds
                        .Where(g => !g.Value.HasSavage && g.Value.DesiredSource == Models.GearSource.Savage)
                        .OrderBy(g => GearSlotMap.TryGetValue(g.Key, out var info) ? info.BookCost : 999)
                        .ToList();
                    
                    foreach (var (slotName, gearState) in gearBySlot)
                    {
                        // Try to buy this piece
                        if (GearSlotMap.TryGetValue(slotName, out var info))
                        {
                            int bookCost = info.BookCost;
                            int availableBooks = info.Floor switch
                            {
                                1 => member.Floor1Books,
                                2 => member.Floor2Books,
                                3 => member.Floor3Books,
                                _ => 0
                            };

                            if (availableBooks >= bookCost)
                            {
                                // Spend books
                                switch (info.Floor)
                                {
                                    case 1: member.Floor1Books -= bookCost; break;
                                    case 2: member.Floor2Books -= bookCost; break;
                                    case 3: member.Floor3Books -= bookCost; break;
                                }

                                gearState.HasSavage = true;
                                weekPlan.Allocations.Add($"    - {member.MemberName} ({job.JobName}): BUYS {slotName} (Savage) - SPENDS {bookCost} Floor {info.Floor} Books");
                                madeProgress = true;
                            }
                        }
                    }
                }
            }
        }
    }
}
