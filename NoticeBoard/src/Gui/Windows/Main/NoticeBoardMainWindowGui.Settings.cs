using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NoticeBoard.BlockType;
using NoticeBoard.Configs;
using NoticeBoard.Extensions;
using NoticeBoard.Packets;
using NoticeBoard.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace NoticeBoard.src.Gui.Windows;

public partial class NoticeBoardMainWindowGui
{
    private static bool IsTheBasicsLoaded(ICoreClientAPI capi)
    {
        if (capi.ModLoader.IsModEnabled("thebasics"))
            return true;

        foreach (var mod in capi.ModLoader.Mods)
        {
            if (string.Equals(mod.Info?.ModID, "thebasics", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static double MeasureLabelWidth(CairoFont font, double minWidth, double rowHeight, params string[] candidates)
    {
        double width = minWidth;
        foreach (string text in candidates)
        {
            ElementBounds probe = ElementBounds.Fixed(0, 0, minWidth, rowHeight);
            font.AutoBoxSize(text, probe, onlyGrow: true);
            width = Math.Max(width, probe.fixedWidth);
        }
        return width;
    }

    private void PopulateSettingsTab(GuiComposer composer, ElementBounds insetBounds)
    {
        string[] fontValues = FontManager.FontDisplayNames;
        string[] fontFileNames = FontManager.FontFileNames;

        const int rowHeight = 30;
        const int rowGap = 14;
        const int minLabelWidth = 195;
        const int labelControlGap = 10;
        const int columnGap = 20;
        const int leftPadding = 36;
        const int rightPadding = 18;
        const int minControlWidth = 120;
        const int saveButtonWidth = 160;
        const int sectionGap = 26;
        const int contentPadding = 18;

        bool isProximityLoaded = IsTheBasicsLoaded(capi);

        capi.Logger.Notification(
            $"[NoticeBoard] Proximity settings visible={isProximityLoaded} (thebasics check)"
        );

        CairoFont normalFont = CairoFont.WhiteSmallText();
        CairoFont sectionFont = CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold);

        string maxPapersLabelText =
            $"{Lang.Get("noticeboard:settings-board-max-papers")} {NoticeBoard.Rendering.NoticeBoardPaperLayout.MaxPapers}";
        string fontSizeLabelText = $"{Lang.Get("noticeboard:settings-board-font-size")} 60";
        string textSharpnessLabelText =
            $"{Lang.Get("noticeboard:settings-board-text-sharpness")} {NoticeBoard.Rendering.PaperSize.MaxTextSharpness}x";
        string swayStrengthLabelText =
            $"{Lang.Get("noticeboard:settings-board-sway-strength")} {NoticeBoard.Rendering.PaperSize.MaxSwayStrength}";
        string proximityDistanceLabelText =
            $"{Lang.Get("noticeboard:settings-board-proximity-distance")} 1000";

        double labelWidth = MeasureLabelWidth(
            normalFont,
            minLabelWidth,
            rowHeight,
            Lang.Get("noticeboard:settings-board-name"),
            Lang.Get("noticeboard:settings-board-owner"),
            Lang.Get("noticeboard:settings-board-permission-mode"),
            Lang.Get("noticeboard:settings-board-parchment"),
            Lang.Get("noticeboard:settings-board-font"),
            Lang.Get("noticeboard:settings-board-theme"),
            Lang.Get("noticeboard:settings-board-particles"),
            Lang.Get("noticeboard:settings-board-notice-aging"),
            Lang.Get("noticeboard:settings-board-notice-aging-days"),
            Lang.Get("noticeboard:settings-board-legacy-board"),
            Lang.Get("noticeboard:settings-board-discord"),
            Lang.Get("noticeboard:settings-board-discord-webhook"),
            Lang.Get("noticeboard:settings-board-proximity"),
            Lang.Get("noticeboard:settings-board-proximity-channel"),
            maxPapersLabelText,
            fontSizeLabelText,
            textSharpnessLabelText,
            swayStrengthLabelText,
            proximityDistanceLabelText
        );
        labelWidth = Math.Max(
            labelWidth,
            MeasureLabelWidth(
                sectionFont,
                minLabelWidth,
                rowHeight,
                Lang.Get("noticeboard:settings-section-board"),
                Lang.Get("noticeboard:settings-section-appearance"),
                Lang.Get("noticeboard:settings-section-proximity")
            )
        );

        double controlWidth = Math.Max(
            minControlWidth,
            (insetBounds.fixedWidth - leftPadding - rightPadding - columnGap) / 2
                - labelWidth
                - labelControlGap
        );
        double controlOffsetX = labelWidth + labelControlGap;
        double columnOffsetX = controlOffsetX + controlWidth + columnGap;

        double contentX = insetBounds.fixedX + leftPadding;
        double contentY = insetBounds.fixedY + contentPadding;
        double rightX = contentX + columnOffsetX;

        double sectionHeaderY = contentY + 20;
        composer.AddStaticText(
            Lang.Get("noticeboard:settings-section-board"),
            sectionFont,
            ElementBounds.Fixed(contentX, sectionHeaderY, labelWidth, rowHeight)
        );
        composer.AddStaticText(
            Lang.Get("noticeboard:settings-section-appearance"),
            sectionFont,
            ElementBounds.Fixed(rightX, sectionHeaderY, labelWidth, rowHeight)
        );

        double firstRowY = sectionHeaderY + rowHeight + rowGap;

        ElementBounds leftLabel = ElementBounds.Fixed(contentX, firstRowY, labelWidth, rowHeight);
        ElementBounds leftControl = ElementBounds.Fixed(
            contentX + controlOffsetX,
            firstRowY,
            controlWidth,
            rowHeight
        );
        ElementBounds rightLabel = ElementBounds.Fixed(rightX, firstRowY, labelWidth, rowHeight);
        ElementBounds rightControl = ElementBounds.Fixed(
            rightX + controlOffsetX,
            firstRowY,
            controlWidth,
            rowHeight
        );

        composer.AddStaticText(
            Lang.Get("noticeboard:settings-board-name"),
            CairoFont.WhiteSmallText(),
            leftLabel
        );

        composer.AddTextInput(
            leftControl,
            (text) =>
            {
                this.boardName = text;
                UpdateDirtyState();
            },
            CairoFont.WhiteSmallText(),
            "boardNameInput"
        );
        composer.GetTextInput("boardNameInput").SetValue(this.boardName ?? "");

        leftLabel = leftLabel.BelowCopy(0, rowGap);
        leftControl = leftControl.BelowCopy(0, rowGap);
        composer.AddStaticText(
            Lang.Get("noticeboard:settings-board-owner"),
            CairoFont.WhiteSmallText(),
            leftLabel
        );

        string[] playerUids = this.players?.Select(p => p.PlayerUID).ToArray() ?? Array.Empty<string>();
        string[] playerNames = this.players?.Select(p => p.PlayerName).ToArray() ?? Array.Empty<string>();
        int selectedOwnerIndex = this.players?.FindIndex(p => p.PlayerUID == boardPlayerId) ?? -1;

        composer.AddDropDown(
            playerUids,
            playerNames,
            selectedOwnerIndex,
            (code, selected) =>
            {
                this.pendingOwnerUid = code;
                UpdateDirtyState();
            },
            leftControl,
            "ownerDropdown"
        );

        leftLabel = leftLabel.BelowCopy(0, rowGap);
        leftControl = leftControl.BelowCopy(0, rowGap);
        composer.AddStaticText(
            Lang.Get("noticeboard:settings-board-permission-mode"),
            CairoFont.WhiteSmallText(),
            leftLabel
        );

        string[] permissionModeCodes = ["0", "1", "2"];
        string[] permissionModeNames =
        [
            Lang.Get("noticeboard:settings-board-permission-mode-default"),
            Lang.Get("noticeboard:settings-board-permission-mode-all"),
            Lang.Get("noticeboard:settings-board-permission-mode-locked"),
        ];
        int selectedPermissionModeIndex = Math.Clamp(this.permissionMode, 0, permissionModeCodes.Length - 1);

        composer.AddDropDown(
            permissionModeCodes,
            permissionModeNames,
            selectedPermissionModeIndex,
            (code, selected) =>
            {
                if (int.TryParse(code, out int parsedMode))
                {
                    this.permissionMode = parsedMode;
                    UpdateDirtyState();
                }
            },
            leftControl,
            "permissionModeDropdown"
        );

        // Enable Parchment. This gates the parchment cost slots rather than anything visual, so
        // it belongs with the board rules and not with Appearance.
        leftLabel = leftLabel.BelowCopy(0, rowGap);
        leftControl = leftControl.BelowCopy(0, rowGap);
        composer.AddStaticText(
            Lang.Get("noticeboard:settings-board-parchment"),
            CairoFont.WhiteSmallText(),
            leftLabel
        );

        composer.AddSwitch(
            (state) =>
            {
                this.enableParchment = state;
                UpdateDirtyState();
            },
            leftControl,
            "parchmentSwitch"
        );
        composer.GetSwitch("parchmentSwitch").On = this.enableParchment;

        leftLabel = leftLabel.BelowCopy(0, rowGap);
        leftControl = leftControl.BelowCopy(0, rowGap);
        composer.AddStaticText(
            Lang.Get("noticeboard:settings-board-manual-pin"),
            CairoFont.WhiteSmallText(),
            leftLabel
        );

        composer.AddSwitch(
            (state) =>
            {
                this.enableManualPin = state;
                UpdateDirtyState();
            },
            leftControl,
            "manualPinSwitch"
        );
        composer.GetSwitch("manualPinSwitch").On = this.enableManualPin;

        leftLabel = leftLabel.BelowCopy(0, rowGap);
        leftControl = leftControl.BelowCopy(0, rowGap);
        composer.AddStaticText(
            Lang.Get("noticeboard:settings-board-discord"),
            CairoFont.WhiteSmallText(),
            leftLabel
        );

        composer.AddSwitch(
            (state) =>
            {
                this.enableDiscord = state;
                UpdateDirtyState();
            },
            leftControl,
            "discordSwitch"
        );
        composer.GetSwitch("discordSwitch").On = this.enableDiscord;

        leftLabel = leftLabel.BelowCopy(0, rowGap);
        leftControl = leftControl.BelowCopy(0, rowGap).WithFixedWidth(controlWidth);
        this.pendingMaxPapers = NoticeBoard.Rendering.NoticeBoardPaperLayout.ClampMax(
            this.pendingMaxPapers
        );

        composer.AddDynamicText(
            MaxPapersLabel(this.pendingMaxPapers),
            CairoFont.WhiteSmallText(),
            leftLabel,
            "maxPapersLabel"
        );

        composer.AddSlider(
            (newValue) =>
            {
                this.pendingMaxPapers = newValue;
                composer
                    .GetDynamicText("maxPapersLabel")
                    .SetNewText(MaxPapersLabel(newValue));
                UpdateDirtyState();
                return true;
            },
            leftControl,
            "maxPapersSlider"
        );
        composer
            .GetSlider("maxPapersSlider")
            .SetValues(
                this.pendingMaxPapers,
                NoticeBoard.Rendering.NoticeBoardPaperLayout.MinPapers,
                NoticeBoard.Rendering.NoticeBoardPaperLayout.MaxPapers,
                1
            );

        // Sway Strength, in the Board column: a strength of 0 leaves the papers still, so this
        // slider alone controls whether they sway at all.
        leftLabel = leftLabel.BelowCopy(0, rowGap);
        leftControl = leftControl.BelowCopy(0, rowGap).WithFixedWidth(controlWidth);
        this.pendingSwayStrength = NoticeBoard.Rendering.PaperSize.ClampSwayStrength(
            this.pendingSwayStrength
        );

        composer.AddDynamicText(
            $"{Lang.Get("noticeboard:settings-board-sway-strength")} {this.pendingSwayStrength}",
            CairoFont.WhiteSmallText(),
            leftLabel,
            "swayStrengthLabel"
        );

        composer.AddSlider(
            (newValue) =>
            {
                this.pendingSwayStrength = newValue;
                composer
                    .GetDynamicText("swayStrengthLabel")
                    .SetNewText(
                        $"{Lang.Get("noticeboard:settings-board-sway-strength")} {newValue}"
                    );
                UpdateDirtyState();
                return true;
            },
            leftControl,
            "swayStrengthSlider"
        );
        composer
            .GetSlider("swayStrengthSlider")
            .SetValues(
                this.pendingSwayStrength,
                NoticeBoard.Rendering.PaperSize.MinSwayStrength,
                NoticeBoard.Rendering.PaperSize.MaxSwayStrength,
                5
            );

        composer.AddStaticText(
            Lang.Get("noticeboard:settings-board-font"),
            CairoFont.WhiteSmallText(),
            rightLabel
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
                rightControl,
                "fontDropdown"
            );
        }
        else
        {
            composer.AddStaticText("No fonts found.", CairoFont.WhiteSmallText(), rightControl);
        }

        rightLabel = rightLabel.BelowCopy(0, rowGap);
        rightControl = rightControl.BelowCopy(0, rowGap);

        this.pendingFontSize = Math.Max(10, this.boardFontSize);

        composer.AddDynamicText(
            $"{Lang.Get("noticeboard:settings-board-font-size")} {this.pendingFontSize}",
            CairoFont.WhiteSmallText(),
            rightLabel,
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
            rightControl,
            "fontSizeSlider"
        );
        composer.GetSlider("fontSizeSlider").SetValues((int)this.pendingFontSize, 10, 60, 1);

        rightLabel = rightLabel.BelowCopy(0, rowGap);
        rightControl = rightControl.BelowCopy(0, rowGap);
        composer.AddStaticText(
            Lang.Get("noticeboard:settings-board-theme"),
            CairoFont.WhiteSmallText(),
            rightLabel
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
            rightControl,
            "themeDropdown"
        );

        rightLabel = rightLabel.BelowCopy(0, rowGap);
        rightControl = rightControl.BelowCopy(0, rowGap);
        composer.AddStaticText(
            Lang.Get("noticeboard:settings-board-particles"),
            CairoFont.WhiteSmallText(),
            rightLabel
        );

        composer.AddSwitch(
            (state) =>
            {
                this.enableParticles = state;
                UpdateDirtyState();
            },
            rightControl,
            "particlesSwitch"
        );
        composer.GetSwitch("particlesSwitch").On = this.enableParticles;

        rightLabel = rightLabel.BelowCopy(0, rowGap);
        rightControl = rightControl.BelowCopy(0, rowGap);
        composer.AddStaticText(
            Lang.Get("noticeboard:settings-board-notice-aging"),
            CairoFont.WhiteSmallText(),
            rightLabel
        );

        composer.AddSwitch(
            (state) =>
            {
                ReadPendingAgingDays(composer);
                this.enableNoticeAging = state;
                UpdateDirtyState();
                this.RefreshMessageList();
            },
            rightControl,
            "agingSwitch"
        );
        composer.GetSwitch("agingSwitch").On = this.enableNoticeAging;

        if (this.enableNoticeAging)
        {
            rightLabel = rightLabel.BelowCopy(0, rowGap);
            rightControl = rightControl.BelowCopy(0, rowGap).WithFixedWidth(controlWidth);
            composer.AddStaticText(
                Lang.Get("noticeboard:settings-board-notice-aging-days"),
                CairoFont.WhiteSmallText(),
                rightLabel
            );
            composer.AddNumberInput(
                rightControl,
                _ =>
                {
                    ReadPendingAgingDays(composer);
                    UpdateDirtyState();
                },
                CairoFont.WhiteSmallText(),
                "agingDays");
            var agingDays = composer.GetNumberInput("agingDays");
            agingDays.IntMode = true;
            agingDays.SetValue(this.pendingNoticeAgingDays.ToString(CultureInfo.InvariantCulture));
        }

        rightLabel = rightLabel.BelowCopy(0, rowGap);
        rightControl = rightControl.BelowCopy(0, rowGap);
        composer.AddStaticText(
            Lang.Get("noticeboard:settings-board-legacy-board"),
            CairoFont.WhiteSmallText(),
            rightLabel
        );

        composer.AddSwitch(
            (state) =>
            {
                this.enableLegacyBoard = state;
                UpdateDirtyState();
            },
            rightControl,
            "legacyBoardSwitch"
        );
        composer.GetSwitch("legacyBoardSwitch").On = this.enableLegacyBoard;

        rightLabel = rightLabel.BelowCopy(0, rowGap);
        rightControl = rightControl.BelowCopy(0, rowGap).WithFixedWidth(controlWidth);
        this.pendingTextSharpness = NoticeBoard.Rendering.PaperSize.ClampSharpness(
            this.pendingTextSharpness
        );

        composer.AddDynamicText(
            TextSharpnessLabel(this.pendingTextSharpness),
            CairoFont.WhiteSmallText(),
            rightLabel,
            "textSharpnessLabel"
        );

        composer.AddSlider(
            (newValue) =>
            {
                this.pendingTextSharpness = newValue;
                composer
                    .GetDynamicText("textSharpnessLabel")
                    .SetNewText(TextSharpnessLabel(newValue));
                UpdateDirtyState();
                return true;
            },
            rightControl,
            "textSharpnessSlider"
        );
        composer
            .GetSlider("textSharpnessSlider")
            .SetValues(
                this.pendingTextSharpness,
                NoticeBoard.Rendering.PaperSize.MinTextSharpness,
                NoticeBoard.Rendering.PaperSize.MaxTextSharpness,
                1
            );

        double webhookY = Math.Max(leftLabel.fixedY, rightLabel.fixedY) + rowHeight + rowGap;
        leftLabel = ElementBounds.Fixed(contentX, webhookY, labelWidth, rowHeight);
        leftControl = ElementBounds.Fixed(
            contentX + controlOffsetX,
            webhookY,
            columnOffsetX + controlWidth,
            rowHeight
        );
        composer.AddStaticText(
            Lang.Get("noticeboard:settings-board-discord-webhook"),
            CairoFont.WhiteSmallText(),
            leftLabel
        );
        composer.AddTextInput(
            leftControl,
            (text) =>
            {
                this.pendingDiscordWebhook = text ?? "";
                if (!string.IsNullOrWhiteSpace(this.pendingDiscordWebhook))
                    this.pendingClearDiscordWebhook = false;
                UpdateDirtyState();
            },
            CairoFont.WhiteSmallText(),
            "discordWebhookInput"
        );
        composer.GetTextInput("discordWebhookInput").SetValue("");

        double discordStatusY = webhookY + rowHeight + rowGap;
        leftLabel = ElementBounds.Fixed(contentX, discordStatusY, labelWidth, rowHeight);
        leftControl = ElementBounds.Fixed(
            contentX + controlOffsetX,
            discordStatusY,
            controlWidth,
            rowHeight
        );
        rightLabel = ElementBounds.Fixed(rightX, discordStatusY, labelWidth, rowHeight);
        rightControl = ElementBounds.Fixed(
            rightX + controlOffsetX,
            discordStatusY,
            controlWidth,
            rowHeight
        );
        composer.AddStaticText(
            this.noticeBoardPacket.BoardProperties.HasDiscordWebhook != 0
                ? Lang.Get("noticeboard:settings-board-discord-set")
                : Lang.Get("noticeboard:settings-board-discord-missing"),
            CairoFont.WhiteSmallText(),
            ElementBounds.Fixed(contentX, discordStatusY, columnOffsetX - columnGap, rowHeight)
        );
        composer.AddSmallButton(
            Lang.Get("noticeboard:settings-board-discord-clear"),
            () =>
            {
                this.pendingClearDiscordWebhook = true;
                this.pendingDiscordWebhook = "";
                composer.GetTextInput("discordWebhookInput")?.SetValue("");
                UpdateDirtyState();
                return true;
            },
            rightControl,
            EnumButtonStyle.Normal,
            "discordWebhookClear"
        );

        if (isProximityLoaded)
        {
            // Which column ends lower depends on which settings are present, so clear both.
            double proximityHeaderY =
                Math.Max(leftLabel.fixedY, rightLabel.fixedY) + rowHeight + sectionGap;
            composer.AddStaticText(
                Lang.Get("noticeboard:settings-section-proximity"),
                sectionFont,
                ElementBounds.Fixed(contentX, proximityHeaderY, labelWidth, rowHeight)
            );

            double proximityRowY = proximityHeaderY + rowHeight + rowGap;
            leftLabel = ElementBounds.Fixed(contentX, proximityRowY, labelWidth, rowHeight);
            leftControl = ElementBounds.Fixed(
                contentX + controlOffsetX,
                proximityRowY,
                controlWidth,
                rowHeight
            );
            rightLabel = ElementBounds.Fixed(rightX, proximityRowY, labelWidth, rowHeight);
            rightControl = ElementBounds.Fixed(
                rightX + controlOffsetX,
                proximityRowY,
                controlWidth,
                rowHeight
            );

            composer.AddStaticText(
                Lang.Get("noticeboard:settings-board-proximity"),
                CairoFont.WhiteSmallText(),
                leftLabel
            );

            composer.AddSwitch(
                (state) =>
                {
                    this.enableProximityMessage = state;
                    UpdateDirtyState();
                },
                leftControl,
                "proximitySwitch"
            );
            composer.GetSwitch("proximitySwitch").On = this.enableProximityMessage;

            leftLabel = leftLabel.BelowCopy(0, rowGap);
            leftControl = leftControl.BelowCopy(0, rowGap).WithFixedWidth(controlWidth);
            composer.AddStaticText(
                Lang.Get("noticeboard:settings-board-proximity-channel"),
                CairoFont.WhiteSmallText(),
                leftLabel
            );

            composer.AddTextInput(
                leftControl,
                (text) =>
                {
                    this.proximityChannel = text;
                    UpdateDirtyState();
                },
                CairoFont.WhiteSmallText(),
                "channelInput"
            );
            composer.GetTextInput("channelInput").SetValue(this.proximityChannel ?? "");

            this.pendingDistance = (int)Math.Max(1, this.proximityDistance);
            composer.AddDynamicText(
                $"{Lang.Get("noticeboard:settings-board-proximity-distance")} {this.pendingDistance}",
                CairoFont.WhiteSmallText(),
                rightLabel,
                "distanceLabel"
            );

            composer.AddSlider(
                (newValue) =>
                {
                    this.pendingDistance = newValue;
                    composer
                        .GetDynamicText("distanceLabel")
                        .SetNewText(
                            $"{Lang.Get("noticeboard:settings-board-proximity-distance")} {newValue}"
                        );
                    UpdateDirtyState();
                    return true;
                },
                rightControl,
                "distanceSlider"
            );
            composer.GetSlider("distanceSlider").SetValues(this.pendingDistance, 1, 1000, 1);
        }

        // The two columns end at different heights, and which one is taller depends on whether
        // the proximity section is present, so the button clears both.
        double saveY = Math.Max(leftLabel.fixedY, rightLabel.fixedY) + rowHeight + sectionGap;

        // Only grow the inset's height to fit what this compose actually measured - never
        // shrink below what the caller passed in. Width stays fixed to match the Messages tab:
        // the column layout above is sized to fit it rather than the other way around.
        insetBounds.fixedHeight = Math.Max(
            insetBounds.fixedHeight,
            saveY - insetBounds.fixedY + rowHeight + contentPadding
        );

        ElementBounds btnSaveAllBounds = ElementBounds.Fixed(
            insetBounds.fixedX + (insetBounds.fixedWidth - saveButtonWidth) / 2,
            saveY,
            saveButtonWidth,
            rowHeight
        );
        composer.AddSmallButton(
            Lang.Get("noticeboard:settings-board-save"),
            OnSaveAllSettingsClick,
            btnSaveAllBounds,
            EnumButtonStyle.Normal,
            "btnSaveAll"
        );
        composer.GetButton("btnSaveAll").Enabled = this.isDirty;
    }

    private string MaxPapersLabel(int value)
    {
        string label = $"{Lang.Get("noticeboard:settings-board-max-papers")} {value}";
        return label;
    }

    // The requested sharpness, plus what the renderer will really manage if a crowded board
    // forces it down, so that a stepped-down slider does not read as a dead control.
    private string TextSharpnessLabel(int requested)
    {
        string label = $"{Lang.Get("noticeboard:settings-board-text-sharpness")} {requested}x";

        NoticeBoardBlockEntity blockEntity = this.boardPos != null
            ? capi.GetNoticeBoardEntity(this.boardPos)
            : null;
        int effective = blockEntity?.PredictTextSuperSample(requested) ?? requested;

        return effective == requested
            ? label
            : $"{label} {Lang.Get("noticeboard:settings-board-text-sharpness-effective", effective)}";
    }

    private void UpdateDirtyState()
    {
        var p = this.noticeBoardPacket.BoardProperties;

        this.isDirty =
            (this.permissionMode != p.PermissionMode)
            || (this.enableParticles != (p.EnableParticles != 0))
            || (this.enableNoticeAging != (p.EnableNoticeAging != 0))
            || (this.pendingNoticeAgingDays != GameDateFormatter.ClampLifeDays(p.NoticeAgingDays))
            || (this.enableParchment != (p.EnableParchment != 0))
            || (this.enableManualPin != (p.EnableManualPin != 0))
            || (this.enableDiscord != (p.EnableDiscord != 0))
            || !string.IsNullOrWhiteSpace(this.pendingDiscordWebhook)
            || this.pendingClearDiscordWebhook
            || (this.enableLegacyBoard != (p.EnableLegacyBoard != 0))
            || (this.enableProximityMessage != (p.EnableProximity != 0))
            || (this.proximityChannel != p.ProximityChannel)
            || (this.boardName != p.BoardName)
            || (this.boardFont != p.BoardFont)
            || (this.pendingFontSize != p.BoardFontSize)
            || (this.pendingMaxPapers != NoticeBoard.Rendering.NoticeBoardPaperLayout.ClampMax(p.MaxPapersOnBoard))
            || (this.pendingTextSharpness != NoticeBoard.Rendering.PaperSize.ResolveSharpness(p.TextSharpness))
            || (this.pendingSwayStrength != NoticeBoard.Rendering.PaperSize.ClampSwayStrength(p.SwayStrength))
            || (this.boardTheme != p.BoardTheme)
            || (this.pendingDistance != p.ProximityDistance)
            || (!string.IsNullOrEmpty(this.pendingOwnerUid) && this.pendingOwnerUid != p.PlayerId);

        this.SingleComposer?.GetButton("btnSaveAll")?.Enabled = this.isDirty;
    }

    private bool OnSaveAllSettingsClick()
    {
        var p = this.noticeBoardPacket.BoardProperties;
        var channel = capi.Network.GetChannel("noticeboard");

        ReadPendingAgingDays(this.SingleComposer);

        if (this.permissionMode != p.PermissionMode)
        {
            channel.SendPacket(
                new EditPermissionMode
                {
                    BoardId = this.boardId,
                    PermissionMode = this.permissionMode,
                }
            );
            p.PermissionMode = this.permissionMode;
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
        if (this.enableNoticeAging != (p.EnableNoticeAging != 0))
        {
            channel.SendPacket(
                new EditEnableNoticeAging
                {
                    BoardId = this.boardId,
                    EnableNoticeAging = this.enableNoticeAging,
                }
            );
            p.EnableNoticeAging = this.enableNoticeAging ? 1 : 0;
        }
        if (this.pendingNoticeAgingDays != GameDateFormatter.ClampLifeDays(p.NoticeAgingDays))
        {
            int days = GameDateFormatter.ClampLifeDays(this.pendingNoticeAgingDays);
            channel.SendPacket(
                new EditNoticeAgingDays
                {
                    BoardId = this.boardId,
                    NoticeAgingDays = days,
                }
            );
            p.NoticeAgingDays = days;
            this.pendingNoticeAgingDays = days;
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
        if (this.enableManualPin != (p.EnableManualPin != 0))
        {
            channel.SendPacket(
                new EditEnableManualPin
                {
                    BoardId = this.boardId,
                    EnableManualPin = this.enableManualPin,
                }
            );
            p.EnableManualPin = this.enableManualPin ? 1 : 0;
        }
        if (this.enableDiscord != (p.EnableDiscord != 0))
        {
            channel.SendPacket(
                new EditEnableDiscord
                {
                    BoardId = this.boardId,
                    EnableDiscord = this.enableDiscord,
                }
            );
            p.EnableDiscord = this.enableDiscord ? 1 : 0;
        }
        if (!string.IsNullOrWhiteSpace(this.pendingDiscordWebhook))
        {
            string url = this.pendingDiscordWebhook.Trim();
            if (DiscordNoticeBridge.IsWebhookUrl(url))
            {
                channel.SendPacket(
                    new EditDiscordWebhook
                    {
                        BoardId = this.boardId,
                        WebhookUrl = url,
                    }
                );
                p.HasDiscordWebhook = 1;
            }
        }
        else if (this.pendingClearDiscordWebhook)
        {
            channel.SendPacket(
                new EditDiscordWebhook { BoardId = this.boardId, WebhookUrl = "" }
            );
            p.HasDiscordWebhook = 0;
        }
        this.pendingDiscordWebhook = "";
        this.pendingClearDiscordWebhook = false;
        if (this.enableLegacyBoard != (p.EnableLegacyBoard != 0))
        {
            channel.SendPacket(
                new EditEnableLegacyBoard
                {
                    BoardId = this.boardId,
                    EnableLegacyBoard = this.enableLegacyBoard,
                }
            );
            p.EnableLegacyBoard = this.enableLegacyBoard ? 1 : 0;
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
            this.boardFontSize = this.pendingFontSize;
        }
        if (this.pendingMaxPapers != NoticeBoard.Rendering.NoticeBoardPaperLayout.ClampMax(p.MaxPapersOnBoard))
        {
            int maxPapers = NoticeBoard.Rendering.NoticeBoardPaperLayout.ClampMax(
                this.pendingMaxPapers
            );
            channel.SendPacket(
                new EditMaxPapersOnBoard
                {
                    BoardId = this.boardId,
                    MaxPapersOnBoard = maxPapers,
                }
            );
            p.MaxPapersOnBoard = maxPapers;
        }
        if (this.pendingTextSharpness != NoticeBoard.Rendering.PaperSize.ResolveSharpness(p.TextSharpness))
        {
            int textSharpness = NoticeBoard.Rendering.PaperSize.ClampSharpness(
                this.pendingTextSharpness
            );
            channel.SendPacket(
                new EditBoardTextSharpness
                {
                    BoardId = this.boardId,
                    TextSharpness = textSharpness,
                }
            );
            p.TextSharpness = textSharpness;
        }
        if (this.pendingSwayStrength != NoticeBoard.Rendering.PaperSize.ClampSwayStrength(p.SwayStrength))
        {
            int swayStrength = NoticeBoard.Rendering.PaperSize.ClampSwayStrength(
                this.pendingSwayStrength
            );
            channel.SendPacket(
                new EditBoardSwayStrength
                {
                    BoardId = this.boardId,
                    SwayStrength = swayStrength,
                }
            );
            p.SwayStrength = swayStrength;
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

    private void ReadPendingAgingDays(GuiComposer composer)
    {
        var input = composer.GetNumberInput("agingDays");
        if (input == null)
            return;
        if (int.TryParse(input.GetText(), CultureInfo.InvariantCulture, out int days))
            this.pendingNoticeAgingDays = GameDateFormatter.ClampLifeDays(days);
    }

    private void ResetSettings()
    {
        var p = this.noticeBoardPacket.BoardProperties;
        this.permissionMode = p.PermissionMode;
        this.enableParticles = p.EnableParticles != 0;
        this.enableNoticeAging = p.EnableNoticeAging != 0;
        this.pendingNoticeAgingDays = GameDateFormatter.ClampLifeDays(p.NoticeAgingDays);
        this.enableParchment = p.EnableParchment != 0;
        this.enableManualPin = p.EnableManualPin != 0;
        this.enableDiscord = p.EnableDiscord != 0;
        this.pendingDiscordWebhook = "";
        this.pendingClearDiscordWebhook = false;
        this.enableLegacyBoard = p.EnableLegacyBoard != 0;
        this.enableProximityMessage = p.EnableProximity != 0;
        this.proximityChannel = p.ProximityChannel;
        this.pendingDistance = (int)p.ProximityDistance;
        this.pendingFontSize = p.BoardFontSize;
        this.pendingMaxPapers = NoticeBoard.Rendering.NoticeBoardPaperLayout.ClampMax(p.MaxPapersOnBoard);
        this.pendingTextSharpness = NoticeBoard.Rendering.PaperSize.ResolveSharpness(p.TextSharpness);
        this.pendingSwayStrength = NoticeBoard.Rendering.PaperSize.ClampSwayStrength(p.SwayStrength);
        this.pendingOwnerUid = p.PlayerId;
        this.isDirty = false;
    }
}
