using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceShared;
using SpaceShared.UI;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace GenericModConfigMenu.Framework
{
    internal class ModConfigMenu : IClickableMenu
    {
        /*********
        ** Fields
        *********/
        private RootElement Ui;
        private Table Table;

        /// <summary>The number of field rows to offset when scrolling a config menu.</summary>
        private readonly int ScrollSpeed;

        /// <summary>Open the config UI for a specific mod.</summary>
        private readonly Action<IManifest, int> OpenModMenu;

        private static bool InGame => Context.IsWorldReady;

        private readonly List<Label> LabelsWithTooltips = [];

        private readonly int KeybindingsTexWidth;

        /// <summary> The filter field (search bar) object. </summary>
        private readonly PerScreen<TextBox> FilterField = new();
        private readonly IEnumerable<ModConfig> AllModConfigs;
        private readonly int FilterPositionX;
        private readonly int FilterPositionY;
        private readonly bool FilterAutofocus;
        private string LastFilteredText;
        /// <summary>The filter textbox area.</summary>
        private Rectangle FilterFieldBounds;

        /*********
        ** Accessors
        *********/
        /// <summary>The scroll position, represented by the row index at the top of the visible area.</summary>
        public int ScrollRow
        {
            get => this.Table.Scrollbar.TopRow;
            set => this.Table.Scrollbar.ScrollTo(value);
        }


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="scrollSpeed">The number of field rows to offset when scrolling a config menu.</param>
        /// <param name="openModMenu">Open the config UI for a specific mod.</param>
        /// <param name="openKeybindsMenu">Open the menu to configure mod keybinds.</param>
        /// <param name="keybindsTexture">The icon texture for the keybinds menu.</param>
        /// <param name="configs">The mod configurations to display.</param>
        /// <param name="scrollTo">The initial scroll position, represented by the row index at the top of the visible area.</param>
        public ModConfigMenu(int scrollSpeed, Action<IManifest, int> openModMenu, Action<int> openKeybindingsMenu, ModConfigManager configs, Texture2D keybindingsTex, int? scrollTo = null,
            bool filterAutofocus = true, int filterPositionX = 0, int filterPositionY = 0)
        public ModConfigMenu(int scrollSpeed, Action<IManifest, int> openModMenu, Action<int> openKeybindsMenu, ModConfigManager configs, Texture2D keybindsTexture, int? scrollTo = null)
        {
            this.ScrollSpeed = scrollSpeed;
            this.OpenModMenu = openModMenu;
            this.FilterAutofocus = filterAutofocus;
            this.FilterPositionX = filterPositionX;
            this.FilterPositionY = filterPositionY;
            this.KeybindingsTexWidth = keybindingsTex.Width;

            // init UI
            this.Ui = new RootElement();
            this.Table = new Table
            {
                RowHeight = 50,
                LocalPosition = new Vector2((Game1.uiViewport.Width - 800) / 2, 64),
                Size = new Vector2(800, Game1.uiViewport.Height - 128)
            };
            this.SetupFilterField();
            this.AllModConfigs = configs.GetAll();

            var filteredConfigs = this.GetFilteredConfigs();
            this.BuildModListTable(filteredConfigs);

            this.Ui.AddChild(this.Table);
            var button = new Button(keybindingsTex)
            {
                LocalPosition = this.Table.LocalPosition - new Vector2(this.KeybindingsTexWidth / 2 + 32, 0),
                Callback = _ => openKeybindingsMenu(this.ScrollRow),
            };
            this.Ui.AddChild(button);

            if (Constants.TargetPlatform == GamePlatform.Android)
                this.initializeUpperRightCloseButton();
            else
                this.upperRightCloseButton = null;

            if (scrollTo != null)
                this.ScrollRow = scrollTo.Value;

            if (!InGame)
            {
                // This hack lets gamepad cursor movement work without a harmony patch
                Mod.instance.Helper.Reflection.GetField<bool>(Game1.activeClickableMenu, "titleInPosition").SetValue(false);
            }
        }

        private void SetupFilterField()
        {
            if (this.FilterField.Value is null)
            {
                this.FilterField.Value = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Game1.textColor)
                {
                    Text = ""
                };

                this.FilterField.Value.OnEnterPressed += sender => sender.Selected = false;
                this.FilterField.Value.OnTabPressed += sender => sender.Selected = false;
            }
            var fieldPosition = new Vector2(this.FilterField.Value.Width / 2 + 128 + FilterPositionX, this.FilterField.Value.Height - 125 + FilterPositionY);
            this.FilterField.Value.X = (int)(this.Table.LocalPosition.X - fieldPosition.X);
            this.FilterField.Value.Y = (int)(this.Table.LocalPosition.Y - fieldPosition.Y);
            this.FilterFieldBounds = new Rectangle(this.FilterField.Value.X, this.FilterField.Value.Y + 4, this.FilterField.Value.Width, 12 * Game1.pixelZoom);
            this.FilterField.Value.Selected = this.FilterAutofocus;
        }

        private void BuildModListTable(IEnumerable<ModConfig> configs)
        {
            this.LabelsWithTooltips.Clear();
            // editable mods section
            {
                // heading
                var heading = new Label
                {
                    String = I18n.List_EditableHeading(),
                    Bold = true
                };
                heading.LocalPosition = new Vector2((800 - heading.Measure().X) / 2, heading.LocalPosition.Y);
                this.Table.AddRow([heading]);

                // mod list
                {
                    ModConfig[] editable = configs
                        .Where(entry => entry.AnyEditableInGame || !InGame)
                        .OrderBy(entry => entry.ModName)
                        .ToArray();

                    foreach (ModConfig entry in editable)
                    {
                        Label label = new Label
                        {
                            String = entry.ModName,
                            UserData = entry.ModManifest.Description,
                            Callback = _ => this.ChangeToModPage(entry.ModManifest)
                        };
                        this.Table.AddRow([label]);
                        this.LabelsWithTooltips.Add(label);
                    }
                }
            }

            // non-editable mods heading
            {
                ModConfig[] notEditable = configs
                    .Where(entry => !entry.AnyEditableInGame && InGame)
                    .OrderBy(entry => entry.ModName)
                    .ToArray();

                if (notEditable.Any())
                {
                    // heading
                    var heading = new Label
                    {
                        String = I18n.List_NotEditableHeading(),
                        Bold = true
                    };
                    this.Table.AddRow([]);
                    this.Table.AddRow([heading]);

                    // mod list
                    foreach (ModConfig entry in notEditable)
                    {
                        Label label = new()
                        {
                            String = entry.ModName,
                            UserData = entry.ModManifest.Description,
                            IdleTextColor = Color.Black * 0.4f,
                            HoverTextColor = Color.Black * 0.4f
                        };

                        this.Table.AddRow(new Element[] { label });
                        LabelsWithTooltips.Add(label);
                    }
                }
            }

            this.Ui.AddChild(this.Table);

            var button = new Button(keybindsTexture)
            {
                LocalPosition = this.Table.LocalPosition - new Vector2( keybindsTexture.Width / 2 + 32, 0 ),
                Callback = _ => openKeybindsMenu( this.ScrollRow),
            };
            this.Ui.AddChild(button);

            if (Constants.TargetPlatform == GamePlatform.Android)
                this.initializeUpperRightCloseButton();
            else
                this.upperRightCloseButton = null;

            if (scrollTo != null)
                this.ScrollRow = scrollTo.Value;

            if (!InGame)
            {
                // This hack lets gamepad cursor movement work without a harmony patch
                Mod.instance.Helper.Reflection.GetField<bool>(Game1.activeClickableMenu, "titleInPosition").SetValue(false);
            }
        }

        /// <inheritdoc />
        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (this.upperRightCloseButton?.containsPoint(x, y) == true && this.readyToClose())
            {
                if (playSound)
                    Game1.playSound("bigDeSelect");

                Mod.ActiveConfigMenu = null;
            }
            if (this.FilterFieldBounds.Contains(x, y) && this.FilterField.Value.Selected == false)
            {
                this.FilterField.Value.Selected = true;
            }
        }

        /// <inheritdoc />
        public override void receiveScrollWheelAction(int direction)
        {
            this.Table.Scrollbar.ScrollBy(direction / -this.ScrollSpeed);
        }

        private int scrollCounter = 0;
        /// <inheritdoc />
        public override void update(GameTime time)
        {
            base.update(time);
            this.Ui.Update();

            if (Game1.input.GetGamePadState().ThumbSticks.Right.Y != 0)
            {
                if (++this.scrollCounter == 5)
                {
                    this.scrollCounter = 0;
                    this.Table.Scrollbar.ScrollBy(Math.Sign(Game1.input.GetGamePadState().ThumbSticks.Right.Y) * 120 / -this.ScrollSpeed);
                }
            }
            else this.scrollCounter = 0;
        }

        /// <inheritdoc />
        public override void draw(SpriteBatch b)
        {
            base.draw(b);
            b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), new Color(0, 0, 0, 192));
            this.Ui.Draw(b);
            this.upperRightCloseButton?.draw(b); // bring it above the backdrop
            if (InGame)
                this.drawMouse(b);
            if (Constants.TargetPlatform != GamePlatform.Android && this.GetChildMenu() == null)
            {
                this.FilterField.Value.Draw(b);
                foreach (var label in this.LabelsWithTooltips)
                {
                    if (!label.Hover || label.UserData == null)
                        continue;
                    string text = (string)label.UserData;
                    if (text != null && !text.Contains('\n'))
                        text = Game1.parseText(text, Game1.smallFont, 800);
                    string title = label.String;
                    if (title != null && !title.Contains('\n'))
                        title = Game1.parseText(title, Game1.dialogueFont, 800);
                    drawToolTip(b, text, title, null);
                }
            }
        }

        /// <inheritdoc />
        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            var oldUi = this.Ui;

            this.Ui = new RootElement();

            Vector2 newSize = new Vector2(800, Game1.uiViewport.Height - 128);
            this.Table.LocalPosition = new Vector2((Game1.uiViewport.Width - 800) / 2, 64);
            foreach (Element opt in this.Table.Children)
                opt.LocalPosition = new Vector2(newSize.X / (this.Table.Size.X / opt.LocalPosition.X), opt.LocalPosition.Y);
            this.SetupFilterField();
            this.Table.Size = newSize;
            this.Table.Scrollbar.Update();
            this.Ui.AddChild(this.Table);

            var b = oldUi.Children.First(e => e is Button);
            oldUi.RemoveChild(b);
            b.LocalPosition = this.Table.LocalPosition - new Vector2(this.KeybindingsTexWidth / 2 + 32, 0);
            this.Ui.AddChild(b);
        }

        /// <inheritdoc/>
        public override bool overrideSnappyMenuCursorMovementBan()
        {
            return true;
        }

        public override void receiveKeyPress(Keys key)
        {
            if (!this.FilterField.Value.Selected || Game1.options.gamepadControls)
                base.receiveKeyPress(key);

            if (key == Keys.Escape)
            {
                this.FilterField.Value.Selected = false;
                base.receiveKeyPress(key);
            }
            if (key == Keys.Enter && this.Table.RowCount == 2)
            {
                // If there's only one mod, open it
                ((Label)this.Table.Children.Last()).Callback?.Invoke(this.Table.Children.Last());
            }
            // Ignores action, user just pressing random buttons.
            if (this.FilterField.Value.Text == this.LastFilteredText) return;
            this.ApplyFilter();
        }


        /*********
        ** Private methods
        *********/
        private void ChangeToModPage(IManifest modManifest)
        {
            Log.Trace("Changing to mod config page for mod " + modManifest.UniqueID);
            Game1.playSound("bigSelect");

            this.OpenModMenu(modManifest, this.ScrollRow);
        }

        private void ApplyFilter()
        {
            var FilteredConfigs = this.GetFilteredConfigs();
            this.Ui.RemoveChild(this.Table);
            this.Table = new Table
            {
                RowHeight = 50,
                LocalPosition = new Vector2((Game1.uiViewport.Width - 800) / 2, 64),
                Size = new Vector2(800, Game1.uiViewport.Height - 128)
            };
            this.BuildModListTable(FilteredConfigs);
            this.Ui.AddChild(this.Table);
            this.LastFilteredText = this.FilterField.Value.Text;
        }
        private IEnumerable<ModConfig> GetFilteredConfigs()
        {
            string filterString = this.FilterField.Value?.Text ?? "";

            IEnumerable<ModConfig> filteredConfigs = this.AllModConfigs
                .Where(entry => entry.ModName.ToLower().Contains(filterString.ToLower()));
            return filteredConfigs;
        }

    }
}
