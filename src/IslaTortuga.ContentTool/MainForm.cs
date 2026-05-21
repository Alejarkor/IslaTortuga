using System.ComponentModel;
using IslaTortuga.ContentTool.Import;

namespace IslaTortuga.ContentTool;

internal sealed class MainForm : Form
{
    private readonly ContentPackImportService _importService = new();
    private readonly Dictionary<string, string> _dependencyOverrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly BindingList<DependencyRow> _dependencyRows = new();

    private ImportScanResult? _currentScan;
    private TextBox _mapPathTextBox = null!;
    private TextBox _contentPathTextBox = null!;
    private TextBox _versionTextBox = null!;
    private TextBox _contentPackIdTextBox = null!;
    private TextBox _mapIdTextBox = null!;
    private TextBox _logTextBox = null!;
    private DataGridView _dependencyGrid = null!;
    private CheckBox _setDefaultPackCheckBox = null!;
    private Button _analyzeButton = null!;
    private Button _exportButton = null!;
    private Button _resolveDependencyButton = null!;

    public MainForm()
    {
        Text = "Isla Tortuga Content Pack Importer";
        Width = 1180;
        Height = 820;
        MinimumSize = new Size(980, 700);
        StartPosition = FormStartPosition.CenterScreen;

        BuildLayout();
        BindEvents();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        Controls.Add(root);

        root.Controls.Add(BuildHeaderPanel(), 0, 0);
        root.Controls.Add(BuildConfigurationPanel(), 0, 1);
        root.Controls.Add(BuildDependenciesPanel(), 0, 2);
        root.Controls.Add(BuildActionPanel(), 0, 3);
        root.Controls.Add(BuildLogPanel(), 0, 4);
    }

