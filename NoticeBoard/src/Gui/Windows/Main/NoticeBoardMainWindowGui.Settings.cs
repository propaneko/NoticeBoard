using System;
using System.IO;
using System.Linq;
using NoticeBoard.Packets;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace NoticeBoard.src.Gui.Windows;

public partial class NoticeBoardMainWindowGui
{
    private void PopulateSettingsTab(GuiComposer composer, ElementBounds insetBounds)
    {
        string fontsDir = Path.Combine(
            capi.ModLoader.GetMod("noticeboard").SourcePath,
            "assets/noticeboard/fonts"
        );

        string[] fontPaths = Directory.Exists(fontsDir)
            ? Directory.GetFiles(fontsDir, "*.ttf")
                  .Concat(Directory.GetFiles(fontsDir, "*.otf"))
                  .ToArray()
            : Array.Empty<string>();

        string[] fontValues = fontPaths.Select(Path.GetFileNameWithoutExtension).ToArray();
        string[] fontFileNames = fontPaths.Select(Path.GetFileNameWithoutExtension).ToArray();

        int checkboxWidth = 50;
        int inputWidth = 200;
        int sliderWidth = 200;

        bool isOwner = (this.boardPlayerId == capi.World.Player.PlayerUID);
        bool isProximityLoaded = capi.ModLoader.IsModEnabled("thebasics");

        ElementBounds leftBounds = ElementBounds.FixedSize(280, 30).WithFixedPosition(insetBounds.fixedX + 10, insetBounds.fixedY + 10);
        ElementBounds rightBounds = ElementBounds.FixedSize(inputWidth, 30).WithFixedPosition(insetBounds.fixedX + 300, insetBounds.fixedY + 10);

        // Board ID
        composer.AddStaticText("Board ID:", CairoFont.WhiteSmallText(), leftBounds);
        composer.AddStaticText(this.boardId ?? "Unknown", CairoFont.WhiteSmallText(), rightBounds.WithFixedWidth(300));

        // Board Name
        leftBounds = leftBounds.BelowCopy(0, 10);
        rightBounds = rightBounds.BelowCopy(0, 5).WithFixedSize(inputWidth, 30);
        composer.AddStaticText("Board Name:", CairoFont.WhiteSmallText(), leftBounds);
        if (isOwner)
        {
            composer.AddTextInput(rightBounds, (text) => { this.boardName = text; UpdateDirtyState(); }, CairoFont.WhiteSmallText(), "boardNameInput");
            composer.GetTextInput("boardNameInput").SetValue(this.boardName ?? "");
        }
        else
        {
            composer.AddStaticText(Lang.Get("(Only the owner can edit this)"), CairoFont.WhiteSmallText(), rightBounds.FlatCopy().WithFixedWidth(300));
        }

        // Board Owner
        leftBounds = leftBounds.BelowCopy(0, 10);
        rightBounds = rightBounds.BelowCopy(0, 10).WithFixedSize(inputWidth, 30);
        composer.AddStaticText("Board Owner:", CairoFont.WhiteSmallText(), leftBounds);
        if (isOwner)
        {
            composer.AddDropDown(
                players.Select(p => p.PlayerUID).ToArray(),
                players.Select(p => p.PlayerName).ToArray(),
                players.FindIndex(p => p.PlayerUID == boardPlayerId),
                (code, selected) => { this.pendingOwnerUid = code; UpdateDirtyState(); },
                rightBounds,
                "ownerDropdown"
            );
        }
        else
        {
            composer.AddStaticText(Lang.Get("(Only the owner can edit this)"), CairoFont.WhiteSmallText(), rightBounds.FlatCopy().WithFixedWidth(300));
        }

        // Board Font
        leftBounds = leftBounds.BelowCopy(0, 10);
        rightBounds = rightBounds.BelowCopy(0, 10).WithFixedSize(inputWidth, 30);
        composer.AddStaticText("Board Font:", CairoFont.WhiteSmallText(), leftBounds);
        if (isOwner)
        {
            if (fontValues.Length > 0)
            {
                int selectedFontIndex = Array.IndexOf(fontValues, this.boardFont ?? fontValues[0]);
                if (selectedFontIndex < 0) selectedFontIndex = 0;
                composer.AddDropDown(fontValues, fontFileNames, selectedFontIndex, (value, selected) => { this.boardFont = value; UpdateDirtyState(); }, rightBounds, "fontDropdown");
            }
            else
            {
                composer.AddStaticText("No fonts found.", CairoFont.WhiteSmallText(), rightBounds);
            }
        }
        else
        {
            composer.AddStaticText(Lang.Get("(Only the owner can edit this)"), CairoFont.WhiteSmallText(), rightBounds.FlatCopy().WithFixedWidth(300));
        }

        // Board Style
        leftBounds = leftBounds.BelowCopy(0, 10);
        rightBounds = rightBounds.BelowCopy(0, 10).WithFixedSize(inputWidth, 30);
        composer.AddStaticText("Board Style:", CairoFont.WhiteSmallText(), leftBounds);
        if (isOwner)
        {
            composer.AddDropDown(
                players.Select(p => p.PlayerUID).ToArray(),
                players.Select(p => p.PlayerName).ToArray(),
                players.FindIndex(p => p.PlayerUID == boardPlayerId),
                (code, selected) => { this.pendingOwnerUid = code; UpdateDirtyState(); },
                rightBounds,
                "styleDropdown"
            );
        }
        else
        {
            composer.AddStaticText(Lang.Get("(Only the owner can edit this)"), CairoFont.WhiteSmallText(), rightBounds.FlatCopy().WithFixedWidth(300));
        }


        // Lock Notice Board
        leftBounds = leftBounds.BelowCopy(0, 10);
        rightBounds = rightBounds.BelowCopy(0, 10).WithFixedSize(checkboxWidth, 30);
        composer.AddStaticText("Lock Notice Board:", CairoFont.WhiteSmallText(), leftBounds);
        if (isOwner)
        {
            composer.AddSwitch((state) => { this.isLocked = state; UpdateDirtyState(); }, rightBounds, "lockSwitch");
            composer.GetSwitch("lockSwitch").On = this.isLocked;
        }
        else
        {
            composer.AddStaticText(Lang.Get("(Only the owner can lock this board)"), CairoFont.WhiteSmallText(), rightBounds.FlatCopy().WithFixedWidth(300));
        }

        // Enable Parchment
        leftBounds = leftBounds.BelowCopy(0, 10);
        rightBounds = rightBounds.BelowCopy(0, 10).WithFixedSize(checkboxWidth, 30);
        composer.AddStaticText("Enable Parchment:", CairoFont.WhiteSmallText(), leftBounds);
        if (isOwner)
        {
            composer.AddSwitch((state) => { this.enableParchment = state; UpdateDirtyState(); }, rightBounds, "parchmentSwitch");
            composer.GetSwitch("parchmentSwitch").On = this.enableParchment;
        }
        else
        {
            composer.AddStaticText(Lang.Get("(Only the owner can edit this)"), CairoFont.WhiteSmallText(), rightBounds.FlatCopy().WithFixedWidth(300));
        }

        // Enable Particles
        leftBounds = leftBounds.BelowCopy(0, 10);
        rightBounds = rightBounds.BelowCopy(0, 10).WithFixedSize(checkboxWidth, 30);
        composer.AddStaticText("Enable Particles:", CairoFont.WhiteSmallText(), leftBounds);
        if (isOwner)
        {
            composer.AddSwitch((state) => { this.enableParticles = state; UpdateDirtyState(); }, rightBounds, "particlesSwitch");
            composer.GetSwitch("particlesSwitch").On = this.enableParticles;
        }
        else
        {
            composer.AddStaticText(Lang.Get("(Only the owner can edit this)"), CairoFont.WhiteSmallText(), rightBounds.FlatCopy().WithFixedWidth(300));
        }

        if (isProximityLoaded)
        {
            // Enable Proximity
            leftBounds = leftBounds.BelowCopy(0, 10);
            rightBounds = rightBounds.BelowCopy(0, 10).WithFixedSize(checkboxWidth, 30);
            composer.AddStaticText("Enable Proximity Annoucment:", CairoFont.WhiteSmallText(), leftBounds);
            if (isOwner)
            {
                composer.AddSwitch((state) => { this.enableProximityMessage = state; UpdateDirtyState(); }, rightBounds, "proximitySwitch");
                composer.GetSwitch("proximitySwitch").On = this.enableProximityMessage;
            }
            else
            {
                composer.AddStaticText(Lang.Get("(Only the owner can edit this)"), CairoFont.WhiteSmallText(), rightBounds.FlatCopy().WithFixedWidth(300));
            }

            // Proximity Channel Name
            leftBounds = leftBounds.BelowCopy(0, 10);
            rightBounds = rightBounds.BelowCopy(0, 10).WithFixedSize(inputWidth, 30);
            composer.AddStaticText("Proximity Channel Name:", CairoFont.WhiteSmallText(), leftBounds);
            if (isOwner)
            {
                composer.AddTextInput(rightBounds, (text) => { this.proximityChannel = text; UpdateDirtyState(); }, CairoFont.WhiteSmallText(), "channelInput");
                composer.GetTextInput("channelInput").SetValue(this.proximityChannel ?? "");
            }
            else
            {
                composer.AddStaticText(Lang.Get("(Only the owner can edit this)"), CairoFont.WhiteSmallText(), rightBounds.FlatCopy().WithFixedWidth(300));
            }

            // Proximity Distance
            leftBounds = leftBounds.BelowCopy(0, 10);
            rightBounds = rightBounds.BelowCopy(0, 10).WithFixedSize(sliderWidth, 30);
            this.pendingDistance = (int)Math.Max(1, this.proximityDistance);
            composer.AddDynamicText($"Proximity Distance: {this.pendingDistance}", CairoFont.WhiteSmallText(), leftBounds, "distanceLabel");
            if (isOwner)
            {
                composer.AddSlider(
                    (newValue) =>
                    {
                        this.pendingDistance = newValue;
                        composer.GetDynamicText("distanceLabel").SetNewText($"Proximity Distance: {newValue}");
                        UpdateDirtyState();
                        return true;
                    },
                    rightBounds,
                    "distanceSlider"
                );
                composer.GetSlider("distanceSlider").SetValues(this.pendingDistance, 1, 1000, 1);
            }
            else
            {
                composer.AddStaticText(Lang.Get("(Only the owner can edit this)"), CairoFont.WhiteSmallText(), rightBounds.FlatCopy().WithFixedWidth(300));
            }
        }

        // Save Button
        ElementBounds btnSaveAllBounds = leftBounds.BelowCopy(0, 20).WithFixedSize(160, 30);
        composer.AddSmallButton(Lang.Get("Save All Settings"), OnSaveAllSettingsClick, btnSaveAllBounds, EnumButtonStyle.Normal, "btnSaveAll");
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
            || (this.pendingDistance != p.ProximityDistance)
            || (!string.IsNullOrEmpty(this.pendingOwnerUid) && this.pendingOwnerUid != p.PlayerId);

        this.SingleComposer?.GetButton("btnSaveAll")?.Enabled = this.isDirty;
    }

