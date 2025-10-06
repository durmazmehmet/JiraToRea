using System;
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
    private readonly BindingList<WorklogEntryViewModel> _worklogEntries = new();
    private readonly BindingList<ReaProject> _reaProjects = new();

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
    private readonly DateTimePicker _endDatePicker;
    private readonly Button _findButton;
    private readonly Button _importButton;
    private readonly Button _statisticsButton;
    private readonly DataGridView _worklogGrid;
    private readonly Label _selectionLabel;
    private readonly Label _statusLabel;
    private readonly Label _footerLabel;

    public MainForm()
    {
        _userSettings = _settingsService.Load();

        Text = "Jira To Rea Portal";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 650);

        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };

        Controls.Add(mainPanel);

        _worklogEntries.ListChanged += (_, _) => UpdateStatisticsButtonState();

        var reaGroup = new GroupBox
        {
            Text = "Rea Portal",
            Location = new Point(10, 10),
            Size = new Size(300, 230)
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
        _reaProjectComboBox.SelectedIndexChanged += (_, _) => UpdateImportButtonState();
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
            Location = new Point(10, 250),
            Size = new Size(300, 200)
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

        mainPanel.Controls.Add(reaGroup);
        mainPanel.Controls.Add(jiraGroup);

        _startDatePicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd.MM.yyyy HH:mm",
            ShowUpDown = true,
            Location = new Point(330, 20),
            Width = 160
        };
        _endDatePicker = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd.MM.yyyy HH:mm",
            ShowUpDown = true,
            Location = new Point(520, 20),
            Width = 160
        };
        _findButton = CreateButton("Find", FindButton_Click);
        _findButton.Location = new Point(710, 18);
        _findButton.Width = 100;

        _statisticsButton = CreateButton("Meraklısına İstatistik", StatisticsButton_Click);
        _statisticsButton.Location = new Point(820, 18);
        _statisticsButton.Width = 180;
        _statisticsButton.Enabled = false;

        mainPanel.Controls.Add(new Label { Text = "Start Date & Time", Location = new Point(330, 0), AutoSize = true });
        mainPanel.Controls.Add(_startDatePicker);
        mainPanel.Controls.Add(new Label { Text = "End Date & Time", Location = new Point(520, 0), AutoSize = true });
        mainPanel.Controls.Add(_endDatePicker);
        mainPanel.Controls.Add(_findButton);
        mainPanel.Controls.Add(_statisticsButton);

        _worklogGrid = new DataGridView
        {
            AutoGenerateColumns = false,
            Location = new Point(330, 60),
            Size = new Size(730, 430),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
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

        mainPanel.Controls.Add(_worklogGrid);

        _importButton = CreateButton("Import To Rea Portal", ImportButton_Click);
        _importButton.Enabled = false;
        _importButton.Location = new Point(330, 500);
        _importButton.Size = new Size(220, 32);
        _importButton.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;

        _selectionLabel = new Label
        {
            Text = "Selected rows count: 0",
            Location = new Point(570, 506),
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom
        };

        _statusLabel = new Label
        {
            Text = "Hazır",
            Location = new Point(330, 540),
            AutoSize = true,
            ForeColor = Color.FromArgb(60, 60, 60),
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom
        };

        _footerLabel = new Label
        {
            Text = "(c) 2024 emre incekara, 2025 mehmet durmaz",
            Location = new Point(330, 560),
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 100, 100),
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom
        };

        mainPanel.Controls.Add(_importButton);
        mainPanel.Controls.Add(_selectionLabel);
        mainPanel.Controls.Add(_statusLabel);
        mainPanel.Controls.Add(_footerLabel);

        _startDatePicker.Value = DateTime.Today.AddDays(-7);
        _endDatePicker.Value = DateTime.Now;

        UpdateStatisticsButtonState();
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
            AutoSize = true
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
            await _reaClient.LoginAsync(username, password).ConfigureAwait(true);
            _reaLogoutButton.Enabled = true;
            SetStatus($"Rea portal giriş başarılı. ({username})");
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
            await _jiraClient.LoginAsync(email, token).ConfigureAwait(true);
            _jiraLogoutButton.Enabled = true;
            SetStatus($"Jira girişi başarılı. {_jiraClient.DisplayName}");
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
            var startDate = _startDatePicker.Value;
            var endDate = _endDatePicker.Value;
            var worklogs = await _jiraClient.GetWorklogsAsync(startDate, endDate).ConfigureAwait(true);

            _worklogEntries.Clear();
            foreach (var entry in worklogs.OrderBy(w => w.StartDate))
            {
                _worklogEntries.Add(entry);
            }

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

    private async void ImportButton_Click(object? sender, EventArgs e)
    {
        if (!_reaClient.IsAuthenticated)
        {
            MessageBox.Show(this, "Önce Rea portalına giriş yapın.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_worklogGrid.SelectedRows.Count == 0)
        {
            MessageBox.Show(this, "Aktarmak için en az bir kayıt seçmelisiniz.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var userId = _reaUserIdTextBox.Text.Trim();
        var projectId = _reaProjectComboBox.SelectedValue as string ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(projectId))
        {
            MessageBox.Show(this, "Rea kullanıcı ID ve proje seçimi zorunludur.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selectedEntries = _worklogGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(row => row.DataBoundItem as WorklogEntryViewModel)
            .Where(item => item != null)
            .Cast<WorklogEntryViewModel>()
            .ToList();

        _importButton.Enabled = false;
        UseWaitCursor = true;
        try
        {
            foreach (var entry in selectedEntries)
            {
                var timeEntry = new ReaTimeEntry
                {
                    Id = 0,
                    UserId = userId,
                    ProjectId = projectId,
                    Task = entry.Task,
                    StartDate = entry.StartDate,
                    EndDate = entry.EndDate,
                    Effort = entry.EffortHours,
                    Comment = entry.Comment
                };

                await _reaClient.CreateTimeEntryAsync(timeEntry).ConfigureAwait(true);
            }

            SetStatus($"{selectedEntries.Count} kayıt Rea portalına gönderildi.");
        }
        catch (Exception ex)
        {
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
        _importButton.Enabled = _reaClient.IsAuthenticated && hasSelection && hasProject;
    }

    private void UpdateStatisticsButtonState()
    {
        _statisticsButton.Enabled = _worklogEntries.Count > 0;
    }

    private void SetStatus(string message)
    {
        _statusLabel.Text = message;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        var settings = new UserSettings
        {
            ReaUsername = _reaUsernameTextBox.Text,
            ReaPassword = _reaPasswordTextBox.Text,
            JiraEmail = _jiraEmailTextBox.Text,
            JiraToken = _jiraTokenTextBox.Text
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
            var profile = await _reaClient.GetUserProfileAsync().ConfigureAwait(true);
            _reaUserIdTextBox.Text = profile.UserId;

            var projects = await _reaClient.GetProjectsAsync(profile.UserId).ConfigureAwait(true);

            _reaProjects.Clear();
            foreach (var project in projects.OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                _reaProjects.Add(project);
            }

            if (_reaProjects.Count > 0)
            {
                _reaProjectComboBox.SelectedIndex = 0;
            }
            else
            {
                SetStatus("Rea profilinde atanmış proje bulunamadı.");
            }
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
