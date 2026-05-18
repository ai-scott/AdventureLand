using Godot;
using System;

namespace AdventureLandPrototype;

/// <summary>
/// Inventory UI — Adventure Land character sheet.
///
/// **Layout is scene-authored** in <c>scenes/ui/InventoryUI.tscn</c>. Edit
/// positions / colors / fonts in the Godot editor; this script only binds
/// the data (player name, stat values, item icons, grid cells, etc.) to
/// the scene's pre-built nodes.
///
/// What stays in code:
///  * Heart row contents (count varies with player MaxHealth)
///  * 5×5 grid cell spawning (parented to the scene's Grid node)
///  * Slot cursor positioning during keyboard nav
///  * Live paper-doll preview SubViewport (mirrors player SpriteLayers)
///  * Hair / Skin cycler index state and label updates
///  * Item details panel population (name, desc, stats, action)
///  * Equipment-slot icon swap when items are equipped
///
/// Toggle with <c>I</c> / <c>Tab</c> / <c>Esc</c>. Arrow keys move the grid
/// cursor; <c>Space</c> equips / uses; <c>Z</c> unequips.
/// </summary>
public partial class InventoryUI : CanvasLayer
{
	/// <summary>Autoload-singleton handle for triggers that need to open the
	/// inventory programmatically (mirrors, menu items, etc.).</summary>
	public static InventoryUI Instance { get; private set; }

	// 6 columns × 5 rows = 30 slots. Cells 0-24 are the original 5×5 in
	// row-major order (cols 0-4); Cells 25-29 are the 6th column,
	// numbered by row (Cell25 = row 0, Cell29 = row 4). MapSlotToCellIndex
	// translates the row-major _selectedSlot into the right scene-cell name.
	private const int GridCols = 6;
	private const int GridRows = 5;

	// Equipment categories shown in the Appearance grid (slot index 0..5).
	// Hair lives separately in the center column's hair cycler.
	private static readonly ItemData.ItemCategory[] AppearanceCategories =
	{
		ItemData.ItemCategory.Head, ItemData.ItemCategory.Hand,
		ItemData.ItemCategory.Neck, ItemData.ItemCategory.Body,
		ItemData.ItemCategory.Legs, ItemData.ItemCategory.Boot,
	};

	// Per-slot placeholder PNG index aligned to AppearanceCategories order
	// (Head, Hand, Neck, Body, Legs, Boot). Source files swap visuals from
	// the labelled stat: equipslot_2.png is actually a glove (Hand), and
	// equipslot_3.png is actually a neck/scarf shape — so Hand→2, Neck→3
	// rather than the C3 PNG numbering by slot index.
	private static readonly int[] EquipPlaceholderIndex = { 1, 2, 3, 4, 5, 6 };

	// Grid cell geometry. The cells themselves are spawned at runtime into
	// the scene's "Grid" node — we pull this from the scene via the Grid
	// node's offsets so changes in the editor flow through.
	private const int CellSize = 36;
	private const int CellGap = 4;

	// ---- Scene-bound nodes (looked up via GetNode in BindNodes) ----
	private Control _panel;
	private Label _playerNameLabel;
	private HBoxContainer _heartRow;
	private Button _closeBtn;

	private readonly Label[] _abilityValues = new Label[4];
	// Same handle-list shape as before but the icons now live as scene
	// children of AppearanceSlotN/Chip/Icon.
	private readonly TextureRect[] _appearanceIcons = new TextureRect[6];
	// The AppearanceSlotN Control nodes themselves — clickable to jump the
	// details panel to whatever's equipped in that category.
	private readonly Control[] _appearanceSlots = new Control[6];

	private TextureRect _previewRect;
	private Label _hairLabel;
	private Label _hairColorLabel;
	private Label _skinLabel;
	private Button _hairLeftBtn, _hairRightBtn;
	private Button _hairColorLeftBtn, _hairColorRightBtn;
	private Button _skinLeftBtn, _skinRightBtn;

	private Label _detailsName;
	private Label _detailsDesc;
	private Label _detailsAction;
	private HBoxContainer _detailsStats;
	// Scene-authored 48×48 item icon next to the description (left-aligned
	// with description column at offset_left=572). Hidden until an item is
	// focused.
	private TextureRect _detailsItemIcon;
	// Code-spawned cursor frame that wraps the DetailsItemIcon — same gold-
	// bordered visual as the inventory slot cursor, used as a "this is the
	// item you're looking at" indicator for the top-right preview.
	private TextureRect _detailsItemCursor;
	// Chip button overlaying the DetailsAction Label slot — replaces the
	// "Z to unequip" / "Space to equip" text with a styled clickable chip.
	private Button _detailsActionBtn;

	private Label _gemLabel;
	private Control _gridContainer;
	private TextureRect _slotCursor;

	// ---- Code-spawned nodes (dynamic content) ----
	private SubViewport _previewViewport;
	private Node2D _previewLayers;
	private Node _sourceLayers;
	// Parallel Sprite2D lists between source (player SpriteLayers) and the
	// preview duplicates. Cached at build time so the mirror doesn't have
	// to count-match against GetChildren() — the source also contains the
	// MSCA AnimationPlayer + AnimationTree, which makes the count mismatch
	// permanently and skips every mirror tick.
	private readonly System.Collections.Generic.List<Sprite2D> _sourceSprites = new();
	private readonly System.Collections.Generic.List<Sprite2D> _previewSprites = new();
	private readonly TextureRect[] _slotIcons = new TextureRect[Inventory.SlotCount];
	private readonly Label[] _slotQtyLabels = new Label[Inventory.SlotCount];

	// Cached grid origin pulled from the scene's Grid node — refresh in
	// RebuildGridCells whenever the scene's Grid moves.
	private int _gridX, _gridY;

	private int _hairIndex;
	private int _hairColorIndex;
	private int _skinIndex;
	private int _selectedSlot;
	private bool _isOpen;
	// Set in _Input on the frame Enter / KP-Enter is pressed; consumed in
	// _Process. Lets us tell Enter and Space apart even though both map to
	// the dialogue_advance action — Enter triggers Sell in shops, Space
	// keeps the existing equip/unequip/use behavior.
	private bool _enterPressedThisFrame;
	// True while a sell-confirm ItemPickupToast is open over the inventory.
	// Gates _Process input so arrow keys / space don't double-fire while the
	// toast has focus, and lets us restore tree-pause when the toast closes
	// (the toast unpauses unconditionally on close).
	private bool _overlayActive;
	// Chips currently rendered in the DetailsAction row. UpdateActionButton
	// frees the prior frame's chips and re-builds; can hold one (Equip/Use)
	// or two (Equip + Sell, in shops). _detailsActionBtn stays as an
	// invisible layout anchor providing top/bottom offset references.
	private readonly System.Collections.Generic.List<Button> _activeActionChips = new();

	// 5-swatch preview rows for the HairColor / Skin cyclers. Built in
	// _Ready, refreshed in UpdateCyclerLabels. Center swatch (index 2) is
	// the active selection — flanked by ±1 and ±2 wrapping around the
	// roster so the player can see what's coming next in either direction.
	private PanelContainer[] _hairColorSwatches;
	private PanelContainer[] _skinSwatches;

	// Keyboard focus zones. Grid is the default 5×5 cell selection; the
	// three Cycler zones are entered by Left from the leftmost grid column
	// (top→Hair, middle→HairColor, bottom→Skin). CloseButton is reached by
	// Up from the top grid row.
	private enum FocusZone { Grid, CyclerHair, CyclerHairColor, CyclerSkin, CloseButton }
	private FocusZone _focusZone = FocusZone.Grid;
	// In a Cycler zone: true = right arrow focused, false = left arrow.
	// Entering from the grid lands on the right arrow (closer to the grid).
	private bool _cyclerOnRightArrow = true;

	// Accumulated HP restored from food eaten this inventory session. Spawned
	// as a single "+N HP" floating number above the player after Close(),
	// since the player can't see anything happening while the inventory is up.
	private int _pendingHealAmount;

	public override void _Ready()
	{
		Instance = this;

		BindNodes();
		BuildLivePreview();
		RebuildGridCells();
		NormalizeCyclerArrows();
		BuildSwatchRows();
		WireSignals();

		_panel.Visible = false;

		Inventory.InventoryChanged += RefreshGrid;
		Inventory.ItemEquipped += (id, cat) => RefreshAll();
		Inventory.ItemUnequipped += (cat) => RefreshAll();
	}

	public override void _Input(InputEvent @event)
	{
		if (!_isOpen || _overlayActive) return;
		if (@event is InputEventKey key && key.Pressed && !key.Echo)
		{
			if (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter)
				_enterPressedThisFrame = true;
		}
	}

