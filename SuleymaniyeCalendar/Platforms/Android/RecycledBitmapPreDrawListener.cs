#if ANDROID
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;

namespace SuleymaniyeCalendar;

/// <summary>
/// Pre-draw listener that prevents "Cannot draw recycled bitmaps" RuntimeException.
/// 
/// When RecyclerView recycles CollectionView cells, Glide may recycle the underlying
/// bitmap before the Android render thread completes its draw pass. This causes
/// <c>BaseCanvas.throwIfCannotDraw</c> → <c>java.lang.RuntimeException</c> which
/// crashes the app on the render thread (bypassing managed exception handlers).
///
/// This listener runs just before each draw pass and checks whether the ImageView's
/// drawable contains a recycled bitmap. If so, it clears the ImageView to prevent
/// the crash. The image will be re-loaded by Glide when the cell is next bound.
///
/// Google Play impact: 81.9% of all crashes (258+ affected users).
/// </summary>
internal sealed class RecycledBitmapPreDrawListener : Java.Lang.Object, ViewTreeObserver.IOnPreDrawListener
{
    private readonly WeakReference<ImageView> _imageViewRef;

    public RecycledBitmapPreDrawListener(ImageView imageView)
    {
        _imageViewRef = new WeakReference<ImageView>(imageView);
    }

    public bool OnPreDraw()
    {
        if (!_imageViewRef.TryGetTarget(out var imageView))
        {
            // ImageView was GC'd — remove this listener to avoid leaks
            return true;
        }

        try
        {
            if (HasRecycledBitmap(imageView.Drawable))
            {
                System.Diagnostics.Debug.WriteLine(
                    "[RecycledBitmapGuard] Cleared recycled bitmap from ImageView to prevent crash");
                imageView.SetImageDrawable(null);
            }
        }
        catch (Exception ex)
        {
            // Last-resort safety: if checking the bitmap itself throws, clear the drawable
            System.Diagnostics.Debug.WriteLine(
                $"[RecycledBitmapGuard] Exception during bitmap check, clearing ImageView: {ex.Message}");
            try { imageView.SetImageDrawable(null); } catch { /* swallow */ }
        }

        return true; // proceed with draw
    }

    /// <summary>
    /// Recursively checks whether a drawable (or any layer within it) wraps a recycled bitmap.
    /// Handles BitmapDrawable, LayerDrawable (used by MAUI for compound drawables), and
    /// DrawableWrapper/InsetDrawable patterns.
    /// </summary>
    private static bool HasRecycledBitmap(Drawable? drawable)
    {
        return drawable switch
        {
            null => false,
            BitmapDrawable bd => bd.Bitmap?.IsRecycled == true,
            LayerDrawable ld => CheckLayerDrawable(ld),
            DrawableWrapper dw => HasRecycledBitmap(dw.Drawable),
            _ => false
        };
    }

    private static bool CheckLayerDrawable(LayerDrawable ld)
    {
        for (int i = 0; i < ld.NumberOfLayers; i++)
        {
            if (HasRecycledBitmap(ld.GetDrawable(i)))
                return true;
        }
        return false;
    }
}
#endif
