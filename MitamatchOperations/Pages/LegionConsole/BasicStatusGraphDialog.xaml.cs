﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using Mitama.Domain;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MitamatchOperations.Pages.LegionConsole
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class BasicStatusGraphDialog : Page
    {
        private readonly SortedDictionary<TimeOnly, AllStatus> History;
        private readonly string Target;

        public BasicStatusGraphDialog(string target, SortedDictionary<TimeOnly, AllStatus> history)
        {
            History = history;
            Target = target;
            InitializeComponent();
            foreach (var (x, y) in History.Select(item => ((item.Key.Minute * 60 + item.Key.Second) * 500 / 600 + 50, -ToTarget(item.Value) + 250)))
            {
                Graph.Points.Add(new(x, y));
            }
        }

        private float ToTarget(AllStatus status)
        {
            return Target switch
            {
                "ATK" => status.Attack / 3000.0f,
                "Sp.ATK" => status.SpecialAttack / 3000.0f,
                "DEF" => status.Defense / 3000.0f,
                "Sp.DEF" => status.SpecialDefense / 3000.0f,
                _ => throw new ArgumentException("Invalid target"),
            };
        }
    }
}
