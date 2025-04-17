using System;
using System.Text;
using Arcen.Universal;
using Arcen.AIW2.Core;
using Arcen.AIW2.External;

namespace NecroParty
{
    public class Np_NecromancerEmpireFactionBaseInfo : ExternalFactionBaseInfoRoot, IExternalBaseInfo_Singleton
    {
        //Serialized
        public int NumRiftsHacked;
        public readonly List<NecromancerUpgradeEvent> NecromancerHistory = List<NecromancerUpgradeEvent>.Create_WillNeverBeGCed( 10, "Faction-NecromancerUpgradeHistory" );
        public readonly List<NecromancerUpgrade> NecromancerCompletedUpgrades = List<NecromancerUpgrade>.Create_WillNeverBeGCed( 90, "NecromancerEmpireFactionBaseInfo-NecromancerCompletedUpgrades" ); //this is filled out from the NecromancerCompletedUpgradeIndices in the necromancer faction code
        //The battle harvest tracks how many units you've raised via necromancer in a given battle;
        //resets when a battle ends. Used only for UI notifications
        public DictionaryOfDictionaries<Planet, GameEntityTypeData, int> BattleHarvest = DictionaryOfDictionaries<Planet, GameEntityTypeData, int>.Create_WillNeverBeGCed( 300, 30, "TemplarFactionBaseInfo-BattleHarvest" );

        public readonly Dictionary<GameEntityTypeData, int> HackingEarnedPerUnitType = Dictionary<GameEntityTypeData, int>.Create_WillNeverBeGCed( 500, "NecromancerEmpireFactionBaseInfo-HackingEarnedPerUnitType" );
        public readonly Dictionary<GameEntityTypeData, int> EssenceEarnedPerUnitType = Dictionary<GameEntityTypeData, int>.Create_WillNeverBeGCed( 500, "NecromancerEmpireFactionBaseInfo-EssenceEarnedPerUnitType" );
        public readonly Dictionary<GameEntityTypeData, int> ScienceEarnedPerUnitType = Dictionary<GameEntityTypeData, int>.Create_WillNeverBeGCed( 500, "NecromancerEmpireFactionBaseInfo-ScienceEarnedPerUnitType" );
        
        //Not Serialized
        //public static NecromancerEmpireFactionBaseInfo Instance; //there can only ever be one of this faction at a time

        //this gets updated during the process of sim steps, so had to be converted to DoubleBufferedConcurrentLists.  This is less performant
        //than DoubleBufferedList, but won't have cross-threading issues from an activity like that
        public readonly DoubleBufferedConcurrentList<SafeSquadWrapper> Necropoleis = DoubleBufferedConcurrentList<SafeSquadWrapper>.Create_WillNeverBeGCed( 300, "Necromancer-Necropoleis" );

        public readonly DoubleBufferedList<SafeSquadWrapper> Flagships = DoubleBufferedList<SafeSquadWrapper>.Create_WillNeverBeGCed( 20, "Necromancer-Flagships" );
        public readonly DoubleBufferedList<NecromancerUpgrade> AvailableBlueprints = DoubleBufferedList<NecromancerUpgrade>.Create_WillNeverBeGCed( 20, "Necromancer-AvailableBlueprints" );
        private readonly List<SafeSquadWrapper> RemoteShips = List<SafeSquadWrapper>.Create_WillNeverBeGCed( 20, "NecromancerEmpireFactionBaseInfo-RemoteShips" ); //this is filled out from the NecromancerCompletedUpgradeIndices in the necromancer faction code
        private readonly List<SafeSquadWrapper> WorkingList = List<SafeSquadWrapper>.Create_WillNeverBeGCed( 20, "NecromancerEmpireFactionBaseInfo-WorkingList" ); //this is filled out from the NecromancerCompletedUpgradeIndices in the necromancer faction code
        private readonly List<SafeSquadWrapper> StructuresGrantingBonuses = List<SafeSquadWrapper>.Create_WillNeverBeGCed( 20, "NecromancerEmpireFactionBaseInfo-StructuresGrantingBonuses" ); //this is filled out from the NecromancerCompletedUpgradeIndices in the necromancer faction code
        public FInt HackingValueForCurrentlySelectedUnits = FInt.Zero;
        public int BonusStartingResources = 5;
        public int BonusSkeletonCap = 0;
        public int BonusWightCap = 0;
        public bool BonusStartingWight = false;
        public bool StartWithAllUpgrades = false;
        public int NumShipyards = 0;

        //these three will stay true, once tripped to true, until the next reload.  This is by design, so no DoubleBufferedValue<bool> is needed.
        public bool hasAnySkeletons = false;
        public bool hasAnyWights = false;
        public bool hasAnyMummies = false;

        //constants
        public readonly FInt SciencePerUnitNecromanced = FInt.FromParts( 2, 000 );
        public readonly int RequiredHopsBetweenNecropoleis = 1;

        public Np_NecromancerEmpireFactionBaseInfo()
        {
            Cleanup();
        }

        protected override void Cleanup()
        {
            NumRiftsHacked = 0;

            Necropoleis.Clear();
            Flagships.Clear();
            BattleHarvest.Clear();
            HackingValueForCurrentlySelectedUnits = FInt.Zero;
            HackingEarnedPerUnitType.Clear();
            EssenceEarnedPerUnitType.Clear();
            ScienceEarnedPerUnitType.Clear();
            hasAnySkeletons = false;
            hasAnyWights = false;
            hasAnyMummies = false;

            BonusStartingResources = 0;
            BonusSkeletonCap = 0;
            BonusWightCap = 0;
            BonusStartingWight = false;
            StartWithAllUpgrades = false;

            NecromancerHistory.Clear();
            NecromancerCompletedUpgrades.Clear();
            WorkingList.Clear();
            StructuresGrantingBonuses.Clear();
            //Instance = null;
        }

