using Godot;

public partial class Enemy : Node2D
{
    [Export] public float Speed = 200f;
    [Export] public int GridX = 0;
    [Export] public int GridY = 0;
    [Export] public int Height = 4;
    [Export] public Vector2 TileSize = new Vector2(64, 32);
    [Export] public Color BlockColor = new Color(0.8f, 0.2f, 0.2f);

    [Export] public int MaxHealth = 1;
    public int CurrentHealth = 1;
    public bool IsDead = false;

    private Vector2 _screenPosition;
    private Label? _hpLabel;
    private bool _hasActedThisTurn = false;
    private bool _waitingForTimer = false;
    private bool _positionSetExternally = false;

    public override void _Ready()
    {
        AddToGroup("enemies");

        // Only randomize position if not set externally via SetGridPosition
        if (!_positionSetExternally)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            int playerStartX = 3;
            int playerStartY = 3;
            do
            {
                GridX = rng.RandiRange(0, 5);
                GridY = rng.RandiRange(0, 5);
            } while (GridX == playerStartX && GridY == playerStartY);
        }

        UpdateScreenPosition();
        QueueRedraw();

        Callable.From(() =>
        {
            var layer = new CanvasLayer();
            layer.Name = "EnemyHud";
            layer.Layer = 128;

            _hpLabel = new Label();
            _hpLabel.Name = "EnemyHpLabel";
            _hpLabel.Text = "Enemy HP: " + CurrentHealth;
            _hpLabel.Size = new Vector2(200, 40);
            _hpLabel.Position = new Vector2(GetViewportRect().Size.X - 210, 10);
            _hpLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _hpLabel.VerticalAlignment = VerticalAlignment.Top;
            _hpLabel.AddThemeFontSizeOverride("font_size", 28);
            _hpLabel.AddThemeColorOverride("font_color", new Color(1f, 0.2f, 0.2f));

            layer.AddChild(_hpLabel);
            AddChild(layer);
        }).CallDeferred();
    }

    public void SetGridPosition(int gx, int gy)
    {
        GridX = gx;
        GridY = gy;
        _positionSetExternally = true;
    }

    public void TakeDamage(int amount)
    {
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        if (_hpLabel != null)
            _hpLabel.Text = "Enemy HP: " + CurrentHealth;
        if (CurrentHealth <= 0)
        {
            IsDead = true;
            QueueFree();
        }
    }

    private void PerformEnemyAction()
    {
        if (_hasActedThisTurn) return;
        _hasActedThisTurn = true;
        _waitingForTimer = false;

        var player = GetNodeOrNull<Player>("../Player");
        if (player == null || player.IsQueuedForDeletion())
        {
            Player.IsPlayerTurn = true;
            return;
        }

        int dx = Mathf.Sign(player.GridX - GridX);
        int dy = Mathf.Sign(player.GridY - GridY);

        if (Mathf.Abs(player.GridX - GridX) + Mathf.Abs(player.GridY - GridY) == 1)
        {
            player.TakeDamage(1);
        }
        else
        {
        int newX = GridX;
        int newY = GridY;

        var map = GetParent() as IsoMap;
        int mw = map?.MapWidth ?? 6;
        int mh = map?.MapHeight ?? 6;

            if (Mathf.Abs(player.GridX - GridX) >= Mathf.Abs(player.GridY - GridY))
            {
                if (dx != 0 && GridX + dx >= 0 && GridX + dx < mw)
                    newX = GridX + dx;
                else if (dy != 0 && GridY + dy >= 0 && GridY + dy < mh)
                    newY = GridY + dy;
            }
            else
            {
                if (dy != 0 && GridY + dy >= 0 && GridY + dy < mh)
                    newY = GridY + dy;
                else if (dx != 0 && GridX + dx >= 0 && GridX + dx < mw)
                    newX = GridX + dx;
            }

        // Ensure the target tile is not already occupied by another enemy.
        if (map != null && map.IsPositionOccupiedByEnemy(newX, newY))
        {
            // Stay in place if another enemy is there.
            newX = GridX;
            newY = GridY;
        }
        GridX = newX;
        GridY = newY;
        }

        UpdateScreenPosition();
        QueueRedraw();
        Player.IsPlayerTurn = true;
    }

    public override void _Process(double delta)
    {
        if (Player.IsPlayerTurn)
        {
            _hasActedThisTurn = false;
            return;
        }

        if (!_hasActedThisTurn && !_waitingForTimer)
        {
            _waitingForTimer = true;
            var timer = new Timer();
            timer.OneShot = true;
            timer.Timeout += PerformEnemyAction;
            AddChild(timer);
            timer.Start(0.5f);
        }
    }

    private void UpdateScreenPosition()
    {
        var halfW = TileSize.X * 0.5f;
        var halfH = TileSize.Y * 0.5f;
        _screenPosition = new Vector2((GridX - GridY) * halfW, (GridX + GridY) * halfH);
    }

    public override void _Draw()
    {
        var halfW = TileSize.X * 0.5f;
        var halfH = TileSize.Y * 0.5f;
        var blockSize = new Vector2(halfW * 0.8f, halfH * 0.8f);

        for (int h = 0; h < Height; h++)
        {
            var blockOffset = new Vector2(0, -h * halfH * 0.6f);
            var blockPos = _screenPosition + blockOffset;
            Vector2[] pts =
            {
                blockPos + new Vector2(0, -blockSize.Y),
                blockPos + new Vector2(blockSize.X, 0),
                blockPos + new Vector2(0, blockSize.Y),
                blockPos + new Vector2(-blockSize.X, 0),
            };
            DrawColoredPolygon(pts, BlockColor);
            DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[3], pts[0] }, Color.Color8(0, 0, 0, 200), 1.5f, true);
        }
    }
}