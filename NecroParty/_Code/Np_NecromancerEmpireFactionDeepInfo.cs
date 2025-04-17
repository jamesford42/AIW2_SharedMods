
using System;
using System.Text;
using Arcen.Universal;
using Arcen.AIW2.Core;
using Arcen.AIW2.External;
using System.Linq;

namespace NecroParty
{
    public class Np_NecromancerEmpireFactionDeepInfo : NecromancerEmpireFactionDeepInfo
    {
        public bool LogStats
        {
            get
            {
                return GameSettings.Current.GetBoolBySetting("Np_LogLight", false);
            }
        }
        
        public bool LogAll
        {
            get
            {
                return GameSettings.Current.GetBoolBySetting("Np_LogAll", false);
            }
        }
        
        public override void SeedSpecialEntities_LateAfterAllFactionSeeding_CustomForPlayerType( Galaxy Galaxy, ArcenHostOnlySimContext Context, MapTypeData MapData )
        {
            if (Engine_AIW2.Instance.ErrorsDuringCurrentMapGen.Count > 0)
                return;
            
            if (!(ExternalDeepLinkTable.Instance.GetRowByName("SeedEntityAlgorithmLink")?.Singleton as SeedEntityAlgorithm)?.Enabled??false)
            {
                Engine_AIW2.Instance.LogErrorDuringCurrentMapGen( "\n<u>Necro Party</u> depends on <u>Generator</u> mod.\nEnsure it is enabled.\n\nAlso ensure the following setting:\nGalaxy Settings > Map Population > Map Populator is <u>Improved</u>.");
                return;
            }
                
            int extraDistanceForAdajentSeededItems = AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "ExtraDistanceForAdajentSeededItems" );
            int extraDistanceForMiddleDistanceItems = AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "ExtraDistanceForMiddleDistanceItems" );
            int reducedDistanceRestrictionForAnyItems = AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "ReducedDistanceRestrictionForAnyItems" );
            
            Context.RandomToUse.ReinitializeWithSeed(Galaxy.RandomSeedBase.HashCombine(this.AttachedFaction.FactionIndex));
            
            // nearby skeleton amp
            StandardMapPopulator.Mapgen_SeedSpecialEntities( Context, Galaxy, null, FactionType.AI, SpecialEntityType.None, "SkeletonAmplifier", SeedingType.CapturableWeightsAndMax,
                        1, MapGenCountPerPlanet.One, MapGenSeedStyle.BigGood, 1, 1 + extraDistanceForAdajentSeededItems, 2, -1, PlanetSeedingZone.MostAnywhere, SeedingExpansionType.ExpandPlayerMaxOnly,
                                     this.AttachedFaction, (Int16)(1 + extraDistanceForAdajentSeededItems) );
            // nearby wight amp
            StandardMapPopulator.Mapgen_SeedSpecialEntities( Context, Galaxy, null, FactionType.AI, SpecialEntityType.None, "WightAmplifier", SeedingType.CapturableWeightsAndMax,
                        1, MapGenCountPerPlanet.One, MapGenSeedStyle.BigGood, 2 - reducedDistanceRestrictionForAnyItems, 3 + extraDistanceForAdajentSeededItems, 2, -1, PlanetSeedingZone.MostAnywhere, SeedingExpansionType.ExpandPlayerMaxOnly,
                                     this.AttachedFaction, (Int16)(3 + extraDistanceForAdajentSeededItems) );
            
            // galaxy wide amps
            if (NecromancerEmpireFactionBaseInfo.GetLastNecromancerFactionOrNull() == this.AttachedFaction)
            {
                int necromancersInGame = NecromancerEmpireFactionBaseInfo.GetNecromancerFactionCount();
                int skeletonAmps = 4 + necromancersInGame * 2;
                int wightAmps = 2 + necromancersInGame * 2;
                int mummyAmps = 1 + necromancersInGame;
                
                StandardMapPopulator.Mapgen_SeedSpecialEntities( Context, Galaxy, null, FactionType.AI, SpecialEntityType.None, "SkeletonAmplifier", SeedingType.CapturableWeightsAndMax,
                                                            skeletonAmps, MapGenCountPerPlanet.One, MapGenSeedStyle.BigGood, 3 - reducedDistanceRestrictionForAnyItems, 99, 3, -1, PlanetSeedingZone.MostAnywhere, SeedingExpansionType.ExpandPlayerMaxOnly, null, -1 );
                
                StandardMapPopulator.Mapgen_SeedSpecialEntities( Context, Galaxy, null, FactionType.AI, SpecialEntityType.None, "WightAmplifier", SeedingType.CapturableWeightsAndMax,
                                                            wightAmps, MapGenCountPerPlanet.One, MapGenSeedStyle.BigGood, 4 - reducedDistanceRestrictionForAnyItems, 99, 3, -1, PlanetSeedingZone.MostAnywhere, SeedingExpansionType.ExpandPlayerMaxOnly, null, -1 );
                
                StandardMapPopulator.Mapgen_SeedSpecialEntities( Context, Galaxy, null, FactionType.AI, SpecialEntityType.None, "MummyAmplifier", SeedingType.CapturableWeightsAndMax,
                                                            mummyAmps, MapGenCountPerPlanet.One, MapGenSeedStyle.BigGood, 5 - reducedDistanceRestrictionForAnyItems, 99, 2, -1, PlanetSeedingZone.MostAnywhere, SeedingExpansionType.ExpandPlayerMaxOnly, null, -1 );
            }

            // rifts
            SeedRifts(Galaxy, Context, MapData);
        }
        
        private void SeedRifts( Galaxy Galaxy, ArcenHostOnlySimContext Context, MapTypeData MapData )
        {
            int debugcode = 0;
            ObjectBag UpgradeChoices = null;
            ObjectList AllUpgrades = null;
            ObjectList SeededUpgrades = null;
            ObjectList SeededRifts = null;
            
            ScopedLogger _log = null;
            ScopedLogger log = null;
            ScopedLogger log_brief = null;
            
            var templar = World_AIW2.Instance.GetSpecialFactionInstanceByName("Templar");
            if ( templar == null )
            {
                LOG.Err( "Error in {0}() no templar faction found.", this.TypeNameAndMethod() );
                return;
            }

            if (LogAll)
            {
                _log = log = log_brief = new ScopedLogger();
            }
            else if (LogStats)
            {
                _log = log_brief = new ScopedLogger();
            }
            
            try
            {
                float mult = AIWar2GalaxySettingTable.GetFauxFloatValueFromSettingByName_DuringGame( "TemplarRiftsToSeed", 0.0f );
                if (mult <= 0)
                    return;
                
                int playerCount = NecromancerEmpireFactionBaseInfo.GetNecromancerFactionCount();
                if (playerCount <= 0)
                    return;
                
                log?.Line("Seeding Rifts for {0}.", this.AttachedFaction.GetDisplayName_Short(15));
                log?.Scope();
                
                UpgradeChoices = ObjectBag.GetTemporary("Necromancer_UpgradeChoices", 5.0f);
                AllUpgrades = ObjectList.GetTemporary("Necromancer_ValidUpgrades", 5.0f);
                SeededUpgrades = ObjectList.GetTemporary("Necromancer_SeededUpgrades", 5.0f);
                SeededRifts = ObjectList.GetTemporary("Necromancer_SeededRifts", 5.0f);

                int extraDistForAdjacent = AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "ExtraDistanceForAdajentSeededItems" );
                int extraDisForMiddle = AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "ExtraDistanceForMiddleDistanceItems" );
                int reducedDistAll = AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "ReducedDistanceRestrictionForAnyItems" );
                int numUpgradesPer = 2 + AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "BonusNecromancerRiftOptions" );
                
                int countToSeed = 0;
                float raw = 0;
                {
                    int numPlanets = Galaxy.GetTotalPlanetCount();
                    if (numPlanets < 80)
                        numPlanets = 80;
                    
                    raw = numPlanets / 5.0f;
                    if (playerCount > 1)
                        raw += numPlanets / 10.0f;

                    countToSeed = (int)(raw * mult);
                    
                    // Because we have already seeded the initial rift for each player.
                    countToSeed -= playerCount;
                }

                int numUpgrades = 0;
                int numConstructs = 0;
                int numSkeletons = 0;
                int numWights = 0;
                int numMummies = 0;
                int numFlagships = 0;
                int numBoosts = 0;
                foreach (var row in NecromancerUpgradeTable.Instance.Rows)
                {
                    if (row.ShouldNotAppearInRift)
                        continue;
                    
                    if (row.Type == NecromancerUpgradeType.UnlockNewShip)
                        numConstructs++;
                    else if (row.Type == NecromancerUpgradeType.UnlockSkeletonType)
                        numSkeletons++;
                    else if (row.Type == NecromancerUpgradeType.UnlockWightType)
                        numWights++;
                    else if (row.Type == NecromancerUpgradeType.UnlockMummyType)
                        numMummies++;
                    else if (row.Type == NecromancerUpgradeType.ClaimBlueprints)
                        numFlagships++;
                    else if (Arcen.AIW2.External.Extensions.ToString(row.Type).Contains("Increase"))
                        numBoosts++;
                    
                    AllUpgrades.Add(row);
                    
                    numUpgrades++;
                }
                
                //log?.Line("Extra Dist Adjacent = {0}", extraDistForAdjacent);
                //log?.Line("Extra Dist Middle = {0}", extraDisForMiddle);
                //log?.Line("Reduced Dist All = {0}", reducedDistAll);
                log?.Line("Num Rifts to Seed = {0} (raw={1} mult={2})", countToSeed, raw, mult);
                log?.Line("Upgrades Per Rift = {0}", numUpgradesPer);
                log?.Line();
                //log?.Line("There are {0} possible necromancer upgrades.", numUpgrades);
                //log?.Line("   {0} Constructs", numUpgrades);
                //log?.Line("   {0} Skeletons", numSkeletons);
                //log?.Line("   {0} Wights", numWights);
                //log?.Line("   {0} Mummies", numMummies);
                //log?.Line("   {0} Flagships", numFlagships);
                //log?.Line("   {0} Boosts", numBoosts);
                //log?.Line();

                #region Local Methods
                bool IsValid(NecromancerUpgrade u)
                {
                    if (u.ShouldNotAppearInRift)
                        return false;
                    
                    if (u.PrereqUpgradeIndex1 > 0)
                    {
                        if (SeededUpgrades.Find((i)=>(i as NecromancerUpgrade).Index == u.PrereqUpgradeIndex1) == null)
                            return false;
                    }
                    
                    if (u.PrereqUpgradeIndex2 > 0)
                    {
                        if (SeededUpgrades.Find((i)=>(i as NecromancerUpgrade).Index == u.PrereqUpgradeIndex2) == null)
                            return false;
                    }
                    
                    return true;
                }
                
                bool IsNecroHome(Planet p)
                {
                    return p.MapGen_FullyUsingFaction.IsNecromancer();
                }
                
                void GetCount(NecromancerUpgrade u, out int count_same, out int count_group)
                {
                    count_same = 0;
                    count_group = 0;
                    
                    foreach (var e in SeededUpgrades)
                    {
                        var eu = e as NecromancerUpgrade;
                        
                        if (eu.InternalName == u.InternalName)
                            count_same++;
                        
                        var a = eu.OriginalXmlData.GetString("custom_Seeding_Group", null, false);
                        var b = u.OriginalXmlData.GetString("custom_Seeding_Group", null, false);
                        if (a == b && a != null && b != null)
                            count_group++;
                    }
                }
                
                ScopedLogger log_w = log;
                
                int Weight_Div_Per_Same = 4;
                int Weight_Div_Per_Group = 2;
                int Weight_Div_Per_Hop = 3;
                int GetWeight(GameEntity_Squad e, NecromancerUpgrade u)
                {
                    int weight = 100;
                    
                    Planet p = e.Planet;
                    int numHopsAway = p.GetHopsTo(IsNecroHome);
                    
                    int numSame;
                    int numGroup;
                    GetCount(u, out numSame, out numGroup);
                    
                    string custom_Seeding_Group = u.OriginalXmlData.GetString("custom_Seeding_Group", null, false);
                    int custom_Seeding_Weight = u.OriginalXmlData.GetInt32("custom_Seeding_Weight", weight, false);
                    int custom_Seeding_Min = u.OriginalXmlData.GetInt32("custom_Seeding_Min", -1, false);
                    int custom_Seeding_Max = u.OriginalXmlData.GetInt32("custom_Seeding_Max", -1, false);
                    int custom_Seeding_Dist_Min = u.OriginalXmlData.GetInt32("custom_Seeding_Dist_Min", -1, false);
                    int custom_Seeding_Dist_Max = u.OriginalXmlData.GetInt32("custom_Seeding_Dist_Max", -1, false);
                    int custom_Seeding_Weight_Div_Per_Same = u.OriginalXmlData.GetInt32("custom_Seeding_Weight_Div_Per_Same", Weight_Div_Per_Same, false);
                    int custom_Seeding_Weight_Div_Per_Hop = u.OriginalXmlData.GetInt32("custom_Seeding_Weight_Div_Per_Hop", Weight_Div_Per_Hop, false);
                    int custom_Seeding_Weight_Div_Per_Group = u.OriginalXmlData.GetInt32("custom_Seeding_Weight_Div_Per_Group", Weight_Div_Per_Group, false);
                    
                    bool firstRift = e.TypeData.GetHasTag("TemplarInitialRift");
                    bool forFirstRift = u.ForInitialRift;
                    
                    bool isvalid = IsValid(u);
                                                
                    log_w?.Line(u.OrNull());
                    log_w?.Scope();
                    
                    if (!isvalid)
                    {
                        log_w?.Line("prereqs not met, so =0");
                        weight = 0;
                        goto done;
                    }
                    
                    var info = e.CreateExternalBaseInfo<TemplarPerUnitBaseInfo>( "TemplarPerUnitBaseInfo" );
                    if (info.AvailableUpgrades.Any((up)=>up.InternalName==u.InternalName))
                    {
                        log_w?.Line("already offered here, so =0");
                        weight = 0;
                        goto done;
                    }
                    
                    if (firstRift)
                    {
                        if (forFirstRift)
                        {
                            weight = 1000;
                            log_w?.Line("firstRift ({0}) == forFirstRift ({1}), so =1000", firstRift, forFirstRift);
                        }
                    }
                    
                    if (numSame >= custom_Seeding_Max && custom_Seeding_Max != -1)
                    {
                        log_w?.Line("numSame ({0}) >= custom_Seeding_Max ({1}), so =0", numSame, custom_Seeding_Max);
                        weight = 0;
                        goto done;
                    }
                    
                    if (numSame > 0)
                    {
                        int div = (Weight_Div_Per_Same * numSame);
                        int cur = weight;
                        weight = cur / div;
                        log_w?.Line("numSame ({0}) > 0, so {1}/{2}={3}", numSame, cur, div, weight);
                    }
                    
                    if (numGroup > 0)
                    {
                        int div = (Weight_Div_Per_Group * numGroup);
                        int cur = weight;
                        weight = cur / div;
                        log_w?.Line("numGroup ({0}) > 0, so {1}/{2}={3}", numGroup, cur, div, weight);
                    }
                    
                    if (numHopsAway < custom_Seeding_Dist_Min && custom_Seeding_Dist_Min != -1)
                    {
                        int div = Weight_Div_Per_Hop * (custom_Seeding_Dist_Min-numHopsAway);
                        int cur = weight;
                        weight = cur / div;
                        log_w?.Line("numHopsAway ({0}) < custom_Seeding_Dist_Min ({1}), so {2}/{3}={4}", numHopsAway, custom_Seeding_Dist_Min, cur, div, weight);
                    }
                    
                    if (numHopsAway > custom_Seeding_Dist_Max && custom_Seeding_Dist_Max != -1)
                    {
                        int div = Weight_Div_Per_Hop * (numHopsAway-custom_Seeding_Dist_Max);
                        int cur = weight;
                        weight = cur / div;
                        log_w?.Line("numHopsAway ({0}) > custom_Seeding_Dist_Max ({1}), so {2}/{3}={4}", numHopsAway, custom_Seeding_Dist_Max, cur, div, weight);
                    }
                    
                    done:
                    log_w?.Line("Final = {0}", weight);
                    log_w?.End();
                    
                    return weight;
                }
                
                ScopedLogger log_s = log;
                
                void OnSeeded(GameEntity_Squad e)
                {
                    log_s?.Line("{0}", e?.ToStringWithPlanet()??"[null]");
                    SeededRifts.Add(e);
                    
                    //log?.Line("Upgrades");
                    //log?.Scope();
                    
                    var data = e.CreateExternalBaseInfo<TemplarPerUnitBaseInfo>( "TemplarPerUnitBaseInfo" );
                                            
                    for (int i = 0; i < numUpgradesPer; i++)
                    {
                        log_s?.Line("Choosing Upgrade #{0}", i+1);
                        log_s?.Scope();
                        
                        UpgradeChoices.Clear();
                        foreach (var u in AllUpgrades)
                        {
                            var up = u as NecromancerUpgrade;
                            var w = GetWeight(e, u as NecromancerUpgrade);
                            UpgradeChoices.AddItem(u, w);
                        }
                        
                        log_s?.End();
                        
                        var upg = UpgradeChoices.PickRandomItemAndReplace(Context.RandomToUse) as NecromancerUpgrade;
                        if (upg == null)
                        {
                            log_s?.Line("Failed to choose an upgrade ({0} choices).", UpgradeChoices.InternalOnly_totalCount);
                            continue;
                        }
                        
                        data.AvailableUpgrades.Add(upg);
                        
                        SeededUpgrades.Add(upg);
                        
                        log_s?.Line("Chose {0} (w={1})", upg.OrNull(), UpgradeChoices.GetWeightByIndex(UpgradeChoices.GetIndexOfItem(upg)));
                    }
                    
                    if (data.AvailableUpgrades.Count == 0)
                    {
                        log_s?.Line("No upgrades found, despawning.");
                        e.Despawn(Context, true, InstancedRendererDeactivationReason.ErrorWhileChecking);
                    }
                    
                    //log?.End();
                }
                #endregion
                
                var Args = new SeedArgs()
                {
                    TagOrEmpty = "TemplarInitialRift",
                    FactionOrNull = templar,
                    SeedType = SeedingType.CapturableWeightsAndMax, 
                    Count = 1,
                    CountPer = MapGenCountPerPlanet.One,
                    SeedStyle = MapGenSeedStyle.BigGood,
                    MinDistanceFromHumanHomeworld = 1,
                    MaxDistanceFromHumanHomeworld = 1 + extraDistForAdjacent, 
                    MinDistanceFromAIHomeworld = 2,
                    MaxDistanceFromAIHomeworld = -1, 
                    SeedingZone = PlanetSeedingZone.MostAnywhere, 
                    ExpansionStyle = SeedingExpansionType.ExpandPlayerMaxOnly,
                    SpecificFactionToTryToBeCloseTo = this.AttachedFaction,
                    SpecificFactionTryToBeAtMost = (short)(1 + extraDistForAdjacent),
                    Callback = OnSeeded,
                };
                
                // Seed nearby rift, for each player (including sidekicks)
                StandardMapPopulator.Mapgen_SeedSpecialEntities(Context, Galaxy, Args);

                // Handle seeding rifts across the whole galaxy ..
                // Since there could be multiple necromancer players and this is called for all of those
                // only do this for the greatest index (last) of them--So that the initial rifts for
                // them both will already be placed before the following work.
                var last = NecromancerEmpireFactionBaseInfo.GetLastNecromancerFactionOrNull();
                if ( last != this.AttachedFaction)
                    return;
                
                Args.TagOrEmpty = "TemplarNormalRift";
                Args.Count = countToSeed;
                Args.ExpansionStyle = SeedingExpansionType.ComplicatedOriginal;
                Args.MaxDistanceFromHumanHomeworld = -1;
                Args.MinDistanceFromHumanHomeworld = -1;
                Args.SpecificFactionToTryToBeCloseTo = null;
                Args.SpecificFactionTryToBeAtMost = -1;
                
                StandardMapPopulator.Mapgen_SeedSpecialEntities(Context, Galaxy, Args);
                
                var log_e = log_brief;
                if (log_e != null)
                {
                    log_e.Line();
                    log_e.Line("Stats");
                    log_e.Scope();
                   
                    foreach (var u in AllUpgrades)
                    {
                        var count = SeededUpgrades.CountContained(u);
                        var upg = u as NecromancerUpgrade;
                        log_e.Line("{0}x of {1}", count, upg.GetShortDisplayName());
                    }
                    
                    log_e.End();
                }
                
                // Add templar defenders to the closer rifts.
                // They are important for necro early income.
                SeededRifts.StableSort(
                    (a,b)=>
                    {
                        var r_a = a as GameEntity_Squad;
                        var r_b = b as GameEntity_Squad;
                        var d_a = r_a.Planet.GetHopsTo(IsNecroHome);
                        var d_b = r_b.Planet.GetHopsTo(IsNecroHome);
                        return d_a.CompareTo(d_b);
                    } );

                var type = GameEntityTypeDataTable.Instance.GetRowByName( "TemplarInitialEncampment" );
                var diff = FactionUtilityMethods.Instance.GetDifficultyFromNecromancerSettings( this.AttachedFaction );
                var templarDiff = TemplarDifficultyTable.Instance.GetRowByIntensity( diff, templar );
                
                for (int i = 0; i < templarDiff.InitialEncampments; i++)
                {
                    if (SeededRifts.Count <= i)
                        break;
                    
                    var rift = SeededRifts[i] as GameEntity_Squad;
                    var loc = rift.Planet.GetSafePlacementPoint_AroundEntity( Context, type, rift, FInt.FromParts( 0, 100 ), FInt.FromParts( 0, 150 ) );
                    GameEntity_Squad.CreateNew_ReturnNullIfMPClient(
                        rift.PlanetFaction, type, 1, rift.PlanetFaction.Faction.LooseFleet, 0, loc, Context, "Templar-SpawnRift" );
                }
            }
            catch (Exception e)
            {
                LOG.Err("Exception in {0}() debugcode={1}\n{2}", this.TypeNameAndMethod(), debugcode, e);
            }
            finally
            {
                ObjectBag.ReleaseTemporary(UpgradeChoices);
                ObjectList.ReleaseTemporary(AllUpgrades);
                ObjectList.ReleaseTemporary(SeededUpgrades);
                ObjectList.ReleaseTemporary(SeededRifts);
                
                if (_log != null) LOG.Msg(_log.GetDump());
            }
        }

        public override void HandleJournalsAndTips( ArcenHostOnlySimContext Context )
        {
            World_AIW2.Instance.QueueLogJournalEntryToSidebar( "Np_Necromancer_Reminder", string.Empty, this.AttachedFaction, null, null, OnClient.DoThisOnHostOnly_WillBeSentToClients );
            
            // Keep this one, but dont do any other standard tips.
            if ( this.BaseInfo.Necropoleis.Count >= 3 && this.BaseInfo.NumShipyards < 2 )
                World_AIW2.Instance.QueueLogJournalEntryToSidebar( "NA_Necromancer_ShipyardsAreCritical", string.Empty, this.AttachedFaction, null, null, OnClient.DoThisOnHostOnly_WillBeSentToClients );
        }
        
        public override void RemindAboutTipsSidebarIfNecessary( ArcenHostOnlySimContext Context )
        {
            // Don't do this. They are playing a modded necromancer game already.
        }
    }

    public enum NecromancyShipType 
    {
        None,
        Skeleton,
        Wight,
        Mummy,
    }
    
    public static partial class Faction_Extensions
    {
        public static bool IsNecromancer(this Faction faction)
        {
            var player = faction?.PlayerTypeDataOrNull_ModeratelyExpensive;
            if (player == null)
                return false;
            return player.InternalName == "NecromancerEmpire" || player.InternalName == "NecromancerSidekick";
        }
        
        public static NecromancyShipType GetNecromancyShipType( this GameEntityTypeData typeData )
        {
            if (typeData.GetHasTag( "BecomesNecromancySkeleton"))
                return NecromancyShipType.Skeleton;
            
            if (typeData.GetHasTag( "BecomesNecromancyWight"))
                return NecromancyShipType.Wight;
            
            if (typeData.GetHasTag( "BecomesNecromancyMummy"))
                return NecromancyShipType.Mummy;
            
            if (typeData.SpecialType == SpecialEntityType.AIDireGuardian)
                return NecromancyShipType.Mummy;
            
            if (typeData.SpecialType == SpecialEntityType.Frigate || typeData.SpecialType == SpecialEntityType.AIGuardian)
                return NecromancyShipType.Wight;
            
            return NecromancyShipType.None;
        }
    }
}
