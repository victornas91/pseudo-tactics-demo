using Godot;

public partial class IsoMap : Node2D
{
    [Export] public int MapWidth = 6;
    [Export] public int MapHeight = 6;

    [Export] public Vector2 TileSize = new Vector2(64, 32);
    [Export] public Vector2 MapOrigin = Vector2.Zero;

    [Export] public Color ColorA = new Color(0.20f, 0.55f, 0.25f);
    [Export] public Color ColorB = new Color(0.18f, 0.50f, 0.22f);
    [Export] public Color OutlineColor = new Color(0f, 0f, 0f, 0.25f);

    [Export] public float CameraSpeed = 600f;
    [Export] public float ZoomStep = 0.10f;
    [Export] public float MinZoom = 0.60f;
    [Export] public float MaxZoom = 2.50f;

    private Camera2D? _camera;
    private Label? _turnLabel;

    public override void _Ready()
    {
        _camera = GetNodeOrNull<Camera2D>("Camera2D");
        if (_camera == null)
        {
            _camera = new Camera2D { Name = "Camera2D" };
            AddChild(_camera);
        }
        _camera.MakeCurrent();

        var center = GridToScreen((MapWidth - 1) * 0.5f, (MapHeight - 1) * 0.5f) + MapOrigin;
        _camera.Position = center;

        // Setup map based on current level
        if (Player.GameLevel > 0)
        {
            var scale = 1.0f + (Player.GameLevel * 0.5f);
            MapWidth = Mathf.RoundToInt(6 * scale);
            MapHeight = Mathf.RoundToInt(6 * scale);

            // Remove existing enemies from scene file
            var existingEnemies = GetTree().GetNodesInGroup("enemies");
            foreach (var node in existingEnemies)
            {
                if (node is Enemy)
                    node.QueueFree();
            }

            // Spawn additional enemies for this level
            int enemyCount = 1 + Player.GameLevel;
            SpawnAdditionalEnemies(enemyCount);

            center = GridToScreen((MapWidth - 1) * 0.5f, (MapHeight - 1) * 0.5f) + MapOrigin;
            _camera.Position = center;
        }

        // Splash
        Callable.From(() =>
        {
            var viewportSize = GetViewportRect().Size;

            var splashLayer = new CanvasLayer();
            splashLayer.Name = "SplashLayer";
            splashLayer.Layer = 128;

            var splash = new ColorRect();
            splash.Name = "SplashScreen";
            splash.Color = new Color(0f, 0f, 0f, 0.85f);
            splash.Position = Vector2.Zero;
            splash.Size = viewportSize;
            splash.MouseFilter = Control.MouseFilterEnum.Ignore;

            var label = new Label();
            label.Text = "WELCOME";
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Size = viewportSize;
            label.Position = Vector2.Zero;
            label.AddThemeFontSizeOverride("font_size", 64);
            label.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));

            splash.AddChild(label);
            splashLayer.AddChild(splash);
            AddChild(splashLayer);

            var timer = new Timer();
            timer.Name = "SplashTimer";
            timer.OneShot = true;
            timer.Timeout += () =>
            {
                var sl = GetNodeOrNull<CanvasLayer>("SplashLayer");
                sl?.QueueFree();
                var t = GetNodeOrNull<Timer>("SplashTimer");
                t?.QueueFree();
            };
            AddChild(timer);
            timer.Start(2.0);
        }).CallDeferred();

        // Turn indicator
        Callable.From(() =>
        {
            var turnLayer = new CanvasLayer();
            turnLayer.Name = "TurnLayer";
            turnLayer.Layer = 128;

            _turnLabel = new Label();
            _turnLabel.Name = "TurnLabel";
            _turnLabel.Text = "Turno do Jogador";
            _turnLabel.Size = new Vector2(300, 50);
            _turnLabel.Position = new Vector2(GetViewportRect().Size.X / 2 - 150, 60);
            _turnLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _turnLabel.VerticalAlignment = VerticalAlignment.Center;
            _turnLabel.AddThemeFontSizeOverride("font_size", 28);
            _turnLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 0.2f));

            turnLayer.AddChild(_turnLabel);
            AddChild(turnLayer);
        }).CallDeferred();

        QueueRedraw();
    }

    private void SpawnAdditionalEnemies(int count)
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();

        for (int i = 0; i < count; i++)
        {
            var enemyScene = ResourceLoader.Load<PackedScene>("res://Enemy.tscn");
            if (enemyScene == null) continue;

            var enemy = enemyScene.Instantiate<Enemy>();

            int ex, ey;
            do
            {
                ex = rng.RandiRange(0, MapWidth - 1);
                ey = rng.RandiRange(0, MapHeight - 1);
            } while ((ex == 3 && ey == 3) || IsPositionOccupiedByEnemy(ex, ey));

            enemy.SetGridPosition(ex, ey);
            AddChild(enemy);
        }
    }

    // Made public so other scripts (e.g., Enemy) can query tile occupancy.
    public bool IsPositionOccupiedByEnemy(int x, int y)
    {
        var enemies = GetTree().GetNodesInGroup("enemies");
        foreach (var node in enemies)
        {
            if (node is Enemy enemy && !enemy.IsDead && enemy.GridX == x && enemy.GridY == y)
                return true;
        }
        return false;
    }

    public void ShowWinScreen()
    {
        Player.GameLevel++;

        Callable.From(() =>
        {
            var viewportSize = GetViewportRect().Size;

            var winLayer = new CanvasLayer();
            winLayer.Name = "WinLayer";
            winLayer.Layer = 128;

            var winSplash = new ColorRect();
            winSplash.Name = "WinSplash";
            winSplash.Color = new Color(0f, 0.2f, 0f, 0.9f);
            winSplash.Position = Vector2.Zero;
            winSplash.Size = viewportSize;
            winSplash.MouseFilter = Control.MouseFilterEnum.Ignore;

            var label = new Label();
            label.Text = "Você venceu!";
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Size = viewportSize;
            label.Position = Vector2.Zero;
            label.AddThemeFontSizeOverride("font_size", 72);
            label.AddThemeColorOverride("font_color", new Color(1f, 1f, 0.2f));

            winSplash.AddChild(label);
            winLayer.AddChild(winSplash);
            AddChild(winLayer);

            var timer = new Timer();
            timer.OneShot = true;
            timer.Timeout += () => GetTree().ReloadCurrentScene();
            AddChild(timer);
            timer.Start(2.0);
        }).CallDeferred();
    }

    public override void _Process(double delta)
    {
        if (_camera == null) return;

        if (_turnLabel != null)
        {
            _turnLabel.Text = Player.IsPlayerTurn ? "Turno do Jogador" : "Turno do Inimigo";
        }

        var input = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
        if (input == Vector2.Zero) return;

        _camera.Position += input * CameraSpeed * (float)delta / _camera.Zoom.X;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_camera == null) return;

        if (e is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp)
                SetZoom(_camera.Zoom.X - ZoomStep);
            else if (mb.ButtonIndex == MouseButton.WheelDown)
                SetZoom(_camera.Zoom.X + ZoomStep);
        }
    }

    public override void _Draw()
    {
        var halfW = TileSize.X * 0.5f;
        var halfH = TileSize.Y * 0.5f;

        for (var y = 0; y < MapHeight; y++)
        {
            for (var x = 0; x < MapWidth; x++)
            {
                var c = GridToScreen(x, y) + MapOrigin;

                Vector2[] pts =
                {
                    c + new Vector2(0, -halfH),
                    c + new Vector2(halfW, 0),
                    c + new Vector2(0, halfH),
                    c + new Vector2(-halfW, 0),
                };

                var fill = ((x + y) & 1) == 0 ? ColorA : ColorB;
                DrawColoredPolygon(pts, fill);
                DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[3], pts[0] }, OutlineColor, 2f, true);
            }
        }
    }

    private Vector2 GridToScreen(float x, float y)
    {
        var halfW = TileSize.X * 0.5f;
        var halfH = TileSize.Y * 0.5f;
        return new Vector2((x - y) * halfW, (x + y) * halfH);
    }

    private Vector2 GridToScreen(int x, int y) => GridToScreen((float)x, (float)y);

    private void SetZoom(float z)
    {
        z = Mathf.Clamp(z, MinZoom, MaxZoom);
        _camera!.Zoom = new Vector2(z, z);
    }
}