        public override void SerializeFactionTo( SerMetaData MetaData, ArcenSerializationBuffer Buffer, SerializationCommandType SerializationCmdType )
        {
            Buffer.WriteHeaderStringToLogIfLoggingActive( "NecromancerEmpireFactionBaseInfo" );
            Buffer.AddInt32( MetaData, ReadStyle.PosExceptNeg1, NumRiftsHacked, "NumRiftsHacked" );

            Buffer.AddInt16( MetaData, ReadStyle.NonNeg, (Int16)this.NecromancerCompletedUpgrades.Count, "NecromancerCompletedUpgrades" );
            for ( int i = 0; i < this.NecromancerCompletedUpgrades.Count; i++ )
                Buffer.AddInt16( MetaData, ReadStyle.NonNeg, (Int16)this.NecromancerCompletedUpgrades[i].Index, "NecromancerCompletedUpgradeIndex" );

            Buffer.AddInt16( MetaData, ReadStyle.NonNeg, (Int16)this.NecromancerHistory.Count, "NecromancerHistory.Count" );
            for ( int i = 0; i < this.NecromancerHistory.Count; i++ )
                this.NecromancerHistory[i].SerializeTo( MetaData, Buffer, SerializationCmdType );

            Buffer.AddInt16( MetaData, ReadStyle.NonNeg, (Int16)this.BattleHarvest.GetCountOfInnerDicts(), "BattleHarvest.Count" );
            foreach ( KeyValuePair<Planet, Dictionary<GameEntityTypeData, int>> outerPair in BattleHarvest )
            {
                Buffer.AddPlanetIndex_Neg1ToPos( MetaData, outerPair.Key.Index, "BattleHarvest.PlanetIdx" );
                Dictionary <GameEntityTypeData, int> harvest = outerPair.Value;
                Buffer.AddInt16( MetaData, ReadStyle.NonNeg, (Int16)harvest.Count, "Harvest.Count" );
                foreach ( KeyValuePair<GameEntityTypeData, int> innerPair in harvest )
                {
                    GameEntityTypeDataTable.Instance.SerializeByIndex( MetaData, innerPair.Key, Buffer, "UnitHarvested" );
                    Buffer.AddInt32( MetaData, ReadStyle.PosExceptNeg1, innerPair.Value, "NumHarvested" );
                }
            }
            
            Buffer.AddInt16( MetaData, ReadStyle.NonNeg, (Int16)this.HackingEarnedPerUnitType.Count, "HackingEarnedPerUnitType.Count" );
            HackingEarnedPerUnitType.DoFor( delegate ( KeyValuePair<GameEntityTypeData, int> pair )
            {
                GameEntityTypeDataTable.Instance.SerializeByIndex( MetaData, pair.Key, Buffer, "HackingPerUnitType-Key" );
                Buffer.AddInt32( MetaData, ReadStyle.PosExceptNeg1, pair.Value, "HackingPerUnitType-Value" );
                return DelReturn.Continue;
            } );
            Buffer.AddInt16( MetaData, ReadStyle.NonNeg, (Int16)this.EssenceEarnedPerUnitType.Count, "EssenceEarnedPerUnitType.Count" );
            EssenceEarnedPerUnitType.DoFor( delegate ( KeyValuePair<GameEntityTypeData, int> pair )
            {
                GameEntityTypeDataTable.Instance.SerializeByIndex( MetaData, pair.Key, Buffer, "EssencePerUnitType-Key" );
                Buffer.AddInt32( MetaData, ReadStyle.PosExceptNeg1, pair.Value, "EssencePerUnitType-Value" );
                return DelReturn.Continue;
            } );
            Buffer.AddInt16( MetaData, ReadStyle.NonNeg, (Int16)this.ScienceEarnedPerUnitType.Count, "ScienceEarnedPerUnitType.Count" );
            ScienceEarnedPerUnitType.DoFor( delegate ( KeyValuePair<GameEntityTypeData, int> pair )
            {
                GameEntityTypeDataTable.Instance.SerializeByIndex( MetaData, pair.Key, Buffer, "SciencePerUnitType-Key" );
                Buffer.AddInt32( MetaData, ReadStyle.PosExceptNeg1, pair.Value, "SciencePerUnitType-Value" );
                return DelReturn.Continue;
            } );

        }
        public override void DeserializeFactionIntoSelf( SerMetaData MetaData, ArcenDeserializationBuffer Buffer, SerializationCommandType SerializationCmdType )
        {
            Buffer.WriteHeaderStringToLogIfLoggingActive( "NecromancerEmpireFactionBaseInfo" );
            Buffer.ActivateOrAddTrackerByNameIfTracking( "NecromancerEmpireFactionBaseInfo Ext", TrackerStyle.ByTypeOnly );
            NumRiftsHacked = Buffer.ReadInt32( MetaData, ReadStyle.PosExceptNeg1, "NumRiftsHacked" );

            int countToExpect = Buffer.ReadInt16( MetaData, ReadStyle.NonNeg, "NecromancerCompletedUpgrades" );
            this.NecromancerCompletedUpgrades.Clear();
            for ( int i = 0; i < countToExpect; i++ )
                this.NecromancerCompletedUpgrades.Add( NecromancerUpgradeTable.Instance.GetRowByIndex( (Int32)Buffer.ReadInt16( MetaData, ReadStyle.NonNeg, "NecromancerCompletedUpgradeIndex" ) ) );

            countToExpect = Buffer.ReadInt16( MetaData, ReadStyle.NonNeg, "NecromancerHistory.Count" );
            this.NecromancerHistory.DeserializeUncertainNumberOfEntriesIntoExistingList( countToExpect,
                delegate { return NecromancerUpgradeEvent.GetFromPoolOrCreate(); },
                delegate ( NecromancerUpgradeEvent Event ) { Event.DeserializedIntoSelf( MetaData, Buffer, SerializationCmdType ); } );
            
            if ( Buffer.FromGameVersion.GetGreaterThanOrEqualTo( 3, 766 ) )
            {
                countToExpect = Buffer.ReadInt16( MetaData, ReadStyle.NonNeg, "BattleHarvest.Count" );
                for ( int i = 0; i < countToExpect; i++ )
                {
                    Int16 planetIdx = Buffer.ReadPlanetIndex_Neg1ToPos( MetaData, "BattleHarvest.PlanetIdx" );
                    Planet planet = World_AIW2.Instance.GetPlanetByIndex( planetIdx );
                    int innerCount = Buffer.ReadInt16( MetaData, ReadStyle.NonNeg, "Harvest.Count" );
                    for ( int j = 0; j < innerCount; j++ )
                    {
                        GameEntityTypeData unitHarvested = GameEntityTypeDataTable.Instance.DeserializeByIndex( MetaData, Buffer, "UnitHarvested" );
                        int numHarvested = Buffer.ReadInt32( MetaData, ReadStyle.PosExceptNeg1, "NumHarvested" );
                        BattleHarvest[planet][unitHarvested] = numHarvested;
                    }
                }
            }
            if ( Buffer.FromGameVersion.GetGreaterThanOrEqualTo( 3, 781 ) )
            {

                countToExpect = Buffer.ReadInt16( MetaData, ReadStyle.NonNeg, "HackingEarnedPerUnitType.Count" );
                for ( int i = 0; i < countToExpect; i++ )
                {
                    GameEntityTypeData unit = GameEntityTypeDataTable.Instance.DeserializeByIndex( MetaData, Buffer, "HackingPerUnitType-Key" );
                    this.HackingEarnedPerUnitType[unit] = Buffer.ReadInt32( MetaData, ReadStyle.PosExceptNeg1, "HackingPerUnitType-Value" );
                }
                countToExpect = Buffer.ReadInt16( MetaData, ReadStyle.NonNeg, "EssenceEarnedPerUnitType.Count" );
                for ( int i = 0; i < countToExpect; i++ )
                {
                    GameEntityTypeData unit = GameEntityTypeDataTable.Instance.DeserializeByIndex( MetaData, Buffer, "EssencePerUnitType-Key" );
                    this.EssenceEarnedPerUnitType[unit] = Buffer.ReadInt32( MetaData, ReadStyle.PosExceptNeg1, "EssencePerUnitType-Value" );
                }
                countToExpect = Buffer.ReadInt16( MetaData, ReadStyle.NonNeg, "ScienceEarnedPerUnitType.Count" );
                for ( int i = 0; i < countToExpect; i++ )
                {
                    GameEntityTypeData unit = GameEntityTypeDataTable.Instance.DeserializeByIndex( MetaData, Buffer, "SciencePerUnitType-Key" );
                    this.ScienceEarnedPerUnitType[unit] = Buffer.ReadInt32( MetaData, ReadStyle.PosExceptNeg1, "SciencePerUnitType-Value" );
                }
            }

        }

        public override int GetDifficultyOrdinal_OrNegativeOneIfNotRelevant()
        {
            return -1; //necromancer is a player faction, so this is not relevant
        }

        public override float CalculateYourPortionOfPredictedGameLoad_Where100IsANormalAI( ArcenCharacterBufferBase OptionalExplainCalculation )
        {
            if ( OptionalExplainCalculation != null )
                OptionalExplainCalculation.Add( "130 Load From Necromancer Humans" );
            return 130;
        }

        #region DoFactionGeneralAggregationsPausedOrUnpaused
        protected override void DoFactionGeneralAggregationsPausedOrUnpaused()
        {
            //Instance = this;
        }
        #endregion

        #region DoRefreshFromFactionSettings        
        protected override void DoRefreshFromFactionSettings()
        {
            ConfigurationForFaction cfg = this.AttachedFaction.Config;
            BonusStartingResources = cfg.GetIntValueForCustomFieldOrDefaultValue( "BonusStartingResources", true );
            BonusSkeletonCap = cfg.GetIntValueForCustomFieldOrDefaultValue( "BonusSkeletonCap", true );
            BonusWightCap = cfg.GetIntValueForCustomFieldOrDefaultValue( "BonusWightCap", true );
            BonusStartingWight = cfg.GetBoolValueForCustomFieldOrDefaultValue( "BonusStartingWight", true );
            StartWithAllUpgrades = cfg.GetBoolValueForCustomFieldOrDefaultValue( "StartWithAllUpgrades", true );
        }
        #endregion

        #region SetStartingFactionRelationships
        public override void SetStartingFactionRelationships()
        {
            base.SetStartingFactionRelationships();
            AllegianceHelper.AllyThisFactionToHumans( this.AttachedFaction );
        }
        #endregion
        public int GetHighestNecropolisMarkLevel()
        {
            int highestLevel = 0;
            this.Necropoleis.Display_DoFor( delegate ( GameEntity_Squad city ) {
                if ( city.CurrentMarkLevel > highestLevel )
                    highestLevel = city.CurrentMarkLevel;
                return DelReturn.Continue;
            });
            return highestLevel;
        }
        public int GetHighestFlagshipMarkLevel()
        {
            List<SafeSquadWrapper> flagships = this.Flagships.GetDisplayList();
            int highestLevel = 0;
            for ( int i = 0; i < flagships.Count; i++ )
            {
                GameEntity_Squad flagship = flagships[i].GetSquad();
                if ( flagship == null )
                    continue;
                if ( flagship.CurrentMarkLevel > highestLevel )
                    highestLevel = flagship.CurrentMarkLevel;
            }
            return highestLevel;
        }