    private bool OnSaveAllSettingsClick()
    {
        var p = this.noticeBoardPacket.BoardProperties;
        var channel = capi.Network.GetChannel("noticeboard");

        if (this.isLocked != (p.IsLocked != 0)) { channel.SendPacket(new EditIsLocked { BoardId = this.boardId, IsLocked = this.isLocked }); p.IsLocked = this.isLocked ? 1 : 0; }
        if (this.enableParticles != (p.EnableParticles != 0)) { channel.SendPacket(new EditEnableParticles { BoardId = this.boardId, EnableParticles = this.enableParticles }); p.EnableParticles = this.enableParticles ? 1 : 0; }
        if (this.enableParchment != (p.EnableParchment != 0)) { channel.SendPacket(new EditEnableParchment { BoardId = this.boardId, EnableParchment = this.enableParchment }); p.EnableParchment = this.enableParchment ? 1 : 0; }
        if (this.boardName != p.BoardName) { channel.SendPacket(new EditBoardName { BoardId = this.boardId, BoardName = this.boardName }); p.BoardName = this.boardName; }
        if (this.boardFont != p.BoardFont) { channel.SendPacket(new EditBoardFont { BoardId = this.boardId, BoardFont = this.boardFont }); p.BoardFont = this.boardFont; }
        if (this.enableProximityMessage != (p.EnableProximity != 0)) { channel.SendPacket(new EditEnableProximity { BoardId = this.boardId, EnableProximity = this.enableProximityMessage }); p.EnableProximity = this.enableProximityMessage ? 1 : 0; }
        if (this.proximityChannel != p.ProximityChannel) { channel.SendPacket(new EditProximityChannel { BoardId = this.boardId, ChannelName = this.proximityChannel ?? "" }); p.ProximityChannel = this.proximityChannel; }
        if (this.pendingDistance != p.ProximityDistance) { channel.SendPacket(new EditProximityDistance { BoardId = this.boardId, Distance = this.pendingDistance }); p.ProximityDistance = this.pendingDistance; }

        if (!string.IsNullOrEmpty(this.pendingOwnerUid) && this.pendingOwnerUid != p.PlayerId)
        {
            channel.SendPacket(new EditBoardOwner { BoardId = this.boardId, NewOwnerUid = this.pendingOwnerUid });
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
        this.pendingOwnerUid = p.PlayerId;
        this.isDirty = false;
    }
}