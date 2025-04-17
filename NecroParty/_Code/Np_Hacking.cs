using Arcen.AIW2.Core;
using System;

using System.Text;
using Arcen.Universal;
using Arcen.AIW2.External;

namespace NecroParty
{
    #region Unused
    #if false
    #region Hacking_ClaimAmplifier
    public class Hacking_ClaimAmplifier : BaseHackingImplementation
    {
        public override string GetDynamicDescription( GameEntity_Squad target, GameEntity_Squad hackerOrNull, Planet planet, Faction hackerFaction, HackingType hackingType )
        {
            //return "\nIn particular, this hack will upgrade <color=#a1ffa1>" + target.FleetMembership.Fleet.GetName() + "</color> to mark level " + (target.CurrentMarkLevel + 1) + ".";
            return "";
        }
        public override Hackable GetCanBeHacked( GameEntity_Squad Target, GameEntity_Squad HackerOrNull, Planet planet, Faction HackerFaction, HackingType Type, string RelatedStringOrNull, int RelatedIntOrNull, out string RejectionReasonDescription )
        {
            if ( Target.PlanetFaction.Faction == HackerFaction )
            {
                RejectionReasonDescription = "You have already claimed this structure";
                return Hackable.NeverCanBeHacked_Hide;
            }
            return base.GetCanBeHacked( Target, HackerOrNull, planet, HackerFaction, Type, RelatedStringOrNull, RelatedIntOrNull, out RejectionReasonDescription );
        }
        public override bool DoSuccessfulCompletionLogic_Extra( GameEntity_Squad Target, Planet planet, GameEntity_Squad Hacker, ArcenHostOnlySimContext Context, HackingType type, HackingEvent Event ){
            NecromancerEmpireFactionBaseInfo baseInfo = Hacker.PlanetFaction.Faction.GetExternalBaseInfoAs<NecromancerEmpireFactionBaseInfo>();
            GameEntity_Squad necropolis = null;
            baseInfo.Necropoleis.Display_DoFor( delegate ( GameEntity_Squad city )
            {
                if ( city.Planet == Target.Planet )
                {
                    necropolis = city;
                    return DelReturn.Break;
                }
                return DelReturn.Continue;
            } );
            if ( necropolis != null )
            {
                GameEntity_Squad amplifier = GameEntity_Squad.CreateNew_ReturnNullIfMPClient( necropolis.PlanetFaction, Target.TypeData, 1,
                                                                                              necropolis.FleetMembership.Fleet, 0, Target.WorldLocation, Context, "Necromancer-HackedAmplifier" );
                Target.Despawn( Context, true, InstancedRendererDeactivationReason.TransformedIntoAnotherEntityType );
            }
            else
                Target.PlanetFaction.SwitchToFaction( Target, Hacker.PlanetFaction, false, "Amplifier Was Hacked" );

            return true;
        }
    }
    #endregion

    #region Hacking_TransformNecromancerFlagship
    public class Hacking_TransformNecromancerFlagship : BaseHackingImplementation
    {
        public override string GetDynamicDescription( GameEntity_Squad target, GameEntity_Squad hackerOrNull, Planet planet, Faction hackerFaction, HackingType hackingType )
        {
            return "This hack will transform the flagship of <color=#a1a1a1>" + target.FleetMembership.Fleet.GetName() + "</color> (" + target.TypeData.GetDisplayName() + ")";
        }

        public override void GetMinAndMaxCostToHackForSidebar( GameEntity_Squad Target, Planet planet, Faction hackerFaction, HackingType Type, out FInt MinCost, out FInt MaxCost )
        {
            if (!NecromancerEmpireFactionBaseInfo.GetIsThisANecromancerFaction(Target.PlanetFaction.Faction))
            {
                //Only the necromancer can see these hacks
                MinCost = FInt.Zero;
                MaxCost = FInt.Zero;
                return;
            }

            NecromancerEmpireFactionBaseInfo baseInfo = Target.PlanetFaction.Faction.TryGetExternalBaseInfoAs<NecromancerEmpireFactionBaseInfo>();
            List<NecromancerUpgrade> workingBlueprints = baseInfo.AvailableBlueprints.GetDisplayList();

            if (workingBlueprints.Count == 0) {
                MinCost = FInt.Zero;
                MaxCost = FInt.Zero;
                return;
            }

            MaxCost = FInt.Zero;
            MinCost = (FInt)999;
            foreach (NecromancerUpgrade upgrade in workingBlueprints) {
                if ( upgrade.BlueprintTransformCostInHacking > 0 ) {
                    if ( upgrade.BlueprintTransformCostInHacking < MinCost )
                        MinCost = upgrade.BlueprintTransformCostInHacking;
                    if ( upgrade.BlueprintTransformCostInHacking > MaxCost )
                        MaxCost = upgrade.BlueprintTransformCostInHacking;
                }
            }
        }

        public override Hackable GetCanBeHacked( GameEntity_Squad Target, GameEntity_Squad HackerOrNull, Planet planet, Faction HackerFaction, HackingType Type, string RelatedStringOrNull, int RelatedIntOrNull, out string RejectionReasonDescription )
        {
            if ( HackerOrNull == null )
            {
                RejectionReasonDescription = "There are no units that can hack on this planet";
                return Hackable.NeverCanBeHacked_Hide;
            }

            if ( !NecromancerEmpireFactionBaseInfo.GetIsThisANecromancerFaction( HackerOrNull.PlanetFaction.Faction ) )
            {
                //Only the necromancer can see these hacks
                RejectionReasonDescription = "Only the necromancer can hack";
                return Hackable.NeverCanBeHacked_Hide;
            }

            NecromancerEmpireFactionBaseInfo gData = HackerOrNull.PlanetFaction.Faction.TryGetExternalBaseInfoAs<NecromancerEmpireFactionBaseInfo>();
            int count = gData.AvailableBlueprints.GetDisplayList().Count;

            if ( count <= 0 )
            {
                //Hide until you have some blueprints
                RejectionReasonDescription = "No blueprints!";
                return Hackable.NeverCanBeHacked_Hide;
            }

            if ( Target.PlanetFaction.DataByStance[FactionStance.Hostile].TotalStrength > 500 )
            {
                RejectionReasonDescription = "You cannot transform on a planet with significant enemy strength.";
                return Hackable.NeverBeHacked_ButStillShow;
            }
            RejectionReasonDescription = "";
            return Hackable.CanBeHacked;
        }
        public override bool GetDoesHackRequireASinglularChoiceFromASubmenu()
        {
            return true;
        }
        public override void DoOneSecondOfHackingLogic_HackSpecificLogic( GameEntity_Squad Target, Planet planet, GameEntity_Squad Hacker, ArcenHostOnlySimContext Context, HackingType type, HackingEvent Event )
        {
            // Handle hacking reaction manually, forcing the faction to be an ai faction.
            int secondsSoFar = Hacker.ActiveHack_DurationThusFar;
            if ( secondsSoFar == 0 )
                return;

            Faction targetFaction = Target.PlanetFaction.Faction;
            if ( targetFaction == null ) // Shouldn't be able to occur, but you never know.
                return;
            if ( Hacker.ActiveHack_DurationThusFar % type.GetPrimaryHackResponseInterval() == 0 )
                targetFaction.Safe_DeepInfo_ReactToHacking_AsPartOfMainSim_HostOnly( Target, FInt.One, Context, Event, targetFaction );
        }

