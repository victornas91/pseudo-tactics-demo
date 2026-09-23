using Godot;

public partial class Player : Node2D
{
    public static bool IsPlayerTurn = true;
    public static int GameLevel = 0;

    [Export] public float Speed = 200f;
    [Export] public int GridX = 3;
    [Export] public int GridY = 3;
    [Export] public int Height = 4;
    [Export] public Vector2 TileSize = new Vector2(64, 32);
    [Export] public Color BlockColor = new Color(0.2f, 0.4f, 0.8f);
    [Export] public Color HighlightColor = new Color(1f, 1f, 0f, 0.25f);

    [Export] public int MaxHealth = 3;
    public int CurrentHealth = 3;

    private Vector2 _screenPosition;
    private Label? _hpLabel;
    private bool _wasPlayerTurn = true;

    public override void _Ready()
    {
        if (!InputMap.HasAction("move_left"))
        {
            InputMap.AddAction("move_left");
            InputMap.AddAction("move_right");
            InputMap.AddAction("move_up");
            InputMap.AddAction("move_down");
            var left  = new InputEventKey { Keycode = (Key)65 };
            var right = new InputEventKey { Keycode = (Key)68 };
            var up    = new InputEventKey { Keycode = (Key)87 };
            var down  = new InputEventKey { Keycode = (Key)83 };

            InputMap.ActionAddEvent("move_left",  left);
            InputMap.ActionAddEvent("move_right", right);
            InputMap.ActionAddEvent("move_up",    up);
            InputMap.ActionAddEvent("move_down",  down);
        }
        UpdateScreenPosition();

        Callable.From(() =>
        {
            var layer = new CanvasLayer();
            layer.Name = "PlayerHud";
            layer.Layer = 128;

            _hpLabel = new Label();
            _hpLabel.Name = "PlayerHpLabel";
            _hpLabel.Text = "Player HP: " + CurrentHealth;
            _hpLabel.Size = new Vector2(200, 40);
            _hpLabel.Position = new Vector2(10, 10);
            _hpLabel.HorizontalAlignment = HorizontalAlignment.Left;
            _hpLabel.VerticalAlignment = VerticalAlignment.Top;
            _hpLabel.AddThemeFontSizeOverride("font_size", 28);
            _hpLabel.AddThemeColorOverride("font_color", new Color(0.2f, 0.6f, 1f));

            layer.AddChild(_hpLabel);
            AddChild(layer);
        }).CallDeferred();
    }

    public void TakeDamage(int amount)
    {
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        if (_hpLabel != null)
            _hpLabel.Text = "Player HP: " + CurrentHealth;
        if (CurrentHealth <= 0)
        {
            GameLevel = 0;
            GetTree().ReloadCurrentScene();
        }
    }

    private Enemy? GetAdjacentEnemy()
    {
        var enemies = GetTree().GetNodesInGroup("enemies");
        Vector2I[] dirs = { new Vector2I(0, -1), new Vector2I(0, 1), new Vector2I(-1, 0), new Vector2I(1, 0) };
        foreach (var dir in dirs)
        {
            int nx = GridX + dir.X;
            int ny = GridY + dir.Y;
            foreach (var node in enemies)
            {
                if (node is Enemy enemy && !enemy.IsDead && enemy.GridX == nx && enemy.GridY == ny)
                    return enemy;
            }
        }
        return null;
    }

    private void CheckVictory()
    {
        var enemies = GetTree().GetNodesInGroup("enemies");
        bool anyAlive = false;
        foreach (var node in enemies)
        {
            if (node is Enemy enemy && !enemy.IsDead)
            {
                anyAlive = true;
                break;
            }
        }
        if (!anyAlive)
        {
            var isoMap = GetParent();
            if (isoMap is IsoMap map)
                map.ShowWinScreen();
        }
    }

    private void SetInputEnabled(bool enabled)
    {
        // Helper to add or remove the movement actions.
        if (enabled)
        {
            if (!InputMap.HasAction("move_left"))
            {
                InputMap.AddAction("move_left");
                InputMap.AddAction("move_right");
                InputMap.AddAction("move_up");
                InputMap.AddAction("move_down");
                var left  = new InputEventKey { Keycode = (Key)65 };
                var right = new InputEventKey { Keycode = (Key)68 };
                var up    = new InputEventKey { Keycode = (Key)87 };
                var down  = new InputEventKey { Keycode = (Key)83 };
                InputMap.ActionAddEvent("move_left",  left);
                InputMap.ActionAddEvent("move_right", right);
                InputMap.ActionAddEvent("move_up",    up);
                InputMap.ActionAddEvent("move_down",  down);
            }
        }
        else
        {
            // Remove actions so they cannot be triggered.
            InputMap.EraseAction("move_left");
            InputMap.EraseAction("move_right");
            InputMap.EraseAction("move_up");
            InputMap.EraseAction("move_down");
        }
    }