    private Control BuildHeaderPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 84,
            Padding = new Padding(8, 0, 8, 8),
        };

        var title = new Label
        {
            Text = "Importer de mapas y assets a content-packs",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 34,
        };

        var subtitle = new Label
        {
            Text = "Selecciona un .tmj exportado por Tiled, resuelve dependencias faltantes y exporta el pack runtime actualizado.",
            Font = new Font("Segoe UI", 10),
            Dock = DockStyle.Top,
            Height = 40,
        };

        panel.Controls.Add(subtitle);
        panel.Controls.Add(title);
        return panel;
    }

    private Control BuildConfigurationPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            Padding = new Padding(0, 0, 0, 8),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        _mapPathTextBox = new TextBox { Dock = DockStyle.Fill };
        _contentPathTextBox = new TextBox { Dock = DockStyle.Fill };
        _versionTextBox = new TextBox { Dock = DockStyle.Fill, Text = "v001" };
        _contentPackIdTextBox = new TextBox { Dock = DockStyle.Fill, Text = "islatortuga-v001" };
        _mapIdTextBox = new TextBox { Dock = DockStyle.Fill };
        _setDefaultPackCheckBox = new CheckBox
        {
            Text = "Marcar como pack por defecto",
            Dock = DockStyle.Fill,
            Checked = true,
        };

        var browseMapButton = new Button { Text = "Buscar mapa...", Dock = DockStyle.Fill, Height = 30 };
        browseMapButton.Click += (_, _) => SelectMapFile();

        var browseContentButton = new Button { Text = "Elegir content...", Dock = DockStyle.Fill, Height = 30 };
        browseContentButton.Click += (_, _) => SelectContentRoot();

        _analyzeButton = new Button
        {
            Text = "Analizar dependencias",
            Dock = DockStyle.Fill,
            Height = 34,
        };

        AddRow(panel, 0, "Mapa TMJ", _mapPathTextBox, browseMapButton, null);
        AddRow(panel, 1, "Carpeta content-packs", _contentPathTextBox, browseContentButton, null);
        AddRow(panel, 2, "Version", _versionTextBox, CreateLabel("ContentPackId"), _contentPackIdTextBox);
        AddRow(panel, 3, "MapId", _mapIdTextBox, null, null);
        AddRow(panel, 4, "Opciones", _setDefaultPackCheckBox, _analyzeButton, null);

        return panel;
    }

    private Control BuildDependenciesPanel()
    {
        var container = new GroupBox
        {
            Text = "Dependencias detectadas",
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        container.Controls.Add(layout);

        _dependencyGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            DataSource = _dependencyRows,
        };

        _dependencyGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DependencyRow.Status),
            HeaderText = "Estado",
            Width = 90,
        });
        _dependencyGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DependencyRow.Kind),
            HeaderText = "Tipo",
            Width = 120,
        });
        _dependencyGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DependencyRow.DisplayName),
            HeaderText = "Nombre",
            Width = 180,
        });
        _dependencyGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DependencyRow.Reference),
            HeaderText = "Referencia en mapa/tsx",
            Width = 260,
        });
        _dependencyGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(DependencyRow.ResolvedPath),
            HeaderText = "Ruta resuelta",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        });

        _resolveDependencyButton = new Button
        {
            Text = "Resolver dependencia seleccionada...",
            Dock = DockStyle.Right,
            Height = 34,
            Width = 280,
        };

        layout.Controls.Add(_dependencyGrid, 0, 0);
        layout.Controls.Add(_resolveDependencyButton, 0, 1);

        return container;
    }

    private Control BuildActionPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48,
            Padding = new Padding(0, 8, 0, 8),
        };

        _exportButton = new Button
        {
            Text = "Exportar al content pack",
            Width = 220,
            Height = 34,
        };

        var clearOverridesButton = new Button
        {
            Text = "Limpiar resoluciones manuales",
            Width = 220,
            Height = 34,
        };
        clearOverridesButton.Click += (_, _) =>
        {
            _dependencyOverrides.Clear();
            AppendLog("Se han limpiado las rutas manuales. Reanaliza el mapa para volver al estado base.");
        };

        panel.Controls.Add(_exportButton);
        panel.Controls.Add(clearOverridesButton);
        return panel;
    }

    private Control BuildLogPanel()
    {
        var container = new GroupBox
        {
            Text = "Log",
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
        };

        _logTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Font = new Font("Consolas", 10),
        };

        container.Controls.Add(_logTextBox);
        return container;
    }

    private void BindEvents()
    {
        _analyzeButton.Click += (_, _) => AnalyzeMap();
        _resolveDependencyButton.Click += (_, _) => ResolveSelectedDependency();
        _exportButton.Click += (_, _) => ExportContentPack();
    }

    private void SelectMapFile()
    {
        try
        {
            AppendLog("Abriendo selector de mapa...");

            using var dialog = new OpenFileDialog
            {
                Filter = "Tiled map (*.tmj)|*.tmj|Todos los archivos (*.*)|*.*",
                Title = "Selecciona un mapa exportado por Tiled",
                CheckFileExists = true,
                CheckPathExists = true,
                Multiselect = false,
                RestoreDirectory = true,
                AutoUpgradeEnabled = true,
                DereferenceLinks = true,
            };

            var suggestedDirectory = ResolveSuggestedMapsDirectory();
            if (!string.IsNullOrWhiteSpace(suggestedDirectory) && Directory.Exists(suggestedDirectory))
            {
                dialog.InitialDirectory = suggestedDirectory;
            }

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                AppendLog("Selector de mapa cancelado.");
                return;
            }

            _mapPathTextBox.Text = dialog.FileName;
            if (string.IsNullOrWhiteSpace(_mapIdTextBox.Text))
            {
                _mapIdTextBox.Text = SlugUtility.ToSlug(Path.GetFileNameWithoutExtension(dialog.FileName));
            }

            AppendLog($"Mapa seleccionado: {dialog.FileName}");
        }
        catch (Exception error)
        {
            ShowError($"No se ha podido abrir el selector de mapa: {error.Message}");
        }
    }

    private void SelectContentRoot()
    {
        try
        {
            AppendLog("Abriendo selector de carpeta content...");

            using var dialog = new FolderBrowserDialog
            {
                Description = "Selecciona la carpeta content-packs del proyecto",
                UseDescriptionForTitle = true,
            };

            var suggestedDirectory = ResolveSuggestedContentDirectory();
            if (!string.IsNullOrWhiteSpace(suggestedDirectory) && Directory.Exists(suggestedDirectory))
            {
                dialog.SelectedPath = suggestedDirectory;
            }

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                AppendLog("Selector de content cancelado.");
                return;
            }

            var selection = _importService.NormalizeContentSelection(dialog.SelectedPath);
            _contentPathTextBox.Text = selection.ContentPacksRootPath;
            if (!string.IsNullOrWhiteSpace(selection.SuggestedVersion))
            {
                _versionTextBox.Text = selection.SuggestedVersion;
                if (string.IsNullOrWhiteSpace(_contentPackIdTextBox.Text) ||
                    _contentPackIdTextBox.Text.StartsWith("islatortuga-v", StringComparison.OrdinalIgnoreCase))
                {
                    _contentPackIdTextBox.Text = $"islatortuga-{selection.SuggestedVersion}";
                }
            }

            AppendLog($"Carpeta content seleccionada: {selection.ContentPacksRootPath}");
        }
        catch (Exception error)
        {
            ShowError($"No se ha podido abrir el selector de carpeta content: {error.Message}");
        }
    }

    private void AnalyzeMap()
    {
        try
        {
            var mapPath = RequireValue(_mapPathTextBox.Text, "Selecciona primero un mapa TMJ.");
            _currentScan = _importService.Scan(mapPath, _dependencyOverrides);

            if (string.IsNullOrWhiteSpace(_mapIdTextBox.Text))
            {
                _mapIdTextBox.Text = _currentScan.SuggestedMapId;
            }

            _dependencyRows.Clear();
            foreach (var dependency in _currentScan.Dependencies)
            {
                _dependencyRows.Add(new DependencyRow
                {
                    Key = dependency.Key,
                    Status = dependency.IsMissing ? "Falta" : "OK",
                    Kind = dependency.Kind,
                    DisplayName = dependency.DisplayName,
                    Reference = dependency.Reference,
                    ResolvedPath = dependency.ResolvedPath ?? string.Empty,
                });
            }

            AppendLog($"Analisis completado. Tilesets resueltos: {_currentScan.ResolvedTilesets.Count}. Dependencias totales: {_currentScan.Dependencies.Count}.");
            if (_currentScan.HasMissingDependencies)
            {
                AppendLog("Hay dependencias faltantes. Seleccionalas en la tabla y usa 'Resolver dependencia...' para ubicarlas manualmente.");
            }
        }
        catch (Exception error)
        {
            ShowError(error.Message);
        }
    }

    private void ResolveSelectedDependency()
    {
        if (_dependencyGrid.CurrentRow?.DataBoundItem is not DependencyRow row)
        {
            ShowError("Selecciona una dependencia en la tabla antes de resolverla.");
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = $"Selecciona el archivo para {row.DisplayName}",
            Filter = "Todos los archivos (*.*)|*.*",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false,
            RestoreDirectory = true,
        };

        try
        {
            AppendLog($"Abriendo selector para dependencia: {row.DisplayName}");

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                AppendLog("Selector de dependencia cancelado.");
                return;
            }

            _dependencyOverrides[row.Key] = dialog.FileName;
            AppendLog($"Ruta manual asociada a {row.DisplayName}: {dialog.FileName}");
            AnalyzeMap();
        }
        catch (Exception error)
        {
            ShowError($"No se ha podido abrir el selector para la dependencia: {error.Message}");
        }
    }

    private void ExportContentPack()
    {
        try
        {
            if (_currentScan is null)
            {
                throw new InvalidOperationException("Analiza primero el mapa antes de exportar.");
            }

            var request = new ImportRequest
            {
                SourceMapPath = RequireValue(_mapPathTextBox.Text, "Falta la ruta del mapa."),
                ContentPacksRootPath = RequireValue(_contentPathTextBox.Text, "Selecciona la carpeta content-packs."),
                Version = RequireValue(_versionTextBox.Text, "Indica una version de content pack, por ejemplo v001."),
                ContentPackId = RequireValue(_contentPackIdTextBox.Text, "Indica un contentPackId."),
                MapId = RequireValue(_mapIdTextBox.Text, "Indica un mapId."),
                SetAsDefaultPack = _setDefaultPackCheckBox.Checked,
                DependencyOverrides = new Dictionary<string, string>(_dependencyOverrides, StringComparer.OrdinalIgnoreCase),
            };

            var result = _importService.Import(request);
            AppendLog($"Exportacion completada. Mapa runtime: {result.MapOutputPath}");
            AppendLog($"Manifest actualizado: {result.ManifestPath}");

            foreach (var copiedFile in result.CopiedFiles)
            {
                AppendLog($"  - {copiedFile}");
            }

            MessageBox.Show(
                this,
                "El content pack se ha actualizado correctamente.",
                "Importacion completada",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception error)
        {
            ShowError(error.Message);
        }
    }

    private void AppendLog(string message)
    {
        _logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void ShowError(string message)
    {
        AppendLog($"ERROR: {message}");
        MessageBox.Show(this, message, "Content Tool", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static string RequireValue(string? value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return value.Trim();
    }

    private static void AddRow(
        TableLayoutPanel panel,
        int rowIndex,
        string label,
        Control primaryControl,
        Control? secondaryControl,
        Control? tertiaryControl)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(CreateLabel(label), 0, rowIndex);
        panel.Controls.Add(primaryControl, 1, rowIndex);

        if (secondaryControl is not null)
        {
            panel.Controls.Add(secondaryControl, 2, rowIndex);
        }

        if (tertiaryControl is not null)
        {
            panel.Controls.Add(tertiaryControl, 3, rowIndex);
        }
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0),
        };
    }

    private string? ResolveSuggestedMapsDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_mapPathTextBox.Text))
        {
            var currentDirectory = Path.GetDirectoryName(_mapPathTextBox.Text);
            if (!string.IsNullOrWhiteSpace(currentDirectory))
            {
                return currentDirectory;
            }
        }

        var projectRoot = AppContext.BaseDirectory;
        var current = new DirectoryInfo(projectRoot);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "assets", "maps");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }

    private string? ResolveSuggestedContentDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_contentPathTextBox.Text) && Directory.Exists(_contentPathTextBox.Text))
        {
            return _contentPathTextBox.Text;
        }

        var projectRoot = AppContext.BaseDirectory;
        var current = new DirectoryInfo(projectRoot);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "content-packs");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }

    private sealed class DependencyRow
    {
        public required string Key { get; init; }

        public required string Status { get; init; }

        public required string Kind { get; init; }

        public required string DisplayName { get; init; }

        public required string Reference { get; init; }

        public required string ResolvedPath { get; init; }
    }
}