        #region GetNecromancerStateForDisplay
        public void GetNecromancerStateForDisplay( ArcenDoubleCharacterBuffer buffer )
        {
            //For debug, this goes in the Threat menu
            buffer.Add( "\n" );
            buffer.Add( "We currently have " ).Add( this.NecromancerCompletedUpgrades.Count ).Add( " faction upgrades:\n" );
            for ( int i = 0; i < this.NecromancerCompletedUpgrades.Count; i++ )
            {
                buffer.Add( "\t" ).Add( this.NecromancerCompletedUpgrades[i].ToString() ).Add( "\n" );
            }
            buffer.Add( "And for fleets:\n" );
            World_AIW2.Instance.DoForFleets( this.AttachedFaction, FleetStatus.CenterpieceMustLiveOrLooseFleet, delegate ( Fleet fleet )
            {
                if ( fleet == null || fleet.Centerpiece.GetSquad() == null )
                    return DelReturn.Continue;
                NecromancerMobileFleetBaseInfo necroMobileInfo = fleet.TryGetExternalBaseInfoAs<NecromancerMobileFleetBaseInfo>();
                if ( necroMobileInfo == null ) //wrong kind of fleet
                    return DelReturn.Continue;
                if ( necroMobileInfo.NecromancerCompletedUpgrades.Count > 0 )
                {
                    buffer.Add( fleet.GetName() ).Add( ":\n" );
                    for ( int i = 0; i < necroMobileInfo.NecromancerCompletedUpgrades.Count; i++ )
                    {
                        NecromancerUpgrade upgrade = necroMobileInfo.NecromancerCompletedUpgrades[i];
                        buffer.Add( "\t" ).Add( upgrade.ToString() ).Add( "\n" );
                    }
                }
                return DelReturn.Continue;
            } );
            buffer.Add( "We have " ).Add( this.AttachedFaction.StoredFactionResourceOne.ToString(), "a1ffa1" ).Add( " Essence.\n" );
        }
        #endregion

        #region DoPerSecondLogic_Stage2Aggregating_OnMainThreadAndPartOfSim_ClientAndHost
        public override void DoPerSecondLogic_Stage2Aggregating_OnMainThreadAndPartOfSim_ClientAndHost( ArcenClientOrHostSimContextCore Context )
        {
            int debugCode = 0;
            try
            {
                debugCode = 100;
                Necropoleis.ClearConstructionListForStartingConstruction();
                Flagships.ClearConstructionListForStartingConstruction();
                StructuresGrantingBonuses.Clear();
                NumShipyards = 0;
                debugCode = 150;
                World_AIW2.Instance.DoForFleets( AttachedFaction, FleetStatus.CenterpieceMustLiveOrLooseFleet, delegate ( Fleet fleet )
                {
                    debugCode = 160;
                    if ( fleet == null )
                        return DelReturn.Continue;

                    if (fleet.Category == FleetCategory.PlayerCustomCityFedMobile && fleet.FleetQualifier == "Necro") {
                        NecromancerMobileFleetBaseInfo necroMobileFleetInfo = fleet.GetExternalBaseInfoAs<NecromancerMobileFleetBaseInfo>();
                        if ( necroMobileFleetInfo == null )
                            throw new Exception("bad fleet!");
                        necroMobileFleetInfo?.ResetAggregateUpgradeData();
                    }

                    return DelReturn.Continue;
                } );
                //int bonusSkeletonPercentTemp = 0;
                debugCode = 200;
                AttachedFaction.DoForEntities( delegate ( GameEntity_Squad entity )
                {
                    debugCode = 300;
                    if ( entity == null )
                        return DelReturn.Continue;
                    if ( entity.TypeData.GetHasTag( "NecromancerNecropolis" ) )
                        Necropoleis.AddToConstructionList( entity );
                    if ( entity.TypeData.GetHasTag( "NecromancerFlagship" ) )
                        Flagships.AddToConstructionList( entity );
                    if ( entity.TypeData.GetHasTag( "NecromancerShipyard" ) )
                        NumShipyards++;
                    if ( ArcenNetworkAuthority.DesiredStatus == DesiredMultiplayerStatus.Host &&
                         !entity.TypeData.IsMobile &&
                         entity.GetSecondsSinceCreation() == 10 )
                        entity.FlagAsNeedingFullSyncCheckIfInMultiplayerAndWeAreHost(); //force sync all necromancer structures once a bit after construction; we've had reports that necro structures often don't show up
                    if ( entity.SelfBuildingMetalRemaining > 0 ||
                         entity.SecondsSpentAsRemains > 0 )
                        return DelReturn.Continue; //if this building is under construction, it doesn't grant any bonuses

                    DLC3GameEntityTypeDataExtension entity_DLC3TypeData = entity.TypeData.TryGetDataExtensionAs<DLC3GameEntityTypeDataExtension>( "DLC3" );
                    if ( entity_DLC3TypeData != null &&
                         (entity_DLC3TypeData.BonusSkeletonPercent > 0 ||
                          entity_DLC3TypeData.BonusWightPercent > 0 ||
                          entity_DLC3TypeData.BonusMummyPercent > 0 ) )
                    {
                        //We can't bail out here, since this structure might grant other things to the necromancer as well (like Skeleton Lord Homes)
                        StructuresGrantingBonuses.Add(entity);
                    }

                    FleetMembership mem = entity.FleetMembership;
                    if ( mem == null )
                        return DelReturn.Continue;
                    Fleet unknownFleet = mem.Fleet;
                    if ( unknownFleet == null )
                        return DelReturn.Continue;

                    debugCode = 310;

                    if ( entity_DLC3TypeData == null ) {
                        return DelReturn.Continue;
                    }

                    Fleet mobileCityFedFleet = null;
                    switch (unknownFleet.Category )
                    {
                        case FleetCategory.PlayerCustomCity:
                            mobileCityFedFleet = unknownFleet.GetFleetBolsteredByThisCity();
                            break;
                        case FleetCategory.PlayerCustomCityFedMobile:
                            mobileCityFedFleet = unknownFleet;
                            break;
                    }
                    NecromancerMobileFleetBaseInfo necroMobileFleetInfo = mobileCityFedFleet?.TryGetExternalBaseInfoAs<NecromancerMobileFleetBaseInfo>();
                    if ( necroMobileFleetInfo == null )
                        return DelReturn.Continue;

                    if ( entity_DLC3TypeData.SkeletonCapIncrease > 0 ||
                            entity_DLC3TypeData.SkeletonCapIncreasePerMark > 0 )
                        necroMobileFleetInfo.SkeletonSoftCap.Construction += entity_DLC3TypeData.SkeletonCapIncrease + (entity.CurrentMarkLevel - 1) * entity_DLC3TypeData.SkeletonCapIncreasePerMark;

                    if ( entity_DLC3TypeData.WightCapIncrease > 0 ||
                            entity_DLC3TypeData.WightCapIncreasePerMark > 0 )
                        necroMobileFleetInfo.WightSoftCap.Construction += entity_DLC3TypeData.WightCapIncrease + (entity.CurrentMarkLevel - 1) * entity_DLC3TypeData.WightCapIncreasePerMark;

                    debugCode = 330;

                    if ( !String.IsNullOrEmpty( entity_DLC3TypeData.SkeletonTag ) && entity_DLC3TypeData.SkeletonTypePercent > 0 )
                    {
                        necroMobileFleetInfo.PercentSkeletonType.Construction[entity_DLC3TypeData.SkeletonTag] += entity_DLC3TypeData.SkeletonTypePercent;
                    }
                    if ( !String.IsNullOrEmpty( entity_DLC3TypeData.WightTag ) && entity_DLC3TypeData.WightTypePercent > 0 )
                    {
                        necroMobileFleetInfo.PercentWightType.Construction[entity_DLC3TypeData.WightTag] += entity_DLC3TypeData.WightTypePercent;
                    }
                    if ( !String.IsNullOrEmpty( entity_DLC3TypeData.MummyTag ) && entity_DLC3TypeData.MummyTypePercent > 0 )
                    {
                        necroMobileFleetInfo.PercentMummyType.Construction[entity_DLC3TypeData.MummyTag] += entity_DLC3TypeData.MummyTypePercent;
                    }

                    debugCode = 340;

                    return DelReturn.Continue;
                } );

                Necropoleis.SwitchConstructionToDisplay();
                Flagships.SwitchConstructionToDisplay();
                UpdateBonusUnits();
                debugCode = 350;
                int skeletonSoftCapIncreaseFromUpgrades = getSkeletonSoftCapIncrease( this.NecromancerCompletedUpgrades ) + this.BonusSkeletonCap;
                int wightSoftCapIncreaseFromUpgrades = getWightSoftCapIncrease( this.NecromancerCompletedUpgrades ) + this.BonusWightCap;
                //do for the mobile fleets only
                World_AIW2.Instance.DoForCityFedMobileFleets( AttachedFaction, FleetStatus.CenterpieceMustLiveOrLooseFleet, delegate ( Fleet fleet )
                {
                    debugCode = 360;
                    if (fleet == null || fleet.FleetQualifier != "Necro") {
                        return DelReturn.Continue;
                    }
                    NecromancerMobileFleetBaseInfo necroMobileFleetInfo = fleet.GetExternalBaseInfoAs<NecromancerMobileFleetBaseInfo>();

                    debugCode = 370;
                    necroMobileFleetInfo.SkeletonSoftCap.Construction += skeletonSoftCapIncreaseFromUpgrades + getSkeletonSoftCapIncrease(necroMobileFleetInfo.NecromancerCompletedUpgrades);
                    necroMobileFleetInfo.WightSoftCap.Construction += wightSoftCapIncreaseFromUpgrades + getWightSoftCapIncrease(necroMobileFleetInfo.NecromancerCompletedUpgrades);

                    debugCode = 380;
                    necroMobileFleetInfo.NormalizeNecromancyRatios();

                    fleet.DoForMemberGroupsUnsorted_Sim( delegate ( FleetMembership mem ) {
                        if ( mem.TypeData.GetHasTag( "NecromancerBottomTier" ) )
                            necroMobileFleetInfo.NumSkeletonsInFleet.Construction += mem.GetCountPresent( true, ExtraFromStacks.IncludePrecalc );
                        if ( mem.TypeData.GetHasTag( "NecromancerMidTier" ) )
                            necroMobileFleetInfo.NumWightsInFleet.Construction += mem.GetCountPresent( true, ExtraFromStacks.IncludePrecalc );
                        if ( mem.TypeData.GetHasTag( "NecromancerHighTier" ) )
                            necroMobileFleetInfo.NumMummiesInFleet.Construction += mem.GetCountPresent( true, ExtraFromStacks.IncludePrecalc );
                        return DelReturn.Continue;
                    } );

                    necroMobileFleetInfo.SwitchAggregateUpgradeDataToDisplay();
                    return DelReturn.Continue;
                } );

                debugCode = 400;
                AvailableBlueprints.ClearConstructionListForStartingConstruction();
                for ( int i = 0; i < this.NecromancerCompletedUpgrades.Count; i++ ) {
                    NecromancerUpgrade upgrade = this.NecromancerCompletedUpgrades[i];
                    if ( upgrade.Type == NecromancerUpgradeType.ClaimBlueprints )
                    {
                        AvailableBlueprints.AddToConstructionList(upgrade);
                    }
                }
                AvailableBlueprints.SwitchConstructionToDisplay();
            }
            catch ( Exception e )
            {
                ArcenDebugging.ArcenDebugLogSingleLine( "Hit exception during necromancer stage 2 debugCode " + debugCode + " " + e.ToString(), Verbosity.DoNotShow );
            }
        }
        #endregion

