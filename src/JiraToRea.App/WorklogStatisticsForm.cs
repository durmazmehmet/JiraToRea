using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using JiraToRea.App.Models;

namespace JiraToRea.App;

public sealed class WorklogStatisticsForm : Form
{
    public WorklogStatisticsForm(IReadOnlyCollection<WorklogEntryViewModel> entries)
    {
        if (entries is null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        Text = "Meraklısına İstatistik";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(700, 500);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        var tabControl = new TabControl
        {
            Dock = DockStyle.Fill
        };

        Controls.Add(tabControl);

        var totalHours = entries.Sum(entry => entry.EffortHours);
        var dailyTotals = entries
            .GroupBy(entry => entry.StartDate.Date)
            .Select(group => new DailyTotal(group.Key, group.Sum(entry => entry.EffortHours)))
            .OrderBy(total => total.Date)
            .ToList();

        var taskTotals = entries
            .GroupBy(entry => string.IsNullOrWhiteSpace(entry.Task) ? "(Görev Yok)" : entry.Task.Trim())
            .Select(group => new TaskTotal(group.Key, group.Sum(entry => entry.EffortHours)))
            .OrderByDescending(total => total.Hours)
            .ThenBy(total => total.Task, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        BuildTotalTab(tabControl, totalHours);
        BuildDailyTotalsTab(tabControl, dailyTotals);
        BuildTaskTotalsTab(tabControl, taskTotals);
    }

    private void BuildTotalTab(TabControl tabControl, double totalHours)
    {
        var tabPage = new TabPage("Toplam");

        var totalLabel = new Label
        {
            Text = $"Toplam\n{totalHours:N2} saat",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font.FontFamily, 36F, FontStyle.Bold, GraphicsUnit.Point)
        };

        tabPage.Controls.Add(totalLabel);
        tabControl.TabPages.Add(tabPage);
    }

    private void BuildDailyTotalsTab(TabControl tabControl, IReadOnlyList<DailyTotal> dailyTotals)
    {
        var tabPage = new TabPage("Günlük");

        if (dailyTotals.Count == 0)
        {
            tabPage.Controls.Add(CreateEmptyLabel("Günlük toplam bulunmuyor."));
            tabControl.TabPages.Add(tabPage);
            return;
        }

        var calendarLayout = CreateCalendarLayout(dailyTotals);
        tabPage.Controls.Add(calendarLayout);
        tabControl.TabPages.Add(tabPage);
    }

    private Control CreateCalendarLayout(IReadOnlyList<DailyTotal> dailyTotals)
    {
        var culture = CultureInfo.CurrentCulture;
        var firstDayOfWeek = culture.DateTimeFormat.FirstDayOfWeek;

        var minDate = dailyTotals.First().Date;
        var maxDate = dailyTotals.Last().Date;

        var startDate = minDate;
        var endDate = maxDate;

        var startCalendarDate = startDate;
        while (startCalendarDate.DayOfWeek != firstDayOfWeek)
        {
            startCalendarDate = startCalendarDate.AddDays(-1);
        }

        var endCalendarDate = endDate;
        var lastDayOfWeek = (DayOfWeek)(((int)firstDayOfWeek + 6) % 7);
        while (endCalendarDate.DayOfWeek != lastDayOfWeek)
        {
            endCalendarDate = endCalendarDate.AddDays(1);
        }

        var totalsByDate = dailyTotals.ToDictionary(total => total.Date, total => total.Hours);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7,
            RowCount = 1,
            BackColor = Color.White,
            Padding = new Padding(8),
            AutoScroll = true
        };

        for (var i = 0; i < 7; i++)
        {
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7f));
        }

        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));

        for (var i = 0; i < 7; i++)
        {
            var dayIndex = ((int)firstDayOfWeek + i) % 7;
            var headerLabel = new Label
            {
                Text = culture.DateTimeFormat.AbbreviatedDayNames[dayIndex],
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(Font, FontStyle.Bold)
            };

            table.Controls.Add(headerLabel, i, 0);
        }

        var totalDays = (endCalendarDate - startCalendarDate).Days + 1;
        var weekCount = (int)Math.Ceiling(totalDays / 7.0);

        table.RowCount = weekCount + 1;

        for (var i = 0; i < weekCount; i++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / Math.Max(1, weekCount)));
        }

        var currentDate = startCalendarDate;
        for (var week = 0; week < weekCount; week++)
        {
            for (var day = 0; day < 7; day++)
            {
                var cellPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(4),
                    BackColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };

                var dayLabel = new Label
                {
                    Text = currentDate.Day.ToString(culture),
                    Dock = DockStyle.Top,
                    TextAlign = ContentAlignment.TopLeft,
                    Padding = new Padding(4, 4, 4, 0),
                    Font = new Font(Font, FontStyle.Bold)
                };

                var hoursLabel = new Label
                {
                    Text = totalsByDate.TryGetValue(currentDate, out var value) ? $"{value:N2} saat" : string.Empty,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font(Font, FontStyle.Regular)
                };

                if (currentDate < minDate || currentDate > maxDate)
                {
                    dayLabel.ForeColor = SystemColors.GrayText;
                    hoursLabel.ForeColor = SystemColors.GrayText;
                }

                cellPanel.Controls.Add(hoursLabel);
                cellPanel.Controls.Add(dayLabel);

                table.Controls.Add(cellPanel, day, week + 1);

                currentDate = currentDate.AddDays(1);
            }
        }

        return table;
    }

    private void BuildTaskTotalsTab(TabControl tabControl, IReadOnlyList<TaskTotal> taskTotals)
    {
        var tabPage = new TabPage("Task Bazında");

        if (taskTotals.Count == 0)
        {
            tabPage.Controls.Add(CreateEmptyLabel("Task bazında toplam bulunmuyor."));
            tabControl.TabPages.Add(tabPage);
            return;
        }

        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false
        };

        var taskColumn = new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TaskTotalRow.Task),
            HeaderText = "Task",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };

        var hoursColumn = new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(TaskTotalRow.TotalHours),
            HeaderText = "Toplam Saat",
            Width = 120,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Format = "N2",
                Alignment = DataGridViewContentAlignment.MiddleRight
            }
        };

        grid.Columns.Add(taskColumn);
        grid.Columns.Add(hoursColumn);

        var bindingList = new BindingList<TaskTotalRow>(taskTotals
            .Select(total => new TaskTotalRow(total.Task, total.Hours))
            .ToList());

        grid.DataSource = bindingList;

        tabPage.Controls.Add(grid);
        tabControl.TabPages.Add(tabPage);
    }

    private Control CreateEmptyLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };
    }

    private sealed record DailyTotal(DateTime Date, double Hours);

    private sealed record TaskTotal(string Task, double Hours);

    private sealed class TaskTotalRow
    {
        public TaskTotalRow(string task, double totalHours)
        {
            Task = task;
            TotalHours = totalHours;
        }

        public string Task { get; }

        public double TotalHours { get; }
    }
}
