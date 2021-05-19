using DaggerfallConnect;
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

namespace ThiefOverhaul
{
    public class ThiefOverhaul : MonoBehaviour
    {
        static Mod mod;
        static ThiefOverhaul instance;

        static DaggerfallUnity dfUnity = DaggerfallUnity.Instance;
        static PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
        static PlayerEnterExit playerEnterExit = GameManager.Instance.PlayerEnterExit;
        static int burglaryCounter = 0;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            Debug.Log("[ThiefOverhaul] Mod Init.");
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<ThiefOverhaul>();

            EntityEffectBroker.OnNewMagicRound += ThiefEffects_OnNewMagicRound;
            PlayerEnterExit.OnTransitionExterior += SneakCounter_OnTransitionExterior;

        }

        void Awake()
        {
            mod.IsReady = true;
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
                            "Halt! You are under arrest for " + playerEntity.CrimeCommitted.ToString() + ".",
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
            int merchantile = playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Mercantile) / 10;
            int cost = Mathf.Max((110 * crimeLevel) / merchantile, 0);

            return cost;
        }

        private static void ArrestPrompt()
        {
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

        static int StealthCalc(int stealthValue, bool stealthCheck)
        {
            uint gameMinutes = DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime();
            PlayerMotor playerMotor = GameManager.Instance.PlayerMotor;
            bool stealthMaster = stealthValue >= 100;

            stealthValue += (playerEntity.Stats.LiveLuck / 10) - 5;
            stealthValue += (playerEntity.Stats.LiveAgility / 5) - 10;
            stealthValue -= Armor(stealthMaster);

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
                stealthValue = 0;
                if (playerEntity.TimeOfLastStealthCheck == gameMinutes && stealthCheck)
                {
                    playerEntity.TallySkill(DFCareer.Skills.Stealth, -1);
                }
            }

            return stealthValue;
        }

        static void ThiefEffects_OnNewMagicRound()
        {
            Debug.Log("[ThiefOverhaul] Magic Round");
            if (playerEnterExit.IsPlayerInsideBuilding)
            {
                PlayerGPS.DiscoveredBuilding buildingData = GameManager.Instance.PlayerEnterExit.BuildingDiscoveryData;
                if (RMBLayout.IsShop(buildingData.buildingType) && !PlayerActivate.IsBuildingOpen(buildingData.buildingType))
                {
                    int stealthValue = playerEntity.Skills.GetLiveSkillValue(DFCareer.Skills.Stealth);
                    stealthValue -= buildingData.quality * 2;

                    if (Dice100.FailedRoll(StealthCalc(stealthValue, false)))
                    {
                        if (burglaryCounter >= 100)
                        {
                            DaggerfallUI.MessageBox("'Guards! Guards! We're being robbed!'");
                            playerEntity.CrimeCommitted = PlayerEntity.Crimes.Breaking_And_Entering;
                            playerEntity.SpawnCityGuards(true);
                        }
                        else if (burglaryCounter == 0)
                        {
                            DaggerfallUI.MessageBox(burglaryString1());
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

        static string burglaryString1()
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

        static int Armor(bool stealthMaster)
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

        static void SneakCounter_OnTransitionExterior(PlayerEnterExit.TransitionEventArgs args)
        {
            burglaryCounter = 0;
        }
    }
}