        public override void DoPerSecondLogic_Stage3Main_OnMainThreadAndPartOfSim_ClientAndHost( ArcenClientOrHostSimContextCore Context )
        {
            AllegianceHelper.AllyThisFactionToHumans( AttachedFaction );
            AttritionOrUpdateSummonedShips( AttachedFaction, Context );
            ResetBattleHarvestIfNecessary( AttachedFaction, Context );
            if ( World_AIW2.Instance.Setup.GetStringBySetting("NecromancerAutoDefend") != "Disabled" &&
                 !this.AttachedFaction.UnderPlayerControl() )
            {
                //For Auto-Defend mode
                LoadOrUnloadFlagships( Context );
                UpdateFleetState(Context);
            }
            if ( World_AIW2.Instance.Setup.GetBoolBySetting("NecromancerAutoLoad") &&
                 World_AIW2.Instance.Setup.GetStringBySetting("NecromancerAutoDefend") == "Disabled" )
            {
                LoadOrUnloadFlagships( Context );
            }
    
        }
        private void UpdateFleetState(ArcenClientOrHostSimContextCore Context)
        {
            //this is used for the Auto-Play mode
            Flagships.Display_DoFor(delegate (GameEntity_Squad flagship)
            {
                if (flagship == null)
                    return DelReturn.Continue;
                if ( World_AIW2.Instance.Setup.GetStringBySetting("NecromancerAutoDefend") == "Disabled" )
                    return DelReturn.Continue;

                Fleet fleet = flagship.FleetMembership.Fleet;
                if (fleet == null)
                    return DelReturn.Continue;

                NecromancerMobileFleetBaseInfo fleetInfo = fleet.CreateExternalBaseInfo<NecromancerMobileFleetBaseInfo>( "NecromancerMobileFleetBaseInfo");
                if ( fleetInfo == null )
                {
                    ArcenDebugging.LogSingleLine("no fleet info for " + flagship.ToStringWithPlanet(), Verbosity.DoNotShow );
                    return DelReturn.Continue;
                }
                int currentStrength = fleet.CalculateEffectiveCurrentFleetStrength_PlayerFleetsOnly();
                int totalstrength = fleet.CalculateEffectiveFullFleetStrength_PlayerFleetsOnly();
                fleetInfo.NeedsToRebuild = DoesFleetNeedToRebuild(fleet);
                if ( flagship.GetIsCrippled() )
                    fleetInfo.NeedsToRebuild = true;
                return DelReturn.Continue;
            });
        }
        public static bool DoesFleetNeedToRebuild( Fleet fleet )
        {
            if ( fleet == null )
                return false;
            int currentStrength = fleet.CalculateEffectiveCurrentFleetStrength_PlayerFleetsOnly();
            int totalStrength = fleet.CalculateEffectiveFullFleetStrength_PlayerFleetsOnly();
            if ( currentStrength <= totalStrength / 2 )
                return true;
            return false;
        }
        private void LoadOrUnloadFlagships(ArcenClientOrHostSimContextCore Context)
        {
            //The logic is as follows: If a flagship has wormhole orders, do nothing
            //if a flagship has enemies on its planet, unload
            //if a flagship has no enemies on its planet, load
            Flagships.Display_DoFor(delegate (GameEntity_Squad flagship)
            {
                if (flagship == null)
                    return DelReturn.Continue;

                Fleet fleet = flagship.FleetMembership.Fleet;
                if (fleet == null)
                    return DelReturn.Continue;
                if (flagship.GetDestinationPlanet() != flagship.Planet)
                {
                    //if we are en route someplace, make sure we are always in load mode
                    if ( !fleet.IsFleetInTransportLoadMode )
                        fleet.IsFleetInTransportLoadMode = true;
                    return DelReturn.Continue; //we are going somewhere
                }
                //if we have enemies or are on a hostile planet, unload
                if ( flagship.PlanetFaction.DataByStance[FactionStance.Hostile].TotalStrength > 0 ||
                     flagship.Planet.GetControllingOrInfluencingFaction().GetIsHostileTowards( this.AttachedFaction ))
                {
                    fleet.IsFleetInTransportLoadMode = false;
                    return DelReturn.Continue;
                }
                else
                    fleet.IsFleetInTransportLoadMode = true;
                return DelReturn.Continue; //we are going somewhere
            });
        }