	public override void _Process(double delta)
	{
		if (Input.IsActionJustPressed("inventory_toggle"))
		{
			// The HUD touch button can synthesize inventory_toggle from a
			// ProcessMode-Always layer, so guard against opening over a
			// dialogue.
			bool dialogueOpen = DialogueManager.IsActive;
			if (dialogueOpen) return;

			if (_isOpen) Close();
			else if (GetTree()?.GetFirstNodeInGroup("player") != null) Open();
			return;
		}

		if (!_isOpen) return;

		UpdatePreviewMirror();

		// Sell confirm overlay has input focus — skip nav / equip / cancel
		// while it's open. The toast handles its own keyboard via _Input.
		if (_overlayActive) return;

		if (Input.IsActionJustPressed("cancel")) { Close(); return; }

		if (Input.IsActionJustPressed("move_up")) NavUp();
		else if (Input.IsActionJustPressed("move_down")) NavDown();
		else if (Input.IsActionJustPressed("move_left")) NavLeft();
		else if (Input.IsActionJustPressed("move_right")) NavRight();
		else if (Input.IsActionJustPressed("dialogue_advance"))
		{
			// Enter on a sellable grid item in a shop opens the sell
			// confirm — matches the [↵] hint on the Sell chip. Space
			// still falls through to NavConfirm for equip/use.
			var item = Inventory.GetSlotItem(_selectedSlot);
			if (_enterPressedThisFrame
				&& _focusZone == FocusZone.Grid
				&& item != null
				&& ShopState.IsActive
				&& CanSell(item))
			{
				OpenSellToast(item, SellPriceFor(item));
			}
			else
			{
				NavConfirm();
			}
		}

		// Reset the Enter flag at end-of-frame regardless of whether
		// dialogue_advance fired (Space without Enter, no key at all, etc).
		_enterPressedThisFrame = false;
	}

	private void NavUp()
	{
		switch (_focusZone)
		{
			case FocusZone.Grid:
				// Top row of the grid → CloseX. Otherwise just move up.
				if (_selectedSlot < GridCols) EnterCloseButton();
				else MoveSelection(-GridCols);
				break;
			case FocusZone.CyclerSkin:      EnterCycler(FocusZone.CyclerHairColor); break;
			case FocusZone.CyclerHairColor: EnterCycler(FocusZone.CyclerHair); break;
			case FocusZone.CyclerHair:      ExitCyclerToGrid(0); break;
			// CloseButton: nothing above.
		}
	}

	private void NavDown()
	{
		switch (_focusZone)
		{
			case FocusZone.Grid:            MoveSelection(GridCols); break;
			case FocusZone.CyclerHair:      EnterCycler(FocusZone.CyclerHairColor); break;
			case FocusZone.CyclerHairColor: EnterCycler(FocusZone.CyclerSkin); break;
			case FocusZone.CyclerSkin:      ExitCyclerToGrid(GridRows - 1); break;
			case FocusZone.CloseButton:
				_focusZone = FocusZone.Grid;
				_selectedSlot = GridCols - 1; // top-right cell, roughly under the X
				_slotCursor.Visible = true;
				UpdateCloseHighlight();
				RefreshHighlight();
				RefreshDetails();
				break;
		}
	}

	private void NavLeft()
	{
		if (_focusZone == FocusZone.Grid)
		{
			// Leftmost grid column → jump into the cyclers. Top rows go to
			// Hair, middle to Hair Color, bottom to Skin.
			if (_selectedSlot % GridCols == 0)
			{
				int row = _selectedSlot / GridCols;
				FocusZone target = row <= 1 ? FocusZone.CyclerHair
								  : row <= 2 ? FocusZone.CyclerHairColor
								  : FocusZone.CyclerSkin;
				EnterCycler(target);
			}
			else MoveSelection(-1);
		}
		else if (_focusZone is FocusZone.CyclerHair or FocusZone.CyclerHairColor or FocusZone.CyclerSkin)
		{
			// Right arrow → step to the left arrow. Already on left = no-op.
			if (_cyclerOnRightArrow) { _cyclerOnRightArrow = false; UpdateCursorOverCyclerArrow(); }
		}
	}

	private void NavRight()
	{
		if (_focusZone == FocusZone.Grid)
		{
			if (_selectedSlot % GridCols < GridCols - 1) MoveSelection(1);
		}
		else if (_focusZone is FocusZone.CyclerHair or FocusZone.CyclerHairColor or FocusZone.CyclerSkin)
		{
			// Left arrow → right arrow; right arrow → exit back to grid
			// leftmost column at the row matching the cycler.
			if (!_cyclerOnRightArrow) { _cyclerOnRightArrow = true; UpdateCursorOverCyclerArrow(); }
			else
			{
				int row = _focusZone switch
				{
					FocusZone.CyclerHair => 0,
					FocusZone.CyclerHairColor => 2,
					_ => GridRows - 1,
				};
				ExitCyclerToGrid(row);
			}
		}
	}

	private void NavConfirm()
	{
		switch (_focusZone)
		{
			case FocusZone.CloseButton: Close(); return;
			case FocusZone.CyclerHair:      CycleHair(_cyclerOnRightArrow ? +1 : -1); return;
			case FocusZone.CyclerHairColor: CycleHairColor(_cyclerOnRightArrow ? +1 : -1); return;
			case FocusZone.CyclerSkin:      CycleSkin(_cyclerOnRightArrow ? +1 : -1); return;
			default: OnAction(); return;
		}
	}

	private void EnterCloseButton()
	{
		_focusZone = FocusZone.CloseButton;
		_slotCursor.Visible = false;
		UpdateCloseHighlight();
	}

	private void EnterCycler(FocusZone zone)
	{
		_focusZone = zone;
		// Default to right arrow on entry — closest to the grid edge.
		_cyclerOnRightArrow = true;
		UpdateCursorOverCyclerArrow();
	}

	private void ExitCyclerToGrid(int row)
	{
		_focusZone = FocusZone.Grid;
		_selectedSlot = Mathf.Clamp(row, 0, GridRows - 1) * GridCols;
		_slotCursor.Visible = true;
		RefreshHighlight();
		RefreshDetails();
	}

	/// <summary>Position SlotCursor over the focused cycler arrow button.
	/// SlotCursor uses Frame-relative offsets; arrows are scoped under the
	/// cycler Control, so we add the cycler's own offsets.</summary>
	private void UpdateCursorOverCyclerArrow()
	{
		Button btn = _focusZone switch
		{
			FocusZone.CyclerHair      => _cyclerOnRightArrow ? _hairRightBtn      : _hairLeftBtn,
			FocusZone.CyclerHairColor => _cyclerOnRightArrow ? _hairColorRightBtn : _hairColorLeftBtn,
			FocusZone.CyclerSkin      => _cyclerOnRightArrow ? _skinRightBtn      : _skinLeftBtn,
			_ => null,
		};
		if (btn == null) return;
		var cycler = btn.GetParent<Control>();
		float left = cycler.OffsetLeft + btn.OffsetLeft;
		float top = cycler.OffsetTop + btn.OffsetTop;
		float width = btn.OffsetRight - btn.OffsetLeft;
		float height = btn.OffsetBottom - btn.OffsetTop;
		_slotCursor.OffsetLeft = left;
		_slotCursor.OffsetTop = top;
		_slotCursor.OffsetRight = left + width;
		_slotCursor.OffsetBottom = top + height;
		_slotCursor.Visible = true;
	}

	private static Texture2D _closeXNormal, _closeXOver;

	/// <summary>Swap the close icon to the C3 yellow over-frame when the
	/// CloseButton zone has focus, or back to the default grey X otherwise.
	/// Two distinct textures rather than a modulate so the highlight reads
	/// pixel-for-pixel like the C3 source.</summary>
	private void UpdateCloseHighlight()
	{
		if (_closeBtn == null) return;
		var icon = _closeBtn.GetNodeOrNull<TextureRect>("Icon");
		if (icon == null) return;
		_closeXNormal ??= GD.Load<Texture2D>("res://assets/sprites/ui/inventory/close_x.png");
		_closeXOver ??= GD.Load<Texture2D>("res://assets/sprites/ui/inventory/close_x_over.png");
		icon.Texture = _focusZone == FocusZone.CloseButton ? _closeXOver : _closeXNormal;
	}

	public void Open()
	{
		if (_isOpen) return;
		_isOpen = true;
		_panel.Visible = true;
		// Snap world player to face-down idle BEFORE pausing so the mirrored
		// preview reads as a clean character portrait. AnimationTree state
		// persists across pause once Travel + Advance commit it.
		// PlayerController is GDScript (Cluster 7b-4) — Pattern H.
		// show_idle_facing is a snake_case method; dispatch via Variant.
		var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		player?.Call("show_idle_facing", Vector2.Down);
		GetTree().Paused = true;
		_selectedSlot = 0;
		_focusZone = FocusZone.Grid;
		EnsurePreviewLayersBuilt();
		RestoreCustomizationFromSave();
		RefreshAll();
	}

	private void Close()
	{
		_isOpen = false;
		_panel.Visible = false;
		GetTree().Paused = false;

		// Flush any accumulated heal as a single "+N HP" toast above the
		// player. Spawned after un-pausing so the drift/fade tweens animate
		// instead of freezing on frame 0. DamageNumber lives in world-space
		// so the camera carries it naturally.
		if (_pendingHealAmount > 0)
		{
			var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
			if (player != null)
			{
				DamageNumber.Spawn(GetTree().CurrentScene, player.GlobalPosition,
				                   _pendingHealAmount, DamageNumber.Kind.Heal);
			}
			_pendingHealAmount = 0;
		}
	}

