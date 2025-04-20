using System;
using System.Text;
using Arcen.Universal;
using Arcen.AIW2.Core;
using Arcen.AIW2.External;
using Ext = Arcen.AIW2.External;

namespace TemplarMOT
{
    /// <summary>
    //Handles the logic for the Templar. Templar are the enemies of the Necromancer. They have defensive structures that act like Sapper Watchtowers,
    //and also just sometimes spawn units to go after the players.
    /// </summary>
    public sealed class TemplarMOTFactionDeepInfo : ExternalFactionDeepInfoRoot, IExternalDeepInfo_Singleton
    {
        public TemplarMOTFactionBaseInfo BaseInfo;
        public static TemplarMOTFactionDeepInfo Instance = null;
        // Corpserule: Only used for this faction, this is the aip templar prefers when the player is refusing to advance the game.

        public int TotalNecropolisMarkLevel;
        public int timeUntilNextAip;
        public bool timeAssigned = false;
        public int lastKnownIntensity = -1;

        public override void DoAnyInitializationImmediatelyAfterFactionAssigned()
        {
            this.BaseInfo = this.AttachedFaction.GetExternalBaseInfoAs<TemplarMOTFactionBaseInfo>();
            Instance = this;
        }

        // Corpserule: Templar is a master of time, in charge of punishing the player whilst they are farming resources, the current version of Templar is too restricted by
        // AIP to be an effective countermeasure to this, whilst they aren't meant to be a faction you're in a hurry to reach, it's important for them to start ignoring AIP,
        // when Necros choose to overfarm and ignore them, the time factor of this equation pushes Necro to expand when farming, and the aip portion causes templar to remain
        // a threat when the player is ignoring the AI.
        int GetAipTime()
        {
            bool tracing = this.tracing_shortTerm = Engine_AIW2.TraceAtAll && Engine_AIW2.TracingFlags.Has(ArcenTracingFlags.Templar);
            // Corpserule: This should be in initialisation... i'll work that out later
            // DEBUG: intensity starts at 5 and changes to 10?
            if (BaseInfo.Difficulty.Intensity != lastKnownIntensity)
            {
                lastKnownIntensity = BaseInfo.Difficulty.Intensity;
                    if (BaseInfo.Difficulty.Intensity > 0)
                    {
                        timeUntilNextAip = 3600 / (5 * BaseInfo.Difficulty.Intensity);
                        BaseInfo.TimeForInternalAip = World_AIW2.Instance.GameSecond + timeUntilNextAip;
                        timeAssigned = true;
                    }


            }
            // TODO: arrange this so that this combination of IFs are done once per second, and this aip comparison only determines which to return.
            FInt aip = FactionUtilityMethods.Instance.GetCurrentAIP();


            int flagCount = BaseInfo.Flags.Count;
            int adjustedAip;
            if (aip.IntValue >= this.BaseInfo.InternalTimeAip)
            {
                // Corpserule: Templar is subservient to the AI and will base their strength depending on Aip
                // Scrambling takes precedent over crusades, even if they had enough flags to enable this.
                adjustedAip = aip.IntValue;
                this.BaseInfo.AStatus = TemplarMOTFactionBaseInfo.ActivityStatus.Scrambling;
            }
            else if (aip.IntValue*2 <= this.BaseInfo.InternalTimeAip + flagCount - GlobalAIWorldBaseInfo.Instance.AIProgress_Reduction.IntValue)
            {
                // Corpserule: Templar have designated this system for necromancer removal and is rapidly increasing in strength
                // Crusade trigger is designed to be quite variable, a lot of flags could mean skipping the organising step!
                adjustedAip = this.BaseInfo.InternalTimeAip;
                this.BaseInfo.AStatus = TemplarMOTFactionBaseInfo.ActivityStatus.Crusading;
            }
            else
            {
                // Corpserule: Templar is considered master of time, proceeds to ignore AIP and scale with time, punishing necromancers for lack of expansion
                // Organising is somewhat the default behavior, between 1.0x (Scrambling) -> 2.0x - flags (Crusade)
                adjustedAip = this.BaseInfo.InternalTimeAip;
                this.BaseInfo.AStatus = TemplarMOTFactionBaseInfo.ActivityStatus.Organising;
            }
            return adjustedAip;
        }

        // Corpserule: Templar managing its internal Time Aip, which it will prefer to using Aip when necromancers are making the choice to hold off captures.
        void AdvanceTime()
        {
            bool tracing = this.tracing_shortTerm = Engine_AIW2.TraceAtAll && Engine_AIW2.TracingFlags.Has(ArcenTracingFlags.Templar);
            if (BaseInfo.TimeForInternalAip == -1)
            {
                // Default result, currently there is no way to declare the period between Internal Aip gain time in the XML (Hardcoded 120)
                // We also use this to declare the initial TimeAip to match a starting game.
                // TODO? AIPTime from xml, scaling with difficutly - don't go too nuts on this, this is the expected default for D8/D9, at 30ph
                this.BaseInfo.InternalTimeAip = 10;
                BaseInfo.TimeForCrusade = 60;
                BaseInfo.TimeForInternalAip = World_AIW2.Instance.GameSecond + timeUntilNextAip;
            }
            // Following format further down for Spawn next assault wave
            if (tracing) ArcenDebugging.ArcenDebugLogSingleLine(" LOGGIN TIME " + BaseInfo.TimeForInternalAip + " GAME SECOND " + World_AIW2.Instance.GameSecond + " TIME UNTIL NEXT AIP " + timeUntilNextAip + " ASSIGNED TIME " + timeAssigned, Verbosity.DoNotShow);

            if (BaseInfo.TimeForInternalAip <= World_AIW2.Instance.GameSecond && timeUntilNextAip > 0 && timeAssigned == true)
            {
                this.BaseInfo.InternalTimeAip += 1;
                BaseInfo.TimeForInternalAip = World_AIW2.Instance.GameSecond + timeUntilNextAip;
            }
            if (BaseInfo.TimeForCrusade <= World_AIW2.Instance.GameSecond)
            {
                // Corpserule: We want about an hour to resolve crusades "safely" - since players in the past could just barely defeat a 3-6x raid by Templar as a result
                // of hacking, this metric is used, as roughly this is +1.8x mult per hour - 2.8x after 1 hour, and lethal near after
                BaseInfo.TimeForCrusade = World_AIW2.Instance.GameSecond + 20;
                if (BaseInfo.CrusadeMult < 0)
                {
                    // Corpserule: Largely Debug, but also a just in case.
                    BaseInfo.CrusadeMult = 0;
                }
                else if (BaseInfo.AStatus == TemplarMOTFactionBaseInfo.ActivityStatus.Crusading)
                {
                    BaseInfo.CrusadeMult += 0.01;
                }
                else if (BaseInfo.CrusadeMult > 0)
                {
                    BaseInfo.CrusadeMult -= 0.01;
                }
            }
            if (tracing) ArcenDebugging.ArcenDebugLogSingleLine("My Internal Aip is " + BaseInfo.InternalTimeAip + " I gain aip every " + timeUntilNextAip + " Templar Intensity is " + BaseInfo.Difficulty.Intensity, Verbosity.DoNotShow);
            // Check for flags.
            List<SafeSquadWrapper> flags = this.BaseInfo.Flags.GetDisplayList();
        }

        protected override void Cleanup()
        {
            Instance = null;
            BaseInfo = null;

            //probably does not matter
            WorkingPlanetsList.Clear();
            UnassignedShipsLRP.Clear();
            UnassignedShipsByPlanetLRP.Clear();
            InCombatShipsNeedingOrdersLRP.Clear();
            WorkingMetalGeneratorList.Clear();
            ConstructorsLRP.Clear();
            CastleDefendersLRP.Clear();
            CastleDefendersRetreatingLRP.Clear();
            GoingHome.Clear();

            //probably matters a little
            playAudioEffectForCommand = false;
            TotalNecropolisMarkLevel = 1;
        }

        protected override int MinimumSecondsBetweenLongRangePlannings => 5;
        private bool playAudioEffectForCommand = false;

        public override void DoPerSecondLogic_Stage3Main_OnMainThreadAndPartOfSim_HostOnly( ArcenHostOnlySimContext Context )
        {
            int debugCode = 0;
            try
            {
                debugCode = 100;
                if ( ArcenNetworkAuthority.DesiredStatus == DesiredMultiplayerStatus.Client )
                    return; //only on host (don't bother doing anything as a client)
                bool tracing = this.tracing_shortTerm = Engine_AIW2.TraceAtAll && Engine_AIW2.TracingFlags.Has( ArcenTracingFlags.Templar );
                ArcenCharacterBuffer tracingBuffer = tracing ? ArcenCharacterBuffer.GetFromPoolOrCreate( "Templar-DoPerSecondLogic_Stage3Main_OnMainThreadAndPartOfSim_HostOnly-trace", 10f ) : null;

                World_AIW2.Instance.QueueLogJournalEntryToSidebar( "NA_Templar_Lore", string.Empty, this.AttachedFaction, null, null, OnClient.DoThisOnHostOnly_WillBeSentToClients );

                World_AIW2.Instance.QueueLogJournalEntryToSidebar("NA_TemplarMOT_Lore", string.Empty, this.AttachedFaction, null, null, OnClient.DoThisOnHostOnly_WillBeSentToClients);

                World_AIW2.Instance.QueueLogJournalEntryToSidebar("NA_MOT_Introduction", string.Empty, this.AttachedFaction, null, null, OnClient.DoThisOnHostOnly_WillBeSentToClients);

                // Corpserule: Context seems to be used to spawn ships (for both players) or sync randomness (for both players), since this is not spawning ships
                // or doing anything random, it has no use for it.
                AdvanceTime();
                MarkUpCastles( Context );
                GenerateNewCastlesIfNecessary( Context );
                HandleConstructorsSim( Context );
                HandleCastlesSim( Context );
                SpawnTemplarForHunter( Context );
                if (AIWar2GalaxySettingTable.GetFauxFloatValueFromSettingByName_DuringGame("TemplarRiftsToSeed", 0.0f) <= 0)
                {
                    SpawnRiftsSim(Context);
                    UpdateRiftContentsSim(Context);

                }
                debugCode = 300;
                SpawnWaveLeaders( Context );
                HandleWaveLeaders( Context );
                debugCode = 1000;
                #region Tracing
                if ( tracing && !tracingBuffer.GetIsEmpty() ) ArcenDebugging.ArcenDebugLogSingleLine( tracingBuffer.ToString(), Verbosity.DoNotShow );
                if ( tracing )
                {
                    tracingBuffer.ReturnToPool();
                    tracingBuffer = null;
                }
                #endregion
            }
            catch ( Exception e )
            {
                ArcenDebugging.ArcenDebugLogSingleLine( "Hit exception in Templar Stage 3 debugCode " + debugCode + " " + e.ToString(), Verbosity.DoNotShow );
            }
        }
        private int GetResourceOneStrengthByPlayerFaction()
        {
            int highestResourceOneEarned = 0;
            for (int i = 0; i < World_AIW2.Instance.Factions.Count; i++)
            {
                FInt resourceOneEarned = FInt.Zero;
                Faction otherFaction = World_AIW2.Instance.Factions[i];
                if (otherFaction == null)
                    continue;
                if (otherFaction.Type != FactionType.Player)
                    continue;
                resourceOneEarned += otherFaction.StoredFactionResourceOne;
                TechHistoryEvent tEvent;
                for (int j = otherFaction.TechHistory.Count - 1; j >= 0; j--)
                {
                    tEvent = otherFaction.TechHistory[j];
                    if (tEvent.GetUpgradeResourceStyle() != UpgradeResourceStyle.Resource1)
                        continue;

                    resourceOneEarned += tEvent.costInWhateverResource;
                }
                if (resourceOneEarned > highestResourceOneEarned)
                    highestResourceOneEarned = resourceOneEarned.IntValue;
            }
            highestResourceOneEarned = (int)Math.Sqrt(highestResourceOneEarned);

            return highestResourceOneEarned;
        }
        public void HandleWaveLeaders( ArcenHostOnlySimContext Context )
        {
            int debugCode = 0;
            try
            {
                bool tracing = this.tracing_shortTerm = Engine_AIW2.TraceAtAll && Engine_AIW2.TracingFlags.Has( ArcenTracingFlags.Templar );
                ArcenCharacterBuffer tracingBuffer = tracing ? ArcenCharacterBuffer.GetFromPoolOrCreate( "Templar-HandleWaveLeaders-trace", 10f ) : null;
                
                // REMOVING THIS
                int ResourceOneStrength = GetResourceOneStrengthByPlayerFaction();
                ArcenDebugging.ArcenDebugLogSingleLine(ResourceOneStrength.ToString(), Verbosity.DoNotShow);
                int aip = GetAipTime();

                // Corpserule: The divide by 60 is now resolving as a float, previous behavior resulted in Necros capping at 99 AIP, as 100 Aip
                // would cause Templar to double, this is a more exact calc, it was too chunky before as 0-99 AIP was so free, Necros would stay on 4 bases.
                int difficulty = FactionUtilityMethods.Instance.GetHighestAIDifficulty();
                int adjustedAip = GetAipTime();
                // This now uses a seperate spawn, check spawning mechanics further below
                double adjustedCrusadeMult = (BaseInfo.AStatus == TemplarMOTFactionBaseInfo.ActivityStatus.Crusading) ? BaseInfo.CrusadeMult : 0;
                if (tracing)
                    tracingBuffer.Add("\n").Add("Mark Level:").Add(ResourceOneStrength).Add("adjAip:").Add(adjustedAip).Add("difficulty:").Add(difficulty).Add("WBStremngth:").Add(this.BaseInfo.Difficulty.WaveBaseStrength / 60).Add("CrusadeStr:").Add(adjustedCrusadeMult).Add("\n");
                int strengthPerCastle = (ResourceOneStrength * adjustedAip * difficulty * this.BaseInfo.Difficulty.WaveBaseStrength)/50;
                if (tracing)
                    tracingBuffer.Add("\n").Add(" My Strength Per castle is ").Add(strengthPerCastle).Add("\n");
                List<SafeSquadWrapper> waveLeaders = this.BaseInfo.WaveLeaders.GetDisplayList();
                for ( int i = 0; i < waveLeaders.Count; i++ )
                {
                    if (tracing)
                        tracingBuffer.Add("Debug C100");
                    GameEntity_Squad entity = waveLeaders[i].GetSquad();
                    if ( entity == null )
                        continue;
                    TemplarPerUnitBaseInfo data = entity.TryGetExternalBaseInfoAs<TemplarPerUnitBaseInfo>();
                    if ( data.PlanetsVisited.Contains( entity.Planet ) )
                        continue;
                    data.PlanetsVisited.Add( entity.Planet );
                    GameEntity_Squad castleToUse = null;
                    List<SafeSquadWrapper> castles = this.BaseInfo.Castles.GetDisplayList();
                    if (tracing)
                        tracingBuffer.Add("Debug C200");
                    for ( int j = 0; j < castles.Count; j++ )
                    {
                        if ( castles[j].Planet == entity.Planet )
                        {
                            castleToUse = castles[j].GetSquad();
                            if ( castleToUse != null )
                                break;
                        }
                    }
                    if ( castleToUse == null )
                        continue;
                    int strikecraftStrengthToSpawn = strengthPerCastle;

                    int guardianStrengthToSpawn = 0;
                    int direStrengthToSpawn = 0;
                    if (tracing)
                        tracingBuffer.Add("Debug C300");
                    if ( ResourceOneStrength >= 10 )
                    {
                        guardianStrengthToSpawn = strikecraftStrengthToSpawn / 2;
                        strikecraftStrengthToSpawn /= 2;
                    }
                    else if ( ResourceOneStrength >= 30 )
                    {
                        guardianStrengthToSpawn = strikecraftStrengthToSpawn / 4;
                        direStrengthToSpawn = strikecraftStrengthToSpawn / 4;
                        strikecraftStrengthToSpawn /= 2;
                    }

                    if ( tracing )
                        tracingBuffer.Add( "spawning templar reinforcements for " ).Add( entity.ToStringWithPlanet() ).Add( "; total strength: " + strengthPerCastle ).Add( ". strikecraft strength " + strikecraftStrengthToSpawn + "  guardian strength " + guardianStrengthToSpawn + " dire strength " + direStrengthToSpawn + ". Necromancer total levels: " + ResourceOneStrength + "\n" );
                    data.StrengthRalliedToWave += direStrengthToSpawn + strikecraftStrengthToSpawn + guardianStrengthToSpawn;
                    int splits = Context.RandomToUse.Next( 1, 4 );
                    if ( strikecraftStrengthToSpawn > 0 )
                    {
                        // Corpserule: Adjusting modifier to castle - make fewer castles count for more.
                        // Planets Visited contains the planet we're currently at, so 1 planet visited is the first planet, but we'll check for zero as a failsafe
                        if (data.PlanetsVisited.Count <= 1)
                        {
                            strikecraftStrengthToSpawn *= 3;
                        }
                        else if (data.PlanetsVisited.Count <= 9)
                        {
                            // Slowly increment the wave, after 9 planets (at which point, the original 3x has been equalised), allow it to progress as normal to provide "crushing force" from too many planets
                            strikecraftStrengthToSpawn = (strikecraftStrengthToSpawn * 0.75f).RoundUp();
                        }
                        if (tracing)
                            tracingBuffer.Add("Debug C400");
                        int percentElite = aip - this.BaseInfo.Difficulty.AIPToStartUsingEliteStrikecraft;
                        if ( percentElite < 0 )
                            percentElite = 0;

                        string tag = "TemplarStrikecraft";
                        if ( Context.RandomToUse.Next(0, 100) < percentElite )
                            tag = "TemplarEliteStrikecraft";

                        if (tracing)
                            tracingBuffer.Add("Debug C500");
                        SpawnShips( tag, strikecraftStrengthToSpawn / splits, castleToUse, Context );
                        if (adjustedCrusadeMult > 0)
                        {
                            if (tag == "TemplarStrikecraft") tag = "CTemplarStrikecraft"; else tag = "CTemplarEliteStrikecraft";
                            SpawnShips(tag, (int)(strikecraftStrengthToSpawn * adjustedCrusadeMult / splits), castleToUse, Context);
                        }
                        if (tracing)
                            tracingBuffer.Add(strikecraftStrengthToSpawn / splits);

                    }
                    if ( guardianStrengthToSpawn > 0 )
                    {
                        int percentElite = aip - this.BaseInfo.Difficulty.AIPToStartUsingEliteGuardians;
                        if ( percentElite < 0 )
                            percentElite = 0;

                        string tag = "TemplarGuardian";
                        if ( Context.RandomToUse.Next(0, 100) < percentElite )
                            tag = "TemplarEliteGuardian";
                        SpawnShips( tag, guardianStrengthToSpawn / splits, castleToUse, Context );
                        if (adjustedCrusadeMult > 0)
                        {
                            if (tag == "TemplarGuardian") tag = "CTemplarGuardian"; else tag = "CTemplarEliteGuardian";
                            SpawnShips(tag, (int)(guardianStrengthToSpawn * adjustedCrusadeMult / splits), castleToUse, Context);
                        }
                    }
                    if ( direStrengthToSpawn > 0 )
                    {
                        SpawnShips("TemplarDire", direStrengthToSpawn / splits, castleToUse, Context);
                        if (adjustedCrusadeMult > 0)
                        {
                            SpawnShips("CTemplarDire", (int)(direStrengthToSpawn * adjustedCrusadeMult / splits), castleToUse, Context);
                        }

                    }
                    if (tracing)
                        tracingBuffer.Add("spawning templar reinforcements for ").Add(entity.ToStringWithPlanet()).Add("; total strength: " + strengthPerCastle).Add(". strikecraft strength " + strikecraftStrengthToSpawn + "  guardian strength " + guardianStrengthToSpawn + " dire strength " + direStrengthToSpawn + ". Necromancer total levels: " + ResourceOneStrength + "\n");

                }

                #region Tracing
                if ( tracing && !tracingBuffer.GetIsEmpty() ) ArcenDebugging.ArcenDebugLogSingleLine( tracingBuffer.ToString(), Verbosity.DoNotShow );
                if ( tracing )
                {
                    tracingBuffer.ReturnToPool();
                    tracingBuffer = null;
                }
                #endregion

            }
            catch ( Exception e )
            {
                ArcenDebugging.ArcenDebugLogSingleLine( "Exception in HandleWaveLeaders debugCode " + debugCode + " " + e.ToString(), Verbosity.DoNotShow );
            }
        }
        private string GetTagForShips( GameEntity_Squad entity, ArcenHostOnlySimContext Context )
          {
            int aip = GetAipTime();
              string tag = "";
              int percentEliteStrikecraft = aip - this.BaseInfo.Difficulty.AIPToStartUsingEliteStrikecraft;
              if ( percentEliteStrikecraft < 0 )
                  percentEliteStrikecraft = 0;
              int percentEliteGuardians = aip - this.BaseInfo.Difficulty.AIPToStartUsingEliteStrikecraft;
              if ( percentEliteGuardians < 0 )
                  percentEliteGuardians = 0;

              if ( entity.TypeData.GetHasTag( "TemplarEncampment" ) || entity.TypeData.GetHasTag( "TemplarInitialEncampment" ) )
              {
                  if ( Context.RandomToUse.Next(0, 100) < percentEliteStrikecraft )
                      tag = "TemplarEliteStrikecraft";
                  else
                      tag = "TemplarStrikecraft";
              }
              if ( entity.TypeData.GetHasTag( "TemplarFastness" ) )
              {
                  if ( Context.RandomToUse.Next( 0, 100 ) < 60 )
                  {
                      if ( Context.RandomToUse.Next(0, 100) < percentEliteStrikecraft )
                          tag = "TemplarEliteStrikecraft";
                      else
                          tag = "TemplarStrikecraft";
                  }
                  else
                  {
                      if ( Context.RandomToUse.Next(0, 100) < percentEliteGuardians )
                          tag = "TemplarEliteGuardian";
                      else
                          tag = "TemplarGuardian";
                  }
              }
              if ( entity.TypeData.GetHasTag( "TemplarCastle" ) )
              {
                  int random = Context.RandomToUse.Next( 0, 100 );
                  if ( random < 30 )
                  {
                      if ( Context.RandomToUse.Next(0, 100) < percentEliteStrikecraft )
                          tag = "TemplarEliteStrikecraft";
                      else
                          tag = "TemplarStrikecraft";
                  }
                  else if ( random < 80 )
                  {
                      if ( Context.RandomToUse.Next(0, 100) < percentEliteGuardians )
                          tag = "TemplarEliteGuardian";
                      else
                          tag = "TemplarGuardian";
                  }
                  else
                      tag = "TemplarDire";
              }
              return tag;
        }

