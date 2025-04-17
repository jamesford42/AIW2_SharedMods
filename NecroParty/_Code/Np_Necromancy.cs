
using System;
using System.Text;
using Arcen.Universal;
using Arcen.AIW2.Core;
using Arcen.AIW2.External;

namespace NecroParty
{
    public struct NecroCandidate
    {
        public FleetMembership Member;
        public int Count;

        public override string ToString()
        {
            if (Member == null)
                return "[null]";
            
            return string.Format("{0} ({1} rem)", Member.ToDebugString(), Count);
        }
    }
    
    public class Np_DeathEffect_Necromancy : IDeathEffectImplementation
    {
        public DeathEffectType Type;
        public void SetType(DeathEffectType type)
        {
            Type = type;
        }

        public static readonly ReferenceTracker RefTracker = new ReferenceTracker( "Np_DeathEffect_Necromancy" );
        public Np_DeathEffect_Necromancy()
        {
            RefTracker.IncrementObjectCount();
        }
        public override string ToString()
        {
            return "Np_Necromancy"; //makes exports more brief and clear
        }
        
        private Log _log;
        
        public void HandleDeathWithEffectApplied_AfterFullDeathOrPartOfStackDeath( bool IsFromOnlyPartOfStackDying, GameEntity_Squad Entity, ref int ThisDeathEffectDamageSustained, 
            Faction FactionThatDidTheKilling, Faction FactionResponsibleForTheDeathEffect, int NumShipsDying, ArcenSimContextAnyStatus Context )
        {
            bool debug = true;
            
            Log log = null;
            if (debug)
            {
                if (_log == null)
                    _log = Log.Yes;
                log = _log;
            }
            
            StructList<NecroCandidate> candidates = null;
            int debugstage = 0;
            try
            {
                debugstage = 1;
                var hostCtx = Context?.GetHostOnlyContext();
                if ( hostCtx == null ) //client
                    return;
                debugstage = 2;
                if ( FactionResponsibleForTheDeathEffect == null )
                    return;
                if ( !Entity.TypeData.IsMobileCombatant )
                    return;
                if ( Entity.TypeData.IsKingUnit )
                    return;
                if ( Entity.TypeData.IsBattlestation )
                    return;
                if ( Entity.TypeData.IsMobileFleetFlagship )
                    return;

                debugstage = 3;
                var dlc3_data = Entity.TypeData.TryGetDataExtensionAs<DLC3GameEntityTypeDataExtension>( "DLC3" );
                if ( dlc3_data != null )
                {
                    if ( dlc3_data.ImmuneToNecromancy )
                        return;
                }

                debugstage = 4;
                
                if ( debug )ArcenDebugging.ArcenDebugLogSingleLine("Triggering necromancy on " + Entity.ToStringWithPlanet() + " caused by " + FactionResponsibleForTheDeathEffect.GetDisplayName(), Verbosity.DoNotShow );

                debugstage = 10;

                if ( FactionResponsibleForTheDeathEffect.Type != FactionType.Player )
                {
                    if (FactionResponsibleForTheDeathEffect.Type == FactionType.AI)
                    {
                        // The AI somehow got necromancer units. 
                        // todo: maybe handle this or let the ai type...
                        //var sentinalData = FactionResponsibleForTheDeathEffect.TryGetAISentinelsCoreData();
                        //sentinalData.SentinelInfo.AIType.Implementation.HandleNecromancy( Entity, Context );
                    }
                    else 
                    if (FactionResponsibleForTheDeathEffect.SpecialFactionData.InternalName == "Reapers")
                    {
                        if ( debug ) ArcenDebugging.LogSingleLine("reapers: " + FactionResponsibleForTheDeathEffect.GetDisplayName(), Verbosity.DoNotShow );
                        ReapersFactionDeepInfo.HandleReaperNecromancy( Entity, hostCtx );
                    }
                }
                
                Faction dst_faction = null;
                if ( NecromancerEmpireFactionBaseInfo.GetIsThisANecromancerFaction( FactionResponsibleForTheDeathEffect ) )
                    dst_faction = FactionResponsibleForTheDeathEffect;
                else
                    dst_faction = FactionUtilityMethods.Instance.GetStrongestNecromancerFactionOnPlanet( Entity.Planet );
                
                if ( dst_faction == null )
                    return;

                debugstage = 30;
                if ( debug ) ArcenDebugging.ArcenDebugLogSingleLine("donating ship to  " + dst_faction.GetDisplayName(), Verbosity.DoNotShow );

                Fleet fleet = NecromancerEmpireFactionDeepInfo.GetBestMobileFleetForShip( Entity, dst_faction, hostCtx );
                if ( fleet == null )
                {
                    if ( debug ) ArcenDebugging.ArcenDebugLogSingleLine("didnt find a suitable fleet", Verbosity.DoNotShow );
                    return;
                }

                var centerpiece = fleet.Centerpiece.GetSquad();
                var dst_pfaction= Entity.Planet.GetPlanetFactionForFaction( dst_faction );
                if (dst_pfaction == null)
                {
                    LOG.Msg("Warning: necromancy-handledeath for dying ship '{0}' but dstfaction {1} appears to have no PlanetFaction on {2}.", 
                        Entity.ToStringWithPlanetAndOwner(), dst_pfaction.ToString(), Entity.Planet?.Name.OrNull());
                    return;
                }
                
                debugstage = 40;
                var spawnCat = Entity.TypeData.GetNecromancyShipType();
                //if (spawnCat == NecromancyShipType.None)
                //{
                //    log?.Msg("{0} is not worth any necro summon.", Entity.TypeData.InternalName);
                //    return;
                //}
                
                var spawnCatName = Arcen.AIW2.External.Extensions.ToString(spawnCat);
                var weakTypeName = spawnCat.GetWeakType();
                if (string.IsNullOrEmpty(weakTypeName))
                {
                    LOG.Msg("Warning: spawnCat {0} expected to have a weakType but does not.", Arcen.AIW2.External.Extensions.ToString(spawnCat));
                    return;
                }
                
                var weakType = GameEntityTypeDataTable.Instance.GetRowByName(weakTypeName);
                
                #region Local Methods
                void Spawn(GameEntityTypeData type, int count)
                {
                    ArcenPoint spawnPoint = Entity.WorldLocation;
                    
                    byte markTospawn = 0;
                    var spawn = GameEntity_Squad.CreateNew_ReturnNullIfMPClient( 
                        dst_pfaction, type, markTospawn, fleet, 0, spawnPoint, hostCtx, "DeathEffect-Necromancy" );
                
                    if ( spawn != null )
                    {
                        //if ( Engine_AIW2.TraceAtAll && Engine_AIW2.TracingFlags.Has( ArcenTracingFlags.Necromancer ) )
                        //    ArcenDebugging.ArcenDebugLogSingleLine("Necromancy Practiced (necromancer path) Spawned a " + newEntity.TypeData.GetDisplayName() + ", unit " + i + " of " + shipsToCreate + " being created on " + newEntity.GetPlanetName_Safe() + " for fleet " + fleet.GetName() + " that has flagship " + fleet.Centerpiece.GetSquad().ToStringWithPlanet() , Verbosity.DoNotShow );

                        debugstage = 100;
                        spawn.SetShipCount(count);
                        //centerpiece?.Orders.CopyTo( centerpiece, spawn, spawn.Orders, true, true, true );
                        spawn.Orders.SetBehaviorDirectlyInSim( EntityBehaviorType.Attacker_Full );
                        
                        if ( type.GetHasTag("WightVariant") )
                        {
                            spawn.SecondsTillTransformation = (short)(60 * AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "WightDecayInterval" ) );
                            spawn.TransformsIntoAfterTime = "BaseWight";
                        }
                        if ( type.GetHasTag("NecromancerWight") )
                        {
                            spawn.SecondsTillTransformation = (short)(60 * (short)AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "WightDecayInterval" ) );
                            spawn.TransformsIntoAfterTime = "WightAttritioner";
                        }
                        if ( type.GetHasTag("SkeletonVariant") )
                        {
                            spawn.SecondsTillTransformation = (short)(60 * AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "SkeletonDecayInterval" ));
                            spawn.TransformsIntoAfterTime = "BaseSkeleton";
                        }
                        if ( type.GetHasTag("NecromancerBaseSkeleton") )
                        {
                            spawn.SecondsTillTransformation = (short)(60 * AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "SkeletonDecayInterval" ));
                            spawn.TransformsIntoAfterTime = "SkeletonAttritioner";
                        }
                        