        private ArcenCachedExternalTypeDirect type_bCustomHackingOptionButton = ArcenExternalTypeManager.GetOrCreateTypeDirect( typeof( bCustomHackingOptionButton ) );
        private static readonly List<NecromancerUpgrade> listOfItemsLastUsedToPopulateCustomButton = List<NecromancerUpgrade>.Create_WillNeverBeGCed( 60, "Hacking_TransformNecromancerFlagship-listOfItemsLastUsedToPopulateCustomButton" );
        private static Hacking_TransformNecromancerFlagship Implementation = null;
        private static HackCustomButtonListInfo Info = new HackCustomButtonListInfo();
        public override void AddAllButtonsForSingularChoiceInSubMenu( GameEntity_Squad TargetIfShip, Planet TargetIfPlanet, Faction HackerFaction, HackingType HackType, ArcenUI_SetOfCreateElementDirectives Set,                                                                                                                                                            ref float runningY, UnityEngine.Rect firstBounds, float rowBuffer, AddHackButtonToWindow WindowAdder )
        {
            Implementation = this;
            //TODO: make sure we can add blueprints for the NecromancerPerUnitData
            //then show a list of the available blueprints for transformation
            //once we pick then we turn our flagship into a TransformingFlagship
            Info.Update( TargetIfShip, TargetIfPlanet, HackerFaction, HackType );
            listOfItemsLastUsedToPopulateCustomButton.Clear();
            NecromancerEmpireFactionBaseInfo gData = HackerFaction.TryGetExternalBaseInfoAs<NecromancerEmpireFactionBaseInfo>();

            List<NecromancerUpgrade> workingBlueprints = gData.AvailableBlueprints.GetDisplayList();

            for ( int i = 0; i < workingBlueprints.Count; i++ )
            {
                listOfItemsLastUsedToPopulateCustomButton.Add( workingBlueprints[i] );
                //this part is the same for any hack
                HackingUtils.PopulateOneCustomHackingOptionButton( Set, ref runningY, ref firstBounds, rowBuffer, WindowAdder,
                                                                   //this part differs for each hack type, telling it how to tell each option apart
                                                                   string.Empty, i, i,
                                                                   //and this part is also different for each hack type, pointing to its own unique button implementation.
                                                                   //they can all be called bCustomHackingOptionButton, since they are a subclass of the hacking implementation class.
                                                                   type_bCustomHackingOptionButton );
            }
        }

        public override bool DoSuccessfulCompletionLogic_Extra( GameEntity_Squad Target, Planet planet, GameEntity_Squad Hacker, ArcenHostOnlySimContext Context, HackingType type, HackingEvent Event )
        {
            int debugCode = 0;
            try
            {
                debugCode = 100;
                Faction facOrNull = Hacker.GetFactionOrNull_Safe();
                NecromancerEmpireFactionBaseInfo gData = facOrNull.TryGetExternalBaseInfoAs<NecromancerEmpireFactionBaseInfo>();
                List<NecromancerUpgrade> workingBlueprints = gData.AvailableBlueprints.GetDisplayList();
                NecromancerUpgrade upgrade = null;
                for ( int i = 0; i < workingBlueprints.Count; i++ )
                {
                    if ( Event.RelatedStringOrNull != null && Event.RelatedStringOrNull.Length > 0 )
                    {
                        if ( workingBlueprints[i].InternalName == Event.RelatedStringOrNull )
                        {
                            upgrade = workingBlueprints[i];
                            break;
                        }
                    }
                }
                if ( upgrade == null )
                {
                    throw new Exception("Could not find upgrade to grant! We were looking for " + Event.RelatedStringOrNull + ".");
                }
                //ArcenDebugging.ArcenDebugLogSingleLine("Transforming " + Target.TypeData.GetDisplayName() + " of " + Target.FleetMembership.Fleet.GetName() + " into " + upgrade.RelatedShip.GetDisplayName() + " hacker " + Hacker.FleetMembership.Fleet.GetName(), Verbosity.DoNotShow );
                Target.TransformInto( Context, upgrade.RelatedShip, 1, true );
                return true;
            }
            catch ( Exception e )
            {
                if ( ArcenNetworkAuthority.DesiredStatus != DesiredMultiplayerStatus.Client )
                    ArcenDebugging.ArcenDebugLogSingleLine( "Exception hit during Hacking_TransformNecromancerFlagship DoSuccessfulCompletionLogic_Extra debug code " + debugCode + " " + e.ToString(), Verbosity.ShowAsError );
                return false;
            }
        }
        
        #region bCustomHackingOptionButton
        public class bCustomHackingOptionButton : ButtonAbstractBase
        {
           #region GetTechUpgradeFromElement
           public static NecromancerUpgrade GetNecromancerUpgradeFromElement( ArcenUI_Element element )
           {
               if ( element == null || listOfItemsLastUsedToPopulateCustomButton == null )
                   return null;
               if ( element.CreatedByCodeDirective == null )
                   return null;
               int index = element.CreatedByCodeDirective.Identifier.CodeDirectiveTag1;
               if ( index < 0 || index >= listOfItemsLastUsedToPopulateCustomButton.Count )
                   return null;
               return listOfItemsLastUsedToPopulateCustomButton[index];
           }
           #endregion