        #region ReactToHacking
        public override void ReactToHacking_AsPartOfMainSim_HostOnly( GameEntity_Squad entityBeingHacked, FInt WaveMultiplier, ArcenHostOnlySimContext Context, HackingEvent Event, Faction overrideFactionOrNull = null )
        {
            ReactToHackingStep_AsPartOfMainSim( entityBeingHacked, Context, overrideFactionOrNull, Event );
        }

        public static void ReactToHackingStep_AsPartOfMainSim( GameEntity_Squad hackingTarget, ArcenHostOnlySimContext Context, Faction overrideFactionOrNull, HackingEvent Event )
        {
            ArcenDebugging.ArcenDebugLogSingleLine("HACK RESPONSE", Verbosity.DoNotShow);

            //I'll use the hackingEvent once i figure out how to tell if hostile or not, timeremaining maybe?
            ArcenDebugging.ArcenDebugLogSingleLine(Event.HackingPointsSpent.ToString(), Verbosity.DoNotShow);
            bool debug = GameSettings.Current.GetBoolBySetting( "HackingDebug" );
            int debugCode = 0;
            
            try{
                debugCode = 100;
                debugCode = 200;
                int aip;
                try
                {
                    // I'm pretty sure this is cheating the static function and it needs to be non-static in future, but we actually need some class values under the new method
                    aip = Instance.GetAipTime();

                }
                catch
                {
                    aip = FactionUtilityMethods.Instance.GetCurrentAIP().IntValue;

                }
                TemplarMOTFactionBaseInfo localBaseInfo = null;
                debugCode = 300;
                if ( overrideFactionOrNull != null )
                {
                    debugCode = 400;
                    localBaseInfo = overrideFactionOrNull.GetExternalBaseInfoAs<TemplarMOTFactionBaseInfo>();
                    if ( localBaseInfo == null )
                        throw new Exception("Could not find TemplarMOTFactionBaseInfo for override faction " + overrideFactionOrNull.GetDisplayName() );
                }
                else
                {
                    debugCode = 500;
                    localBaseInfo = hackingTarget.PlanetFaction.Faction.GetExternalBaseInfoAs<TemplarMOTFactionBaseInfo>();
                    if ( localBaseInfo == null )
                        throw new Exception("Could not find TemplarMOTFactionBaseInfo for hackingTarget faction " + hackingTarget.PlanetFaction.Faction.GetDisplayName() +". Target " + hackingTarget.ToStringWithPlanetAndOwner() );
                }
                debugCode = 600;
                if (localBaseInfo.ValidHack == true)
                {
                    localBaseInfo.ValidHack = false;
                    localBaseInfo.TimeForHackCD = World_AIW2.Instance.GameSecond + 10;
                    List<SafeSquadWrapper> TopTierCastlesForHacking = localBaseInfo.TopTierCastlesForHacking.GetDisplayList();
                    if (TopTierCastlesForHacking == null ||
                         TopTierCastlesForHacking.Count == 0)
                    {
                        debugCode = 700;
                        return;
                    }
                    debugCode = 800;
                    int hackingPointsAgainst = 0;
                    if (overrideFactionOrNull != null)
                    {
                        debugCode = 900;
                        hackingPointsAgainst = overrideFactionOrNull.HackingPointsUsedAgainstThisFaction.IntValue;
                    }
                    else
                    {
                        debugCode = 1000;
                        hackingPointsAgainst = hackingTarget.PlanetFaction.Faction.HackingPointsUsedAgainstThisFaction.IntValue;
                    }
                    debugCode = 1100;
                    //spawn my wave leaders
                    int waveLeaders = 3 + (aip / localBaseInfo.Difficulty.AIPPerBaseWaveLeader);
                    waveLeaders += hackingPointsAgainst / localBaseInfo.Difficulty.HackingPointsSpentPerBonusWaveLeaderFromHack; //for every X hacking points spent, add an extra wave leader response
                    debugCode = 1200;
                    if (debug)
                    {
                        ArcenDebugging.ArcenDebugLogSingleLine("we have " + waveLeaders + " waveLeaders and " + TopTierCastlesForHacking.Count + " possible castles to launch from:", Verbosity.DoNotShow);
                        // for ( int i = 0; i < TopTierCastlesForHacking.Count; i++ )
                        // {
                        //     ArcenDebugging.ArcenDebugLogSingleLine("\t" + TopTierCastlesForHacking[i].ToStringWithPlanet(), Verbosity.DoNotShow );
                        // }
                    }
                    string amount = "leaders";
                    if (waveLeaders == 1)
                        amount = "leader";
                    World_AIW2.Instance.QueueChatMessageOrCommand("The " + hackingTarget.PlanetFaction.Faction.StartFactionColourForLog() + "Templar</color> are launching a hacking response wave with <color=#ff0000>" + waveLeaders + "</color> wave " + amount + ".", ChatType.LogToCentralChat, null);
                    debugCode = 1300;
                    for (int i = 0; i < waveLeaders; i++)
                    {
                        debugCode = 1400;
                        GameEntity_Squad castle = null;
                        try
                        {
                            castle = TopTierCastlesForHacking[Context.RandomToUse.Next(0, localBaseInfo.TopTierCastlesForHacking.Count)].GetSquad();
                        }
                        catch { } //this can race with stage2; don't worry about it
                        if (castle == null)
                            continue;
                        debugCode = 1500;
                        byte markLevel = GetMarkLevelForWaveLeader(localBaseInfo.Difficulty, castle, Context);
                        GameEntityTypeData leaderTypeData = GetWaveLeaderType(localBaseInfo.Difficulty, castle, true, Context);
                        PlanetFaction pFaction = castle.PlanetFaction;
                        GameEntity_Squad leader = GameEntity_Squad.CreateNew_ReturnNullIfMPClient(pFaction, leaderTypeData, castle.CurrentMarkLevel,
                                                                                                   pFaction.Faction.LooseFleet, 0, castle.WorldLocation, Context, "Templar-ReactToHacking");
                        if (debug)
                            ArcenDebugging.ArcenDebugLogSingleLine("Spawning " + leader.ToStringWithPlanet(), Verbosity.DoNotShow);
                        debugCode = 1600;

                        if (leader != null)
                            leader.Orders.SetBehaviorDirectlyInSim(EntityBehaviorType.Attacker_Full); //is fine, main sim thread
                    }

                }
            } catch ( Exception e )
            {
                ArcenDebugging.ArcenDebugLogSingleLine("Hit exception in Templar ReactToHackingStep_AsPartOfMainSim debugCode " + debugCode + " " + e.ToString(), Verbosity.DoNotShow );
            }
        }
        #endregion
        #region SpawnRiftsSim
        //constants
        const int preferredRiftCountPerNecromancer = 6;
        const int baseRiftSpawnInterval = 300;
        public void SpawnRiftsSim( ArcenHostOnlySimContext Context )
        {
            int debugCode = 0;
            try
            {
                int effectiveRiftCount = BaseInfo.Rifts.Count;
                bool tracing = this.tracing_shortTerm = Engine_AIW2.TraceAtAll && Engine_AIW2.TracingFlags.Has( ArcenTracingFlags.Templar );
                ArcenCharacterBuffer tracingBuffer = tracing ? ArcenCharacterBuffer.GetFromPoolOrCreate( "Templar-SpawnRiftsSim-trace", 10f ) : null;
                debugCode = 100;
                int riftSpawnInterval = baseRiftSpawnInterval;
                int preferredRiftCount = preferredRiftCountPerNecromancer * NecromancerEmpireFactionBaseInfo.GetNecromancerFactionCount();
                if ( effectiveRiftCount < preferredRiftCount / 2 )
                    riftSpawnInterval /= 2; //spawn more quickly when there are fewer rifts

                if ( NecromancerEmpireFactionBaseInfo.GetNecromancerFactionCount() == 0 )
                {
                    if ( tracing && !tracingBuffer.GetIsEmpty() ) ArcenDebugging.ArcenDebugLogSingleLine( tracingBuffer.ToString(), Verbosity.DoNotShow );
                    if ( tracing )
                    {
                        tracingBuffer.ReturnToPool();
                        tracingBuffer = null;
                    }
                    return; //I want it to be possible for the templar to be played w/o a necromancer, but only use Rifts when there is a necromancer
                }
                debugCode = 200;
                //TODO: I originally wanted to delay the first relay spawn so as to not flood a novice necromancer with information immediately
                //However, if you are an expert playing as necromancer empire, the delay is just boring.
                //I think perhaps the necromancer sidekick should have a larger delay (say 30 seconds) but not the empire?
                //in the meantime, the delay is now very short
                int initialDelay = 5;
                if ( World_AIW2.Instance.GameSecond < initialDelay )
                {
                    if ( tracing && !tracingBuffer.GetIsEmpty() ) ArcenDebugging.ArcenDebugLogSingleLine( tracingBuffer.ToString(), Verbosity.DoNotShow );
                    if ( tracing )
                    {
                        tracingBuffer.ReturnToPool();
                        tracingBuffer = null;
                    }
                    return;
                }
                debugCode = 300;
                WorkingPlanetsList.Clear();
                if ( BaseInfo.RiftsPopulated == 0 )
                {
                    debugCode = 400;

                    GameEntityTypeData initialRiftTypeData = GameEntityTypeDataTable.Instance.GetRowByName( "TemplarInitialRift" );
                    if ( initialRiftTypeData == null )
                        throw new Exception( "Could not find initial rift XML" );
                    //TODO: potentially this needs to just seed near Necromancer Home Necropolises?
                    World_AIW2.Instance.DoForEntities( EntityRollupType.KingUnitsOnly, delegate ( GameEntity_Squad entity )
                    {
                        debugCode = 500;
                        if ( entity.PlanetFaction.Faction.Type == FactionType.Player )
                        {
                            //seed a rift one hop away
                            entity.Planet.DoForLinkedNeighbors( false, delegate (Planet neighbor )
                            {
                                WorkingPlanetsList.Add( neighbor );
                                return DelReturn.Continue;
                            } );
                        }
                        return DelReturn.Continue;
                    } );
                    if ( WorkingPlanetsList.Count == 0 )
                    {
                        throw new Exception("Unable to find a King unit");
                    }
                    debugCode = 600;
                    WorkingPlanetsList.Sort( delegate ( Planet L, Planet R)
                    {
                        debugCode = 700;
                        var lFactionData = L.GetStanceDataForFaction( AttachedFaction );
                        var rFactionData = R.GetStanceDataForFaction( AttachedFaction );

                        int lEnemyStrength = lFactionData[FactionStance.Hostile].TotalStrength;
                        int lFriendlyStrength = lFactionData[FactionStance.Friendly].TotalStrength +
                            lFactionData[FactionStance.Self].TotalStrength;
                        int rEnemyStrength = rFactionData[FactionStance.Hostile].TotalStrength;
                        int rFriendlyStrength = rFactionData[FactionStance.Friendly].TotalStrength +
                            rFactionData[FactionStance.Self].TotalStrength;
                        if ( lEnemyStrength == rEnemyStrength )
                            return lFriendlyStrength.CompareTo( rFriendlyStrength );
                        return lEnemyStrength.CompareTo( rEnemyStrength );

                    } );
                    debugCode = 800;
                    Planet riftPlanet = WorkingPlanetsList[0];
                    ArcenPoint spawnLocation = riftPlanet.GetSafePlacementPointAroundPlanetCenter( Context, initialRiftTypeData, FInt.FromParts( 0, 200 ), FInt.FromParts( 0, 500 ) );
                    SpawnRift( riftPlanet, initialRiftTypeData, spawnLocation, Context );
                    World_AIW2.Instance.QueueLogJournalEntryToSidebar( "NA_Necromancer_Rifts", string.Empty, NecromancerEmpireFactionBaseInfo.GetFirstNecromancerFactionOrNull(), null, riftPlanet, OnClient.DoThisOnHostOnly_WillBeSentToClients );
                    #region Tracing
                    if ( tracing && !tracingBuffer.GetIsEmpty() ) ArcenDebugging.ArcenDebugLogSingleLine( tracingBuffer.ToString(), Verbosity.DoNotShow );
                    if ( tracing )
                    {
                        tracingBuffer.ReturnToPool();
                        tracingBuffer = null;
                    }
                    #endregion

                    return;
                }
                debugCode = 900;
                if ( BaseInfo.RiftsPopulated == 1 )
                {
                    //wait until the first rift is hacked to seed more rifts
                    if ( GetTotalHackedRiftsForForAllNecromancers() == 1 )
                    {
                        debugCode = 1000;
                        //a necromancer has hacked the first rift. Now populate a bunch of additional rifts
                        Planet riftPlanet = GetPlanetForRift( Context, 1, 3, false );
                        SpawnRift( riftPlanet, Context );

                        riftPlanet = GetPlanetForRift( Context, 2, 5, false );
                        SpawnRift( riftPlanet, Context );
                    }
                    #region Tracing
                    if ( tracing && !tracingBuffer.GetIsEmpty() ) ArcenDebugging.ArcenDebugLogSingleLine( tracingBuffer.ToString(), Verbosity.DoNotShow );
                    if ( tracing )
                    {
                        tracingBuffer.ReturnToPool();
                        tracingBuffer = null;
                    }
                    #endregion

                    return;
                }
                //this is the "main" code path. We just say "If we should seed more rifts, go ahead and so so every so often"
                //
                debugCode = 1100;
                if ( tracing )
                    tracingBuffer.Add("There are " + effectiveRiftCount + " rifts and we want to have at least " + preferredRiftCount + ". our spawn interval is " + riftSpawnInterval ).Add("\n"); 
                if ( effectiveRiftCount < preferredRiftCount && World_AIW2.Instance.GameSecond % riftSpawnInterval == 0 )
                {
                    debugCode = 1200;
                    //if we don't have enough rifts, spawn one new rift then wait till the next interval.
                    Planet riftPlanet = GetPlanetForRift( Context, -1, -1, true );
                    if ( riftPlanet != null )
                    {
                        if ( tracing )
                            tracingBuffer.Add("spawning a rift on " + riftPlanet.Name ).Add("\n");
                        SpawnRift( riftPlanet, Context );
                    }
                    else{
                        if ( tracing )
                            tracingBuffer.Add("could not find a planet to put a rift on" ).Add("\n");
                    }
                        
                }
                debugCode = 1300;

                #region Tracing
                if ( tracing && !tracingBuffer.GetIsEmpty() ) ArcenDebugging.ArcenDebugLogSingleLine( tracingBuffer.ToString(), Verbosity.DoNotShow );
                if ( tracing )
                {
                    tracingBuffer.ReturnToPool();
                    tracingBuffer = null;
                }
                #endregion
            }
            catch ( Exception e )
            {
                ArcenDebugging.ArcenDebugLogSingleLine( "Hit an exception in HandleRiftsSim debugCode " + debugCode + " " + e.ToString(), Verbosity.DoNotShow );
            }
            return;
        }
        #endregion

        private readonly List<NecromancerUpgrade> tempUpgradesList = List<NecromancerUpgrade>.Create_WillNeverBeGCed( 10, "TemplarMOTFactionDeepInfo-tempUpgradesList" );

