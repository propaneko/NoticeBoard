using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using NoticeBoard.Packets;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace NoticeBoard.src.Gui.Windows;

public partial class NoticeBoardMainWindowGui
{
    private void PopulateSettingsTab(GuiComposer composer, ElementBounds insetBounds)
    {
        string[] fontValues = FontManager.FontDisplayNames;
        string[] fontFileNames = FontManager.FontFileNames;

        int checkboxWidth = 50;
        int inputWidth = 200;
        int sliderWidth = 200;

        bool isOwner = (this.boardPlayerId == capi.World.Player.PlayerUID);
        bool isProximityLoaded = capi.ModLoader.IsModEnabled("thebasics");

        ElementBounds leftBounds = ElementBounds
            .FixedSize(280, 30)
            .WithFixedPosition(insetBounds.fixedX + 10, insetBounds.fixedY + 10);
        ElementBounds rightBounds = ElementBounds
            .FixedSize(inputWidth, 30)
            .WithFixedPosition(insetBounds.fixedX + 300, insetBounds.fixedY + 10);

        // Board ID
        composer.AddStaticText(
            Lang.Get("noticeboard:settings-board-id"),
            CairoFont.WhiteSmallText(),
            leftBounds
        );
        composer.AddStaticText(
            this.boardId ?? "Unknown",
            CairoFont.WhiteSmallText(),
            rightBounds.WithFixedWidth(350)
        );

        // Board Name
        leftBounds = leftBounds.BelowCopy(0, 10);
        rightBounds = rightBounds.BelowCopy(0, 5).WithFixedSize(inputWidth, 30);
        composer.AddStaticText(
            Lang.Get("noticeboard:settings-board-name"),
            CairoFont.WhiteSmallText(),
            leftBounds
        );

        composer.AddTextInput(
            rightBounds,
            (text) =>
            {
                this.boardName = text;
                UpdateDirtyState();
            },
            CairoFont.WhiteSmallText(),
            "boardNameInput"
        );
        composer.GetTextInput("boardNameInput").SetValue(this.boardName ?? "");

        // Board Owner
        leftBounds = leftBounds.BelowCopy(0, 10);
        rightBounds = rightBounds.BelowCopy(0, 10).WithFixedSize(inputWidth, 30);
        composer.AddStaticText(
            Lang.Get("noticeboard:settings-board-owner"),
            CairoFont.WhiteSmallText(),
            leftBounds
        );

        composer.AddDropDown(
            [.. players.Select(p => p.PlayerUID)],
            [.. players.Select(p => p.PlayerName)],
            players.FindIndex(p => p.PlayerUID == boardPlayerId),
            (code, selected) =>
            {
                this.pendingOwnerUid = code;
                UpdateDirtyState();
            },
            rightBounds,
            "ownerDropdown"
        );

        // Board Font
        leftBounds = leftBounds.BelowCopy(0, 10);
        rightBounds = rightBounds.BelowCopy(0, 10).WithFixedSize(inputWidth, 30);
        composer.AddStaticText(
            Lang.Get("noticeboard:settings-board-font"),
            CairoFont.WhiteSmallText(),
            leftBounds
        );

        if (fontValues.Length > 0)
        {
            int selectedFontIndex = Array.IndexOf(fontValues, this.boardFont ?? fontValues[0]);
            if (selectedFontIndex < 0)
                selectedFontIndex = 0;
            composer.AddDropDown(
                fontValues,
                fontFileNames,
                selectedFontIndex,
                (value, selected) =>
                {
                    this.boardFont = value;
                    UpdateDirtyState();
                },
                rightBounds,
                "fontDropdown"
            );
        }
        else
        {
            composer.AddStaticText("No fonts found.", CairoFont.WhiteSmallText(), rightBounds);
        }

        // Board Font Size
        leftBounds = leftBounds.BelowCopy(0, 10);
        rightBounds = rightBounds.BelowCopy(0, 10).WithFixedSize(sliderWidth, 30);

        this.pendingFontSize = Math.Max(10, this.boardFontSize);

        composer.AddDynamicText(
            $"{Lang.Get("noticeboard:settings-board-font-size")} {this.pendingFontSize}",
            CairoFont.WhiteSmallText(),
            leftBounds,
            "fontSizeLabel"
        );

        composer.AddSlider(
            (newValue) =>
            {
                this.pendingFontSize = newValue;
                composer
                    .GetDynamicText("fontSizeLabel")
                    .SetNewText($"{Lang.Get("noticeboard:settings-board-font-size")} {newValue}");
                UpdateDirtyState();
                return true;
            },
            rightBounds,
            "fontSizeSlider"
        );
        composer.GetSlider("fontSizeSlider").SetValues((int)this.pendingFontSize, 10, 60, 1);

        // Board Theme
        leftBounds = leftBounds.BelowCopy(0, 10);
        rightBounds = rightBounds.BelowCopy(0, 10).WithFixedSize(inputWidth, 30);
        composer.AddStaticText(
            Lang.Get("noticeboard:settings-board-theme"),
            CairoFont.WhiteSmallText(),
            leftBounds
        );

        string[] themeNames = ThemeManager.GetThemeNames();
        int selectedThemeIndex = Array.IndexOf(
            themeNames,
            this.boardTheme ?? (themeNames.Length > 0 ? themeNames[0] : "")
        );
        if (selectedThemeIndex < 0)
            selectedThemeIndex = 0;

        composer.AddDropDown(
            themeNames,
            themeNames,
            selectedThemeIndex,
            (code, selected) =>
            {
                this.boardTheme = code;
                UpdateDirtyState();
            },
            rightBounds,
            "themeDropdown"
        );

        // Lock Notice Board
        leftBounds = leftBounds.BelowCopy(0, 10);
        rightBounds = rightBounds.BelowCopy(0, 10).WithFixedSize(checkboxWidth, 30);
        composer.AddStaticText(
            Lang.Get("noticeboard:settings-board-lock"),
            CairoFont.WhiteSmallText(),
            leftBounds
        );

        composer.AddSwitch(
            (state) =>
            {
                this.isLocked = state;
                UpdateDirtyState();
            },
            rightBounds,
            "lockSwitch"
        );
        composer.GetSwitch("lockSwitch").On = this.isLocked;

        // Enable Parchment
        leftBounds = leftBounds.BelowCopy(0, 10);
        rightBounds = rightBounds.BelowCopy(0, 10).WithFixedSize(checkboxWidth, 30);
        composer.AddStaticText(
            Lang.Get("noticeboard:settings-board-parchment"),
            CairoFont.WhiteSmallText(),
            leftBounds
        );

        composer.AddSwitch(
            (state) =>
            {
                this.enableParchment = state;
                UpdateDirtyState();
            },
            rightBounds,
            "parchmentSwitch"
        );
        composer.GetSwitch("parchmentSwitch").On = this.enableParchment;

        // Enable Particles
        leftBounds = leftBounds.BelowCopy(0, 10);
        rightBounds = rightBounds.BelowCopy(0, 10).WithFixedSize(checkboxWidth, 30);
        composer.AddStaticText(
            Lang.Get("noticeboard:settings-board-particles"),
            CairoFont.WhiteSmallText(),
            leftBounds
        );

        composer.AddSwitch(
            (state) =>
            {
                this.enableParticles = state;
                UpdateDirtyState();
            },
            rightBounds,
            "particlesSwitch"
        );
        composer.GetSwitch("particlesSwitch").On = this.enableParticles;

        if (isProximityLoaded)
        {
            // Enable Proximity
            leftBounds = leftBounds.BelowCopy(0, 10);
            rightBounds = rightBounds.BelowCopy(0, 10).WithFixedSize(checkboxWidth, 30);
            composer.AddStaticText(
                Lang.Get("noticeboard:settings-board-proximity"),
                CairoFont.WhiteSmallText(),
                leftBounds
            );

            composer.AddSwitch(
                (state) =>
                {
                    this.enableProximityMessage = state;
                    UpdateDirtyState();
                },
                rightBounds,
                "proximitySwitch"
            );
            composer.GetSwitch("proximitySwitch").On = this.enableProximityMessage;
        }

        // Proximity Channel Name
        leftBounds = leftBounds.BelowCopy(0, 10);
        rightBounds = rightBounds.BelowCopy(0, 10).WithFixedSize(inputWidth, 30);
        composer.AddStaticText(
            Lang.Get("noticeboard:settings-board-proximity-channel"),
            CairoFont.WhiteSmallText(),
            leftBounds
        );

        composer.AddTextInput(
            rightBounds,
            (text) =>
            {
                this.proximityChannel = text;
                UpdateDirtyState();
            },
            CairoFont.WhiteSmallText(),
            "channelInput"
        );
        composer.GetTextInput("channelInput").SetValue(this.proximityChannel ?? "");

        // Proximity Distance
        leftBounds = leftBounds.BelowCopy(0, 10);
        rightBounds = rightBounds.BelowCopy(0, 10).WithFixedSize(sliderWidth, 30);
        this.pendingDistance = (int)Math.Max(1, this.proximityDistance);
        composer.AddDynamicText(
            $"{Lang.Get("noticeboard:settings-board-proximity-distance")} {this.pendingDistance}",
            CairoFont.WhiteSmallText(),
            leftBounds,
            "distanceLabel"
        );

        composer.AddSlider(
            (newValue) =>
            {
                this.pendingDistance = newValue;
                composer
                    .GetDynamicText("distanceLabel")
                    .SetNewText($"Proximity Distance: {newValue}");
                UpdateDirtyState();
                return true;
            },
            rightBounds,
            "distanceSlider"
        );
        composer.GetSlider("distanceSlider").SetValues(this.pendingDistance, 1, 1000, 1);

        // Save Button
        ElementBounds btnSaveAllBounds = leftBounds.BelowCopy(0, 20).WithFixedSize(160, 30);
        composer.AddSmallButton(
            Lang.Get("noticeboard:settings-board-save"),
            OnSaveAllSettingsClick,
            btnSaveAllBounds,
            EnumButtonStyle.Normal,
            "btnSaveAll"
        );
        composer.GetButton("btnSaveAll").Enabled = false;
    }