            public override void GetTextToShowFromVolatile( ArcenDoubleCharacterBuffer Buffer )
            {
                NecromancerUpgrade upgrade = GetNecromancerUpgradeFromElement( this.Element );
                if ( upgrade == null )
                {
                    Buffer.Add( "<color=#c74639>Null NecromancerUpgrade</color>" );
                    return;
                }

                if ( !this.GetCanHackForThisItem( upgrade ) )
                {
                    Buffer.Add( "<color=#c74639>Not Possible:</color> " );
                }
                
                GameEntityTypeData typeData = upgrade.RelatedShip;
                if ( typeData == null || typeData.TexEmbedSprite_Icon == null )
                    typeData = upgrade.ShipForCapIncrease;

                Faction localFaction = World_AIW2.Instance.GetPlayerFactionForUIOrNull();
                Buffer.AddShipIconInline(typeData, localFaction );
                Buffer.Add( upgrade.RelatedShip.GetDisplayName() );

                int essCost = upgrade.BlueprintTransformCostInResourceOne.IntValue;
                int hapCost = upgrade.BlueprintTransformCostInHacking.IntValue;
                if (essCost > 0 || hapCost > 0)
                {
                    Buffer.Add(" ");
                    if (essCost > 0)
                        Buffer.AddResourceOne(upgrade.BlueprintTransformCostInResourceOne.IntValue, true);
                    if (hapCost > 0)
                        Buffer.AddHacking(upgrade.BlueprintTransformCostInHacking.IntValue, true);    
                }
            }

            public override MouseHandlingResult HandleClick_Subclass( MouseHandlingInput input )
            {
                NecromancerUpgrade upgrade = GetNecromancerUpgradeFromElement( this.Element );
                if ( upgrade == null )
                    return MouseHandlingResult.PlayClickDeniedSound;

                Faction localFaction = World_AIW2.Instance.GetLocalPlayerFactionOrNull();
                if ( localFaction == null )
                    return MouseHandlingResult.PlayClickDeniedSound;
                if ( !this.GetCanHackForThisItem( upgrade ) )
                    return MouseHandlingResult.PlayClickDeniedSound;
                // #region instead of normal click behavior, show details
                // if ( InputCaching.CalculateHoldAndClickToViewDetailsOfContents() )
                // {
                //     int upgradesSoFar = localFaction.TechUnlocks[upgrade.RowIndexNonSim] + localFaction.FreeTechUnlocks[upgrade.RowIndexNonSim];
                //     Window_InGameSidebarScience.btnScienceTech.ShowDetailsOfATechUpgradeContents( upgrade, 0, -1, upgradesSoFar );
                //     return MouseHandlingResult.None;    // }
                // #endregion

                // if ( !this.GetCanHackForThisItem( upgrade ) )
                //     return MouseHandlingResult.PlayClickDeniedSound;

                 bool doAreYouSurePrompt = GameSettings.Current.GetBoolBySetting( "UpgradeShipPrompt" ) && !InputCaching.CalculateHoldAndSuppressTechUpgradePrompt();
                 if ( doAreYouSurePrompt )
                 {
                    GameEntity_Squad hacker = HackingUtils.GetPreferredHacker( Info.TargetShip, Info.HackingType, Info.TargetPlanet, true );
                    if ( Engine_Universal.CurrentPopups.Count > 0 ) //we got some sort of warning telling us we can't do this
                        return MouseHandlingResult.PlayClickDeniedSound;
                    ModalPopupData.CreateAndLogYesNoStyle( DoHack, null, "Are you sure", "Are you sure you would like to do the hack " + Info.HackingType.DisplayName +
                         " for " + upgrade.DisplayName + "?\n \n" + "<color=#888888>To disable this prompt, go into Game Settings, under the Game tab, and toggle this to OFF.  Or just hold down " +
                         InputActionTypeDataTable.GetActionByName_FairlySlow( "SuppressTechUpgradePrompt" ).GetHumanReadableKeyCombo() + " when clicking the upgrade button to skip it once.</color>", "Yes, Hack", "No, Do Not" );
                 }
                 else
                     DoHack();

                return MouseHandlingResult.None;
            }

            public void DoHack()
            {
                NecromancerUpgrade upgrade = GetNecromancerUpgradeFromElement( this.Element );
                if ( upgrade == null )
                    return;
                if ( Engine_Universal.CurrentPopups.Count > 0 ) //we got some sort of warning telling us we can't do this
                    return;

                GameCommand command = GameCommand.Create( BaseGameCommand.CommandsByCode[BaseGameCommand.Code.TransformNecrofleet], GameCommandSource.IsLiterallyFromDirectClickOfLocalPlayer );
                command.RelatedEntityIDs.Add( Info.TargetShip.PrimaryKeyID );
                command.RelatedString2 = upgrade.InternalName;
                World_AIW2.Instance.QueueGameCommand( World_AIW2.Instance.GetLocalPlayerFactionOrNaturalObjectsNeverNull(), command, true );
                Window_HackChoicesSidebarPopout.Instance.Close();
            }

