using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using JiraToRea.App.Models;
using JiraToRea.App.Services;

namespace JiraToRea.App;

public sealed class MainForm : Form
{
    private readonly ReaApiClient _reaClient = new();
    private readonly JiraApiClient _jiraClient = new();
    private readonly UserSettingsService _settingsService = new();
    private readonly ImportLogger _importLogger = new();
    private readonly BindingList<WorklogEntryViewModel> _worklogEntries = new();
    private readonly BindingList<ReaProject> _reaProjects = new();
    private readonly Dictionary<DateRangeKey, List<ReaTimeEntry>> _reaTimeEntryCache = new();

    private UserSettings _userSettings = new();

    private readonly TextBox _reaUsernameTextBox;
    private readonly TextBox _reaPasswordTextBox;
    private readonly TextBox _reaUserIdTextBox;
    private readonly ComboBox _reaProjectComboBox;
    private readonly Button _reaLoginButton;
    private readonly Button _reaLogoutButton;

    private readonly TextBox _jiraEmailTextBox;
    private readonly TextBox _jiraTokenTextBox;
    private readonly Button _jiraLoginButton;
    private readonly Button _jiraLogoutButton;

    private readonly DateTimePicker _startDatePicker;
    private readonly DateTimePicker _startTimePicker;
    private readonly DateTimePicker _endDatePicker;
    private readonly DateTimePicker _endTimePicker;
    private readonly Button _findButton;
    private readonly Button _statisticsButton;
    private readonly Button _importSelectedButton;
    private readonly Button _importAllButton;
    private readonly Button _cancelAllButton;
    private readonly DataGridView _worklogGrid;
    private readonly Label _selectionLabel;
    private readonly Label _statusLabel;
    private readonly Label _footerLabel;
    private readonly FlowLayoutPanel _statusActionPanel;
    private readonly Panel _footerContainer;

    public MainForm()
    {
        _userSettings = _settingsService.Load();

        Text = "Jira To Rea Portal";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1230, 650);
        MinimumSize = new Size(1230, 650);
        MaximumSize = new Size(1230, 650);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            ColumnCount = 2,
            RowCount = 1
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        Controls.Add(mainLayout);

        _worklogEntries.ListChanged += (_, _) => UpdateStatisticsButtonState();

