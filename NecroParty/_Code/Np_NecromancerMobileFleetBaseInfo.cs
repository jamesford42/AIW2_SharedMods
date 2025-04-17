using System;
using System.Text;
using System.Linq;
using Arcen.Universal;
using Arcen.AIW2.Core;
using Arcen.AIW2.External;

namespace NecroParty
{
    public class Np_NecromancerMobileFleetBaseInfo : NecromancerMobileFleetBaseInfo, IFleetTransforms
    {
        /// <summary>
        /// Do not directly access this.
        /// If you want to know the current cap for a line get it the normal way from the FleetMembership.
        /// </summary>
        private Dictionary<GameEntityTypeData,int> _lineCaps = Dictionary<GameEntityTypeData,int>.Create_WillNeverBeGCed(100,"Np_NecromancerMobileFleetBaseInfo._lineCaps");
        //private List<NecromancerUpgrade> _fleetUpgrades = List<NecromancerUpgrade>.Create_WillNeverBeGCed(10, "Np_NecromancerMobileFleetBaseInfo._fleetUpgrades");
        
        public Np_NecromancerMobileFleetBaseInfo()
        {
            Cleanup();
        }

        protected override void Cleanup()
        {
            _lineCaps.Clear();
        }

        //public void ResetAggregateUpgradeData()
        //{
        //    this.NumSkeletonsInFleet.ClearConstructionValueForStartingConstruction();
        //    this.NumWightsInFleet.ClearConstructionValueForStartingConstruction();
        //    this.NumMummiesInFleet.ClearConstructionValueForStartingConstruction();

        //    this.SkeletonSoftCap.ClearConstructionValueForStartingConstruction();
        //    this.WightSoftCap.ClearConstructionValueForStartingConstruction();

        //    this.BonusSkeletonPercent.ClearConstructionValueForStartingConstruction();
        //    this.BonusWightPercent.ClearConstructionValueForStartingConstruction();
        //    this.BonusMummyPercent.ClearConstructionValueForStartingConstruction();

        //    this.PercentSkeletonType.ClearConstructionDictForStartingConstruction();
        //    this.PercentWightType.ClearConstructionDictForStartingConstruction();
        //    this.PercentMummyType.ClearConstructionDictForStartingConstruction();
        //}

        //public void SwitchAggregateUpgradeDataToDisplay() {
        //    this.NumSkeletonsInFleet.SwitchConstructionToDisplay();
        //    this.NumWightsInFleet.SwitchConstructionToDisplay();
        //    this.NumMummiesInFleet.SwitchConstructionToDisplay();

        //    this.SkeletonSoftCap.SwitchConstructionToDisplay();
        //    this.WightSoftCap.SwitchConstructionToDisplay();

        //    this.BonusSkeletonPercent.SwitchConstructionToDisplay();
        //    this.BonusWightPercent.SwitchConstructionToDisplay();
        //    this.BonusMummyPercent.SwitchConstructionToDisplay();

        //    this.PercentSkeletonType.SwitchConstructionToDisplay();
        //    this.PercentWightType.SwitchConstructionToDisplay();
        //    this.PercentMummyType.SwitchConstructionToDisplay();
        //}

        public override void SerializeTo( SerMetaData MetaData, ArcenSerializationBuffer Buffer, SerializationCommandType SerializationCmdType )
        {
            Buffer.WriteHeaderStringToLogIfLoggingActive( "NecromancerMobileFleetBaseInfo" );

            //if ( this.NecromancerCompletedUpgrades == null )
            //{
            //    Buffer.AddInt16( MetaData, ReadStyle.NonNeg, 0, "NecromancerUpgradeIndices" );
            //}
            //else
            //{
            //    Buffer.AddInt16( MetaData, ReadStyle.NonNeg, (Int16)this.NecromancerCompletedUpgrades.Count, "NecromancerUpgradeIndices" );
            //    for ( int i = 0; i < this.NecromancerCompletedUpgrades.Count; i++ )
            //        Buffer.AddInt16( MetaData, ReadStyle.NonNeg, (Int16)this.NecromancerCompletedUpgrades[i].Index, "NecromancerUpgradeIndex" );
            //}
            //Buffer.AddInt32( MetaData, ReadStyle.NonNeg, this.BonusSkeletonsEarned, "BonusSkeletonsEarned" );
            //Buffer.AddInt32( MetaData, ReadStyle.NonNeg, this.BonusWightsEarned, "BonusWightsEarned" );
            //Buffer.AddInt32( MetaData, ReadStyle.NonNeg, this.BonusMummiesEarned, "BonusMummiesEarned" );
            
        }

