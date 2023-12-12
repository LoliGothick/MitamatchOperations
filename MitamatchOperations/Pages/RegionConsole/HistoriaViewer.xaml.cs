using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using mitama.Domain;
using mitama.Pages.Common;
using MitamatchOperations.Pages.RegionConsole;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace mitama.Pages.RegionConsole;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class HistoriaViewer : Page
{
    private Summary Summary;
    private readonly ObservableCollection<OrderLog> AllyOrders = [];
    private readonly ObservableCollection<OrderLog> OpponentOrders = [];
    private readonly ObservableCollection<Player> AllyMembers = [];
    private readonly ObservableCollection<Player> OpponentMembers = [];

    public HistoriaViewer()
    {
        InitializeComponent();
    }

    private void Load_Click(object _, RoutedEventArgs _e)
    {
        var allyRegion = Director.ReadCache().Region;
        var logDir = @$"{Director.ProjectDir()}\{allyRegion}\BattleLog";
        var path = $@"{logDir}\{Calendar.Date:yyyy-MM-dd}\summary.json";
        if (!File.Exists(path))
        {
            return;
        }
        
        var summary = Summary = JsonSerializer.Deserialize<Summary>(File.ReadAllText(path));
        var r1 = summary.AllyPoints > summary.OpponentPoints ? "Win" : "Lose";
        var r2 = summary.AllyPoints > summary.OpponentPoints ? "Lose" : "Win";
        
        Date.Text = $"{Calendar.Date:yyyy-MM-dd}";
        Title.Text = $"{r1}: {allyRegion}（{summary.AllyPoints:#,0}） - {r2}: {summary.Opponent}（{summary.OpponentPoints:#,0}）";
        NeunWelt.Text = $"ノイン: {summary.NeunWelt}";
        Comment.Text = summary.Comment;

        AllyMembers.Clear();
        foreach (var player in summary.Allies)
        {
            AllyMembers.Add(player);
        }
        OpponentMembers.Clear();
        foreach (var player in summary.Opponents)
        {
            OpponentMembers.Add(player);
        }
        AllyOrders.Clear();
        foreach (var x in summary.AllyOrders)
        {
            AllyOrders.Add(new OrderLog(Order.List[x.Index], $"{x.Time.Minute:D2}:{x.Time.Second:D2}"));
        }
        OpponentOrders.Clear();
        foreach (var x in summary.OpponentOrders)
        {
            OpponentOrders.Add(new OrderLog(Order.List[x.Index], $"{x.Time.Minute:D2}:{x.Time.Second:D2}"));
        }
    }

    private void MenuFlyout_Opening(object sender, object e)
    {
        var menu = sender as MenuFlyout;
        if (menu.Items.Any(item => item.Name == "Unit")) return;

        var playerName = menu.Target.AccessKey;
        var allyRegion = Director.ReadCache().Region;
        var logDir = @$"{Director.ProjectDir()}\{allyRegion}\BattleLog";
        var date = $"{Calendar.Date:yyyy-MM-dd}";
        var dir = Summary.Allies.Select(p => p.Name).Contains(playerName)
            ? "Ally"
            : "Opponent";
        var unitPath = $@"{logDir}\{date}\{dir}\[{playerName}]\Units";
        var files = Directory.GetFiles(unitPath);
        var unitSubItem = new MenuFlyoutSubItem { Name = "Unit", Text = "Unit" };
        foreach (var file in files)
        {
            var text = file.Split("\\").Last().Replace(".json", string.Empty);
            var (_, unit)  = Unit.FromJson(File.ReadAllText(file));
            var dialog = new DialogBuilder(XamlRoot)
                .WithTitle(text)
                .WithBody(new UnitViewDialog(unit))
                .WithCancel("閉じる")
                .Build();

            unitSubItem.Items.Add(new MenuFlyoutItem
            {
                Text = text,
                Command = new Defer(async () => await dialog.ShowAsync()),
            });
        }
        menu.Items.Add(unitSubItem);

        var statusSubItem = new MenuFlyoutSubItem { Name = "Status", Text = "Status" };
        var statusPath = $@"{logDir}\{date}\{dir}\[{playerName}]\status.json";
        var history = JsonSerializer.Deserialize<SortedDictionary<TimeOnly, AllStatus>>(File.ReadAllText(statusPath));
        foreach (var text in new List<string> 
        {
            "ATK", "Sp.ATK", "DEF", "Sp.DEF",
            "Wind ATK", "Wind DEF",
            "Fire ATK", "Fire DEF",
            "Water ATK", "Water DEF"
        })
        {
            var dialog = new DialogBuilder(XamlRoot)
                .WithTitle(text)
                .WithSize(800, 600)
                .WithBody(new GraphDialog(text, history))
                .WithCancel("閉じる")
                .Build();

            statusSubItem.Items.Add(new MenuFlyoutItem
            {
                Text = text,
                Command = new Defer(async () => await dialog.ShowAsync()),
            });
        }
        menu.Items.Add(statusSubItem);
    }
}

internal record OrderLog(Order Order, string Time);

internal record Summary(
    string Opponent,
    string Comment,
    int AllyPoints,
    int OpponentPoints,
    string NeunWelt,
    Player[] Allies,
    Player[] Opponents,
    OrderIndexAndTime[] AllyOrders,
    OrderIndexAndTime[] OpponentOrders
);
