using SkiaSharp;

namespace PresidentsMap;

/// <summary>Draws the 1200x630 PNG that social networks show when a certificate link is shared.</summary>
public sealed class CardRenderer
{
    private const int W = 1200, H = 630;
    private readonly SKTypeface[] _serif, _sans;

    public CardRenderer(IWebHostEnvironment env)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Assets");
        SKTypeface Load(string file) => SKTypeface.FromFile(Path.Combine(dir, file))
            ?? throw new FileNotFoundException("Card font is missing", file);
        // The Georgian faces carry no Latin letters, digits or punctuation, so each has a companion face.
        _serif = [Load("NotoSerifGeorgian-Bold.ttf"), Load("NotoSerif-Bold.ttf")];
        _sans = [Load("NotoSansGeorgian-Regular.ttf"), Load("NotoSans-Regular.ttf")];
    }

    public byte[] Render(Gift g, string countryName)
    {
        using var surface = SKSurface.Create(new SKImageInfo(W, H));
        var c = surface.Canvas;
        c.Clear(new SKColor(0xF7, 0xF4, 0xEA));

        using var frame = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(0xB9, 0xA9, 0x75), IsAntialias = true };
        frame.StrokeWidth = 6; c.DrawRect(24, 24, W - 48, H - 48, frame);
        frame.StrokeWidth = 2; c.DrawRect(40, 40, W - 80, H - 80, frame);

        var ink = new SKColor(0x22, 0x20, 0x1A);
        var soft = new SKColor(0x6A, 0x63, 0x50);
        Line(c, "საპატიო პრეზიდენტის სერტიფიკატი", _sans, 30, 128, soft);
        Line(c, "№ " + g.Number, _sans, 24, 172, soft);
        Line(c, g.To, _serif, 92, 300, ink);
        Line(c, "ქვეყანა: " + countryName, _serif, 50, 390, ink);
        Line(c, "მმართველობის დღე: " + Ka.Date(g.Date), _sans, 34, 456, ink);
        if (g.From.Length > 0) Line(c, "გამჩუქებელი: " + g.From, _sans, 30, 514, soft);
        Line(c, "სიმბოლური ტიტული · პრეზიდენტების რუკა", _sans, 20, 566, soft);

        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    /// <summary>Centered single line, shrunk until it fits the frame. Each character is drawn in the first face that has it.</summary>
    private static void Line(SKCanvas c, string text, SKTypeface[] faces, float size, float y, SKColor color)
    {
        var runs = new List<(SKTypeface Face, string Text)>();
        foreach (var rune in text.EnumerateRunes())
        {
            var face = faces.FirstOrDefault(f => f.ContainsGlyph(rune.Value)) ?? faces[^1];
            if (runs.Count > 0 && runs[^1].Face == face) runs[^1] = (face, runs[^1].Text + rune);
            else runs.Add((face, rune.ToString()));
        }

        using var p = new SKPaint { TextSize = size, Color = color, IsAntialias = true };
        float Width() => runs.Sum(r => { p.Typeface = r.Face; return p.MeasureText(r.Text); });
        while (Width() > W - 160 && p.TextSize > 18) p.TextSize -= 2;

        var x = (W - Width()) / 2f;
        foreach (var (face, run) in runs)
        {
            p.Typeface = face;
            c.DrawText(run, x, y, p);
            x += p.MeasureText(run);
        }
    }
}