        private void UpdateRiftContentsSim( ArcenHostOnlySimContext Context )
        {
            if ( BaseInfo.Rifts.Count == 0 )
                return; //if there are no rifts, nothing to do
            int debugCode = 0;
            try{
                //First, iterate over all the available upgrade indices and make sure we have the right upgrades (for the case that we just loaded the game)
                debugCode = 100;
                if ( NecromancerEmpireFactionBaseInfo.GetNecromancerFactionCount() <= 0 )
                    return;
                BaseInfo.Rifts.Display_DoFor( delegate ( GameEntity_Squad rift )
                {
                    //if it already has that on it, it will just get the existing one
                    TemplarPerUnitBaseInfo data = rift.CreateExternalBaseInfo<TemplarPerUnitBaseInfo>( "TemplarPerUnitBaseInfo" );
                    debugCode = 410;
                    Faction necromancerFaction = FactionUtilityMethods.Instance.GetNearestNecromancerFactionToThisPlanet( rift.Planet, Context ); //when checking out a rift, always use the information for the nearest necromancer
                    if ( necromancerFaction == null )
                        throw new Exception( "Could not find the nearest necromancer  faction to " + rift.Planet.Name );
                    NecromancerEmpireFactionBaseInfo necroFactionBaseInfo = necromancerFaction.GetExternalBaseInfoAs<NecromancerEmpireFactionBaseInfo>();
                    if ( data.AvailableUpgrades.Count == 0 )
                    {
                        //fill in the upgrades
                        debugCode = 420;
                        int preferredRiftUpgradeCount = 5;//Context.RandomToUse.Next( 2, 4 ) + 2;
                        //TODO: also pass in the highest necropolis/flagship mark levels
                        preferredRiftUpgradeCount += AIWar2GalaxySettingTable.GetIsIntValueFromSettingByName_DuringGame( "BonusNecromancerRiftOptions" );
                        NecromancerUpgradeTable.Instance.GetUpgradesForRift_HostOnly( data.AvailableUpgrades, Context, necroFactionBaseInfo.NecromancerCompletedUpgrades, null, preferredRiftUpgradeCount, BaseInfo.RiftsPopulated, necroFactionBaseInfo.GetHighestNecropolisMarkLevel(), necroFactionBaseInfo.GetHighestFlagshipMarkLevel() );
                        BaseInfo.RiftsPopulated++;
                        debugCode = 425;
                        //ArcenDebugging.ArcenDebugLogSingleLine("We got " + data.AvailableUpgrades.Count + " upgrades to work with", Verbosity.DoNotShow );
                    }
                    debugCode = 500;
                    //Every so often we go through all the available upgrades and see if any of them need to be replaced with new options
                    if ( World_AIW2.Instance.GameSecond % 11 == 0 )
                    {
                        debugCode = 600;
                        for ( int j = 0; j < data.AvailableUpgrades.Count; j++ )
                        {
                            debugCode = 610;
                            bool replaceMe = false;
                            if ( data.AvailableUpgrades[j].ReplaceIfAlreadyUpgraded )
                            {
                                debugCode = 620;
                                for ( int k = 0; k < necroFactionBaseInfo.NecromancerCompletedUpgrades.Count; k++ )
                                {
                                    debugCode = 630;
                                    if ( necroFactionBaseInfo.NecromancerCompletedUpgrades[k].Index == data.AvailableUpgrades[j].Index )
                                    {
                                        debugCode = 640;
                                        replaceMe = true;
                                        break;
                                    }
                                }
                            }
                            debugCode = 650;
                            if ( replaceMe )
                            {
                                debugCode = 660;
                                tempUpgradesList.Clear();
                                NecromancerUpgradeTable.Instance.GetUpgradesForRift_HostOnly( tempUpgradesList, Context, necroFactionBaseInfo.NecromancerCompletedUpgrades, data.AvailableUpgrades, 1, BaseInfo.RiftsPopulated, necroFactionBaseInfo.GetHighestNecropolisMarkLevel(), necroFactionBaseInfo.GetHighestFlagshipMarkLevel() );
                                data.AvailableUpgrades[j] = tempUpgradesList[0];
                            }
                        }
                    }
                    return DelReturn.Continue;
                } );
            }catch ( Exception e )
            {
                ArcenDebugging.ArcenDebugLogSingleLine("Hit exception in  UpdateRiftContentsSim debugCode " + debugCode + " " + e.ToString(), Verbosity.DoNotShow );
            }
        }

        #region GetPlanetForRift
        private readonly DrawBag<Planet> WorkingPlanetBag = DrawBag<Planet>.Create_WillNeverBeGCed( 30, "TemplarMOTFactionDeepInfo-WorkingPlanetBag" );
        public Planet GetPlanetForRift(  ArcenHostOnlySimContext Context, Int16 minHopsFromHumanPlanet, Int16 maxHopsFromHumanPlanet, bool allowUnexplored )
        {
            WorkingPlanetBag.Clear();
            int AIP = GetAipTime();
            if ( minHopsFromHumanPlanet == -1 )
                minHopsFromHumanPlanet = (Int16)2;
            if ( maxHopsFromHumanPlanet == -1 )
                maxHopsFromHumanPlanet = (Int16)(5 + (int)(AIP/150));
            byte maxMarkLevel = (byte)(3 + (byte)(AIP/150));
            int allowedRetries = 100;
            int retries = 0;
            do
            {
                if ( retries > 0 )
                {
                    //the previous attempt was too restrictive
                    minHopsFromHumanPlanet--;
                    maxHopsFromHumanPlanet++;
                    maxMarkLevel++;
                }
                retries++;
                World_AIW2.Instance.DoForPlanetsSingleThread( false, delegate ( Planet planet )
                {
                    if ( !allowUnexplored &&
                         planet.IntelLevel == PlanetIntelLevel.Unexplored )
                        return DelReturn.Continue;
                    if ( planet.MarkLevelForAIOnly.Ordinal > maxMarkLevel )
                        return DelReturn.Continue; //honour the maxMarkLevel
                    if ( maxMarkLevel < 7 &&
                         planet.PopulationType == PlanetPopulationType.AIBastionWorld )
                        return DelReturn.Continue; //try not to seed on bastion worlds
                    var pFaction = planet.GetStanceDataForFaction( this.AttachedFaction );
                    if ( pFaction[FactionStance.Hostile].TotalStrength >
                         pFaction[FactionStance.Friendly].TotalStrength + pFaction[FactionStance.Self].TotalStrength )
                        return DelReturn.Continue; //not on planets with more enemies than our strength
                    int hopsToPlayerPlanet = 999;
                    if ( minHopsFromHumanPlanet > 1 )
                    {
                        //this planet must not be too close to a player planet
                        //to make the bounds listed above inclusive minHops - 1 must be used
                        bool foundPlayerPlanetWithinMinHops = false;
                        planet.DoForPlanetsWithinXHops( (Int16)(minHopsFromHumanPlanet - 1),
                                                        delegate ( Planet otherPlanet, Int16 distance )
                        {
                            if ( distance < hopsToPlayerPlanet )
                                hopsToPlayerPlanet = distance;
                            if ( otherPlanet.GetControllingFactionType() == FactionType.Player )
                                foundPlayerPlanetWithinMinHops = true;
                            return DelReturn.Continue;
                        } );
                        if ( foundPlayerPlanetWithinMinHops )
                            return DelReturn.Continue;
                    }
                    if ( maxHopsFromHumanPlanet > 0 )
                    {
                        //this planet can't be too far from a player planet
                        bool foundPlayerPlanetWithinMaxHops = false;
                        planet.DoForPlanetsWithinXHops( maxHopsFromHumanPlanet,
                                                        delegate ( Planet otherPlanet, Int16 distance )
                                                        {
                                                            if ( otherPlanet.GetControllingFactionType() == FactionType.Player )
                                                                foundPlayerPlanetWithinMaxHops = true;
                                                            return DelReturn.Continue;
                                                        } );
                        if ( !foundPlayerPlanetWithinMaxHops )
                            return DelReturn.Continue;
                    }
                    bool foundRiftAlready = false;

                    BaseInfo.Rifts.Display_DoFor( delegate ( GameEntity_Squad rift )
                    {
                        if ( rift.Planet == planet )
                        {
                            foundRiftAlready = true;
                            return DelReturn.Break;
                        }
                        return DelReturn.Continue;
                    } );
                    if ( foundRiftAlready )
                        return DelReturn.Continue;
                    //Prefer closer planets
                    if ( hopsToPlayerPlanet <= 2 )
                        WorkingPlanetBag.AddItem( planet, 2);
                    else if ( hopsToPlayerPlanet <= 5 )
                        WorkingPlanetBag.AddItem( planet, 4);
                    else
                        WorkingPlanetBag.AddItem( planet, 1);
                    return DelReturn.Continue;
                } );
            }  while ( WorkingPlanetBag.InternalListSize == 0 && retries < allowedRetries );

            return WorkingPlanetBag.PickRandomItemAndDoNotReplace( Context.RandomToUse );
        }
        #endregion

        #region SpawnRift
        public void SpawnRift( Planet planet, ArcenHostOnlySimContext Context )
        {
            GameEntityTypeData riftTypeData = GameEntityTypeDataTable.Instance.GetRowByName( "TemplarRift" );
            ArcenPoint spawnLocation = planet.GetSafePlacementPointAroundPlanetCenter( Context, riftTypeData, FInt.FromParts( 0, 200 ), FInt.FromParts( 0, 500 ) );
            this.SpawnRift( planet, riftTypeData, spawnLocation, Context );
        }
            
        public void SpawnRift( Planet planet, GameEntityTypeData riftTypeData, ArcenPoint spawnLocation, ArcenHostOnlySimContext Context )
        {
            if ( riftTypeData == null )
                throw new Exception("Could not find XML for TemplarRift");
            //we also spawn a templar defensive structure near the rift. In the beginning we spawn encampments,
            //but we will start spawning fastnesses once the necromancer has gotten more powerful
            GameEntityTypeData fastnessTypeData = GameEntityTypeDataTable.Instance.GetRowByName( "TemplarEncampment" );
            if ( this.BaseInfo.InitialEncampmentsSpawned < this.BaseInfo.Difficulty.InitialEncampments )
            {
                fastnessTypeData = GameEntityTypeDataTable.Instance.GetRowByName( "TemplarInitialEncampment" );
                this.BaseInfo.InitialEncampmentsSpawned++;
            }

            int minNecromancerMarklevelForHigherTier = 5;
            if ( FactionUtilityMethods.Instance.AnyNecromancerFactions() && TotalNecropolisMarkLevel > minNecromancerMarklevelForHigherTier )
                fastnessTypeData = GameEntityTypeDataTable.Instance.GetRowByName( "TemplarFastness" );
            if ( fastnessTypeData == null )
                throw new Exception("Could not find XML for spawning the rift defenses");
            
            bool tracing = this.tracing_shortTerm = Engine_AIW2.TraceAtAll && Engine_AIW2.TracingFlags.Has( ArcenTracingFlags.Templar );
            ArcenCharacterBuffer tracingBuffer = tracing ? ArcenCharacterBuffer.GetFromPoolOrCreate( "Templar-SpawnRift-trace", 10f ) : null;
            
            if ( tracing )
                tracingBuffer.Add("Seeding a rift on " + planet.Name +" with a " + fastnessTypeData +". Initial: " + this.BaseInfo.InitialEncampmentsSpawned + " of " + this.BaseInfo.Difficulty.InitialEncampments + "\n");
            PlanetFaction pFaction = planet.GetPlanetFactionForFaction( AttachedFaction );
            GameEntity_Squad rift = GameEntity_Squad.CreateNew_ReturnNullIfMPClient( pFaction, riftTypeData, 1,
                                                                                     pFaction.Faction.LooseFleet, 0, spawnLocation, Context, "Templar-SpawnRift" );
            spawnLocation = planet.GetSafePlacementPoint_AroundEntity( Context, fastnessTypeData, rift, FInt.FromParts( 0, 100 ), FInt.FromParts( 0, 150 ) );
            
            GameEntity_Squad fastness = GameEntity_Squad.CreateNew_ReturnNullIfMPClient( pFaction, fastnessTypeData, 1,
                                                                                         pFaction.Faction.LooseFleet, 0, spawnLocation, Context, "Templar-SpawnRift" );

            BaseInfo.Rifts.AddToDisplayList( rift ); //this is threadsafe because of the type of list it is

            if ( planet.IntelLevel > PlanetIntelLevel.Unexplored )
            {
                PlanetViewChatHandlerBase chatHandlerOrNull = ChatClickHandler.CreateNewAs<PlanetViewChatHandlerBase>( "PlanetGeneralFocus" );
                if ( chatHandlerOrNull != null )
                    chatHandlerOrNull.PlanetToView = planet;

                World_AIW2.Instance.QueueChatMessageOrCommand( "The " + AttachedFaction.StartFactionColourForLog() + "Templar</color> are now defending a newly opened rift on " +
                    planet.Name, ChatType.LogToCentralChat, chatHandlerOrNull );
            }

        }
        #endregion