        public override void DeserializeIntoSelf( SerMetaData MetaData, ArcenDeserializationBuffer Buffer, SerializationCommandType SerializationCmdType )
        {
            Buffer.WriteHeaderStringToLogIfLoggingActive( "NecromancerMobileFleetBaseInfo" );
            Buffer.ActivateOrAddTrackerByNameIfTracking( "NecromancerMobileFleetBaseInfo Ext", TrackerStyle.ByTypeOnly );

            //int count = Buffer.ReadInt16( MetaData, ReadStyle.NonNeg, "NecromancerUpgradeIndices" );
            //this.NecromancerCompletedUpgrades.Clear();
            //for ( int i = 0; i < count; i++ )
            //    this.NecromancerCompletedUpgrades.Add( NecromancerUpgradeTable.Instance.GetRowByIndex( (Int32)Buffer.ReadInt16( MetaData, ReadStyle.NonNeg, "NecromancerUpgradeIndex" ) ) );

            //if ( Buffer.FromGameVersion.GetGreaterThanOrEqualTo( 3, 764 ) ) //3763_GuardPostConstruction^M
            //{
            //    BonusSkeletonsEarned = Buffer.ReadInt32( MetaData, ReadStyle.NonNeg, "BonusSkeletonsEarned" );
            //    BonusWightsEarned = Buffer.ReadInt32( MetaData, ReadStyle.NonNeg, "BonusWightsEarned" );
            //    BonusMummiesEarned = Buffer.ReadInt32( MetaData, ReadStyle.NonNeg, "BonusMummiesEarned" );
            //}

            Buffer.StopTrackerByName( "NecromancerMobileFleetBaseInfo Ext" );

            //if ( Buffer.FromGameVersion.GetLessThan( 5, 010 ) ) {
            //    RebuildUpgrades();
            //}
        }

        /// <summary>Fix NecromancerCompletedUpgrades for save games before 5.009</summary>
        /// In older versions, that field was not cleared between games, so could contain
        /// garbage. However, we can recreate that information based on data on the faction.
        //private void RebuildUpgrades()
        //{
        //    NecromancerEmpireFactionBaseInfo necroFac = this.AttachedFleet?.Faction?.TryGetExternalBaseInfoAs<NecromancerEmpireFactionBaseInfo>();
        //    if (necroFac == null) {
        //        ArcenDebugging.ArcenDebugLog("Couldn't find Necromancer Faction info when trying to fix per-fleet upgrade information. ", Verbosity.ShowAsError);
        //    }
        //    this.NecromancerCompletedUpgrades.Clear();
        //    foreach (NecromancerUpgradeEvent upgradeEvent in necroFac.NecromancerHistory) {
        //        if (upgradeEvent.RelatedFleetId == this.AttachedFleet.FleetID && upgradeEvent.Upgrade != null) {
        //            this.NecromancerCompletedUpgrades.Add(upgradeEvent.Upgrade);
        //        }
        //    }
        //}
        #if false
        public void NormalizeNecromancyRatios()
        {
            //this.PercentSkeletonTypeExcess = NormalizeRatios(this.PercentSkeletonType.Construction);
            //this.PercentWightTypeExcess = NormalizeRatios(this.PercentWightType.Construction);
            //this.PercentMummyTypeExcess = NormalizeRatios(this.PercentMummyType.Construction);
        }
 
