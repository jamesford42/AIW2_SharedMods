using Arcen.AIW2.Core;
using Arcen.Universal;
using System;
using System.Text;
using Arcen.AIW2.External;
using Ext = Arcen.AIW2.External;



namespace TemplarMOT
{
    public class TemplarMOTDescAppender : GameEntityDescriptionAppenderBase
    {
        public override void AddToDescriptionBuffer(GameEntity_Squad RelatedEntityOrNull, GameEntityTypeData RelatedEntityTypeData, ArcenCharacterBufferBase Buffer)
        {
            try
            {
                bool debug = GameSettings.Current.GetBoolBySetting("Debug_Tooltip") || (Engine_AIW2.TraceAtAll && Engine_AIW2.TracingFlags.Has(ArcenTracingFlags.Templar));
                if (RelatedEntityTypeData == null)
                {
                    ArcenDebugging.ArcenDebugLogSingleLine("No type data?", Verbosity.DoNotShow);
                    return;
                }

                TemplarPerUnitBaseInfo data = RelatedEntityOrNull.TryGetExternalBaseInfoAs<TemplarPerUnitBaseInfo>();
                if (data == null)
                {
                    //this is valid, if the AI has templar units (which is allowed)
                    return;
                }
                Faction faction = RelatedEntityOrNull.GetFactionOrNull_Safe();
                TemplarMOTFactionBaseInfo globaldata = faction.TryGetExternalBaseInfoAs<TemplarMOTFactionBaseInfo>();
                if (RelatedEntityTypeData.GetHasTag("TemplarRift"))
                {
                    //Buffer.Add( "This structure is a rift!. " );
                    return;
                }
                if (RelatedEntityTypeData.GetHasTag("TemplarPrimaryDefensiveStructure"))
                {
                    if (data.TimeTillmarkUp > 0)
                    {
                        string color = ArcenExternalUIUtilities.GetColorForNomadMoveTime(data.TimeTillmarkUp);
                        Buffer.Add("This structure will mark up in ").Add(data.TimeTillmarkUp.ToString(), color).Add(". ");
                    }
                    if (data.TimeTillSpawnNextConstructor > 0)
                    {
                        string color = ArcenExternalUIUtilities.GetColorForNomadMoveTime(data.TimeTillSpawnNextConstructor);
                        Buffer.Add("This structure is able to build new fortifications in ").Add(data.TimeTillSpawnNextConstructor.ToString(), color).Add(". ");
                    }
                    if (debug)
                        Buffer.Add("This structure's available metal is ").Add(data.MetalStored.ToString(), "a1a1ff").Add(". ");
                    Buffer.Add("This ").Add(RelatedEntityTypeData.GetDisplayName()).Add(" has the following inside: ");
                    Buffer.Add(data.ShipsInside_ForUI);

                    if (data.PlanetCastleWantsToHelp != null)
                        Buffer.Add("This unit would like to help defend ").Add(data.PlanetCastleWantsToHelp.Name, "a1ffa1").Add(". ");
                }
                if (RelatedEntityTypeData.GetHasTag("TemplarWaveLeader") || RelatedEntityTypeData.GetHasTag("TemplarPrimaryDefensiveStructure"))
                {
                    Buffer.Add("\n\n");
                    if (globaldata.AStatus == TemplarMOTFactionBaseInfo.ActivityStatus.Scrambling)
                    {
                        Buffer.Add("The Templar are ").Add("Scrambling", "00ff00").Add(". They are producing, based on ").Add("AIP", "ff0000").Add(" in an attempt to stop your expansion. Eventually if things remain stagnant long enough, they will start Organising.");
                    }
                    else if (globaldata.AStatus == TemplarMOTFactionBaseInfo.ActivityStatus.Organising)
                    {
                        Buffer.Add("The Templar are ").Add("Organising", "ffff00").Add(". They feel comfortable with your rate of expansion, however, if left Organising too long, they will start crusading.");

                    }
                    else
                    {
                        Buffer.Add("The Templar are ").Add("Crusading", "ff0000").Add(". They are quickly ramping up production as they are ready to destroy you! (").StartColor("ff0000").Add(1+globaldata.CrusadeMult).Add("x").EndColor().Add(") Increase AIP to intimidate them into rethinking their plans.");
                    }
                    if (globaldata.CrusadeMult > 1.0f && globaldata.AStatus != TemplarMOTFactionBaseInfo.ActivityStatus.Crusading)
                    {
                        Buffer.Add("\nThe Templar were recently on a crusade and are cooling off, this multiplier is inactive and decreasing, but if they were to begin again, it would be at ").StartColor("00aa00").Add(globaldata.CrusadeMult).Add("x").EndColor().Add("\n");
                    }
                }

                if (data.DefenseMode && RelatedEntityTypeData.IsMobileCombatant)
                {
                    GameEntity_Squad castle = data.HomeCastle.GetSquad();
                    if (castle != null)
                    {
                        Buffer.Add("This ship is dispatched from the ").Add(castle.TypeData.GetDisplayName(), "a1ffa1").Add(" on ").Add(castle.Planet.Name, "ffa1a1");
                        TemplarPerUnitBaseInfo castleData = castle.TryGetExternalBaseInfoAs<TemplarPerUnitBaseInfo>();
                        Planet defensePlanet = null;
                        if (castleData != null)
                            defensePlanet = castleData.PlanetCastleWantsToHelp;
                        if (defensePlanet != null)
                            Buffer.Add(" to defend ").Add(defensePlanet.Name, "ffa1a1").Add(". ");
                        else
                            Buffer.Add(". ");

                    }
                }
                if (RelatedEntityTypeData.GetHasTag("TemplarConstructor"))
                {
                    Planet destPlanet = World_AIW2.Instance.GetPlanetByIndex((short)data.PlanetIdx);
                    if (destPlanet != null && data.UnitToBuild != null)
                    {
                        Buffer.Add("This constructor will build a ").Add(data.UnitToBuild.GetDisplayName(), "a1ffa1").Add(" on ").Add(destPlanet.Name, "a1a1ff").Add("\n");
                    }
                    else
                    {
                        Buffer.Add("This constructor has no build target?!\n");
                    }
                }
                else if (RelatedEntityTypeData.IsCombatant && RelatedEntityTypeData.IsMobileCombatant)
                    Buffer.Add("This ship is dispatched to attack the players. ");

                if (data.StrengthRalliedToWave > 0 && debug)
                {
                    Buffer.Add("This ship has rallied ").Add(data.StrengthRalliedToWave.ToString(), "a1ffa1").Add(" strength.");
                }
            }
            catch (Exception) { }

            return;
        }
    }
}