        public void UpdateBonusUnits()
        {
            for ( int i = 0; i < StructuresGrantingBonuses.Count; i++ )
            {
                GameEntity_Squad structure = StructuresGrantingBonuses[i].GetSquad();
                if ( structure == null )
                    continue;
                DLC3GameEntityTypeDataExtension entity_DLC3TypeData = structure.TypeData.TryGetDataExtensionAs<DLC3GameEntityTypeDataExtension>( "DLC3" );
                if ( entity_DLC3TypeData == null )
                {
                    continue;
                }
                GameEntity_Squad necro = FactionUtilityMethods.Instance.GetNecropolisForPlanetOrNull( structure.Planet, this.AttachedFaction );
                if ( necro == null )
                {
                    continue; //no necropolis here, so nothing to bolster
                }
                Fleet fleet = necro.FleetMembership.Fleet.GetFleetBolsteredByThisCity();
                if ( fleet == null )
                {
                    //this can happen if we are a defensive necropolis
                    continue;
                }
                NecromancerMobileFleetBaseInfo necroMobileFleetInfo = fleet.TryGetExternalBaseInfoAs<NecromancerMobileFleetBaseInfo>();
                if ( necroMobileFleetInfo == null )
                {
                    continue;
                }
                necroMobileFleetInfo.BonusSkeletonPercent.Construction += entity_DLC3TypeData.BonusSkeletonPercent;
                necroMobileFleetInfo.BonusWightPercent.Construction += entity_DLC3TypeData.BonusWightPercent;
                necroMobileFleetInfo.BonusMummyPercent.Construction += entity_DLC3TypeData.BonusMummyPercent;
            }
        }
        #region ResetBattleHarvestIfNecessary
        private void ResetBattleHarvestIfNecessary( Faction faction, ArcenClientOrHostSimContextCore Context )
        {
            int interval = 10;
            if ( World_AIW2.Instance.GameSecond % interval == 0 )
            {
                //only check if we want to reset the values every so often
                World_AIW2.Instance.DoForPlanetsSingleThread( false, delegate ( Planet planet )
                {
                    Dictionary<GameEntityTypeData, int> harvest = this.BattleHarvest[planet];
                    if ( harvest.Count <= 0 )
                        return DelReturn.Continue;
                    PlanetFaction pFaction = planet.GetPlanetFactionForFaction( faction );
                    int hostileStrength = pFaction.DataByStance[FactionStance.Hostile].TotalStrength;
                    int friendlyStrength = pFaction.DataByStance[FactionStance.Self].TotalStrength + pFaction.DataByStance[FactionStance.Friendly].TotalStrength;
                    if ( hostileStrength <= 0 || friendlyStrength <= 0 )
                        harvest.Clear();
                    //if there are no enemies here, reset the harvest since the battle is over
                    return DelReturn.Continue;
                } );
            }
        }
        #endregion

        #region AttritionOrUpdateSummonedShips
        private void AttritionOrUpdateSummonedShips( Faction faction, ArcenClientOrHostSimContextCore Context )
        {
            int slowAttritionInterval = 30;
            int fastAttritionInterval = 15;
            int baseSlowAttritionPercent = 5;
            int baseFastAttritionPercent = 20;
            int attritionPercentAtNecropolis = 1;
            if ( World_AIW2.Instance.GameSecond % slowAttritionInterval == 0 )
            {
                faction.DoForEntities( "NecromancerSlowAttrition", delegate ( GameEntity_Squad entity )
                {
                    bool atNecropolis = false;
                    this.Necropoleis.Display_DoFor( delegate ( GameEntity_Squad city ) {
                        if ( entity.Planet == city.Planet )
                        {
                            atNecropolis = true;
                            return DelReturn.Break;
                        }
                        return DelReturn.Continue;
                    });
                    int damageToTake = 0;
                    if ( atNecropolis )
                        damageToTake = (int)(((float)attritionPercentAtNecropolis / 100) * (entity.GetMaxHullPoints()));
                    else
                        damageToTake = (int)(((float)baseSlowAttritionPercent / 100) * (entity.GetMaxHullPoints()));
                    entity.TakeDamageDirectly( damageToTake, null, null, DamageSource.BeingScrapped, Context );
                    return DelReturn.Continue;
                } );
            }
            if ( World_AIW2.Instance.GameSecond % fastAttritionInterval == 0 )
            {
                faction.DoForEntities( "NecromancerFastAttrition", delegate ( GameEntity_Squad entity )
                {
                    Int16 stacksToRemove = (Int16)(entity.ExtraStackedSquadsInThis / -10);
                    if ( stacksToRemove < 0 )
                        entity.AddOrSetExtraStackedSquadsInThis( stacksToRemove, false ); //If we have a bunch of stacks, make them decay quickly
                    int damageToTake = (int)(((float)baseFastAttritionPercent / 100) * (entity.GetMaxHullPoints()));
                    entity.TakeDamageDirectly( damageToTake, null, null, DamageSource.BeingScrapped, Context );

                    return DelReturn.Continue;
                } );
            }
            faction.DoForEntities( "SkeletonVariant", delegate ( GameEntity_Squad entity )
            {
                //check if we've set the decay transformation
                if ( String.IsNullOrEmpty( entity.TransformsIntoAfterTime ) )
                {
                    entity.SecondsTillTransformation = (short)(60 * AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "SkeletonDecayInterval" ));
                    entity.TransformsIntoAfterTime = "BaseSkeleton";
                }
                return DelReturn.Continue;
            } );