            private string lastRejectionReason = string.Empty;
            public bool GetCanHackForThisItem( NecromancerUpgrade item )
            {
                int debugCode = 0;
                try{
                    debugCode = 100;
                    if ( Info == null )
                    {
                        lastRejectionReason = "Info was null";
                        return false;
                    }
                    debugCode = 110;
                    GameEntity_Squad hacker = Info.TargetShip; //this is handled above, in terms of true cases
                    if ( hacker == null )
                    {
                        lastRejectionReason = "No hacker found";
                        return false;
                    }
                    debugCode = 115;
                    PlanetFaction pFaction = hacker.PlanetFaction;
                    if ( pFaction == null )
                    {
                        lastRejectionReason = "No pfaction found";
                        return false;
                    }
                    debugCode = 120;
                    Faction faction = pFaction.Faction;
                    if ( faction == null )
                    {
                        lastRejectionReason = "No faction found";
                        return false;
                    }
                    debugCode = 130;
                    FInt costForActiveHacks = HackingUtils.CalculateActiveHackingCosts(faction);
                    if ( hacker == null )
                    {
                        lastRejectionReason = "No hacker available at the moment";
                        return false;
                    }
                    debugCode = 200;
                    if ( item == null )
                    {
                        throw new Exception("How is item null?");
                    }
                    debugCode = 250;
                    if ( hacker.TypeData == item.RelatedShip )
                    {
                        lastRejectionReason = "Your flagship is already of this type";
                        return false;
                    }
                    debugCode = 300;
                    if (  Info.TargetShip.CurrentMarkLevel < item.MinFlagshipLevel )
                    {
                        lastRejectionReason = "Flagship is at too low mark level. This item requires a flagship with mark level " + item.MinFlagshipLevel;
                        return false;
                    }
                    debugCode = 400;
                    if ( faction.StoredHacking < item.BlueprintTransformCostInHacking )
                    {
                        lastRejectionReason = "You don't have enough hacking points";
                        return false;
                    }
                    if ( faction.StoredFactionResourceOne < item.BlueprintTransformCostInResourceOne )
                    {
                        if ( NecromancerEmpireFactionBaseInfo.GetIsThisANecromancerFaction( faction ) )
                            lastRejectionReason = "You don't have enough Essence. You will need to hack Rifts or fight Elderlings for more Essence";
                        else
                            lastRejectionReason = "You don't have enough resource one";
                        return false;
                    }
                    if ( faction.StoredHacking < item.BlueprintTransformCostInHacking + costForActiveHacks )
                    {
                        lastRejectionReason = "You don't have enough hacking points due to your active hacks";
                        return false;
                    }
                    debugCode = 500;
                    return Implementation.GetCanBeHacked( Info.TargetShip, hacker,
                                                          Info.TargetPlanet, hacker.GetFactionOrNull_Safe(), Info.HackingType,
                                                          this.Element.CreatedByCodeDirective.Identifier.CodeDirectiveTagString, -1, out lastRejectionReason ) == Hackable.CanBeHacked;
                } catch(Exception e)
                {
                    ArcenDebugging.ArcenDebugLogSingleLine("Hit excpetion in transform flagship GetCanHackForThisItem debugCode " + debugCode + " " + e.ToString(), Verbosity.DoNotShow );
                }

                return false;
            }

            private static readonly ArcenDoubleCharacterBuffer tooltipBuffer = new ArcenDoubleCharacterBuffer( "Hacking_TransformNecromancerFlagship-bCustomHackingOptionButton-tooltipBuffer" );
            public override void HandleMouseover()
            {
                int debugCode = 0;
                NecromancerUpgrade upgrade = GetNecromancerUpgradeFromElement( this.Element );
                try{
                    debugCode = 100;
                    if ( upgrade == null )
                    {
                        Window_AtMouseTooltipPanelNarrow.bPanel.Instance.SetText( this.Element, "Null NecromancerUpgrade, this is a bug." );
                        return;
                    }
                    else
                    {
                        debugCode = 200;
                        Faction localFaction = World_AIW2.Instance.GetPlayerFactionForUIOrNull();
                        debugCode = 300;
                        tooltipBuffer.Add( "<b><u>Hack: " ).Add( Info.HackingType.DisplayName ).Add( "</u></b>\n" );
                        tooltipBuffer.Add("This upgrade will allow you to transform any flagship of a suitable mark level into a  ").Add(upgrade.RelatedShip.GetDisplayName(), "a1ffa1").Add(" by spending Essence.\n");

                        if ( upgrade.MinFlagshipLevel  > 1 )
                        {
                            tooltipBuffer.Add("\n\n").Add("This upgrade is only available to a flagship at mark ").Add( upgrade.MinFlagshipLevel.ToString(), "ffa1a1").Add(" or above.\n");
                        }
                        debugCode = 400;
                        if ( !this.GetCanHackForThisItem( upgrade ) )
                        {
                            tooltipBuffer.Add( "<color=#c74639>Not Possible:</color> " );
                        }
                        debugCode = 500;
                        GameEntityTypeData typeData = upgrade.RelatedShip;
                        if ( typeData == null || typeData.TexEmbedSprite_Icon == null )
                        {
                            typeData = upgrade.RelatedShip;
                        }
                        debugCode = 600;
                        if ( typeData != null && typeData.TexEmbedSprite_Icon != null )
                        {
                            debugCode = 700;
                            //Write unit tooltip if appropriate
                            byte markLevel = localFaction.GetGlobalMarkLevelForShipLine( typeData );
                            tooltipBuffer.Add("\n");
                            EntityText.GetTooltip( tooltipBuffer, null, null, typeData, 1,
                                                                           localFaction, Info.TargetShip.CurrentMarkLevel, FromSidebarType.Sidebar_MultipleUnits,
                                                                           ShipExtraDetailFlags.BuildInfo | ShipExtraDetailFlags.AnyGrantHackInfo | ShipExtraDetailFlags.WindowHackChoicesSidebarPopoutForData, 1f, false );

                            EntityText.Write_Tooltip_Hotkeys_Footer( tooltipBuffer, false, true, null );
                        }
                        debugCode = 800;
                        if ( !this.GetCanHackForThisItem( upgrade ) )
                            tooltipBuffer.Add( "\n\n<color=#c74639>Cannot choose this option: " + this.lastRejectionReason + ".</color>" );
                        else
                        {
                            if ( GameSettings.Current.GetBoolBySetting( "UpgradeShipPrompt" ) )
                            {
                                tooltipBuffer.Add( "\n\n<color=#3f6c9e>Hold </color><color=#4486d1>" ).Add( InputActionTypeDataTable.Instance.GetHumanReadableKeyComboForAction( "SuppressTechUpgradePrompt" ) )
                                    .Add( "</color> <color=#3f6c9e>to suppress the 'Are you sure' prompt for a given hack.</color>  " );
                            }
                        }

                        Window_AtMouseTooltipPanelBesideSidebar.bPanel.Instance.SetText( tooltipBuffer.GetStringAndResetForNextUpdate(), "ShipTooltipScale" );
                    }
                } catch( Exception e )
                {
                    ArcenDebugging.ArcenDebugLogSingleLine("Hit exception in mouseover for a transformation: " + upgrade.ToString() + ". debugCode " + debugCode + " " + e.ToString(), Verbosity.DoNotShow );
                }
            }
        }
        #endregion
    }
    #endregion
    #endif
    #endregion
    #region Hacking_RiftHack
    public class Np_Hacking_RiftHack : HackingImplementation_WithMenu<NecromancerUpgrade, NecromancerUpgrade>
    {
        [ThreadStatic]
        private ArcenCharacterBuffer _buffer;
        
