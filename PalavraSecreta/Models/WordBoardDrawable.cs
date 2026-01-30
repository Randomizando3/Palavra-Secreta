using Microsoft.Maui.Graphics;

namespace PalavraSecreta.Models;

public sealed class WordBoardDrawable : IDrawable
{
    private readonly List<WordCell> _cells = new();

    private float _lensX;
    private float _lensY;

    private float _lensRadius = 82;
    private bool _lensInitialized;

    private float _panStartX;
    private float _panStartY;

    private RectF _lastRect;

    public string CurrentLensColor => "red";

    private const float LensZoom = 2.0f;

    private static readonly IFont PosterFont =
        new Microsoft.Maui.Graphics.Font("PosterBlack");

    public void Load(List<SecretWordItem> items)
    {
        _cells.Clear();

        foreach (var it in items)
        {
            if (it == null) continue;
            if (!it.IsStructurallyValid()) continue;

            it.HiddenColor = "red";
            _cells.Add(new WordCell { Item = it });
        }

        _lensInitialized = false;
    }

    public void SetLensPosition(float x, float y)
    {
        _lensX = x;
        _lensY = y;
        _lensInitialized = true;

        ClampLens();

        _panStartX = _lensX;
        _panStartY = _lensY;
    }

    public void BeginPan()
    {
        _panStartX = _lensX;
        _panStartY = _lensY;
    }

    public void MoveLensBy(float totalX, float totalY)
    {
        _lensX = _panStartX + totalX;
        _lensY = _panStartY + totalY;
        ClampLens();
    }

    public void CommitPan()
    {
        _panStartX = _lensX;
        _panStartY = _lensY;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        _lastRect = dirtyRect;

        if (!_lensInitialized)
        {
            _lensX = dirtyRect.Center.X;
            _lensY = dirtyRect.Center.Y;
            _lensInitialized = true;
            ClampLens();

            _panStartX = _lensX;
            _panStartY = _lensY;
        }

        canvas.FillColor = Colors.White;
        canvas.FillRectangle(dirtyRect);

        float padding = 6f;
        float gap = 6f;

        int colCount = 4;

        if (_cells.Count == 0)
        {
            DrawLens(canvas);
            return;
        }

        int rows = (int)MathF.Ceiling(_cells.Count / (float)colCount);

        float usableH = dirtyRect.Height - padding * 2 - gap * Math.Max(0, rows - 1);
        float cellH = Math.Clamp(usableH / rows, 56f, 140f);

        float usableW = dirtyRect.Width - padding * 2 - gap * (colCount - 1);
        float cellW = usableW / colCount;

        float baseFontSize = 20f;
        float baselineOffsetY = cellH * 0.32f;

        for (int i = 0; i < _cells.Count; i++)
        {
            int row = i / colCount;
            int col = i % colCount;

            float x = padding + col * (cellW + gap);
            float y = padding + row * (cellH + gap);

            var r = new RectF(x, y, cellW, cellH);
            _cells[i].Bounds = r;

            canvas.StrokeColor = Color.FromArgb("#E6E6E6");
            canvas.StrokeSize = 2;
            canvas.FillColor = Color.FromArgb("#F7F7F7");
            canvas.FillRoundedRectangle(r, 14);
            canvas.DrawRoundedRectangle(r, 14);

            var it = _cells[i].Item;
            var word = (it.BaseWord ?? "").Trim().ToUpperInvariant();
            if (word.Length == 0) continue;

            canvas.Font = PosterFont;

            float step = baseFontSize * 1.2f;
            float zoomStep = step * 3.5f;

            float wordW = step * word.Length;
            float maxW = cellW - 10f;

            if (wordW > maxW)
            {
                step = maxW / word.Length;
                zoomStep = step * 1.25f;
                wordW = step * word.Length;
            }

            float startX = x + (cellW - wordW) / 2f;
            float baselineY = y + baselineOffsetY;

            int start = it.HiddenStart;
            int len = it.HiddenLength;

            for (int k = 0; k < word.Length; k++)
            {
                bool inHidden = (k >= start && k < start + len);

                var letterRect = new RectF(
                    startX + k * step,
                    baselineY,
                    step,
                    baseFontSize * 1.9f
                );

                bool underLens =
                    CircleIntersectsRect(_lensX, _lensY, _lensRadius, letterRect);

                if (inHidden && underLens)
                    continue;

                canvas.FontColor = inHidden
                    ? Colors.Red
                    : Color.FromArgb("#111111");

                if (underLens)
                {
                    // 🔍 ZOOM CENTRALIZADO (cresce para todos os lados)
                    canvas.FontSize = baseFontSize * LensZoom;

                    float zoomW = zoomStep * LensZoom;
                    float zoomH = baseFontSize * LensZoom * 1.9f;

                    var zoomRect = new RectF(
                        letterRect.Center.X - zoomW / 2f,
                        letterRect.Center.Y - zoomH / 2f,
                        zoomW,
                        zoomH
                    );

                    canvas.DrawString(
                        word[k].ToString(),
                        zoomRect.X,
                        zoomRect.Y,
                        zoomRect.Width,
                        zoomRect.Height,
                        HorizontalAlignment.Center,
                        VerticalAlignment.Center
                    );
                }
                else
                {
                    canvas.FontSize = baseFontSize;

                    canvas.DrawString(
                        word[k].ToString(),
                        letterRect.X,
                        letterRect.Y,
                        letterRect.Width,
                        letterRect.Height,
                        HorizontalAlignment.Center,
                        VerticalAlignment.Center
                    );
                }
            }
        }

        DrawLens(canvas);
    }