    public override void _Process(double delta)
    {
        // Enable or disable input based on whose turn it is.
        SetInputEnabled(IsPlayerTurn);

        // If it's not the player's turn, just reset the visual flag and exit.
        if (!IsPlayerTurn)
        {
            _wasPlayerTurn = false;
            return;
        }

        // When the turn just became the player's, request a redraw to update the highlight tiles.
        if (IsPlayerTurn && !_wasPlayerTurn)
        {
            QueueRedraw();
        }
        _wasPlayerTurn = IsPlayerTurn;

        bool hasMoved = false;
        int newX = GridX;
        int newY = GridY;

        if (Input.IsActionJustPressed("move_right")) { newX += 1; hasMoved = true; }
        else if (Input.IsActionJustPressed("move_left")) { newX -= 1; hasMoved = true; }
        else if (Input.IsActionJustPressed("move_down")) { newY += 1; hasMoved = true; }
        else if (Input.IsActionJustPressed("move_up")) { newY -= 1; hasMoved = true; }

        if (hasMoved)
        {
            if (IsInBounds(newX, newY) && !IsTileOccupiedByEnemy(newX, newY))
            {
                GridX = newX;
                GridY = newY;
                UpdateScreenPosition();
                QueueRedraw();
            }
            // End the player's turn after a successful move.
            IsPlayerTurn = false;
            return;
        }

        if (Input.IsActionJustPressed("ui_accept"))
        {
            var enemy = GetAdjacentEnemy();
            if (enemy != null && !enemy.IsQueuedForDeletion())
            {
                enemy.TakeDamage(1);
                IsPlayerTurn = false;
                CheckVictory();
            }
        }
    }

    private void UpdateScreenPosition()
    {
        var halfW = TileSize.X * 0.5f;
        var halfH = TileSize.Y * 0.5f;
        _screenPosition = new Vector2((GridX - GridY) * halfW, (GridX + GridY) * halfH);
    }

    private bool IsInBounds(int x, int y)
    {
        var map = GetParent() as IsoMap;
        if (map == null) return false;
        return x >= 0 && x < map.MapWidth && y >= 0 && y < map.MapHeight;
    }

    private bool IsTileOccupiedByEnemy(int x, int y)
    {
        var enemies = GetTree().GetNodesInGroup("enemies");
        foreach (var node in enemies)
        {
            if (node is Enemy enemy && !enemy.IsDead && enemy.GridX == x && enemy.GridY == y)
                return true;
        }
        return false;
    }

    public override void _Draw()
    {
        var map = GetParent() as IsoMap;
        var mapWidth = map?.MapWidth ?? 6;
        var mapHeight = map?.MapHeight ?? 6;

        var halfW = TileSize.X * 0.5f;
        var halfH = TileSize.Y * 0.5f;
        var blockSize = new Vector2(halfW * 0.8f, halfH * 0.8f);

        if (IsPlayerTurn)
        {
            Vector2I[] directions = { new Vector2I(0, -1), new Vector2I(0, 1), new Vector2I(-1, 0), new Vector2I(1, 0) };
            foreach (var dir in directions)
            {
                int nx = GridX + dir.X;
                int ny = GridY + dir.Y;
                if (nx >= 0 && nx < mapWidth && ny >= 0 && ny < mapHeight && !IsTileOccupiedByEnemy(nx, ny))
                {
                    var tilePos = new Vector2((nx - ny) * halfW, (nx + ny) * halfH);
                    var pts = new Vector2[]
                    {
                        tilePos + new Vector2(0, -halfH),
                        tilePos + new Vector2(halfW, 0),
                        tilePos + new Vector2(0, halfH),
                        tilePos + new Vector2(-halfW, 0),
                    };
                    DrawColoredPolygon(pts, HighlightColor);
                    DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[3], pts[0] }, Colors.Yellow, 1.5f, true);
                }
            }
        }

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