        private ArcenCharacterBuffer Buffer
        {
            get
            {
                if (_buffer == null)
                    _buffer = ArcenCharacterBuffer.Create_WillNeverBeGC();
                
                _buffer.Clear();
                return _buffer;
            }
        }
        
        public override NecromancerUpgrade GetItemFromWrapper(NecromancerUpgrade wrapper, out bool found) {
            found = true;
            return wrapper;
        }
        public override void PopulateItemsToShow(List<NecromancerUpgrade> listToPopulate, GameEntity_Squad TargetIfShip, Planet TargetIfPlanet, HackingType HackType)
        {
            TemplarPerUnitBaseInfo data = TargetIfShip?.TryGetExternalBaseInfoAs<TemplarPerUnitBaseInfo>();
            if ( data == null ) {
                ArcenDebugging.ArcenDebugLogSingleLine("Null TemplarPerUnitBaseInfo on " + TargetIfShip.ToStringWithPlanetAndOwner(), Verbosity.DoNotShow );
                return;
            }
            listToPopulate.CopyFrom(data.AvailableUpgrades);
        }
        public override Hackable GetCanHackForThisItem(NecromancerUpgrade item, out string rejectionReason, GameEntity_Squad Target, Planet planet, HackingType Type)
        {
            int debugCode = 0;
            try {
                debugCode = 100;
                Faction localFaction = World_AIW2.Instance.GetLocalPlayerFactionOrNull();
                if ( localFaction != null && !NecromancerEmpireFactionBaseInfo.GetIsThisANecromancerFaction(localFaction) ) {
                    //Only the necromancer can see these hacks
                    rejectionReason = "Only the necromancer can hack";
                    return Hackable.NeverCanBeHacked_Hide;
                }
                
                NecromancerEmpireFactionBaseInfo baseInfo = localFaction.GetExternalBaseInfoAs<NecromancerEmpireFactionBaseInfo>();
                if ( baseInfo == null ) {
                    throw new Exception("Could not find necromancer base info for local faction ");
                }
                
                debugCode = 500;
                if ( baseInfo.GetHighestNecropolisMarkLevel() < item.MustHaveAnyNecropolisAtThisLevel )
                {
                    rejectionReason = "You must have at least one necropolis at mark level " + item.MustHaveAnyNecropolisAtThisLevel + " to claim this upgrade, and your highest mark level necropolis is " + baseInfo.GetHighestNecropolisMarkLevel();
                    return Hackable.NeverBeHacked_ButStillShow;
                }
                
                GameEntity_Squad hacker = HackingUtils.CalculateHackerForHack( Target, planet, Type, false ); //this is handled above, in terms of true cases
                if ( hacker != null && hacker.CurrentMarkLevel < item.MinFlagshipLevel )
                {
                    rejectionReason = "Hacker is at too low mark level; this can only be hacked by a flagship at mark level " + item.MinFlagshipLevel;
                    return Hackable.NeverBeHacked_ButStillShow;
                }
                
                if (item.AdditionalHapCost > 0 || item.AdditionalEssenceCost > 0)
                {
                    var hapCost = Type.GetHackPointCostForTarget(Target) + item.AdditionalHapCost * Type.GetHapCostScale(Target, true);
                    var essCost = Type.GetResourceOneCostForTarget(Target) + item.AdditionalEssenceCost;

                    bool hapShort = hapCost > localFaction.StoredHacking;
                    bool essShort = essCost > localFaction.StoredFactionResourceOne;
                    
                    if (hapShort || essShort)
                    {
                        var buffer = this.Buffer;
                        buffer.Add("Not enough ");
                            
                        if (hapShort)
                            buffer.StartHacking(false).Add("Hacking").EndColor();
                                
                        if (essShort)
                        {
                            if (hapShort)
                                buffer.Add(" or ");
                            buffer.StartResourceOne(false).Add("Essence").EndColor();
                        }
                        
                        rejectionReason =  buffer.ToString();
                        
                        return Hackable.NeverBeHacked_ButStillShow;
                    }
                }
                
            } catch(Exception e) {
                ArcenDebugging.ArcenDebugLogSingleLine("Hit excpetion in rift hack GetCanHackForThisItem debugCode " + debugCode + " " + e.ToString(), Verbosity.DoNotShow );
                rejectionReason = "Exception in rift hack GetCanHackForThisItem";
                return Hackable.NeverCanBeHacked_Hide;
            }

            rejectionReason = "";
            return Hackable.CanBeHacked;
        }
        public override void GetDisplayNameForItem(ArcenCharacterBufferBase Buffer, NecromancerUpgrade upgrade)
        {
            GameEntityTypeData typeData = upgrade.RelatedShip;
            if ( typeData == null || typeData.TexEmbedSprite_Icon == null )
                typeData = upgrade.ShipForCapIncrease;
            
            Faction localFaction = World_AIW2.Instance.GetPlayerFactionForUIOrNull();

            if (typeData != null)
                Buffer.AddShipIconInline( typeData, localFaction );
            Buffer.Add( upgrade.GetShortDisplayName() );
                
            if ( upgrade.Type == NecromancerUpgradeType.IncreaseSkeletonCap ||
                 upgrade.Type == NecromancerUpgradeType.IncreaseWightCap )
            {
                Buffer.Add( " for Fleet" );
            }
        }
        public override void GetExtraLabelInfoForItem(ArcenCharacterBufferBase Buffer, NecromancerUpgrade upgrade, GameEntity_Squad Target, Planet planet, HackingType HackType)
        {
            if ( upgrade.AdditionalAIPCost > 0 || upgrade.AdditionalEssenceCost > 0 || upgrade.AdditionalHapCost > 0)
                Buffer.Add("   ");
            
            if ( upgrade.AdditionalAIPCost > 0 )
            {
                Buffer.AddAIP(upgrade.AdditionalAIPCost + HackType.AIPOnCompletion, true);
            }
            if ( upgrade.AdditionalEssenceCost > 0 )
            {
                Buffer.AddResourceOne(upgrade.AdditionalEssenceCost + HackType.GetResourceOneCostForTarget(Target), true);
            }
            if ( upgrade.AdditionalHapCost > 0 )
            {
                Buffer.AddHacking(upgrade.AdditionalHapCost * HackType.GetHapCostScale(Target, true), true);
            }
        }
        
