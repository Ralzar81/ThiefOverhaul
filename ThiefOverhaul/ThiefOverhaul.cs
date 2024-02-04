using DaggerfallConnect;
using Wenzil.Console;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Utility;
using System.Collections.Generic;
using System;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.Guilds;
using DaggerfallWorkshop.Game.Banking;

namespace ThiefOverhaul
{
    public class ThiefOverhaul : MonoBehaviour
    {
        static Mod mod;
        static ThiefOverhaul instance;
        FenceWindow fenceWindow;
        internal FenceWindow GetFenceWindow() { return fenceWindow; }

        public const int templateIndex_Bracelet = 543;
        public const int templateIndex_Bracer = 544;
        public const int templateIndex_Crystal = 545;
        public const int templateIndex_Ring = 546;
        public const int templateIndex_Mark = 547;

        static DaggerfallUnity dfUnity = DaggerfallUnity.Instance;
        static PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
        static PlayerEnterExit playerEnterExit = GameManager.Instance.PlayerEnterExit;
        static EntityEffectManager playerEffectManager = playerEntity.EntityBehaviour.GetComponent<EntityEffectManager>();
        static BuildingDirectory buildingDirectory;
        static int burglaryCounter = 0;
        static StaticNPC npc = QuestMachine.Instance.LastNPCClicked;
        static int lockpickingBonus = 20;
        static int streetwiseBonus = 20;
        static int pickpocketBonus = 20;        
        static int climbingBonus = 20;
        static int stealthBonus = 20;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            Debug.Log("[ThiefOverhaul] Mod Init.");
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<ThiefOverhaul>();

            EntityEffectBroker.OnNewMagicRound += ThiefEffects_OnNewMagicRound;
            PlayerEnterExit.OnTransitionInterior += SneakIntoHouse;
            PlayerEnterExit.OnTransitionExterior += SneakCounter_OnTransitionExterior;
            PlayerActivate.RegisterCustomActivation(mod, 182, 25, ShadowAppraiserClicked);
            PlayerActivate.RegisterCustomActivation(mod, 182, 35, ShadowAppraiserClicked);
            PlayerActivate.RegisterCustomActivation(mod, 186, 26, ShadowAppraiserClicked);
            PlayerActivate.RegisterCustomActivation(mod, 182, 35, ShadowAppraiserClicked);
            PlayerActivate.OnLootSpawned += TheftItems_OnLootSpawned;
            GameManager.Instance.RegisterPreventRestCondition(() => { return RestingInOpenShop(); }, "'Are you going to buy something?' asks the merchant.");
            GameManager.Instance.RegisterPreventRestCondition(() => { return RestingInClosedShop(); }, "You can not rest or loiter now.");

            ItemHelper itemHelper = DaggerfallUnity.Instance.ItemHelper;

            itemHelper.RegisterCustomItem(templateIndex_Ring, ItemGroups.None, typeof(ItemLockpicks));
            itemHelper.RegisterCustomItem(templateIndex_Mark, ItemGroups.None, typeof(ItemMark));
            itemHelper.RegisterCustomItem(templateIndex_Bracelet, ItemGroups.None, typeof(ItemBracelet));
            itemHelper.RegisterCustomItem(templateIndex_Bracer, ItemGroups.None, typeof(ItemRope));
            itemHelper.RegisterCustomItem(templateIndex_Crystal, ItemGroups.None, typeof(ItemPebbles));

            PlayerActivate.RegisterCustomActivation(mod, 41006, ShopShelfBurglar);
            PlayerActivate.RegisterCustomActivation(mod, 41011, ShopShelfBurglar);
            PlayerActivate.RegisterCustomActivation(mod, 41017, ShopShelfBurglar);
            PlayerActivate.RegisterCustomActivation(mod, 41018, ShopShelfBurglar);
            PlayerActivate.RegisterCustomActivation(mod, 41028, ShopShelfBurglar);
            PlayerActivate.RegisterCustomActivation(mod, 41031, ShopShelfBurglar);
            PlayerActivate.RegisterCustomActivation(mod, 41040, ShopShelfBurglar);
            PlayerActivate.RegisterCustomActivation(mod, 41042, ShopShelfBurglar);
            PlayerActivate.RegisterCustomActivation(mod, 41044, ShopShelfBurglar);
            PlayerActivate.RegisterCustomActivation(mod, 41046, ShopShelfBurglar);
        }

        void Awake()
        {
            mod.IsReady = true;

            fenceWindow = new FenceWindow(DaggerfallUI.UIManager, npc, GameManager.Instance.GuildManager.GetGuildGroup(805));

            RegisterRWCommands();

            Debug.Log("[ThiefOverhaul] Mod is ready.");
        }