        var reaGroup = new GroupBox
        {
            Text = "Rea Portal",
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 10)
        };

        var reaUsername = string.IsNullOrWhiteSpace(_userSettings.ReaUsername)
            ? "mehmet.durmaz"
            : _userSettings.ReaUsername;

        _reaUsernameTextBox = CreateTextBox(reaUsername);
        _reaPasswordTextBox = CreatePasswordTextBox();
        _reaPasswordTextBox.Text = _userSettings.ReaPassword;
        _reaUserIdTextBox = CreateTextBox(string.Empty);
        _reaUserIdTextBox.ReadOnly = true;
        _reaUserIdTextBox.TabStop = false;

        _reaProjectComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DataSource = _reaProjects,
            DisplayMember = nameof(ReaProject.DisplayName),
            ValueMember = nameof(ReaProject.Id)
        };
        _reaProjectComboBox.SelectedIndexChanged += ReaProjectComboBox_SelectedIndexChanged;
        _reaLoginButton = CreateButton("Login", ReaLoginButton_Click);
        _reaLogoutButton = CreateButton("Logout", ReaLogoutButton_Click);
        _reaLogoutButton.Enabled = false;

        var reaLayout = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 5,
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        reaLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        reaLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        reaLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        reaLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        reaLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        reaLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        reaLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        reaLayout.Controls.Add(new Label { Text = "Username", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        reaLayout.Controls.Add(_reaUsernameTextBox, 1, 0);
        reaLayout.Controls.Add(new Label { Text = "Password", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        reaLayout.Controls.Add(_reaPasswordTextBox, 1, 1);
        reaLayout.Controls.Add(new Label { Text = "User ID", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
        reaLayout.Controls.Add(_reaUserIdTextBox, 1, 2);
        reaLayout.Controls.Add(new Label { Text = "Project", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 3);
        reaLayout.Controls.Add(_reaProjectComboBox, 1, 3);

        var reaButtonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Fill
        };
        reaButtonPanel.Controls.Add(_reaLoginButton);
        reaButtonPanel.Controls.Add(_reaLogoutButton);
        reaLayout.Controls.Add(reaButtonPanel, 0, 4);
        reaLayout.SetColumnSpan(reaButtonPanel, 2);

        reaGroup.Controls.Add(reaLayout);

        var jiraGroup = new GroupBox
        {
            Text = "Jira",
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10),
            Margin = new Padding(0)
        };

        _jiraEmailTextBox = CreateTextBox(_userSettings.JiraEmail);
        _jiraTokenTextBox = CreatePasswordTextBox();
        _jiraTokenTextBox.Text = _userSettings.JiraToken;
        _jiraTokenTextBox.UseSystemPasswordChar = true;
        _jiraLoginButton = CreateButton("Login", JiraLoginButton_Click);
        _jiraLogoutButton = CreateButton("Logout", JiraLogoutButton_Click);
        _jiraLogoutButton.Enabled = false;

        var jiraLayout = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 4,
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        jiraLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        jiraLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        jiraLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        jiraLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        jiraLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        jiraLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        jiraLayout.Controls.Add(new Label { Text = "Email", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        jiraLayout.Controls.Add(_jiraEmailTextBox, 1, 0);
        jiraLayout.Controls.Add(new Label { Text = "API Token", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        jiraLayout.Controls.Add(_jiraTokenTextBox, 1, 1);

        var jiraButtonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Fill
        };
        jiraButtonPanel.Controls.Add(_jiraLoginButton);
        jiraButtonPanel.Controls.Add(_jiraLogoutButton);
        jiraLayout.Controls.Add(jiraButtonPanel, 0, 2);
        jiraLayout.SetColumnSpan(jiraButtonPanel, 2);

        var jiraInfoLabel = new Label
        {
            Text = "Jira API token Atlassian hesabınızdan oluşturulmalıdır.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        jiraLayout.Controls.Add(jiraInfoLabel, 0, 3);
        jiraLayout.SetColumnSpan(jiraInfoLabel, 2);

        jiraGroup.Controls.Add(jiraLayout);

        var leftPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0)
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        leftPanel.Controls.Add(reaGroup, 0, 0);
        leftPanel.Controls.Add(jiraGroup, 0, 1);
        leftPanel.SetColumnSpan(reaGroup, 1);
        leftPanel.SetColumnSpan(jiraGroup, 1);

        mainLayout.Controls.Add(leftPanel, 0, 0);

        leftPanel.Margin = new Padding(0, 0, 10, 0);

        _startDatePicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Width = 120
        };
        _startTimePicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH:mm",
            ShowUpDown = true,
            Width = 80
        };
        _endDatePicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Width = 120
        };
        _endTimePicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH:mm",
            ShowUpDown = true,
            Width = 80
        };


        var startFilterPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false
        };
        startFilterPanel.Controls.Add(_startDatePicker);
        startFilterPanel.Controls.Add(_startTimePicker);
        startFilterPanel.Margin = new Padding(0, 0, 20, 0);

        var endFilterPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false
        };
        endFilterPanel.Controls.Add(_endDatePicker);
        endFilterPanel.Controls.Add(_endTimePicker);
        endFilterPanel.Margin = new Padding(0, 0, 20, 0);

        _findButton = CreateButton("Find", FindButton_Click);

        _statisticsButton = CreateButton("Meraklısına İstatistik", StatisticsButton_Click);
        _statisticsButton.Enabled = false;

        var filterPanel = new TableLayoutPanel
        {
            ColumnCount = 6,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var startLabel = new Label { Text = "Start Date & Time", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 10, 0) };
        var endLabel = new Label { Text = "End Date & Time", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 10, 0) };

        filterPanel.Controls.Add(startLabel, 0, 0);
        filterPanel.Controls.Add(startFilterPanel, 1, 0);
        filterPanel.Controls.Add(endLabel, 2, 0);
        filterPanel.Controls.Add(endFilterPanel, 3, 0);
        filterPanel.Controls.Add(_findButton, 4, 0);
        filterPanel.Controls.Add(_statisticsButton, 5, 0);

        _findButton.Margin = new Padding(0, 0, 10, 0);
        _statisticsButton.Margin = new Padding(0);

        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        mainLayout.Controls.Add(rightPanel, 1, 0);

        rightPanel.Controls.Add(filterPanel, 0, 0);
        filterPanel.Margin = new Padding(0, 0, 0, 10);

        _worklogGrid = new DataGridView
        {
            AutoGenerateColumns = false,
            Dock = DockStyle.Fill,
            DataSource = _worklogEntries,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            MultiSelect = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ReadOnly = false,
            RowHeadersVisible = false,
            EditMode = DataGridViewEditMode.EditOnEnter
        };

        _worklogGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(WorklogEntryViewModel.IssueKey),
            HeaderText = "Key",
            ReadOnly = true,
            Width = 80
        });

        _worklogGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(WorklogEntryViewModel.Task),
            HeaderText = "Task",
            Width = 220
        });

        _worklogGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(WorklogEntryViewModel.StartDate),
            HeaderText = "Start",
            DefaultCellStyle = new DataGridViewCellStyle { Format = "g" },
            ReadOnly = true,
            Width = 120
        });

        _worklogGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(WorklogEntryViewModel.EndDate),
            HeaderText = "End",
            DefaultCellStyle = new DataGridViewCellStyle { Format = "g" },
            ReadOnly = true,
            Width = 120
        });

        _worklogGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(WorklogEntryViewModel.EffortHours),
            HeaderText = "Hours",
            DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" },
            Width = 80
        });

        _worklogGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(WorklogEntryViewModel.Comment),
            HeaderText = "Comment",
            Width = 250
        });

        _worklogGrid.SelectionChanged += (_, _) => UpdateSelectionInfo();

        rightPanel.Controls.Add(_worklogGrid, 0, 1);

        _importSelectedButton = CreateButton("Import Selected", ImportSelectedButton_Click);
        _importSelectedButton.Enabled = false;
        _importAllButton = CreateButton("Import All", ImportAllButton_Click);
        _importAllButton.Enabled = false;

        var importPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 10, 0, 0)
        };

        importPanel.Controls.Add(_importSelectedButton);
        importPanel.Controls.Add(_importAllButton);

        _selectionLabel = new Label
        {
            Text = "Selected rows count: 0",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(10, 6, 0, 0)
        };

        importPanel.Controls.Add(_selectionLabel);

        rightPanel.Controls.Add(importPanel, 0, 2);

        _cancelAllButton = CreateButton("X", CancelAndLogoutButton_Click);
        _cancelAllButton.Margin = new Padding(10, 0, 0, 0);

        var actionLabel = new Label
        {
            Text = "Aksiyonlar:",
            AutoSize = true,
            ForeColor = Color.FromArgb(178, 34, 34),
            Font = new Font(Font, FontStyle.Bold),
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.Left
        };

        var actionPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.FromArgb(255, 235, 238),
            Margin = new Padding(0, 0, 0, 6),
            Padding = new Padding(8, 4, 8, 4)
        };

        actionPanel.Controls.Add(actionLabel);
        actionPanel.Controls.Add(_cancelAllButton);

        _statusLabel = new Label
        {
            Text = "Hazır",
            AutoSize = true,
            ForeColor = Color.FromArgb(178, 34, 34),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 4, 8, 4),
            MaximumSize = new Size(420, 0)
        };

        _footerLabel = new Label
        {
            Text = "(c) 2024 emre incekara, 2025 mehmet durmaz",
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 100, 100),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 12, 0)
        };

        _cancelAllButton = CreateButton("X", CancelAndLogoutButton_Click);
        _cancelAllButton.Margin = new Padding(8, 0, 0, 0);

        var statusActionPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.FromArgb(255, 235, 238),
            Margin = new Padding(0),
            Padding = new Padding(10, 6, 10, 6),
            Anchor = AnchorStyles.Right
        };

        statusActionPanel.Controls.Add(_statusLabel);
        statusActionPanel.Controls.Add(_cancelAllButton);

        var footerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Margin = new Padding(0, 10, 0, 0)
        };

        footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footerPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        footerPanel.Controls.Add(_footerLabel, 0, 0);
        footerPanel.Controls.Add(statusActionPanel, 1, 0);

        rightPanel.Controls.Add(footerPanel, 0, 3);

        SetDefaultDateRange();

        UpdateStatisticsButtonState();
    }

    private void SetDefaultDateRange()
    {
        var today = DateTime.Today;
        var daysSinceMonday = (7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7;

        var thisWeekMonday = today.AddDays(-daysSinceMonday);
        var lastWeekMonday = thisWeekMonday.AddDays(-7);
        var lastWeekSunday = lastWeekMonday.AddDays(6);

        _startDatePicker.Value = lastWeekMonday;
        _startTimePicker.Value = lastWeekMonday.Date;
        _endDatePicker.Value = lastWeekSunday;
        _endTimePicker.Value = lastWeekSunday.AddHours(8);
    }


    private void ReaProjectComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        UpdateImportButtonState();

        if (_reaProjectComboBox.SelectedItem is ReaProject project)
        {
            _userSettings.ReaProjectId = project.Id;
        }
    }

    private void ApplySavedReaProjectSelection()
    {
        if (_reaProjects.Count == 0)
        {
            _reaProjectComboBox.SelectedIndex = -1;
            return;
        }

        var savedProjectId = _userSettings.ReaProjectId;
        if (!string.IsNullOrWhiteSpace(savedProjectId))
        {
            for (var i = 0; i < _reaProjects.Count; i++)
            {
                if (string.Equals(_reaProjects[i].Id, savedProjectId, StringComparison.OrdinalIgnoreCase))
                {
                    _reaProjectComboBox.SelectedIndex = i;
                    return;
                }
            }
        }

        _reaProjectComboBox.SelectedIndex = 0;
    }

    private static TextBox CreateTextBox(string text)
    {
        return new TextBox
        {
            Dock = DockStyle.Fill,
            Text = text
        };
    }

    private static TextBox CreatePasswordTextBox()
    {
        return new TextBox
        {
            Dock = DockStyle.Fill,
            UseSystemPasswordChar = true
        };
    }

    private static Button CreateButton(string text, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Margin = new Padding(0, 0, 10, 0)
        };
        button.Click += onClick;
        return button;
    }

    private async void ReaLoginButton_Click(object? sender, EventArgs e)
    {
        var username = _reaUsernameTextBox.Text.Trim();
        var password = _reaPasswordTextBox.Text;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show(this, "Rea kullanıcı adı ve şifre zorunludur.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _reaLoginButton.Enabled = false;
        UseWaitCursor = true;
        try
        {
            AnnounceEndpointCall("Rea portal", "api/Auth/Login", "giriş yapılıyor");
            await _reaClient.LoginAsync(username, password).ConfigureAwait(true);
            _reaLogoutButton.Enabled = true;
            SetStatus($"Rea portal giriş başarılı. ({username})");
            _reaTimeEntryCache.Clear();
            await RefreshReaMetadataAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Rea Portal", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _reaLoginButton.Enabled = true;
            SetStatus("Rea portal giriş başarısız.");
        }
        finally
        {
            UseWaitCursor = false;
            UpdateImportButtonState();
        }
    }

    private void ReaLogoutButton_Click(object? sender, EventArgs e) 
    {
        _reaClient.Logout();
        _reaTimeEntryCache.Clear();
        _reaLoginButton.Enabled = true;
        _reaLogoutButton.Enabled = false;
        _reaUserIdTextBox.Clear();
        _reaProjects.Clear();
        _reaProjectComboBox.SelectedIndex = -1;
        SetStatus("Rea portal oturumu kapatıldı.");
        UpdateImportButtonState();
    }

    private async void JiraLoginButton_Click(object? sender, EventArgs e)
    {
        var email = _jiraEmailTextBox.Text.Trim();
        var token = _jiraTokenTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            MessageBox.Show(this, "Jira e-posta ve API token zorunludur.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _jiraLoginButton.Enabled = false;
        UseWaitCursor = true;
        try
        {
            AnnounceEndpointCall("Jira", "rest/api/3/myself", "kullanıcı doğrulaması yapılıyor");
            await _jiraClient.LoginAsync(email, token).ConfigureAwait(true);
            _jiraLogoutButton.Enabled = true;
            SetStatus($"Jira girişi başarılı. {_jiraClient.DisplayName}");

            if (_reaClient.IsAuthenticated)
            {
                await RefreshReaMetadataAsync().ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Jira", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _jiraLoginButton.Enabled = true;
            SetStatus("Jira girişi başarısız.");
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void JiraLogoutButton_Click(object? sender, EventArgs e)
    {
        _jiraClient.Logout();
        _jiraLoginButton.Enabled = true;
        _jiraLogoutButton.Enabled = false;
        _worklogEntries.Clear();
        SetStatus("Jira oturumu kapatıldı.");
        UpdateSelectionInfo();
    }

    private async void FindButton_Click(object? sender, EventArgs e)
    {
        if (!_jiraClient.IsAuthenticated)
        {
            MessageBox.Show(this, "Önce Jira'ya giriş yapın.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _findButton.Enabled = false;
        UseWaitCursor = true;
        try
        {
            if (_reaClient.IsAuthenticated)
            {
                await RefreshExistingReaEntriesForCurrentRangeAsync().ConfigureAwait(true);
            }

            var startDate = _startDatePicker.Value.Date + _startTimePicker.Value.TimeOfDay;
            var endDate = _endDatePicker.Value.Date + _endTimePicker.Value.TimeOfDay;
            AnnounceEndpointCall("Jira", "rest/api/3/search/jql", "worklog araması yapılıyor");
            var worklogs = await _jiraClient.GetWorklogsAsync(startDate, endDate).ConfigureAwait(true);

            _worklogEntries.Clear();
            foreach (var entry in worklogs.OrderBy(w => w.StartDate))
            {
                _worklogEntries.Add(entry);
            }

            _worklogGrid.ClearSelection();
            UpdateSelectionInfo();
            SetStatus($"{worklogs.Count} adet worklog yüklendi.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Worklog", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("Worklog listesi alınamadı.");
        }
        finally
        {
            UseWaitCursor = false;
            _findButton.Enabled = true;
            UpdateSelectionInfo();
        }
    }

    private async void ImportSelectedButton_Click(object? sender, EventArgs e)
    {
        var selectedEntries = _worklogGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(row => row.DataBoundItem as WorklogEntryViewModel)
            .Where(item => item != null)
            .Cast<WorklogEntryViewModel>()
            .ToList();

        if (selectedEntries.Count == 0)
        {
            MessageBox.Show(this, "Aktarmak için en az bir kayıt seçmelisiniz.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await ImportEntriesAsync(selectedEntries).ConfigureAwait(true);
    }

    private async void ImportAllButton_Click(object? sender, EventArgs e)
    {
        if (_worklogEntries.Count == 0)
        {
            MessageBox.Show(this, "Aktarılacak kayıt bulunamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await ImportEntriesAsync(_worklogEntries.ToList()).ConfigureAwait(true);
    }

    private async Task ImportEntriesAsync(IReadOnlyCollection<WorklogEntryViewModel> entries)
    {
        var userId = _reaUserIdTextBox.Text.Trim();
        var projectId = _reaProjectComboBox.SelectedValue as string ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(projectId))
        {
            MessageBox.Show(this, "Rea kullanıcı ID ve proje seçimi zorunludur.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _importSelectedButton.Enabled = false;
        _importAllButton.Enabled = false;
        UseWaitCursor = true;
        try
        {
            var rangeKey = GetCurrentDateRangeKey();
            var existingEntries = await EnsureReaEntriesCachedAsync(rangeKey, userId, forceRefresh: true).ConfigureAwait(true);

            var sentCount = 0;
            var skippedCount = 0;

            foreach (var entry in entries)
            {
                if (IsDuplicateWithExistingEntry(entry, existingEntries, userId, projectId))
                {
                    skippedCount++;
                    continue;
                }

                var timeEntry = new ReaTimeEntry
                {
                    Id = 0,
                    UserId = userId,
                    ProjectId = projectId,
                    Task = entry.Task,
                    StartDate = entry.StartDate.Date,
                    EndDate = entry.EndDate.Date,
                    Effort = entry.EffortHours,
                    Comment = entry.Comment
                };

                AnnounceEndpointCall("Rea portal", "api/TimeSheet/Create", "kayıt oluşturuluyor");
                await _reaClient.CreateTimeEntryAsync(timeEntry).ConfigureAwait(true);
                sentCount++;
                existingEntries.Add(ConvertToCachedEntry(entry, userId, projectId));
            }

            if (skippedCount > 0)
            {
                MessageBox.Show(this, $"{skippedCount} kayıt Rea portalında bulunduğu için gönderilmedi.", "Rea Portal", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            _importLogger.LogImport(userId, projectId, entries, success: true);
            SetStatus($"{entries.Count} kayıt Rea portalına gönderildi.");
        }
        catch (Exception ex)
        {
            _importLogger.LogImport(userId, projectId, entries, success: false, errorMessage: ex.Message);
            MessageBox.Show(this, ex.Message, "Rea Portal", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("Rea portal aktarımı sırasında hata oluştu.");
        }
        finally
        {
            UseWaitCursor = false;
            UpdateImportButtonState();
            UpdateSelectionInfo();
        }
    }

    private void CancelAndLogoutButton_Click(object? sender, EventArgs e)
    {
        UseWaitCursor = false;
        Cursor = Cursors.Default;
        LogoutFromAllServices();
        _findButton.Enabled = true;
        SetStatus("İşlem iptal edildi. Tüm oturumlar kapatıldı.");
    }

    private void LogoutFromAllServices()
    {
        if (_reaClient.IsAuthenticated)
        {
            _reaClient.Logout();
            _reaLoginButton.Enabled = true;
            _reaLogoutButton.Enabled = false;
            _reaUserIdTextBox.Clear();
            _reaProjects.Clear();
            _reaProjectComboBox.SelectedIndex = -1;
        }

        if (_jiraClient.IsAuthenticated)
        {
            _jiraClient.Logout();
            _jiraLoginButton.Enabled = true;
            _jiraLogoutButton.Enabled = false;
            _worklogEntries.Clear();
        }

        UpdateImportButtonState();
        UpdateSelectionInfo();
    }

    private async Task RefreshExistingReaEntriesForCurrentRangeAsync(bool forceRefresh = false)
    {
        if (!_reaClient.IsAuthenticated)
        {
            return;
        }

        var userId = _reaUserIdTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var rangeKey = GetCurrentDateRangeKey();
        _ = await EnsureReaEntriesCachedAsync(rangeKey, userId, forceRefresh).ConfigureAwait(true);
    }

    private async Task<List<ReaTimeEntry>> EnsureReaEntriesCachedAsync(DateRangeKey rangeKey, string userId, bool forceRefresh = false)
    {
        if (!forceRefresh && _reaTimeEntryCache.TryGetValue(rangeKey, out var cachedEntries))
        {
            return cachedEntries;
        }

        if (string.IsNullOrWhiteSpace(userId) || !_reaClient.IsAuthenticated)
        {
            var emptyEntries = new List<ReaTimeEntry>();
            _reaTimeEntryCache[rangeKey] = emptyEntries;
            return emptyEntries;
        }

        try
        {
            AnnounceEndpointCall("Rea portal", "api/TimeSheet/GetByUserId", "mevcut kayıtlar sorgulanıyor");
            var reaEntries = await _reaClient.GetTimeEntriesAsync(userId).ConfigureAwait(true);
            var filtered = reaEntries
                .Where(entry => entry is not null)
                .Select(entry => entry!)
                .Where(entry => entry.StartDate.Date <= rangeKey.End && entry.EndDate.Date >= rangeKey.Start)
                .ToList();

            _reaTimeEntryCache[rangeKey] = filtered;
            return filtered;
        }
        catch (Exception)
        {
            SetStatus("Rea portal kayıtları alınırken hata oluştu.");
            _reaTimeEntryCache.Remove(rangeKey);
            return new List<ReaTimeEntry>();
        }
    }

    private DateRangeKey GetCurrentDateRangeKey()
    {
        var start = _startDatePicker.Value.Date + _startTimePicker.Value.TimeOfDay;
        var end = _endDatePicker.Value.Date + _endTimePicker.Value.TimeOfDay;
        return DateRangeKey.From(start, end);
    }

    private static bool IsDuplicateWithExistingEntry(WorklogEntryViewModel entry, IEnumerable<ReaTimeEntry> existingEntries, string userId, string projectId)
    {
        foreach (var existing in existingEntries)
        {
            if (existing is null)
            {
                continue;
            }

            if (!StringsEqual(existing.UserId, userId))
            {
                continue;
            }

            if (!StringsEqual(existing.ProjectId, projectId))
            {
                continue;
            }

            if (!StringsEqual(existing.Task, entry.Task))
            {
                continue;
            }

            if (!StringsEqual(existing.Comment, entry.Comment))
            {
                continue;
            }

            if (existing.StartDate.Date != entry.StartDate.Date || existing.EndDate.Date != entry.EndDate.Date)
            {
                continue;
            }

            if (EffortEquals(existing.Effort, entry.EffortHours))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeForComparison(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static bool StringsEqual(string? left, string? right)
    {
        return string.Equals(NormalizeForComparison(left), NormalizeForComparison(right), StringComparison.OrdinalIgnoreCase);
    }

    private static bool EffortEquals(double left, double right)
    {
        return Math.Abs(left - right) <= 0.01;
    }

    private static ReaTimeEntry ConvertToCachedEntry(WorklogEntryViewModel entry, string userId, string projectId)
    {
        return new ReaTimeEntry
        {
            Id = 0,
            UserId = userId,
            ProjectId = projectId,
            Task = entry.Task,
            StartDate = entry.StartDate.Date,
            EndDate = entry.EndDate.Date,
            Effort = entry.EffortHours,
            Comment = entry.Comment
        };
    }

    private void UpdateImportStatus(int sentCount, int skippedCount)
    {
        if (sentCount > 0 && skippedCount > 0)
        {
            SetStatus($"{sentCount} kayıt Rea portalına gönderildi, {skippedCount} kayıt zaten mevcut olduğu için atlandı.");
        }
        else if (sentCount > 0)
        {
            SetStatus($"{sentCount} kayıt Rea portalına gönderildi.");
        }
        else if (skippedCount > 0)
        {
            SetStatus($"{skippedCount} kayıt Rea portalında bulunduğu için gönderilmedi.");
        }
        else
        {
            SetStatus("Gönderilecek kayıt bulunamadı.");
        }
    }

    private readonly record struct DateRangeKey(DateTime Start, DateTime End)
    {
        public static DateRangeKey From(DateTime start, DateTime end)
        {
            start = start.Date;
            end = end.Date;

            if (end < start)
            {
                (start, end) = (end, start);
            }

            return new DateRangeKey(start, end);
        }
    }

    private void UpdateSelectionInfo()
    {
        var count = _worklogGrid.SelectedRows.Count;
        _selectionLabel.Text = $"Selected rows count: {count}";
        UpdateImportButtonState();
    }

    private void UpdateImportButtonState()
    {
        var hasSelection = _worklogGrid.SelectedRows.Count > 0;
        var hasProject = _reaProjectComboBox.SelectedItem is ReaProject;
        var hasEntries = _worklogEntries.Count > 0;
        var canImport = _reaClient.IsAuthenticated && hasProject && hasEntries;
        _importAllButton.Enabled = canImport;
        _importSelectedButton.Enabled = _reaClient.IsAuthenticated && hasProject && hasSelection;
    }

    private void UpdateStatisticsButtonState()
    {
        _statisticsButton.Enabled = _worklogEntries.Count > 0;
    }

    private void AlignFooterElements()
    {
        if (_footerContainer is null)
        {
            return;
        }

        const int horizontalPadding = 4;
        const int verticalPadding = 4;

        var containerSize = _footerContainer.ClientSize;

        var statusX = Math.Max(horizontalPadding, containerSize.Width - _statusActionPanel.Width - horizontalPadding);
        var statusY = Math.Max(verticalPadding, containerSize.Height - _statusActionPanel.Height - verticalPadding);
        _statusActionPanel.Location = new Point(statusX, statusY);

        var footerY = Math.Max(verticalPadding, containerSize.Height - _footerLabel.Height - verticalPadding);
        _footerLabel.Location = new Point(horizontalPadding, footerY);
    }

    private void SetStatus(string message)
    {
        _statusLabel.Text = message;
        AlignFooterElements();
    }

    private void AnnounceEndpointCall(string source, string endpoint, string action)
    {
        SetStatus($"{source} endpoint çağrısı: {endpoint} -> {action}...");
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        var settings = new UserSettings
        {
            ReaUsername = _reaUsernameTextBox.Text,
            ReaPassword = _reaPasswordTextBox.Text,
            JiraEmail = _jiraEmailTextBox.Text,
            JiraToken = _jiraTokenTextBox.Text,
            ReaProjectId = (_reaProjectComboBox.SelectedItem as ReaProject)?.Id ?? _userSettings.ReaProjectId
        };

        _userSettings = settings;

        base.OnFormClosed(e);

        _settingsService.Save(settings);
        _reaClient.Dispose();
        _jiraClient.Dispose();
    }

    private async Task RefreshReaMetadataAsync()
    {
        try
        {
            AnnounceEndpointCall("Rea portal", "api/Auth/GetUserProfileInfo", "kullanıcı profili alınıyor");
            var profile = await _reaClient.GetUserProfileAsync().ConfigureAwait(true);
            _reaUserIdTextBox.Text = profile.UserId;

            AnnounceEndpointCall("Rea portal", "api/Project/GetAll", "proje listesi alınıyor");
            var projects = await _reaClient.GetProjectsAsync().ConfigureAwait(true);

            _reaProjects.Clear();
            foreach (var project in projects.OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                _reaProjects.Add(project);
            }

            if (_reaProjects.Count > 0)
            {
                ApplySavedReaProjectSelection();
            }
            else
            {
                SetStatus("Rea profilinde atanmış proje bulunamadı.");
            }

            await RefreshExistingReaEntriesForCurrentRangeAsync(forceRefresh: true).ConfigureAwait(true);
        }
        catch (Exception metadataEx)
        {
            MessageBox.Show(this, metadataEx.Message, "Rea Portal", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            SetStatus("Rea profil bilgileri alınamadı.");
        }
        finally
        {
            UpdateImportButtonState();
        }
    }

    private void StatisticsButton_Click(object? sender, EventArgs e)
    {
        if (_worklogEntries.Count == 0)
        {
            MessageBox.Show(this, "İstatistik oluşturmak için kayıt bulunmuyor.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var statisticsForm = new WorklogStatisticsForm(_worklogEntries.ToList());
        statisticsForm.ShowDialog(this);
    }
}