        public override void GetTooltipForItem(ArcenCharacterBufferBase buffer, NecromancerUpgrade upgrade, GameEntity_Squad Target, Planet planet, HackingType Type)
        {
            int debugCode = 0;
            try {
                debugCode = 200;
                Faction localFaction = World_AIW2.Instance.GetPlayerFactionForUIOrNull();
                NecromancerEmpireFactionBaseInfo gData = localFaction.TryGetExternalBaseInfoAs<NecromancerEmpireFactionBaseInfo>();
                int upgradeCount = 0;
                if ( gData != null )
                {
                    for ( int i = 0; i < gData.NecromancerCompletedUpgrades.Count; i++ )
                    {
                        if ( gData.NecromancerCompletedUpgrades[i] == upgrade )
                            upgradeCount++;
                    }
                }
                if ( upgrade.Type == NecromancerUpgradeType.GrantEssence )
                {
                    buffer.Add("This upgrade will harvest ").Add( World_AIW2.Instance.Resource1TextColorAndIcon ).Add(" ").Add( upgrade.RelatedResource ).Add(" Essence from the Rift; it will allow you to build or upgrade a Necropolis, or upgrade a flagship.\n");
                }
                else if ( upgrade.Type == NecromancerUpgradeType.UnlockSkeletonType ||
                        upgrade.Type == NecromancerUpgradeType.UnlockWightType ||
                        upgrade.Type == NecromancerUpgradeType.UnlockMummyType )
                {
                    debugCode = 310;
                    buffer.Add("This upgrade will grant the ability to build ").Add( upgrade.CapIncrease.ToString(), "ffa1a1" ).Add(" additional ").Add(upgrade.ShipForCapIncrease.GetDisplayName() ).Add( " at any necropolis. This will allow the flagship for that necropolis to acquire ").Add(upgrade.RelatedShip.GetDisplayName(), "a1ffa1").Add(" when you kill suitable enemies");
                    if ( upgrade.GalaxyCapIncrease != 0 )
                        buffer.Add( " and increases the galaxy-wide cap by " ).Add( upgrade.GalaxyCapIncrease.ToString(), "ffa1a1" );
                    buffer.Add(".\n");
                    if ( upgradeCount > 0 )
                    {
                        string attempts = "time";
                        if ( upgradeCount > 1 )
                            attempts = "times";
                        buffer.Add("\n<size=90%>\nYou have already unlocked this upgrade ").Add(upgradeCount.ToString(), "a1a1ff").Add(" " + attempts + ". Unlocking something multiple times means you can build more ").Add( upgrade.ShipForCapIncrease.GetDisplayName() ).Add("s per Necropolis, which can be handy.\n</size>");
                    }
                    // TODO: Mention bodyguards
                }
                else if ( upgrade.Type == NecromancerUpgradeType.UnlockNewShip )
                {
                    debugCode = 320;
                    buffer.Add("This upgrade will grant the ability to build ").Add(  upgrade.CapIncrease.ToString(), "ffa1a1" ).Add(" additional ").Add(upgrade.ShipForCapIncrease.GetDisplayName() ).Add(" at any necropolis. The flagship of that necropolis will then be able to build ").Add(upgrade.RelatedShip.GetDisplayName(), "a1ffa1").Add(" at any necromancer shipyard");
                    if ( upgrade.GalaxyCapIncrease != 0 )
                        buffer.Add( " and increases the galaxy-wide cap by " ).Add( upgrade.GalaxyCapIncrease.ToString(), "ffa1a1" );
                    buffer.Add(".\n");
                    if ( upgradeCount > 0 )
                    {
                        string attempts = "time";
                        if ( upgradeCount > 1 )
                            attempts = "times";

                        buffer.Add("<size=90%>\nYou have already unlocked this upgrade ").Add(upgradeCount.ToString(), "a1a1ff").Add(" " + attempts + ". Unlocking something multiple times means you can build more ").Add( upgrade.ShipForCapIncrease.GetDisplayName() ).Add("s per Necropolis, which can be handy.\n</size>");
                    }

                }
                else if ( upgrade.Type == NecromancerUpgradeType.IncreaseSkeletonCap || upgrade.Type == NecromancerUpgradeType.IncreaseWightCap )
                {
                    debugCode = 330;
                    buffer.Add("This upgrade will increase the ship cap for every line of ").Add(upgrade.ShipForCapIncrease.GetDisplayName(), "a1ffa1").Add(" by " ).Add( upgrade.CapIncrease.ToString(), "a1ffa1").Add(" for ONLY this flagship.\n");
                }
                else if ( upgrade.Type == NecromancerUpgradeType.IncreaseSkeletonSoftCap )
                {
                    debugCode = 330;
                    buffer.Add("This upgrade will increase the soft cap for skeletons by ").Add( upgrade.CapIncrease.ToString(), "a1ffa1").Add(" for this flagship.\n");
                }
                else if ( upgrade.Type == NecromancerUpgradeType.IncreaseWightSoftCap )
                {
                    debugCode = 340;
                    buffer.Add("This upgrade will increase the soft cap for wights by ").Add(upgrade.CapIncrease.ToString(), "a1ffa1").Add(" for this flagship.\n");
                }
                else if ( upgrade.Type == NecromancerUpgradeType.ClaimBlueprints )
                {
                    debugCode = 340;
                    buffer.Add("This upgrade will allow you to transform any flagship of a suitable mark level into a  ").AddShipIconNameShort(upgrade.RelatedShip).Add(" by spending Essence harvested from a Rift.\n");
                }
                else
                {
                    buffer.Add("Unknown upgrade\n");
                }
                
                int count = 0;
                if ( upgrade.MinFlagshipLevel  > 1 )
                {
                    if (count == 0) buffer.Pad();
                    count++;
                    Buffer.Add("This upgrade is only available to a flagship at mark ").Add( upgrade.MinFlagshipLevel.ToString(), "ffa1a1").Add(" or above.\n");
                }
                if ( upgrade.MustHaveAnyNecropolisAtThisLevel > 1 )
                {
                    if (count == 0) buffer.Pad();
                    count++;
                    buffer.Add("This upgrade is only available to a necropolis at mark ").Add( upgrade.MustHaveAnyNecropolisAtThisLevel.ToString(), "ffa1a1").Add(" or above.\n");
                }

                if ( upgrade.AdditionalAIPCost > 0 || upgrade.AdditionalEssenceCost > 0 || upgrade.AdditionalHapCost > 0)
                {
                    buffer.Pad().NewLineIfNeeded().Add("This hack costs ");
                    
                    int counter = 0;
                    if ( upgrade.AdditionalAIPCost > 0 )
                    {
                        if (counter > 0) buffer.Add(Text.ThinSpace);
                        counter++;
                        buffer.AddAIP(upgrade.AdditionalAIPCost, true);
                    }
                    if ( upgrade.AdditionalEssenceCost > 0 )
                    {
                        if (counter > 0) buffer.Add(Text.ThinSpace);
                        counter++;
                        buffer.AddResourceOne(Type.GetResourceOneCostForTarget(Target) + upgrade.AdditionalEssenceCost, true);
                    }
                    if ( upgrade.AdditionalHapCost > 0 )
                    {
                        if (counter > 0) buffer.Add(Text.ThinSpace);
                        counter++;
                        buffer.AddHacking(Type.GetHackPointCostForTarget(Target) + upgrade.AdditionalHapCost * Type.GetHapCostScale(Target, true), true);
                    }
            
                    buffer.Add( "\n" );
                }


                debugCode = 500;
                GameEntityTypeData typeData = upgrade.ShipForCapIncrease ?? upgrade.RelatedShip;
                debugCode = 600;
                if ( typeData != null && typeData.TexEmbedSprite_Icon != null )
                {
                    debugCode = 700;
                    //Write unit tooltip if appropriate
                    byte markLevel = localFaction.GetGlobalMarkLevelForShipLine( typeData );
                    buffer.Add("\n");
                    EntityText.GetTooltip( buffer, null, null, typeData, 1,
                            localFaction, markLevel, FromSidebarType.Sidebar_MultipleUnits,
                            ShipExtraDetailFlags.BuildInfo | ShipExtraDetailFlags.AnyGrantHackInfo | ShipExtraDetailFlags.WindowHackChoicesSidebarPopoutForData, 1f, false );

                    EntityText.Write_Tooltip_Hotkeys_Footer( buffer, false, true, null );
                }
            } catch( Exception e )
            {
                ArcenDebugging.ArcenDebugLogSingleLine("Hit exception in mouseover for a hacking upgrade " + upgrade.ToString() + ". debugCode " + debugCode + " " + e.ToString(), Verbosity.DoNotShow );
            }
        }
        public override void DoHack(NecromancerUpgrade upgrade, GameEntity_Squad TargetIfShip, Planet TargetIfPlanet, HackingType HackType)
        {
            string lastRejectionReason = "";
            HackingUtils.TryDoHack( ref lastRejectionReason, TargetIfShip, TargetIfPlanet, HackType,
                    upgrade.InternalName, -1, null );
        }