	private void OnAction()
	{
		var item = Inventory.GetSlotItem(_selectedSlot);
		if (item == null) return;

		if (item.IsEquippable)
		{
			// SPC toggles equip/unequip — pressing the chip when an item is
			// already equipped should take it off, not re-equip.
			var player = GetTree().GetFirstNodeInGroup("player") as CharacterBody2D;
			var costume = player?.GetNodeOrNull<Node>("CostumeController");
			if (Inventory.IsEquipped(item.Id))
			{
				Inventory.Unequip(item.Category);
				if (costume != null && !string.IsNullOrEmpty(item.CostumeLayer))
					costume.Call("unequip_layer", item.CostumeLayer);
			}
			else
			{
				Inventory.Equip(_selectedSlot);
				costume?.Call("equip_item", item);
			}
		}
		else if (item.IsConsumable)
		{
			// Capture how much HP actually moved (capped by MaxHealth) so the
			// post-close "+N HP" toast shows the real heal, not the food's
			// nominal strength. Pre-fetch the health system before UseItem
			// runs the heal so we can diff before/after.
			int healed = 0;
			if (item.Category == ItemData.ItemCategory.Food)
			{
				var player = GetTree().GetFirstNodeInGroup("player") as CharacterBody2D;
				var health = player?.GetNodeOrNull<HealthSystem>("HealthSystem");
				int before = health?.CurrentHealth ?? 0;
				Inventory.UseItem(_selectedSlot);
				int after = health?.CurrentHealth ?? before;
				healed = Mathf.Max(0, after - before);
			}
			else
			{
				Inventory.UseItem(_selectedSlot);
			}
			if (healed > 0) _pendingHealAmount += healed;
		}
		RefreshAll();
	}

	private void MoveSelection(int delta)
	{
		int newSlot = _selectedSlot + delta;
		if (newSlot >= 0 && newSlot < Inventory.SlotCount)
		{
			_selectedSlot = newSlot;
			RefreshHighlight();
			RefreshDetails();
		}
	}

	// ---- Scene binding ----

	/// <summary>Populate every scene-bound field with a GetNode lookup. Path
	/// names match the scene tree authored in InventoryUI.tscn — moving a
	/// node in the editor will break the lookup, so keep the names stable
	/// even if you reposition or restyle.</summary>
	private void BindNodes()
	{
		_panel = GetNode<Control>("Panel");
		_playerNameLabel = GetNode<Label>("Panel/Frame/PlayerName");
		_heartRow = GetNode<HBoxContainer>("Panel/Frame/HeartRow");
		_closeBtn = GetNode<Button>("Panel/Frame/CloseX");

		for (int i = 0; i < 4; i++)
		{
			_abilityValues[i] = GetNode<Label>($"Panel/Frame/AbilityRow{i}/Value");
		}

		// Attack row (AbilityRow0) shows the equipped weapon — make it
		// clickable like the gear slots so tapping the weapon opens its
		// details (with the "Unequip" chip) and parks the cursor there.
		var attackRow = GetNodeOrNull<Control>("Panel/Frame/AbilityRow0");
		if (attackRow != null)
		{
			UiFrames.MakeClickable(attackRow);
			attackRow.GuiInput += evt => OnEquippedSlotTapped(evt, ItemData.ItemCategory.Weapon, attackRow);
		}

		for (int i = 0; i < 6; i++)
		{
			_appearanceIcons[i] = GetNode<TextureRect>($"Panel/Frame/AppearanceSlot{i}/Chip/Icon");

			// Click/tap an equipped-gear slot to select that item in the
			// grid, open its details panel (with the "Unequip" chip), and
			// move the yellow cursor onto the clicked slot.
			var slotNode = GetNode<Control>($"Panel/Frame/AppearanceSlot{i}");
			_appearanceSlots[i] = slotNode;
			UiFrames.MakeClickable(slotNode);
			var cat = AppearanceCategories[i]; // capture for closure
			slotNode.GuiInput += evt => OnEquippedSlotTapped(evt, cat, slotNode);
		}

		_previewRect = GetNode<TextureRect>("Panel/Frame/PreviewRect");
		_hairLabel = GetNode<Label>("Panel/Frame/HairCycler/Label");
		_hairColorLabel = GetNodeOrNull<Label>("Panel/Frame/HairColorCycler/Label");
		_skinLabel = GetNode<Label>("Panel/Frame/SkinCycler/Label");
		_hairLeftBtn = GetNode<Button>("Panel/Frame/HairCycler/LeftArrow");
		_hairRightBtn = GetNode<Button>("Panel/Frame/HairCycler/RightArrow");
		_hairColorLeftBtn = GetNodeOrNull<Button>("Panel/Frame/HairColorCycler/LeftArrow");
		_hairColorRightBtn = GetNodeOrNull<Button>("Panel/Frame/HairColorCycler/RightArrow");
		_skinLeftBtn = GetNode<Button>("Panel/Frame/SkinCycler/LeftArrow");
		_skinRightBtn = GetNode<Button>("Panel/Frame/SkinCycler/RightArrow");

		_detailsName = GetNode<Label>("Panel/Frame/DetailsName");
		_detailsDesc = GetNode<Label>("Panel/Frame/DetailsDesc");
		_detailsStats = GetNode<HBoxContainer>("Panel/Frame/DetailsStats");
		_detailsAction = GetNode<Label>("Panel/Frame/DetailsAction");

		_gemLabel = GetNode<Label>("Panel/Frame/GemLabel");
		_gridContainer = GetNode<Control>("Panel/Frame/Grid");
		_slotCursor = GetNode<TextureRect>("Panel/Frame/SlotCursor");

		// Big item preview icon (scene-authored at Panel/Frame/DetailsItemIcon).
		// Optional — bind only if the scene has the node.
		_detailsItemIcon = GetNodeOrNull<TextureRect>("Panel/Frame/DetailsItemIcon");
		if (_detailsItemIcon != null) _detailsItemIcon.Visible = false;

		BuildDetailsItemCursor();
		BuildDetailsActionButton();
	}

	/// <summary>Spawn a copy of the slot-cursor texture sized to wrap the
	/// DetailsItemIcon. Acts as a static "this is the focused item" frame
	/// around the top-right preview, mirroring the gold border the grid's
	/// SlotCursor uses on the focused cell. Hidden when no item is focused.</summary>
	private void BuildDetailsItemCursor()
	{
		if (_detailsItemIcon == null || _slotCursor == null) return;
		var frame = _detailsItemIcon.GetParent<Control>();
		_detailsItemCursor = new TextureRect
		{
			Name = "DetailsItemCursor",
			Texture = _slotCursor.Texture,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false,
		};
		// Wrap the icon with a 6 px outer halo so the icon sits inset within
		// the gold border instead of touching it. Cursor stays the same
		// 48×48 visual it had before the icon was shrunk.
		const int cursorPad = 6;
		_detailsItemCursor.OffsetLeft = _detailsItemIcon.OffsetLeft - cursorPad;
		_detailsItemCursor.OffsetTop = _detailsItemIcon.OffsetTop - cursorPad;
		_detailsItemCursor.OffsetRight = _detailsItemIcon.OffsetRight + cursorPad;
		_detailsItemCursor.OffsetBottom = _detailsItemIcon.OffsetBottom + cursorPad;
		frame.AddChild(_detailsItemCursor);
		// Render above the icon — frame draws children in order, this needs
		// to come last to sit on top of the texture.
		frame.MoveChild(_detailsItemCursor, frame.GetChildCount() - 1);
	}

	/// <summary>Hide the DetailsAction Label and overlay a design-system chip
	/// Button at the same offsets. The button label/key-hint changes per item
	/// state — Equip / Unequip / Use — and the Pressed handler routes to
	/// OnAction (toggle) so SPC works for both equip and unequip.</summary>
	private void BuildDetailsActionButton()
	{
		if (_detailsAction == null) return;
		_detailsAction.Visible = false;
		var frame = _detailsAction.GetParent<Control>();
		_detailsActionBtn = UiFrames.BuildChipButton("Equip", "spc", UiFrames.ApplyPrimaryButton);
		_detailsActionBtn.Visible = false;
		_detailsActionBtn.OffsetLeft = _detailsAction.OffsetLeft;
		_detailsActionBtn.OffsetTop = _detailsAction.OffsetTop - 6;
		_detailsActionBtn.OffsetRight = _detailsAction.OffsetRight;
		_detailsActionBtn.OffsetBottom = _detailsAction.OffsetBottom + 10;
		_detailsActionBtn.FocusMode = Control.FocusModeEnum.None;
		_detailsActionBtn.Pressed += OnAction;
		frame.AddChild(_detailsActionBtn);
	}