        public void HandleCastlesSim( ArcenHostOnlySimContext Context )
        {
            int debugCode = 0;
            try
            {
                bool tracing = this.tracing_shortTerm = Engine_AIW2.TraceAtAll && Engine_AIW2.TracingFlags.Has( ArcenTracingFlags.Templar );
                ArcenCharacterBuffer tracingBuffer = tracing ? ArcenCharacterBuffer.GetFromPoolOrCreate( "Templar-HandleCastlesSim-trace", 10f ) : null;
                debugCode = 100;
                int aip = GetAipTime();
                bool localCastleActive = false;
                bool verboseDebug = true;
                List<SafeSquadWrapper> castles = this.BaseInfo.Castles.GetDisplayList();
                List<SafeSquadWrapper> castleDefenders = this.BaseInfo.CastleDefenders.GetDisplayList();
                for ( int i = 0; i < castles.Count; i++ )
                {
                    debugCode = 200;
                    GameEntity_Squad entity = castles[i].GetSquad();
                    if ( entity == null )
                        continue;
                    TemplarPerUnitBaseInfo data = entity.TryGetExternalBaseInfoAs<TemplarPerUnitBaseInfo>();
                    if ( tracing && verboseDebug )
                        tracingBuffer.Add( "Processing " + entity.ToStringWithPlanet() ).Add(" our current strength " ).Add(data.GetTotalStrengthInside( entity )).Add(" and max allowed strength: " ).Add(GetMaxStrengthForCastle( entity )  ).Add(" our current metal: " + data.MetalStored ).Add( "\n" );
                    this.BaseInfo.LastTimePlanetWasOwned[entity.Planet] = World_AIW2.Instance.GameSecond;
                    if ( data.GetTotalStrengthInside( entity ) < GetMaxStrengthForCastle( entity ) )
                    {
                        debugCode = 300;
                        //lets buy some new ships
                        int income = this.BaseInfo.Difficulty.CastleIncome + (aip / 50) * this.BaseInfo.Difficulty.CastleIncomeIncreasePer50AIP;
                        if ( entity.TypeData.GetHasTag("TemplarInitialEncampment") )
                            income /= 3; //much less income for the initial rift; its intended just to teach the mechanics
                        if ( entity.TypeData.GetHasTag("TemplarEliteLowTierStructure") )
                            income *= 2; //bonus income for late game encampments

                        var pFaction = entity.Planet.GetStanceDataForFaction( entity.GetFactionOrNull_Safe() );
                        if ( pFaction[FactionStance.Hostile].TotalStrength <
                             pFaction[FactionStance.Friendly].TotalStrength + pFaction[FactionStance.Self].TotalStrength )
                            data.MetalStored += income; //Castles on planets with lots of enemies will stop making units so you can't just park a fleet on the planet and farm endlessly
                        if ( tracing && verboseDebug )
                            tracingBuffer.Add( "\tincome: " + income ).Add("\n");

                        if ( data.MetalStored > 0 )
                        {
                            debugCode = 400;
                            if ( tracing && verboseDebug )
                                tracingBuffer.Add( "\t" + entity.ToStringWithPlanet() + " has < " + GetMaxStrengthForCastle( entity ) + " strength, so we get " + income + " metal, giving us a total of " + data.MetalStored ).Add( "\n" );

                            debugCode = 500;
                            string tag = GetTagForShips( entity, Context );
                            GameEntityTypeData entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, tag );
                            if ( entityData == null )
                            {
                                ArcenDebugging.ArcenDebugLogSingleLine( "Failed to find entityData for " + entity.ToStringWithPlanet() + " that wants to build a " + tag, Verbosity.DoNotShow );
                                continue;
                            }
                            debugCode = 600;
                            data.MetalStored -= entityData.CostForAIToPurchase;
                            if ( data.ShipsInside[entityData] > 0 )
                                data.ShipsInside[entityData]++;
                            else
                                data.ShipsInside[entityData] = 1;
                            if ( tracing && verboseDebug )
                                tracingBuffer.Add( "\t\tAfter purchasing a " + entityData.GetDisplayName() + " we have " + data.MetalStored + " metal left\n" );
                        }
                    }
                    debugCode = 1000;
                    data.UpdateShipsInside_ForUI( entity );
                    //If there are enemies in range, spawn all ships inside us

                    Planet planetToHelp = FindEnemiesInCastleRange( entity );
                    data.PlanetCastleWantsToHelp = planetToHelp; //this can be null
                    if ( planetToHelp != null )
                    {
                        debugCode = 1100;
                        localCastleActive = true;
                        PlanetFaction pFaction = entity.PlanetFaction;
                        int shipsDeployed = 0;
                        data.ShipsInside.DoFor( delegate ( KeyValuePair<GameEntityTypeData, int> pair )
                        {
                            debugCode = 1200;
                            shipsDeployed += data.ShipsInside[pair.Key];
                            for ( int j = 0; j < data.ShipsInside[pair.Key]; j++ )
                            {
                                ArcenPoint spawnLocation = entity.Planet.GetSafePlacementPoint_AroundEntity( Context, pair.Key, entity, FInt.FromParts( 0, 005 ), FInt.FromParts( 0, 030 ) );
                                int markLevel = (int)entity.CurrentMarkLevel;
                                if ( entity.TypeData.GetHasTag("TemplarEncampment")  && markLevel > BaseInfo.Difficulty.MaxUnitMarkLevelLowTier )
                                    markLevel = BaseInfo.Difficulty.MaxUnitMarkLevelLowTier;
                                if ( entity.TypeData.GetHasTag("TemplarFastness") && markLevel > BaseInfo.Difficulty.MaxUnitMarkLevelMidTier )
                                    markLevel = BaseInfo.Difficulty.MaxUnitMarkLevelMidTier;
                                if ( entity.TypeData.GetHasTag("TemplarFastness") && markLevel < BaseInfo.Difficulty.MinUnitMarkLevelMidTier )
                                    markLevel = BaseInfo.Difficulty.MinUnitMarkLevelMidTier;
                                if ( entity.TypeData.GetHasTag("TemplarCastle") && markLevel > BaseInfo.Difficulty.MaxUnitMarkLevelHighTier )
                                    markLevel = BaseInfo.Difficulty.MaxUnitMarkLevelHighTier;
                                if ( entity.TypeData.GetHasTag("TemplarCastle") && markLevel < BaseInfo.Difficulty.MinUnitMarkLevelHighTier )
                                    markLevel = BaseInfo.Difficulty.MinUnitMarkLevelHighTier;

                                GameEntity_Squad newEntity = GameEntity_Squad.CreateNew_ReturnNullIfMPClient( pFaction, pair.Key, (byte)markLevel,
                                                                                                              pFaction.Faction.LooseFleet, 0, spawnLocation, Context, "Templar-ExistingCastles" );  //is fine, main sim thread
                                if ( newEntity != null )
                                {
                                    newEntity.ShouldNotBeConsideredAsThreatToHumanTeam = true; //just in case
                                    newEntity.Orders.SetBehaviorDirectlyInSim( EntityBehaviorType.Attacker_Full ); //is fine, main sim thread
                                    TemplarPerUnitBaseInfo newData = newEntity.CreateExternalBaseInfo<TemplarPerUnitBaseInfo>( "TemplarPerUnitBaseInfo" );
                                    newData.DefenseMode = true;
                                    newData.HomeCastle = LazyLoadSquadWrapper.Create(entity);
                                }
                            }

                            return DelReturn.Continue;
                        } );
                        if ( tracing && shipsDeployed > 0 )
                            tracingBuffer.Add( "\t" + entity.ToStringWithPlanet() + " has detected nearby enemies on " + planetToHelp.Name + " and is spawning " + shipsDeployed + " ships.\n" );


                        data.ShipsInside.Clear();
                    }
                    else
                    {
                        debugCode = 1300;
                        //If there are no enemies in range and we have a ship close to us, absorb it
                        //Note we can go over the limit here, or absorb random ships from other castles; that's fine.
                        int range = 100;
                        for ( int j = 0; j < castleDefenders.Count; j++ )
                        {
                            GameEntity_Squad ship = castleDefenders[j].GetSquad();
                            if ( ship == null )
                                continue;
                            if ( ship.Planet != entity.Planet )
                                continue;
                            if ( Mat.DistanceBetweenPointsImprecise( entity.WorldLocation, ship.WorldLocation ) < range )
                            {
                                if ( data.ShipsInside[ship.TypeData] > 0 )
                                    data.ShipsInside[ship.TypeData]++;
                                else
                                    data.ShipsInside[ship.TypeData] = 1;
                                ship.Despawn( Context, true, InstancedRendererDeactivationReason.AFactionJustWarpedMeOut );
                            }
                        }
                    }
                }
                BaseInfo.AnyCastlesActive = localCastleActive;
                if ( tracing && !tracingBuffer.GetIsEmpty() ) ArcenDebugging.ArcenDebugLogSingleLine( tracingBuffer.ToString(), Verbosity.DoNotShow );
                if ( tracing )
                {
                    tracingBuffer.ReturnToPool();
                    tracingBuffer = null;
                }
            }
            catch ( Exception e )
            {
                ArcenDebugging.ArcenDebugLogSingleLine( "Hit an exception in HandleCastlesSim debugCode " + debugCode + " " + e.ToString(), Verbosity.DoNotShow );
            }
        }
        public Planet FindEnemiesInCastleRange( GameEntity_Squad castle )
        {
            bool tracing = this.tracing_shortTerm = Engine_AIW2.TraceAtAll && Engine_AIW2.TracingFlags.Has( ArcenTracingFlags.Templar );
            ArcenCharacterBuffer tracingBuffer = tracing ? ArcenCharacterBuffer.GetFromPoolOrCreate( "Templar-FindEnemiesInCastleRange-trace", 10f ) : null;

            //we can help on friendly or neutral planets with enemies and friends
            Planet foundPlanet = null;
            bool verboseDebug = false;
            short range = this.BaseInfo.Difficulty.LowCastleRange;
            if ( castle.TypeData.GetHasTag("TemplarFastness" ) )
                range = this.BaseInfo.Difficulty.MidCastleRange;
            if ( castle.TypeData.GetHasTag("TemplarCastle" ) )
                range = this.BaseInfo.Difficulty.HighCastleRange;
            if ( tracing && verboseDebug )
                tracingBuffer.Add(castle.ToStringWithPlanet() + " Is checking for enemies within " + range + " hops").Add("\n");
            castle.Planet.DoForPlanetsWithinXHops( range, delegate ( Planet planet, Int16 Distance )
            {
                if ( verboseDebug && tracing )
                    tracingBuffer.Add("\tChecking whether there are enemies on " + planet.Name).Add("\n");

                var pFaction = planet.GetStanceDataForFaction( AttachedFaction );
                int hostileStrength = pFaction[FactionStance.Hostile].TotalStrength;
                if ( planet == castle.Planet && hostileStrength > 0 )
                {
                    //always go for enemies on our planet
                    foundPlanet = planet;
                    if ( verboseDebug && tracing )
                        tracingBuffer.Add("\t\tfound enemies right here\n" );

                    return DelReturn.Break;
                }

                if ( hostileStrength < 2000 )
                {
                    if ( verboseDebug && tracing )
                        tracingBuffer.Add("\t\tNo enemies\n" );
                    return DelReturn.Continue;
                }
                int friendlyStrength = pFaction[FactionStance.Friendly].TotalStrength + pFaction[FactionStance.Self].TotalStrength;
                if ( friendlyStrength < 2000 )
                {
                    if ( verboseDebug && tracing )
                        tracingBuffer.Add("\t\tNo friends\n" );

                    return DelReturn.Continue;
                }
                if ( friendlyStrength < hostileStrength )
                {
                    //if on a remote planet, never go where we are outnumbered
                    //Always come out for our own planet though
                    if ( verboseDebug && tracing )
                        tracingBuffer.Add("\t\tWe are outnumbered\n" );
                    return DelReturn.Continue;
                }
                if ( verboseDebug && tracing )
                    tracingBuffer.Add("\tFOUND " + planet.Name + " with friendly strength " + friendlyStrength + " and hostile strength " + hostileStrength ).Add("\n");

                foundPlanet = planet;
                return DelReturn.Break;
            } ,
              delegate ( Planet secondaryPlanet )
             {
                 var spFaction = secondaryPlanet.GetStanceDataForFaction( AttachedFaction );
                 int hostileStrength = spFaction[FactionStance.Hostile].TotalStrength;
                 int friendlystrength = spFaction[FactionStance.Friendly].TotalStrength + spFaction[FactionStance.Self].TotalStrength;
                 if ( hostileStrength > 10 * 1000 &&
                      hostileStrength > friendlystrength / 2 )
                     return PropogationEvaluation.SelfButNotNeighbors;

                 return PropogationEvaluation.Yes;
             } );
             #region Tracing
            if ( tracing && !tracingBuffer.GetIsEmpty() ) ArcenDebugging.ArcenDebugLogSingleLine( tracingBuffer.ToString(), Verbosity.DoNotShow );
            if ( tracing )
            {
                tracingBuffer.ReturnToPool();
                tracingBuffer = null;
            }
            #endregion

            return foundPlanet;
        }
        public static GameEntityTypeData GetWaveLeaderType ( TemplarDifficulty Difficulty, GameEntity_Squad Castle, bool IsFromHack, ArcenHostOnlySimContext Context )
        {
            int aip;
            try
            {
                // I'm pretty sure this is cheating the static function and it needs to be non-static in future, but we actually need some class values under the new method
                aip = Instance.GetAipTime();

            }
            catch
            {
                aip = FactionUtilityMethods.Instance.GetCurrentAIP().IntValue;

            }
            int percentLowTier = 100;
            int percentMidTier = 0;
            int percentElite = aip - Difficulty.AIPToStartUsingEliteWaveLeaders;
            if ( percentElite < 0 )
                percentElite = 0;
            //int percentHighTier = 0;
            if ( aip > Difficulty.AIPForWaveLeaderTierOne && aip <= Difficulty.AIPForWaveLeaderTierTwo )
            {
                percentLowTier = 75;
                percentMidTier = 25;
                //percentHighTier = 0;
            }
            else if ( aip <= Difficulty.AIPForWaveLeaderTierThree )
            {
                percentLowTier = 50;
                percentMidTier = 45;
                //percentHighTier = 5;
            }
            else if ( aip <= Difficulty.AIPForWaveLeaderTierFour )
            {
                percentLowTier = 25;
                percentMidTier = 50;
                //percentHighTier = 25;
            }
            else
            {
                percentLowTier = 10;
                percentMidTier = 40;
                //percentHighTier = 50;
            }
            int random = Context.RandomToUse.Next(0, 100);
            if ( random < percentLowTier || Castle.TypeData.GetHasTag("TemplarEncampment") )
            {
                //encampments can only spawn low tier wave leaders
                if ( IsFromHack )
                    return GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "LowTierTemplarHackingWaveLeader" );
                if ( Context.RandomToUse.Next(0, 100) < percentElite )
                    return GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "LowTierTemplarEliteWaveLeader" );
                else
                    return GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "LowTierTemplarWaveLeader" );
            }
            else if ( random < (percentMidTier + percentLowTier) || Castle.TypeData.GetHasTag("TemplarFastness") )
            {
                //fastnesses can spawn low or mid tier wave leaders
                if ( IsFromHack )
                    return GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "MidTierTemplarHackingWaveLeader" );

                if ( Context.RandomToUse.Next(0, 100) < percentElite )
                    return GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "MidTierTemplarEliteWaveLeader" );
                else
                    return GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "MidTierTemplarWaveLeader" );
            }
            else{
                if ( IsFromHack )
                    return GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "HighTierTemplarHackingWaveLeader" );

                if ( Context.RandomToUse.Next(0, 100) < percentElite )
                    return GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "HighTierTemplarEliteWaveLeader" );
                else
                    return GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "HighTierTemplarWaveLeader" );
            }
        }
        public static byte GetMarkLevelForWaveLeader(TemplarDifficulty Difficulty, GameEntity_Squad Castle, ArcenHostOnlySimContext Context )
        {
            int markLevel = 1;
            int aip;
            try
            {
                // I'm pretty sure this is cheating the static function and it needs to be non-static in future, but we actually need some class values under the new method
                aip = Instance.GetAipTime();

            }
            catch
            {
                aip = FactionUtilityMethods.Instance.GetCurrentAIP().IntValue;

            }
            if ( aip < Difficulty.AIPForWaveLeaderTierOne )
            {
                markLevel = Context.RandomToUse.Next(1, 2);
            }
            else if ( aip <= Difficulty.AIPForWaveLeaderTierTwo )
            {
                markLevel = Context.RandomToUse.Next(2, 3);
            }
            else if ( aip <= Difficulty.AIPForWaveLeaderTierThree )
                markLevel = Context.RandomToUse.Next(3, 6);
            else if ( aip <= Difficulty.AIPForWaveLeaderTierFour )
                markLevel = Context.RandomToUse.Next(5, 7);

            if ( Castle.TypeData.GetHasTag("TemplarEncampment")  && markLevel > Difficulty.MaxUnitMarkLevelLowTier )
                markLevel = Difficulty.MaxUnitMarkLevelLowTier;
            if ( Castle.TypeData.GetHasTag("TemplarFastness") && markLevel > Difficulty.MaxUnitMarkLevelMidTier )
                markLevel = Difficulty.MaxUnitMarkLevelMidTier;
            if ( Castle.TypeData.GetHasTag("TemplarFastness") && markLevel < Difficulty.MinUnitMarkLevelMidTier )
                markLevel = Difficulty.MinUnitMarkLevelMidTier;
            if ( Castle.TypeData.GetHasTag("TemplarCastle") && markLevel > Difficulty.MaxUnitMarkLevelHighTier )
                markLevel = Difficulty.MaxUnitMarkLevelHighTier;
            if ( Castle.TypeData.GetHasTag("TemplarCastle") && markLevel < Difficulty.MinUnitMarkLevelHighTier )
                markLevel = Difficulty.MinUnitMarkLevelHighTier;

            return (byte) markLevel;
        }
        public override void SeedStartingEntities_EarlyMajorFactionClaimsOnly( Galaxy galaxy, ArcenHostOnlySimContext Context, MapTypeData mapType )
        {
            //First we spawn castles at all AI homeworlds
            GameEntityTypeData typeData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "TemplarSovereign" );
            GameEntityTypeData weakTypeData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "TemplarWeakSovereign" );
            int numCastles = 0;
            World_AIW2.Instance.DoForEntities( EntityRollupType.KingUnitsOnly, delegate ( GameEntity_Squad entity )
            {
                if ( !entity.GetIsFriendlyTowards_Safe( AttachedFaction ) )
                    return DelReturn.Continue;
                numCastles++;
                GameEntity_Squad squad = null;
                if ( this.BaseInfo.Difficulty.WeakSovereign )
                    squad = entity.Planet.Mapgen_SeedEntity( Context, AttachedFaction, weakTypeData, PlanetSeedingZone.MostAnywhere );
                else
                    squad = entity.Planet.Mapgen_SeedEntity( Context, AttachedFaction, typeData, PlanetSeedingZone.MostAnywhere );
                TemplarPerUnitBaseInfo data = squad.CreateExternalBaseInfo<TemplarPerUnitBaseInfo>( "TemplarPerUnitBaseInfo" );
                data.TimeTillmarkUp = 360;
                data.TimeTillSpawnNextConstructor = World_AIW2.Instance.GameSecond + 900; //wait a while before getting started

                return DelReturn.Continue;
            } );
            //always start with a bonus castle somewhere on the map
            int bonusCastles = 1;
            if ( World_AIW2.Instance.CurrentGalaxy.GetTotalPlanetCount() >= 80 )
                bonusCastles++;
            if ( World_AIW2.Instance.CampaignType.HarshnessRating >= 500 ) //challenger and above
                bonusCastles += 2;

            if ( this.BaseInfo.Difficulty.NoCastles )
            {
                //no castles, just spawn fastnesses
                StandardMapPopulator.Mapgen_SeedSpecialEntities( Context, galaxy, AttachedFaction, SpecialEntityType.None, "TemplarMidTierStructure", SeedingType.HardcodedCount, bonusCastles,
                                                                 MapGenCountPerPlanet.One, MapGenSeedStyle.SmallBad, 6, 4, PlanetSeedingZone.MostAnywhere, SeedingExpansionType.ComplicatedOriginal );
            }
            else
            {
                //normal code path; seed castles
                StandardMapPopulator.Mapgen_SeedSpecialEntities( Context, galaxy, AttachedFaction, SpecialEntityType.None, "TemplarHighTierStructure", SeedingType.HardcodedCount, bonusCastles,
                                                                 MapGenCountPerPlanet.One, MapGenSeedStyle.SmallBad, 6, 4, PlanetSeedingZone.MostAnywhere, SeedingExpansionType.ComplicatedOriginal );

            }
            //And now a few other low tier structures
            int numAdditionalLowTier = 2;
            if ( World_AIW2.Instance.CurrentGalaxy.GetTotalPlanetCount() >= 80 )
                numAdditionalLowTier += 1;
            StandardMapPopulator.Mapgen_SeedSpecialEntities( Context, galaxy, AttachedFaction, SpecialEntityType.None, "TemplarLowTierStructure", SeedingType.HardcodedCount, numAdditionalLowTier,
                                                    MapGenCountPerPlanet.One, MapGenSeedStyle.SmallBad, 5, 7, PlanetSeedingZone.MostAnywhere, SeedingExpansionType.ComplicatedOriginal );
        }
        readonly int MaxMarkForLowTier = 7;
        readonly int MaxMarkForMidTier = 7;

        //These are for the strength of units that can be in a castle
        readonly FInt StrengthMulitplierIncreasePerNecromancerTotalMarkForLowTier = FInt.FromParts(0, 07);
        readonly FInt StrengthMulitplierIncreasePerNecromancerTotalMarkForMidTier  = FInt.FromParts(0, 08);
        readonly FInt StrengthMulitplierIncreasePerNecromancerTotalMarkForHighTier = FInt.FromParts(0, 011);
        readonly FInt StrengthMulitplierIncreasePerAIPForLowTier = FInt.FromParts(0, 001);
        readonly FInt StrengthMulitplierIncreasePerAIPForMidTier = FInt.FromParts(0, 002);
        readonly FInt StrengthMulitplierIncreasePerAIPForHighTier = FInt.FromParts(0, 003);
        public int GetMaxStrengthForCastle(  GameEntity_Squad entity )
        {
            int baseStrength = this.BaseInfo.Difficulty.BaseStrengthLowTier;
            FInt strengthMultForNecro = StrengthMulitplierIncreasePerNecromancerTotalMarkForLowTier;
            FInt strengthMultAIP = StrengthMulitplierIncreasePerAIPForLowTier;
            AIDifficulty highestDifficulty = FactionUtilityMethods.Instance.GetHighestAIDifficulty_AsDifficulty();

            if ( highestDifficulty.Difficulty >= 7 )
                baseStrength += 500;
            if ( highestDifficulty.Difficulty >= 9 )
                baseStrength += 500;

            if ( entity.TypeData.GetHasTag("TemplarMidTierStructure") )
            {
                baseStrength = this.BaseInfo.Difficulty.BaseStrengthMidTier;
                strengthMultForNecro = StrengthMulitplierIncreasePerNecromancerTotalMarkForMidTier;
                strengthMultAIP = StrengthMulitplierIncreasePerAIPForMidTier;
            }
            if ( entity.TypeData.GetHasTag("TemplarHighTierStructure") )
            {
                baseStrength = this.BaseInfo.Difficulty.BaseStrengthHighTier;
                strengthMultForNecro = StrengthMulitplierIncreasePerNecromancerTotalMarkForHighTier;
                strengthMultAIP = StrengthMulitplierIncreasePerAIPForHighTier;
            }
            int aip = GetAipTime();
            int output = baseStrength + (baseStrength * (TotalNecropolisMarkLevel * strengthMultForNecro)).IntValue + (baseStrength * (aip * strengthMultAIP)).IntValue;
            return output;
        }

        public void MarkUpCastles( ArcenHostOnlySimContext Context )
        {
            List<SafeSquadWrapper> castles = this.BaseInfo.Castles.GetDisplayList();
            for ( int i = 0; i < castles.Count; i++ )
            {
                GameEntity_Squad castle = castles[i].GetSquad();
                if ( castle == null )
                    continue;
                TemplarPerUnitBaseInfo templarUnitInfo = castle.CreateExternalBaseInfo<TemplarPerUnitBaseInfo>( "TemplarPerUnitBaseInfo" );
                if ( castle.TypeData.GetHasTag("TemplarInitialEncampment" ) )
                    continue; //don't mark up
                if ( castle.CurrentMarkLevel >= 7 && castle.TypeData.GetHasTag("TemplarHighTierStructure" ) )
                    continue;
                if ( this.BaseInfo.Difficulty.NoCastles &&
                     castle.CurrentMarkLevel >= 7 && castle.TypeData.GetHasTag("TemplarMidTierStructure" ) )
                    continue;
                if ( templarUnitInfo.TimeTillmarkUp == -1 )
                    templarUnitInfo.TimeTillmarkUp = World_AIW2.Instance.GameSecond + 240; //we were created unintialized, so initial mapgen
                templarUnitInfo.TimeTillmarkUp--;
                if ( templarUnitInfo.TimeTillmarkUp <= 0 )
                {
                    //mark up or transform as is appropriate
                    if ( castle.TypeData.GetHasTag("TemplarEncampment") &&
                           castle.CurrentMarkLevel >= MaxMarkForLowTier )
                    {
                        GameEntityTypeData postType = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "TemplarMidTierStructure" );
                        if ( castle.TypeData.GetHasTag("TemplarEliteLowTierStructure") )
                             postType = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "TemplarEliteMidTierStructure" );
                        GameEntity_Squad newEntity =  castle.TransformInto( Context, postType, 1, castle.TypeData.KeepDamageAndDebuffsOnTransformation );
                        newEntity.SetCurrentMarkLevel( (byte)1 );
                        TemplarPerUnitBaseInfo newData = newEntity.CreateExternalBaseInfo<TemplarPerUnitBaseInfo>( "TemplarPerUnitBaseInfo" );
                        newData.CopyFrom( templarUnitInfo );
                        newData.TimeTillmarkUp = this.BaseInfo.Difficulty.MarkupIntervalMidTier;
                        continue;
                    }
                    if ( castle.TypeData.GetHasTag("TemplarFastness") &&
                         castle.CurrentMarkLevel >= MaxMarkForMidTier )
                    {
                        GameEntityTypeData postType = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "TemplarHighTierStructure" );
                        if ( castle.TypeData.GetHasTag("TemplarEliteMidTierStructure") )
                            postType = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "TemplarEliteHighTierStructure" );

                        GameEntity_Squad newEntity =  castle.TransformInto( Context, postType, 1, castle.TypeData.KeepDamageAndDebuffsOnTransformation );
                        newEntity.SetCurrentMarkLevel( (byte)1 );
                        TemplarPerUnitBaseInfo newData = newEntity.CreateExternalBaseInfo<TemplarPerUnitBaseInfo>( "TemplarPerUnitBaseInfo" );
                        newData.CopyFrom( templarUnitInfo );
                        newData.TimeTillmarkUp = this.BaseInfo.Difficulty.MarkupIntervalHighTier;
                        continue;
                    }
                    castle.SetCurrentMarkLevel( (byte)(castle.CurrentMarkLevel + 1) );

                    if ( castle.TypeData.GetHasTag("TemplarEncampment") )
                    {
                        templarUnitInfo.TimeTillmarkUp = this.BaseInfo.Difficulty.MarkupIntervalLowTier;
                    }
                    else if ( castle.TypeData.GetHasTag("TemplarFastness") )
                        templarUnitInfo.TimeTillmarkUp = this.BaseInfo.Difficulty.MarkupIntervalMidTier;
                    else if ( castle.TypeData.GetHasTag("TemplarCastle") )
                        templarUnitInfo.TimeTillmarkUp = this.BaseInfo.Difficulty.MarkupIntervalHighTier;
                    else
                        throw new Exception("unable to correctly figure out the markup interval for " + castle.ToStringWithPlanet() );
                }
            }
        }

        public void GenerateNewCastlesIfNecessary( ArcenHostOnlySimContext Context )
        {
            bool tracing = this.tracing_shortTerm = Engine_AIW2.TraceAtAll && Engine_AIW2.TracingFlags.Has(ArcenTracingFlags.Templar);
            ArcenCharacterBuffer tracingBuffer = tracing ? ArcenCharacterBuffer.GetFromPoolOrCreate("Templar-GenerateNewCastlesIfNecessary-trace", 10f) : null;
            try
            {
                GameEntityTypeData constructorData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag(Context, "TemplarConstructor");

                int aip = GetAipTime();
                int percentElite = aip - this.BaseInfo.Difficulty.AIPToStartUsingEliteStructures;
                if (percentElite < 0)
                    percentElite = 0;
                string tag = "TemplarLowTierStructure";
                if (Context.RandomToUse.Next(0, 100) < percentElite)
                    tag = "TemplarEliteLowTierStructure";
                GameEntityTypeData encampmentData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag(Context, tag);
                if (tracing)
                    tracingBuffer.Add("Debug C101");
                bool verboseDebug = false;
                List<SafeSquadWrapper> castles = this.BaseInfo.Castles.GetDisplayList();
                for (int i = 0; i < castles.Count; i++)
                {
                    GameEntity_Squad entity = castles[i].GetSquad();
                    if (entity == null)
                        continue;
                    TemplarPerUnitBaseInfo data = entity.TryGetExternalBaseInfoAs<TemplarPerUnitBaseInfo>();
                    if (!entity.TypeData.GetHasTag("TemplarHighTierStructure"))
                        continue; //this is only for Castles
                    if (tracing && verboseDebug)
                        tracingBuffer.Add(entity.ToStringWithPlanet() + " will spawn a constructor in " + data.TimeTillSpawnNextConstructor + " seconds.");
                    data.TimeTillSpawnNextConstructor--;
                    if (data.TimeTillSpawnNextConstructor < 0)
                    {
                        if (tracing)
                            tracingBuffer.Add("Debug C201");
                        data.TimeTillSpawnNextConstructor = this.BaseInfo.Difficulty.CastleSpawnInterval;
                        if (World_AIW2.Instance.CampaignType.HarshnessRating >= 500) //challenger and above, spawn constructors faster
                            data.TimeTillSpawnNextConstructor -= data.TimeTillSpawnNextConstructor / 5;
                        Planet newCastlePlanet = GetNewCastleLocation(entity, Context);
                        if (tracing)
                            tracingBuffer.Add("\tBuilding new object...").Add("\n");
                        PlanetFaction pFaction = entity.PlanetFaction;
                        int markLevel = (int)entity.CurrentMarkLevel;
                        if (markLevel > this.BaseInfo.Difficulty.MinUnitMarkLevelHighTier)
                            markLevel = this.BaseInfo.Difficulty.MinUnitMarkLevelHighTier;
                        if (markLevel > this.BaseInfo.Difficulty.MaxUnitMarkLevelHighTier)
                            markLevel = this.BaseInfo.Difficulty.MaxUnitMarkLevelHighTier;
                        if (newCastlePlanet == null)
                        {
                            //Corpserule: There's no valid Castle locations - must have captured everything nearby - let's make a flag instead!
                            //Corpserule: Flags should build faster than new encampments, providing opportunities to expand if a castle were to get destroyed, and representing how quickly new flags can be built.
                            data.TimeTillSpawnNextConstructor = data.TimeTillSpawnNextConstructor / 3;
                            ArcenDebugging.ArcenDebugLogSingleLine("C102", Verbosity.DoNotShow);
                            GameEntityTypeData flagData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag(Context, "TemplarFlag");
                            var loc = pFaction.Planet.GetSafePlacementPoint_AroundEntity(Context, flagData, entity, FInt.FromParts(0, 025), FInt.FromParts(0, 120));

                            ArcenDebugging.ArcenDebugLogSingleLine("C202", Verbosity.DoNotShow);
                            GameEntity_Squad newEntity = GameEntity_Squad.CreateNew_ReturnNullIfMPClient(pFaction, flagData, (byte)markLevel,
                                                                                                          null, 0, loc, Context, "Templar-NewFlags");
                            ArcenDebugging.ArcenDebugLogSingleLine("C302", Verbosity.DoNotShow);
                            TemplarPerUnitBaseInfo newData = newEntity?.CreateExternalBaseInfo<TemplarPerUnitBaseInfo>("TemplarPerUnitBaseInfo");


                            ArcenDebugging.ArcenDebugLogSingleLine("C402", Verbosity.DoNotShow);

                        }
                        else
                        {
                            //Corpserule: We found a new castle location, let's build as normal.

                            GameEntity_Squad newEntity = GameEntity_Squad.CreateNew_ReturnNullIfMPClient(pFaction, constructorData, (byte)markLevel,
                                                                                                          pFaction.Faction.LooseFleet, 0, entity.WorldLocation, Context, "Templar-NewCastles");
                            TemplarPerUnitBaseInfo newData = newEntity.CreateExternalBaseInfo<TemplarPerUnitBaseInfo>("TemplarPerUnitBaseInfo");

                            newData.PlanetIdx = newCastlePlanet.Index;
                            newData.UnitToBuild = encampmentData;
                            newData.LocationToBuild = ArcenPoint.ZeroZeroPoint;
                            if (tracing)
                                tracingBuffer.Add("\tConstructor").Add("\n");

                        }
                    }
                }
            }
            catch (Exception e)
            {
                ArcenDebugging.ArcenDebugLogSingleLine("Hit exception in GenerateNewCastlesIfNecessary " + " " + e.ToString(), Verbosity.DoNotShow);
            }
            
            #region Tracing
            if ( tracing && !tracingBuffer.GetIsEmpty() ) ArcenDebugging.ArcenDebugLogSingleLine( tracingBuffer.ToString(), Verbosity.DoNotShow );
            if ( tracing )
            {
                tracingBuffer.ReturnToPool();
                tracingBuffer = null;
            }
            #endregion
        }
        private static readonly List<Planet> WorkingPlanetsList = List<Planet>.Create_WillNeverBeGCed( 100, "TemplarMOTFactionDeepInfo-WorkingPlanetsList" );
        private Planet GetNewCastleLocation( GameEntity_Squad entity, ArcenHostOnlySimContext Context )
        {
            bool tracing = this.tracing_shortTerm = Engine_AIW2.TraceAtAll && Engine_AIW2.TracingFlags.Has( ArcenTracingFlags.Templar );
            ArcenCharacterBuffer tracingBuffer = tracing ? ArcenCharacterBuffer.GetFromPoolOrCreate( "Templar-GetNewCastleLocation-trace", 10f ) : null;

            List<SafeSquadWrapper> castles = this.BaseInfo.Castles.GetDisplayList();
            List<SafeSquadWrapper> constructors = this.BaseInfo.Constructors.GetDisplayList();
            bool verboseDebug = false;
            WorkingPlanetsList.Clear();
            int debugCode = 0;
            try
            {
                entity.Planet.DoForPlanetsWithinXHops( (short)this.BaseInfo.Difficulty.CastleConstructionRange, delegate ( Planet planet, Int16 Distance )
                {
                    debugCode = 400;
                    var pFaction = planet.GetStanceDataForFaction( AttachedFaction );
                    if ( tracing && verboseDebug )
                        tracingBuffer.Add( "\tChecking whether we can build on " + planet.Name ).Add( "\n" );
                    if ( pFaction[FactionStance.Hostile].TotalStrength > 0 )
                    {
                        if ( tracing && verboseDebug )
                            tracingBuffer.Add( "\t\tNo, hostile enemies" ).Add( "\n" );

                        return DelReturn.Continue;
                    }
                    if ( this.BaseInfo.LastTimePlanetWasOwned[planet] > 0 &&
                         ( World_AIW2.Instance.GameSecond - this.BaseInfo.LastTimePlanetWasOwned[planet] ) < BaseInfo.Difficulty.MinTimeBeforeRebuildingCastle )
                    {
                        if ( tracing && verboseDebug )
                            tracingBuffer.Add( "\t\tNo, we've recently owned this planet" ).Add( "\n" );

                        return DelReturn.Continue;
                    }
                    for ( int i = 0; i < castles.Count; i++ )
                    {
                        if ( castles[i].Planet == planet )
                        {
                            if ( tracing && verboseDebug )
                                tracingBuffer.Add( "\t\tNo, we have a castle already" ).Add( "\n" );

                            return DelReturn.Continue;
                        }
                    }
                    for ( int i = 0; i < constructors.Count; i++ )
                    {
                        GameEntity_Squad ship = constructors[i].GetSquad();
                        if ( ship == null )
                            continue;
                        //make sure we don't have a constructor going here
                        TemplarPerUnitBaseInfo constructorData = ship.TryGetExternalBaseInfoAs<TemplarPerUnitBaseInfo>();
                        if ( constructorData.PlanetIdx == planet.Index )
                        {
                            if ( tracing && verboseDebug )
                                tracingBuffer.Add( "\t\tNo, we have a constructor en route already" ).Add( "\n" );

                            return DelReturn.Continue;
                        }
                    }

                    WorkingPlanetsList.Add( planet );
                    if ( WorkingPlanetsList.Count > 5 )
                        return DelReturn.Break;
                    return DelReturn.Continue;
                }, delegate ( Planet secondaryPlanet )
                 {
                     debugCode = 1700;
                 //don't path through hostile planets.
                 var spFaction = secondaryPlanet.GetStanceDataForFaction( AttachedFaction );
                     int hostileStrength = spFaction[FactionStance.Hostile].TotalStrength;
                     int friendlystrength = spFaction[FactionStance.Friendly].TotalStrength + spFaction[FactionStance.Self].TotalStrength;
                     if ( hostileStrength > friendlystrength )
                         return PropogationEvaluation.No;
                     return PropogationEvaluation.Yes;
                 } );

                if ( WorkingPlanetsList.Count == 0 )
                    return null;
                //We prefer further away planets (To make sure the Templar expand rapidly, and also
                //to give the player more chances to kill Constructors
                WorkingPlanetsList.Sort ( delegate( Planet L, Planet R )
                {
                    //sort from "furthest away" to "closest"
                    int lHops = L.GetHopsTo( entity.Planet );
                    int rHops = R.GetHopsTo( entity.Planet );
                    return rHops.CompareTo( lHops );
                } );

                for ( int i = 0; i < WorkingPlanetsList.Count; i++ )
                {
                    if ( Context.RandomToUse.Next( 0, 100 ) < 40 )
                        return WorkingPlanetsList[i];
                }
                return WorkingPlanetsList[0];
            }
            catch ( Exception e )
            {
                ArcenDebugging.ArcenDebugLogSingleLine( "Hit exception in GetNewCastleLocation debugCode " + debugCode + " " + e.ToString(), Verbosity.DoNotShow );
            }
            return null;
        }
        readonly int BuildRange = 150;
        public void HandleConstructorsSim( ArcenHostOnlySimContext Context )
        {
            int debugCode = 0;
            try
            {
                debugCode = 100;
                List<SafeSquadWrapper> constructors = this.BaseInfo.Constructors.GetDisplayList();
                for ( int i = 0; i < constructors.Count; i++ )
                {
                    debugCode = 200;
                    GameEntity_Squad entity = constructors[i].GetSquad();
                    if ( entity == null )
                        continue;
                    TemplarPerUnitBaseInfo data = entity.TryGetExternalBaseInfoAs<TemplarPerUnitBaseInfo>();
                    if ( data.UnitToBuild == null )
                    {
                        //this shouldn't be possible, but just in case
                        data.UnitToBuild = GameEntityTypeDataTable.Instance.GetRandomRowWithTag(Context, "TemplarLowTierStructure");
                        continue;
                    }

                    if ( data.PlanetIdx == -1 )
                    {
                        //This shouldn't be possible, since we only create the constructor if there's a planet
                        //to build on, but just in case
                        Planet newCastlePlanet = GetNewCastleLocation( entity, Context );
                        if ( newCastlePlanet != null )
                        {
                            data.PlanetIdx = newCastlePlanet.Index;
                            continue;
                        }
                        //we couldn't find a planet to build on, so just self-destruct
                        entity.Despawn( Context, true, InstancedRendererDeactivationReason.AFactionJustWarpedMeOut ); //we've created our new castle
                    }

                    if ( entity.Planet.Index != data.PlanetIdx )
                        continue;
                    debugCode = 300;
                    PlanetFaction pFaction = entity.PlanetFaction;
                    if ( data.LocationToBuild == ArcenPoint.ZeroZeroPoint )
                    {
                        //if we have just arrived at our planet but don't have a location to build, pick one
                        data.LocationToBuild = entity.Planet.GetSafePlacementPoint_AroundEntity( Context, data.UnitToBuild, entity, FInt.FromParts( 0, 200 ), FInt.FromParts( 0, 650 ) );
                        continue;
                    }
                    debugCode = 400;
                    if ( Mat.DistanceBetweenPointsImprecise( entity.WorldLocation, data.LocationToBuild ) > BuildRange )
                        continue;
                    debugCode = 500;
                    ArcenPoint finalPoint = entity.Planet.GetSafePlacementPoint_AroundDesiredPointVicinity( Context, data.UnitToBuild, data.LocationToBuild, FInt.FromParts( 0, 10 ), FInt.FromParts( 0, 50 ) );
                    debugCode = 600;

                    GameEntity_Squad newEntity = GameEntity_Squad.CreateNew_ReturnNullIfMPClient( pFaction, data.UnitToBuild, (byte)1,
                                                                                                  pFaction.Faction.LooseFleet, 0, finalPoint, Context, "Templar-Constructor" );
                    debugCode = 700;
                    TemplarPerUnitBaseInfo newData = newEntity.CreateExternalBaseInfo<TemplarPerUnitBaseInfo>( "TemplarPerUnitBaseInfo" );
                    newData.TimeTillmarkUp = this.BaseInfo.Difficulty.MarkupIntervalLowTier;
                    debugCode = 800;
                    debugCode = 900;
                    entity.Despawn( Context, true, InstancedRendererDeactivationReason.AFactionJustWarpedMeOut ); //we've created our new castle
                    debugCode = 1000;
                }
            }
            catch ( Exception e )
            {
                ArcenDebugging.ArcenDebugLogSingleLine( "Hit exception in HandleConstructorsSim debugCode " + debugCode + " " + e.ToString(), Verbosity.DoNotShow );
            }
        }

        public void SpawnTemplarForHunter( ArcenHostOnlySimContext Context )
        {
            int hunterBonusShipInterval = 380;
            int totalMarkLevel = 1;
            if ( FactionUtilityMethods.Instance.AnyNecromancerFactions())
                totalMarkLevel = TotalNecropolisMarkLevel;
            if ( totalMarkLevel < 3 )
                return;
            if ( World_AIW2.Instance.GameSecond % hunterBonusShipInterval != 0 )
                return;
            Faction aiFaction = World_AIW2.GetRandomAIFaction(Context);
            if ( aiFaction == null )
                return; //no living AIs

            AISentinelsFactionBaseInfo sentinelsBaseInfo = aiFaction.GetAISentinelsCoreData();
            Faction hunterFaction = sentinelsBaseInfo.SubFac_Hunter;
            if ( hunterFaction == null )
                return;

            GameEntity_Squad king = FactionUtilityMethods.Instance.findKing( aiFaction );
            if ( king == null )
                return;
            Planet planet = king.Planet;
            if ( planet == null )
                return;
            PlanetFaction pFaction = planet.GetPlanetFactionForFaction( hunterFaction );
            int strengthToSpawn = 2500 * totalMarkLevel;
            int retries = 10;
            while ( strengthToSpawn > 0 && retries-- > 0 )
            {
                GameEntityTypeData entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, "TemplarGuardian" );
                if ( entityData.CostForAIToPurchase > strengthToSpawn )
                {
                    retries--;
                    continue;
                }
                strengthToSpawn -= entityData.CostForAIToPurchase;
                AngleDegrees angle = AngleDegrees.Create( (float)Context.RandomToUse.Next( 1, 360 ) );
                float warpInMultiplier = 0.01f * Context.RandomToUse.Next(0, 20);
                ArcenPoint spawnLocation = planet.GetRandomPointWithinCircleAndAlsoWithinGravWell( king.WorldLocation, (int)(planet.GravWellSize.DistanceScale_GravwellRadius * warpInMultiplier), Context.RandomToUse );
                GameEntity_Squad newEntity = GameEntity_Squad.CreateNew_ReturnNullIfMPClient( pFaction, entityData, AttachedFaction.CurrentGeneralMarkLevel,
                                                                                              pFaction.Faction.LooseFleet, 0, spawnLocation, Context, "Templar-ForHunter" );
                newEntity.Orders.SetBehaviorDirectlyInSim( EntityBehaviorType.Attacker_Full ); //is fine, main sim thread
            }
        }
        public int GetTotalMarkLevelForAllNecromancers()
        {
            int count = 0;
            if (FactionUtilityMethods.Instance.AnyNecromancerFactions())
            {

                NecromancerEmpireFactionBaseInfo.DoForAllNecromancerFactions(delegate (Faction faction)
                {
                    NecromancerEmpireFactionBaseInfo necroBaseInfo = faction.GetExternalBaseInfoAs<NecromancerEmpireFactionBaseInfo>();
                    count += necroBaseInfo.TotalMarkLevelForAllNecropoleis();
                });
            }
            else
                count = 1;
            return count;
        }
        public static int GetTotalHackedRiftsForForAllNecromancers()
        {
            int count = 0;
            NecromancerEmpireFactionBaseInfo.DoForAllNecromancerFactions( delegate ( Faction faction )
            {
                NecromancerEmpireFactionBaseInfo necroBaseInfo = faction.GetExternalBaseInfoAs<NecromancerEmpireFactionBaseInfo>();
                count += necroBaseInfo.NumRiftsHacked;
            } );
            return count;
        }

        public void SpawnWaveLeaders(ArcenHostOnlySimContext Context)
        {
            //These are periodic "Waves" that go after the players. They serve as consistent income for the player, and to challenge them with unpredictable assaults.
            int timeToWait;
            if (BaseInfo.TimeForNextAssaultWave == -1)
            {
                //first wave is at 15 minutes
                BaseInfo.TimeForNextAssaultWave = World_AIW2.Instance.GameSecond + 900;
                BaseInfo.WaveLeadersToSpawn = 3;
            }
            if (BaseInfo.TimeForHackCD == -1 || BaseInfo.TimeForHackCD <= World_AIW2.Instance.GameSecond)
            {
                // Corpserule: Turns the ability to hack on, this continually is set to on until a hack response happens, in which case ValidHack = false, and TimeForHackCD gets 20s added
                BaseInfo.ValidHack = true;
            }
            bool spawnWave = false;
            if (BaseInfo.SpawnTemplarWaveNextSecond ||
                 BaseInfo.TimeForNextAssaultWave <= World_AIW2.Instance.GameSecond)
            {
                spawnWave = true;
                BaseInfo.SpawnTemplarWaveNextSecond = false;
            }

            if (!spawnWave)
            {
                return;
            }

            if (NecromancerEmpireFactionBaseInfo.GetNecromancerFactionCount() > 0)
            {
                World_AIW2.Instance.QueueLogJournalEntryToSidebar("NA_Templar_Gameplay", string.Empty, this.AttachedFaction, null, null, OnClient.DoThisOnHostOnly_WillBeSentToClients);
            }

            int difficulty = FactionUtilityMethods.Instance.GetHighestAIDifficulty();
            int aip = GetAipTime();
            List<SafeSquadWrapper> castlesTopTier = this.BaseInfo.CastlesTopTier.GetDisplayList();
            //spawn my wave leaders
            ArcenDebugging.ArcenDebugLogSingleLine("We are spawning " + BaseInfo.WaveLeadersToSpawn + " leaders", Verbosity.DoNotShow);
            for (int i = 0; i < BaseInfo.WaveLeadersToSpawn; i++)
            {
                if (castlesTopTier.Count == 0)
                    break; //nothing to be done; the player has killed all our castles
                GameEntity_Squad castle = castlesTopTier[Context.RandomToUse.Next(0, castlesTopTier.Count)].GetSquad();
                if (castle == null)
                    continue;

                byte markLevel = GetMarkLevelForWaveLeader(this.BaseInfo.Difficulty, castle, Context);
                GameEntityTypeData leaderTypeData = GetWaveLeaderType(BaseInfo.Difficulty, castle, false, Context);
                if (leaderTypeData == null)
                    throw new Exception("Could not find wave suitable wave leader");
                PlanetFaction pFaction = castle.PlanetFaction;
                GameEntity_Squad leader = GameEntity_Squad.CreateNew_ReturnNullIfMPClient(pFaction, leaderTypeData, (byte)markLevel,
                                                                                           pFaction.Faction.LooseFleet, 0, castle.WorldLocation, Context, "Templar-WaveLeaders");
                leader.Orders.SetBehaviorDirectlyInSim(EntityBehaviorType.Attacker_Full); //is fine, main sim thread
                ArcenDebugging.ArcenDebugLogSingleLine("\tSpawned " + leader.ToStringWithPlanet(), Verbosity.DoNotShow);
            }

            //Decide the next wave time
            int maxTime = BaseInfo.Difficulty.BaseWaveInterval;
            timeToWait = Context.RandomToUse.Next(maxTime - maxTime / 10, maxTime + maxTime / 10);
            BaseInfo.TimeForNextAssaultWave = World_AIW2.Instance.GameSecond + timeToWait;
            //Decide the number of wave leaders
            int baseWaveLeaders = 3 + (aip / this.BaseInfo.Difficulty.AIPPerBaseWaveLeader);
            if (World_AIW2.Instance.CampaignType.HarshnessRating >= 500) //challenger and above, spawn more wave leaders
                BaseInfo.WaveLeadersToSpawn += 3;
            if (NecromancerEmpireFactionBaseInfo.GetNecromancerFactionCount() > 0)
            {
                //more wave leaders for more necromancers, to make sure there are more resources around
                baseWaveLeaders += (NecromancerEmpireFactionBaseInfo.GetNecromancerFactionCount() - 1) * 2;
            }
            BaseInfo.WaveLeadersToSpawn = Context.RandomToUse.Next(baseWaveLeaders - 1, baseWaveLeaders + 1); //a bit of randomness
            if (BaseInfo.WaveLeadersToSpawn <= 0)
                BaseInfo.WaveLeadersToSpawn = 1;
            if (NecromancerEmpireFactionBaseInfo.GetNecromancerFactionCount() > 0)
            {
                Faction necromancerFaction = NecromancerEmpireFactionBaseInfo.GetFirstNecromancerFactionOrNull();
                World_AIW2.Instance.QueueChatMessageOrCommand("The " + AttachedFaction.StartFactionColourForLog() + "Templar</color> are launching an assault wave against the " +
                                                               necromancerFaction.StartFactionColourForLog() + "Necromancer</color> with <color=#ff0000>" + BaseInfo.WaveLeadersToSpawn + "</color> wave leaders.", ChatType.LogToCentralChat, null);
            }
            else
            {
                World_AIW2.Instance.QueueChatMessageOrCommand("The " + AttachedFaction.StartFactionColourForLog() + "Templar</color> are launching an assault wave against their enemies with <color=#ff0000>" + BaseInfo.WaveLeadersToSpawn + "</color> wave leaders.", ChatType.LogToCentralChat, null);
            }
        }
        private void SpawnShips( string TagToSpawn, int StrengthToSpawn, GameEntity_Squad castle, ArcenHostOnlySimContext Context )
        {
            bool tracing = this.tracing_shortTerm = Engine_AIW2.TraceAtAll && Engine_AIW2.TracingFlags.Has( ArcenTracingFlags.Templar );
            ArcenCharacterBuffer tracingBuffer = tracing ? ArcenCharacterBuffer.GetFromPoolOrCreate( "Templar-SpawnShips-trace", 10f ) : null;

            PlanetFaction pFaction = castle.PlanetFaction;
            int numSpawned = 0;
            //ArcenDebugging.ArcenDebugLogSingleLine("Got mark level " + markLevel + " for " + TagToSpawn + " spawning from " + castle.ToStringWithPlanet() + " totalMarkLevel " + totalMarkLevel, Verbosity.DoNotShow );
            int retries = 10;
            while ( StrengthToSpawn > 0 && retries-- > 0 )
            {
                numSpawned++;
                GameEntityTypeData entityData = GameEntityTypeDataTable.Instance.GetRandomRowWithTag( Context, TagToSpawn );
                if ( entityData == null )
                    throw new Exception("Couldn't find templar units with tag " + TagToSpawn);
                if ( StrengthToSpawn < (entityData.CostForAIToPurchase * 2) / 3 )
                    continue; //We can round up if we're close
                StrengthToSpawn -= entityData.CostForAIToPurchase;
                AngleDegrees angle = AngleDegrees.Create( (float)Context.RandomToUse.Next( 1, 360 ) );
                float warpInMultiplier = 0.001f * Context.RandomToUse.Next(0, 200);
                ArcenPoint spawnLocation = castle.Planet.GetRandomPointWithinCircleAndAlsoWithinGravWell( castle.WorldLocation, (int)(castle.Planet.GravWellSize.DistanceScale_GravwellRadius * warpInMultiplier), Context.RandomToUse );

                int markLevel = (int)castle.CurrentMarkLevel;
                if ( castle.TypeData.GetHasTag("TemplarEncampment")  && markLevel > BaseInfo.Difficulty.MaxUnitMarkLevelLowTier )
                    markLevel = BaseInfo.Difficulty.MaxUnitMarkLevelLowTier;
                if ( castle.TypeData.GetHasTag("TemplarFastness") && markLevel > BaseInfo.Difficulty.MaxUnitMarkLevelMidTier )
                    markLevel = BaseInfo.Difficulty.MaxUnitMarkLevelMidTier;
                if ( castle.TypeData.GetHasTag("TemplarFastness") && markLevel < BaseInfo.Difficulty.MinUnitMarkLevelMidTier )
                    markLevel = BaseInfo.Difficulty.MinUnitMarkLevelMidTier;
                if ( castle.TypeData.GetHasTag("TemplarCastle") && markLevel > BaseInfo.Difficulty.MaxUnitMarkLevelHighTier )
                    markLevel = BaseInfo.Difficulty.MaxUnitMarkLevelHighTier;
                if ( castle.TypeData.GetHasTag("TemplarCastle") && markLevel < BaseInfo.Difficulty.MinUnitMarkLevelHighTier )
                    markLevel = BaseInfo.Difficulty.MinUnitMarkLevelHighTier;

                GameEntity_Squad newEntity = GameEntity_Squad.CreateNew_ReturnNullIfMPClient( pFaction, entityData, (byte)markLevel,
                                                                                              pFaction.Faction.LooseFleet, 0, spawnLocation, Context, "Templar-GeneralShips" );
                newEntity.Orders.SetBehaviorDirectlyInSim( EntityBehaviorType.Attacker_Full ); //is fine, main sim thread
                // if ( newEntity.TypeData.GetHasTag("TemplarDire") )
                //     ArcenDebugging.ArcenDebugLogSingleLine("\tSpawning a " + entityData.GetDisplayName(), Verbosity.DoNotShow );
                
            }
            if ( tracing )
                tracingBuffer.Add("We have spawned " + numSpawned + " units with tag " + TagToSpawn + " on " + castle.ToStringWithPlanet() +"\n");
            #region Tracing
            if ( tracing && !tracingBuffer.GetIsEmpty() ) ArcenDebugging.ArcenDebugLogSingleLine( tracingBuffer.ToString(), Verbosity.DoNotShow );
            if ( tracing )
            {
                tracingBuffer.ReturnToPool();
                tracingBuffer = null;
            }
            #endregion
        }
        public static readonly List<SafeSquadWrapper> UnassignedShipsLRP = List<SafeSquadWrapper>.Create_WillNeverBeGCed( 150, "TemplarMOTFactionDeepInfo-UnassignedShipsLRP" );
        public static readonly DictionaryOfLists<Planet, SafeSquadWrapper> UnassignedShipsByPlanetLRP = DictionaryOfLists<Planet, SafeSquadWrapper>.Create_WillNeverBeGCed( 100, 60, "TemplarMOTFactionDeepInfo-UnassignedShipsByPlanetLRP" );
        public static readonly DictionaryOfLists<Planet, SafeSquadWrapper> InCombatShipsNeedingOrdersLRP = DictionaryOfLists<Planet, SafeSquadWrapper>.Create_WillNeverBeGCed( 100, 60, "TemplarMOTFactionDeepInfo-InCombatShipsNeedingOrdersLRP" );
        public static readonly List<SafeSquadWrapper> WorkingMetalGeneratorList = List<SafeSquadWrapper>.Create_WillNeverBeGCed( 150, "TemplarMOTFactionDeepInfo-WorkingMetalGeneratorList" );

        public readonly List<SafeSquadWrapper> ConstructorsLRP = List<SafeSquadWrapper>.Create_WillNeverBeGCed( 150, "TemplarMOTFactionDeepInfo-ConstructorsLRP" );
        public readonly List<SafeSquadWrapper> CastleDefendersLRP = List<SafeSquadWrapper>.Create_WillNeverBeGCed( 150, "TemplarMOTFactionDeepInfo-CastleDefendersLRP" );
        public readonly List<SafeSquadWrapper> CastleDefendersRetreatingLRP = List<SafeSquadWrapper>.Create_WillNeverBeGCed( 150, "TemplarMOTFactionDeepInfo-CastleDefendersRetreatingLRP" );
        public override void DoLongRangePlanning_OnBackgroundNonSimThread_Subclass( ArcenLongTermIntermittentPlanningContext Context )
        {
            bool tracing = this.tracing_longTerm = Engine_AIW2.TraceAtAll && Engine_AIW2.TracingFlags.Has( ArcenTracingFlags.Templar );
            ArcenCharacterBuffer tracingBuffer = tracing ? ArcenCharacterBuffer.GetFromPoolOrCreate( "Templar-DoLongRangePlanning_OnBackgroundNonSimThread_Subclass-trace", 10f ) : null;
            int debugCode = 0;
            PerFactionPathCache pathingCacheData = PerFactionPathCache.GetCacheForTemporaryUse_MustReturnToPoolAfterUseOrLeaksMemory();
            try
            {
                var sum = 0;
                World_AIW2.Instance.DoForEntities(
                    (GameEntity_Squad e)=>
                    {
                        sum += e.CurrentMarkLevel;
                        return DelReturn.Continue;
                    },
                    "NecromancerNecropolis");
                if (sum < 1)
                    sum = 1;
                TotalNecropolisMarkLevel = sum;

                int highestDifficulty = FactionUtilityMethods.Instance.GetHighestAIDifficulty();
                debugCode = 100;
                UnassignedShipsLRP.Clear();
                UnassignedShipsByPlanetLRP.Clear();
                InCombatShipsNeedingOrdersLRP.Clear();
                ConstructorsLRP.Clear();
                CastleDefendersLRP.Clear();
                CastleDefendersRetreatingLRP.Clear();
                int totalShips = 0;
                int totalStrength = 0;
                //Iterate over all our units to figure out if any need orders
                AttachedFaction.DoForEntities( delegate ( GameEntity_Squad entity )
                {
                    debugCode = 200;
                    if ( entity == null )
                        return DelReturn.Continue;

                    debugCode = 101;
                    if ( entity.TypeData.GetHasTag( "TemplarConstructor" ) )
                    {
                        debugCode = 300;
                        ConstructorsLRP.Add( entity );
                        return DelReturn.Continue;
                    }
                    if ( !entity.TypeData.IsMobileCombatant )
                        return DelReturn.Continue;
                    TemplarPerUnitBaseInfo data = entity.TryGetExternalBaseInfoAs<TemplarPerUnitBaseInfo>();
                    if ( data == null )
                        return DelReturn.Continue; //race against sim I expect
                    debugCode = 400;
                    if ( data.DefenseMode )
                    {
                        //We are defensive units
                        CastleDefendersLRP.Add( entity );
                        return DelReturn.Continue;
                    }
                    debugCode = 500;
                    totalShips++;
                    totalStrength += entity.GetStrengthOfSelfAndContents();
                    if ( entity.HasQueuedOrders() )
                    {
                        debugCode = 600;
                        //entity is doing something already (either attacking a target or en route somewhere)
                        Planet eventualDestinationOrNull = entity.Orders.GetFinalDestinationOrNull();
                        if ( eventualDestinationOrNull == null && entity.HasExplicitOrders() )
                            return DelReturn.Continue; //we are going somewhere on this planet, that's not an FRD order (like we have specifically chosen a target), let this ship keep doing what it's doing
                        if ( eventualDestinationOrNull != null )
                        {
                            var factionData = eventualDestinationOrNull.GetStanceDataForFaction( AttachedFaction );
                            if ( factionData[FactionStance.Hostile].TotalStrength > 5000 ||
                                 eventualDestinationOrNull.GetControllingFaction().GetIsHostileTowards( AttachedFaction ) )
                                return DelReturn.Continue; //if the planet we are going has enemies, keep going there
                        }
                    }
                    if ( ShouldTemplarAvoidPlanet ( entity.Planet ))
                    {
                        //flee this planet
                        UnassignedShipsLRP.Add( entity );
                        UnassignedShipsByPlanetLRP[entity.Planet].Add( entity );
                        return DelReturn.Continue;
                    }
                    debugCode = 700;
                    Faction controllingFaction = entity.Planet.GetControllingOrInfluencingFaction();
                    if ( controllingFaction.GetIsHostileTowards( AttachedFaction ) &&
                         controllingFaction.Type == FactionType.Player )
                    {
                        //This entity is on an enemy planet but doesn't have orders to attack a specific valuable target
                        //This means we will always kill a command stations before moving on
                        //If it's not a player-owned planet then we will be more willing to move on, since things like the or Dyson Sphere (faction) may not ever lose controll of the planet
                        debugCode = 800;
                        InCombatShipsNeedingOrdersLRP[entity.Planet].Add( entity );

                        return DelReturn.Continue;
                    }
                    //okay, we're on a friendly planet instead
                    if ( entity.Planet.GetControllingOrInfluencingFaction().GetIsFriendlyTowards( AttachedFaction ) )
                    {
                        var factionData = entity.Planet.GetStanceDataForFaction( AttachedFaction );
                        if ( factionData[FactionStance.Hostile].TotalStrength > 6000 ) //if there is at least 3 strength there that we are enemies to
                        {
                            //This entity is on an allied planet with at least 6 enemy strength there, but doesn't have orders to attack a specific valuable target
                            //So go kill some dudes
                            debugCode = 900;
                            InCombatShipsNeedingOrdersLRP[entity.Planet].Add( entity );

                            return DelReturn.Continue;
                        }
                    }
                    //This unit doesn't have any active orders and isn't on an enemy planet. Find an enemy planet
                    debugCode = 1000;
                    UnassignedShipsLRP.Add( entity );
                    UnassignedShipsByPlanetLRP[entity.Planet].Add( entity );
                    return DelReturn.Continue;
                } );
                debugCode = 1100;
                HandleConstructorsLRP( Context, pathingCacheData );

                debugCode = 1300;
                if ( tracing && totalShips > 0 )
                    tracingBuffer.Add( totalShips + " with strength " + (totalStrength / 1000) + " in the Templar Assault right now.\n" );

                //First, handle the ships not in combat
                //Here's the rule. First we pick our preferred planets (player or civil war AI planets), then we sort them
                //We prefer close and weak player planets.
                List<Planet> preferredTargets = Planet.GetTemporaryPlanetList( "Templar-DoLongRangePlanning_OnBackgroundNonSimThread_Subclass-preferredTargets", 10f );
                List<Planet> fallbackTargets = Planet.GetTemporaryPlanetList( "Templar-DoLongRangePlanning_OnBackgroundNonSimThread_Subclass-fallbackTargets", 10f );

                UnassignedShipsByPlanetLRP.DoFor( delegate ( KeyValuePair<Planet, List<SafeSquadWrapper>> pair )
                 {
                     debugCode = 1400;
                     Planet startPlanet = pair.Key;
                     if ( tracing )
                         tracingBuffer.Add( pair.Value.Count + " ships on " + startPlanet.Name + " are looking for a target\n" );
                     preferredTargets.Clear();
                     fallbackTargets.Clear();
                     debugCode = 1500;
                     startPlanet.DoForPlanetsWithinXHops( -1, delegate ( Planet planet, Int16 Distance )
                     {
                         debugCode = 1600;
                         if ( IsPreferredTarget( planet, Context ) )
                         {
                             preferredTargets.Add( planet );
                             return DelReturn.Continue;
                         }

                         if ( IsFallbackTarget( planet, Context ) )
                             fallbackTargets.Add( planet );

                         return DelReturn.Continue;
                     }, delegate ( Planet secondaryPlanet )
                     {
                         debugCode = 1700;
                         if ( startPlanet == secondaryPlanet )
                             return PropogationEvaluation.Yes; //always look outside us
                         //don't path through hostile planets.
                         if ( secondaryPlanet.GetControllingOrInfluencingFaction().GetIsHostileTowards( AttachedFaction ) )
                             return PropogationEvaluation.SelfButNotNeighbors;
                         return PropogationEvaluation.Yes;
                     } );
                     debugCode = 1800;
                     if ( tracing )
                     {
                         tracingBuffer.Add( "\tWe have " + preferredTargets.Count + " preferred targets\n" );
                         for ( int i = 0; i < preferredTargets.Count; i++ )
                             tracingBuffer.Add( "\t\t" + preferredTargets[i].Name ).Add( "\n" );
                         if ( fallbackTargets.Count > 0 )
                         {
                             tracingBuffer.Add( "\tWe have " + fallbackTargets.Count + " fallback targets\n" );
                             for ( int i = 0; i < fallbackTargets.Count; i++ )
                                 tracingBuffer.Add( "\t\t" + fallbackTargets[i].Name ).Add( "\n" );
                         }
                     }
                     debugCode = 1900;
                     //choose the target. First try to get a preferred planet
                     Planet target = null;
                     if ( preferredTargets.Count > 0 )
                     {
                         debugCode = 2000;
                         if ( tracing )
                             tracingBuffer.Add( "\tAttempting to pick a preferredTarget\n" );
                         preferredTargets.Sort( delegate ( Planet L, Planet R )
                         {
                             debugCode = 2100;
                             //To sort the planets, we factor how scary a planet is and how far it is away. We prefer nearer and weaker targets
                             //TODO if desired: also say "if this has an AIP increaser, want to attack it a bit more"
                             //that would make it a tad more evil
                             var lFactionData = L.GetStanceDataForFaction( AttachedFaction );
                             var rFactionData = R.GetStanceDataForFaction( AttachedFaction );
                             int lhops = startPlanet.GetHopsTo( L );
                             int rhops = startPlanet.GetHopsTo( R );
                             int lEnemyStrength = lFactionData[FactionStance.Hostile].TotalStrength;
                             int rEnemyStrength = rFactionData[FactionStance.Hostile].TotalStrength;
                             int lFriendlyStrength = lFactionData[FactionStance.Friendly].TotalStrength +
                                 lFactionData[FactionStance.Self].TotalStrength;
                             int rFriendlyStrength = rFactionData[FactionStance.Friendly].TotalStrength +
                                 rFactionData[FactionStance.Self].TotalStrength;

                             int lstrength = lEnemyStrength - lFriendlyStrength;
                             int rstrength = rEnemyStrength - rFriendlyStrength;
                             FInt factorPerHop = FInt.FromParts( 5, 000 );
                             if ( highestDifficulty > 7 )
                                 factorPerHop = FInt.One; //on higher difficulties, the AI prefers to focus exclusively on weaker targets
                             int lVal = (lhops * factorPerHop).IntValue * lstrength;
                             int rVal = (rhops * factorPerHop).IntValue * rstrength;

                             return lVal.CompareTo( rVal );
                         } );
                         debugCode = 2200;
                         for ( int i = 0; i < preferredTargets.Count; i++ )
                         {
                             int percentToUse = 50;
                             if ( highestDifficulty > 7 )
                                 percentToUse = 80; //more focus on higher difficulties
                             if ( Context.RandomToUse.Next( 0, 100 ) < percentToUse )
                             {
                                 //weighted choice
                                 target = preferredTargets[i];
                                 break;
                             }
                         }
                         if ( target == null ) //if we didn't pick one randomly, just take the best
                             target = preferredTargets[0];
                     }

                     debugCode = 2300;
                     if ( target == null && fallbackTargets.Count > 0 )
                     {
                         debugCode = 2400;
                         //this is very similar to the preferredTargets code above
                         if ( tracing )
                             tracingBuffer.Add( "\tAttempting to pick a fallbackTarget\n" );
                         fallbackTargets.Sort( delegate ( Planet L, Planet R )
                         {
                             var lFactionData = L.GetStanceDataForFaction( AttachedFaction );
                             var rFactionData = R.GetStanceDataForFaction( AttachedFaction );
                             int lhops = startPlanet.GetHopsTo( L );
                             int rhops = startPlanet.GetHopsTo( R );
                             int lEnemyStrength = lFactionData[FactionStance.Hostile].TotalStrength;
                             int rEnemyStrength = rFactionData[FactionStance.Hostile].TotalStrength;
                             int lFriendlyStrength = lFactionData[FactionStance.Friendly].TotalStrength +
                                 lFactionData[FactionStance.Self].TotalStrength;
                             int rFriendlyStrength = rFactionData[FactionStance.Friendly].TotalStrength +
                                 rFactionData[FactionStance.Self].TotalStrength;

                             int lstrength = lEnemyStrength - lFriendlyStrength;
                             int rstrength = rEnemyStrength - rFriendlyStrength;
                             FInt factorPerHop = FInt.FromParts( 0, 500 );

                             int lVal = (lhops / factorPerHop).IntValue * lstrength;
                             int rVal = (rhops / factorPerHop).IntValue * rstrength;

                             return lVal.CompareTo( rVal );
                         } );
                         for ( int i = 0; i < fallbackTargets.Count; i++ )
                         {
                             if ( Context.RandomToUse.Next( 0, 100 ) < 50 )
                             {
                                 target = fallbackTargets[i];
                                 break;
                             }
                         }
                         if ( target == null ) //if we didn't pick one randomly, just take the best
                             target = fallbackTargets[0];
                     }


                     debugCode = 2500;
                     if ( target == null )
                         return DelReturn.Continue;
                     if ( tracing )
                     {
                         tracingBuffer.Add( "\tSending " + pair.Value.Count + " ships from " + startPlanet.Name + " to attack " + target.Name ).Add( "\n" );
                     }
                     debugCode = 2600;
                     AttackTargetPlanet( startPlanet, target, pair.Value, Context, pathingCacheData );
                     return DelReturn.Continue;
                 } );

                Planet.ReleaseTemporaryPlanetList( preferredTargets );
                Planet.ReleaseTemporaryPlanetList( fallbackTargets );

                debugCode = 3000;
                //This is a ship on a planet controlled by our enemies. Pick a target and go after it.
                List<SafeSquadWrapper> targetSquads = GameEntity_Squad.GetTemporarySquadList( "Templar-DoLongRangePlanning_OnBackgroundNonSimThread_Subclass-targetSquads", 10f );

                debugCode = 3100;
                InCombatShipsNeedingOrdersLRP.DoFor( delegate ( KeyValuePair<Planet, List<SafeSquadWrapper>> pair )
                {
                    debugCode = 3200;
                    PlanetFaction pFaction = pair.Key.GetControllingPlanetFaction();
                    Planet planet = pair.Key;
                    targetSquads.Clear();
                    if ( tracing )
                        tracingBuffer.Add( "Finding a target for " + pair.Value.Count + " ships on " + pair.Key.Name ).Add( " if necessary\n" );

                    int rand = Context.RandomToUse.Next( 0, 100 );
                    var factionData = planet.GetStanceDataForFaction( AttachedFaction );
                    int enemyStrength = factionData[FactionStance.Hostile].TotalStrength;
                    int friendlyStrength = factionData[FactionStance.Self].TotalStrength + factionData[FactionStance.Friendly].TotalStrength;

                    if ( enemyStrength < friendlyStrength / 10 ) //Relentless waves can tachyon blast planets to prevent a small number of cloaked units from disrupting things. This is particularly possible with civilian industry cloaked defensive structures
                        FactionUtilityMethods.Instance.TachyonBlastPlanet( planet, AttachedFaction, Context );


                    if ( planet.GetControllingFactionType() == FactionType.Player )
                    {
                        //This is a player planet, so lets see if we can do anything clever/sneaky

                        Planet kingPlanet = null;
                        if ( FactionUtilityMethods.Instance.IsPlanetAdjacentToPlayerKing( planet, out kingPlanet ) )
                        {
                            //First, if we are adjacent to a player homeworld then go for that if we can
                            var neighborFactionData = kingPlanet.GetStanceDataForFaction( AttachedFaction );
                            StrengthData_PlanetFaction_Stance neighborHostileStrengthData = neighborFactionData[FactionStance.Hostile];
                            int neighborHostileStrengthTotal = neighborHostileStrengthData.TotalStrength;
                            if ( neighborHostileStrengthTotal < (friendlyStrength) / 2 )
                            {
                                //if we are too much weaker than the AI homeworld, don't bother. Only if we might make things interesting
                                GameEntity_Other thisWormhole = planet.GetWormholeTo( kingPlanet );
                                bool foundBlockingShield = FactionUtilityMethods.Instance.IsShieldBlockingWormholeToPlanet( planet, kingPlanet, AttachedFaction );
                                if ( !foundBlockingShield )
                                {
                                    if ( tracing )
                                        tracingBuffer.Add( "\tWe are going to attack the player king on " ).Add( kingPlanet.Name ).Add( "\n" );

                                    GameCommand sneakCommand = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetWormholePath_AIRaidKing], GameCommandSource.AnythingElse );
                                    debugCode = 3300;
                                    for ( int j = 0; j < pair.Value.Count; j++ )
                                    {
                                        sneakCommand.RelatedEntityIDs.Add( pair.Value[j].PrimaryKeyID );
                                    }

                                    debugCode = 3400;
                                    if ( sneakCommand != null && sneakCommand.RelatedEntityIDs.Count > 0 )
                                    {
                                        sneakCommand.RelatedString = "AI_WAVE_GOKING";
                                        sneakCommand.ToBeQueued = false;
                                        sneakCommand.RelatedIntegers.Add( kingPlanet.Index );
                                        World_AIW2.Instance.QueueGameCommand( this.AttachedFaction, sneakCommand, playAudioEffectForCommand );
                                        return DelReturn.Continue;
                                    }
                                }
                            }
                        }
                        if ( Context.RandomToUse.Next( 0, 100 ) < 25 )
                        {
                            debugCode = 3500;
                            GameEntity_Squad target = planet.GetControllingCityCenterOrNull() ?? planet.GetCommandStationOrNull();
                            if ( target != null )
                            {
                                debugCode = 3600;
                                GameCommand commandStationAttackCommand = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.Attack], GameCommandSource.AnythingElse );
                                commandStationAttackCommand.ToBeQueued = false;

                                commandStationAttackCommand.RelatedIntegers4.Add( target.PrimaryKeyID );
                                debugCode = 3700;
                                for ( int j = 0; j < pair.Value.Count; j++ )
                                {
                                    commandStationAttackCommand.RelatedEntityIDs.Add( pair.Value[j].PrimaryKeyID );
                                }
                                debugCode = 3800;
                                World_AIW2.Instance.QueueGameCommand( this.AttachedFaction, commandStationAttackCommand, playAudioEffectForCommand );
                                if ( tracing ) tracingBuffer.Add( "\n" ).Add( "Threat of " + commandStationAttackCommand.RelatedEntityIDs.Count + " units attacking command station" );
                                commandStationAttackCommand = null;
                                return DelReturn.Continue;
                            }
                        }
                        if ( enemyStrength > friendlyStrength )
                        {
                            //if we don't think we can comfortably win this (and remember, we are at a disadvantage when attacking a player)
                            //then see if we can bypass and find a weaker target
                            debugCode = 3900;
                            rand = Context.RandomToUse.Next( 0, 100 ); //recalculate the random number
                            if ( rand < 50 )
                            {
                                debugCode = 4000;
                                //If this planet seems pretty tough for me, see if there are any adjacent weaker player planets and go for those
                                //TODO: we should actually use a List here and select randomly in case there are multiple good options
                                //We might also want to enhance Helper_RetreatThreat to incorporate this style of 'sneaking past player defenses'
                                Planet newTarget = null;
                                int weakestPlanetNeighborStrength = 0;
                                planet.DoForLinkedNeighbors( false, delegate ( Planet neighbor )
                                {
                                    if ( neighbor.GetControllingFactionType() != FactionType.Player )
                                        return DelReturn.Continue;
                                    debugCode = 4100;
                                    var neighborFactionData = neighbor.GetStanceDataForFaction( AttachedFaction );
                                    StrengthData_PlanetFaction_Stance neighborHostileStrengthData = neighborFactionData[FactionStance.Hostile];
                                    int neighborHostileStrengthTotal = neighborHostileStrengthData.TotalStrength - friendlyStrength;
                                    if ( neighborHostileStrengthTotal > enemyStrength )
                                        return DelReturn.Continue; //not interested in more heavily defeneded planets

                                    debugCode = 4200;
                                    if ( neighborHostileStrengthTotal < weakestPlanetNeighborStrength && !FactionUtilityMethods.Instance.IsShieldBlockingWormholeToPlanet( planet, neighbor, AttachedFaction ) )
                                    {
                                        weakestPlanetNeighborStrength = neighborHostileStrengthTotal;
                                        newTarget = neighbor;
                                    }
                                    return DelReturn.Continue;
                                } );
                                debugCode = 4300;
                                if ( newTarget != null )
                                {
                                    debugCode = 4400;
                                    if ( tracing ) tracingBuffer.Add( "\n" ).Add( "Threat bypass to weaker planet: " + newTarget.Name ).Add( "\n" );
                                    GameCommand sneakCommand = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetWormholePath_AIRaidKing], GameCommandSource.AnythingElse );
                                    for ( int j = 0; j < pair.Value.Count; j++ )
                                    {
                                        sneakCommand.RelatedEntityIDs.Add( pair.Value[j].PrimaryKeyID );
                                    }

                                    debugCode = 4500;
                                    if ( sneakCommand.RelatedEntityIDs.Count > 0 )
                                    {
                                        sneakCommand.RelatedString = "AI_T_CHUNKS";
                                        sneakCommand.ToBeQueued = false;
                                        sneakCommand.RelatedIntegers.Add( newTarget.Index );
                                        World_AIW2.Instance.QueueGameCommand( this.AttachedFaction, sneakCommand, playAudioEffectForCommand );
                                        sneakCommand = null;
                                        return DelReturn.Continue;
                                    }
                                }
                            }
                        }
                    }
                    if ( tracing )
                        tracingBuffer.Add( "No fancy behaviours. Find some targets\n" );
                    debugCode = 4600;
                    //We haven't done any of the fancy behaviours, so the default behaviour is "Just fight"
                    //Exception: if the player has anything particularly toothsome on this planet, always kill that first
                    planet.DoForEntities( EntityRollupType.KingUnitsOnly, delegate ( GameEntity_Squad entity )
                    {
                        debugCode = 4610;
                        if ( !entity.PlanetFaction.Faction.GetIsHostileTowards( AttachedFaction ) )
                            return DelReturn.Continue;
                        targetSquads.Add( entity );
                        return DelReturn.Break; ;
                    } );
                    debugCode = 4620;
                    if ( targetSquads.Count == 0 )
                    {
                        debugCode = 4630;
                        if ( planet == null )
                            ArcenDebugging.ArcenDebugLogSingleLine( "??", Verbosity.DoNotShow );
                        //if we already had a target then it's a king, so don't bother
                        planet.DoForEntities( EntityRollupType.AIPOnDeath, delegate ( GameEntity_Squad entity )
                        {
                            debugCode = 4640;
                            if ( !entity.PlanetFaction.Faction.GetIsHostileTowards( AttachedFaction ) )
                                return DelReturn.Continue;

                            targetSquads.Add( entity );
                            return DelReturn.Continue;
                        } );
                    }
                    if ( targetSquads.Count > 0 )
                    {
                        //we know we have a tasty target
                        debugCode = 4700;
                        GameEntity_Squad target = targetSquads[Context.RandomToUse.Next( 0, targetSquads.Count )].GetSquad();
                        if ( target != null )
                        {
                            if ( tracing )
                            {
                                tracingBuffer.Add( "We are attacking " + target.ToStringWithPlanet() + ", one of " + targetSquads.Count + " target(s).\n" );
                                bool debug = false;
                                if ( tracing && debug )
                                {
                                    for ( int i = 0; i < targetSquads.Count; i++ )
                                    {
                                        tracingBuffer.Add( i + ": " + targetSquads[i].ToStringWithPlanet() + ".\n" );
                                    }
                                }
                            }

                            GameCommand attackCommand = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.Attack], GameCommandSource.AnythingElse );
                            attackCommand.RelatedIntegers4.Add( target.PrimaryKeyID );
                            for ( int j = 0; j < pair.Value.Count; j++ )
                            {
                                attackCommand.RelatedEntityIDs.Add( pair.Value[j].PrimaryKeyID );
                            }

                            World_AIW2.Instance.QueueGameCommand( this.AttachedFaction, attackCommand, false );
                            return DelReturn.Continue;
                        }
                    }

                    if ( rand < 25 )
                    {
                        //The AI is allowed to send its fast and cloaked units after your metal generators
                        GameEntity_Squad target = FactionUtilityMethods.Instance.GetRandomMetalGeneratorOnPlanet( planet, Context, WorkingMetalGeneratorList, true );
                        if ( target == null )
                            return DelReturn.Continue;
                        GameCommand attackCommand = null;
                        for ( int j = 0; j < pair.Value.Count; j++ )
                        {
                            GameEntity_Squad entity = pair.Value[j].GetSquad();
                            if ( entity == null )
                                continue;
                            if ( entity.GetMaxCloakingPoints() > 0 ||
                                 entity.CalculateSpeed( true ) >= 1300 )
                            {
                                if ( attackCommand == null )
                                    attackCommand = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.Attack], GameCommandSource.AnythingElse );
                                attackCommand.RelatedEntityIDs.Add( entity.PrimaryKeyID );
                            }
                        }
                        if ( attackCommand != null )
                        {
                            if ( tracing )
                                tracingBuffer.Add( attackCommand.RelatedEntityIDs.Count + " Fast/cloaked units are going after " + target.ToStringWithPlanet() );
                            attackCommand.RelatedIntegers4.Add( target.PrimaryKeyID );
                            World_AIW2.Instance.QueueGameCommand( this.AttachedFaction, attackCommand, false );
                        }
                    }
                    if ( tracing )
                        tracingBuffer.Add( "Very boring; just fight very genericallys\n" );

                    {
                        GameCommand attackCommand = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetBehavior_FromFaction_NoPraetorianGuard], GameCommandSource.AnythingElse );
                        attackCommand.RelatedMagnitude = (int)EntityBehaviorType.Attacker_Full;
                        for ( int j = 0; j < pair.Value.Count; j++ )
                        {
                            attackCommand.RelatedEntityIDs.Add( pair.Value[j].PrimaryKeyID );
                        }
                    }

                    return DelReturn.Continue;
                } );

                GameEntity_Squad.ReleaseTemporarySquadList( targetSquads );

                if ( CastleDefendersLRP.Count > 0 )
                {
                    if ( tracing )
                        tracingBuffer.Add( "We have " + CastleDefendersLRP.Count + " defense ships. AnyCastlesActive: " + BaseInfo.AnyCastlesActive + "\n" );
                    if ( !BaseInfo.AnyCastlesActive )
                    {
                        CastleDefendersRetreatingLRP.Clear();
                        CastleDefendersRetreatingLRP.AddRange( CastleDefendersLRP );
                    }
                    else
                    {
                        //Lets find some enemies to fight!
                        for ( int i = 0; i < CastleDefendersLRP.Count; i++ )
                        {
                            debugCode = 300;
                            GameEntity_Squad entity = CastleDefendersLRP[i].GetSquad();
                            if ( entity == null )
                                continue;
                            TemplarPerUnitBaseInfo data = entity.TryGetExternalBaseInfoAs<TemplarPerUnitBaseInfo>();
                            GameEntity_Squad castle = data.HomeCastle.GetSquad();
                            if ( castle == null )
                                continue; //we will be despawned by the sim code soon
                            if ( entity.HasQueuedOrders() )
                                continue; //we're going someplace, so just go
                            TemplarPerUnitBaseInfo castleData = castle.TryGetExternalBaseInfoAs<TemplarPerUnitBaseInfo>();
                            if ( castleData == null )
                            {
                                //ArcenDebugging.ArcenDebugLogSingleLine("confusingly, we have no castleData for " + castle.ToStringWithPlanet(), Verbosity.DoNotShow );
                                //Unclear why we have this happen sometimes?
                                continue;
                            }
                            Planet planetToHelp = castleData.PlanetCastleWantsToHelp;
                            if ( planetToHelp == null )
                            {
                                CastleDefendersRetreatingLRP.Clear();
                                CastleDefendersRetreatingLRP.Add( entity );
                                continue;
                            }
                            //we have a ship without orders that wants to go to a planet
                            if ( entity.Planet != planetToHelp )
                            {
                                PathBetweenPlanetsForFaction pathCache = PathingHelper.FindPathFreshOrFromCache( AttachedFaction, "TemplarsLRP", entity.Planet, planetToHelp, PathingMode.Default, Context, pathingCacheData );
                                if ( pathCache != null && pathCache.PathToReadOnly.Count > 0 )
                                {
                                    GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetWormholePath_NPCSingleSpecialMission], GameCommandSource.AnythingElse );
                                    debugCode = 1000;
                                    command.RelatedString = "Tmp_ToAttack";
                                    command.RelatedEntityIDs.Add( entity.PrimaryKeyID );
                                    command.ToBeQueued = false;
                                    for ( int k = 0; k < pathCache.PathToReadOnly.Count; k++ )
                                        command.RelatedIntegers.Add( pathCache.PathToReadOnly[k].Index );
                                    World_AIW2.Instance.QueueGameCommand( this.AttachedFaction, command, false );
                                }
                            }
                        }
                    }
                    if ( CastleDefendersRetreatingLRP != null &&
                         CastleDefendersRetreatingLRP.Count > 0 )
                    {
                        if ( tracing )
                            tracingBuffer.Add( "To retreat: " + CastleDefendersRetreatingLRP.Count + " ships\n" );
                        ReturnShipsToCastles( CastleDefendersRetreatingLRP, Context, pathingCacheData );
                    }

                }

            }
            catch ( Exception e )
            {
                ArcenDebugging.ArcenDebugLogSingleLine( "Hit exception in TemplarLogic LRP. debugCode " + debugCode + " " + e.ToString(), Verbosity.ShowAsError );
            }
            finally
            {
                pathingCacheData.ReturnToPool();

                if ( tracing )
                {
                    #region Tracing
                    if ( tracing && !tracingBuffer.GetIsEmpty() ) ArcenDebugging.ArcenDebugLogSingleLine( "TemplarLogic " + AttachedFaction.FactionIndex + ". " + tracingBuffer.ToString(), Verbosity.DoNotShow );
                    if ( tracing )
                    {
                        tracingBuffer.ReturnToPool();
                        tracingBuffer = null;
                    }
                    #endregion
                }
            }
        }
        public void HandleConstructorsLRP( ArcenLongTermIntermittentPlanningContext Context, PerFactionPathCache PathCacheData )
        {
            int debugCode = 0;
            try{
                debugCode = 100;
                for ( int i = 0; i < ConstructorsLRP.Count; i++ )
                {
                    debugCode = 200;
                    GameEntity_Squad entity = ConstructorsLRP[i].GetSquad();
                    if ( entity == null )
                        continue;
                    TemplarPerUnitBaseInfo data = entity.TryGetExternalBaseInfoAs<TemplarPerUnitBaseInfo>();
                    if ( entity.HasQueuedOrders() )
                        continue;
                    Planet destPlanet = World_AIW2.Instance.GetPlanetByIndex( (short)data.PlanetIdx );
                    if ( destPlanet == null )
                        continue; //should only be by race with sim
                    if ( destPlanet != entity.Planet )
                        SendShipToPlanet( entity, destPlanet, Context, PathCacheData );//go to the planet
                    if ( data.LocationToBuild == ArcenPoint.ZeroZeroPoint )
                        continue; //should only be by race with sim
                    SendShipToLocation( entity, data.LocationToBuild, Context );
                }
            } catch( Exception e )
            {
                ArcenDebugging.ArcenDebugLogSingleLine("Hit exception in HandleConstructorsLRP debugCode " + debugCode + " " + e.ToString(), Verbosity.DoNotShow );
            }
        }
        public static readonly DictionaryOfLists<SafeSquadWrapper, SafeSquadWrapper> GoingHome = DictionaryOfLists<SafeSquadWrapper, SafeSquadWrapper>.Create_WillNeverBeGCed( 40, 40, "TemplarMOTFactionDeepInfo-GoingHome" );
        public void ReturnShipsToCastles( List<SafeSquadWrapper> ships, ArcenLongTermIntermittentPlanningContext Context, PerFactionPathCache PathCacheData )
        {
            bool tracing = this.tracing_longTerm = Engine_AIW2.TraceAtAll && Engine_AIW2.TracingFlags.Has( ArcenTracingFlags.Templar );
            ArcenCharacterBuffer tracingBuffer = tracing ? ArcenCharacterBuffer.GetFromPoolOrCreate( "Templar-ReturnShipsToCastles-trace", 10f ) : null;
            int debugCode = 0;
            try{
            debugCode = 100;
            GoingHome.Clear();
            if ( tracing )
                tracingBuffer.Add("We have " + ships.Count + " ships that need to return to castles.\n");

            //This isn't the most efficient code, but it's hopefully run infrequently (only when under attack) and without too many ships
            for ( int i = 0; i < ships.Count; i++ )
            {
                debugCode = 200;
                GameEntity_Squad entity = ships[i].GetSquad();
                if ( entity == null )
                    continue;
                
                TemplarPerUnitBaseInfo data = entity.TryGetExternalBaseInfoAs<TemplarPerUnitBaseInfo>();
                GameEntity_Squad castle = data.HomeCastle.GetSquad();
                if ( castle == null )
                    continue;
                GoingHome.Get( castle ).Add(entity);
            }
            debugCode = 300;
            bool verboseDebug = false;
            GoingHome.DoFor( delegate(KeyValuePair<SafeSquadWrapper, List<SafeSquadWrapper>> pair )
            {
                debugCode = 400;
                if ( tracing && verboseDebug )
                    tracingBuffer.Add("We have " + pair.Value.Count + " ships that need to return to " + pair.Key.ToStringWithPlanet() + ".\n");

                //can be batched with some work if necessary
                for ( int i = 0; i < pair.Value.Count; i++ )
                {
                    debugCode = 500;
                    GameEntity_Squad castle = pair.Key.GetSquad();
                    if ( castle == null )
                        break;
                    GameEntity_Squad entity = pair.Value[i].GetSquad();
                    if ( entity == null )
                        continue;
                    if ( entity.Planet != castle.Planet &&
                         entity.Orders != null && entity.Orders.GetFinalDestinationOrNull() == castle.Planet)
                        continue; //we're already going to the planet we want to

                    if ( entity.Planet == castle.Planet && !entity.HasQueuedOrders() )
                    {
                        GameCommand moveCommand = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.MoveManyToOnePoint_NPCVisitTargetOnPlanet], GameCommandSource.AnythingElse );
                        moveCommand.PlanetOrderWasIssuedFrom = entity.Planet.Index;
                        moveCommand.RelatedPoints.Add( castle.WorldLocation );
                        moveCommand.RelatedEntityIDs.Add( entity.PrimaryKeyID );
                        World_AIW2.Instance.QueueGameCommand( this.AttachedFaction, moveCommand, false );
                    }
                    else
                    {
                        PathBetweenPlanetsForFaction pathCache = PathingHelper.FindPathFreshOrFromCache( AttachedFaction, "TemplarsReturnShipsToCastles", entity.Planet, castle.Planet, PathingMode.Default, Context, PathCacheData );
                        if ( pathCache != null && pathCache.PathToReadOnly.Count > 0 )
                        {
                            GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetWormholePath_NPCSingleSpecialMission], GameCommandSource.AnythingElse );
                            debugCode = 600;
                            command.RelatedString = "Spr_ToCastle";
                            command.RelatedEntityIDs.Add( entity.PrimaryKeyID );
                            command.ToBeQueued = false;
                            for ( int k = 0; k < pathCache.PathToReadOnly.Count; k++ )
                                command.RelatedIntegers.Add( pathCache.PathToReadOnly[k].Index );
                            World_AIW2.Instance.QueueGameCommand( this.AttachedFaction, command, false );
                        }
                    }
                }
                return DelReturn.Continue;
            } );
            debugCode = 700;
            }catch( Exception e )
            {
                ArcenDebugging.ArcenDebugLogSingleLine("Hit exception in ReturnShipsToCastles debugCode " + debugCode + " " + e.ToString(), Verbosity.DoNotShow );
            }
            #region Tracing
            if ( tracing && !tracingBuffer.GetIsEmpty() ) ArcenDebugging.ArcenDebugLogSingleLine( tracingBuffer.ToString(), Verbosity.DoNotShow );
            if ( tracing )
            {
                tracingBuffer.ReturnToPool();
                tracingBuffer = null;
            }
            #endregion
        }
        public void SendShipToPlanet( GameEntity_Squad entity, Planet destination, ArcenLongTermIntermittentPlanningContext Context, PerFactionPathCache PathCacheData )
        {
            PathBetweenPlanetsForFaction pathCache = PathingHelper.FindPathFreshOrFromCache( entity.PlanetFaction.Faction, "TemplarsSendShipToPlanet", entity.Planet, destination, PathingMode.Safest, Context, PathCacheData );
            if ( pathCache != null && pathCache.PathToReadOnly.Count > 0 )
            {
                GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetWormholePath_NPCSingleUnit], GameCommandSource.AnythingElse );
                command.RelatedString = "Templar_Dest";
                command.RelatedEntityIDs.Add( entity.PrimaryKeyID );
                for ( int k = 0; k < pathCache.PathToReadOnly.Count; k++ )
                    command.RelatedIntegers.Add( pathCache.PathToReadOnly[k].Index );
                World_AIW2.Instance.QueueGameCommand( this.AttachedFaction, command, false );
            }
        }
        public void SendShipToLocation( GameEntity_Squad entity, ArcenPoint dest, ArcenLongTermIntermittentPlanningContext Context )
        {
            GameCommand moveCommand = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.MoveManyToOnePoint_NPCVisitTargetOnPlanet], GameCommandSource.AnythingElse );
            moveCommand.PlanetOrderWasIssuedFrom = entity.Planet.Index;
            moveCommand.RelatedPoints.Add( dest );
            moveCommand.RelatedEntityIDs.Add( entity.PrimaryKeyID );
            World_AIW2.Instance.QueueGameCommand( this.AttachedFaction, moveCommand, false );
        }
        public bool IsPreferredTarget(Planet planet, ArcenLongTermIntermittentPlanningContext Context)
        {
            //Whether this is an enemy controlled planet. Note that we try not to overkill planets too badly
            bool tracing = this.tracing_longTerm = Engine_AIW2.TraceAtAll && Engine_AIW2.TracingFlags.Has( ArcenTracingFlags.Templar );
            ArcenCharacterBuffer tracingBuffer = tracing ? ArcenCharacterBuffer.GetFromPoolOrCreate( "Templar-IsPreferredTarget-trace", 10f ) : null;
            bool debug = false;
            if ( tracing && debug )
                tracingBuffer.Add("\tchecking " + planet.Name + " for preferredness\n");
            Faction controllingFaction = planet.GetControllingFaction();
            bool foundNecro = false;
            planet.DoForEntities( "Necropolis", delegate (GameEntity_Squad necro )
            {
                if ( necro != null )
                {
                    foundNecro = true;
                    return DelReturn.Break;
                }
                return DelReturn.Continue;
            } );

            bool playerOwned = controllingFaction.Type == FactionType.Player || foundNecro;
            if ( !playerOwned )
            {
                if ( tracing && debug )
                    tracingBuffer.Add("\t\tNot owned by primary enemy (ie player). Owned by " + controllingFaction.GetDisplayName() +"\n");
                #region Tracing
                if ( tracing && !tracingBuffer.GetIsEmpty() ) ArcenDebugging.ArcenDebugLogSingleLine( tracingBuffer.ToString(), Verbosity.DoNotShow );
                if ( tracing )
                {
                    tracingBuffer.ReturnToPool();
                    tracingBuffer = null;
                }
                #endregion
                return false;
            }

            if ( !controllingFaction.GetIsHostileTowards(AttachedFaction) && !foundNecro )
            {
                if ( tracing && debug )
                    tracingBuffer.Add("\t\t" + controllingFaction.GetDisplayName() + " is friendly\n");
                #region Tracing
                if ( tracing && !tracingBuffer.GetIsEmpty() ) ArcenDebugging.ArcenDebugLogSingleLine( tracingBuffer.ToString(), Verbosity.DoNotShow );
                if ( tracing )
                {
                    tracingBuffer.ReturnToPool();
                    tracingBuffer = null;
                }
                #endregion
                return false;
            }
            if ( ShouldTemplarAvoidPlanet( planet ))
            {
                if ( tracing && debug )
                    tracingBuffer.Add("\t\t" + controllingFaction.GetDisplayName() + " is avoided by the templar\n");
                #region Tracing
                if ( tracing && !tracingBuffer.GetIsEmpty() ) ArcenDebugging.ArcenDebugLogSingleLine( tracingBuffer.ToString(), Verbosity.DoNotShow );
                if ( tracing )
                {
                    tracingBuffer.ReturnToPool();
                    tracingBuffer = null;
                }
                #endregion
                return false;
            }
            var factionData = planet.GetStanceDataForFaction( AttachedFaction );
            int enemyStrength = factionData[FactionStance.Hostile].TotalStrength;
            int friendlyStrength = factionData[FactionStance.Friendly].TotalStrength + factionData[FactionStance.Self].TotalStrength ;
            if ( enemyStrength * 5 < friendlyStrength )
            {
                if ( tracing && debug )
                    tracingBuffer.Add("\t\t" + enemyStrength + " < " + (friendlyStrength * 5)+ ", so discard\n");
                #region Tracing
                if ( tracing && !tracingBuffer.GetIsEmpty() ) ArcenDebugging.ArcenDebugLogSingleLine( tracingBuffer.ToString(), Verbosity.DoNotShow );
                if ( tracing )
                {
                    tracingBuffer.ReturnToPool();
                    tracingBuffer = null;
                }
                #endregion
                #region Tracing
                if ( tracing && !tracingBuffer.GetIsEmpty() ) ArcenDebugging.ArcenDebugLogSingleLine( tracingBuffer.ToString(), Verbosity.DoNotShow );
                if ( tracing )
                {
                    tracingBuffer.ReturnToPool();
                    tracingBuffer = null;
                }
                #endregion
                return false; //if we outnumber 7 to 1, don't bother attacking
            }
            return true;
        }
        public bool IsFallbackTarget(Planet planet, ArcenLongTermIntermittentPlanningContext Context)
        {
            bool tracing = this.tracing_longTerm = Engine_AIW2.TraceAtAll && Engine_AIW2.TracingFlags.Has( ArcenTracingFlags.Templar );
            ArcenCharacterBuffer tracingBuffer = tracing ? ArcenCharacterBuffer.GetFromPoolOrCreate( "Templar-IsFallbackTarget-trace", 10f ) : null;
            Faction controllingFaction = planet.GetControllingOrInfluencingFaction();

            if ( !controllingFaction.GetIsHostileTowards(AttachedFaction) )
            {
                if ( tracing )
                {
                    tracingBuffer.ReturnToPool();
                    tracingBuffer = null;
                }
                return false;
            }
            if ( ShouldTemplarAvoidPlanet( planet ))
            {
                //just avoid these factions
                if ( tracing )
                {
                    tracingBuffer.ReturnToPool();
                    tracingBuffer = null;
                }
                return false;
            }
            var factionData = planet.GetStanceDataForFaction( AttachedFaction );
            int enemyStrength = factionData[FactionStance.Hostile].TotalStrength;
            int friendlyStrength = factionData[FactionStance.Friendly].TotalStrength + factionData[FactionStance.Self].TotalStrength ;
            #region Tracing
            if ( tracing && !tracingBuffer.GetIsEmpty() ) ArcenDebugging.ArcenDebugLogSingleLine( tracingBuffer.ToString(), Verbosity.DoNotShow );
            if ( tracing )
            {
                tracingBuffer.ReturnToPool();
                tracingBuffer = null;
            }
            #endregion

            if ( enemyStrength < friendlyStrength * 7 )
            {
                return false; //if we outnumber 7 to 1, don't bother attacking
            }

            return true;
        }
        public bool ShouldTemplarAvoidPlanet( Planet planet)
        {
            Faction controllingFaction = planet.GetControllingOrInfluencingFaction();
            if ( controllingFaction.SpecialFactionData.TemplarAvoidsMe )
                return true;
            return false;
        }
        public int CalculateSpeed(List<SafeSquadWrapper> ships, ArcenLongTermIntermittentPlanningContext Context)
        {
            //Return the speed we want these ships to use. It's "a bit faster than the average speed, and at least 500".
            //We make the speeds all a little bit different to make it less obvious to the player that we are using speed groups (since
            //ships from multiple planets will be going by at the same time, and moving at different speeds)
            int debugCode = 0;
            try{
                debugCode = 100;
                if ( ships == null || ships.Count == 0 )
                    return 0;
                int maxSpeed = 0;
                Int64 totalSpeed = 0;
                debugCode = 200;
                for ( int i = 0; i < ships.Count; i++ )
                {
                    debugCode = 300;
                    GameEntity_Squad squad = ships[i].GetSquad();
                    if ( squad == null )
                        continue;
                    debugCode = 400;
                    int newSpeed = squad.DataForMark.Speed;
                    totalSpeed += newSpeed;
                    if ( maxSpeed < newSpeed )
                        maxSpeed = newSpeed;
                }
                debugCode = 500;
                int average = (Int32)( totalSpeed / (Int64)ships.Count );

                average += average / 9; //a bit faster than average
                if ( average < 500 )
                    average = 500;
                debugCode = 600;
                average += Context.RandomToUse.Next(0, 50); //a little more randomness

                //if we only have one ship type, then they just go the speed the go, for example
                if ( average > maxSpeed )
                    average = maxSpeed;

                return average;
            }catch ( Exception e )
            {
                ArcenDebugging.ArcenDebugLogSingleLine("Hit exception in TemplarMOTFactionDeepInfo::CalculateSpeed debugCode " + debugCode + " " + e.ToString(), Verbosity.DoNotShow );
            }
            return 0;
        }
        public void AttackTargetPlanet(Planet start, Planet destination, List<SafeSquadWrapper> ships, ArcenLongTermIntermittentPlanningContext Context, PerFactionPathCache PathCacheData )
        {
            if ( ships.Count <= 0 )
                return;
            int debugCode = 0;
            try{
                bool tracing = this.tracing_longTerm = Engine_AIW2.TraceAtAll && Engine_AIW2.TracingFlags.Has( ArcenTracingFlags.Templar );
                ArcenCharacterBuffer tracingBuffer = tracing ? ArcenCharacterBuffer.GetFromPoolOrCreate( "Templar-AttackTargetPlanet-trace", 10f ) : null;
                debugCode = 100;
                PathBetweenPlanetsForFaction pathCache = PathingHelper.FindPathFreshOrFromCache( AttachedFaction, "TemplarsAttackTargetPlanet", start, destination, PathingMode.Safest, Context, PathCacheData );
                int gatheringDistance = -1;
                debugCode = 200;
                if ( pathCache != null && pathCache.PathToReadOnly.Count > 0 )
                {
                    debugCode = 400;
                    if ( tracing )
                        tracingBuffer.Add("\tpath count " + pathCache.PathToReadOnly.Count + " and gatheringDistance " + gatheringDistance );
                    debugCode = 500;
                    if ( pathCache.PathToReadOnly.Count > gatheringDistance || gatheringDistance == -1)
                    {
                        debugCode = 600;
                        //if we are coming from a long ways away, fly a little faster so the Templar are less spread out
                        int speedToUse = CalculateSpeed(ships, Context);
                        GameCommand speedCommand = GameCommand.Create ( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.CreateSpeedGroup_FireteamAttack],  GameCommandSource.AnythingElse);
                        for (int j = 0; j < ships.Count; j++)
                            speedCommand.RelatedEntityIDs.Add(ships[j].PrimaryKeyID);
                        int exoGroupSpeed = speedToUse;
                        speedCommand.RelatedBool = true;
                        speedCommand.RelatedIntegers.Add(exoGroupSpeed);
                        World_AIW2.Instance.QueueGameCommand( this.AttachedFaction, speedCommand, false );
                    }
                    debugCode = 700;
                    GameCommand command = GameCommand.Create ( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.SetWormholePath_UtilRaidSpecific], GameCommandSource.AnythingElse );
                    command.RelatedString = "Templar_Planetary_Movement";
                    for ( int k = 0; k < ships.Count; k++ )
                        command.RelatedEntityIDs.Add( ships[k].PrimaryKeyID );
                    //determine whether we are actually going to the target, or just getting close so we are ready to strike
                    debugCode = 800;
                    if ( gatheringDistance == -1 )
                    {
                        debugCode = 900;
                        if ( tracing )
                            tracingBuffer.Add("\tJust attack the planet you are on now, please\n");

                        for ( int k = 0; k < pathCache.PathToReadOnly.Count; k++ )
                            command.RelatedIntegers.Add( pathCache.PathToReadOnly[k].Index );
                    }
                    else
                    {
                        debugCode = 1000;
                        int hopsForwardToMove = pathCache.PathToReadOnly.Count - gatheringDistance;
                        if ( hopsForwardToMove >= pathCache.PathToReadOnly.Count || hopsForwardToMove < 0 )
                            throw new Exception("huh?");
                        if ( tracing )
                            tracingBuffer.Add("\tMove forward only " + hopsForwardToMove + " hops from " + start.Name + " -> " + pathCache.PathToReadOnly[hopsForwardToMove - 1].Name + " en route to " + destination.Name + "\n");
                        debugCode = 1100;
                        for ( int k = 0; k < hopsForwardToMove; k++ )
                            command.RelatedIntegers.Add( pathCache.PathToReadOnly[k].Index );
                    }

                    World_AIW2.Instance.QueueGameCommand( this.AttachedFaction, command, false );
                }
                #region Tracing
                if ( tracing && !tracingBuffer.GetIsEmpty() ) ArcenDebugging.ArcenDebugLogSingleLine( tracingBuffer.ToString(), Verbosity.DoNotShow );
                if ( tracing )
                {
                    tracingBuffer.ReturnToPool();
                    tracingBuffer = null;
                }
                #endregion

            } catch(Exception e)
            {
                ArcenDebugging.ArcenDebugLogSingleLine("Hit exception in Templar AttackTargetPlanet debugCode " + debugCode + " " + e.ToString() , Verbosity.ShowAsError );
            }
        }
    }
}
