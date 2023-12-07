using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using mitama.Domain;
using mitama.Pages.Common;
using Windows.ApplicationModel.DataTransfer;
using WinRT;
using SimdLinq;
using mitama.Algorithm.IR;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Microsoft.UI.Text;
using Windows.Storage;
using System.Xml.Linq;

namespace mitama.Pages.DeckBuilder
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class BuilderPage : Page
    {
        // Temporarily store the selected Memoria
        private List<Memoria> selectedMemorias = [];

        private ObservableCollection<MemoriaWithConcentration> Deck { get; set; } = [];
        private ObservableCollection<MemoriaWithConcentration> LegendaryDeck { get; set; } = [];
        private ObservableCollection<Memoria> Pool { get; set; } = new(Memoria.List.Where(Costume.DummyRearguard.CanBeEquipped));
        private ObservableCollection<MyTreeNode> TreeNodes { get; set; } = [];
        private HashSet<FilterType> _currentFilters = [];
        private string region = "";
        private MemberInfo[] members = [];
        readonly Dictionary<KindType, int> kindPairs = [];
        readonly Dictionary<SkillType, int> skillPairs = [];
        readonly Dictionary<SupportType, SupportBreakdown> supportPairs = [];
        private readonly Dictionary<FilterType, Func<Memoria, bool>> Filters = [];
        private readonly string _regionName;
        private readonly ObservableCollection<SupportBreakdown> SupporBreakdowns = [];

        public BuilderPage()
        {
            InitFilters();
            InitMembers();
            InitializeComponent();
            InitFilterOptions();
            InitSearchOptions();
            _regionName = Director.ReadCache().Region;
        }

        private void InitSearchOptions()
        {
            string[] searchOptions = [
                "???",
                "���W�F���_���[",
                "ATK�A�b�v",
                "ATK�_�E��",
                "Sp.ATK�A�b�v",
                "Sp.ATK�_�E��",
                "DEF�A�b�v",
                "DEF�_�E��",
                "Sp.DEF�A�b�v",
                "Sp.DEF�_�E��",
                "�Α����U���̓A�b�v",
                "�Α����U���̓_�E��",
                "�������U���̓A�b�v",
                "�������U���̓_�E��",
                "�������U���̓A�b�v",
                "�������U���̓_�E��",
                "�������U���̓A�b�v",
                "�������U���̓_�E��",
                "�ő����U���̓A�b�v",
                "�ő����U���̓_�E��",
                "�Α����h��̓A�b�v",
                "�Α����h��̓_�E��",
                "�������h��̓A�b�v",
                "�������h��̓_�E��",
                "�������h��̓A�b�v",
                "�������h��̓_�E��",
                "�������h��̓A�b�v",
                "�������h��̓_�E��",
                "�ő����h��̓A�b�v",
                "�ő����h��̓_�E��",
                "HP�A�b�v",
                "�Ό��ʃA�b�v",
                "�����ʃA�b�v",
                "�����ʃA�b�v",
                "�����ʃA�b�v",
                "�Ō��ʃA�b�v",
                "�΋�",
                "����",
                "����",
                "�Ύ�",
                "����",
                "����",
                "�Ίg",
                "���g",
                "���g",
                "�q�[��",
                "�`���[�W",
                "���J�o�[",
                "�J�E���^�[",
            ];

            foreach (var option in searchOptions)
            {
                var checkBox = new CheckBox { Content = option, IsChecked = false };
                checkBox.Checked += SearchOption_Checked;
                checkBox.Unchecked += SearchOption_Unchecked;
                SearchOptions.Children.Add(checkBox);
            }
        }

        private void InitMembers()
        {
            var cache = Director.ReadCache();
            region = cache.Region;
            members = Util.LoadMembersInfo(region);
        }

        private void Memeria_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            try
            {
                selectedMemorias = e.Items.Select(v => (Memoria)v).ToList();
                e.Data.RequestedOperation = DataPackageOperation.Move;
            } catch
            {
                selectedMemorias = e.Items.Select(v => ((MemoriaWithConcentration)v).Memoria).ToList();
                e.Data.RequestedOperation = DataPackageOperation.Move;
            }
        }

        private void MemeriaSources_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Move;
        }
        private void Deck_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Move;
        }

        private void MyDropCompleted(UIElement sender, DropCompletedEventArgs args)
        {
        }

        private void Cleanup()
        {
            selectedMemorias.Clear();

            if (Deck.Concat(LegendaryDeck).Any())
            {
                var (atk, spatk, def, spdef) = Deck.Concat(LegendaryDeck).Select(m => m.Memoria.Status[m.Concentration]).Aggregate((a, b) => a + b);
                Atk.Content = $"Atk: {atk}";
                SpAtk.Content = $"SpAtk: {spatk}";
                Def.Content = $"Def: {def}";
                SpDef.Content = $"SpDef: {spdef}";
            }
            else
            {
                Atk.Content = $"Atk: 0";
                SpAtk.Content = $"SpAtk: 0";
                Def.Content = $"Def: 0";
                SpDef.Content = $"SpDef: 0";
            }

            Fire.Content = $"��: {Deck.Concat(LegendaryDeck).Count(m => m.Memoria.Element is Element.Fire)}";
            Water.Content = $"��: {Deck.Concat(LegendaryDeck).Count(m => m.Memoria.Element is Element.Water)}";
            Wind.Content = $"��: {Deck.Concat(LegendaryDeck).Count(m => m.Memoria.Element is Element.Wind)}";
            Light.Content = $"��: {Deck.Concat(LegendaryDeck).Count(m => m.Memoria.Element is Element.Light)}";
            Dark.Content = $"��: {Deck.Concat(LegendaryDeck).Count(m => m.Memoria.Element is Element.Dark)}";

            if (Deck.DistinctBy(m => m.Memoria.Name).Count() != Deck.Count)
            {
                GeneralInfoBar.Title = "���o���̃������A���d�����Ă��܂�";
                GeneralInfoBar.Severity = InfoBarSeverity.Error;
                GeneralInfoBar.IsOpen = true;
            } else
            {
                GeneralInfoBar.IsOpen = false;
            }

            supportPairs.Clear();
            foreach (var (effect, level) in Deck.Concat(LegendaryDeck).SelectMany(m => m.Memoria.SupportSkill.Effects.Select(e => (e, m.Memoria.SupportSkill.Level))))
            {
                var type = BuilderPageHelpers.ToSupportType(effect);
                if (supportPairs.TryGetValue(type, out SupportBreakdown breakdown))
                {
                    if (breakdown.Breakdown.TryGetValue(level, out int count))
                    {
                        supportPairs[type].Breakdown[level] = count + 1;
                    }
                    else
                    {
                        supportPairs[type].Breakdown.Add(level, 1);
                    }
                }
                else
                {
                    supportPairs.Add(type, new SupportBreakdown()
                    {
                        Type = type,
                        Breakdown = new() { { level, 1 } }
                    });
                }
            }

            SupporBreakdowns.Clear();
            foreach (var (_, breakdown) in supportPairs)
            {
                SupporBreakdowns.Add(breakdown);
            }

            skillPairs.Clear();
            foreach (var effect in Deck.Concat(LegendaryDeck).SelectMany(m => m.Memoria.Skill.StatusChanges))
            {
                var type = BuilderPageHelpers.ToSkillType(effect);
                if (skillPairs.TryGetValue(type, out int count))
                {
                    skillPairs[type] = count + 1;
                }
                else
                {
                    skillPairs.Add(type, 1);
                }
            }

            SkillSummary.Items.Clear();
            foreach (var (type, num) in skillPairs)
            {
                SkillSummary.Items.Add(new Button() {
                    Content = $"{BuilderPageHelpers.SkillTypeToString(type)}: {num}",
                    Width = 120,
                });
            }

            kindPairs.Clear();
            foreach (var kind in Deck.Concat(LegendaryDeck).Select(m => m.Memoria.Kind))
            {
                var type = BuilderPageHelpers.ToKindType(kind);
                if (kindPairs.TryGetValue(type, out int count))
                {
                    kindPairs[type] = count + 1;
                }
                else
                {
                    kindPairs.Add(type, 1);
                }
            }

            Breakdown.Items.Clear();
            foreach (var (type, num) in kindPairs)
            {
                Breakdown.Items.Add(new Button()
                {
                    Content = $"{BuilderPageHelpers.KindTypeToString(type)}: {num}",
                    Width = 120,
                });
            }
        }

        private void Deck_Drop(object sender, DragEventArgs e)
        {
            foreach (var memoria in selectedMemorias.Where(m => !m.IsLegendary))
            {
                Deck.Add(new MemoriaWithConcentration(memoria, 4));
            }
            foreach (var memoria in selectedMemorias.Where(m => m.IsLegendary))
            {
                LegendaryDeck.Add(new MemoriaWithConcentration(memoria, 4));
            }
            foreach (var toRemove in Pool.Where(m => selectedMemorias.Select(s => s.Name).Contains(m.Name)).ToList())
            {
                Pool.Remove(toRemove);
            }
            Cleanup();
        }

        private void MemeriaSources_Drop(object sender, DragEventArgs e)
        {
            var dummyCostume = Switch.IsOn ? Costume.DummyVanguard : Costume.DummyRearguard;
            foreach (var toAdd in Memoria
                .List
                .Where(dummyCostume.CanBeEquipped)
                .Where(m => selectedMemorias.Select(s => s.Name).Contains(m.Name)))
            {
                Pool.Add(toAdd);
            }
            foreach (var toRemove in LegendaryDeck.ToList().Where(m => selectedMemorias.Contains(m.Memoria)))
            {
                LegendaryDeck.Remove(toRemove);
            }
            foreach (var toRemove in Deck.ToList().Where(m => selectedMemorias.Contains(m.Memoria)))
            {
                Deck.Remove(toRemove);
            }
            Cleanup();
            Sort(SortOption.SelectedIndex);
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggleSwitch)
            {
                SupporBreakdowns.Clear();
                LegendaryDeck.Clear();
                Deck.Clear();
                Breakdown.Items.Clear();
                SkillSummary.Items.Clear();
                Atk.Content = $"Atk: 0";
                SpAtk.Content = $"SpAtk: 0";
                Def.Content = $"Def: 0";
                SpDef.Content = $"SpDef: 0";
                Fire.Content = $"��: 0";
                Water.Content = $"��: 0";
                Wind.Content = $"��: 0";
                Light.Content = $"��: 0";
                Dark.Content = $"��: 0";

                if (toggleSwitch.IsOn)
                {
                    VoR.Label = "�O�q";
                    Pool = new(Memoria.List.Where(Costume.DummyVanguard.CanBeEquipped));
                    MemoriaSources.ItemsSource = Pool;
                    TreeNodes[0].Children.Clear();
                    TreeNodes[0].Children.Add(new() { Text = "�ʏ�P��" });
                    TreeNodes[0].Children.Add(new() { Text = "�ʏ�͈�" });
                    TreeNodes[0].Children.Add(new() { Text = "����P��" });
                    TreeNodes[0].Children.Add(new() { Text = "����͈�" });
                    _currentFilters = [
                        FilterType.NormalSingle,
                        FilterType.NormalRange,
                        FilterType.SpecialSingle,
                        FilterType.SpecialRange,
                    ];
                    foreach (var type in Enum.GetValues(typeof(FilterType)).Cast<FilterType>().Where(f => !IsKindFilter(f) && !IsSearchOption(f)))
                    {
                        _currentFilters.Add(type);
                    }
                }
                else
                {
                    VoR.Label = "��q";
                    Pool = new(Memoria.List.Where(Costume.DummyRearguard.CanBeEquipped));
                    TreeNodes[0].Children.Clear();
                    TreeNodes[0].Children.Add(new() { Text = "�x��" });
                    TreeNodes[0].Children.Add(new() { Text = "�W�Q" });
                    TreeNodes[0].Children.Add(new() { Text = "��" });
                    _currentFilters = [
                        FilterType.Support,
                        FilterType.Interference,
                        FilterType.Recovery
                    ];
                    foreach (var type in Enum.GetValues(typeof(FilterType)).Cast<FilterType>().Where(f => !IsKindFilter(f) && !IsSearchOption(f)))
                    {
                        _currentFilters.Add(type);
                    }
                    MemoriaSources.ItemsSource = Pool;
                }
            }
        }

        private void FilterOption_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox box) return;
            var prevCount = _currentFilters.Count;

            switch (box.Content)
            {
                case "���":
                    {
                        foreach (var node in TreeNodes[0].Children)
                        {
                            node.IsChecked = true;
                        }
                        if (Switch.IsOn)
                        {
                            _currentFilters.Add(FilterType.NormalSingle);
                            _currentFilters.Add(FilterType.NormalRange);
                            _currentFilters.Add(FilterType.SpecialSingle);
                            _currentFilters.Add(FilterType.SpecialRange);
                        }
                        else
                        {
                            _currentFilters.Add(FilterType.Support);
                            _currentFilters.Add(FilterType.Interference);
                            _currentFilters.Add(FilterType.Recovery);
                        }
                        break;
                    }
                case "�ʏ�P��":
                    {
                        _currentFilters.Add(FilterType.NormalSingle);
                        break;
                    }
                case "�ʏ�͈�":
                    {
                        _currentFilters.Add(FilterType.NormalRange);
                        break;
                    }
                case "����P��":
                    {
                        _currentFilters.Add(FilterType.SpecialSingle);
                        break;
                    }
                case "����͈�":
                    {
                        _currentFilters.Add(FilterType.SpecialRange);
                        break;
                    }
                case "�x��":
                    {
                        _currentFilters.Add(FilterType.Support);
                        break;
                    }
                case "�W�Q":
                    {
                        _currentFilters.Add(FilterType.Interference);
                        break;
                    }
                case "��":
                    {
                        _currentFilters.Add(FilterType.Recovery);
                        break;
                    }
                case "����":
                    {
                        foreach (var node in TreeNodes[1].Children)
                        {
                            node.IsChecked = true;
                        }
                        foreach (var filter in Enum.GetValues(typeof(FilterType)).Cast<FilterType>().Where(IsElementFilter))
                        {
                            _currentFilters.Add(filter);
                        }
                        break;
                    }
                case "��":
                    {
                        _currentFilters.Add(FilterType.Fire);
                        break;
                    }
                case "��":
                    {
                        _currentFilters.Add(FilterType.Water);
                        break;
                    }
                case "��":
                    {
                        _currentFilters.Add(FilterType.Wind);
                        break;
                    }
                case "��":
                    {
                        _currentFilters.Add(FilterType.Light);
                        break;
                    }
                case "��":
                    {
                        _currentFilters.Add(FilterType.Dark);
                        break;
                    }
                case "�͈�":
                    {
                        foreach (var node in TreeNodes[2].Children)
                        {
                            node.IsChecked = true;
                        }
                        foreach (var filter in Enum.GetValues(typeof(FilterType)).Cast<FilterType>().Where(IsRangeFilter))
                        {
                            _currentFilters.Add(filter);
                        }
                        break;
                    }
                case "A":
                    {
                        _currentFilters.Add(FilterType.A);
                        break;
                    }
                case "B":
                    {
                        _currentFilters.Add(FilterType.B);
                        break;
                    }
                case "C":
                    {
                        _currentFilters.Add(FilterType.C);
                        break;
                    }
                case "D":
                    {
                        _currentFilters.Add(FilterType.D);
                        break;
                    }
                case "E":
                    {
                        _currentFilters.Add(FilterType.E);
                        break;
                    }
                case "���ʗ�":
                    {
                        foreach (var node in TreeNodes[3].Children)
                        {
                            node.IsChecked = true;
                        }
                        foreach (var filter in Enum.GetValues(typeof(FilterType)).Cast<FilterType>().Where(IsLevelFilter))
                        {
                            _currentFilters.Add(filter);
                        }
                        break;
                    }
                case "�T":
                    {
                        _currentFilters.Add(FilterType.One);
                        break;
                    }
                case "�U":
                    {
                        _currentFilters.Add(FilterType.Two);
                        break;
                    }
                case "�V":
                    {
                        _currentFilters.Add(FilterType.Three);
                        break;
                    }
                case "�V+":
                    {
                        _currentFilters.Add(FilterType.ThreePlus);
                        break;
                    }
                case "�W":
                    {
                        _currentFilters.Add(FilterType.Four);
                        break;
                    }
                case "�W+":
                    {
                        _currentFilters.Add(FilterType.FourPlus);
                        break;
                    }
                case "�X":
                    {
                        _currentFilters.Add(FilterType.Five);
                        break;
                    }
                case "�X+":
                    {
                        _currentFilters.Add(FilterType.FivePlus);
                        break;
                    }
                case "LG":
                    {
                        _currentFilters.Add(FilterType.Lg);
                        break;
                    }
                case "LG+":
                    {
                        _currentFilters.Add(FilterType.LgPlus);
                        break;
                    }
                default:
                    {
                        throw new UnreachableException("Unreachable");
                    }
            }
            if (prevCount == _currentFilters.Count) return;
            foreach (var memoria in Memoria
                .List
                .Where(memoria => !Pool.Contains(memoria))
                .Where(memoria => !Deck.Concat(LegendaryDeck).Select(m => m.Memoria.Name).Contains(memoria.Name))
                .Where(ApplyFilter))
            {
                Pool.Add(memoria);
            }
            if (SortOption == null)
            {
                Sort(0);
            }
            else
            {
                Sort(SortOption.SelectedIndex);
            }
        }

        private void FilterOption_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox box) return;
            var prevCount = _currentFilters.Count;

            switch (box.Content)
            {
                case "���":
                    {
                        _currentFilters.RemoveWhere(IsKindFilter);
                        break;
                    }
                case "�ʏ�P��":
                    {
                        _currentFilters.Remove(FilterType.NormalSingle);
                        break;
                    }
                case "�ʏ�͈�":
                    {
                        _currentFilters.Remove(FilterType.NormalRange);
                        break;
                    }
                case "����P��":
                    {
                        _currentFilters.Remove(FilterType.SpecialSingle);
                        break;
                    }
                case "����͈�":
                    {
                        _currentFilters.Remove(FilterType.SpecialRange);
                        break;
                    }
                case "�x��":
                    {
                        _currentFilters.Remove(FilterType.Support);
                        break;
                    }
                case "�W�Q":
                    {
                        _currentFilters.Remove(FilterType.Interference);
                        break;
                    }
                case "��":
                    {
                        _currentFilters.Remove(FilterType.Recovery);
                        break;
                    }
                case "����":
                    {
                        foreach (var node in TreeNodes[1].Children)
                        {
                            if (!node.IsChecked) return;
                            node.IsChecked = false;
                        }
                        _currentFilters.RemoveWhere(IsElementFilter);
                        Pool.Clear();
                        break;
                    }
                case "��":
                    {
                        _currentFilters.Remove(FilterType.Fire);
                        break;
                    }
                case "��":
                    {
                        _currentFilters.Remove(FilterType.Water);
                        break;
                    }
                case "��":
                    {
                        _currentFilters.Remove(FilterType.Wind);
                        break;
                    }
                case "��":
                    {
                        _currentFilters.Remove(FilterType.Light);
                        break;
                    }
                case "��":
                    {
                        _currentFilters.Remove(FilterType.Dark);
                        break;
                    }
                case "�͈�":
                    {
                        foreach (var node in TreeNodes[2].Children)
                        {
                            if (!node.IsChecked) return;
                            node.IsChecked = false;
                        }
                        _currentFilters.RemoveWhere(IsRangeFilter);
                        Pool.Clear();
                        break;
                    }
                case "A":
                    {
                        _currentFilters.Remove(FilterType.A);
                        break;
                    }
                case "B":
                    {
                        _currentFilters.Remove(FilterType.B);
                        break;
                    }
                case "C":
                    {
                        _currentFilters.Remove(FilterType.C);
                        break;
                    }
                case "D":
                    {
                        _currentFilters.Remove(FilterType.D);
                        break;
                    }
                case "E":
                    {
                        _currentFilters.Remove(FilterType.E);
                        break;
                    }
                case "���ʗ�":
                    {
                        foreach (var node in TreeNodes[3].Children)
                        {
                            if (!node.IsChecked) return;
                            node.IsChecked = false;
                        }
                        _currentFilters.RemoveWhere(IsLevelFilter);
                        Pool.Clear();
                        break;
                    }
                case "�T":
                    {
                        _currentFilters.Remove(FilterType.One);
                        break;
                    }
                case "�U":
                    {
                        _currentFilters.Remove(FilterType.Two);
                        break;
                    }
                case "�V":
                    {
                        _currentFilters.Remove(FilterType.Three);
                        break;
                    }
                case "�V+":
                    {
                        _currentFilters.Remove(FilterType.ThreePlus);
                        break;
                    }
                case "�W":
                    {
                        _currentFilters.Remove(FilterType.Four);
                        break;
                    }
                case "�W+":
                    {
                        _currentFilters.Remove(FilterType.FourPlus);
                        break;
                    }
                case "�X":
                    {
                        _currentFilters.Remove(FilterType.Five);
                        break;
                    }
                case "�X+":
                    {
                        _currentFilters.Remove(FilterType.FivePlus);
                        break;
                    }
                case "LG":
                    {
                        _currentFilters.Remove(FilterType.Lg);
                        break;
                    }
                case "LG+":
                    {
                        _currentFilters.Remove(FilterType.LgPlus);
                        break;
                    }
                default:
                    {
                        throw new UnreachableException("Unreachable");
                    }
            }
            if (prevCount == _currentFilters.Count) return;
            foreach (var memoria in Pool.ToList().Where(m => !ApplyFilter(m)))
            {
                Pool.Remove(memoria);
            }
            Sort(SortOption.SelectedIndex);
        }

        private void InitFilterOptions()
        {
            TreeNodes =
            [
                new()
                {
                    Text = "���",
                    Children =
                    [
                        new() { Text = "�x��" },
                        new() { Text = "�W�Q" },
                        new() { Text = "��" },
                    ]
                },
                new()
                {
                    Text = "����",
                    Children =
                    [
                        new() { Text = "��" },
                        new() { Text = "��" },
                        new() { Text = "��" },
                        new() { Text = "��" },
                        new() { Text = "��" }
                    ]
                },
                new()
                {
                    Text = "�͈�",
                    Children =
                    [
                        new() { Text = "A" },
                        new() { Text = "B" },
                        new() { Text = "C" },
                        new() { Text = "D" },
                        new() { Text = "E" }
                    ]
                },
                new()
                {
                    Text = "���ʗ�",
                    Children =
                    [
                        new() { Text = "�T" },
                        new() { Text = "�U" },
                        new() { Text = "�V" },
                        new() { Text = "�V+" },
                        new() { Text = "�W" },
                        new() { Text = "�W+" },
                        new() { Text = "�X" },
                        new() { Text = "�X+" },
                        new() { Text = "LG" },
                        new() { Text = "LG+" }
                    ]
                },
            ];

            FilterOptions.ItemsSource = TreeNodes;
            _currentFilters = [
                FilterType.Support,
                FilterType.Interference,
                FilterType.Recovery
            ];
            foreach (var type in Enum.GetValues(typeof(FilterType)).Cast<FilterType>().Where(f => !IsKindFilter(f) && !IsSearchOption(f)))
            {
                _currentFilters.Add(type);
            }
        }

        private void SearchOption_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox box) return;
            var prevCount = _currentFilters.Count;
            switch (box.Content)
            {
                case "���W�F���_���[":
                    {
                        _currentFilters.Add(FilterType.Legendary);
                        break;
                    }
                case "ATK�A�b�v":
                    {
                        _currentFilters.Add(FilterType.Au);
                        break;
                    }
                case "ATK�_�E��":
                    {
                        _currentFilters.Add(FilterType.Ad);
                        break;
                    }
                case "Sp.ATK�A�b�v":
                    {
                        _currentFilters.Add(FilterType.SAu);
                        break;
                    }
                case "Sp.ATK�_�E��":
                    {
                        _currentFilters.Add(FilterType.SAd);
                        break;
                    }
                case "DEF�A�b�v":
                    {
                        _currentFilters.Add(FilterType.Du);
                        break;
                    }
                case "DEF�_�E��":
                    {
                        _currentFilters.Add(FilterType.Dd);
                        break;
                    }
                case "Sp.DEF�A�b�v":
                    {
                        _currentFilters.Add(FilterType.SDu);
                        break;
                    }
                case "Sp.DEF�_�E��":
                    {
                        _currentFilters.Add(FilterType.SDd);
                        break;
                    }
                case "�Α����U���̓A�b�v":
                    {
                        _currentFilters.Add(FilterType.FPu);
                        break;
                    }
                case "�Α����U���̓_�E��":
                    {
                        _currentFilters.Add(FilterType.FPd);
                        break;
                    }
                case "�������U���̓A�b�v":
                    {
                        _currentFilters.Add(FilterType.WaPu);
                        break;
                    }
                case "�������U���̓_�E��":
                    {
                        _currentFilters.Add(FilterType.WaPd);
                        break;
                    }
                case "�������U���̓A�b�v":
                    {
                        _currentFilters.Add(FilterType.WiPu);
                        break;
                    }
                case "�������U���̓_�E��":
                    {
                        _currentFilters.Add(FilterType.WiPd);
                        break;
                    }
                case "�������U���̓A�b�v":
                    {
                        _currentFilters.Add(FilterType.LPu);
                        break;
                    }
                case "�������U���̓_�E��":
                    {
                        _currentFilters.Add(FilterType.LPd);
                        break;
                    }
                case "�ő����U���̓A�b�v":
                    {
                        _currentFilters.Add(FilterType.DPu);
                        break;
                    }
                case "�ő����U���̓_�E��":
                    {
                        _currentFilters.Add(FilterType.DPd);
                        break;
                    }
                case "�Α����h��̓A�b�v":
                    {
                        _currentFilters.Add(FilterType.FGu);
                        break;
                    }
                case "�Α����h��̓_�E��":
                    {
                        _currentFilters.Add(FilterType.FGd);
                        break;
                    }
                case "�������h��̓A�b�v":
                    {
                        _currentFilters.Add(FilterType.WaGu);
                        break;
                    }
                case "�������h��̓_�E��":
                    {
                        _currentFilters.Add(FilterType.WaGd);
                        break;
                    }
                case "�������h��̓A�b�v":
                    {
                        _currentFilters.Add(FilterType.WiGu);
                        break;
                    }
                case "�������h��̓_�E��":
                    {
                        _currentFilters.Add(FilterType.WiGd);
                        break;
                    }
                case "�������h��̓A�b�v":
                    {
                        _currentFilters.Add(FilterType.LGu);
                        break;
                    }
                case "�������h��̓_�E��":
                    {
                        _currentFilters.Add(FilterType.LGd);
                        break;
                    }
                case "�ő����h��̓A�b�v":
                    {
                        _currentFilters.Add(FilterType.DGu);
                        break;
                    }
                case "�ő����h��̓_�E��":
                    {
                        _currentFilters.Add(FilterType.DGd);
                        break;
                    }
                case "HP�A�b�v":
                    {
                        _currentFilters.Add(FilterType.HPu);
                        break;
                    }
                case "�Ό��ʃA�b�v":
                    {
                        _currentFilters.Add(FilterType.FireStimulation);
                        break;
                    }
                case "�����ʃA�b�v":
                    {
                        _currentFilters.Add(FilterType.WaterStimulation);
                        break;
                    }
                case "�����ʃA�b�v":
                    {
                        _currentFilters.Add(FilterType.WindStimulation);
                        break;
                    }
                case "�����ʃA�b�v":
                    {
                        _currentFilters.Add(FilterType.LightStimulation);
                        break;
                    }
                case "�Ō��ʃA�b�v":
                    {
                        _currentFilters.Add(FilterType.DarkStimulation);
                        break;
                    }
                case "�΋�":
                    {
                        _currentFilters.Add(FilterType.FireStrong);
                        break;
                    }
                case "����":
                    {
                        _currentFilters.Add(FilterType.WaterStrong);
                        break;
                    }
                case "����":
                    {
                        _currentFilters.Add(FilterType.WindStrong);
                        break;
                    }
                case "�Ύ�":
                    {
                        _currentFilters.Add(FilterType.FireWeak);
                        break;
                    }
                case "����":
                    {
                        _currentFilters.Add(FilterType.WaterWeak);
                        break;
                    }
                case "����":
                    {
                        _currentFilters.Add(FilterType.WindWeak);
                        break;
                    }
                case "�Ίg":
                    {
                        _currentFilters.Add(FilterType.FireSpread);
                        break;
                    }
                case "���g":
                    {
                        _currentFilters.Add(FilterType.WaterSpread);
                        break;
                    }
                case "���g":
                    {
                        _currentFilters.Add(FilterType.WindSpread);
                        break;
                    }
                case "�q�[��":
                    {
                        _currentFilters.Add(FilterType.Heal);
                        break;
                    }
                case "�`���[�W":
                    {
                        _currentFilters.Add(FilterType.Charge);
                        break;
                    }
                case "���J�o�[":
                    {
                        _currentFilters.Add(FilterType.Recover);
                        break;
                    }
                case "�J�E���^�[":
                    {
                        _currentFilters.Add(FilterType.Counter);
                        break;
                    }
                default:
                    {
                        throw new UnreachableException("Unreachable");
                    }
            }
            if (prevCount == _currentFilters.Count) return;
            foreach (var memoria in Pool.ToList().Where(m => !ApplyFilter(m)))
            {
                Pool.Remove(memoria);
            }
            Sort(SortOption.SelectedIndex);
        }

        private void SearchOption_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox box) return;
            var prevCount = _currentFilters.Count;

            switch (box.Content)
            {
                case "���W�F���_���[":
                    {
                        _currentFilters.Remove(FilterType.Legendary);
                        break;
                    }
                case "ATK�A�b�v":
                    {
                        _currentFilters.Remove(FilterType.Au);
                        break;
                    }
                case "ATK�_�E��":
                    {
                        _currentFilters.Remove(FilterType.Ad);
                        break;
                    }
                case "Sp.ATK�A�b�v":
                    {
                        _currentFilters.Remove(FilterType.SAu);
                        break;
                    }
                case "Sp.ATK�_�E��":
                    {
                        _currentFilters.Remove(FilterType.SAd);
                        break;
                    }
                case "DEF�A�b�v":
                    {
                        _currentFilters.Remove(FilterType.Du);
                        break;
                    }
                case "DEF�_�E��":
                    {
                        _currentFilters.Remove(FilterType.Dd);
                        break;
                    }
                case "Sp.DEF�A�b�v":
                    {
                        _currentFilters.Remove(FilterType.SDu);
                        break;
                    }
                case "Sp.DEF�_�E��":
                    {
                        _currentFilters.Remove(FilterType.SDd);
                        break;
                    }
                case "�Α����U���̓A�b�v":
                    {
                        _currentFilters.Remove(FilterType.FPu);
                        break;
                    }
                case "�Α����U���̓_�E��":
                    {
                        _currentFilters.Remove(FilterType.FPd);
                        break;
                    }
                case "�������U���̓A�b�v":
                    {
                        _currentFilters.Remove(FilterType.WaPu);
                        break;
                    }
                case "�������U���̓_�E��":
                    {
                        _currentFilters.Remove(FilterType.WaPd);
                        break;
                    }
                case "�������U���̓A�b�v":
                    {
                        _currentFilters.Remove(FilterType.WiPu);
                        break;
                    }
                case "�������U���̓_�E��":
                    {
                        _currentFilters.Remove(FilterType.WiPd);
                        break;
                    }
                case "�������U���̓A�b�v":
                    {
                        _currentFilters.Remove(FilterType.LPu);
                        break;
                    }
                case "�������U���̓_�E��":
                    {
                        _currentFilters.Remove(FilterType.LPd);
                        break;
                    }
                case "�ő����U���̓A�b�v":
                    {
                        _currentFilters.Remove(FilterType.DPu);
                        break;
                    }
                case "�ő����U���̓_�E��":
                    {
                        _currentFilters.Remove(FilterType.DPd);
                        break;
                    }
                case "�Α����h��̓A�b�v":
                    {
                        _currentFilters.Remove(FilterType.FGu);
                        break;
                    }
                case "�Α����h��̓_�E��":
                    {
                        _currentFilters.Remove(FilterType.FGd);
                        break;
                    }
                case "�������h��̓A�b�v":
                    {
                        _currentFilters.Remove(FilterType.WaGu);
                        break;
                    }
                case "�������h��̓_�E��":
                    {
                        _currentFilters.Remove(FilterType.WaGd);
                        break;
                    }
                case "�������h��̓A�b�v":
                    {
                        _currentFilters.Remove(FilterType.WiGu);
                        break;
                    }
                case "�������h��̓_�E��":
                    {
                        _currentFilters.Remove(FilterType.WiGd);
                        break;
                    }
                case "�������h��̓A�b�v":
                    {
                        _currentFilters.Remove(FilterType.LGu);
                        break;
                    }
                case "�������h��̓_�E��":
                    {
                        _currentFilters.Remove(FilterType.LGd);
                        break;
                    }
                case "�ő����h��̓A�b�v":
                    {
                        _currentFilters.Remove(FilterType.DGu);
                        break;
                    }
                case "�ő����h��̓_�E��":
                    {
                        _currentFilters.Remove(FilterType.DGd);
                        break;
                    }
                case "HP�A�b�v":
                    {
                        _currentFilters.Remove(FilterType.HPu);
                        break;
                    }
                case "�Ό��ʃA�b�v":
                    {
                        _currentFilters.Remove(FilterType.FireStimulation);
                        break;
                    }
                case "�����ʃA�b�v":
                    {
                        _currentFilters.Remove(FilterType.WaterStimulation);
                        break;
                    }
                case "�����ʃA�b�v":
                    {
                        _currentFilters.Remove(FilterType.WindStimulation);
                        break;
                    }
                case "�����ʃA�b�v":
                    {
                        _currentFilters.Remove(FilterType.LightStimulation);
                        break;
                    }
                case "�Ō��ʃA�b�v":
                    {
                        _currentFilters.Remove(FilterType.DarkStimulation);
                        break;
                    }
                case "�΋�":
                    {
                        _currentFilters.Remove(FilterType.FireStrong);
                        break;
                    }
                case "����":
                    {
                        _currentFilters.Remove(FilterType.WaterStrong);
                        break;
                    }
                case "����":
                    {
                        _currentFilters.Remove(FilterType.WindStrong);
                        break;
                    }
                case "�Ύ�":
                    {
                        _currentFilters.Remove(FilterType.FireWeak);
                        break;
                    }
                case "����":
                    {
                        _currentFilters.Remove(FilterType.WaterWeak);
                        break;
                    }
                case "����":
                    {
                        _currentFilters.Remove(FilterType.WindWeak);
                        break;
                    }
                case "�Ίg":
                    {
                        _currentFilters.Remove(FilterType.FireSpread);
                        break;
                    }
                case "���g":
                    {
                        _currentFilters.Remove(FilterType.WaterSpread);
                        break;
                    }
                case "���g":
                    {
                        _currentFilters.Remove(FilterType.WindSpread);
                        break;
                    }
                case "�q�[��":
                    {
                        _currentFilters.Remove(FilterType.Heal);
                        break;
                    }
                case "�`���[�W":
                    {
                        _currentFilters.Remove(FilterType.Charge);
                        break;
                    }
                case "���J�o�[":
                    {
                        _currentFilters.Remove(FilterType.Recover);
                        break;
                    }
                case "�J�E���^�[":
                    {
                        _currentFilters.Remove(FilterType.Counter);
                        break;
                    }
                default:
                    {
                        throw new UnreachableException("Unreachable");
                    }
            }
            if (prevCount == _currentFilters.Count) return;
            foreach (var memoria in Memoria
                    .List
                    .Where(memoria => !Pool.Contains(memoria))
                    .Where(memoria => !Deck.Concat(LegendaryDeck).Select(m => m.Memoria.Name).Contains(memoria.Name))
                    .Where(ApplyFilter))
            {
                Pool.Add(memoria);
            }
            Sort(SortOption.SelectedIndex);
        }
        private void InitFilters()
        {
            Filters.Add(FilterType.Legendary, memoria => memoria.IsLegendary);
            Filters.Add(FilterType.NormalSingle, memoria => memoria.Kind is Vanguard(VanguardKind.NormalSingle));
            Filters.Add(FilterType.NormalRange, memoria => memoria.Kind is Vanguard(VanguardKind.NormalRange));
            Filters.Add(FilterType.SpecialSingle, memoria => memoria.Kind is Vanguard(VanguardKind.SpecialSingle));
            Filters.Add(FilterType.SpecialRange, memoria => memoria.Kind is Vanguard(VanguardKind.SpecialRange));
            Filters.Add(FilterType.Support, memoria => memoria.Kind is Rearguard(RearguardKind.Support));
            Filters.Add(FilterType.Interference, memoria => memoria.Kind is Rearguard(RearguardKind.Interference));
            Filters.Add(FilterType.Recovery, memoria => memoria.Kind is Rearguard(RearguardKind.Recovery));

            Filters.Add(FilterType.Fire, memoria => memoria.Element is Element.Fire);
            Filters.Add(FilterType.Water, memoria => memoria.Element is Element.Water);
            Filters.Add(FilterType.Wind, memoria => memoria.Element is Element.Wind);
            Filters.Add(FilterType.Light, memoria => memoria.Element is Element.Light);
            Filters.Add(FilterType.Dark, memoria => memoria.Element is Element.Dark);

            Filters.Add(FilterType.A, memoria => memoria.Skill.Range is Domain.Range.A);
            Filters.Add(FilterType.B, memoria => memoria.Skill.Range is Domain.Range.B);
            Filters.Add(FilterType.C, memoria => memoria.Skill.Range is Domain.Range.C);
            Filters.Add(FilterType.D, memoria => memoria.Skill.Range is Domain.Range.D);
            Filters.Add(FilterType.E, memoria => memoria.Skill.Range is Domain.Range.E);

            Filters.Add(FilterType.One, memoria => memoria.Skill.Level is Level.One);
            Filters.Add(FilterType.Two, memoria => memoria.Skill.Level is Level.Two);
            Filters.Add(FilterType.Three, memoria => memoria.Skill.Level is Level.Three);
            Filters.Add(FilterType.ThreePlus, memoria => memoria.Skill.Level is Level.ThreePlus);
            Filters.Add(FilterType.Four, memoria => memoria.Skill.Level is Level.Four);
            Filters.Add(FilterType.FourPlus, memoria => memoria.Skill.Level is Level.FourPlus);
            Filters.Add(FilterType.Five, memoria => memoria.Skill.Level is Level.Five);
            Filters.Add(FilterType.FivePlus, memoria => memoria.Skill.Level is Level.FivePlus);
            Filters.Add(FilterType.Lg, memoria => memoria.Skill.Level is Level.Lg);
            Filters.Add(FilterType.LgPlus, memoria => memoria.Skill.Level is Level.LgPlus);

            Filters.Add(
                FilterType.Au,
                memoria => memoria.Skill.StatusChanges.Any(stat => stat is StatusUp && stat.As<StatusUp>().Stat is Atk)
            );
            Filters.Add(
               FilterType.Ad,
               memoria => memoria.Skill.StatusChanges.Any(stat => stat is StatusDown && stat.As<StatusDown>().Stat is Atk)
            );
            Filters.Add(
               FilterType.SAu,
               memoria => memoria.Skill.StatusChanges.Any(stat => stat is StatusUp && stat.As<StatusUp>().Stat is SpAtk)
            );
            Filters.Add(
                FilterType.SAd,
                memoria => memoria.Skill.StatusChanges.Any(stat => stat is StatusDown && stat.As<StatusDown>().Stat is SpAtk)
            );
            Filters.Add(
                FilterType.Du,
                memoria => memoria.Skill.StatusChanges.Any(stat => stat is StatusUp && stat.As<StatusUp>().Stat is Def)
            );
            Filters.Add(
                FilterType.Dd,
                memoria => memoria.Skill.StatusChanges.Any(stat => stat is StatusDown && stat.As<StatusDown>().Stat is Def)
            );
            Filters.Add(
                FilterType.SDu,
                memoria => memoria.Skill.StatusChanges.Any(stat => stat is StatusUp && stat.As<StatusUp>().Stat is SpDef)
            );
            Filters.Add(
                FilterType.SDd,
                memoria => memoria.Skill.StatusChanges.Any(stat => stat is StatusDown && stat.As<StatusDown>().Stat is SpDef)
            );
            Filters.Add(
                FilterType.HPu,
                memoria => memoria.Skill.StatusChanges.Any(stat => stat is StatusUp && stat.As<StatusUp>().Stat is Life)
            );
            Filters.Add(
                FilterType.FPu,
                memoria => memoria
                    .Skill
                    .StatusChanges
                    .Any(stat =>
                        stat is StatusUp(ElementAttack(Element.Fire), _)
                    )
            );
            Filters.Add(
                FilterType.FPd,
                memoria => memoria
                    .Skill
                    .StatusChanges
                    .Any(stat =>
                        stat is StatusDown(ElementAttack(Element.Fire), _)
                    )
            );
            Filters.Add(
                FilterType.WaPu,
                memoria => memoria
                    .Skill
                    .StatusChanges
                    .Any(stat =>
                        stat is StatusUp(ElementAttack(Element.Water), _)
                    )
            );
            Filters.Add(
                FilterType.WaPd,
                memoria => memoria
                    .Skill
                    .StatusChanges
                    .Any(stat =>
                        stat is StatusDown(ElementAttack(Element.Water), _)
                    )
            );
            Filters.Add(
                FilterType.WiPu,
                memoria => memoria
                    .Skill
                    .StatusChanges
                    .Any(stat =>
                        stat is StatusUp(ElementAttack(Element.Wind), _)
                    )
            );
            Filters.Add(
                FilterType.WiPd,
                memoria => memoria
                    .Skill
                    .StatusChanges
                    .Any(stat =>
                        stat is StatusDown(ElementAttack(Element.Wind), _)
                    )
            );
            Filters.Add(
                FilterType.LPu,
                memoria => memoria
                    .Skill
                    .StatusChanges
                    .Any(stat =>
                        stat is StatusUp(ElementAttack(Element.Light), _)
                    )
            );
            Filters.Add(
                FilterType.LPd,
                memoria => memoria
                    .Skill
                    .StatusChanges
                    .Any(stat =>
                        stat is StatusDown(ElementAttack(Element.Light), _)
                    )
            );
            Filters.Add(
                FilterType.DPu,
                memoria => memoria
                    .Skill
                    .StatusChanges
                    .Any(stat =>
                        stat is StatusUp(ElementAttack(Element.Dark), _)
                    )
            );
            Filters.Add(
                FilterType.DPd,
                memoria => memoria
                    .Skill
                    .StatusChanges
                    .Any(stat =>
                        stat is StatusDown(ElementAttack(Element.Dark), _)
                    )
            );
            Filters.Add(
                FilterType.FGu,
                memoria => memoria
                    .Skill
                    .StatusChanges
                    .Any(stat =>
                        stat is StatusUp(ElementGuard(Element.Fire), _)
                    )
            );
            Filters.Add(
                FilterType.FGd,
                memoria => memoria
                    .Skill
                    .StatusChanges
                    .Any(stat =>
                        stat is StatusDown(ElementGuard(Element.Fire), _)
                    )
            );
            Filters.Add(
                FilterType.WaGu,
                memoria => memoria
                    .Skill
                    .StatusChanges
                    .Any(stat => stat is StatusUp(ElementGuard(Element.Water), _))
            );
            Filters.Add(
                FilterType.WaGd,
                memoria => memoria
                    .Skill
                    .StatusChanges
                    .Any(stat =>
                        stat is StatusDown(ElementGuard(Element.Water), _)
                    )
            );
            Filters.Add(
                FilterType.WiGu,
                memoria => memoria
                    .Skill
                    .StatusChanges
                    .Any(stat =>
                        stat is StatusUp(ElementGuard(Element.Wind), _)
                    )
            );
            Filters.Add(
                FilterType.WiGd,
                memoria => memoria
                    .Skill
                    .StatusChanges
                    .Any(stat =>
                        stat is StatusDown(ElementGuard(Element.Wind), _)
                    )
            );
            Filters.Add(
                FilterType.LGu,
                memoria => memoria
                    .Skill
                    .StatusChanges
                    .Any(stat =>
                        stat is StatusUp(ElementGuard(Element.Light), _)
                    )
            );
            Filters.Add(
                FilterType.LGd,
                memoria => memoria
                    .Skill
                    .StatusChanges
                    .Any(stat =>
                        stat is StatusDown(ElementGuard(Element.Light), _)
                    )
            );
            Filters.Add(
                FilterType.DGu,
                memoria => memoria
                    .Skill
                    .StatusChanges
                    .Any(stat =>
                        stat is StatusUp(ElementGuard(Element.Dark), _)
                    )
            );
            Filters.Add(
                FilterType.DGd,
                memoria => memoria
                    .Skill
                    .StatusChanges
                    .Any(stat =>
                        stat is StatusDown(ElementGuard(Element.Dark), _)
                    )
            );
            // ����
            Filters.Add(
                FilterType.FireStimulation,
                memoria => memoria
                    .Skill
                    .Effects
                    .Any(eff =>
                        eff is ElementStimulation(Element.Fire)
                    )
            );
            Filters.Add(
                FilterType.WaterStimulation,
                memoria => memoria
                    .Skill
                    .Effects
                    .Any(eff =>
                        eff is ElementStimulation(Element.Water)
                    )
            );
            Filters.Add(
                FilterType.WindStimulation,
                memoria => memoria
                    .Skill
                    .Effects
                    .Any(eff =>
                        eff is ElementStimulation(Element.Wind)
                    )
            );
            Filters.Add(
                FilterType.LightStimulation,
                memoria => memoria
                    .Skill
                    .Effects
                    .Any(eff =>
                        eff is ElementStimulation(Element.Light)
                    )
            );
            Filters.Add(
                FilterType.DarkStimulation,
                memoria => memoria
                    .Skill
                    .Effects
                    .Any(eff =>
                        eff is ElementStimulation(Element.Dark)
                    )
            );
            // ����
            Filters.Add(
                FilterType.FireStrong,
                memoria => memoria
                    .Skill
                    .Effects
                    .Any(eff =>
                        eff is ElementStrengthen(Element.Fire)
                    )
            );
            Filters.Add(
                FilterType.WaterStrong,
                memoria => memoria
                    .Skill
                    .Effects
                    .Any(eff =>
                        eff is ElementStrengthen(Element.Water)
                    )
            );
            Filters.Add(
                FilterType.WindStrong,
                memoria => memoria
                    .Skill
                    .Effects
                    .Any(eff =>
                        eff is ElementStrengthen(Element.Wind)
                    )
            );
            // ����
            Filters.Add(
               FilterType.FireWeak,
               memoria => memoria
                    .Skill
                    .Effects
                    .Any(eff =>
                        eff is ElementWeaken(Element.Fire)
                    )
            );
            Filters.Add(
               FilterType.WaterWeak,
               memoria => memoria
                    .Skill
                    .Effects
                    .Any(eff =>
                        eff is ElementWeaken(Element.Water)
                    )
            );
            Filters.Add(
               FilterType.WindWeak,
               memoria => memoria
                    .Skill
                    .Effects
                    .Any(eff =>
                        eff is ElementWeaken(Element.Wind)
                    )
            );
            // ���g
            Filters.Add(
                FilterType.FireSpread,
                memoria => memoria
                    .Skill
                    .Effects
                    .Any(eff =>
                        eff is ElementSpread(Element.Fire)
                    )
            );
            Filters.Add(
                FilterType.WaterSpread,
                memoria => memoria
                    .Skill
                    .Effects
                    .Any(eff =>
                        eff is ElementSpread(Element.Water)
                    )
            );
            Filters.Add(
                FilterType.WindSpread,
                memoria => memoria
                    .Skill
                    .Effects
                    .Any(eff =>
                        eff is ElementSpread(Element.Wind)
                    )
            );
            // �q�[��
            Filters.Add(FilterType.Heal,�@memoria => memoria.Skill.Effects.Any(eff =>�@eff is Heal));
            // �`���[�W
            Filters.Add(FilterType.Charge, memoria => memoria.Skill.Effects.Any(eff => eff is Charge));
            // ���J�o�[
            Filters.Add(FilterType.Recover, memoria => memoria.Skill.Effects.Any(eff => eff is Recover));
            // �J�E���^�[
            Filters.Add(FilterType.Counter, memoria => memoria.Skill.Effects.Any(eff => eff is Counter));

            // push all FilterTypes to _currentFilters
            foreach (var type in Enum.GetValues(typeof(FilterType)).Cast<FilterType>().Where(f => !IsSearchOption(f)))
            {
                _currentFilters.Add(type);
            }
        }

        bool ApplyFilter(Memoria memoria)
        {
            var p0 = _currentFilters.Where(IsKindFilter).Any(key => Filters[key](memoria));
            var p1 = _currentFilters.Where(IsElementFilter).Any(key => Filters[key](memoria));
            var p2 = _currentFilters.Where(IsRangeFilter).Any(key => Filters[key](memoria));
            var p3 = _currentFilters.Where(IsLevelFilter).Any(key => Filters[key](memoria));
            var p4 = _currentFilters.Where(IsSearchOption).All(key => Filters[key](memoria));
            return p0 && p1 && p2 && p3 && p4;
        }

        bool IsKindFilter(FilterType filter)
        {
            FilterType[] kindFilters = [
                FilterType.NormalSingle,
                FilterType.NormalRange,
                FilterType.SpecialSingle,
                FilterType.SpecialRange,
                FilterType.Support,
                FilterType.Interference,
                FilterType.Recovery,
            ];

            return kindFilters.Contains(filter);
        }
        bool IsElementFilter(FilterType filter)
        {
            FilterType[] elementFilters = [
                FilterType.Fire,
                FilterType.Water,
                FilterType.Wind,
                FilterType.Light,
                FilterType.Dark,
            ];

            return elementFilters.Contains(filter);
        }
        bool IsRangeFilter(FilterType filter)
        {
            FilterType[] rangeFilters = [
                FilterType.A,
                FilterType.B,
                FilterType.C,
                FilterType.D,
                FilterType.E,
            ];

            return rangeFilters.Contains(filter);
        }
        bool IsLevelFilter(FilterType filter)
        {
            FilterType[] LevelFilters = [
                FilterType.One,
                FilterType.Two,
                FilterType.Three,
                FilterType.ThreePlus,
                FilterType.Four,
                FilterType.FourPlus,
                FilterType.Five,
                FilterType.FivePlus,
                FilterType.Lg,
                FilterType.LgPlus,
            ];

            return LevelFilters.Contains(filter);
        }
        bool IsSearchOption(FilterType filter)
        {
            FilterType[] effectFilters = [
                FilterType.Legendary,
                FilterType.Au,
                FilterType.Ad,
                FilterType.SAu,
                FilterType.SAd,
                FilterType.Du,
                FilterType.Dd,
                FilterType.SDu,
                FilterType.SDd,
                FilterType.HPu,
                FilterType.FPu,
                FilterType.FPd,
                FilterType.WaPu,
                FilterType.WaPd,
                FilterType.WiPu,
                FilterType.WiPd,
                FilterType.LPu,
                FilterType.LPd,
                FilterType.DPu,
                FilterType.DPd,
                FilterType.FGu,
                FilterType.FGd,
                FilterType.WaGu,
                FilterType.WaGd,
                FilterType.WiGu,
                FilterType.WiGd,
                FilterType.LGu,
                FilterType.LGd,
                FilterType.DGu,
                FilterType.DGd,
                FilterType.FireStimulation,
                FilterType.WaterStimulation,
                FilterType.WindStimulation,
                FilterType.LightStimulation,
                FilterType.DarkStimulation,
                FilterType.FireStrong,
                FilterType.WaterStrong,
                FilterType.WindStrong,
                FilterType.FireWeak,
                FilterType.WaterWeak,
                FilterType.WindWeak,
                FilterType.FireSpread,
                FilterType.WaterSpread,
                FilterType.WindSpread,
                FilterType.Heal,
                FilterType.Charge,
                FilterType.Recover,
                FilterType.Counter,
            ];

            return effectFilters.Contains(filter);
        }

        private void Sort(int option)
        {
            switch (option)
            {
                case 0:
                    BuilderPageHelpers.Sort(Pool, (a, b) => b.Id.CompareTo(a.Id));
                    break;
                case 1:
                    BuilderPageHelpers.Sort(Pool, (a, b) => b.Status[4].Atk.CompareTo(a.Status[4].Atk));
                    break;
                case 2:
                    BuilderPageHelpers.Sort(Pool, (a, b) => b.Status[4].SpAtk.CompareTo(a.Status[4].SpAtk));
                    break;
                case 3:
                    BuilderPageHelpers.Sort(Pool, (a, b) => b.Status[4].Def.CompareTo(a.Status[4].Def));
                    break;
                case 4:
                    BuilderPageHelpers.Sort(Pool, (a, b) => b.Status[4].SpDef.CompareTo(a.Status[4].SpDef));
                    break;
                case 5:
                    BuilderPageHelpers.Sort(Pool, (a, b) => b.Status[4].ASA.CompareTo(a.Status[4].ASA));
                    break;
                case 6:
                    BuilderPageHelpers.Sort(Pool, (a, b) => b.Status[4].DSD.CompareTo(a.Status[4].DSD));
                    break;
            }
        }

        private void Sort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is RadioButtons rb)
            {
                Sort(rb.SelectedIndex);
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var member = MemberSelect.SelectedItem.As<MemberInfo>();
            if (Deck.Count == 0)
            {
                GeneralInfoBar.Title = "�K�{�g������܂���B�Œ�1���̃������A��Ґ����Ă��������B";
                GeneralInfoBar.IsOpen = true;
                GeneralInfoBar.Severity = InfoBarSeverity.Error;
                await Task.Delay(3000);
                GeneralInfoBar.IsOpen = false;
                return;
            }
            if (LegendaryDeck.Count > 4 || Deck.Count > 20)
            {
                GeneralInfoBar.Title = "�f�b�L��20���܂łł��i���W�F���_���[��4���܂łł��j�B";
                GeneralInfoBar.IsOpen = true;
                GeneralInfoBar.Severity = InfoBarSeverity.Error;
                await Task.Delay(3000);
                GeneralInfoBar.IsOpen = false;
                return;
            }
            if (Deck.Count == 0)
            {
                GeneralInfoBar.Title = "�K�{�����g������܂���B�Œ�1���͕Ґ����Ă��������B";
                GeneralInfoBar.IsOpen = true;
                GeneralInfoBar.Severity = InfoBarSeverity.Error;
                await Task.Delay(3000);
                GeneralInfoBar.IsOpen = false;
                return;
            }
            else
            {
                var name = DeckName.Text;
                new DirectoryInfo($@"{Director.ProjectDir()}\{region}\Members\{member.Name}\Units").Create();
                var path = $@"{Director.ProjectDir()}\{region}\Members\{member.Name}\Units\{name}.json";
                using var unit = File.Create(path);
                await unit.WriteAsync(new UTF8Encoding(true).GetBytes(
                    new Unit(name, member.Position is Front, [.. Deck, .. LegendaryDeck]).ToJson()));
                GeneralInfoBar.Title = "�ۑ����܂���";
                GeneralInfoBar.IsOpen = true;
                GeneralInfoBar.Severity = InfoBarSeverity.Informational;
                await Task.Delay(3000);
                GeneralInfoBar.IsOpen = false;
            }
        }

        private async void GenerateLink_Click(object _sender, RoutedEventArgs _e)
        {
            var legendary = string.Join(",", LegendaryDeck.Select(m => m.Memoria.ToJson()));
            var deck = string.Join(",", Deck.Select(m => m.Memoria.ToJson()));
            var json = $"{{ \"legendary\":[{legendary}],\"deck\": [{deck}] }}";
            var jsonBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            // copy to clipboard
            System.Windows.Clipboard.SetText($"http://mitama.tech/deck/?json={jsonBase64}");
            GeneralInfoBar.Title = "�N���b�v�{�[�h�Ƀ����N���R�s�[���܂���";
            GeneralInfoBar.Severity = InfoBarSeverity.Success;
            GeneralInfoBar.IsOpen = true;
            await Task.Delay(3000);
            GeneralInfoBar.IsOpen = false;
        }

        private void LoadMemberSelect_SelectionChanged(object sender, SelectionChangedEventArgs _)
        {
            var units = Util.LoadUnitNames(_regionName, sender.As<ComboBox>().SelectedItem.As<MemberInfo>().Name);
            DeckSelect.ItemsSource = units;
        }

        private async void LoadButton_Click(object _, RoutedEventArgs _e)
        {
            var name = LoadMemberSelect.SelectedItem.As<MemberInfo>().Name;
            var deck = DeckSelect.SelectedItem.As<string>();
            var path = $@"{Director.ProjectDir()}\{region}\Members\{name}\Units\{deck}.json";
            using var sr = new StreamReader(path);
            var json = sr.ReadToEnd();
            var (isLegacy, unit) = Unit.FromJson(json);
            if (isLegacy)
            {
                File.WriteAllBytes(path, new UTF8Encoding(true).GetBytes(unit.ToJson()));
            }
            if (unit.IsFront != Switch.IsOn)
            {
                GeneralInfoBar.Title = "�O�q���[�h�Ō�q�Ґ������[�h���邱�Ɓi�܂��͂��̋t�j�͂ł��܂���I";
                GeneralInfoBar.IsOpen = true;
                GeneralInfoBar.Severity = InfoBarSeverity.Error;
                await Task.Delay(3000);
                GeneralInfoBar.IsOpen = false;
                return;
            }
            LegendaryDeck.Clear();
            Deck.Clear();
            foreach (var memoria in unit.Memorias.Where(m => m.Memoria.IsLegendary))
            {
                LegendaryDeck.Add(memoria);
            }
            foreach (var memoria in unit.Memorias.Where(m => !m.Memoria.IsLegendary))
            {
                Deck.Add(memoria);
            }
        }

        private void Concentration_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender.As<ComboBox>();
            if (comboBox.AccessKey == string.Empty) return;
            var id = int.Parse(comboBox.AccessKey);
            foreach (var item in Deck.ToList())
            {
                if (item.Memoria.Id == id)
                {
                    var newItem = item with { Concentration = comboBox.SelectedIndex };
                    var idx = Deck.IndexOf(item);
                    Deck[idx] = newItem;
                }
            }
        }

        private void Import_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void Import_Drop(object sender, DragEventArgs e)
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

            var items = await e.DataView.GetStorageItemsAsync();

            if (items.Count <= 0) return;

            try
            {
                using var img = new System.Drawing.Bitmap(items[0].As<StorageFile>()!.Path);
                var (result, detected) = await Match.Recognise(img, Switch.IsOn);
                LegendaryDeck.Clear();
                Deck.Clear();
                foreach (var memoria in detected.Where(m => m.IsLegendary))
                {
                    LegendaryDeck.Add(new MemoriaWithConcentration(memoria, 4));
                }
                foreach (var memoria in detected.Where(m => !m.IsLegendary))
                {
                    Deck.Add(new MemoriaWithConcentration(memoria, 4));
                }
            }
            catch (Exception ex)
            {
                var dialog = new DialogBuilder(XamlRoot)
                    .WithTitle("�Ǎ����s")
                    .WithPrimary("OK")
                    .WithBody(new TextBlock
                    {
                        Text = ex.ToString()
                    })
                    .Build();

                await dialog.ShowAsync();
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var content = new Grid {
                AllowDrop = true,
                MinHeight = 300,
                MinWidth = 300,
                // LightBlue
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xAD, 0xD8, 0xE6)),
                Children =
                {
                    new TextBlock
                    {
                        Text = "�����ɉ摜���h���b�O���h���b�v���Ă�������",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 24,
                        FontWeight = FontWeights.Bold,
                    }
                }
            };

            content.DragOver += Import_DragOver;
            content.Drop += Import_Drop;

            var dialog = new DialogBuilder(XamlRoot)
                .WithTitle("�ǂݍ���")
                .WithCancel("����")
                .WithBody(content)
                .Build();

            await dialog.ShowAsync();
        }
    }

    public enum FilterType
    {
        // Kinds
        Legendary,
        NormalSingle,
        NormalRange,
        SpecialSingle,
        SpecialRange,
        Support,
        Interference,
        Recovery,
        // Elements
        Fire,
        Water,
        Wind,
        Light,
        Dark,
        // Ranges
        A,
        B,
        C,
        D,
        E,
        // Skill Levels
        One,
        Two,
        Three,
        ThreePlus,
        Four,
        FourPlus,
        Five,
        FivePlus,
        Lg,
        LgPlus,
        // Skill Effects (Status Changes)
        Au,
        Ad,
        SAu,
        SAd,
        Du,
        Dd,
        SDu,
        SDd,
        HPu,
        FPu,
        FPd,
        WaPu,
        WaPd,
        WiPu,
        WiPd,
        LPu,
        LPd,
        DPu,
        DPd,
        FGu,
        FGd,
        WaGu,
        WaGd,
        WiGu,
        WiGd,
        LGu,
        LGd,
        DGu,
        DGd,
        // Other Skill Effects
        FireStimulation,
        WaterStimulation,
        WindStimulation,
        LightStimulation,
        DarkStimulation,
        FireStrong,
        WaterStrong,
        WindStrong,
        FireWeak,
        WaterWeak,
        WindWeak,
        FireSpread,
        WaterSpread,
        WindSpread,
        Heal,
        Charge,
        Recover,
        Counter,
    }

    public enum SkillType
    {
        Au,
        Ad,
        SAu,
        SAd,
        Du,
        Dd,
        SDu,
        SDd,
        HPu,
        FPu,
        FPd,
        WaPu,
        WaPd,
        WiPu,
        WiPd,
        LPu,
        LPd,
        DPu,
        DPd,
        FGu,
        FGd,
        WaGu,
        WaGd,
        WiGu,
        WiGd,
        LGu,
        LGd,
        DGu,
        DGd,
        Other,
    }

    public enum SupportType
    {
        NormalMatchPtUp,
        SpecialMatchPtUp,
        DamageUp,
        PowerUp,
        PowerDown,
        GuardUp,
        GuardDown,
        SpPowerUp,
        SpPowerDown,
        SpGuardUp,
        SpGuardDown,
        FirePowerUp,
        WaterPowerUp,
        WindPowerUp,
        FirePowerDown,
        WaterPowerDown,
        WindPowerDown,
        FireGuardUp,
        WaterGuardUp,
        WindGuardUp,
        FireGuardDown,
        WaterGuardDown,
        WindGuardDown,
        SupportUp,
        RecoveryUp, 
        MpCostDown,
    }

    public enum KindType
    {
        NormalSingle,
        NormalRange,
        SpecialSingle,
        SpecialRange,
        Support,
        Interference,
        Recovery,
    }

    public class MyTreeNode
    {
        public string Text { get; set; } = string.Empty;
        public bool IsChecked { get; set; } = true;

        public ObservableCollection<MyTreeNode> Children { get; set; } = [];
    }

    public class SupportBreakdown
    {
        public SupportType Type { get; set; }
        public Dictionary<Level, int> Breakdown { get; set; } = [];
        public ObservableCollection<BreakdownItem> Data => new(Breakdown.Select(p => new BreakdownItem(p)));
        public int Total => Breakdown.Values.Sum();
        public string Content => $"{BuilderPageHelpers.SupportTypeToString(Type)}: {Total}";
    }

    public class BreakdownItem(KeyValuePair<Level, int> pair)
    {
        public Level Level { get; set; } = pair.Key;
        public int Value { get; set; } = pair.Value;

        public string Content => $"{BuilderPageHelpers.LevelToString(Level)}: {Value}";
    }

    public record MemoriaWithConcentration(Memoria Memoria, int Concentration)
    {
        public Memoria Memoria { get; set; } = Memoria;
        public int Concentration { get; set; } = Concentration;

        public int FontSize => Concentration switch
        {
            4 => 12,
            _ => 18,
        };

        public Thickness Margin => Concentration switch
        { 
            4 => new(0, 30, 2, 0),
            _ => new(0, 26, -4, 0) 
        };

        public string LimitBreak => Concentration switch
        {
            0 => "0",
            1 => "1",
            2 => "2",
            3 => "3",
            4 => "MAX",
            _ => throw new UnreachableException("Unreachable"),
        };

        public static implicit operator Memoria(MemoriaWithConcentration m) => m.Memoria;

        public BasicStatus Status => Memoria.Status[Concentration];

        bool IEquatable<MemoriaWithConcentration>.Equals(MemoriaWithConcentration other)
            => other is not null
            && Memoria.Id == other.Memoria.Id
            && Concentration == other.Concentration;
    }
}
