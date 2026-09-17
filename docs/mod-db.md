<div style="max-width: 860px; line-height: 1.55;"><div style="border: 2px solid #c4a574; padding: 12px 16px; margin: 0 0 1.2em;"><p style="margin: 0;"><strong>Required:</strong> install <a href="https://mods.vintagestory.at/attributerenderinglibrary">Attribute Rendering Library</a> <strong>3.2.0</strong> on client and server.&nbsp;</p>
</div>
<p style="text-align: center; margin: 0 0 0.5em;">☕ Feel free to donate me a cup of coffee if you can~ ☕</p>
<p style="text-align: center; margin: 0 0 0.5em;">&nbsp;</p>
<div style="border: 1px solid #c4a574; padding: 12px 16px; margin: 0 0 1.2em;">
<h2 style="border-bottom: 2px solid #c4a574; padding-bottom: 0.2em; margin: 0 0 0.6em;">Show me your boards</h2>
<p style="margin: 0;">I would be glad to see screenshots of how and where you use Notice Board: town squares, taverns, RP halls, shop walls. Post them in the comments on this page. If you are happy for me to share yours, I may add a gallery here later, with credit.</p>
</div>
<div style="border: 1px solid #c4a574; padding: 12px 16px; margin: 0 0 1.2em;">
<h2 style="border-bottom: 2px solid #c4a574; padding-bottom: 0.2em; margin: 0 0 0.6em;">What's new in 3.0.0</h2>
<p><strong>Breaking:</strong> 3.0.0 needs <a href="https://mods.vintagestory.at/attributerenderinglibrary">Attribute Rendering Library</a> 3.2.0. Recraft placed boards from 2.x. To make sure everything is working. All the boards that were placed previously will be broken.</p>
<ul>
<li><strong>Hanging papers</strong>: notices show on the board in-world (text + seals), not only in the GUI. Up to 50 sheets (default 20).</li>
<li><strong>Sway</strong>: papers and lanterns move; 0 is still. Culled at distance.</li>
<li><strong>Sharpness</strong>: 1-4× raster for in-world text.</li>
<li><strong>Wood + metal</strong> as stack attributes (ARL), eight orientation block IDs.</li>
<li><strong>Legacy Board</strong>: optional old baked 0-6 paper shapes (no dynamic sheets).</li>
<li><strong>Lanterns</strong>: hang up to two small lanterns (left post, then right). Ctrl + right-click to attach; empty hand + Ctrl + right-click to take one off. Lights the post area at night.</li>
<li><strong>Manual Pin</strong>: on (default): new ones use a look-at-cork ghost of the same sheet that will hang (light green tint when the tack is valid, light red when it is not). Right-click pins. Left/Right arrows tilt. Up/Down arrows layer 0-3. Left-click or Esc cancels. Off: auto-places and may reshuffle. Legacy Board always auto-places.</li>
<li><strong>Holders</strong>: Nail (default), Knife, Arrow, Stick, Bone, Spear, Chisel, Nails, or Cleaver. Visual only. Composer also has a per-notice Paper look (board default, or a fixed parchment theme).</li>
<li><strong>Edit pin</strong>: on a Manual Pin board, saving an edit closes the GUI and uses that same ghost to move the notice. Esc keeps the new text at the old pin. A move button on the list starts that ghost without editing.</li>
<li><strong>Preview</strong>: hold R (rebindable) while looking at a hanging notice, or click the paper or body on the Messages list. Same full-sheet overlay either way.</li>
<li><strong>Paper Aging</strong>: optional. Sheets yellow, tear, and fade over in-game days, then peel off as parchment.</li>
<li><strong>Attach location</strong>: optional X/Z waypoint on a compose. Any reader can click the map ink button to pin it. Pin Parchment cannot attach a location.</li>
<li><strong>Discord ping</strong>: optional per-board webhook. A new pin posts two sentences (board name, who, in-game date). Body and HUD coords are not sent. <strong>Who</strong> is the TheBasics nick when that mod is loaded.</li>
<li><strong>Scribe</strong>: optional. When <a href="https://mods.vintagestory.at/scribe">Scribe</a> is loaded, a tablet ink button on each notice copies it into the reader's writeable Scribe item (last opened, else first). First line is a checkbox task; the rest is a nested note. Not a hard dependency.</li>
<li><strong>Settings</strong> split into Board / Appearance / Proximity. Max Papers, Sway, Sharpness, Legacy Board, Discord, and Paper Aging are per-board.</li>
<li><strong>ModConfig removed</strong>: <code>DivisionForPapersOnBoard</code> did nothing useful; Max Papers is the control. Delete leftover <code>ModConfig/noticeboard.json</code> if you want.</li>
<li><strong>Locales</strong>: <code>en</code>, <code>es-es</code>, <code>pl</code>, <code>ru</code>, <code>uk</code> share the same keys.</li>
</ul>
<p style="margin: 0;">2.1.x features (parchment, WYSIWYG, themes, particles, permission modes, proximity) are unchanged.</p>
</div>
<div style="border: 1px solid #c4a574; padding: 12px 16px; margin: 0 0 1.2em;">
<p style="font-size: 1.1em; line-height: 1.5; margin: 0 0 0.8em;">Place a board, pin notices, read them in the GUI or on the hanging papers. RP boards, town ads, to-do lists, newsletters; same block. Each placed board keeps its own messages. Wood and metal variants. Wall or ground.</p>
<img style="display: block; width: 825px; height: 565px; border: 1px solid #c4a574;" src="https://i.imgur.com/kr2Fsyn.png" alt="Notice board in the world with hanging papers and lanterns"></div>
<div style="border: 1px solid #c4a574; padding: 12px 16px; margin: 0 0 1.2em;">
<h2 style="border-bottom: 2px solid #c4a574; padding-bottom: 0.2em; margin: 0 0 0.6em;">Get one</h2>
<p style="margin: 0;">Survival Handbook recipe <strong>Notice Board</strong>, or spawn it in creative.</p>
</div>
<div style="border: 1px solid #c4a574; padding: 12px 16px; margin: 0 0 1.2em;">
<h2 style="border-bottom: 2px solid #c4a574; padding-bottom: 0.2em; margin: 0 0 0.6em;">Use</h2>
<ol>
<li>Right-click the board (needs land-claim <strong>Use</strong>, or <strong>Traverse</strong> if the claim allows it).</li>
<li><strong>Messages</strong>: read the list. <strong>Compose notice</strong> writes a new one. <strong>Pin Parchment</strong> takes a signed parchment (right slot).</li>
<li><strong>Edit</strong> / <strong>delete</strong> / <strong>bump</strong> / <strong>move</strong> appear when you are allowed to. Delete with parchment enabled returns a signed parchment. After an edit on a Manual Pin board, look at the cork to move that notice; Esc keeps the previous pin. A <strong>map</strong> button appears on notices with a location; any reader can add that waypoint. When <a href="https://mods.vintagestory.at/scribe">Scribe</a> is loaded, a tablet button copies the notice into that player's Scribe item.</li>
<li><strong>Settings</strong> is owner-only. Name, owner, permission mode, parchment, Manual Pin, Discord ping, Paper Aging, fonts, theme, max papers, sway, sharpness, particles, legacy baked papers, optional proximity.</li>
<li><strong>Lanterns</strong>: Ctrl + right-click a small lantern onto the board (up to two). Take with Ctrl + right-click and an <strong>empty hand</strong>, one at a time (right first if both hang). Not shown in the GUI.</li>
<li><strong>Preview</strong>: hold <strong>R</strong> (rebindable) on a hanging sheet, or click the paper or body on the Messages list.</li>
</ol>
<p>Composer: VTML toolbar (bold, color, links, handbook, commands, icons, itemstacks), live preview, anonymous, Attach location, Holder (nail/knife/arrow/stick/bone/spear/chisel/nails/cleaver), Paper look, Ctrl+Z / Ctrl+Y. With Manual Pin on, Pin closes the GUI and the ghost follows your look. <a href="https://wiki.vintagestory.at/VTML">VTML help</a>.</p>
<p>&nbsp;</p>
<div style="display: flex; flex-wrap: wrap; gap: 12px; margin: 0.6em 0 0;">
<div style="flex: 1 1 280px; min-width: 240px;"><img style="display: block; width: 384px; height: 326px; border: 1px solid #c4a574;" src="https://i.imgur.com/tPVcRd2.png" alt="Messages tab"></div>
<div style="flex: 1 1 280px; min-width: 240px;"><img style="display: block; width: 358px; height: 323px; border: 1px solid #c4a574;" src="https://i.imgur.com/aiKcwCd.png" alt="Notice composer with preview"></div>
</div>
<img style="display: block; width: 781px; height: 875px; border: 1px solid #c4a574; margin-top: 0.6em;" src="https://i.imgur.com/k9aZYAs.png" alt="Settings tab: Board and Appearance"><img src="https://i.imgur.com/71WvOVg.png" alt="" width="782" height="183"></div>
<div style="border: 1px solid #c4a574; padding: 12px 16px; margin: 0 0 1.2em;">
<h2 style="border-bottom: 2px solid #c4a574; padding-bottom: 0.2em; margin: 0 0 0.6em;">Permission modes</h2>
<table>
<thead>
<tr>
<th>Mode</th>
<th>Who can post</th>
<th>Who can edit / delete / bump</th>
</tr>
</thead>
<tbody>
<tr>
<td><strong>Default</strong></td>
<td>Anyone</td>
<td>Owner, admin, or the author</td>
</tr>
<tr>
<td><strong>All</strong></td>
<td>Anyone</td>
<td>Anyone</td>
</tr>
<tr>
<td><strong>Locked</strong></td>
<td>Owner or admin</td>
<td>Owner or admin</td>
</tr>
</tbody>
</table>
<p>Pick up a board and it keeps messages (<em>It has some messages attached</em> on the tooltip). Recraft in the grid to reset to a blank aged-brass board.</p>
<p style="margin: 0;">Optional: <a href="https://mods.vintagestory.at/thebasics">TheBasics</a> (not required). Proximity chat when a notice is posted. When that mod is loaded, hanging sheets, the Messages list, returned parchment, and Discord use the RP nick instead of the account name.</p>
<p style="margin: 0.8em 0 0;">Optional: <a href="https://mods.vintagestory.at/scribe">Scribe</a> (not required). When that mod is loaded, a tablet ink button on each notice copies it into your last-opened writeable Scribe item (else the first writeable one). The first line becomes a checkbox task; the rest is a nested note. Scribe explains if you have no writeable item. The notice stays on the board.</p>
</div>
<div style="border-left: 4px solid #c4a574; padding: 0.5em 0 0.5em 0.9em; margin: 0 0 1.2em;">
<p style="margin: 0;"><strong>T</strong>here is no <code>ModConfig/noticeboard.json</code> anymore; you can delete leftover copies<strong>.</strong></p>
</div>
<p>Some fonts miss symbols; try another in Settings.</p>
<div class="spoiler">
<div class="spoiler-toggle">Example VTML message</div>
<div class="spoiler-text">
<p>&lt;strong&gt;&lt;font size="20" color="#ccf4af"&gt;The Fallen Observatory&lt;/font&gt;&lt;/strong&gt;</p>
<p>The ruins atop the northern ridge are said to have once tracked the movements of the stars with unnatural precision.</p>
<p>&lt;i&gt;Locals avoid the site.&lt;/i&gt; They claim the instruments still move at night, even though no wind reaches the summit.</p>
<p>&nbsp; &lt;icon name=wpCross&gt;&lt;/icon&gt; &lt;font size="20" weight="bold" lineheight="-2" &gt; Known Hazards&lt;/font&gt; &lt;icon name=wpCross&gt;&lt;/icon&gt;</p>
<p>The structure is unstable. Cracked stone floors may collapse under weight, especially after rainfall.</p>
<p>&lt;font color="#b33a3a"&gt;Presence of hostile &lt;itemstack floattype="left" type="item" code="creature-drifter-corrupt" rsize="1" offx="0" offy="0"&gt;&lt;/itemstack&gt; has been reported within the lower chambers. Prepare your &lt;itemstack floattype="left" type="item" code="blade-falx-copper" rsize="1" offx="0" offy="0"&gt;&lt;/itemstack&gt; !</p>
<p>&lt;a href="https://example.com"&gt;example&lt;/a&gt;<br>&lt;a href='handbook://item-flint'&gt;flint&lt;/a&gt;<br>&lt;a href='command:///kill'&gt;kill&lt;/a&gt;</p>
</div>
</div>
<div style="border: 1px solid #c4a574; padding: 12px 16px; margin: 1.2em 0;">
<h2 style="border-bottom: 2px solid #c4a574; padding-bottom: 0.2em; margin: 0 0 0.6em;">Contribution &amp; Thanks</h2>
<ul>
<li><strong>Automatic_Yoba_Machine</strong>: the model. Custom models: <a href="https://boosty.to/auto_yoba">boosty.to/auto_yoba</a></li>
<li><strong>BASIC</strong>: Proximity / TheBasics</li>
<li><strong>C4BR3R4</strong>: Spanish translation</li>
<li><strong>RaptorKhan</strong>: <a href="https://mods.vintagestory.at/scribe">Scribe</a> external-task API</li>
</ul>
<h2 style="border-bottom: 2px solid #c4a574; padding-bottom: 0.2em; margin: 1em 0 0.6em;">My other mods</h2>
<p style="margin: 0;"><a href="https://mods.vintagestory.at/unconscious">Unconscious</a>: unconscious behavior, mainly for multiplayer.</p></div></div>