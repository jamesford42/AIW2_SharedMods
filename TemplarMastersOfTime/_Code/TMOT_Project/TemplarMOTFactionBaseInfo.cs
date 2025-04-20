using System;
using System.Text;
using Arcen.Universal;
using Arcen.AIW2.Core;
using Arcen.AIW2.External;
using Ext = Arcen.AIW2.External;

namespace TemplarMOT
{
    public class TemplarMOTFactionBaseInfo : ExternalFactionBaseInfoRoot, IExternalBaseInfo_Singleton
    {
        public enum ActivityStatus : int
        {
            Scrambling,
            Organising,
            Crusading
        }
        public ActivityStatus AStatus;
        //Serialized
        // Corpserule: Only used for this faction, this is the aip templar prefers when the player is refusing to advance the game.
        public int InternalTimeAip;
        public double CrusadeMult;
        public int TimeForCrusade;
        public bool HasGeneratedCastles; //note that "Castles" are the Templar defensive structures in general, as well as the name of the top tier unit
        public int TimeForNextAssaultWave;
        public int TimeForInternalAip;
        public int TimeForHackCD;
        public int WaveLeadersToSpawn;
        public int RiftsPopulated;
        public readonly Dictionary<Planet, int> LastTimePlanetWasOwned = Dictionary<Planet, int>.Create_WillNeverBeGCed( 500, "TemplarMOTFactionBaseInfo-LastTimePlanetWasOwned" );
        public bool SpawnTemplarWaveNextSecond = false; //This is used for other factions/cheats to request templar actions
        public int InitialEncampmentsSpawned;
        //Not Serialized
        int Intensity = 0;
        public bool ValidHack;
        public static TemplarMOTFactionBaseInfo Instance; //there can only ever be one of this faction at a time
        public TemplarDifficulty Difficulty = null;
        public readonly DoubleBufferedList<SafeSquadWrapper> Flags = DoubleBufferedList<SafeSquadWrapper>.Create_WillNeverBeGCed(300, "Templars-Flags");
        public readonly DoubleBufferedList<SafeSquadWrapper> Castles = DoubleBufferedList<SafeSquadWrapper>.Create_WillNeverBeGCed( 300, "Templars-Castles" );
        public readonly DoubleBufferedList<SafeSquadWrapper> CastlesTopTier = DoubleBufferedList<SafeSquadWrapper>.Create_WillNeverBeGCed( 300, "Templars-CastlesTopTier" );
        public readonly DoubleBufferedList<SafeSquadWrapper> Constructors = DoubleBufferedList<SafeSquadWrapper>.Create_WillNeverBeGCed( 300, "Templars-Constructors" );
        public readonly DoubleBufferedList<SafeSquadWrapper> CastleDefenders = DoubleBufferedList<SafeSquadWrapper>.Create_WillNeverBeGCed( 300, "Templars-Cast leDefenders" );
        public readonly DoubleBufferedList<SafeSquadWrapper> WaveLeaders = DoubleBufferedList<SafeSquadWrapper>.Create_WillNeverBeGCed( 300, "Templars-WaveLeaders" );
        public DoubleBufferedList<SafeSquadWrapper> TopTierCastlesForHacking = DoubleBufferedList<SafeSquadWrapper>.Create_WillNeverBeGCed( 300, "Templares-CastlesForHacking" );
        public readonly DoubleBufferedDictionary<Planet, int> StrengthPerPlanet_TracingOnly = DoubleBufferedDictionary<Planet, int>.Create_WillNeverBeGCed( 300, "Templars-StrengthPerPlanet_TracingOnly" );
        //we need to be able to add to this as we go
        public readonly DoubleBufferedConcurrentList<SafeSquadWrapper> Rifts = DoubleBufferedConcurrentList<SafeSquadWrapper>.Create_WillNeverBeGCed( 300, "Templars-Rifts" );

        public bool AnyCastlesActive;

        public TemplarMOTFactionBaseInfo()
        {
            Cleanup();
        }

        protected override void Cleanup()
        {
            CrusadeMult = 0;
            Intensity = 0;
            HasGeneratedCastles = false;
            TimeForNextAssaultWave = -1;
            TimeForInternalAip = -1;
            TimeForHackCD = 0;
            WaveLeadersToSpawn = 0;
            RiftsPopulated = 0;
            Difficulty = null;
            LastTimePlanetWasOwned.Clear();
            Flags.Clear();
            Castles.Clear();
            CastlesTopTier.Clear();
            Constructors.Clear();
            CastleDefenders.Clear();
            Rifts.Clear();
            WaveLeaders.Clear();
            AnyCastlesActive = false;
            InitialEncampmentsSpawned = 0;
            AStatus = ActivityStatus.Scrambling;
            Instance = null;
            ValidHack = false;
        }
        public override int GetDifficultyOrdinal_OrNegativeOneIfNotRelevant()
        {
            return Intensity;
        }

