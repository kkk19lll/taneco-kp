using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Taneco.Models;
using Taneco.ViewModels;

namespace Taneco.Views;

public partial class ProblemDetailsWindow : Window
{
    public ProblemDetailsWindow()
    {
        InitializeComponent();

        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow ?? desktop.Windows.FirstOrDefault(w => w.IsVisible);
            if (mainWindow != null)
            {
                Owner = mainWindow;
            }
        }
    }

    private void OnTimelineCanvasSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        DrawTimeline();
    }

    private void DrawTimeline()
    {
        if (TimelineCanvas == null || DataContext is not ProblemDetailsViewModel viewModel) return;

        TimelineCanvas.Children.Clear();

        var historyEvents = viewModel.HistoryEvents.ToList();
        if (historyEvents.Count == 0) return;

        double canvasWidth = TimelineCanvas.Bounds.Width;
        if (canvasWidth <= 0) canvasWidth = 800;

        double startX = 40;
        double endX = canvasWidth - 40;
        double lineY = 50;
        double availableWidth = endX - startX;

        if (availableWidth <= 0) return;

        // Серая линия
        var mainLine = new Line
        {
            StartPoint = new Avalonia.Point(startX, lineY),
            EndPoint = new Avalonia.Point(endX, lineY),
            Stroke = new SolidColorBrush(Color.Parse("#555555")),
            StrokeThickness = 2
        };
        TimelineCanvas.Children.Add(mainLine);

        var minDate = historyEvents.Min(e => e.EventDate);
        var maxDate = historyEvents.Max(e => e.EventDate);
        var today = DateTime.Now;
        if (today > maxDate) maxDate = today;

        var totalDays = (maxDate - minDate).TotalDays;
        if (totalDays <= 0) totalDays = 1;

        // ГОДА - серые насечки от первого года до текущего
        for (int year = minDate.Year; year <= maxDate.Year; year++)
        {
            var yearDate = new DateTime(year, 1, 1);
            if (yearDate < minDate) yearDate = minDate;
            if (yearDate > maxDate) continue;

            double daysFromStart = (yearDate - minDate).TotalDays;
            double position = (daysFromStart / totalDays) * availableWidth;
            double x = startX + position;

            if (x < startX || x > endX) continue;

            // Серая насечка
            var tick = new Border
            {
                Width = 2,
                Height = 15,
                Background = new SolidColorBrush(Color.Parse("#888888"))
            };
            Canvas.SetLeft(tick, x - 1);
            Canvas.SetTop(tick, lineY - 7);
            TimelineCanvas.Children.Add(tick);

            // Подпись года
            var yearText = new TextBlock
            {
                Text = year.ToString(),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#888888")),
                FontWeight = FontWeight.SemiBold
            };
            Canvas.SetLeft(yearText, x - 15);
            Canvas.SetTop(yearText, lineY + 8);
            TimelineCanvas.Children.Add(yearText);
        }

        // Группируем события по дням
        var eventsByDay = historyEvents
            .GroupBy(e => e.EventDate.Date)
            .Select(g => new { Date = g.Key, Events = g.ToList() })
            .OrderBy(g => g.Date)
            .ToList();

        double lastX = -100;
        double minDistance = 40;

        foreach (var dayGroup in eventsByDay)
        {
            double daysFromStart = (dayGroup.Date - minDate).TotalDays;
            double position = (daysFromStart / totalDays) * availableWidth;
            double x = startX + position;

            // Анти-слипание
            if (x - lastX < minDistance && lastX > 0)
            {
                x = lastX + minDistance;
                if (x > endX) x = endX - 15;
            }

            if (x < startX || x > endX) continue;

            // Белая насечка для даты
            var dateTick = new Border
            {
                Width = 1.5,
                Height = 12,
                Background = Brushes.White
            };
            Canvas.SetLeft(dateTick, x - 0.75);
            Canvas.SetTop(dateTick, lineY - 6);
            TimelineCanvas.Children.Add(dateTick);

            // Подпись даты (день.месяц)
            var dateText = new TextBlock
            {
                Text = dayGroup.Date.ToString("dd.MM"),
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.Parse("#AAAAAA"))
            };
            Canvas.SetLeft(dateText, x - 18);
            Canvas.SetTop(dateText, lineY + 10);
            TimelineCanvas.Children.Add(dateText);

            // Точки событий
            int eventCount = dayGroup.Events.Count;
            double startYOffset = -((eventCount - 1) * 12) / 2.0;

            for (int i = 0; i < eventCount; i++)
            {
                var evt = dayGroup.Events[i];
                double yOffset = startYOffset + (i * 12);
                double pointY = lineY - 10 + yOffset;
                double pointX = x;

                // Если много точек в один день - раздвигаем по горизонтали
                if (eventCount > 1)
                {
                    pointX = x + (i - (eventCount - 1) / 2.0) * 8;
                }

                var point = new Button
                {
                    Width = 12,
                    Height = 12,
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.Parse(GetEventColor(evt.EventType))),
                    BorderBrush = new SolidColorBrush(Color.Parse("#1E1E1E")),
                    BorderThickness = new Thickness(2),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Tag = evt
                };

                point.Click += OnEventPointClick;
                ToolTip.SetTip(point, $"{evt.EventDate:dd.MM.yyyy HH:mm}\n{evt.Details}");

                Canvas.SetLeft(point, pointX - 6);
                Canvas.SetTop(point, pointY - 6);
                TimelineCanvas.Children.Add(point);
            }

            lastX = x;
        }
    }

    private string GetEventColor(string eventType)
    {
        return eventType switch
        {
            "Проблема" => "#E74C3C",
            "detection" => "#E74C3C",
            "Проверка" => "#F39C12",
            "inspection" => "#F39C12",
            "Ремонт" => "#3498DB",
            "repair" => "#3498DB",
            "Ложные показания" => "#27AE60",
            "completion" => "#27AE60",
            "Запланирована проверка" => "#F39C12",
            "В процессе ремонта" => "#3498DB",
            _ => "#7F8C8D"
        };
    }

    private void OnEventPointClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ProblemHistoryEvent historyEvent)
        {
            if (DataContext is ProblemDetailsViewModel viewModel)
            {
                viewModel.SelectedHistoryEvent = historyEvent;
            }
        }
    }
}