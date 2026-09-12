# Configuration

[← README](../README.md) · [Player guide](player-guide.md) · [Development](development.md)

Server file, per-board settings, database location, and upgrade notes.

## Global mod config (removed)

The mod no longer reads or writes `ModConfig/noticeboard.json`.

Older versions created that file with a single key:

```json
{
  "DivisionForPapersOnBoard": 1.0
}
```

That key never affected visible paper count (Max Papers on each board controls that, **1–50**, default **20**). Admins can delete `ModConfig/noticeboard.json` if it is still present.

## Per-board settings (Settings tab)

Owner-only UI. Values persist in the SQLite `noticeBoard` table.

### Board

| Setting | Default | Notes |
| --- | --- | --- |
| Board ID | (generated) | Read-only |
| Board Name | `Notice Board` | |
| Board Owner | placer | Dropdown of players who have joined (UID stored in `players`) |
| Permission Mode | Default (`0`) | Default / All (`1`) / Locked (`2`). Legacy `isLocked=1` migrated to Locked |
| Enable Parchment | **on** (`1`) | |
| Manual Pin | **on** (`1`) | On: freeze every notice where it is; new ones use the look-at-cork ghost (same sheet as will hang, light green/red tint), then right-click. Off: Pin auto-places and reshuffles the whole board, including notices you clicked earlier. Existing boards that already stored off stay off. Legacy Board ignores this and auto-places. |
| Discord ping | **off** (`0`) | Owner switch. New pins POST to that board’s webhook when this is on and a URL is stored |
| Webhook | empty | Write-only Discord webhook URL (`noticeBoard.discordWebhook`). Never on `NoticeBoardObject`, never shown after save. Leave the field blank to keep the current URL; Clear removes it |
| Max Papers | **20** | Slider **1–50**. Legacy Board visually caps at **6** baked papers |
| Sway Strength | **50** | **0–100**, step **5**. `0` = still papers. Max amplitude **0.12** blocks at full strength |
| Enable Particles | **on** (`1`) | Unread gold sparkles |
| Paper Aging | **off** (`0`) | Yellows hanging notices, then expires them to a ground parchment after the lifetime below |
| Notice aging days | world’s `DaysPerMonth` | In-game days, minimum **1**. Hidden in Settings when Paper Aging is off. New boards store the calendar month length (vanilla **9**). Existing boards keep whatever they already stored. |
| Enable Proximity | **off** (`0`) | UI hidden unless [TheBasics](https://mods.vintagestory.at/thebasics) (`thebasics`) is loaded |
| Channel Name | `Proximity` | Must match an existing player group name |
| Distance | **100** | **1–1000** Manhattan blocks |

### Appearance

| Setting | Default | Notes |
| --- | --- | --- |
| Board Font | `Ari-W9500` | `Default` plus bundled fonts: Alagard, Ari-W9500, Determination, Graph 35+ pix, MedievalSharp, Minecraft Standard, RuneScape UF, Venice Classic |
| Board Font Size | **16** | **10–60**. Default **16** is paper body **5** / header **12** / date **8**. The slider scales the Messages list, hanging sheets, and overlay preview |
| Board Theme | Classic Aged | Classic Aged, Dark Medieval, Royal Ivory, Burned Edges, Desert Map, Moldy Archive, Vampire Manuscript, Frosted Paper, Dwarven Ledger, Elven Songbook |
| Sharpness | **2×** | **1–4×**. Unset/`0` in DB resolves to **2** |
| Legacy Board | **off** | Sets types to aged-brass and baked `messageCount` 0-6 shapes; uncheck restores the previous wood and metal (kept on the block/item attributes). Disables procedural papers, permits, and dynamic text |

**Save All Settings** sends one network packet per changed field. Each manage packet requires board owner or server role `admin` on the server.

## Proximity + TheBasics

Optional integration; not a hard dependency.

When proximity is enabled, the channel group exists, and group uid ≠ 0, posting a notice broadcasts to nearby players on that group:

`{RP nick or name} left new notice on the board ({BoardName})`

RP nick comes from `TheBasicsNick.Resolve`: TheBasics `GetNickname` when `thebasics` is loaded, else mod data key `BASIC_NICKNAME`, else the account name.

## Discord webhook

Each board has its own webhook. The owner enables **Discord ping** and pastes a URL in Settings. The URL is stored in `noticeBoard.discordWebhook` in the same SQLite file. It is never sent to clients (`HasDiscordWebhook` is a 0/1 flag only).

Operators who do not own the board (privilege `controlserver`):

```
/nbdiscord webhook <boardId> <url>
/nbdiscord webhook <boardId>
/nbdiscord status <boardId>
```

Omit the URL to clear. `status` never prints the URL. Board ID is the read-only field on the Settings tab.

A successful compose or Pin Parchment then POSTs `A new notice hangs on {board}. {name} posted it on {date}.` (immersive in-game date, no HUD coords). `{name}` is the TheBasics nick when that mod is loaded, else the account name. Anonymous notices use Someone. The notice body is not sent. Edits, deletes, bumps, and expires do not ping.

## Database

**Path:** `{GamePaths.DataPath}/ModData/{SavegameIdentifier}/noticeboard/noticeboard.db`

If the save identifier is empty, the folder uses `global`.

**Tables:** `players` (`playerName` is the account; `displayName` is the TheBasics nick when set; message joins use `COALESCE(NULLIF(displayName, ''), playerName)`), `noticeBoard`, `messages`, `playerBoardReads`. Owner dropdown still uses `playerName`.

Schema migrations add missing columns on startup. The column `enableCustomPaperTextures` was renamed to `enableLegacyBoard`.

Breaking a board does **not** delete its database row; the `uniqueID` on the item stack keeps messages when the board is picked up and re-placed.

## Upgrading from older versions

**3.0.0 drops the old wood/metal/count block IDs.** There are eight orientation IDs; materials live on `types`. Boards placed before 3.0.0 will not convert. Break them and craft new ones. Keep the ModData SQLite database. Install Attribute Rendering Library 3.2.0 with this version.

### Old config folder

Very old builds used a `ModConfig/noticeboard` **directory**. Delete that **folder** only. You can also delete leftover `ModConfig/noticeboard.json`; the mod no longer uses it. Keep the ModData SQLite database.

### Block remaps

`assets/noticeboard/patches/config/remap.json` registers Vintage Story `/bir remap` lists for:

- **Noticeboard-1.2.0**: old `-{n}-{side}` codes → ground variants
- **Noticeboard-2.2.0**: pre wood/metal variant boards → `aged-brass`

Those lists stay for saves that already applied them. They do not migrate 2.x wood/metal boards onto 3.0.0's eight IDs.