    private void UpdateDirtyState()
    {
        var p = this.noticeBoardPacket.BoardProperties;

        this.isDirty =
            (this.isLocked != (p.IsLocked != 0))
            || (this.enableParticles != (p.EnableParticles != 0))
            || (this.enableParchment != (p.EnableParchment != 0))
            || (this.enableProximityMessage != (p.EnableProximity != 0))
            || (this.proximityChannel != p.ProximityChannel)
            || (this.boardName != p.BoardName)
            || (this.boardFont != p.BoardFont)
            || (this.pendingFontSize != p.BoardFontSize)
            || (this.boardTheme != p.BoardTheme)
            || (this.pendingDistance != p.ProximityDistance)
            || (!string.IsNullOrEmpty(this.pendingOwnerUid) && this.pendingOwnerUid != p.PlayerId);

        this.SingleComposer?.GetButton("btnSaveAll")?.Enabled = this.isDirty;
    }

    private bool OnSaveAllSettingsClick()
    {
        var p = this.noticeBoardPacket.BoardProperties;
        var channel = capi.Network.GetChannel("noticeboard");

        if (this.isLocked != (p.IsLocked != 0))
        {
            channel.SendPacket(
                new EditIsLocked { BoardId = this.boardId, IsLocked = this.isLocked }
            );
            p.IsLocked = this.isLocked ? 1 : 0;
        }
        if (this.enableParticles != (p.EnableParticles != 0))
        {
            channel.SendPacket(
                new EditEnableParticles
                {
                    BoardId = this.boardId,
                    EnableParticles = this.enableParticles,
                }
            );
            p.EnableParticles = this.enableParticles ? 1 : 0;
        }
        if (this.enableParchment != (p.EnableParchment != 0))
        {
            channel.SendPacket(
                new EditEnableParchment
                {
                    BoardId = this.boardId,
                    EnableParchment = this.enableParchment,
                }
            );
            p.EnableParchment = this.enableParchment ? 1 : 0;
        }
        if (this.boardName != p.BoardName)
        {
            channel.SendPacket(
                new EditBoardName { BoardId = this.boardId, BoardName = this.boardName }
            );
            p.BoardName = this.boardName;
        }
        if (this.boardFont != p.BoardFont)
        {
            channel.SendPacket(
                new EditBoardFont { BoardId = this.boardId, BoardFont = this.boardFont }
            );
            p.BoardFont = this.boardFont;
        }
        if (this.pendingFontSize != p.BoardFontSize)
        {
            channel.SendPacket(
                new EditBoardFontSize
                {
                    BoardId = this.boardId,
                    BoardFontSize = this.pendingFontSize,
                }
            );
            p.BoardFontSize = this.pendingFontSize;
        }
        if (this.boardTheme != p.BoardTheme)
        {
            channel.SendPacket(
                new EditBoardTheme { BoardId = this.boardId, BoardTheme = this.boardTheme }
            );
            p.BoardTheme = this.boardTheme;
        }
        if (this.enableProximityMessage != (p.EnableProximity != 0))
        {
            channel.SendPacket(
                new EditEnableProximity
                {
                    BoardId = this.boardId,
                    EnableProximity = this.enableProximityMessage,
                }
            );
            p.EnableProximity = this.enableProximityMessage ? 1 : 0;
        }
        if (this.proximityChannel != p.ProximityChannel)
        {
            channel.SendPacket(
                new EditProximityChannel
                {
                    BoardId = this.boardId,
                    ChannelName = this.proximityChannel ?? "",
                }
            );
            p.ProximityChannel = this.proximityChannel;
        }
        if (this.pendingDistance != p.ProximityDistance)
        {
            channel.SendPacket(
                new EditProximityDistance
                {
                    BoardId = this.boardId,
                    Distance = this.pendingDistance,
                }
            );
            p.ProximityDistance = this.pendingDistance;
        }

        if (!string.IsNullOrEmpty(this.pendingOwnerUid) && this.pendingOwnerUid != p.PlayerId)
        {
            channel.SendPacket(
                new EditBoardOwner { BoardId = this.boardId, NewOwnerUid = this.pendingOwnerUid }
            );
            p.PlayerId = this.pendingOwnerUid;
        }

        this.isDirty = false;
        this.SingleComposer.GetButton("btnSaveAll").Enabled = false;
        TryClose();
        return true;
    }

    private void ResetSettings()
    {
        var p = this.noticeBoardPacket.BoardProperties;
        this.isLocked = p.IsLocked != 0;
        this.enableParticles = p.EnableParticles != 0;
        this.enableParchment = p.EnableParchment != 0;
        this.enableProximityMessage = p.EnableProximity != 0;
        this.proximityChannel = p.ProximityChannel;
        this.pendingDistance = (int)p.ProximityDistance;
        this.pendingFontSize = p.BoardFontSize;
        this.pendingOwnerUid = p.PlayerId;
        this.isDirty = false;
    }
}