            faction.DoForEntities( "NecromancerBaseSkeleton", delegate ( GameEntity_Squad entity )
            {
                //check if we've set the decay transformation
                if ( String.IsNullOrEmpty( entity.TransformsIntoAfterTime ) )
                {
                    entity.SecondsTillTransformation = (short)(60 * AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "SkeletonDecayInterval" ));
                    entity.TransformsIntoAfterTime = "SkeletonAttritioner";
                }
                return DelReturn.Continue;
            } );
            faction.DoForEntities( "WightVariant", delegate ( GameEntity_Squad entity )
            {
                //check if we've set the decay transformation
                if ( String.IsNullOrEmpty( entity.TransformsIntoAfterTime ) )
                {
                    entity.SecondsTillTransformation = (short)(60 * AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "WightDecayInterval" ));
                    entity.TransformsIntoAfterTime = "BaseWight";
                }
                return DelReturn.Continue;
            } );
            faction.DoForEntities( "NecromancerWight", delegate ( GameEntity_Squad entity )
            {
                //check if we've set the decay transformation
                if ( String.IsNullOrEmpty( entity.TransformsIntoAfterTime ) )
                {
                    entity.SecondsTillTransformation = (short)(60 * AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "WightDecayInterval" ));
                    entity.TransformsIntoAfterTime = "WightAttritioner";
                }
                return DelReturn.Continue;
            } );
        }
        #endregion
        
        #region getSkeletonSoftCapIncrease
        public static int getSkeletonSoftCapIncrease( List<NecromancerUpgrade> completedUpgrades )
        {
            int output = 0;
            for ( int i = 0; i < completedUpgrades.Count; i++ )
            {
                NecromancerUpgrade upgrade = completedUpgrades[i];
                if ( upgrade.Type == NecromancerUpgradeType.IncreaseSkeletonSoftCap )
                    output += upgrade.CapIncrease;
            }
            return output;
        }
        #endregion

        #region getWightSoftCapIncrease
        public static int getWightSoftCapIncrease(List<NecromancerUpgrade> completedUpgrades)
        {
            int output = 0;
            for ( int i = 0; i < completedUpgrades.Count; i++ )
            {
                NecromancerUpgrade upgrade = completedUpgrades[i];
                if ( upgrade.Type == NecromancerUpgradeType.IncreaseWightSoftCap )
                    output += upgrade.CapIncrease;
            }
            return output;
        }
        #endregion

        public bool DoesPlanetHaveNecropolis( Planet planet )
        {
            bool foundNecropolis = false;
            this.Necropoleis.Display_DoFor( delegate ( GameEntity_Squad city ) {
                if ( city.Planet == planet ) {
                    foundNecropolis = true;
                    return DelReturn.Break;
                }
                return DelReturn.Continue;
            });
            return foundNecropolis;
        }
        #region TotalMarkLevelForAllNecropoleis
        public int TotalMarkLevelForAllNecropoleis()
        {
            int total = 0;
            this.Necropoleis.Display_DoFor( delegate ( GameEntity_Squad city ) {
                total += city.CurrentMarkLevel;
                return DelReturn.Continue;
            });
            return total;
        }
        #endregion

        #region DoOnLocalStartNonSimUpdates_OnMainThread
        public override void DoOnLocalStartNonSimUpdates_OnMainThread( ArcenClientOrHostSimContextCore Context )
        {
        }
        #endregion

        #region CheckIfPlayerFactionShouldGetRewardBasedOnAUnitDying
        /// <summary>
        /// This is called on every player faction when any unit is killed, period.  It identifies who the killing faction is, and allows for custom logic to be run.
        /// Many times, this will be utterly unrelated to anything the player faction needs to do.  But if the player faction is "the strongest faction of type X on that planet where the thing died,"
        /// for instance, then this is a method where that sort of thing can be calculated and then some reward can be granted.
        /// Note that the necromancer's logic here is different because the killing faction can be null
        /// </summary>
        public override void CheckIfPlayerFactionShouldGetRewardBasedOnAUnitDying( Faction factionThatKilledEntityOrNull, bool IsFromOnlyPartOfStackDying, GameEntity_Squad entity,
            DamageSource Damage, EntitySystem FiringSystemOrNull, int numExtraStacksKilled, ArcenHostOnlySimContext Context )
        {
            FInt multiplier = FInt.One;
            bool debug = false;
            if ( debug )
            {
                if ( factionThatKilledEntityOrNull == null )
                    ArcenDebugging.ArcenDebugLogSingleLine( entity.ToStringWithPlanet() + " was killed by <unknown> " , Verbosity.DoNotShow );
                else
                    ArcenDebugging.ArcenDebugLogSingleLine( entity.ToStringWithPlanet() + " was killed by " + factionThatKilledEntityOrNull.GetDisplayName() , Verbosity.DoNotShow );
            }
            
            if ( this.GetShouldThisNecromancerFactionGetARewardBasedOnThisKill( factionThatKilledEntityOrNull, entity, ref multiplier, Context ) )
            {
                DLC3GameEntityTypeDataExtension entity_DLC3TypeData = entity.TypeData.TryGetDataExtensionAs<DLC3GameEntityTypeDataExtension>( "DLC3" );
                if ( debug ) {
                    int bonusMultiplier = AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "BonusNecromancerResources" );
                    ArcenDebugging.ArcenDebugLogSingleLine( "\t We get resources; multiplier " + multiplier + " (bonus multiplier component: " + bonusMultiplier +")" , Verbosity.DoNotShow );
                }

                if ( entity_DLC3TypeData != null )
                {
                    //Necromancer science/hacking
                    entity_DLC3TypeData.GetNecromancerResourcesToGrantOnDeath(entity,
                            out FInt scienceToGrantOnDeath, out FInt hackingToGrantOnDeath, out FInt resourceOneToGrantOnDeath);

                    if ( hackingToGrantOnDeath > FInt.Zero )
                    {
                        hackingToGrantOnDeath *= multiplier;
                        if ( hackingToGrantOnDeath < FInt.One )
                            hackingToGrantOnDeath = FInt.One;

                        this.AttachedFaction.StoredHacking += hackingToGrantOnDeath;
                        this.HackingEarnedPerUnitType[entity.TypeData] += hackingToGrantOnDeath.IntValue;
                        entity.Planet.NecromancerHackingEarned = hackingToGrantOnDeath.IntValue;
                    }

                    if ( scienceToGrantOnDeath > FInt.Zero )
                    {
                        scienceToGrantOnDeath *= multiplier;
                        if ( scienceToGrantOnDeath < FInt.One )
                            scienceToGrantOnDeath = FInt.One;
                        this.AttachedFaction.StoredScience += scienceToGrantOnDeath;
                        this.ScienceEarnedPerUnitType[entity.TypeData] += scienceToGrantOnDeath.IntValue;
                        entity.Planet.NecromancerScienceEarned = scienceToGrantOnDeath.IntValue;
                    }

                    if ( resourceOneToGrantOnDeath > FInt.Zero )
                    {
                        resourceOneToGrantOnDeath *= multiplier;
                        this.AttachedFaction.StoredFactionResourceOne += resourceOneToGrantOnDeath;
                        this.EssenceEarnedPerUnitType[entity.TypeData] += resourceOneToGrantOnDeath.IntValue;
                        entity.Planet.NecromancerEssenceEarned = resourceOneToGrantOnDeath.IntValue;
                    }

                    if ( entity_DLC3TypeData.NecromancerUpgradeToGrantOnDeath != null)
                    {
                        NecromancerUpgrade upgrade = entity_DLC3TypeData.NecromancerUpgradeToGrantOnDeath;
                        NecromancerEmpireFactionBaseInfo gData = this.AttachedFaction.TryGetExternalBaseInfoAs<NecromancerEmpireFactionBaseInfo>();
                        if ( !gData.NecromancerCompletedUpgrades.Contains( upgrade ) )
                        {
                            gData.NecromancerCompletedUpgrades.Add( upgrade );
                            NecromancerUpgradeEvent thisEvent = NecromancerUpgradeEvent.Create( this.AttachedFaction.FactionIndex, entity.PlanetFaction.Faction.FactionIndex, entity.Planet.Index, upgrade.Index, -1, entity.TypeData );
                            gData.NecromancerHistory.Add( thisEvent );
                        }
                    }
                }
            }
        }
        #endregion

        #region GetShouldThisNecromancerFactionGetARewardBasedOnThisKill
        private bool GetShouldThisNecromancerFactionGetARewardBasedOnThisKill( Faction killingFactionOrNull, GameEntity_Squad entity, ref FInt multiplier, ArcenHostOnlySimContext Context )
        {
            //A bunch of rules; fundamentally, the rules are:
            //if we killed the unit, or were part of the fight, we get full resources
            //If this was killed by an Allied NPC faction, we get partial resources
            //If this was killed by another necromancer (and we weren't in the fight), we get nothing
            //If this was killed by an allied, non-necromancer faction we get partial resources

            FInt resourceMultiplierForAlliedHumanKill = FInt.FromParts( 0, 300 );
            FInt resourceMultiplierForAlliedNPCKill = FInt.FromParts( 0, 750 );

            if ( !entity.PlanetFaction.Faction.GetIsHostileTowards( this.AttachedFaction ) ) 
                return false; //We must be must be hostile to the killed unit
            if ( killingFactionOrNull == this.AttachedFaction )
                return true; //if we killed the unit, we get full resources
            PlanetFaction pFaction = entity.Planet.GetPlanetFactionForFaction( this.AttachedFaction );
            if ( pFaction.DataByStance[FactionStance.Self].TotalStrength > 500 )
                return true; //if we are involved with the fight, we get full resources

            //now for the case where we aren't involved with the fight
            if ( killingFactionOrNull != null &&
                 killingFactionOrNull.GetIsFriendlyTowards( this.AttachedFaction ) )
            {
                //Killed by an ally
                if ( NecromancerEmpireFactionBaseInfo.GetIsThisANecromancerFaction( killingFactionOrNull ) )
                    return false; //if another necromancer killed this, we get nothing
                if ( killingFactionOrNull.Type == FactionType.Player )
                {
                    //30% resources from a human player
                    multiplier = resourceMultiplierForAlliedHumanKill;
                    return true;
                }
                else
                {
                    //75% resources from an NPC faction
                    multiplier = resourceMultiplierForAlliedNPCKill;
                    return true;
                }
            }
            return false; //if this specific necromancer doesn't get these resources, someone else still might
        }
        #endregion

        #region GetCustomHackingHistory
        /// <summary>
        /// If we want a given player faction to have a custom hacking history in place of the regular one, then we can write this here and return true.
        /// If we want it to have ADDED hacking history in addition to the regular one, we can write it here and then return false.
        /// </summary>
        public override bool GetCustomHackingHistory( ArcenDoubleCharacterBuffer Buffer )
        {
            //Show the necromancer upgrade history here.
            if ( this.NecromancerHistory.Count == 0 )
            {
                Buffer.Add( "There is no history" );
                return false;
            }
            for ( int i = this.NecromancerHistory.Count - 1; i >= 0; i-- )
            {
                NecromancerUpgradeEvent nEvent = this.NecromancerHistory[i];
                Buffer.Add( nEvent.ToDebugString );
            }
            return true; //return true so it does not also show the local hacking history
        }
        #endregion

        #region IsPlanetEligibleForNecropolis
        public bool IsPlanetEligibleForNecropolis( Planet planet, out bool isOnNecropolisPlanet )
        {
            isOnNecropolisPlanet = false;
            try
            {
                int fewestHops = -1;
                this.Necropoleis.Display_DoFor( delegate ( GameEntity_Squad city ) {
                    int hops = city.Planet.GetHopsTo( planet );
                    if ( hops < fewestHops || fewestHops == -1 )
                        fewestHops = hops;
                    return DelReturn.Continue;
                });
                if ( fewestHops == 0 )
                    isOnNecropolisPlanet = true;

                if ( fewestHops < this.RequiredHopsBetweenNecropoleis )
                    return false;

            }
            catch //(Exception e)
            {
                //this can race with the sim code, so just assume that it's bad
                return false;
            }
            return true;
        }
        #endregion

        public static int GetNecromancerFactionCount()
        {
            int count = 0;
            PlayerTypeData necroType = PlayerTypeDataTable.Instance.GetRowByName( "NecromancerEmpire" );
            count += (necroType == null ? 0 : necroType.CurrentFactionsInThisGame.Count);
            necroType = PlayerTypeDataTable.Instance.GetRowByName( "NecromancerSidekick" );
            count += (necroType == null ? 0 : necroType.CurrentFactionsInThisGame.Count);
            return count;
        }

        public static bool GetIsThisANecromancerFaction( Faction fac )
        {
            if ( fac == null )
                return false;
            if ( fac.Type != FactionType.Player )
                return false;
            PlayerTypeData playerTypeData = fac.PlayerTypeDataOrNull_ModeratelyExpensive;
            if ( playerTypeData == null )
                return false;
            switch (playerTypeData.InternalName)
            {
                case "NecromancerSidekick":
                case "NecromancerEmpire":
                    return true; 
            }
            return false;
        }

        public static void DoForAllNecromancerFactions( Action<Faction> ToDo )
        {
            foreach ( PlayerTypeData playerType in PlayerTypeDataTable.Instance.Rows )
            {
                if ( playerType.CurrentFactionsInThisGame.Count <= 0 )
                    continue;
                switch ( playerType.InternalName )
                {
                    case "NecromancerSidekick":
                    case "NecromancerEmpire":
                        foreach ( Faction fac in playerType.CurrentFactionsInThisGame )
                        {
                            ToDo( fac );
                        }
                        break;
                }
            }
        }

        public static Faction GetFirstNecromancerFactionOrNull()
        {
            foreach ( PlayerTypeData playerType in PlayerTypeDataTable.Instance.Rows )
            {
                if ( playerType.CurrentFactionsInThisGame.Count <= 0 )
                    continue;
                switch ( playerType.InternalName )
                {
                    case "NecromancerSidekick":
                    case "NecromancerEmpire":
                        foreach ( Faction fac in playerType.CurrentFactionsInThisGame )
                        {
                            return fac;
                        }
                        break;
                }
            }
            return null;
        }
        
        public static Faction GetLastNecromancerFactionOrNull()
        {
            var list = World_AIW2.Instance?.AllPlayerFactions;
            if (list == null)
                return null;
            
            int itr = list.Count-1;
            while (itr >= 0)
            {
                var fac = list[itr];
                itr--;
                
                if (fac == null)
                    continue;
                
                var player = fac.PlayerTypeDataOrNull_ModeratelyExpensive;
                if (player == null)
                    continue;
                
                if (player.InternalName == "NecromancerSidekick" ||
                    player.InternalName == "NecromancerEmpire")
                {
                    return fac;
                }
            }
            
            return null;
        }
        
        public static Faction GetRandomNecromancerFaction( ArcenSimContextAnyStatus Context )
        {
            List<Faction> workingFactions = Faction.GetTemporaryFactionList( "NecroFac-GetRandomNecromancerFaction-workingFactions", 10f );
            foreach ( PlayerTypeData playerType in PlayerTypeDataTable.Instance.Rows )
            {
                if ( playerType.CurrentFactionsInThisGame.Count <= 0 )
                    continue;
                switch ( playerType.InternalName )
                {
                    case "NecromancerSidekick":
                    case "NecromancerEmpire":
                        foreach ( Faction faction in playerType.CurrentFactionsInThisGame )
                            workingFactions.Add( faction );
                        break;
                }
            }
            Faction fac = null;
            if ( workingFactions.Count == 1 )
                fac = workingFactions[0];
            else if ( workingFactions.Count > 1 )
                fac = workingFactions[Context.RandomToUse.Next( 0, workingFactions.Count )];
            Faction.ReleaseTemporaryFactionList( workingFactions );
            return fac;
        }

        public GameEntity_Squad GetRandomNecropolisForSwapOrNull( ArcenHostOnlySimContext Context )
        {
            WorkingList.Clear();
            this.Necropoleis.Display_DoFor( delegate ( GameEntity_Squad city ) {
                if ( city == null || city.FleetMembership == null )
                    return DelReturn.Continue;
                if ( city.TypeData.GetHasTag("Phylactery" ) )
                    return DelReturn.Continue;
                if ( city.GetIsCrippled() )
                    return DelReturn.Continue;
                WorkingList.Add( city );
                return DelReturn.Continue;
            });
            if ( WorkingList.Count == 0 )
                return null;
            WorkingList.Sort( delegate ( SafeSquadWrapper Left, SafeSquadWrapper Right )
            {
                PlanetFaction lFaction = Left.PlanetFaction;
                PlanetFaction rFaction = Right.PlanetFaction;
                int lHostileStrength = lFaction.DataByStance[FactionStance.Hostile].TotalStrength;
                int rHostileStrength = rFaction.DataByStance[FactionStance.Hostile].TotalStrength;
                if ( lHostileStrength == rHostileStrength )
                    return Left.Planet.Name.CompareTo( Right.Planet.Name );
                return lHostileStrength.CompareTo( rHostileStrength );
            } );
            return WorkingList[0].GetSquad();
        }

        public void SwapNecropoleis( GameEntity_Squad oldPhylactery, GameEntity_Squad newPhylactery, ArcenHostOnlySimContext Context )
        {
            ArcenPoint oldLocation = oldPhylactery.WorldLocation;
            Planet oldPlanet = oldPhylactery.Planet;
            Planet newPlanet = newPhylactery.Planet;
            Fleet oldFleet = oldPhylactery.FleetMembership.Fleet;
            Fleet newFleet = newPhylactery.FleetMembership.Fleet;
            GameCommand command = null;

            this.AttachedFaction.DoForEntities( "NecromancerAmplifier", delegate ( GameEntity_Squad entity )
            {
                //Swap fleets for any amplifiers on the planet
                ArcenPoint newPoint = ArcenPoint.ZeroZeroPoint;
                if ( entity.Planet != oldPlanet && entity.Planet != newPlanet )
                    return DelReturn.Continue;

                command = GameCommand.Create( GameCommandTypeTable.CoreFunctions[CoreFunction.EditFleetData], GameCommandSource.IsLiterallyFromDirectClickOfLocalPlayer );
                if ( entity.Planet == oldPlanet )
                {
                    command.RelatedIntegers.Add( oldFleet.FleetID );
                    command.RelatedIntegers2.Add( newFleet.FleetID );
                    command.RelatedIntegers3.Add( entity.FleetMembership.UniqueTypeDataDifferentiatorForDuplicates );
                    command.RelatedIntegers4.Add( oldFleet.GetNextUniqueIntToUseOfMatchingMembershipGroupsBasedOnSquadType( entity.TypeData ) );
                    command.RelatedString = "SwapFleetMemberWithEmpty";
                    command.RelatedString2 = entity.TypeData.InternalName;
                }
                if ( entity.Planet == newPlanet )
                {
                    command.RelatedIntegers.Add( newFleet.FleetID );
                    command.RelatedIntegers2.Add( oldFleet.FleetID );
                    command.RelatedIntegers3.Add( entity.FleetMembership.UniqueTypeDataDifferentiatorForDuplicates );
                    command.RelatedIntegers4.Add( newFleet.GetNextUniqueIntToUseOfMatchingMembershipGroupsBasedOnSquadType( entity.TypeData) );
                    command.RelatedString = "SwapFleetMemberWithEmpty";
                    command.RelatedString2 = entity.TypeData.InternalName;
                }
                World_AIW2.Instance.QueueGameCommand( this.AttachedFaction, command, true );
                return DelReturn.Continue;
            } );
            //Now move the necropolises
            oldPhylactery.WarpToPlanet( newPhylactery.Planet, newPhylactery.WorldLocation, "Swap Necropolis Location" );
            newPhylactery.WarpToPlanet( oldPlanet, oldLocation, "Swap Necropolis Location" );

            //And destroy the old structures
            command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.DestroyDistantFleetMembers], GameCommandSource.IsLiterallyFromDirectClickOfLocalPlayer );
            command.RelatedEntityIDs.Add( oldPhylactery.PrimaryKeyID );
            command.RelatedEntityIDs.Add( newPhylactery.PrimaryKeyID );
            World_AIW2.Instance.QueueGameCommand( this.AttachedFaction, command, true );
            if ( oldPhylactery.GetIsCrippled() )
            {
                newPhylactery.TakeDamageDirectly( newPhylactery.GetMaxHullPoints() * 2, null, null, DamageSource.SelfDamageFromMyOwnWeapons, Context );
                newPhylactery.CrippledUntilReachesFullHealth = true;
            }

            //World_AIW2.Instance.QueueGameCommand( this.AttachedFaction, command, false );
        }

        #region UpdatePowerLevel
        public override void UpdatePowerLevel()
        {
            bool debug = false;
            if ( debug )
                ArcenDebugging.LogSingleLine("harshness: " + World_AIW2.Instance.CampaignType.HarshnessRating, Verbosity.DoNotShow );
            FInt powerLevelSoFar = FInt.Zero;
            if ( World_AIW2.Instance.CampaignType.HarshnessRating >= 500 ) //Doesn't apply to HA
            {
                powerLevelSoFar = FInt.FromParts(0, 200);
                if ( World_AIW2.Instance.CampaignType.HarshnessRating >= 1000 )
                    powerLevelSoFar = FInt.FromParts(0, 100);
            }
            FInt totalStrength = FInt.Zero;
            FInt totalFlagshipMarkLevel = FInt.Zero;
            bool foundMarkVFlagship = false;
            bool foundMarkVNecropolis = false;

            World_AIW2.Instance.DoForFleets( this.AttachedFaction, FleetStatus.CenterpieceMustLiveOrLooseFleet, delegate ( Fleet fleet )
            {
                GameEntity_Squad centerpiece = fleet.Centerpiece.GetSquad();
                if ( centerpiece == null )
                    return DelReturn.Continue;
                if ( centerpiece.PlanetFaction.Faction != this.AttachedFaction )
                    return DelReturn.Continue;
                if ( centerpiece.GetMatches_SemiSlow( EntityRollupType.MobileFleetFlagships ) ||
                     centerpiece.GetMatches_SemiSlow( EntityRollupType.MobileCombatFlagships ) )
                {
                    totalStrength += fleet.GetMaxStrengthOfFleet_ForUIOnly( false );
                }
                if ( centerpiece.CurrentMarkLevel >= 5 )
                {
                    if ( centerpiece.TypeData.IsMobile )
                        foundMarkVFlagship = true;
                    else
                        foundMarkVNecropolis = true;
                }
                return DelReturn.Continue;
            } );
            if ( debug )
                ArcenDebugging.LogSingleLine("total strength: " + totalStrength + " total flagships: " + this.Flagships.Count + " blueprints " + this.AvailableBlueprints.Count + " bonus struct " + this.StructuresGrantingBonuses.Count + " upgrades " + this.NecromancerCompletedUpgrades.Count, Verbosity.DoNotShow );
            totalStrength /= 1000;
            FInt totalFleetStrengthComponent = FInt.Zero;
            if ( totalStrength > 400 )
            {
                FInt strengthUnits = (totalStrength - 400) / 100;
                if (debug )
                    ArcenDebugging.LogSingleLine("strengthUnits "+ strengthUnits, Verbosity.DoNotShow );
                totalFleetStrengthComponent = FInt.FromParts(0, 050) * strengthUnits;
            }
            if ( totalFleetStrengthComponent > FInt.FromParts( 0, 500 ) )
            {
                totalFleetStrengthComponent = FInt.FromParts( 0, 500 );
            }
            powerLevelSoFar += totalFleetStrengthComponent;

            FInt powerLevelPerFlagship = FInt.FromParts(0, 100 );
            FInt powerLevelFromFlagships = powerLevelPerFlagship * this.Flagships.Count;
            powerLevelSoFar += powerLevelFromFlagships;

            FInt powerLevelPerBlueprint = FInt.FromParts(0, 100 );
            FInt powerLevelFromBlueprints = powerLevelPerBlueprint * this.AvailableBlueprints.Count;
            powerLevelSoFar += powerLevelFromBlueprints;

            FInt powerLevelPerAmplifier = FInt.FromParts(0, 050 );
            FInt powerLevelFromAmplifiers = powerLevelPerAmplifier * this.StructuresGrantingBonuses.Count;
            powerLevelSoFar += powerLevelFromAmplifiers;

            FInt powerLevelPerUpgrade = FInt.FromParts(0, 020 );
            FInt powerLevelFromUpgrades=  powerLevelPerUpgrade * this.NecromancerCompletedUpgrades.Count;
            powerLevelSoFar += powerLevelFromUpgrades;

            if ( foundMarkVNecropolis )
            {
                if ( debug )
                    ArcenDebugging.LogSingleLine("Adding 0.1 for a mark 5 necropolis", Verbosity.DoNotShow );
                powerLevelSoFar += FInt.FromParts(0, 100 );
            }
            if ( foundMarkVFlagship )
            {
                if ( debug )
                    ArcenDebugging.LogSingleLine("Adding 0.1 for a mark 5 flagship", Verbosity.DoNotShow );
                powerLevelSoFar += FInt.FromParts(0, 100 );
            }
            this.AttachedFaction.OverallPowerLevel = powerLevelSoFar;
            if ( debug )
                ArcenDebugging.LogSingleLine("Got power level " + this.AttachedFaction.OverallPowerLevel + " fleet component " + totalFleetStrengthComponent + " flagship " + powerLevelFromFlagships + " blueprints " + powerLevelFromBlueprints + " amplifiers " + powerLevelFromAmplifiers + " upgrades "+ powerLevelFromUpgrades , Verbosity.DoNotShow );
        }
        #endregion

        public override int GetAddedToGalaxyCapForType( GameEntityTypeData type )
        {
            int numAdded = 0;
            foreach ( var u in NecromancerCompletedUpgrades )
            {
                switch ( u.Type )
                {
                    case NecromancerUpgradeType.UnlockSkeletonType:
                    case NecromancerUpgradeType.UnlockWightType:
                    case NecromancerUpgradeType.UnlockMummyType:
                    case NecromancerUpgradeType.UnlockNewShip:
                    {
                        if ( u.ShipForCapIncrease != null && u.ShipForCapIncrease == type )
                            numAdded += u.GalaxyCapIncrease;

                        break;
                    }
                }
            }

            return numAdded;
        }

        public override void WriteFactionIcon( ArcenCharacterBufferBase buffer )
        {
            var fac = AttachedFaction;
            
            buffer.Add( "<pos=3><size=8px><voffset=6px>" );
            buffer.AddSprite( "PlayerIcon_Necromancer", 
                              "PlayerIcon_Necromancer_Border", 
                              null,
                              fac.FactionCenterColor.ColorHex,
                              fac.FactionTrimColor.ColorHex,
                              null );
            buffer.Add( "</pos></size></voffset>" );
        }

        public override void WriteFactionSlotStatus( ArcenCharacterBufferBase buffer )
        {
            if ( World_AIW2.Instance == null )
                return;

            //in the lobby
            if ( World_AIW2.Instance.InSetupPhase ) 
            {
                buffer.Add( "Empire Is Ready" );
            }
           
            //in game
            WriteControllingAccount(buffer);
        }

        public override void WriteAddedHackingHeaderInfo( ArcenCharacterBufferBase buffer )
        {
            //Show Essence Resource
            buffer.Add( "    " );
            buffer.Add( this.PlayerType.Resource1TextColorAndIcon ).Add( AttachedFaction.StoredFactionResourceOne.ToString(), this.PlayerType.Resource1Color );
        }

        public override void WriteAddedScienceHeaderInfo( ArcenCharacterBufferBase buffer )
        {
            //Show Essence Resource
            buffer.Add( "    " );
            buffer.Add( this.PlayerType.Resource1TextColorAndIcon ).Add( AttachedFaction.StoredFactionResourceOne.ToString(), this.PlayerType.Resource1Color );
        }
    }
}