        public override float CalculateYourPortionOfPredictedGameLoad_Where100IsANormalAI( ArcenCharacterBufferBase OptionalExplainCalculation )
        {
            DoRefreshFromFactionSettings();

            int load = 40 + (Intensity * 5);

            if ( OptionalExplainCalculation != null )
                OptionalExplainCalculation.Add( load ).Add( " Load From Templars" );
            return load;
        }

        #region Ser / Deser
        public override void SerializeFactionTo( SerMetaData MetaData, ArcenSerializationBuffer Buffer, SerializationCommandType SerializationCmdType )
        {
            Buffer.WriteHeaderStringToLogIfLoggingActive( "TemplarMOTFactionBaseInfo" );
            Buffer.AddBool( MetaData, HasGeneratedCastles );
            // Saving a float - even using the correct term - causes deserialisation problems, so we save the crusade as an int by multing it by 100.
            int CrusadeMultAsInt = (int)(CrusadeMult * 100);
            if (!SerializationCmdType.GetIsNetworkType())
            {
                Buffer.AddInt32(MetaData, ReadStyle.PosExceptNeg1, InternalTimeAip, "InternalTimeAip");
                Buffer.AddInt32(MetaData, ReadStyle.PosExceptNeg1, TimeForInternalAip, "TimeForInternalAip");
                Buffer.AddInt32(MetaData, ReadStyle.PosExceptNeg1, TimeForCrusade, "TimeForCrusade");
                Buffer.AddInt32(MetaData, ReadStyle.PosExceptNeg1, CrusadeMultAsInt, "CrusadeMult");
                Buffer.AddInt32(MetaData, ReadStyle.PosExceptNeg1, TimeForNextAssaultWave, "TimeForNextAssaultWave");
                Buffer.AddInt32(MetaData, ReadStyle.PosExceptNeg1, TimeForHackCD, "TimeForHackCD");
                Buffer.AddInt32(MetaData, ReadStyle.PosExceptNeg1, WaveLeadersToSpawn, "WaveLeadersToSpawn" );
                Buffer.AddInt32(MetaData, ReadStyle.PosExceptNeg1, RiftsPopulated, "RiftsPopulated" );
                Buffer.AddInt16(MetaData, ReadStyle.NonNeg, (Int16)LastTimePlanetWasOwned.Count, "LastTimePlanetWasOwned_Count");
                foreach (KeyValuePair<Planet, int> pair in LastTimePlanetWasOwned)
                {
                    Buffer.AddPlanetIndex_Neg1ToPos(MetaData, pair.Key.Index, "planetIdx");
                    Buffer.AddInt32(MetaData, ReadStyle.PosExceptNeg1, (Int32)pair.Value, "planetLastTime");
                }
                Buffer.AddBool(MetaData, SpawnTemplarWaveNextSecond);
                Buffer.AddInt16(MetaData, ReadStyle.NonNeg, (Int16)InitialEncampmentsSpawned, "InitialEncampmentsSpawned");
            }
        }
        public override void DeserializeFactionIntoSelf( SerMetaData MetaData, ArcenDeserializationBuffer Buffer, SerializationCommandType SerializationCmdType )
        {
            
            ArcenDebugging.ArcenDebugLogSingleLine( "DESERIALISING... standby", Verbosity.DoNotShow );
            Buffer.WriteHeaderStringToLogIfLoggingActive( "TemplarMOTFactionBaseInfo" );
            Buffer.ActivateOrAddTrackerByNameIfTracking( "TemplarMOTFactionBaseInfo Ext", TrackerStyle.ByTypeOnly );
            HasGeneratedCastles = Buffer.ReadBool( MetaData );
            if (!SerializationCmdType.GetIsNetworkType())
            {
                InternalTimeAip = Buffer.ReadInt32(MetaData, ReadStyle.PosExceptNeg1, "InternalTimeAip");
                TimeForInternalAip = Buffer.ReadInt32(MetaData, ReadStyle.PosExceptNeg1, "TimeForInternalAip");
                TimeForCrusade = Buffer.ReadInt32(MetaData, ReadStyle.PosExceptNeg1, "TimeForCrusade");
                // CrusadeMult was serialised as an Intx100
                CrusadeMult = Buffer.ReadInt32(MetaData, ReadStyle.PosExceptNeg1, "CrusadeMult") / 100;
                TimeForNextAssaultWave = Buffer.ReadInt32(MetaData, ReadStyle.PosExceptNeg1, "TimeForNextAssaultWave");
                TimeForHackCD = Buffer.ReadInt32(MetaData, ReadStyle.PosExceptNeg1, "TimeForHackCD");
                WaveLeadersToSpawn = Buffer.ReadInt32(MetaData, ReadStyle.PosExceptNeg1, "WaveLeadersToSpawn");
                RiftsPopulated = Buffer.ReadInt32(MetaData, ReadStyle.PosExceptNeg1, "RiftsPopulated");
                LastTimePlanetWasOwned.Clear();
                int count = Buffer.ReadInt16(MetaData, ReadStyle.NonNeg, "LastTimePlanetWasOwned_Count");
                for (int i = 0; i < count; i++)
                {
                    Int16 planetIdx = Buffer.ReadPlanetIndex_Neg1ToPos(MetaData, "planetIdx");
                    Planet planet = World_AIW2.Instance.GetPlanetByIndex(planetIdx);
                    LastTimePlanetWasOwned[planet] = Buffer.ReadInt32(MetaData, ReadStyle.PosExceptNeg1, "planetLastTime");
                }
                if (Buffer.FromGameVersion.GetGreaterThanOrEqualTo(4, 004))
                    SpawnTemplarWaveNextSecond = Buffer.ReadBool(MetaData);
                if (Buffer.FromGameVersion.GetGreaterThanOrEqualTo(4, 017))
                    this.InitialEncampmentsSpawned = Buffer.ReadInt16(MetaData, ReadStyle.NonNeg, "InitialEncampmentsSpawned");
            }
        }
        #endregion