	private void UpdateActionButton(ItemData item, bool equipped)
	{
		if (_detailsActionBtn == null) return;

		// Free the previous frame's chips. _detailsActionBtn stays as the
		// invisible layout anchor — its top/bottom offsets dictate the
		// chip-row vertical position.
		foreach (var old in _activeActionChips)
			if (GodotObject.IsInstanceValid(old)) old.QueueFree();
		_activeActionChips.Clear();

		var frame = _detailsActionBtn.GetParent<Control>();
		var chips = new System.Collections.Generic.List<(Button btn, int width)>();

		// 1) Equip / Unequip / Use chip — shown outside shops AND inside
		//    shops (user wants both chips visible when in a shop). Hidden
		//    only for non-actionable items (quest items, etc).
		string text = null;
		System.Action<Button> equipStyle = UiFrames.ApplyPrimaryButton;
		if (item.IsEquippable && !equipped) text = "Equip";
		else if (item.IsEquippable && equipped) { text = "Unequip"; equipStyle = UiFrames.ApplySecondaryButton; }
		else if (item.IsConsumable) text = "Use";

		if (text != null)
		{
			var equipBtn = UiFrames.BuildChipButton(text, "spc", equipStyle);
			equipBtn.Pressed += OnAction;
			int w = text switch
			{
				// Wide enough that the [spc] kbd chip + its bevel sit
				// fully inside the button border. BuildChipButton uses
				// 12 px hbox padding each side and "spc" renders ~45 px
				// wide at 20 pt — narrower buttons let Center alignment
				// push the chip past the right edge.
				"Equip" => 118,
				"Unequip" => 142,
				_ => 110,
			};
			chips.Add((equipBtn, w));
		}

		// 2) Sell chip — shop only, sellable items only. Sits to the RIGHT
		//    of the equip chip per the user's layout (sell is the optional
		//    extra; equip is the primary action).
		if (ShopState.IsActive && CanSell(item))
		{
			int price = SellPriceFor(item);
			var sellBtn = BuildSellChip(price);
			sellBtn.Pressed += () => OpenSellToast(item, price);
			chips.Add((sellBtn, 122));
		}

		if (chips.Count == 0) return;

		// Right-align the row to the Collection grid's right edge so the
		// rightmost chip's right border meets the rightmost cell column.
		// Walk right→left placing each chip's right edge against the running
		// cursor — last chip in list ends up on the right (Sell), first on
		// the left (Equip).
		float rightEdge = _gridContainer != null ? _gridContainer.OffsetRight : _detailsActionBtn.OffsetRight;
		float top = _detailsActionBtn.OffsetTop;
		float bottom = _detailsActionBtn.OffsetBottom;
		const float ChipGap = 8f;

		float cursor = rightEdge;
		for (int i = chips.Count - 1; i >= 0; i--)
		{
			var (btn, w) = chips[i];
			btn.OffsetRight = cursor;
			btn.OffsetLeft = cursor - w;
			btn.OffsetTop = top;
			btn.OffsetBottom = bottom;
			btn.FocusMode = Control.FocusModeEnum.None;
			frame.AddChild(btn);
			_activeActionChips.Add(btn);
			cursor -= (w + ChipGap);
		}
	}

	/// <summary>The cycler arrow Icons authored in the .tscn use a
	/// <c>scale = Vector2(-1, -1)</c> trick + ad-hoc offsets to flip the
	/// shared <c>cycle_arrow.png</c> for left/right buttons. The trick
	/// pushes the rendered glyph outside the Button's hit rect, which is
	/// why the visible arrows sit lower than the click target.
	///
	/// Reset each Icon to a clean centered fill (identity scale, zeroed
	/// offsets, FullRect anchors) and recompute the Button's vertical
	/// position from its sibling Label so the arrows baseline against
	/// "Hair NN" / "Color NN" / "Skin NN" — the labels are authored at
	/// different Y per cycler, so we can't share a single offset across
	/// all three.
	///
	/// FlipH on the right arrows: cycle_arrow.png natively points LEFT,
	/// so left arrows keep the texture as-is and right arrows flip.</summary>
	private void NormalizeCyclerArrows()
	{
		var arrows = new[]
		{
			_hairLeftBtn,      _hairRightBtn,
			_hairColorLeftBtn, _hairColorRightBtn,
			_skinLeftBtn,      _skinRightBtn,
		};
		var isLeft = new[] { true, false, true, false, true, false };

		for (int i = 0; i < arrows.Length; i++)
		{
			var btn = arrows[i];
			if (btn == null) continue;
			btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			var icon = btn.GetNodeOrNull<TextureRect>("Icon");
			if (icon == null) continue;

			icon.Scale = Vector2.One;
			icon.PivotOffset = Vector2.Zero;
			icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			icon.OffsetLeft = 0;
			icon.OffsetTop = 0;
			icon.OffsetRight = 0;
			icon.OffsetBottom = 0;
			icon.FlipH = !isLeft[i];
			icon.FlipV = false;

			// Snap the button's vertical span to the sibling Label's
			// vertical centre. Button width / X stay as-authored so the
			// left/right arrows hug their cycler edges; only Y moves.
			var cycler = btn.GetParent<Control>();
			var label = cycler?.GetNodeOrNull<Label>("Label");
			if (label != null)
			{
				float labelCenterY = (label.OffsetTop + label.OffsetBottom) * 0.5f;
				float btnHeight = btn.OffsetBottom - btn.OffsetTop;
				btn.OffsetTop = labelCenterY - btnHeight * 0.5f;
				btn.OffsetBottom = btn.OffsetTop + btnHeight;
			}
		}
	}

	/// <summary>Hide the text labels on the HairColor and Skin cyclers and
	/// drop a 5-swatch preview row in their place. Hair STYLE keeps its
	/// text label since the variation is shape, not color, and the swatch
	/// vocabulary doesn't apply.
	///
	/// Each swatch is a PanelContainer with a colored StyleBoxFlat. Sizes
	/// step down from the centered active swatch (22 px) to the ±1
	/// flankers (18 px) to the ±2 outer swatches (14 px), matching the
	/// "selected one pops" visual the user asked for.</summary>
	private void BuildSwatchRows()
	{
		var hairCycler = GetNodeOrNull<Control>("Panel/Frame/HairColorCycler");
		if (hairCycler != null)
		{
			if (_hairColorLabel != null) _hairColorLabel.Visible = false;
			_hairColorSwatches = BuildSwatchRow(hairCycler);
			WireSwatchClicks(_hairColorSwatches, CycleHairColor);
		}

		var skinCycler = GetNodeOrNull<Control>("Panel/Frame/SkinCycler");
		if (skinCycler != null)
		{
			if (_skinLabel != null) _skinLabel.Visible = false;
			_skinSwatches = BuildSwatchRow(skinCycler);
			WireSwatchClicks(_skinSwatches, CycleSkin);
		}
	}

	/// <summary>Make the four flanking swatches clickable: clicking the
	/// swatch at offset ±1 / ±2 from center cycles the selection by that
	/// many steps, so the clicked color lands in the center slot. The
	/// center swatch (index 2) is already selected, so it's left inert.</summary>
	private void WireSwatchClicks(PanelContainer[] frames, System.Action<int> cycleBy)
	{
		if (frames == null) return;
		for (int i = 0; i < frames.Length; i++)
		{
			if (i == 2) continue; // center = current selection
			int delta = i - 2; // -2, -1, +1, +2
			var frame = frames[i];
			frame.MouseFilter = Control.MouseFilterEnum.Stop;
			frame.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			frame.GuiInput += evt =>
			{
				bool tapped = (evt is InputEventScreenTouch t && t.Pressed)
							  || (evt is InputEventMouseButton m && m.Pressed && m.ButtonIndex == MouseButton.Left);
				if (tapped) cycleBy(delta);
			};
		}
	}

