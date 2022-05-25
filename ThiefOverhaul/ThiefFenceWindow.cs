
using System;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallConnect;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Utility.AssetInjection;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Guilds;
using System.Linq;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Utility;

namespace ThiefOverhaul
{
    public class FenceWindow : DaggerfallPopupWindow
    {
        #region UI Rects

        Rect joinButtonRect = new Rect(5, 5, 120, 7);
        Rect talkButtonRect = new Rect(5, 14, 120, 7);
        Rect buyButtonRect = new Rect(5, 23, 120, 7);
        Rect sellButtonRect = new Rect(5, 32, 120, 7);
        Rect exitButtonRect = new Rect(44, 42, 43, 15);

        #endregion

        #region UI Controls

        Panel mainPanel = new Panel();
        protected Button joinButton;
        protected Button talkButton;
        protected Button buyButton;
        protected Button sellButton;
        protected Button exitButton;
        protected TextLabel joinLabel = new TextLabel();
        protected TextLabel talkLabel = new TextLabel();
        protected TextLabel buyLabel = new TextLabel();
        protected TextLabel sellLabel = new TextLabel();

        #endregion

        #region UI Textures

        protected Texture2D baseTexture;

        #endregion

        #region Fields

        const string baseTextureName = "BLANKMENU_4";

        protected IGuild guild;
        protected GuildManager guildManager;
        protected FactionFile.GuildGroups guildGroup;
        protected StaticNPC serviceNPC;
        protected GuildNpcServices npcService;
        protected int buildingFactionId;

        bool isCloseWindowDeferred = false;
        bool isTalkWindowDeferred = false;
        bool isBuyDeferred = false;
        bool isSellDeferred = false;


        #endregion

        #region Constructors

        public FenceWindow(IUserInterfaceManager uiManager, StaticNPC npc, FactionFile.GuildGroups guildGroup)
            : base(uiManager)
        {
            ParentPanel.BackgroundColor = Color.clear;
            guildManager = GameManager.Instance.GuildManager;

            serviceNPC = npc;
            npcService = GuildNpcServices.TG_SellMagicItems;
            this.guildGroup = guildGroup;
            guild = guildManager.GetGuild(guildGroup, buildingFactionId);
            Debug.Log("guild = " + guild.ToString());
        }

        #endregion

        #region Setup Methods

        protected override void Setup()
        {
            base.Setup();

            // Load all textures
            Texture2D tex;
            TextureReplacement.TryImportTexture(baseTextureName, true, out tex);
            Debug.Log("Texture is:" + tex.ToString());
            baseTexture = tex;

            // Create interface panel
            mainPanel.HorizontalAlignment = HorizontalAlignment.Center;
            mainPanel.VerticalAlignment = VerticalAlignment.Middle;
            mainPanel.BackgroundTexture = baseTexture;
            mainPanel.Position = new Vector2(0, 50);
            mainPanel.Size = new Vector2(130, 60);

            // join button
            joinLabel.Position = new Vector2(0, 1);
            joinLabel.TextColor = Color.gray;
            joinLabel.ShadowPosition = Vector2.one;
            joinLabel.HorizontalAlignment = HorizontalAlignment.Center;
            joinLabel.Text = "JOIN GUILD";
            joinButton = DaggerfallUI.AddButton(joinButtonRect, mainPanel);
            joinButton.Components.Add(joinLabel);
            joinButton.OnMouseClick += JoinButton_OnMouseClick;
            //roomButton.Hotkey = DaggerfallShortcut.GetBinding(DaggerfallShortcut.Buttons.TavernRoom);

            // Talk button
            talkLabel.Position = new Vector2(0, 10);
            talkLabel.ShadowPosition = Vector2.one;
            talkLabel.HorizontalAlignment = HorizontalAlignment.Center;
            talkLabel.Text = "TALK";
            talkButton = DaggerfallUI.AddButton(talkButtonRect, mainPanel);
            talkButton.Components.Add(talkLabel);
            talkButton.OnMouseClick += TalkButton_OnMouseClick;
            //talkButton.Hotkey = DaggerfallShortcut.GetBinding(DaggerfallShortcut.Buttons.TavernTalk);
            //talkButton.OnKeyboardEvent += TalkButton_OnKeyboardEvent;

            // Buy Items button
            buyLabel.Position = new Vector2(0, 10);
            buyLabel.ShadowPosition = Vector2.one;
            buyLabel.HorizontalAlignment = HorizontalAlignment.Center;
            buyLabel.Text = "BUY ITEMS";
            buyButton = DaggerfallUI.AddButton(buyButtonRect, mainPanel);
            buyButton.Components.Add(buyLabel);
            buyButton.OnMouseClick += BuyButton_OnMouseClick;
            //foodButton.Hotkey = DaggerfallShortcut.GetBinding(DaggerfallShortcut.Buttons.TavernFood);
            //buyButton.OnKeyboardEvent += BuyButton_OnKeyboardEvent;

            // Sell Items button
            sellLabel.Position = new Vector2(0, 10);
            sellLabel.ShadowPosition = Vector2.one;
            sellLabel.HorizontalAlignment = HorizontalAlignment.Center;
            sellLabel.Text = "SELL ITEMS";
            sellButton = DaggerfallUI.AddButton(sellButtonRect, mainPanel);
            sellButton.Components.Add(sellLabel);
            sellButton.OnMouseClick += SellButton_OnMouseClick;
            //drinksButton.Hotkey = DaggerfallShortcut.GetBinding(DaggerfallShortcut.Buttons.TavernFood);
            //sellButton.OnKeyboardEvent += SellButton_OnKeyboardEvent;

            // Exit button
            exitButton = DaggerfallUI.AddButton(exitButtonRect, mainPanel);
            exitButton.OnMouseClick += ExitButton_OnMouseClick;
            //exitButton.Hotkey = DaggerfallShortcut.GetBinding(DaggerfallShortcut.Buttons.TavernExit);
            //exitButton.OnKeyboardEvent += ExitButton_OnKeyboardEvent;

            NativePanel.Components.Add(mainPanel);
        }

