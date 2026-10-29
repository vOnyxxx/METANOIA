namespace Metanoia.Core.Services;

public readonly record struct ImageCropRegion(int X, int Y, int Width, int Height);

public static class ImageCropGeometry
{
    /// <summary>Maps a drag in a Uniform viewport to source pixels, excluding letterboxing.</summary>
    public static ImageCropRegion? FromViewport(int width, int height, double viewportWidth, double viewportHeight,
        double startX, double startY, double endX, double endY)
    {
        if (width <= 0 || height <= 0 || viewportWidth <= 0 || viewportHeight <= 0) return null;
        var scale = Math.Min(viewportWidth / width, viewportHeight / height);
        var offsetX = (viewportWidth - width * scale) / 2; var offsetY = (viewportHeight - height * scale) / 2;
        var x = Math.Clamp((int)Math.Floor((Math.Min(startX, endX) - offsetX) / scale), 0, width);
        var y = Math.Clamp((int)Math.Floor((Math.Min(startY, endY) - offsetY) / scale), 0, height);
        var right = Math.Clamp((int)Math.Ceiling((Math.Max(startX, endX) - offsetX) / scale), 0, width);
        var bottom = Math.Clamp((int)Math.Ceiling((Math.Max(startY, endY) - offsetY) / scale), 0, height);
        return right - x < 2 || bottom - y < 2 ? null : new(x, y, right - x, bottom - y);
    }
}