    private void ClampLens()
    {
        if (_lastRect.Width <= 0 || _lastRect.Height <= 0)
            return;

        float minX = _lensRadius + 6;
        float minY = _lensRadius + 6;
        float maxX = Math.Max(minX, _lastRect.Width - _lensRadius - 6);
        float maxY = Math.Max(minY, _lastRect.Height - _lensRadius - 6);

        _lensX = Math.Clamp(_lensX, minX, maxX);
        _lensY = Math.Clamp(_lensY, minY, maxY);
    }

    private void DrawLens(ICanvas canvas)
    {
        var red = Colors.Red;
        var fill = new Color(red.Red, red.Green, red.Blue, 0.5f);

        canvas.StrokeSize = 6;
        canvas.StrokeColor = red;
        canvas.FillColor = fill;

        canvas.FillCircle(_lensX, _lensY, _lensRadius);
        canvas.DrawCircle(_lensX, _lensY, _lensRadius);

        canvas.StrokeSize = 3;
        canvas.StrokeColor = new Color(1, 1, 1, 0.35f);
        canvas.DrawArc(
            _lensX - _lensRadius * 0.35f,
            _lensY - _lensRadius * 0.35f,
            _lensRadius * 0.7f,
            _lensRadius * 0.7f,
            210,
            320,
            false,
            false
        );

        canvas.StrokeSize = 10;
        canvas.StrokeColor = Color.FromArgb("#222222");
        canvas.DrawLine(
            _lensX + _lensRadius * 0.55f,
            _lensY + _lensRadius * 0.55f,
            _lensX + _lensRadius * 1.15f,
            _lensY + _lensRadius * 1.15f
        );
    }

    private static bool CircleIntersectsRect(float cx, float cy, float r, RectF rect)
    {
        float closestX = Math.Clamp(cx, rect.Left, rect.Right);
        float closestY = Math.Clamp(cy, rect.Top, rect.Bottom);
        float dx = cx - closestX;
        float dy = cy - closestY;
        return (dx * dx + dy * dy) <= r * r;
    }

    private sealed class WordCell
    {
        public SecretWordItem Item { get; set; } = new();
        public RectF Bounds { get; set; }
    }
}
