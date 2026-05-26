using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace NoticeBoard.src.Gui.Windows
{
    public class GuiElementInkButton : GuiElementControl
    {
        private Action onClick;
        private string actionType; // "delete", "edit", or "bump"
        private ParchmentPalette parchmentPalette;
        private LoadedTexture normalTexture;
        private LoadedTexture hoverTexture;
        private bool isHovered;

        public GuiElementInkButton(
            ICoreClientAPI capi,
            string actionType,
            Action onClick,
            ElementBounds bounds,
            ParchmentPalette parchmentPalette
        )
            : base(capi, bounds)
        {
            this.actionType = actionType;
            this.onClick = onClick;
            this.parchmentPalette = parchmentPalette;
            this.normalTexture = new LoadedTexture(capi);
            this.hoverTexture = new LoadedTexture(capi);
        }

        public override void ComposeElements(Context ctx, ImageSurface surface)
        {
            Bounds.CalcWorldBounds();

            ImageSurface normalSurface = new ImageSurface(
                Format.Argb32,
                (int)Bounds.OuterWidth,
                (int)Bounds.OuterHeight
            );
            using (Context normalCtx = new Context(normalSurface))
            {
                DrawCustomIcon(normalCtx, this.actionType, this.parchmentPalette.InkColor);
            }
            this.api.Gui.LoadOrUpdateCairoTexture(normalSurface, false, ref this.normalTexture);
            normalSurface.Dispose();

            ImageSurface hoverSurface = new ImageSurface(
                Format.Argb32,
                (int)Bounds.OuterWidth,
                (int)Bounds.OuterHeight
            );
            using (Context hoverCtx = new Context(hoverSurface))
            {
                double[] hoverColor = new double[]
                {
                    Math.Max(0, this.parchmentPalette.InkColor[0] - 0.15),
                    Math.Max(0, this.parchmentPalette.InkColor[1] - 0.15),
                    Math.Max(0, this.parchmentPalette.InkColor[2] - 0.15),
                    1.0,
                };
                DrawCustomIcon(hoverCtx, this.actionType, hoverColor);
            }
            this.api.Gui.LoadOrUpdateCairoTexture(hoverSurface, false, ref this.hoverTexture);
            hoverSurface.Dispose();
        }

        private void DrawCustomIcon(Context ctx, string action, double[] color)
        {
            ctx.Antialias = Antialias.None;
            ctx.SetSourceRGBA(color[0], color[1], color[2], color[3]);

            string[] pixelGrid = null;

            if (action == "delete")
            {
                pixelGrid = new string[]
                {
                    "1110000000000111",
                    "0111000000001110",
                    "0011100000011100",
                    "0001110000111000",
                    "0000111001110000",
                    "0000011111100000",
                    "0000001111000000",
                    "0000000110000000",
                    "0000001111000000",
                    "0000011111100000",
                    "0000111001110000",
                    "0001110000111000",
                    "0011100000011100",
                    "0111000000001110",
                    "1110000000000111",
                    "0000000000000000",
                };
            }
            else if (action == "bump")
            {
                pixelGrid = new string[]
                {
                    "0000000110000000",
                    "0000001111000000",
                    "0000011111100000",
                    "0000110110110000",
                    "0001100110011000",
                    "0011000110001100",
                    "0110000110000110",
                    "0000000110000000",
                    "0000000110000000",
                    "0000000110000000",
                    "0000000110000000",
                    "0000000110000000",
                    "0000000110000000",
                    "0000000110000000",
                    "0000000110000000",
                    "0000000000000000",
                };
            }
            else if (action == "edit")
            {
                pixelGrid = new string[]
                {
                    "0000000000001110",
                    "0000000000011111",
                    "0000000000111110",
                    "0000000001111100",
                    "0000000011110000",
                    "0000000111100000",
                    "0000001111000000",
                    "0000011110000000",
                    "0000111100000000",
                    "0001111000000000",
                    "0011100000000000",
                    "0011000000000000",
                    "0000000000000000",
                    "0000000000000000",
                    "1111111111111100",
                    "1111111111111111",
                };
            }
            else if (action == "take")
            {
                pixelGrid = new string[]
                {
                    "0000000110000000",
                    "0000000110000000",
                    "0000000110000000",
                    "0000000110000000",
                    "0000000110000000",
                    "0000000110000000",
                    "0110000110000110",
                    "0011000110001100",
                    "0001100110011000",
                    "0000110110110000",
                    "0000011111100000",
                    "0000001111000000",
                    "0000000110000000",
                    "0000000000000000",
                    "1111111111111111",
                    "1111111111111111",
                };
            }

            if (pixelGrid == null)
                return;

            double w = Bounds.OuterWidth;
            double h = Bounds.OuterHeight;

            // Adjusted divisor to account for the larger 16x16 grid while keeping a nice margin
            double pixelSize = Math.Min(w, h) / 24.0;

            // Shifted from 12 to 16 for centering
            double startX = (w - (16 * pixelSize)) / 2.0;
            double startY = (h - (16 * pixelSize)) / 2.0;

            // Updated loops to 16x16
            for (int row = 0; row < 16; row++)
            {
                for (int col = 0; col < 16; col++)
                {
                    if (pixelGrid[row][col] == '1')
                    {
                        ctx.Rectangle(
                            startX + (col * pixelSize),
                            startY + (row * pixelSize),
                            pixelSize + 0.5,
                            pixelSize + 0.5
                        );
                    }
                }
            }

            ctx.Fill();
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            LoadedTexture texToDraw = isHovered ? hoverTexture : normalTexture;
            if (texToDraw != null && texToDraw.TextureId != 0)
            {
                this.api.Render.Render2DTexturePremultipliedAlpha(
                    texToDraw.TextureId,
                    Bounds.renderX,
                    Bounds.renderY,
                    Bounds.OuterWidth,
                    Bounds.OuterHeight
                );
            }
        }

        public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
        {
            base.OnMouseMove(api, args);
            isHovered = Bounds.PointInside(args.X, args.Y);
        }

        public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
        {
            base.OnMouseDownOnElement(api, args);
            if (Bounds.PointInside(args.X, args.Y) && args.Button == EnumMouseButton.Left)
            {
                api.Gui.PlaySound("ticking");
                onClick?.Invoke();
                args.Handled = true;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            normalTexture?.Dispose();
            hoverTexture?.Dispose();
        }
    }
}