        public override void DoOneSecondOfHackingLogic_HackSpecificLogic( GameEntity_Squad Target, Planet planet, GameEntity_Squad Hacker, ArcenHostOnlySimContext Context, HackingType type, HackingEvent Event )
        {
            // Handle hacking reaction manually, forcing the faction to be an ai faction.
            int secondsSoFar = Hacker.ActiveHack_DurationThusFar;
            if ( secondsSoFar == 0 )
                return;

            Faction targetFaction = Target.PlanetFaction.Faction;
            if ( targetFaction == null ) // Shouldn't be able to occur, but you never know.
                return;
            if ( Hacker.ActiveHack_DurationThusFar % type.GetPrimaryHackResponseInterval() == 0 )
                targetFaction.Safe_DeepInfo_ReactToHacking_AsPartOfMainSim_HostOnly( Target, FInt.One, Context, Event, targetFaction );
        }
        
        public override bool DoSuccessfulCompletionLogic_Extra( GameEntity_Squad Target, Planet planet, GameEntity_Squad Hacker, ArcenHostOnlySimContext Context, HackingType type, HackingEvent Event )
        {
            int debugCode = 0;
            try
            {
                debugCode = 100;
                var facOrNull = Hacker.GetFactionOrNull_Safe();
                var data = Target.TryGetExternalBaseInfoAs<TemplarPerUnitBaseInfo>();
                if ( data == null )
                    throw new Exception("Tried to hack " + Target.ToStringWithPlanetAndOwner() + " but it had no templar data");
                
                NecromancerUpgrade upgrade = null;
                
                debugCode = 200;
                if (!string.IsNullOrEmpty(Event.RelatedStringOrNull))
                    upgrade = data.AvailableUpgrades.Find((u)=>u.InternalName == Event.RelatedStringOrNull);
                
                if ( upgrade == null )
                    throw new Exception("Could not find upgrade to grant! We were looking for " + Event.RelatedStringOrNull + ".");

                debugCode = 400;
                if ( upgrade.AdditionalEssenceCost > 0)
                    facOrNull.StoredFactionResourceOne -= upgrade.AdditionalEssenceCost;

                if ( upgrade.AdditionalAIPCost > 0 )
                {
                    Event.AIPchanged += (FInt)upgrade.AdditionalAIPCost;
                    GlobalAIWorldBaseInfo.Instance.ChangeAIP( (FInt)upgrade.AdditionalAIPCost, AIPChangeReason.Hacking, Target.TypeData, upgrade.RelatedShip, Hacker.GetFactionIndex_Safe(), planet.Index, -1 );
                }

                if ( upgrade.AdditionalHapCost > 0 )
                {
                    FInt hapAdded = upgrade.AdditionalHapCost.ToFInt();
                    if (Event.IsAgainstPlanet)
                        hapAdded *= type.GetHapCostScale(planet, true);
                    else
                        hapAdded *= type.GetHapCostScale(Target, true);
                    
                    Event.HackingPointsSpent += hapAdded;
                    Event.HackingPointsLeftAfterHack -= hapAdded;
                    facOrNull.StoredHacking -= hapAdded;
                    Target.GetFactionOrNull_Safe().HackingPointsUsedAgainstThisFaction += hapAdded;
                }
                
                var gData = facOrNull.TryGetExternalBaseInfoAs<NecromancerEmpireFactionBaseInfo>();
                gData.NumRiftsHacked++;
                
                var thisEvent = NecromancerUpgradeEvent.Create( facOrNull.FactionIndex, Target.PlanetFaction.Faction.FactionIndex, Target.Planet.Index, upgrade.Index, -1, Target.TypeData);
                thisEvent.HackingPointsSpent = Event.HackingPointsSpent;
                thisEvent.HackingPointsLeftAfterHack = Event.HackingPointsLeftAfterHack;
                thisEvent.AIPchanged = Event.AIPchanged;
                thisEvent.EssenceSpent = (short)(Event.HackType.GetResourceOneCostForTarget(Target).ToInt() + upgrade.AdditionalEssenceCost);
                
                if ( upgrade.Type == NecromancerUpgradeType.GrantEssence )
                {
                    debugCode = 500;
                    Hacker.PlanetFaction.Faction.StoredFactionResourceOne += upgrade.RelatedResource;
                    
                    return true;
                }
                
                switch (upgrade.Target) 
                {
                    case NecromancerUpgradeTarget.Faction: 
                    {
                        debugCode = 600;
                        //ArcenDebugging.ArcenDebugLogSingleLine("Granting " + upgrade.ToString() + " to a faction" , Verbosity.DoNotShow );
                        
                        gData.NecromancerCompletedUpgrades.Add( upgrade );
                        gData.NecromancerHistory.Add( thisEvent );
                        
                        return true;
                    }
                    case NecromancerUpgradeTarget.Fleet: 
                    {
                        debugCode = 700;
                        //ArcenDebugging.ArcenDebugLogSingleLine("Granting " + upgrade.ToString() + " to a fleet" , Verbosity.DoNotShow );

                        var fleet = Hacker.FleetMembership.Fleet;
                        var fleetInfo = fleet.GetExternalBaseInfoAs<Np_NecromancerMobileFleetBaseInfo>();
                        thisEvent.RelatedFleetId = fleet.FleetID;
                        
                        fleetInfo.NecromancerCompletedUpgrades.Add(upgrade);
                        gData.NecromancerHistory.Add( thisEvent );
                        
                        return true;
                    }
                    default: 
                    {
                        ArcenDebugging.ArcenDebugLogSingleLine( "Unknown NecromancerUpgradeTarget " + upgrade.Target +" in Hacking_RiftHack DoSuccessfulCompletionLogic_Extra.", Verbosity.ShowAsError );
                        return false;
                    }
                }
            }
            catch ( Exception e )
            {
                if ( ArcenNetworkAuthority.DesiredStatus != DesiredMultiplayerStatus.Client )
                    ArcenDebugging.ArcenDebugLogSingleLine( "Exception hit during Hacking_RiftHack DoSuccessfulCompletionLogic_Extra debug code " + debugCode + " " + e.ToString(), Verbosity.ShowAsError );
            }
            
            return false;
        }
        public override System.Collections.Generic.IEnumerable<GrantedShip> EnumeratePossibleShipGrants(GameEntity_Squad Target)
        {
            var data = Target?.TryGetExternalBaseInfoAs<TemplarPerUnitBaseInfo>();
            if ( data == null )
                yield break;
            
            foreach (var up in data.AvailableUpgrades)
            {
                if (up != null)
                {
                    // We want searches for "Wight" or "Vengeful Wight" to show us (TemplarRift)
                    // as a location hackable for it, even though what we actually grant
                    // is a "Vengeful Wight Home". 
                    // 
                    // And the count we want to show is "1" because
                    // this is a single location offering them.
                    //
                    // The actual count of wights you get depends on how many homes you build, after all.
                    //
                    if ( up.RelatedShip != null )
                        yield return new GrantedShip(up.RelatedShip, 1);
                    // We also want searches for "Wight" or "Bodyguard" to show the hacks which increase 
                    // a wight bodyguard cap.
                    else
                    if ( up.ShipTypeNameForCapIncrease != null )
                        yield return new GrantedShip(up.ShipForCapIncrease, 1);
                    // We also want the wight/skeleton softcap increase to appear for "wight" or "skeleton" searches.
                    else
                    if ( up.Type == NecromancerUpgradeType.IncreaseSkeletonSoftCap )
                        yield return new GrantedShip(GameEntityTypeDataTable.Instance.GetRowByName("BaseSkeleton"), 1);
                    else
                    if ( up.Type == NecromancerUpgradeType.IncreaseWightSoftCap )
                        yield return new GrantedShip(GameEntityTypeDataTable.Instance.GetRowByName("BaseWight"), 1);
                }
            }
        }
        
        protected override void Internal_GetMinAndMaxCostToHackForSidebar( List<NecromancerUpgrade> Items, GameEntity_Squad Target, Planet planet, Faction hackerFaction, HackingType Type, out FInt MinCost, out FInt MaxCost )
        {
            FInt? min = null;
            FInt? max = null;
            
            foreach (var u in Items)
            {
                var res = GetCanHackForThisItem(u, out _, Target, planet, Type);
                var cost = Type.GetHackPointCostForTarget(Target) + u.AdditionalHapCost * Type.GetHapCostScale(Target, true);
                
                if (res.IsHidden())
                    continue;

                if (!min.HasValue || cost < min.Value)
                    min = cost;
                if (!max.HasValue || cost > max.Value)
                    max = cost;
            }
            
            MinCost = FInt.Zero;
            if (min.HasValue)
                MinCost = min.Value;
            
            MaxCost = FInt.Zero;
            if (max.HasValue)
                MaxCost = max.Value;
        }
    }
    #endregion
}