        #region DoFactionGeneralAggregationsPausedOrUnpaused
        protected override void DoFactionGeneralAggregationsPausedOrUnpaused()
        {
            Instance = this;
        }
        #endregion

        #region DoRefreshFromFactionSettings
        protected override void DoRefreshFromFactionSettings()
        {
            try{
            ConfigurationForFaction cfg = this.AttachedFaction.Config;
            Intensity = -1;
            if ( this.AttachedFaction.SpecialFactionData.TakesDifficultyFromNecromancer ) 
                Intensity = FactionUtilityMethods.Instance.GetDifficultyFromNecromancerSettings( this.AttachedFaction );
            if ( Intensity == -1 )
                Intensity = cfg.GetIntValueForCustomFieldOrDefaultValue( "Intensity", true );

            Difficulty = TemplarDifficultyTable.Instance.GetRowByIntensity( this.Intensity, this.AttachedFaction );
            if ( Difficulty == null )
                throw new Exception("Could not find difficulty for templar");
            } catch ( Exception e )
            {
                ArcenDebugging.ArcenDebugLogSingleLine("hit exception in templar DoRefreshFromFactionSettings, Exception: " + e, Verbosity.ShowAsError );
            }
        }
        #endregion

        #region GetTemplarStateForDisplay
        public void GetTemplarStateForDisplay( ArcenDoubleCharacterBuffer buffer )
        {
            //For debug, this goes in the Threat menu
            buffer.Add( "\n" );
            buffer.Add( "Time till next assault wave: " ).Add( (this.TimeForNextAssaultWave - World_AIW2.Instance.GameSecond).ToString(), "a1ffa1" ).Add( " with " ).Add( this.WaveLeadersToSpawn.ToString(), "ffa1a1" ).Add( " leaders.\n" );
        }
        #endregion