        private static int NormalizeRatios(DoubleBufferedDictionary<string, int>.ConstructionData PercentType)
        {
            int totalPercent = 0;
            //if the percent chance of getting any ship type is over 100, scale
            //everything appropriately
            foreach ( KeyValuePair<string, int> kv in PercentType )
            {
                totalPercent += kv.Value;
            }
            if ( totalPercent > 100 )
            {
                FInt factor = (FInt)totalPercent / 100;
                foreach ( KeyValuePair<string, int> kv in PercentType )
                {
                    PercentType[kv.Key] = (kv.Value / factor).IntValue;
                }
                return totalPercent - 100;
            } else {
                return 0;
            }
        }

        public override void AddToTooltipForFleet( ArcenCharacterBufferBase buffer, TooltipDetail detailLevel )
        {
            bool fullDetail = detailLevel >= TooltipDetail.Full;
            if (fullDetail) {
                buffer.NewLine();
            }
            float skeletonRatio = (float)this.NumSkeletonsInFleet.Display / this.SkeletonSoftCap.Display;
            UnityEngine.Color skeletonColor = EntityText.GetProportionalStrengthColor( skeletonRatio );
            buffer.Add( "This fleet has " ).Add( this.NumSkeletonsInFleet.Display.ToString(), skeletonColor ).Add( "/" ).Add( this.SkeletonSoftCap.Display.ToString(), skeletonColor ).Add( " skeletons");
            if ( fullDetail ) {
                buffer.Add( ".\n");
                Dictionary<string, int> PercentSkeletonType = this.PercentSkeletonType.GetDisplayDict();
                if ( PercentSkeletonType.Count != 0 ) {
                    buffer.Add("Skeleton Ratios:");
                    if (PercentSkeletonTypeExcess > 0) {
                        buffer.Add(" (excess ").Add(PercentSkeletonTypeExcess).Add("%)");
                    }
                    buffer.NewLine();
                    DisplayTypePercentages(buffer, PercentSkeletonType);
                }
                if ( this.BonusSkeletonPercent.Display > 0 ) {
                    buffer.Add( "You have a " ).Add( this.BonusSkeletonPercent.Display.ToString(), "ffa1a1" ).Add( "% chance of getting additional skeletons whenever you get a skeleton.\n" );
                }
                if ( this.BonusSkeletonsEarned > 0 )
                    buffer.Add( "You have earned " ).Add( this.BonusSkeletonsEarned.ToString(), "a1a1ff" ).Add( " bonus skeletons.\n" );
                buffer.Add( "This fleet has " );
            }

            float wightRatio = (float)this.NumWightsInFleet.Display / this.WightSoftCap.Display;
            UnityEngine.Color wightColor = EntityText.GetProportionalStrengthColor( wightRatio );
            if ( !fullDetail ) {
                buffer.Add(" and ");
            }
            buffer.Add( this.NumWightsInFleet.Display.ToString(), wightColor ).Add( "/" ).Add( this.WightSoftCap.Display.ToString(), wightColor ).Add( " wights. " );

            if ( fullDetail ) {
                Dictionary<string, int> PercentWightType = this.PercentWightType.GetDisplayDict();
                if ( PercentWightType.Count != 0 ) {
                    buffer.Add("\nWight Ratios:");
                    if (PercentWightTypeExcess > 0) {
                        buffer.Add(" (excess ").Add(PercentWightTypeExcess).Add("%)");
                    }
                    buffer.NewLine();
                    DisplayTypePercentages(buffer, PercentWightType);
                }
                if ( this.BonusWightPercent.Display > 0 )
                    buffer.Add( "You have a " ).Add( this.BonusWightPercent.Display.ToString(), "ffa1a1" ).Add( "% chance of getting additional wights whenever you get a wight.\n" );
                if ( this.BonusWightsEarned > 0 )
                    buffer.Add( "You have earned " ).Add( this.BonusWightsEarned.ToString(), "a1a1ff" ).Add( " bonus wights.\n" );
            }
            if ( fullDetail ) {
                Dictionary<string, int> PercentMummyType = this.PercentMummyType.GetDisplayDict();
                if ( PercentMummyType.Count != 0 ) {
                    buffer.Add("Mummy Ratios:");
                    if (PercentMummyTypeExcess > 0) {
                        buffer.Add(" (excess ").Add(PercentMummyTypeExcess).Add("%)");
                    }
                    buffer.NewLine();
                    DisplayTypePercentages(buffer, PercentMummyType);
                }
                if ( this.BonusMummyPercent.Display > 0 )
                    buffer.Add( "You have a " ).Add( this.BonusMummyPercent.Display.ToString(), "ffa1a1" ).Add( "% chance of getting additional mummies whenever you get a mummy.\n" );
                if ( this.BonusMummiesEarned > 0 )
                    buffer.Add( "You have earned " ).Add( this.BonusMummiesEarned.ToString(), "a1a1ff" ).Add( " bonus mummies.\n" );

            }

        }