	/// <summary>Spawn an HBox of 5 swatch panels inside the given cycler
	/// Control. The HBox spans the gap between the cycler's left and right
	/// arrows (offsets 22 / -40 mirror the arrow Buttons' offsets) AND
	/// matches the Label child's vertical band so the swatches baseline
	/// with the arrow icons (which NormalizeCyclerArrows pinned to the
	/// label's vertical center). Without this alignment the swatches sit
	/// at the cycler's vertical center while the arrows sit at the
	/// label's center, and the two end up out of line for cyclers whose
	/// label is authored off-center.</summary>
	private static PanelContainer[] BuildSwatchRow(Control parent)
	{
		var hbox = new HBoxContainer
		{
			Name = "SwatchRow",
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		// Mixed anchors: X spans full parent (left=0, right=1) so the
		// inset offsets clear the arrow buttons; Y is pinned to the parent
		// TOP only (top=0, bottom=0) so the offsets read in the same
		// reference frame as the Label child (which uses anchors_preset=0).
		// Earlier versions used FullRect — that made offset_bottom relative
		// to parent.bottom, throwing the swatch row below the arrow band.
		hbox.AnchorLeft = 0f;
		hbox.AnchorRight = 1f;
		hbox.AnchorTop = 0f;
		hbox.AnchorBottom = 0f;
		// Inset to clear the left + right arrow buttons (which sit at the
		// cycler's ends per the .tscn). The 40 px right inset accounts for
		// the right arrow button + the cycler control's own right margin.
		hbox.OffsetLeft = 22;
		hbox.OffsetRight = -40;
		// Align vertically with the cycler's Label band — the arrows were
		// pinned to the label's center by NormalizeCyclerArrows, so
		// matching the same band keeps swatches and arrows on one line.
		var sibLabel = parent.GetNodeOrNull<Label>("Label");
		if (sibLabel != null)
		{
			hbox.OffsetTop = sibLabel.OffsetTop;
			hbox.OffsetBottom = sibLabel.OffsetBottom;
		}
		else
		{
			hbox.OffsetTop = 0;
			hbox.OffsetBottom = 30;
		}
		hbox.Alignment = BoxContainer.AlignmentMode.Center;
		hbox.AddThemeConstantOverride("separation", 4);
		parent.AddChild(hbox);

		var sizes = new[] { 14, 18, 22, 18, 14 };
		var frames = new PanelContainer[5];
		for (int i = 0; i < 5; i++)
		{
			var frame = new PanelContainer
			{
				CustomMinimumSize = new Vector2(sizes[i], sizes[i]),
				MouseFilter = Control.MouseFilterEnum.Ignore,
				SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			};
			// Every swatch keeps the 1 px ink border (the "green outline" —
			// DesignTokens.Ink is #10180F, very dark green). The center
			// (selected) swatch ALSO gets a 3 px gold outline OUTSIDE the
			// ink — drawn via the stylebox shadow with offset (0,0) so it
			// reads as a second border layer rather than a drop shadow.
			// 3 px is enough to read at the swatch's small render size;
			// 1 px was getting lost. Ink between bg and gold means lighter
			// swatch colors (pale skin tones, blonde hair) don't blend
			// into the gold.
			var sb = new StyleBoxFlat
			{
				BgColor = new Color(0.5f, 0f, 0.5f, 1f), // magenta = unset, helps spot bind bugs
				BorderColor = DesignTokens.Ink,
				ShadowColor = i == 2 ? DesignTokens.Gold : new Color(0, 0, 0, 0),
				ShadowSize = i == 2 ? 3 : 0,
				ShadowOffset = Vector2.Zero,
			};
			sb.SetBorderWidthAll(1);
			sb.SetCornerRadiusAll(2);
			frame.AddThemeStyleboxOverride("panel", sb);
			frames[i] = frame;
			hbox.AddChild(frame);
		}
		return frames;
	}

	/// <summary>Refresh the 5 swatch panels' BgColors. Indices wrap, so a
	/// 4-color roster repeats colors at the outer slots — that's expected
	/// and reads as "you've seen everything, here it is again".</summary>
	private static void UpdateSwatchRow(PanelContainer[] frames, int activeIdx, int count, System.Func<int, Color> colorFor)
	{
		if (frames == null || count == 0) return;
		for (int i = 0; i < 5; i++)
		{
			int wrapped = ((activeIdx + (i - 2)) % count + count) % count;
			if (frames[i].GetThemeStylebox("panel") is StyleBoxFlat sb)
			{
				sb.BgColor = colorFor(wrapped);
			}
		}
	}

	/// <summary>"Sell N [gem] [↵]" chip used in shops. Inline-built rather
	/// than going through UiFrames.BuildChipButton so we can splice a gem
	/// TextureRect between the label and the kbd-hint chip — the standard
	/// chip helper only takes (text, hint) and doesn't expose its inner
	/// hbox.</summary>
	private static Button BuildSellChip(int price)
	{
		var btn = new Button { Text = "" };
		btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		UiFrames.ApplyPrimaryButton(btn);

		var hbox = new HBoxContainer();
		hbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		// Tightened padding (was 12 / 4) — fits "Sell N + gem + ↵" in a
		// chip ~135 wide so the row sits beside the Equip chip without
		// running off the grid right edge.
		hbox.OffsetLeft = 6;
		hbox.OffsetRight = -6;
		hbox.OffsetTop = 4;
		hbox.OffsetBottom = -4;
		hbox.Alignment = BoxContainer.AlignmentMode.Center;
		hbox.AddThemeConstantOverride("separation", 4);
		hbox.MouseFilter = Control.MouseFilterEnum.Ignore;
		btn.AddChild(hbox);

		// Label + gem live in a tighter inner hbox (separation 1) so the
		// "N" digit hugs the gem icon. The outer hbox keeps separation 4
		// so the kbd chip still has breathing room next to the cluster.
		var labelGem = new HBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		labelGem.AddThemeConstantOverride("separation", 1);
		hbox.AddChild(labelGem);

		var label = new Label
		{
			Text = $"Sell {price}",
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", 18);
		label.AddThemeColorOverride("font_color", DesignTokens.Paper);
		labelGem.AddChild(label);

		var gem = new TextureRect
		{
			Texture = UiStyles.Gem,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(18, 18),
		};
		labelGem.AddChild(gem);

		// Skip the ↵ kbd chip on touch builds — same rationale as
		// UiFrames.BuildChipButton's mobile branch.
		if (!UiStyles.IsMobile) hbox.AddChild(UiFrames.BuildKbdChip("↵"));
		return btn;
	}

	/// <summary>Sell qualifier — non-quest, priced, and not a currency or key
	/// item. Equipped gear is allowed (sell unequips first). Hair never lands
	/// in inventory at all so it falls out via Cost == 0.</summary>
	private static bool CanSell(ItemData item) =>
		item != null && !item.QuestItem && item.Cost > 0
		&& item.Category != ItemData.ItemCategory.Money
		&& item.Category != ItemData.ItemCategory.Key;

	/// <summary>Half of the buy price (rounded down, min 1). Tune the ratio
	/// here if shops should pay more / less for resale.</summary>
	private static int SellPriceFor(ItemData item) => System.Math.Max(1, item.Cost / 2);

	/// <summary>Spawn a sell-confirm toast over the inventory. The toast pauses
	/// the tree itself; we set _overlayActive so our own _Process skips
	/// keyboard nav while it's up, and re-pause on close (the toast unpauses
	/// unconditionally, which would otherwise unpause the inventory beneath).</summary>
	private void OpenSellToast(ItemData item, int sellPrice)
	{
		var toast = new ItemPickupToast();
		GetTree().CurrentScene.AddChild(toast);
		_overlayActive = true;
		toast.TreeExited += () =>
		{
			_overlayActive = false;
			// Toast.Close() unpauses the tree on the way out. If the
			// inventory is still open, restore its pause so the world
			// behind it stays frozen.
			if (_isOpen && IsInstanceValid(this)) GetTree().Paused = true;
		};
		toast.ShowSell(item, sellPrice, onAccept: () =>
		{

			// Auto-unequip if the player is selling the gear they're wearing,
			// then strip the costume layer so the live preview refreshes.
			if (Inventory.IsEquipped(item.Id))
			{
				Inventory.Unequip(item.Category);
				var player = GetTree().GetFirstNodeInGroup("player") as CharacterBody2D;
				var costume = player?.GetNodeOrNull<Node>("CostumeController");
				if (costume != null && !string.IsNullOrEmpty(item.CostumeLayer))
					costume.Call("unequip_layer", item.CostumeLayer);
			}

			Inventory.RemoveItem(item.Id, 1);
			CurrencySystem.AddGems(sellPrice);
			SaveManager.Save();
			SFXController.Play("collectible_pickup");
			RefreshAll();
		});
	}

	private void WireSignals()
	{
		_closeBtn.Pressed += Close;

		_hairLeftBtn.Pressed += () => CycleHair(-1);
		_hairRightBtn.Pressed += () => CycleHair(+1);
		if (_hairColorLeftBtn != null) _hairColorLeftBtn.Pressed += () => CycleHairColor(-1);
		if (_hairColorRightBtn != null) _hairColorRightBtn.Pressed += () => CycleHairColor(+1);
		_skinLeftBtn.Pressed += () => CycleSkin(-1);
		_skinRightBtn.Pressed += () => CycleSkin(+1);
	}

	// ---- Cyclers (apply, persist, restore) ----

	private void CycleHair(int delta)
	{
		int max = CharacterCustomization.HairStyleCount;
		if (max == 0) return;
		_hairIndex = Mathf.Wrap(_hairIndex + delta, 0, max);
		ApplyCustomizationToPlayer();
		PersistCustomization();
	}

	private void CycleHairColor(int delta)
	{
		int max = CharacterCustomization.HairColorCount;
		if (max == 0) return;
		_hairColorIndex = Mathf.Wrap(_hairColorIndex + delta, 0, max);
		ApplyCustomizationToPlayer();
		PersistCustomization();
	}

	private void CycleSkin(int delta)
	{
		int max = CharacterCustomization.SkinCount;
		if (max == 0) return;
		_skinIndex = Mathf.Wrap(_skinIndex + delta, 0, max);
		ApplyCustomizationToPlayer();
		PersistCustomization();
	}

	/// <summary>Push the active (style, color, skin) tuple to the player's
	/// SpriteLayers — preview mirror copies texture + material the next
	/// tick, so both the world player AND the preview update.</summary>
	private void ApplyCustomizationToPlayer()
	{
		var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		var spriteLayers = player?.GetNodeOrNull("SpriteLayers");
		if (spriteLayers != null)
		{
			CharacterCustomization.Apply(spriteLayers, _hairIndex, _hairColorIndex, _skinIndex);
		}
		UpdateCyclerLabels();
	}

	/// <summary>Write current cycler indices into SaveData. The next world
	/// transition's auto-save persists them; cycling within a session sticks
	/// across reloads as long as one transition fires before quit.
	/// SaveData is GDScript (Cluster 9) -- snake_case Variant Set.</summary>
	private void PersistCustomization()
	{
		var data = SaveManager.CurrentData;
		if (data == null) return;
		data.Set("hair_style_index", _hairIndex);
		data.Set("hair_color_index", _hairColorIndex);
		data.Set("skin_index", _skinIndex);
	}

	/// <summary>Pull persisted indices from SaveData onto our local state +
	/// re-label the cyclers. Apply already happens in
	/// CostumeController.restore_equipment so the player visual is correct
	/// before we ever open inventory; this just syncs the UI.</summary>
	private void RestoreCustomizationFromSave()
	{
		var data = SaveManager.CurrentData;
		if (data == null) return;
		int hair = data.Get("hair_style_index").AsInt32();
		int hairColor = data.Get("hair_color_index").AsInt32();
		int skin = data.Get("skin_index").AsInt32();
		if (hair >= 0) _hairIndex = hair;
		if (hairColor >= 0) _hairColorIndex = hairColor;
		if (skin >= 0) _skinIndex = skin;
		UpdateCyclerLabels();
	}

	/// <summary>SubViewport-based live paper-doll. Sized to match the scene's
	/// PreviewRect so the rendered character lines up with whatever rect
	/// the scene authoring placed it at. Re-syncs each open in case the
	/// rect was moved/resized in the editor between sessions.</summary>
	private void BuildLivePreview()
	{
		var rectSize = _previewRect.Size;
		if (rectSize.X < 1 || rectSize.Y < 1)
		{
			// Scene hasn't laid out yet — fall back to the scene-authored
			// offsets so we still have a sensible viewport size.
			rectSize = new Vector2(
				_previewRect.OffsetRight - _previewRect.OffsetLeft,
				_previewRect.OffsetBottom - _previewRect.OffsetTop);
		}

		_previewViewport = new SubViewport
		{
			Size = new Vector2I((int)rectSize.X, (int)rectSize.Y),
			TransparentBg = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
			Disable3D = true,
		};
		AddChild(_previewViewport);

		_previewLayers = new Node2D
		{
			Position = new Vector2(rectSize.X * 0.5f, rectSize.Y * 0.95f),
			Scale = new Vector2(8, 8),
		};
		// Nearest-filter on the parent so sprites with Inherit don't render
		// blurred by the SubViewport's default Linear filter.
		_previewLayers.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
		_previewViewport.AddChild(_previewLayers);

		_previewRect.Texture = _previewViewport.GetTexture();
	}

	/// <summary>Bind to the 25 scene-authored cells (Cell0..Cell24) under
	/// the Grid container. Each cell has an "Icon" child whose texture we
	/// swap when items are added/removed; quantity Labels are spawned at
	/// runtime since they're hidden for non-stacking items.</summary>
	/// <summary>Translate a row-major slot index (0–29) into the scene-cell
	/// name suffix. The scene's first 25 cells (Cell0–24) cover the original
	/// 5×5 in row-major order; the new 6th column (Cell25–29) is numbered
	/// by row, so for col 5 we return 25+row instead of row*GridCols+col.</summary>
	private static int MapSlotToCellIndex(int slot)
	{
		int row = slot / GridCols;
		int col = slot % GridCols;
		return col < 5 ? row * 5 + col : 25 + row;
	}

	private void RebuildGridCells()
	{
		_gridX = (int)_gridContainer.OffsetLeft;
		_gridY = (int)_gridContainer.OffsetTop;

		for (int i = 0; i < Inventory.SlotCount; i++)
		{
			var cell = _gridContainer.GetNodeOrNull<Control>($"Cell{MapSlotToCellIndex(i)}");
			if (cell == null) continue;
			_slotIcons[i] = cell.GetNodeOrNull<TextureRect>("Icon");

			// Spawn a Qty label per cell once — overlays the cell's bottom-
			// right and shows "x{n}" when item count > 1.
			var qty = new Label
			{
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Bottom,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			qty.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			qty.AddThemeFontOverride("font", UiFonts.Body);
			qty.AddThemeFontSizeOverride("font_size", 13);
			qty.AddThemeColorOverride("font_color", DesignTokens.Paper);
			qty.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
			qty.AddThemeConstantOverride("shadow_offset_x", 1);
			qty.AddThemeConstantOverride("shadow_offset_y", 1);
			cell.AddChild(qty);
			_slotQtyLabels[i] = qty;

			// Touch / mouse-click support: a single click/tap only *selects*
			// the cell (mirrors the arrow-key cursor) and opens its details
			// panel — it never equips. Equip / Unequip / Use happens via the
			// action chip in the details panel, or via a double-click /
			// double-tap on the cell. Wired on every build, not just mobile,
			// so desktop mouse users get it too — keyboard nav still works
			// in parallel.
			UiFrames.MakeClickable(cell);
			int slotIndex = i; // capture for closure
			cell.GuiInput += evt => OnCellTapped(evt, slotIndex);
		}

		// Move the slot cursor to the front so it draws above the cell BGs.
		_slotCursor.GetParent().MoveChild(_slotCursor, -1);
	}

	// Double-tap tracking for touch (InputEventScreenTouch has no DoubleTap
	// flag the way mouse buttons do, so we time it ourselves).
	private int _lastTapSlot = -1;
	private ulong _lastTapMsec;
	private const ulong DoubleTapWindowMsec = 300;

	/// <summary>Click / tap handler for one inventory cell. A single
	/// click/tap selects the cell and opens its details panel — it never
	/// equips. A double-click (desktop) or double-tap (touch) fires the
	/// same code path Space does (Equip / Unequip / Use) as a shortcut.
	/// Routes through _selectedSlot + OnAction so equipment, use, sell, and
	/// details-panel state stay consistent with the keyboard flow.</summary>
	private void OnCellTapped(InputEvent evt, int slot)
	{
		bool tapped;
		bool isDoubleActivate;

		if (evt is InputEventMouseButton m && m.Pressed && m.ButtonIndex == MouseButton.Left)
		{
			tapped = true;
			isDoubleActivate = m.DoubleClick;
		}
		else if (evt is InputEventScreenTouch t && t.Pressed)
		{
			tapped = true;
			ulong now = Time.GetTicksMsec();
			isDoubleActivate = _lastTapSlot == slot && now - _lastTapMsec <= DoubleTapWindowMsec;
			_lastTapSlot = slot;
			_lastTapMsec = now;
		}
		else
		{
			return;
		}

		if (!tapped) return;

		// Always (re)select first so the details panel + highlight reflect
		// the tapped cell, even on the activating double-tap.
		_focusZone = FocusZone.Grid;
		_selectedSlot = slot;
		RefreshHighlight();
		RefreshDetails();

		if (isDoubleActivate)
			OnAction();
	}

	/// <summary>Click/tap handler for one equipped-gear slot in the left
	/// Appearance grid (or the Attack row, which displays the equipped
	/// weapon). Finds the inventory cell holding whatever's equipped in that
	/// category, selects it, opens its details panel (the chip there reads
	/// "Unequip"), and moves the yellow slot cursor onto the clicked slot.
	/// Does nothing if the slot is empty. Never unequips directly; that's
	/// the chip's job (or a double-tap on the cell).</summary>
	private void OnEquippedSlotTapped(InputEvent evt, ItemData.ItemCategory category, Control slotNode)
	{
		bool tapped = (evt is InputEventScreenTouch t && t.Pressed)
					  || (evt is InputEventMouseButton m && m.Pressed && m.ButtonIndex == MouseButton.Left);
		if (!tapped) return;


		int equippedId = Inventory.GetEquippedId(category);
		if (equippedId <= 0) return; // nothing equipped in this slot

		int gridSlot = -1;
		for (int i = 0; i < Inventory.SlotCount; i++)
		{
			if (Inventory.GetSlotItemId(i) == equippedId) { gridSlot = i; break; }
		}
		if (gridSlot < 0) return; // equipped item not present in the grid

		// Keep details / OnAction pointed at the real grid cell, but park
		// the visible cursor over the slot the player actually clicked.
		_focusZone = FocusZone.Grid;
		_selectedSlot = gridSlot;
		RefreshDetails();
		MoveSlotCursorTo(slotNode);
	}

	/// <summary>Position the yellow SlotCursor over an arbitrary slot-style
	/// Control on the left side (an AppearanceSlot or the AbilityRow Attack
	/// chip). Both the cursor and the target are children of Panel/Frame, so
	/// we work in Frame-relative offsets: target's offset plus its "Chip"
	/// child's offset.</summary>
	private void MoveSlotCursorTo(Control slotNode)
	{
		if (_slotCursor == null || slotNode == null) return;
		var chip = slotNode.GetNodeOrNull<Control>("Chip");
		float chipL = chip?.OffsetLeft ?? 0f;
		float chipT = chip?.OffsetTop ?? 0f;
		float chipW = chip != null ? chip.OffsetRight - chip.OffsetLeft : CellSize;
		float chipH = chip != null ? chip.OffsetBottom - chip.OffsetTop : CellSize;
		_slotCursor.OffsetLeft = slotNode.OffsetLeft + chipL;
		_slotCursor.OffsetTop = slotNode.OffsetTop + chipT;
		_slotCursor.OffsetRight = _slotCursor.OffsetLeft + chipW;
		_slotCursor.OffsetBottom = _slotCursor.OffsetTop + chipH;
		_slotCursor.Visible = true;
	}

	// ---- Live preview (mirror player SpriteLayers into SubViewport) ----

	private void EnsurePreviewLayersBuilt()
	{
		if (_previewLayers == null) return;
		// Rebuild when the source goes invalid (e.g. SaveManager.Load
		// respawns the player and frees the old SpriteLayers Node2D).
		if (GodotObject.IsInstanceValid(_sourceLayers) && _previewSprites.Count > 0) return;

		var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		_sourceLayers = player?.GetNodeOrNull("SpriteLayers");
		if (_sourceLayers == null)
		{
			GD.PrintErr("[InventoryUI] Player has no SpriteLayers — preview disabled.");
			return;
		}

		foreach (var child in _previewLayers.GetChildren()) child.QueueFree();
		_sourceSprites.Clear();
		_previewSprites.Clear();

		foreach (var child in _sourceLayers.GetChildren())
		{
			if (child is not Sprite2D src) continue;
			var dup = new Sprite2D
			{
				Name = src.Name,
				Texture = src.Texture,
				Hframes = src.Hframes,
				Vframes = src.Vframes,
				Frame = src.Frame,
				Visible = src.Visible,
				Material = src.Material,
				Position = src.Position,
				Offset = src.Offset,
				Centered = src.Centered,
				FlipH = src.FlipH,
				FlipV = src.FlipV,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			};
			_previewLayers.AddChild(dup);
			_sourceSprites.Add(src);
			_previewSprites.Add(dup);
		}
	}

	private void UpdatePreviewMirror()
	{
		if (_previewLayers == null) return;
		// Rebuild if the player got respawned underneath us — a freed
		// _sourceLayers throws ObjectDisposedException on GetChildren().
		if (!GodotObject.IsInstanceValid(_sourceLayers))
		{
			_sourceLayers = null;
			EnsurePreviewLayersBuilt();
			if (_sourceLayers == null) return;
		}
		// Iterate via the cached Sprite2D parallel lists — SpriteLayers
		// also has AnimationPlayer + AnimationTree children, so a raw
		// count match against GetChildren() would skip every tick.
		for (int i = 0; i < _sourceSprites.Count && i < _previewSprites.Count; i++)
		{
			var src = _sourceSprites[i];
			var dst = _previewSprites[i];
			if (!GodotObject.IsInstanceValid(src) || !GodotObject.IsInstanceValid(dst)) continue;
			dst.Texture = src.Texture;
			dst.Frame = src.Frame;
			dst.Visible = src.Visible;
			dst.FlipH = src.FlipH;
			dst.FlipV = src.FlipV;
			dst.Material = src.Material;
		}
	}

	// ---- Refresh ----

	private void RefreshAll()
	{
		RefreshHeader();
		RefreshAbilities();
		RefreshAppearance();
		RefreshGrid();
		RefreshHighlight();
		RefreshDetails();
		RefreshGems();
		UpdateCyclerLabels();
	}

	private void RefreshHeader()
	{
		var playerName = SaveManager.CurrentData?.Get("player_name").AsString();
		_playerNameLabel.Text = string.IsNullOrEmpty(playerName) ? "Hero" : playerName;

		foreach (var c in _heartRow.GetChildren()) c.QueueFree();
		var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		var health = player?.GetNodeOrNull<HealthSystem>("HealthSystem");
		if (health == null) return;

		const int hpPerHeart = 2;
		int totalHearts = Mathf.Min((health.MaxHealth + hpPerHeart - 1) / hpPerHeart, 5);
		for (int i = 0; i < totalHearts; i++)
		{
			int heartCap = (i + 1) * hpPerHeart;
			Texture2D tex = health.CurrentHealth >= heartCap ? UiStyles.Heart
						  : health.CurrentHealth >= heartCap - 1 ? GD.Load<Texture2D>("res://assets/sprites/ui/heart_half.png")
						  : GD.Load<Texture2D>("res://assets/sprites/ui/heart_empty.png");
			var heart = new TextureRect
			{
				Texture = tex,
				CustomMinimumSize = new Vector2(20, 20),
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			_heartRow.AddChild(heart);
		}
	}

	private void RefreshAbilities()
	{
		var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		var health = player?.GetNodeOrNull<HealthSystem>("HealthSystem");

		var weapon = Inventory.GetEquipped(ItemData.ItemCategory.Weapon);
		int attack = weapon?.Strength ?? 0;
		int maxHearts = (health?.MaxHealth ?? 0) / 2;
		int defense = StrengthOf(ItemData.ItemCategory.Head)
					+ StrengthOf(ItemData.ItemCategory.Neck)
					+ StrengthOf(ItemData.ItemCategory.Body)
					+ StrengthOf(ItemData.ItemCategory.Hand)
					+ StrengthOf(ItemData.ItemCategory.Legs);
		int speed = StrengthOf(ItemData.ItemCategory.Boot);

		_abilityValues[0].Text = attack.ToString();
		_abilityValues[1].Text = maxHearts.ToString();
		_abilityValues[2].Text = defense.ToString();
		_abilityValues[3].Text = speed.ToString();

		// Swap the Attack chip's icon to the equipped weapon's sprite so the
		// row reads "this is the weapon you're using" rather than a generic
		// stat glyph. Falls back to stat_0 (sword) when nothing is equipped.
		var attackIcon = GetNodeOrNull<TextureRect>("Panel/Frame/AbilityRow0/Chip/Icon");
		if (attackIcon != null)
		{
			_defaultAttackIcon ??= attackIcon.Texture;
			attackIcon.Texture = weapon?.Icon ?? _defaultAttackIcon;
		}
	}
	private Texture2D _defaultAttackIcon;

	private static int StrengthOf(ItemData.ItemCategory cat)
		=> Inventory.GetEquipped(cat)?.Strength ?? 0;

	private void RefreshAppearance()
	{
		for (int i = 0; i < AppearanceCategories.Length; i++)
		{
			if (_appearanceIcons[i] == null) continue;
			var item = Inventory.GetEquipped(AppearanceCategories[i]);
			_appearanceIcons[i].Texture = item?.Icon ?? GD.Load<Texture2D>(
				$"res://assets/sprites/ui/inventory/equipslot_{EquipPlaceholderIndex[i]}.png");
		}
	}

	private void RefreshGrid()
	{
		for (int i = 0; i < Inventory.SlotCount; i++)
		{
			var item = Inventory.GetSlotItem(i);
			var qty = Inventory.GetSlotQuantity(i);
			if (item != null)
			{
				_slotIcons[i].Texture = item.Icon;
				_slotQtyLabels[i].Text = qty > 1 ? $"x{qty}" : "";
			}
			else
			{
				_slotIcons[i].Texture = null;
				_slotQtyLabels[i].Text = "";
			}
		}
	}

	private void RefreshHighlight()
	{
		if (_slotCursor == null) return;
		int col = _selectedSlot % GridCols;
		int row = _selectedSlot / GridCols;
		_slotCursor.OffsetLeft = _gridX + col * (CellSize + CellGap);
		_slotCursor.OffsetTop = _gridY + row * (CellSize + CellGap);
		_slotCursor.OffsetRight = _slotCursor.OffsetLeft + CellSize;
		_slotCursor.OffsetBottom = _slotCursor.OffsetTop + CellSize;
	}

	private void RefreshDetails()
	{
		var item = Inventory.GetSlotItem(_selectedSlot);

		foreach (var c in _detailsStats.GetChildren()) c.QueueFree();

		if (item == null)
		{
			_detailsName.Text = "";
			_detailsDesc.Text = "";
			if (_detailsItemIcon != null) _detailsItemIcon.Visible = false;
			if (_detailsItemCursor != null) _detailsItemCursor.Visible = false;
			if (_detailsActionBtn != null) _detailsActionBtn.Visible = false;
			// Free any chips left over from the prior selection so an empty
			// slot doesn't show a dangling Equip / Sell button.
			foreach (var old in _activeActionChips)
				if (GodotObject.IsInstanceValid(old)) old.QueueFree();
			_activeActionChips.Clear();
			return;
		}

		bool equipped = Inventory.IsEquipped(item.Id);
		// Equipped state is communicated by the live preview (item visible
		// on the character) and the chip's "Unequip" label. Key / quest
		// items get a "★ Quest Item" line in the stat row instead of a
		// title prefix — same vertical slot the +N stat modifier uses for
		// normal gear, so the layout stays balanced and the title doesn't
		// shift right.
		_detailsName.Text = item.Name;
		_detailsDesc.Text = item.Description ?? "";

		// Big item icon next to the description, with a slot-cursor frame
		// wrapping it so the top-right preview matches the gold-bordered
		// highlight vocabulary the grid uses.
		if (_detailsItemIcon != null)
		{
			_detailsItemIcon.Texture = item.Icon;
			_detailsItemIcon.Visible = item.Icon != null;
		}
		if (_detailsItemCursor != null)
		{
			_detailsItemCursor.Visible = item.Icon != null;
		}

		if (item.IsEquippable || item.IsConsumable)
		{
			int displayValue = item.IsConsumable ? Mathf.CeilToInt(item.Strength / 2f) : item.Strength;
			var icon = StatIconFor(item.Category);
			if (icon != null)
			{
				var sign = displayValue >= 0 ? "+" : "";
				Control arrow = null;
				if (item.IsEquippable && !equipped)
				{
					var current = Inventory.GetEquipped(item.Category);
					if (current != null) arrow = BuildDirectionArrow(item.Strength - current.Strength);
				}
				// Pass arrow to the stat builder so it nests tight against
				// the icon (separation 1) instead of inheriting the wider
				// 4 px text-icon spacing.
				_detailsStats.AddChild(BuildInlineStat($"{sign}{displayValue}", icon, arrow));
			}
		}
		else if (item.IsKeyItem)
		{
			// "★ Quest Item" line — slots into the same row as +N stat
			// modifiers for normal gear. Gold star + moss-green label so
			// the marker reads as related to the description, not as a
			// separate UI chip.
			_detailsStats.AddChild(BuildKeyItemMarker());
		}
		// Gem cost is intentionally NOT shown in the inventory details —
		// the player only sees a gem amount when they're standing in a
		// shop and the action chip flips to a Sell chip with the price.

		// Action chip: Equip / Unequip / Use, or Sell when in a shop.
		UpdateActionButton(item, equipped);
	}

	/// <summary>Bouncing up/down arrow: green up for a stat upgrade, red
	/// down for a sidegrade. Returns null on a 0-diff so equal-strength
	/// items render no arrow.
	///
	/// The wrapper is a fixed-size Control so the inner TextureRect can be
	/// position-tweened freely; if we tried to tween a TextureRect parented
	/// directly to an HBoxContainer, the container's resort would clobber
	/// the position each layout pass. The wrapper takes the layout slot,
	/// the inner arrow oscillates inside it.</summary>
	private static Control BuildDirectionArrow(int diff)
	{
		if (diff == 0) return null;
		bool up = diff > 0;
		var wrapper = new Control
		{
			Name = "DirectionArrow",
			CustomMinimumSize = new Vector2(14, 22),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
		};
		var arrow = new TextureRect
		{
			Texture = up ? UiStyles.ArrowUp : UiStyles.ArrowDown,
			Modulate = up ? new Color(0.42f, 0.82f, 0.36f) : new Color(0.92f, 0.32f, 0.28f),
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		arrow.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		wrapper.AddChild(arrow);

		// Bounce loop — green arrows nudge up (-y), red arrows nudge down
		// (+y). Sine-eased so it reads as drift rather than a snap.
		wrapper.TreeEntered += () =>
		{
			float bounce = up ? -3f : 3f;
			var tween = wrapper.CreateTween().SetLoops();
			tween.TweenProperty(arrow, "position:y", bounce, 0.4)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			tween.TweenProperty(arrow, "position:y", 0f, 0.4)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		};
		return wrapper;
	}

	/// <summary>"★ Quest Item" inline row used for key / quest items in
	/// place of a stat modifier. Cream star (matches the DetailsName title
	/// face for the "pop") + moss-green label at body-text size so the
	/// marker reads as part of the description block. Position is driven
	/// by the scene's DetailsStats offsets — edit those in the Godot
	/// editor to move the row.
	///
	/// Star and label both bottom-align inside the HBox so the glyphs
	/// share a baseline regardless of the font-size delta (Alagard 22 vs
	/// Jersey 20). Without this the star sits a few px high.</summary>
	private static HBoxContainer BuildKeyItemMarker()
	{
		var box = new HBoxContainer();
		box.AddThemeConstantOverride("separation", 4);
		box.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;

		var star = new Label
		{
			Text = "★",
			VerticalAlignment = VerticalAlignment.Bottom,
			SizeFlagsVertical = Control.SizeFlags.Fill,
		};
		star.AddThemeFontSizeOverride("font_size", 22);
		star.AddThemeColorOverride("font_color", UiStyles.Cream);
		box.AddChild(star);

		var label = new Label
		{
			Text = "Quest Item",
			VerticalAlignment = VerticalAlignment.Bottom,
			SizeFlagsVertical = Control.SizeFlags.Fill,
		};
		label.AddThemeFontOverride("font", UiFonts.Body);
		// Match DetailsDesc font_size (20) so the line reads as part of
		// the description block rather than a separate chip.
		label.AddThemeFontSizeOverride("font_size", 20);
		label.AddThemeColorOverride("font_color", new Color(0.23529412f, 0.4117647f, 0.101960786f));
		box.AddChild(label);

		return box;
	}

	private static HBoxContainer BuildInlineStat(string text, Texture2D icon, Control arrow = null)
	{
		var box = new HBoxContainer();
		// 4 px separation so the modifier digit doesn't crowd the icon —
		// reads as "+N · shield" with a clear gap, not as one cluster.
		box.AddThemeConstantOverride("separation", 4);

		var label = new Label
		{
			Text = text,
			VerticalAlignment = VerticalAlignment.Center,
		};
		label.AddThemeFontOverride("font", UiFonts.Body);
		label.AddThemeFontSizeOverride("font_size", 18);
		// Match the DetailsDesc dark-moss tint (#3C691A in the .tscn) so the
		// "+N" reads as part of the description block, not a contrasting
		// chip. RGB 0.235 / 0.412 / 0.102 = the same color the desc uses.
		label.AddThemeColorOverride("font_color", new Color(0.23529412f, 0.4117647f, 0.101960786f));
		box.AddChild(label);

		var iconRect = new TextureRect
		{
			Texture = icon,
			CustomMinimumSize = new Vector2(20, 20),
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

		// When an arrow is provided, nest it tight against the icon (1 px
		// separation) so the green/red hint reads as part of the icon
		// composite — not pushed away by the box's wider 4 px text-icon
		// spacing.
		if (arrow != null)
		{
			var iconArrow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			iconArrow.AddThemeConstantOverride("separation", 1);
			iconArrow.AddChild(iconRect);
			iconArrow.AddChild(arrow);
			box.AddChild(iconArrow);
		}
		else
		{
			box.AddChild(iconRect);
		}

		return box;
	}

	/// <summary>Stat icon for item-details inline +N rows. Pulls from the
	/// C3 UI_StateSprite frames (stat_0..stat_4): 0=sword (Attack), 1=heart
	/// (Max Hearts), 2=shield (Defense), 3=boot (Speed), 4=spare.</summary>
	private static Texture2D StatIconFor(ItemData.ItemCategory cat) => cat switch
	{
		ItemData.ItemCategory.Weapon => GD.Load<Texture2D>("res://assets/sprites/ui/inventory/stat_0.png"),
		ItemData.ItemCategory.Food => GD.Load<Texture2D>("res://assets/sprites/ui/inventory/stat_1.png"),
		ItemData.ItemCategory.Boot => GD.Load<Texture2D>("res://assets/sprites/ui/inventory/stat_3.png"),
		ItemData.ItemCategory.Head or ItemData.ItemCategory.Neck or ItemData.ItemCategory.Body
			or ItemData.ItemCategory.Hand or ItemData.ItemCategory.Legs
			=> GD.Load<Texture2D>("res://assets/sprites/ui/inventory/stat_2.png"),
		_ => null,
	};

	private void UpdateCyclerLabels()
	{
		// Hair STYLE keeps the text label — variation is shape, not color.
		if (_hairLabel != null)
			_hairLabel.Text = CharacterCustomization.HairStyleCount > 0
				? CharacterCustomization.HairStyleName(_hairIndex)
				: "Hair —";

		// Hair COLOR + SKIN show 5-swatch preview rows. The labels were
		// hidden in BuildSwatchRows; the swatch row IS the language now.
		UpdateSwatchRow(_hairColorSwatches, _hairColorIndex,
			CharacterCustomization.HairColorCount,
			CharacterCustomization.DominantHairColor);
		UpdateSwatchRow(_skinSwatches, _skinIndex,
			CharacterCustomization.SkinCount,
			CharacterCustomization.DominantSkinColor);
	}

	private void RefreshGems()
	{
		if (_gemLabel == null) return;
		// Just the number — the gem icon to the right of the label is the
		// unit. Right-aligned in the .tscn (horizontal_alignment=2) so the
		// digits hug the icon's left edge.
		_gemLabel.Text = CurrencySystem.GetGems().ToString();
	}
}
