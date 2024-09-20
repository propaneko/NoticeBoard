
```diff
- IMPORTANT! If you used the older version of my mod you need to remove "Y:\*\ModConfig\noticeboard\noticeboard.db" or crashes might happen!
```
## Basics

Notice board where you can post and read player notices. It's main purpose is to use it on the RP servers.

Crafting recipe is in Survival Handbook, item is called "Notice Board". You can also spawn it using creative gamemode.
You can of course place multiple Notice Boards, every one of them saves and shows their respective messages.

When you place it, just press rightclick and GUI will show. Hopefully it's intuitive enough :P

It is not hard dependency, but I suggest playing it with https://mods.vintagestory.at/thebasics


With this mod when you post new notice there will be broadcast on the Proximity channel for now its 100 blocks.

Will add some config option later if needed.

## Configuration

```js
{
  "SendProximityMessage": true, // set to true if you want to send message on Proximity channel
  "ProximityMessageDistance": 100, // distance how far the distance will be broadcasted (blocks)
  "DivisionForPapersOnBoard": 1.0 // if you set this on 2 for example, it will show one paper on board every two notice, 1.5 is also valid
}
```

## Ideas
need of use for the "paper-parchment" when trying to post message (to make it more immersive?)
Contribution

"Automatic_Yoba_Machine" - Thank you for providing this amazing new model!


Shield: [![CC BY 4.0][cc-by-shield]][cc-by]

This work is licensed under a
[Creative Commons Attribution 4.0 International License][cc-by].

[![CC BY 4.0][cc-by-image]][cc-by]

[cc-by]: http://creativecommons.org/licenses/by/4.0/
[cc-by-image]: https://i.creativecommons.org/l/by/4.0/88x31.png
[cc-by-shield]: https://img.shields.io/badge/License-CC%20BY%204.0-lightgrey.svg
