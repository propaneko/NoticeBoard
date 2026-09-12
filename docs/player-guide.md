# Player guide

[← README](../README.md) · [Configuration](configuration.md) · [Development](development.md)

How to craft, place, and use Notice Board in-game.

## Required dependency

Install [Attribute Rendering Library](https://mods.vintagestory.at/attributerenderinglibrary) **3.2.0** on client and server. The game will not load this mod without it.

Boards placed in 2.x do not convert; break them and craft new ones.

## Crafting

The recipe is in the Survival Handbook under **Notice Board**. Grid pattern `NH_,WPW,SSS`:

| Key | Item |
| --- | --- |
| `N` | `game:metalnailsandstrips-{metal}` |
| `H` | `game:hammer-*` (tool) |
| `W` | `game:plank-{wood}` × **3** (two slots) |
| `P` | `game:paper-parchment` × **6** |
| `S` | `game:supportbeam-{wood}` × **2** (three slots) |

**Woods:** aged, veryaged, birch, oak, maple, pine, acacia, kapok, baldcypress, larch, redwood, ebony, walnut, purpleheart.

**Metals:** brass, copper, cupronickel, tinbronze, bismuthbronze, blackbronze, iron, meteoriciron, steel, gold, silver, bismuth, molybdochalkos, electrum.

There is also a 1x1 recycle recipe that turns any `noticeboard:noticeboard-*` into `noticeboard:noticeboard-ground-north`. It keeps wood, metal, and inventory. It does not keep `uniqueID`; placing the result is a new board. Held `messageCount` is 0.

## Placement

- Aim at a **horizontal** face of an attachable block → wall board facing that support.
- Otherwise → ground board, yaw from your look direction.
- Opening the GUI needs land-claim **Use**, or **Traverse** if the claim allows it (e.g. `/land allowtraverseeveryone`).
- Right-click opens the GUI when you are within **6** blocks (measured from your eye to the board). Vanilla picking is 4.5, so past that the mod traces the look ray itself. Creative extra reach does not extend this past 6. If your hotbar holds another Notice Board block, vanilla placement wins and the GUI does not open. There is no sneak/shift action. The block interaction helper lists this as **Open notice board** when vanilla can pick the block.
- Breaking the block drops it with its `uniqueID` and inventory, including in creative. The drop is always **north** (`noticeboard-ground-north`) so wall and other-facing boards can be placed on the floor or against a wall again. Messages stay with that ID when the board is placed again, so a creative drop can be placed as a second board that shares the same ID.
- Held tooltip: *It has some messages attached* if the stack has a `uniqueID`.
- Block info overlay shows custom name, owner, and permission mode when set.

## Lanterns

- **Ctrl + right-click** a **small lantern** onto the board to hang it (up to two: left post, then right). Same **6** block reach as opening the GUI.
- They hang by the lantern's own metal ring on a wooden arm with a wood brace capped in metal at both ends. The lamp sways with **Sway Strength** (same setting as papers); storms swing it harder, around the ring rather than only in and out of the board. Papers and lamps lean with the wind direction; the ring stays on the boom. Papers shade the cork; lamps shade the ground and cork. Each lamp lights from the cage, not from inside the cork, so paper shade on the board still reads in daylight. A lining (gold, silver, or electrum) makes that light a bit stronger, same orange as a placed lantern.
- Take them with **Ctrl + right-click and an empty hand**, one per click (right first if both hang).
- Hidden slots; not in the GUI.

## Reading a hanging notice

Look at a sheet and hold **Preview notice under crosshair** (default **R**, rebind in Controls). When the board has notices, the same line appears in the block interaction helper with your bound key. A dimmed overlay shows the full text, taller than the physical sheet when the note is long (up to three sheet-heights, then the same `…` overflow as the hanging paper). The overlay is as tall as the window when the note is long enough, and capped at **45%** of the window width. Release the key to hide it. Client-only. It does not run while a dialog has the mouse, past **6** blocks, or on a sheet with no text. The preview is a HUD layer above the block interaction helper. When **Paper Aging** is on, yellowing, tears, and faded letters match the hanging sheet.

The same overlay opens from the Messages list (below) and draws on top of the board window (the GUI stays open underneath). Click anywhere on the dim or the overlay sheet to close a pinned preview. Closing the board GUI also closes it.

## Using the board

Right-click the board → **Messages** tab.

### Parchment on (default)

Five inventory slots appear. Slots 0–3 are blank cost. Slot 4 is for **Pin Parchment**.

- **Compose notice** (slots 0–3): accepts `paper-parchment`, `paper-parchment-*`, `papyrus-paper`, or `papyrus-paper-*`. The server consumes **1** sheet when you pin.
- **Pin Parchment** (slot **4** only): signed `paper-parchment` / `paper-parchment-*` with a non-empty `text` attribute. **Papyrus is not accepted here.** Authorship comes from the `signedby` attribute when present.

### Parchment off

Cost slots and Pin Parchment are hidden. **Compose notice** posts with no item.

### Locked mode

**Compose notice** is disabled for non-owners.

### Discord ping

The owner can enable **Discord ping** in Settings and paste a channel webhook. Leave the field blank when saving to keep the current URL. **Clear webhook** removes it. A new pin (compose or Pin Parchment) posts a two-sentence line: the board name, then who posted it and the immersive in-game date (no HUD coords). Who is the [TheBasics](https://mods.vintagestory.at/thebasics) nick when that mod is loaded, else the account name, else Someone if anonymous. The notice body is not sent. The webhook URL is never shown again after save.

### Message list

Each notice shows author (blank if anonymous), an immersive in-game date, and the body. When [TheBasics](https://mods.vintagestory.at/thebasics) is loaded, the author is that player's nick; otherwise it is the account name. Hanging sheets, the overlay, and a returned parchment's Written by use the same name. Hovering a row dims the other hanging sheets so the matching one stands out. Click the paper or the body text to pin the full-sheet overlay (same as holding R in the world). Ink buttons and VTML links do not open it.

Type in **Search notices** and press **Enter** to filter by notice text or author (anonymous notices match text only). The sort dropdown is **Newest** (server order), **Oldest**, or **Author**. Sorting and a new message list from the server both refresh the visible rows; typing does not.

**Edit**, **delete**, **bump**, and **move** ink buttons appear only when the client thinks you may edit (same rules as the server, below). **Move** is hidden when Manual Pin is off or Legacy Board is on. A **map** ink button appears on every notice that has a location attached; any reader can click it to add a waypoint at those HUD X/Z (`y=0`) using the notice's **name**, **icon**, and **color** (default steelblue circle). Empty name uses the board name. `/tpwp` to that pin may land at world height 0.

- **Edit**: reopens the composer. After you save on a board with **Manual Pin** on (and Legacy Board off), the GUI closes and the cork ghost lets you move that notice. Esc or left-click keeps the new text at the old pin. Manual Pin off or Legacy Board: the edit saves and you stay in the message list.
- **Delete**: removes the notice. If parchment is enabled, the server returns a **signed** `game:paper-parchment`. Hover shows **Written by**. The item name is author and date. Right-click reads the body (no ink). The sheet also keeps pin style and paper look, restored by **Pin Parchment**.
- **Bump**: moves the notice to the top of the list (`updatedAt` is set to now; messages are ordered by `updatedAt DESC`, then `id DESC`).
- **Move**: closes the GUI and starts the cork ghost for that notice without changing the text (same ghost as after saving an edit).

### Unread indicator

Gold sparkles can appear around a board you have not opened since the last new notice, if particles are enabled and you are within **32** blocks (checked about every **800** ms). Opening the GUI marks the board read for you.

## Composer

**Compose notice** or **Edit** opens the text window.

Toolbar buttons insert VTML:

- **B** / **I**: bold / italic
- **Big** / **Sml**: `<font size="24">` / `<font size="12">`
- **Link**, **Hdbk** (`handbook://`), **Cmd** (`command://`), **Icon**, **Item**
- Colors: Ink `#332211`, Red `#b22222`, Blue `#2a52be`, Green `#228b22`, Gold `#ffd700`
- **VTML Help**: opens https://wiki.vintagestory.at/VTML

Other controls:

- **Preview** switch
- **Send Anonymously**
- **Attach location**: off by default. On reveals editable **X** and **Z** (no height), **Name** (prefilled with the board name), **Icon** (vanilla waypoint icons, default circle), and **Color** (named colors `parsers.Color` accepts, default steelblue). First turn-on fills the coordinate HUD X/Z (relative to world spawn, same numbers as Ctrl+V); editing a notice that already has a location restores those coords plus the stored name, icon, and color. Pin Parchment documents cannot attach a location. The Messages list then shows a map ink button for any reader.
- **Holder**: Nail (default tack), Knife, Arrow, Stick, Bone, Spear, Chisel, Nails, or Cleaver. Visual only.
- **Paper**: Board default, or a specific parchment look. Board default tracks the Settings theme; a specific pick stays on that notice.
- Undo/redo: Ctrl+Z / Ctrl+Y. Ctrl+X with no selection deletes the current line.
- **Pin** (empty text is rejected). With **Manual Pin** on (default), every notice stays where it is (new ones still use a ghost: the same sheet that will hang after you pin, including wear when moving an aged notice, with a light green tint when the tack is valid and a light red tint when it is not; right-click still only pins when the nail is a valid cork tack). With it off, the notice auto-places and the whole board may reshuffle, including notices you clicked earlier. Saving an **Edit** uses that same ghost to move the notice you just saved. The ghost starts upright. Hold Left/Right to keep tilting; that lean is what gets pinned. Up/Down arrows set layer 0-3 (in front of other notices). The HUD shows current tilt and layer. Left-click or Esc cancels a new pin and returns to the composer; after an edit it keeps the previous pin and does not reopen the composer. Legacy Board always auto-places. Max papers still keeps the newest N notices, including hand-pinned ones.

**Character limits:** the compose field trims at **10000** characters on the client. The in-world hanging sheet cap is **4000** characters. You can type more than the physical paper will keep. The overlay preview can grow to three sheet-heights so a long note stays readable; past that it uses the same `…` overflow as the hanging sheet.

### Formatting on the physical paper

The **Messages** tab reading view uses Vintage Story's full VTML (`VtmlUtil`). **In-world hanging sheets** use a smaller subset only:

- `<strong>` / `<b>`, `<i>` / `<em>`
- `<font>` with `size`, `scale` (percent), `family`, `color`, `opacity`, `weight`, `lineheight`, `align`
- `<a>`, `<icon>`, `<itemstack>`, `<br>`, and raw newlines

The Board Font Size slider scales the Messages-tab list and the ink on hanging sheets and the overlay preview. Default **16** keeps paper body **5** and header **12**. Big / Sml stay in ratio to that body size. Author stays at the header size. Date is **8** at slider **16**. Anonymous notices start the body where the author line would be; signed notices keep a small gap under the name. The composer VTML preview stays at **18**.

Client command **.nbpinhud** toggles a two-line pin-ghost debug HUD (off by default). The composer **Cmd** button only inserts a `command://` VTML link for you to edit.

## Permissions

| Mode | Who can post | Who can edit / delete / bump |
| --- | --- | --- |
| **Default** | Anyone (with parchment rules) | Board owner, server role `admin`, or the original author |
| **All** | Anyone | Anyone |
| **Locked** | Owner or `admin` only | Owner or `admin` only |

The **Settings** tab is visible only to the board owner in the GUI. A player with role `admin` can still pass server checks for manage/edit actions, but they do **not** see Settings unless they own the board.

**Paper Aging** (off by default) yellows hanging notices as they sit. Yellowing is linear across the lifetime: yellower paper means less time left. While it is on, Settings shows a **Lifetime (days)** number (in-game days, default is the world’s days per month, vanilla **9**, minimum **1**). Side/bottom tears and faded letters ramp across the lifetime (stronger toward the end). Top edge stays intact around the holder. Tears are a continuous chewed edge, not a ring of holes. Hanging sheets and the overlay preview deepen the chewed edge as they age; Messages-list body text stays fully readable (no missing letters), only the paper edge wear matches. Raising Lifetime (days) stretches that same journey; hanging paper still updates about four times per in-game day. When lifetime runs out, the sheet peels off the cork and falls to the ground; the parchment appears when it lands (Pin Parchment can hang it again). Manual **Delete** still returns parchment to the deleter when posting parchment is on. Turning it on can immediately drop notices already past that lifetime.

The owner can transfer ownership from Settings; see [Configuration](configuration.md).