                        log?.Msg("Spawned {0} x{1} for {2}", spawn.TypeData.InternalName, count, fleet.OrNull());
                    }
                }
                
                bool RateNecroCandidate(NecroCandidate cand, out RateInfo<NecroCandidate> r)
                {
                    r = new RateInfo<NecroCandidate>();
                    r.Tier = 0;
                    r.Rating = 0;

                    //if (cand.Member?.TypeData != weakType)
                    //    r.Tier = 1;
                    r.Rating = (uint)cand.Count;
                    
                    return true;
                }
                #endregion
                
                int countToSpawn = NumShipsDying;
                if (!string.IsNullOrEmpty(spawnCatName))
                {
                    candidates = StructList<NecroCandidate>.Get();
                    
                    fleet.DoForMemberGroupsUnsorted_Sim(
                        (mem)=>
                            {
                                string nectype = mem.TypeData.OriginalXmlData.GetString("custom_NecromancyType", null, false);
                                int num;
                                var res = mem.GetCanBuildAnother(out num);
                                    
                                //if (!string.IsNullOrEmpty(nectype))
                                log?.Msg("{0} custom_NecromancyType={1} canbuild={2} x{3}", mem.ToDebugString(), nectype.OrNull(), res.IsTrue(), num);
                                
                                if (nectype == spawnCatName)
                                {
                                    if (res.IsTrue())
                                    {
                                        NecroCandidate cand = new NecroCandidate()
                                        {
                                            Member = mem,
                                            Count = num,
                                        };
                                        candidates.Add(cand);
                                    }
                                }
                               return DelReturn.Continue; 
                            });
                    
                    while (candidates.Count > 0)
                    {
                        if (countToSpawn <= 0)
                            break;

                        var choice = candidates.GetRandomStruct<NecroCandidate>(Context.RandomToUse, RateNecroCandidate);
                        if (!choice.HasValue)
                            break;
                        
                        int amount = Math.Min(NumShipsDying, choice.Value.Count);
                        if (amount > 0)
                        {
                            Spawn(choice.Value.Member.TypeData, amount );
                            countToSpawn -= amount;
                        }

                        int idx = candidates.FindIndex((c)=>c.Member == choice.Value.Member);
                        if (idx > -1)
                            candidates.RemoveAt(idx);
                    }
                }
                
                if (countToSpawn > 0)
                {
                    Spawn(weakType, countToSpawn);
                }
            }
            catch (Exception e)
            {
                LOG.Err("exception at debugstage {0}:\n{1}", debugstage, e);
            }
        }
}
    
    public static class Np_Extensions
    {
        public static string GetBasicType(this NecromancyShipType type)
        {
            if (type == NecromancyShipType.Skeleton)
                return "SkeletonAttritioner";
            if (type == NecromancyShipType.Wight)
                return "WightAttritioner";
            if (type == NecromancyShipType.Mummy)
                return "MummyAttritioner";
            
            return null;
        }
        
        public static string GetWeakType(this NecromancyShipType type)
        {
            if (type == NecromancyShipType.None)
                return "SkeletonAttritioner";
            if (type == NecromancyShipType.Skeleton)
                return "SkeletonAttritioner";
            if (type == NecromancyShipType.Wight)
                return "WightAttritioner";
            if (type == NecromancyShipType.Mummy)
                return "MummyAttritioner";

            return null;
        }
    }
    
    public static class Log_Extensions
    {
        public static void List<T>(this Log log, System.Collections.Generic.IEnumerable<T> list)
        {
            if (log==null)
                return;

            int counter = 0;
            foreach (var item in list)
            {
                log.AppendLine(string.Format("[{0}] {1} ({2})", counter, item.OrNull()));
                counter++;
            }
            
            log.AppendLine();
            log.Flush();
        }
    }
}