        #region DoPerSecondLogic_Stage2Aggregating_OnMainThreadAndPartOfSim_ClientAndHost
        public override void DoPerSecondLogic_Stage2Aggregating_OnMainThreadAndPartOfSim_ClientAndHost( ArcenClientOrHostSimContextCore Context )
        {
            int debugCode = 0;
            try
            {
                debugCode = 100;
                bool tracing = this.tracing_shortTerm = Engine_AIW2.TraceAtAll && Engine_AIW2.TracingFlags.Has( ArcenTracingFlags.Templar );
                ArcenCharacterBuffer tracingBuffer = tracing ? ArcenCharacterBuffer.GetFromPoolOrCreate( "Templar-Stage2A-trace", 10f ) : null;
                debugCode = 300;
                int totalUnits = 0;
                int totalStrength = 0;

                if ( tracing )
                    StrengthPerPlanet_TracingOnly.ClearConstructionDictForStartingConstruction();
                Flags.ClearConstructionListForStartingConstruction();
                CastleDefenders.ClearConstructionListForStartingConstruction();
                Castles.ClearConstructionListForStartingConstruction();
                Rifts.ClearConstructionListForStartingConstruction();
                WaveLeaders.ClearConstructionListForStartingConstruction();
                CastlesTopTier.ClearConstructionListForStartingConstruction();
                Constructors.ClearConstructionListForStartingConstruction();
                TopTierCastlesForHacking.ClearConstructionListForStartingConstruction();

                debugCode = 400;
                AttachedFaction.DoForEntities( delegate ( GameEntity_Squad entity )
                {
                    if ( entity.TypeData.GetHasTag( "TemplarPrimaryDefensiveStructure" ) )
                    {
                        //templar castles can be created by mapgen_seedspecialentities, so just make sure the external data exists
                        TemplarPerUnitBaseInfo data = entity.CreateExternalBaseInfo<TemplarPerUnitBaseInfo>( "TemplarPerUnitBaseInfo" );
                        if ( entity.TypeData.GetHasTag( "TemplarHighTierStructure" ) )
                        {
                            Castles.AddToConstructionList( entity );
                            CastlesTopTier.AddToConstructionList( entity );
                            TopTierCastlesForHacking.AddToConstructionList( entity );
                        }
                        else
                        {
                            if ( this.Difficulty.NoCastles && entity.TypeData.GetHasTag("TemplarMidTierStructure") )
                            {

                                CastlesTopTier.AddToConstructionList( entity );
                                TopTierCastlesForHacking.AddToConstructionList( entity );
                            }

                            Castles.AddToConstructionList( entity );
                        }
                    }
                    if (entity.TypeData.GetHasTag("TemplarConstructor"))
                        Constructors.AddToConstructionList(entity);
                    if (entity.TypeData.GetHasTag("TemplarFlag"))
                        Flags.AddToConstructionList(entity);
                    if ( entity.TypeData.GetHasTag( "TemplarRift" ) )
                        Rifts.AddToConstructionList( entity );
                    if ( entity.TypeData.GetHasTag( "TemplarWaveLeader" ) )
                        WaveLeaders.AddToConstructionList( entity );

                    if ( entity.TypeData.IsMobileCombatant )
                    {
                        //only creates it if it couldn't find it.
                        TemplarPerUnitBaseInfo data = entity.CreateExternalBaseInfo<TemplarPerUnitBaseInfo>( "TemplarPerUnitBaseInfo" );
                        if ( data.DefenseMode )
                        {
                            GameEntity_Squad HomeCastle = data.HomeCastle.GetSquad();
                            if ( entity.PlanetFaction.Faction != HomeCastle?.PlanetFaction.Faction ) {
                                HomeCastle = null;
                                data.HomeCastle.Clear();
                            }
                            if ( HomeCastle == null ) {
                                entity.Despawn( Context, true, InstancedRendererDeactivationReason.AFactionJustWarpedMeOut ); //if we've lost our castle, we die
                            }
                            CastleDefenders.AddToConstructionList( entity );
                        }
                    }

                    totalUnits += 1 + entity.ExtraStackedSquadsInThis;
                    totalStrength += entity.GetStrengthOfSelfAndContents();
                    if ( !tracing )
                        return DelReturn.Continue; //we don't need to count the total units except for tracing
                    StrengthPerPlanet_TracingOnly.Construction[entity.Planet] += entity.GetStrengthOfSelfAndContents();
                    return DelReturn.Continue;
                } );
                debugCode = 900;

                CastleDefenders.SwitchConstructionToDisplay();
                Castles.SwitchConstructionToDisplay();
                Flags.SwitchConstructionToDisplay();
                Rifts.SwitchConstructionToDisplay();
                WaveLeaders.SwitchConstructionToDisplay();
                CastlesTopTier.SwitchConstructionToDisplay();
                TopTierCastlesForHacking.SwitchConstructionToDisplay();
                Constructors.SwitchConstructionToDisplay();

                if ( tracing )
                {
                    StrengthPerPlanet_TracingOnly.SwitchConstructionToDisplay();

                    bool unitDebug = false;
                    if ( totalUnits > 0 && unitDebug && tracing )
                    {
                        tracingBuffer.Add( "Total strength of Templar: " + (totalStrength / 1000) + " totalUnits " + totalUnits + " \n" );
                        StrengthPerPlanet_TracingOnly.GetDisplayDict().DoFor( delegate ( KeyValuePair<Planet, int> pair )
                        {
                            tracingBuffer.Add( "\t" + pair.Key.Name + " -> " + (pair.Value / 1000) + " strength\n" );
                            return DelReturn.Continue;
                        } );
                    }
                }

                debugCode = 1200;
                if ( tracing )
                {
                    if ( !tracingBuffer.GetIsEmpty() ) ArcenDebugging.ArcenDebugLogSingleLine( "Templar " + AttachedFaction.FactionIndex + " Sim Code. " + tracingBuffer.ToString(), Verbosity.DoNotShow );
                    tracingBuffer.ReturnToPool();
                    tracingBuffer = null;
                }
            }
            catch ( Exception e )
            {
                ArcenDebugging.ArcenDebugLogSingleLine( "Hit exception in Templar DoPerSecondLogic_Stage2Aggregating_OnMainThreadAndPartOfSim debugCode " + debugCode + " " + e.ToString(), Verbosity.ShowAsError );
            }
        }
        #endregion
    }
}