        public static void DisplayTypePercentages(ArcenCharacterBufferBase buffer, Dictionary<string, int> percentageTypes)
        {
            percentageTypes.DoFor( delegate (KeyValuePair<string, int> pair ) {
                buffer.Add(" - ").Add(pair.Value).Add("% of ").Add(pair.Key);
                buffer.NewLine();
                return DelReturn.Continue;
            });
        }
        #endif
        public override int AddToGetBaseSquadCapWithAdditions( int CapSoFar, FleetMembership Mem )
        {
            return CapSoFar;
        }

        public override void PerFrame_UpdateFleetData( Faction LocalPlayerFactionForUICalculations )
        {
            if (ArcenNetworkAuthority.IsClient)
                return;

            var fleet = this.AttachedFleet;
            var faction = fleet.Faction;
            var factionBase = faction.BaseInfo as Np_NecromancerEmpireFactionBaseInfo;
            
            _lineCaps.Clear();
            
            foreach (var city in faction.PlanetFactionEntityLists
                                     .OfRollup(EntityRollupType.CityCenter))
            {
                if (city.ComputeDisabledReason() != ArcenRejectionReason.Unknown)
                    continue;
                
                var city_flt = city.FleetMembership.Fleet;
                if (city_flt.CityBolstersFleetID != fleet.FleetID)
                    continue;
                
                foreach (var city_mem in city_flt.MemberGroups)
                {
                    if (city_mem == null)
                        continue;
                    
                    var count = city_mem.GetCurrentTotalCount_ForUIOnly();
                    if (count < 1)
                        continue;
                    
                    foreach (var item in city_mem.TypeData.FleetDesignTemplatesIAlwaysGrant
                                                 .Where(i=>i.DesignLogic==FleetDesignLogic.AddedToBolsteredFleet)
                                                 .SelectMany((i)=>i.Items()))
                    {
                        int cap = item.Cap * count;
                        _lineCaps[item.TypeData] += cap;
                    }
                }
            }

            if ( factionBase?.NecromancerCompletedUpgrades?.Count > 0 )
            {
                for ( int i = 0; i < factionBase.NecromancerCompletedUpgrades.Count; i++ )
                {
                    var upgrade = factionBase.NecromancerCompletedUpgrades[i];
                    if ( upgrade.ShipForCapIncrease == null )
                        continue;
                    
                    _lineCaps[upgrade.ShipForCapIncrease] += upgrade.CapIncrease;
                    
                    int cap = 0;
                    if (_lineCaps.TryGetValue(upgrade.ShipForCapIncrease, out cap))
                    {
                        cap += upgrade.CapIncrease;
                        _lineCaps[upgrade.ShipForCapIncrease] = cap;
                    }
                }
            }
            
            if ( this.NecromancerCompletedUpgrades.Count > 0 )
            {
                for ( int i = 0; i < this.NecromancerCompletedUpgrades.Count; i++ )
                {
                    var upgrade = this.NecromancerCompletedUpgrades[i];
                    if ( upgrade.ShipForCapIncrease == null )
                        continue;
                    
                    int cap = 0;
                    if (_lineCaps.TryGetValue(upgrade.ShipForCapIncrease, out cap))
                    {
                        cap += upgrade.CapIncrease;
                        _lineCaps[upgrade.ShipForCapIncrease] = cap;
                    }
                }
            }
            
            foreach (var p in _lineCaps)
            {
                var mem = fleet.GetOrAddMembershipGroupBasedOnSquadType_AssumeNoDuplicates(p.Key);
                mem.ExplicitBaseSquadCap = p.Value;
            }

            //var context = Engine_AIW2.Instance.MainThreadContext_ClientOrHost;
            //var random = context.RandomToUse;
            //fleet.DoForMemberGroupsUnsorted_Sim(
            //    (m)=>
            //    {
            //        int numShips = m.GetCurrentTotalCount_ForUIOnly();        
            //        int cap = m.EffectiveSquadCap;
            //        if (numShips > cap)
            //        {
            //            while (numShips > cap)
            //            {
            //                var e = m.EntitiesOfFMem.GetRandom<GameEntity_Squad>(random, StandardRatings.RateSquadByShipCount) as GameEntity_Squad;
            //                if (e == null)
            //                    break;
                            
            //                if (e.ShipCount > 1)
            //                    e.SetShipCount(e.ShipCount-1);
            //                if (e.ShipCount == 0)
            //                    e.Despawn(context, true, InstancedRendererDeactivationReason.SelfDestructOnTooHighOfCap);
                            
            //                numShips--;
            //            }
            //        }
                    
            //        return DelReturn.Continue;  
            //    });
        }
        