        #endregion

        #region Event Handlers

        private void JoinButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            Debug.Log("Join");
        }

        private void TalkButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
            GameManager.Instance.TalkManager.TalkToStaticNPC(serviceNPC);
            Debug.Log("Talk");
        }

        private void BuyButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {            
            DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
            if (!guild.CanAccessService(Services.GetService(npcService)))
            {
                DaggerfallMessageBox msgBox = new DaggerfallMessageBox(uiManager, this);
                msgBox.SetTextTokens(DaggerfallUnity.Instance.TextProvider.GetRandomTokens(DaggerfallGuildServicePopupWindow.InsufficientRankId));
                msgBox.ClickAnywhereToClose = true;
                msgBox.Show();
            }
            else
            {
                CloseWindow();
                DaggerfallTradeWindow tradeWindow;
                tradeWindow = (DaggerfallTradeWindow)UIWindowFactory.GetInstanceWithArgs(UIWindowType.Trade, new object[] { uiManager, this, DaggerfallTradeWindow.WindowModes.Buy, guild });
                tradeWindow.MerchantItems = GetThiefItems();
                uiManager.PushWindow(tradeWindow);
            }            
        }

        private void SellButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            Debug.Log("Sell");
            DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
            if (!guild.CanAccessService(Services.GetService(npcService)))
            {
                DaggerfallMessageBox msgBox = new DaggerfallMessageBox(uiManager, this);
                msgBox.SetTextTokens(DaggerfallUnity.Instance.TextProvider.GetRandomTokens(DaggerfallGuildServicePopupWindow.InsufficientRankId));
                msgBox.ClickAnywhereToClose = true;
                msgBox.Show();
            }
            else
            {
                CloseWindow();
                uiManager.PushWindow(UIWindowFactory.GetInstanceWithArgs(UIWindowType.Trade, new object[] { uiManager, this, DaggerfallTradeWindow.WindowModes.SellMagic, guild }));
            }
            
        }

        private void ExitButton_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            Debug.Log("Exit");
            DaggerfallUI.Instance.PlayOneShot(SoundClips.ButtonClick);
            CloseWindow();
        }


        protected virtual ItemCollection GetThiefItems()
        {
            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
            ItemCollection items = new ItemCollection();
            //int numOfItems = (buildingDiscoveryData.quality / 2) + 1;
            //int numOfItems = (playerEntity.Stats.LiveLuck / 10) + UnityEngine.Random.Range(1, 5);

            //int seed = (int)(DaggerfallUnity.Instance.WorldTime.DaggerfallDateTime.ToClassicDaggerfallTime() / DaggerfallDateTime.MinutesPerDay);
            //UnityEngine.Random.InitState(seed);

            //for (int i = 0; i <= numOfItems; i++)
            //{
            //    DaggerfallUnityItem thiefItem;

            //    thiefItem = ItemBuilder.CreateItem(ItemGroups.MiscItems, ThiefOverhaul.templateIndex_Ring);
            //    items.AddItem(thiefItem);
            //}
            items.AddItem(ItemBuilder.CreateItem(ItemGroups.MiscItems, ThiefOverhaul.templateIndex_Ring));
            items.AddItem(ItemBuilder.CreateItem(ItemGroups.MiscItems, ThiefOverhaul.templateIndex_Mark));
            items.AddItem(ItemBuilder.CreateItem(ItemGroups.MiscItems, ThiefOverhaul.templateIndex_Bracelet));
            items.AddItem(ItemBuilder.CreateItem(ItemGroups.MiscItems, ThiefOverhaul.templateIndex_Bracer));
            items.AddItem(ItemBuilder.CreateItem(ItemGroups.MiscItems, ThiefOverhaul.templateIndex_Crystal));

            Debug.Log("GetThiefItems finished.");
            return items;
        }

        #endregion
    }

}