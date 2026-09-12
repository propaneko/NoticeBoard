using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace NoticeBoard.Rendering;

// Process-lifetime Cairo stamps for <itemstack> on paper. The pixels are the engine's own GUI
// shot of the stack (RenderItemStackToAtlas, the handbook view), read back once per code, so a
// shaped item reads as the item and not as its texture map. The engine renders on the next
// Ortho stage: the first Draw for a code paints nothing and onReady fires later on the main
// thread, where the caller marks itself dirty and repaints. Null results are cached too.
public static class ItemIconCache
{
    // Atlas texels per icon. Body ink is about 20 Cairo px per line, supersampled up to 4x.
    private const int AtlasSize = 64;

    private static readonly Dictionary<string, ImageSurface> cache = new();
    private static readonly Dictionary<string, List<Action>> pending = new();

    public static void Draw(
        ICoreClientAPI api,
        Context ctx,
        string code,
        bool isBlock,
        double x,
        double y,
        double size,
        Action onReady
    )
    {
        if (ctx == null || size <= 0 || !TryGet(api, code, isBlock, onReady, out ImageSurface surface))
            return;

        ctx.Save();
        ctx.Translate(x, y);
        ctx.Scale(size / surface.Width, size / surface.Height);
        ctx.SetSourceSurface(surface, 0, 0);
        ctx.Paint();
        ctx.Restore();
    }

    private static bool TryGet(ICoreClientAPI api, string code, bool isBlock, Action onReady, out ImageSurface surface)
    {
        string key = (isBlock ? "block:" : "item:") + code;

        if (cache.TryGetValue(key, out surface))
            return surface != null;

        if (pending.TryGetValue(key, out List<Action> waiters))
        {
            if (onReady != null)
                waiters.Add(onReady);
            return false;
        }

        var location = new AssetLocation(code);
        CollectibleObject collectible = isBlock
            ? api.World.GetBlock(location)
            : api.World.GetItem(location);
        if (collectible == null)
        {
            cache[key] = null;
            return false;
        }

        waiters = new List<Action>();
        if (onReady != null)
            waiters.Add(onReady);
        pending[key] = waiters;

        ITextureAtlasAPI atlas = api.BlockTextureAtlas;
        // Set false once the engine call returns. Inside the Ortho stage the render completes
        // synchronously and the callback runs before that, in which case the caller gets the
        // surface from this very TryGet and must not also be told to repaint.
        bool inCall = true;
        try
        {
            api.Render.RenderItemStackToAtlas(new ItemStack(collectible), atlas, AtlasSize, subId =>
            {
                cache[key] = ReadBack(api, atlas, subId, code);
                pending.Remove(key);
                if (inCall)
                    return;
                foreach (Action waiter in waiters)
                    waiter();
            });
        }
        catch (Exception ex)
        {
            pending.Remove(key);
            cache[key] = null;
            api.Logger.Warning($"[NoticeBoard] Failed to render item icon for '{code}': {ex.Message}");
        }
        inCall = false;

        return cache.TryGetValue(key, out surface) && surface != null;
    }

    // Runs on the main thread inside the Ortho stage, right after the engine drew the stack into
    // the atlas. Copies that atlas cell into a scratch texture of its own size, reads it back
    // through the current framebuffer, and hands the pixels to Cairo.
    private static ImageSurface ReadBack(ICoreClientAPI api, ITextureAtlasAPI atlas, int subId, string code)
    {
        IRenderAPI rpi = api.Render;
        FrameBufferRef previous = rpi.CurrentFrameBuffer;
        FrameBufferRef fb = null;
        LoadedTexture scratch = null;
        try
        {
            TextureAtlasPosition pos = atlas.Positions[subId];
            LoadedTexture atlasTexture = atlas.AtlasTextures[pos.atlasNumber];

            scratch = new LoadedTexture(api) { Width = AtlasSize, Height = AtlasSize };
            rpi.LoadOrUpdateTextureFromRgba(new int[AtlasSize * AtlasSize], false, 0, ref scratch);
            fb = rpi.CreateFrameBuffer(scratch);

            // A negative alphaTest turns blending off inside the engine copy, so the shot's own
            // alpha lands in the scratch texture instead of being composited over it.
            rpi.RenderTextureIntoFrameBuffer(
                0,
                atlasTexture,
                pos.x1 * atlasTexture.Width,
                pos.y1 * atlasTexture.Height,
                AtlasSize,
                AtlasSize,
                fb,
                0,
                0,
                -1f
            );

            rpi.CurrentFrameBuffer = fb;
            using BitmapRef bitmap = rpi.GrabScreenshot(AtlasSize, AtlasSize, false, false, true);
            return ToPremultipliedSurface(bitmap);
        }
        catch (Exception ex)
        {
            api.Logger.Warning($"[NoticeBoard] Failed to read back item icon for '{code}': {ex.Message}");
            return null;
        }
        finally
        {
            // Binding the default framebuffer (null) does not touch the viewport, so put the
            // window size back by hand or the rest of this Ortho stage draws into 64x64 px.
            rpi.CurrentFrameBuffer = previous;
            if (previous == null)
                rpi.GlViewport(0, 0, rpi.FrameWidth, rpi.FrameHeight);
            if (fb != null)
                rpi.DestroyFrameBuffer(fb);
            scratch?.Dispose();
        }
    }

    // BitmapRef.Pixels is 0xAARRGGBB straight alpha; Cairo Argb32 is the same word order but
    // premultiplied, so the shot's antialiased edges need the multiply or they bloom white.
    private static ImageSurface ToPremultipliedSurface(BitmapRef bitmap)
    {
        int[] pixels = bitmap.Pixels;
        for (int i = 0; i < pixels.Length; i++)
        {
            uint c = (uint)pixels[i];
            uint a = c >> 24;
            if (a == 255)
                continue;
            if (a == 0)
            {
                pixels[i] = 0;
                continue;
            }
            uint r = ((c >> 16) & 0xFF) * a / 255;
            uint g = ((c >> 8) & 0xFF) * a / 255;
            uint b = (c & 0xFF) * a / 255;
            pixels[i] = (int)((a << 24) | (r << 16) | (g << 8) | b);
        }

        var surface = new ImageSurface(Format.Argb32, bitmap.Width, bitmap.Height);
        Marshal.Copy(pixels, 0, surface.DataPtr, pixels.Length);
        surface.MarkDirty();
        return surface;
    }
}