        #region IFleetTransforms
        string IFleetTransforms.DisplayName { get { return "Flagship"; } }
        public string NoTransformsText { get { return "No flagship blueprints available."; } }

        bool IFleetTransforms.HasAnyTransforms {
            get {
                NecromancerEmpireFactionBaseInfo baseInfo = this.AttachedFleet.Faction.TryGetExternalBaseInfoAs<NecromancerEmpireFactionBaseInfo>();
                return baseInfo.AvailableBlueprints.GetDisplayList().Count > 0;
            }
        }
        System.Collections.Generic.IEnumerable<IFleetTransformTarget> IFleetTransforms.TypesCanSwitchTo {
            get {
                NecromancerEmpireFactionBaseInfo baseInfo = this.AttachedFleet.Faction.TryGetExternalBaseInfoAs<NecromancerEmpireFactionBaseInfo>();
                return baseInfo.AvailableBlueprints.GetDisplayList();
            }
        }

        GameCommand IFleetTransforms.CreateTransformCommand(GameCommandSource source, GameEntity_Squad centerpiece, string InternalName) {
            GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.TransformNecrofleet], GameCommandSource.IsLiterallyFromDirectClickOfLocalPlayer );
            command.RelatedEntityIDs.Add( centerpiece.PrimaryKeyID );
            command.RelatedString2 = InternalName;
            return command;
        }

        GameEntityTypeData IFleetTransforms.GetTypeDataForName(string InternalName) {
            NecromancerUpgrade upgrade = NecromancerUpgradeTable.Instance.GetRowByName(InternalName);
            return GameEntityTypeDataTable.Instance.GetRowByName(upgrade?.RelatedShip.InternalName);
        }

        void IFleetTransforms.GetTooltip(ArcenCharacterBufferBase buffer)
        {
            buffer.Add("You can spend essence points to change the form of this flagship.");
            NecromancerEmpireFactionBaseInfo baseInfo = this.AttachedFleet.Faction.TryGetExternalBaseInfoAs<NecromancerEmpireFactionBaseInfo>();
            if (baseInfo.AvailableBlueprints.GetDisplayList().Count == 0) {
                buffer.NewLine();
                buffer.Add("You can find blueprints from rifts or by using the transform elderling hack.");
            }
        }
        #endregion
    }
}
