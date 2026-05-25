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
                    "110000000011",
                    "011000000110",
                    "001100001100",
                    "000110011000",
                    "000011110000",
                    "000001100000",
                    "000001100000",
                    "000011110000",
                    "000110011000",
                    "001100001100",
                    "011000000110",
                    "110000000011",
                };
            }
            else if (action == "bump")
            {
                pixelGrid = new string[]
                {
                    "000001100000",
                    "000011110000",
                    "000110011000",
                    "001100001100",
                    "011000000110",
                    "110000000011",
                    "000001100000",
                    "000001100000",
                    "000001100000",
                    "000001100000",
                    "000001100000",
                    "000001100000",
                };
            }
            else if (action == "edit")
            {
                pixelGrid = new string[]
                {
                    "000000000000", 
                    "000000000111", 
                    "000000000101",
                    "000000001010", 
                    "000000010100",
                    "000000101000",
                    "000001010000",
                    "000010100000",
                    "000101000000",
                    "001010000000",
                    "001000000000",
                    "111111111111",
                };
            }

            if (pixelGrid == null)
                return;

            double w = Bounds.OuterWidth;
            double h = Bounds.OuterHeight;

            double pixelSize = Math.Min(w, h) / 18.0;

            double startX = (w - (12 * pixelSize)) / 2.0;
            double startY = (h - (12 * pixelSize)) / 2.0;

            for (int row = 0; row < 12; row++)
            {
                for (int col = 0; col < 12; col++)
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