        void Start()
        {
            Debug.Log("[ThiefOverhaul] Loading Start().");

            FormulaHelper.RegisterOverride<Func<int, int, int, int>>(mod, "CalculateInteriorLockpickingChance", (level, lockvalue, lockpickingSkill) =>
            {
                int agInt = ((playerEntity.Stats.LiveAgility + playerEntity.Stats.LiveIntelligence) / 2) - 50;

                int luck = (playerEntity.Stats.LiveLuck / 10) - 5;

                lockvalue *= 4;

                int chance = agInt + luck - lockvalue + lockpickingSkill;


                return Mathf.Clamp(chance, 5, 95);
            });

            FormulaHelper.RegisterOverride<Func<int, int, int>>(mod, "CalculateExteriorLockpickingChance", (lockvalue, lockpickingSkill) =>
            {
                int agInt = ((playerEntity.Stats.LiveAgility + playerEntity.Stats.LiveIntelligence) / 2) - 50;

                int luck = (playerEntity.Stats.LiveLuck / 10) - 5;

                lockvalue *= 4;

                int chance = agInt + luck - lockvalue + lockpickingSkill;


                return Mathf.Clamp(chance, 5, 95);
            });

            FormulaHelper.RegisterOverride<Func<PlayerEntity, EnemyEntity, int>>(mod, "CalculatePickpocketingChance", (player, target) =>
            {
                int AgSpd = ((playerEntity.Stats.LiveAgility + playerEntity.Stats.LiveSpeed) / 2) - 50;
                int luck = (playerEntity.Stats.LiveLuck / 10) - 5;
                int chance = player.Skills.GetLiveSkillValue(DFCareer.Skills.Pickpocket) + AgSpd + luck;
                // If target is an enemy mobile, apply level modifier.
                if (target != null)
                {
                    if (target.EntityBehaviour.EntityType == EntityTypes.EnemyClass)
                    {
                        EnemySenses enemySenses = target.EntityBehaviour.GetComponent<EnemySenses>();
                        if (enemySenses != null && enemySenses.HasEncounteredPlayer)
                        {
                            chance -= 30;
                        }
                    }
                    chance -= 5 * target.Level;
                }
                return Mathf.Clamp(chance, 5, 95);
            });

            //CalculateShopliftingChance formula is also used for house robbing in DaggerfallInvnetoryWindow.AttemptPrivatePropertyTheft() upon exiting the inventory.
            FormulaHelper.RegisterOverride<Func<PlayerEntity, int, int, int>>(mod, "CalculateShopliftingChance", (player, shopQuality, weightAndNumItems) =>
            {
                int AgSpd = ((playerEntity.Stats.LiveAgility + playerEntity.Stats.LiveSpeed) / 2) - 50;
                int luck = (playerEntity.Stats.LiveLuck / 10) - 5;
                int chance = 100 - (player.Skills.GetLiveSkillValue(DFCareer.Skills.Pickpocket) + AgSpd + luck);

                chance += (shopQuality * 2) + weightAndNumItems;

                return Mathf.Clamp(chance, 5, 95);
            });

            FormulaHelper.RegisterOverride<Func<float, DaggerfallEntityBehaviour, int>>(mod, "CalculateStealthChance", (distanceToTarget, target) =>
            {
                int stealthValue = target.Entity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth);
                bool stealthMaster = stealthValue >= 100;
                if (target == GameManager.Instance.PlayerEntityBehaviour)
                {
                    stealthValue = StealthCalc(stealthValue, true);
                }

                int chance = 2 * ((int)(distanceToTarget / MeshReader.GlobalScale) * stealthValue >> 10);

                return Mathf.Clamp(chance, 5, 95);
            });
        }

        private void Update()
        {
            if (!dfUnity.IsReady || !playerEnterExit || GameManager.IsGamePaused || DaggerfallUI.Instance.FadeBehaviour.FadeInProgress)
                return;

            if (!playerEntity.HaveShownSurrenderToGuardsDialogue && playerEntity.CrimeCommitted != PlayerEntity.Crimes.None && GameManager.Instance.HowManyEnemiesOfType(MobileTypes.Knight_CityWatch, false, true) > 0)
            {
                Halt();
            }
        }

        public static void ShadowAppraiserClicked(RaycastHit hit)
        {
            
            FactionFile.FactionData factionData;
            FactionFile.GuildGroups guildGroup;
            npc = QuestMachine.Instance.LastNPCClicked;
            GuildServices service = Services.GetService((GuildNpcServices)npc.Data.factionID);
            if (QuestMachine.Instance.HasFactionListener(npc.Data.factionID))
                return;
            if (GameManager.Instance.PlayerEntity.FactionData.GetFactionData(npc.Data.factionID, out factionData))
            {
                if (Services.HasGuildService(npc.Data.factionID) && npc.Data.factionID == 805)
                {
                    (DaggerfallUI.Instance.UserInterfaceManager.TopWindow as DaggerfallGuildServicePopupWindow).CloseWindow();
                    guildGroup = GameManager.Instance.GuildManager.GetGuildGroup(805);
                    FenceWindow fenceWindow = new FenceWindow(DaggerfallUI.UIManager, npc, guildGroup);
                    DaggerfallUI.UIManager.PushWindow(fenceWindow);
                }
            }
        }


        private static void Halt()
        {
            DaggerfallEntityBehaviour[] entityBehaviours = FindObjectsOfType<DaggerfallEntityBehaviour>();
            for (int i = 0; i < entityBehaviours.Length; i++)
            {
                DaggerfallEntityBehaviour entityBehaviour = entityBehaviours[i];
                if (entityBehaviour.EntityType == EntityTypes.EnemyClass && !playerEntity.HaveShownSurrenderToGuardsDialogue)
                {
                    EnemyEntity enemyEntity = entityBehaviour.Entity as EnemyEntity;
                    EnemyMotor enemyMotor = entityBehaviour.GetComponent<EnemyMotor>();
                    if (enemyEntity.MobileEnemy.Team == MobileTeams.CityWatch && enemyMotor.IsHostile && Vector3.Distance(entityBehaviour.transform.position, GameManager.Instance.PlayerObject.transform.position) < 4f)
                    {
                        EnemySenses enemySenses = entityBehaviour.GetComponent<EnemySenses>();
                        bool guardCanSeePlayer = enemySenses.Target == GameManager.Instance.PlayerEntityBehaviour && enemySenses.TargetInSight;
                        if (guardCanSeePlayer)
                        {
                            playerEntity.HaveShownSurrenderToGuardsDialogue = true;
                            BribePrompt();
                            return;
                        }
                    }
                }
            }
        }

        private static void BribePrompt()
        {
            string[] message = {
                            "Halt! You are under arrest for " + CrimeComitted() + ".",
                            " ",
                            "Do you attempt to bribe the guard?"
                        };
            DaggerfallMessageBox messageBox = new DaggerfallMessageBox(DaggerfallUI.UIManager);
            messageBox.SetText(message);
            messageBox.ParentPanel.BackgroundColor = Color.clear;
            messageBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.Yes);
            messageBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.No, true);
            messageBox.OnButtonClick += Bribe_OnButtonClick;
            messageBox.Show();
        }

        private static void Bribe_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {
            sender.CloseWindow();
            if (messageBoxButton == DaggerfallMessageBox.MessageBoxButtons.Yes)
                BribePopup();
            else
                ArrestPrompt();
        }

        private static void BribePopup()
        {
            int regionIndex = GameManager.Instance.PlayerGPS.CurrentRegionIndex;
            int crimeType = (int)playerEntity.CrimeCommitted - 1;
            int legalRep = playerEntity.RegionData[regionIndex].LegalRep;
            int streetwise = playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Streetwise) + (playerEntity.Stats.LiveLuck / 10) -5;
            int crimeLevel = 1;
            //Legalrep = 0 common. -11 gives chance of Criminal Conspiracy, -80 is hated.

            //1 = minor, 2 = major, 3 = high
            if ((crimeType >= 4 && crimeType <= 7) || crimeType == 9)
                crimeLevel = 2;
            else if (crimeType == 10 || crimeType == 11 || crimeType == 14)
                crimeLevel = 3;

            int cost = CalculateBribeCost(crimeLevel);

            if (cost > playerEntity.GoldPieces)
                DaggerfallUI.MessageBox("You are not carrying enough gold to pay the guard.");

            bool charmGuard = UnityEngine.Random.Range(10, 100) * crimeLevel < streetwise + legalRep;
            bool affordBribe = cost <= playerEntity.GoldPieces;


            if (charmGuard)
            {
                if (cost == 0)
                {
                    playerEntity.CrimeCommitted = PlayerEntity.Crimes.None;
                    playerEntity.SpawnCityGuards(false);
                    string[] message = {
                            "You manage to talk yourself out of the situation.",
                            " ",
                            "The guard leaves you alone without even taking a bribe."
                        };
                    DaggerfallMessageBox messageBox = new DaggerfallMessageBox(DaggerfallUI.UIManager);
                    messageBox.SetText(message);
                    messageBox.ClickAnywhereToClose = true;
                    messageBox.AllowCancel = true;
                    messageBox.ParentPanel.BackgroundColor = Color.clear;
                    messageBox.Show();
                }
                if (affordBribe)
                {
                    playerEntity.CrimeCommitted = PlayerEntity.Crimes.None;
                    playerEntity.SpawnCityGuards(false);
                    string[] message = {
                            "You come to an agreement with the guard.",
                            " ",
                            cost.ToString() + " gold changes hands and you part ways."
                        };
                    DaggerfallMessageBox messageBox = new DaggerfallMessageBox(DaggerfallUI.UIManager);
                    messageBox.SetText(message);
                    messageBox.ClickAnywhereToClose = true;
                    messageBox.AllowCancel = true;
                    messageBox.ParentPanel.BackgroundColor = Color.clear;
                    messageBox.Show();
                }
                else
                {
                    ArrestPrompt();
                    string[] message = {
                            "You almost come to an agreement with the guard.",
                            " ",
                            "But you do not have the " + cost.ToString() + " gold he demands."
                        };
                    DaggerfallMessageBox messageBox = new DaggerfallMessageBox(DaggerfallUI.UIManager);
                    messageBox.SetText(message);
                    messageBox.ClickAnywhereToClose = true;
                    messageBox.AllowCancel = true;
                    messageBox.ParentPanel.BackgroundColor = Color.clear;
                    messageBox.Show();
                }
            }
            else
            {
                ArrestPrompt();
                DaggerfallUI.MessageBox("The guard is not interested in your attempts at a conversation.");
            }
        }

        private static int CalculateBribeCost(int crimeLevel)
        {
            int merchantile = Mathf.Max(playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Mercantile) / 10, 1);
            int cost = Mathf.Max((300 * crimeLevel) / merchantile, 0);

            return cost;
        }

        private static void ArrestPrompt()
        {
            playerEntity.LowerRepForCrime();
            DaggerfallMessageBox messageBox = new DaggerfallMessageBox(DaggerfallUI.UIManager);
            messageBox.SetTextTokens(DaggerfallUnity.Instance.TextProvider.GetRSCTokens(15));
            messageBox.ParentPanel.BackgroundColor = Color.clear;
            messageBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.Yes);
            messageBox.AddButton(DaggerfallMessageBox.MessageBoxButtons.No, true);
            messageBox.OnButtonClick += SurrenderToGuardsDialogue_OnButtonClick;
            messageBox.Show();
        }

        private static void SurrenderToGuardsDialogue_OnButtonClick(DaggerfallMessageBox sender, DaggerfallMessageBox.MessageBoxButtons messageBoxButton)
        {
            sender.CloseWindow();
            if (messageBoxButton == DaggerfallMessageBox.MessageBoxButtons.Yes)
                GameManager.Instance.PlayerEntity.SurrenderToCityGuards(true);;
        }

        static int StealthCalc(int stealthValue, bool formulaStealthCheck, bool trainStealth = false)
        {
            uint gameMinutes = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime();
            PlayerMotor playerMotor = GameManager.Instance.PlayerMotor;
            bool stealthMaster = stealthValue >= 100;

            stealthValue += (playerEntity.Stats.LiveLuck / 10) - 5;
            stealthValue += (playerEntity.Stats.LiveAgility / 5) - 10;
            stealthValue -= StealthArmor(stealthMaster);

            if (playerMotor.IsCrouching)
            {
                stealthValue += 10;
            }
            if (playerMotor.IsStandingStill)
            {
                stealthValue += 20;
            }
            else if (!playerMotor.IsMovingLessThanHalfSpeed && !stealthMaster)
            {
                bool lucky = Dice100.SuccessRoll(playerEntity.Stats.LiveLuck);
                if (playerEntity.TimeOfLastStealthCheck == gameMinutes && formulaStealthCheck && !lucky)
                {
                    playerEntity.TallySkill(DFCareer.Skills.Stealth, -1);
                    stealthValue = 0;
                }
                else
                    stealthValue /= 2;
            }
            else if (trainStealth && playerEntity.TimeOfLastStealthCheck != gameMinutes)
            {
                playerEntity.TallySkill(DFCareer.Skills.Stealth, 1);
                playerEntity.TimeOfLastStealthCheck = gameMinutes;
            }
            return stealthValue;
        }

        static void ThiefEffects_OnNewMagicRound()
        {
            if (playerEnterExit.IsPlayerInsideBuilding)
            {
                PlayerGPS.DiscoveredBuilding buildingData = GameManager.Instance.PlayerEnterExit.BuildingDiscoveryData;
                buildingDirectory = GameManager.Instance.StreamingWorld.GetCurrentBuildingDirectory();
                
                bool isShop = RMBLayout.IsShop(buildingData.buildingType);                
                int nowHour = DaggerfallUnity.Instance.WorldTime.Now.Hour;
                int nowMinute = DaggerfallUnity.Instance.WorldTime.Now.Hour;
                bool isBuildingOpen = IsBuildingOpen(buildingData.buildingType);

                if (buildingDirectory.GetBuildingSummary(buildingData.buildingKey, out BuildingSummary buildingSummary))
                {
                    if (GameManager.Instance.PlayerActivate.IsActiveQuestBuilding(buildingSummary, false))
                    {
                        Debug.Log("Active quest in this building");
                    }
                    else if(isBuildingOpen && isShop && (PlayerActivate.closeHours[(int)buildingData.buildingType] == (nowHour + 1)) && nowMinute >= 58)
                    {
                        if (DaggerfallUnity.Instance.WorldTime.Now.Minute >= 59)
                        {
                            DaggerfallUI.MessageBox("The shopkeeper escorts you out the door.");
                            playerEnterExit.TransitionExterior(true);
                        }
                        else
                            DaggerfallUI.MessageBox("'We are closing the shop in a few minutes.'");
                    }
                    else if (isShop && (!isBuildingOpen || playerEntity.CrimeCommitted == PlayerEntity.Crimes.Breaking_And_Entering))
                    {
                        Debug.Log("Shop Is Closed");
                        int stealthValue = playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth);
                        stealthValue -= buildingData.quality * 2;
                        int roll = UnityEngine.Random.Range(0, 101);

                        if (roll > StealthCalc(stealthValue, false, true))
                        {
                            PlayerMotor playerMotor = GameManager.Instance.PlayerMotor;
                            if (playerMotor.IsRunning)
                            {
                                burglaryCounter += Mathf.Clamp(UnityEngine.Random.Range(100, 200) - playerEntity.Stats.LiveLuck, 10, 100);
                            }

                            if (burglaryCounter >= 100)
                            {
                                if (playerEntity.CrimeCommitted < PlayerEntity.Crimes.Breaking_And_Entering)
                                {
                                    DaggerfallUI.MessageBox("'Guards! Guards! We're being robbed!'");
                                    if (playerEntity.MagicalConcealmentFlags != MagicalConcealmentFlags.None && Dice100.FailedRoll(playerEntity.Stats.LiveLuck))
                                    {
                                        playerEntity.MagicalConcealmentFlags = MagicalConcealmentFlags.None;
                                        DaggerfallUI.AddHUDText("Your magical concealment is broken");
                                    }

                                    playerEntity.CrimeCommitted = PlayerEntity.Crimes.Breaking_And_Entering;
                                    playerEntity.SpawnCityGuards(true);
                                }
                            }
                            else if (burglaryCounter == 0)
                            {
                                DaggerfallUI.MessageBox(BurglaryString1());
                                burglaryCounter += Mathf.Clamp(UnityEngine.Random.Range(100, 200) - playerEntity.Stats.LiveLuck, 10, 100);
                            }
                            else if (burglaryCounter < 50)
                            {
                                DaggerfallUI.MessageBox(burglaryString2());
                                burglaryCounter += Mathf.Clamp(UnityEngine.Random.Range(100, 200) - playerEntity.Stats.LiveLuck, 10, 100);
                            }
                            else
                                burglaryCounter += Mathf.Clamp(UnityEngine.Random.Range(100, 200) - playerEntity.Stats.LiveLuck, 10, 100);
                        }
                        
                    }
                }
            }

            DaggerfallUnityItem ringSlot0 = playerEntity.ItemEquipTable.GetItem(EquipSlots.Ring0);
            DaggerfallUnityItem ringSlot1 = playerEntity.ItemEquipTable.GetItem(EquipSlots.Ring1);
            DaggerfallUnityItem markSlot0 = playerEntity.ItemEquipTable.GetItem(EquipSlots.Mark0);
            DaggerfallUnityItem markSlot1 = playerEntity.ItemEquipTable.GetItem(EquipSlots.Mark1);
            DaggerfallUnityItem braceletSlot0 = playerEntity.ItemEquipTable.GetItem(EquipSlots.Bracelet0);
            DaggerfallUnityItem braceletSlot1 = playerEntity.ItemEquipTable.GetItem(EquipSlots.Bracelet1);
            DaggerfallUnityItem bracerSlot0 = playerEntity.ItemEquipTable.GetItem(EquipSlots.Bracer0);
            DaggerfallUnityItem bracerSlot1 = playerEntity.ItemEquipTable.GetItem(EquipSlots.Bracer1);
            DaggerfallUnityItem crystalSlot0 = playerEntity.ItemEquipTable.GetItem(EquipSlots.Crystal0);
            DaggerfallUnityItem crystalSlot1 = playerEntity.ItemEquipTable.GetItem(EquipSlots.Crystal1);

            if (ringSlot0 != null && ringSlot0.TemplateIndex == templateIndex_Ring)
            {
                lockpickingBonus = 20;
            }
            else if (ringSlot1 != null && ringSlot1.TemplateIndex == templateIndex_Ring)
            {
                lockpickingBonus = 20;
            }
            else
            {
                lockpickingBonus = 0;
            }

            if (markSlot0 != null && markSlot0.TemplateIndex == templateIndex_Mark)
            {
                streetwiseBonus = 20;
            }
            else if (markSlot1 != null && markSlot1.TemplateIndex == templateIndex_Mark)
            {
                streetwiseBonus = 20;
            }
            else
            {
                streetwiseBonus = 0;
            }

            if (braceletSlot0 != null && braceletSlot0.TemplateIndex == templateIndex_Bracelet)
            {
                pickpocketBonus = 20;
            }
            else if (braceletSlot1 != null && braceletSlot1.TemplateIndex == templateIndex_Bracelet)
            {
                pickpocketBonus = 20;
            }
            else
            {
                pickpocketBonus = 0;
            }

            if (bracerSlot0 != null && bracerSlot0.TemplateIndex == templateIndex_Bracer)
            {
                climbingBonus = 20;
            }
            else if (bracerSlot1 != null && bracerSlot1.TemplateIndex == templateIndex_Bracer)
            {
                climbingBonus = 20;
            }
            else
            {
                climbingBonus = 0;
            }

            if (crystalSlot0 != null && crystalSlot0.TemplateIndex == templateIndex_Crystal)
            {
                stealthBonus = 20;
            }
            else if (crystalSlot1 != null && crystalSlot1.TemplateIndex == templateIndex_Crystal)
            {
                stealthBonus = 20;
            }
            else
            {
                stealthBonus = 0;
            }


            if (!GameManager.IsGamePaused && playerEntity.CurrentHealth > 0)
            {
                int[] skillMods = new int[DaggerfallSkills.Count];
                skillMods[(int)DFCareer.Skills.Lockpicking] = +lockpickingBonus;
                skillMods[(int)DFCareer.Skills.Streetwise] = +streetwiseBonus;
                skillMods[(int)DFCareer.Skills.Pickpocket] = +pickpocketBonus;
                skillMods[(int)DFCareer.Skills.Climbing] = +climbingBonus;
                skillMods[(int)DFCareer.Skills.Stealth] = +stealthBonus;
                playerEffectManager.MergeDirectSkillMods(skillMods);
            }

        }

        public static bool RestingInOpenShop()
        {
            if (playerEnterExit.IsPlayerInsideBuilding)
            {
                buildingDirectory = GameManager.Instance.StreamingWorld.GetCurrentBuildingDirectory();
                PlayerGPS.DiscoveredBuilding buildingData = GameManager.Instance.PlayerEnterExit.BuildingDiscoveryData;
                if (buildingDirectory.GetBuildingSummary(buildingData.buildingKey, out BuildingSummary buildingSummary))
                {
                    if (!GameManager.Instance.PlayerActivate.IsActiveQuestBuilding(buildingSummary, false))
                    {
                        if (RMBLayout.IsShop(buildingData.buildingType) && BuildingOpenCheck(buildingData.buildingType))
                            return true;
                        else
                            return false;
                    }
                    else
                        return false;
                }
                else
                    return false;
            }
            else
                return false;
        }

        public static bool RestingInClosedShop()
        {
            if (playerEnterExit.IsPlayerInsideBuilding)
            {
                buildingDirectory = GameManager.Instance.StreamingWorld.GetCurrentBuildingDirectory();
                PlayerGPS.DiscoveredBuilding buildingData = GameManager.Instance.PlayerEnterExit.BuildingDiscoveryData;
                if (buildingDirectory.GetBuildingSummary(buildingData.buildingKey, out BuildingSummary buildingSummary))
                {
                    if (!GameManager.Instance.PlayerActivate.IsActiveQuestBuilding(buildingSummary, false))
                    {
                        if (RMBLayout.IsShop(buildingData.buildingType) && !BuildingOpenCheck(buildingData.buildingType))
                            return true;
                        else
                            return false;
                    }
                    else
                        return false;
                }
                else
                    return false;
            }
            else
                return false;
        }

        private static void ShopShelfBurglar(RaycastHit hit)
        {
            PlayerGPS.DiscoveredBuilding buildingData = GameManager.Instance.PlayerEnterExit.BuildingDiscoveryData;
            if (RMBLayout.IsShop(buildingData.buildingType) && !BuildingOpenCheck(buildingData.buildingType))
            {
                int stealth = playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth);
                stealth -= buildingData.quality * 2;
                int diceRoll = UnityEngine.Random.Range(1, 100);
                if (diceRoll > stealth)
                {
                    burglaryCounter += Mathf.Clamp(UnityEngine.Random.Range(100, 300) - playerEntity.Stats.LiveLuck, 10, 100);
                }
                else if (UnityEngine.Random.Range(0, 100) < playerEntity.Stats.LiveLuck && burglaryCounter < 100)
                {
                    playerEntity.TallySkill(DFCareer.Skills.Stealth, 1);
                }
                DaggerfallUnity.Instance.WorldTime.Now.RaiseTime(5);
            }
        }

        static string BurglaryString1()
        {
            int roll = UnityEngine.Random.Range(0, 6);

            switch (roll)
            {
                case 1:
                    return "You bump a flower pot, making it rock back and forth.";
                case 2:
                    return "A startled cat knocks an item to the floor as it scampers away.";
                case 3:
                    return "You stub your toe on a protruding floorboard.";
                case 4:
                    return "You hear footsteps. Is someone awake in the house?";
            }

            return "A floorboard creaks loudly as you step on it.";
        }

        static string burglaryString2()
        {
            int roll = UnityEngine.Random.Range(0, 6);

            switch (roll)
            {
                case 1:
                    return "'Hello? Is someone there?'";
                case 2:
                    return "'I think there's someone in the house!'";
                case 3:
                    return "'Call the guards dear, I think there's someone here.'";
                case 4:
                    return "'Did you hear that?'";
            }

            return "You hear voices from somewhere else in the house.";
        }

        static int StealthArmor(bool stealthMaster)
        {
            DaggerfallUnityItem rArm = playerEntity.ItemEquipTable.GetItem(EquipSlots.RightArm);
            DaggerfallUnityItem lArm = playerEntity.ItemEquipTable.GetItem(EquipSlots.LeftArm);
            DaggerfallUnityItem chest = playerEntity.ItemEquipTable.GetItem(EquipSlots.ChestArmor);
            DaggerfallUnityItem legs = playerEntity.ItemEquipTable.GetItem(EquipSlots.LegsArmor);
            DaggerfallUnityItem head = playerEntity.ItemEquipTable.GetItem(EquipSlots.Head);
            int sound = 0;

            if (chest != null)
            {
                switch (chest.NativeMaterialValue & 0xF00)
                {
                    case (int)ArmorMaterialTypes.Leather:
                        sound += 1;
                        break;
                    case (int)ArmorMaterialTypes.Chain:
                        sound += 10;
                        break;
                    default:
                        sound += 15;
                        break;
                }
            }

            if (legs != null)
            {
                switch (legs.NativeMaterialValue & 0xF00)
                {
                    case (int)ArmorMaterialTypes.Leather:
                        sound += 2;
                        break;
                    case (int)ArmorMaterialTypes.Chain:
                        sound += 5;
                        break;
                    default:
                        sound += 10;
                        break;
                }
            }

            if (lArm != null)
            {
                switch (lArm.NativeMaterialValue & 0xF00)
                {
                    case (int)ArmorMaterialTypes.Leather:
                        sound += 1;
                        break;
                    case (int)ArmorMaterialTypes.Chain:
                        sound += 4;
                        break;
                    default:
                        sound += 6;
                        break;
                }

            }
            if (rArm != null)
            {
                switch (rArm.NativeMaterialValue & 0xF00)
                {
                    case (int)ArmorMaterialTypes.Leather:
                        sound += 1;
                        break;
                    case (int)ArmorMaterialTypes.Chain:
                        sound += 4;
                        break;
                    default:
                        sound += 6;
                        break;
                }
            }
            if (head != null)
            {
                switch (head.NativeMaterialValue & 0xF00)
                {
                    case (int)ArmorMaterialTypes.Leather:
                        sound += 0;
                        break;
                    case (int)ArmorMaterialTypes.Chain:
                        sound += 2;
                        break;
                    default:
                        sound += 5;
                        break;
                }
            }

            if (stealthMaster)
                sound /= 2;

            return sound;
        }

        static bool BuildingOpenCheck(DFLocation.BuildingTypes buildingType)
        {
            int buildingInt = (int)buildingType;
            int hour = DaggerfallUnity.Instance.WorldTime.Now.Hour;
            if (buildingType == DFLocation.BuildingTypes.GuildHall)
                return true;
            if (buildingInt < 18)
                return PlayerActivate.IsBuildingOpen(buildingType);
            else if (buildingInt <= 22)
                return hour < 6 || hour > 18 ? false : true;
            else
                return true;
        }

        static void SneakIntoHouse(PlayerEnterExit.TransitionEventArgs args)
        {
                buildingDirectory = GameManager.Instance.StreamingWorld.GetCurrentBuildingDirectory();
                PlayerGPS.DiscoveredBuilding buildingData = GameManager.Instance.PlayerEnterExit.BuildingDiscoveryData;
                bool buildingOpen = BuildingOpenCheck(buildingData.buildingType);

                if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideBuilding && !buildingOpen && buildingDirectory != null)
                {
                    if (buildingDirectory.GetBuildingSummary(buildingData.buildingKey, out BuildingSummary buildingSummary))
                    {
                        if (GameManager.Instance.PlayerActivate.IsActiveQuestBuilding(buildingSummary, false))
                        {
                            Debug.Log("Active quest in this building");
                        }
                        else
                        {
                            int stealthValue = playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth);
                            stealthValue -= buildingData.quality * 2;
                            int roll = UnityEngine.Random.Range(0, 101);

                            if (roll > StealthCalc(stealthValue - 10, false))
                            {
                                DaggerfallUI.MessageBox(BurglaryString1());
                                burglaryCounter += Mathf.Clamp(UnityEngine.Random.Range(100, 200) - playerEntity.Stats.LiveLuck, 10, 100);
                                playerEntity.TallySkill(DFCareer.Skills.Stealth, 1);
                            }
                        }
                    }
                }
        }

        static void SneakCounter_OnTransitionExterior(PlayerEnterExit.TransitionEventArgs args)
        {
            if (burglaryCounter >= 100)
                playerEntity.SpawnCityGuards(true);
            burglaryCounter = 0;
        }

        public static void TheftItems_OnLootSpawned(object sender, ContainerLootSpawnedEventArgs e)
        {
            if (e.ContainerType == LootContainerTypes.HouseContainers)
            {
                int liveLuck = playerEntity.Stats.LiveLuck;
                int itemNumber = (liveLuck / 10) - UnityEngine.Random.Range(1, 40);
                while (itemNumber > 0)
                {
                    DaggerfallUnityItem item = BurglaryLoot();
                    e.Loot.AddItem(item);
                    itemNumber--;
                    itemNumber--;
                }

                if (Dice100.SuccessRoll(liveLuck / 10))
                {
                    DaggerfallUnityItem item = ItemBuilder.CreateItem(ItemGroups.Currency, (int)Currency.Gold_pieces);
                    item.stackCount = UnityEngine.Random.Range(Mathf.Max(1, liveLuck / 10), liveLuck * 10);
                    e.Loot.AddItem(item);
                }

                if (Dice100.SuccessRoll(liveLuck / 10))
                {
                    DaggerfallUnityItem map = ItemBuilder.CreateItem(ItemGroups.MiscItems, (int)MiscItems.Map);
                }

                if (Dice100.SuccessRoll(liveLuck / 10))
                {
                    int typeRoll = UnityEngine.Random.Range(0, 100);
                    DaggerfallUnityItem itemWpnArm;
                    int level = UnityEngine.Random.Range(1, playerEnterExit.Interior.BuildingData.Quality);
                    if (typeRoll > 50)
                    {
                        itemWpnArm = ItemBuilder.CreateRandomWeapon(level);
                    }
                    else
                    {
                        itemWpnArm = ItemBuilder.CreateRandomArmor(level, playerEntity.Gender, playerEntity.Race);
                    }
                    itemWpnArm.currentCondition = (int)(UnityEngine.Random.Range(0.3f, 0.75f) * itemWpnArm.maxCondition);
                    e.Loot.AddItem(itemWpnArm);
                }
            }
        }

        static DaggerfallUnityItem BurglaryLoot()
        {
            DaggerfallUnityItem item = null;
            int qualityRoll = playerEnterExit.Interior.BuildingData.Quality + UnityEngine.Random.Range(0, 90);
            if (qualityRoll > 70)
            {
                item = ItemBuilder.CreateRandomGem();
            }
            else if (qualityRoll > 30)
            {
                item = ItemBuilder.CreateRandomJewellery();
            }
            else if (qualityRoll > 10)
            {
                item = ItemBuilder.CreateRandomReligiousItem();
            }
            else
            {
                int roll = UnityEngine.Random.Range(0, 10);
                if (roll > 8)
                    item = ItemBuilder.CreateRandomBook();
                else if (roll > 5)
                    item = ItemBuilder.CreateRandomPotion();
                else if (roll > 2)
                    item = ItemBuilder.CreateRandomIngredient();
                else
                    item = ItemBuilder.CreateRandomClothing(playerEntity.Gender, playerEntity.Race);
            }

            item.currentCondition = (int)UnityEngine.Random.Range(0.3f, 1.1f) * item.maxCondition;
            if (item.currentCondition > item.maxCondition)
                item.currentCondition = item.maxCondition;

            return item;
        }

        private static string CrimeComitted()
        {
            switch((int)playerEntity.CrimeCommitted)
            {
                case 1:
                    return "attempted breaking entering";
                case 2:
                    return "trespassing";
                case 3:
                    return "breaking and entering";
                case 4:
                    return "assault";
                case 5:
                    return "murder";
                case 6:
                    return "tax evasion";
                case 7:
                    return "criminal conspiracy";
                case 8:
                    return "vagrancy";
                case 9:
                    return "smuggling";
                case 10:
                    return "piracy";
                case 11:
                    return "high treason";
                case 12:
                    return "pickpocketing";
                case 13:
                    return "theft";
                case 14:
                    return "treason";
                case 15:
                    return "loan default";
                default:
                    return "being a " + playerEntity.Race;
            }
        }

        static bool IsBuildingOpen(DFLocation.BuildingTypes buildingType)
        {
            if (RMBLayout.IsResidence(buildingType))
                return DaggerfallUnity.Instance.WorldTime.Now.IsDay;
            else
                return PlayerActivate.IsBuildingOpen(buildingType);
        }

        //static void HouseItems_OnTransitionInterior(PlayerEnterExit.TransitionEventArgs args)
        //{
        //    QuestMarker spawnMarker;
        //    Vector3 buildingOrigin;
        //    DFLocation.BuildingTypes buildingType = GameManager.Instance.PlayerEnterExit.BuildingType;
        //    bool result = QuestMachine.Instance.GetCurrentLocationQuestMarker(out spawnMarker, out buildingOrigin);

        //    if (!SaveLoadManager.Instance.LoadInProgress && !result && (spawnMarker.targetResources == null || spawnMarker.targetResources.Count == 0))
        //    {

        //        if (RMBLayout.IsResidence(buildingType) && buildingType != DFLocation.BuildingTypes.Ship && !DaggerfallBankManager.IsHouseOwned(playerEnterExit.BuildingDiscoveryData.buildingKey))
        //        {
        //            Vector3 markerPos;
        //            if (GameManager.Instance.PlayerEnterExit.Interior.FindMarker(out markerPos, (DaggerfallInterior.InteriorMarkerTypes)MarkerTypes.QuestItem, true))
        //            {
        //                Debug.Log("Thief Overhaul: QuestMarker true");
        //                PlaceHouseLoot(markerPos);
        //            }
        //            else
        //                Debug.Log("Thief Overhaul: QuestMarker false");
        //        }
        //    }
        //}

        //static void PlaceHouseLoot(Vector3 markerPos)
        //{
        //    int tex = 21; //UnityEngine.Random.Range(21, 26);

        //    houseLootPile = GameObjectHelper.CreateDaggerfallBillboardGameObject(205, tex, null); ;

        //    houseLootPile.transform.SetPositionAndRotation(markerPos, Quaternion.FromToRotation(markerPos + Vector3.up, markerPos));

        //    houseLootPile.SetActive(true);
        //}

        //public static void HouseLootClicked(RaycastHit hit)
        //{
        //    Debug.Log("ID = " + hit.transform.gameObject.GetInstanceID().ToString());
        //    Debug.Log("ID = " + houseLootPile.GetInstanceID().ToString());
        //    if (hit.transform.gameObject.GetInstanceID() == houseLootPile.GetInstanceID())
        //    {
        //        DaggerfallUI.MessageBox("House Loot Pile");
        //    }
        //}

        public static void RegisterRWCommands()
        {
            Debug.Log("[Skullduggery] Trying to register console commands.");
            try
            {
                ConsoleCommandsDatabase.RegisterCommand(AddThiefItems.name, AddThiefItems.description, AddThiefItems.usage, AddThiefItems.Execute);
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format("Error Registering RealisticWagon Console commands: {0}", e.Message));
            }
        }
        private static class AddThiefItems
        {
            public static readonly string name = "add_thiefitems";
            public static readonly string description = "Adds all thief items to player inventory";
            public static readonly string usage = "add_thiefitems";

            public static string Execute(params string[] args)
            {
                ItemCollection playerItems = GameManager.Instance.PlayerEntity.Items;
                playerItems.AddItem(ItemBuilder.CreateItem(ItemGroups.MiscItems, ThiefOverhaul.templateIndex_Ring));
                playerItems.AddItem(ItemBuilder.CreateItem(ItemGroups.MiscItems, ThiefOverhaul.templateIndex_Mark));
                playerItems.AddItem(ItemBuilder.CreateItem(ItemGroups.MiscItems, ThiefOverhaul.templateIndex_Bracelet));
                playerItems.AddItem(ItemBuilder.CreateItem(ItemGroups.MiscItems, ThiefOverhaul.templateIndex_Bracer));
                playerItems.AddItem(ItemBuilder.CreateItem(ItemGroups.MiscItems, ThiefOverhaul.templateIndex_Crystal));
                return "Thief items added to inventory";
            }
        }
    }
}