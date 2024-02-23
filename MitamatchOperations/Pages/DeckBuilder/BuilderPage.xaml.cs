using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using static mitama.Pages.Common.ObservableCollectionExtensions;
using mitama.Models;

namespace mitama.Pages.DeckBuilder
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class BuilderPage : Page
    {
        // CheckBoxView
        private CheckBoxView FilterOptionView { get; set; }
        private CheckBoxView SkillSearchOptionView { get; set; }
        // Temporarily store the selected Memoria
        private List<Memoria> selectedMemorias = [];

        private ObservableCollection<MemoriaWithConcentration> Deck { get; set; } = [];
        private ObservableCollection<MemoriaWithConcentration> LegendaryDeck { get; set; } = [];
        private List<MemoriaWithConcentration> OriginalPool { get; set; } = [.. Memoria.List.Select(x => new MemoriaWithConcentration(x, 4))];
        private ObservableCollection<MemoriaWithConcentration> Pool { get; set; } = [.. Memoria.List.Select(x => new MemoriaWithConcentration(x, 4))];
        private HashSet<FilterType> _currentFilters = [];
        private string region = "";
        private MemberInfo[] members = [];
        readonly Dictionary<KindType, int> kindPairs = [];
        readonly Dictionary<SkillType, int> skillPairs = [];
        readonly Dictionary<SupportType, SupportBreakdown> supportPairs = [];
        private readonly Dictionary<FilterType, Func<Memoria, bool>> Filters = [];
        private readonly string _regionName;
        private readonly ObservableCollection<SupportBreakdown> SupportBreakdowns = [];
        private readonly ObservableCollection<string> TargetMembers = ["All"];

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
            SkillSearchOptionView = new(new Dictionary<string, Dictionary<string, string[]>>
            {
                ["���M�I���}�b�`�X�L��"] = new()
                {
                    ["��b�o�t"] = [
                        "ATK�A�b�v",
                        "ATK�_�E��",
                        "Sp.ATK�A�b�v",
                        "Sp.ATK�_�E��",
                        "DEF�A�b�v",
                        "DEF�_�E��",
                        "Sp.DEF�A�b�v",
                        "Sp.DEF�_�E��",
                    ],
                    ["����"] = [
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
                    ],
                    ["�����n"] = [
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
                    ],
                    ["���̑�"] = [
                        "HP�A�b�v",
                        "�q�[��",
                        "�`���[�W",
                        "���J�o�[",
                        "�J�E���^�[",
                    ]
                },
                ["���M�I���}�b�`�⏕�X�L��"] = new()
                {
                    ["��b�X�e�n"] = [
                        "�p���[UP",
                        "�p���[DOWN",
                        "�K�[�hUP",
                        "�K�[�hDOWN",
                        "Sp.�p���[UP",
                        "Sp.�p���[DOWN",
                        "Sp.�K�[�hUP",
                        "Sp.�K�[�hDOWN",
                    ],
                    ["�����n"] = [
                        "�΃p���[UP",
                        "���p���[UP",
                        "���p���[UP",
                        "�΃p���[DOWN",
                        "���p���[DOWN",
                        "���p���[DOWN",
                        "�΃K�[�hUP",
                        "���K�[�hUP",
                        "���K�[�hUP",
                        "�΃K�[�hDOWN",
                        "���K�[�hDOWN",
                        "���K�[�hDOWN",
                    ],
                    ["���̑�"] = [
                        "�_���[�WUP",
                        "�x��UP",
                        "��UP",
                        "���ʔ͈�+",
                        "�l���}�b�`PtUP/�ʏ�P��",
                        "�l���}�b�`PtUP/����P��",
                        "MP����DOWN",
                    ],
                },
                ["���̑�"] = new()
                {
                    ["���x��"] = [
                        "���W�F���_���[",
                        "�A���e�B���b�g",
                    ]
                },
            });
        }

        private void InitMembers()
        {
            var cache = Director.ReadCache();
            region = cache.Region;
            members = Util.LoadMembersInfo(region);
        }

        private void Memeria_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items.First() is Memoria)
            {
                selectedMemorias = e.Items.Select(v => (Memoria)v).ToList();
                e.Data.RequestedOperation = DataPackageOperation.Move;
            }
            else
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
            }
            else
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

            SupportBreakdowns.Clear();
            foreach (var (_, breakdown) in supportPairs)
            {
                SupportBreakdowns.Add(breakdown);
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
                SkillSummary.Items.Add(new Button()
                {
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
            Pool.RemoveWhere(m => selectedMemorias.Select(s => s.Name).Contains(m.Memoria.Name));
            Cleanup();
        }

        private void MemeriaSources_Drop(object sender, DragEventArgs e)
        {
            var dummyCostume = Switch.IsOn ? Costume.DummyVanguard : Costume.DummyRearguard;
            foreach (var toAdd in OriginalPool
                .Where(m => dummyCostume.CanBeEquipped(m.Memoria))
                .Where(m => selectedMemorias.Select(s => s.Name).Contains(m.Memoria.Name)))
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
                SupportBreakdowns.Clear();
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
                    Pool = new(OriginalPool.Where(m => Costume.DummyVanguard.CanBeEquipped(m.Memoria)));
                    MemoriaSources.ItemsSource = Pool;
                    FilterOptionView = new(new Dictionary<string, string[]>
                    {
                        ["���"] = ["�ʏ�P��", "�ʏ�͈�", "����P��", "����͈�"],
                        ["����"] = ["��", "��", "��", "��", "��"],
                        ["�͈�"] = ["A", "B", "C", "D", "E"],
                        ["���ʗ�"] = ["�T", "�U", "�V", "�V+", "�W", "�W+", "�X", "�X+", "LG", "LG+"],
                    });
                    _currentFilters = [
                        FilterType.NormalSingle,
                        FilterType.NormalRange,
                        FilterType.SpecialSingle,
                        FilterType.SpecialRange,
                    ];
                    foreach (var type in Enum.GetValues(typeof(FilterType)).Cast<FilterType>().Where(f => !IsKindFilter(f) && !IsSkillOption(f) && !IsSupportOption(f) && !IsOtherOption(f)))
                    {
                        _currentFilters.Add(type);
                    }
                }
                else
                {
                    VoR.Label = "��q";
                    Pool = new(OriginalPool.Where(m => Costume.DummyRearguard.CanBeEquipped(m.Memoria)));
                    FilterOptionView = new(new Dictionary<string, string[]>
                    {
                        ["���"] = ["�x��", "�W�Q", "��"],
                        ["����"] = ["��", "��", "��", "��", "��"],
                        ["�͈�"] = ["A", "B", "C", "D", "E"],
                        ["���ʗ�"] = ["�T", "�U", "�V", "�V+", "�W", "�W+", "�X", "�X+", "LG", "LG+"],
                    });
                    _currentFilters = [
                        FilterType.Support,
                        FilterType.Interference,
                        FilterType.Recovery
                    ];
                    foreach (var type in Enum.GetValues(typeof(FilterType)).Cast<FilterType>().Where(f => !IsKindFilter(f) && !IsSkillOption(f) && !IsSupportOption(f) && !IsOtherOption(f)))
                    {
                        _currentFilters.Add(type);
                    }
                    MemoriaSources.ItemsSource = Pool;
                }
                FilterView.ItemsSource = FilterOptionView.Items;
                FilterView.CheckedItems = FilterOptionView.CheckedItems;
            }
        }

        private void FilterView_NodeChecked(object _, Syncfusion.UI.Xaml.TreeView.NodeCheckedEventArgs e)
        {
            switch (e.Node.Content.As<CheckBoxModel>().State)
            {
                case "���":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            FilterType[] types = Switch.IsOn
                                ? [FilterType.NormalSingle, FilterType.NormalRange, FilterType.SpecialSingle, FilterType.SpecialRange]
                                : [FilterType.Support, FilterType.Interference, FilterType.Recovery];
                            foreach (var type in types)
                            {
                                _currentFilters.Add(type);
                            }
                        }
                        else
                        {
                            _currentFilters.RemoveWhere(IsKindFilter);
                        }
                        break;
                    }
                case "����":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            foreach (var type in Enum.GetValues(typeof(FilterType)).Cast<FilterType>().Where(IsElementFilter))
                            {
                                _currentFilters.Add(type);
                            }
                        }
                        else
                        {
                            _currentFilters.RemoveWhere(IsElementFilter);
                        }
                        break;
                    }
                case "�͈�":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            foreach (var type in Enum.GetValues(typeof(FilterType)).Cast<FilterType>().Where(IsRangeFilter))
                            {
                                _currentFilters.Add(type);
                            }
                        }
                        else
                        {
                            _currentFilters.RemoveWhere(IsRangeFilter);
                        }
                        break;
                    }
                case "���ʗ�":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            foreach (var type in Enum.GetValues(typeof(FilterType)).Cast<FilterType>().Where(IsLevelFilter))
                            {
                                _currentFilters.Add(type);
                            }
                        }
                        else
                        {
                            _currentFilters.RemoveWhere(IsLevelFilter);
                        }
                        break;
                    }
                case "�ʏ�P��":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.NormalSingle);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.NormalSingle);
                        }
                        break;
                    }
                case "�ʏ�͈�":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.NormalRange);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.NormalRange);
                        }
                        break;
                    }
                case "����P��":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.SpecialSingle);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.SpecialSingle);
                        }
                        break;
                    }
                case "����͈�":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.SpecialRange);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.SpecialRange);
                        }
                        break;
                    }
                case "�x��":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.Support);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.Support);
                        }
                        break;
                    }
                case "�W�Q":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.Interference);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.Interference);
                        }
                        break;
                    }
                case "��":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.Recovery);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.Recovery);
                        }
                        break;
                    }
                default: break;
            }
            if ((bool)e.Node.IsChecked)
            {
                foreach (var memoria in OriginalPool
                        .Where(memoria => !Pool.Contains(memoria))
                        .Where(memoria => !Deck.Concat(LegendaryDeck).Select(m => m.Memoria.Name).Contains(memoria.Memoria.Name))
                        .Where(ApplyFilter))
                {
                    Pool.Add(memoria);
                }
            }
            else
            {
                foreach (var memoria in Pool.ToList().Where(m => !ApplyFilter(m)))
                {
                    Pool.Remove(memoria);
                }
            }
            Sort(SortOption.SelectedIndex);
        }

        private void InitFilterOptions()
        {
            FilterOptionView = new(new Dictionary<string, string[]>
            {
                ["���"] = ["�x��", "�W�Q", "��"],
                ["����"] = ["��", "��", "��", "��", "��"],
                ["�͈�"] = ["A", "B", "C", "D", "E"],
                ["���ʗ�"] = ["�T", "�U", "�V", "�V+", "�W", "�W+", "�X", "�X+", "LG", "LG+"],
            });
            _currentFilters = [
                FilterType.Support,
                FilterType.Interference,
                FilterType.Recovery
            ];
            foreach (var type in Enum.GetValues(typeof(FilterType)).Cast<FilterType>().Where(f => !IsKindFilter(f) && !IsSkillOption(f) && !IsSupportOption(f) && !IsOtherOption(f)))
            {
                _currentFilters.Add(type);
            }
        }

        private void SkillSearchView_NodeChecked(object _, Syncfusion.UI.Xaml.TreeView.NodeCheckedEventArgs e)
        {
            if (e.Node.HasChildNodes) return;

            switch (e.Node.Content.As<CheckBoxModel>().State)
            {
                case "ATK�A�b�v":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.Au);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.Au);
                        }
                        break;
                    }
                case "ATK�_�E��":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.Ad);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.Ad);
                        }
                        break;
                    }
                case "Sp.ATK�A�b�v":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.SAu);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.SAu);
                        }
                        break;
                    }
                case "Sp.ATK�_�E��":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.SAd);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.SAd);
                        }
                        break;
                    }
                case "DEF�A�b�v":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.Du);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.Du);
                        }
                        break;
                    }
                case "DEF�_�E��":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.Dd);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.Dd);
                        }
                        break;
                    }
                case "Sp.DEF�A�b�v":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.SDu);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.SDu);
                        }
                        break;
                    }
                case "Sp.DEF�_�E��":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.SDd);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.SDd);
                        }
                        break;
                    }
                case "�Α����U���̓A�b�v":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.FPu);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.FPu);
                        }
                        break;
                    }
                case "�Α����U���̓_�E��":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.FPd);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.FPd);
                        }
                        break;
                    }
                case "�������U���̓A�b�v":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.WaPu);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.WaPu);
                        }
                        break;
                    }
                case "�������U���̓_�E��":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.WaPd);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.WaPd);
                        }
                        break;
                    }
                case "�������U���̓A�b�v":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.WiPu);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.WiPu);
                        }
                        break;
                    }
                case "�������U���̓_�E��":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.WiPd);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.WiPd);
                        }
                        break;
                    }
                case "�������U���̓A�b�v":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.LPu);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.LPu);
                        }
                        break;
                    }
                case "�������U���̓_�E��":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.LPd);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.LPd);
                        }
                        break;
                    }
                case "�ő����U���̓A�b�v":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.DPu);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.DPu);
                        }
                        break;
                    }
                case "�ő����U���̓_�E��":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.DPd);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.DPd);
                        }
                        break;
                    }
                case "�Ό��ʃA�b�v":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.FireStimulation);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.FireStimulation);
                        }
                        break;
                    }
                case "�����ʃA�b�v":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.WaterStimulation);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.WaterStimulation);
                        }
                        break;
                    }
                case "�����ʃA�b�v":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.WindStimulation);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.WindStimulation);
                        }
                        break;
                    }
                case "�����ʃA�b�v":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.LightStimulation);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.LightStimulation);
                        }
                        break;
                    }
                case "�Ō��ʃA�b�v":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.DarkStimulation);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.DarkStimulation);
                        }
                        break;
                    }
                case "�΋�":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.FireStrong);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.FireStrong);
                        }
                        break;
                    }
                case "����":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.WaterStrong);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.WaterStrong);
                        }
                        break;
                    }
                case "����":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.WindStrong);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.WindStrong);
                        }
                        break;
                    }
                case "�Ύ�":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.FireWeak);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.FireWeak);
                        }
                        break;
                    }
                case "����":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.WaterWeak);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.WaterWeak);
                        }
                        break;
                    }
                case "����":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.WindWeak);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.WindWeak);
                        }
                        break;
                    }
                case "�Ίg":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.FireSpread);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.FireSpread);
                        }
                        break;
                    }
                case "���g":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.WaterSpread);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.WaterSpread);
                        }
                        break;
                    }
                case "���g":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.WindSpread);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.WindSpread);
                        }
                        break;
                    }
                case "HP�A�b�v":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.HPu);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.HPu);
                        }
                        break;
                    }
                case "�q�[��":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.Heal);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.Heal);
                        }
                        break;
                    }
                case "�`���[�W":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.Charge);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.Charge);
                        }
                        break;
                    }
                case "���J�o�[":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.Recover);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.Recover);
                        }
                        break;
                    }
                case "�J�E���^�[":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.Counter);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.Counter);
                        }
                        break;
                    }
                case "�p���[UP":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.PowerUp);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.PowerUp);
                        }
                        break;
                    }
                case "�p���[DOWN":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.PowerDown);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.PowerDown);
                        }
                        break;
                    }
                case "�K�[�hUP":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.GuardUp);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.GuardUp);
                        }
                        break;
                    }
                case "�K�[�hDOWN":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.GuardDown);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.GuardDown);
                        }
                        break;
                    }
                case "Sp.�p���[UP":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.SpPowerUp);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.SpPowerUp);
                        }
                        break;
                    }
                case "Sp.�p���[DOWN":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.SpPowerDown);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.SpPowerDown);
                        }
                        break;
                    }
                case "Sp.�K�[�hUP":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.SpGuardUp);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.SpGuardUp);
                        }
                        break;
                    }
                case "Sp.�K�[�hDOWN":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.SpGuardDown);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.SpGuardDown);
                        }
                        break;
                    }
                case "�΃p���[UP":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.FirePowerUp);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.FirePowerUp);
                        }
                        break;
                    }
                case "���p���[UP":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.WaterPowerUp);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.WaterPowerUp);
                        }
                        break;
                    }
                case "���p���[UP":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.WindPowerUp);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.WindPowerUp);
                        }
                        break;
                    }
                case "�΃p���[DOWN":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.FirePowerDown);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.FirePowerDown);
                        }
                        break;
                    }
                case "���p���[DOWN":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.WaterPowerDown);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.WaterPowerDown);
                        }
                        break;
                    }
                case "���p���[DOWN":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.WindPowerDown);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.WindPowerDown);
                        }
                        break;
                    }
                case "�΃K�[�hUP":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.FireGuardUp);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.FireGuardUp);
                        }
                        break;
                    }
                case "���K�[�hUP":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.WaterGuardUp);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.WaterGuardUp);
                        }
                        break;
                    }
                case "���K�[�hUP":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.WindGuardUp);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.WindGuardUp);
                        }
                        break;
                    }
                case "�΃K�[�hDOWN":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.FireGuardDown);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.FireGuardDown);
                        }
                        break;
                    }
                case "���K�[�hDOWN":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.WaterGuardDown);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.WaterGuardDown);
                        }
                        break;
                    }
                case "���K�[�hDOWN":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.WindGuardDown);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.WindGuardDown);
                        }
                        break;
                    }
                case "�_���[�WUP":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.DamageUp);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.DamageUp);
                        }
                        break;
                    }
                case "�x��UP":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.SupportUp);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.SupportUp);
                        }
                        break;
                    }
                case "��UP":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.RecoveryUp);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.RecoveryUp);
                        }
                        break;
                    }
                case "���ʔ͈�+":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.RangeUp);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.RangeUp);
                        }
                        break;
                    }
                case "�l���}�b�`PtUP/�ʏ�P��":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.NormalMatchPtUp);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.NormalMatchPtUp);
                        }
                        break;
                    }
                case "�l���}�b�`PtUP/����P��":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.SpecialMatchPtUp);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.SpecialMatchPtUp);
                        }
                        break;
                    }
                case "MP����DOWN":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.MpCostDown);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.MpCostDown);
                        }
                        break;
                    }
                case "���W�F���_���[":
                    {
                        {
                            if ((bool)e.Node.IsChecked)
                            {
                                _currentFilters.Add(FilterType.Legendary);
                            }
                            else
                            {
                                _currentFilters.Remove(FilterType.Legendary);
                            }
                            break;
                        }
                    }
                case "�A���e�B���b�g":
                    {
                        if ((bool)e.Node.IsChecked)
                        {
                            _currentFilters.Add(FilterType.Ultimate);
                        }
                        else
                        {
                            _currentFilters.Remove(FilterType.Ultimate);
                        }
                        break;
                    }
                default: break;
            }
            if ((bool)e.Node.IsChecked)
            {
                foreach (var memoria in Pool.ToList().Where(m => !ApplyFilter(m)))
                {
                    Pool.Remove(memoria);
                }

            }
            else
            {
                foreach (var memoria in OriginalPool
                        .Where(memoria => !Pool.Contains(memoria))
                        .Where(memoria => !Deck.Concat(LegendaryDeck).Select(m => m.Memoria.Name).Contains(memoria.Memoria.Name))
                        .Where(ApplyFilter))
                {
                    Pool.Add(memoria);
                }
            }
            Sort(SortOption.SelectedIndex);
        }

        private void InitFilters()
        {
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
            Filters.Add(FilterType.Heal, memoria => memoria.Skill.Effects.Any(eff => eff is Heal));
            // �`���[�W
            Filters.Add(FilterType.Charge, memoria => memoria.Skill.Effects.Any(eff => eff is Charge));
            // ���J�o�[
            Filters.Add(FilterType.Recover, memoria => memoria.Skill.Effects.Any(eff => eff is Recover));
            // �J�E���^�[
            Filters.Add(FilterType.Counter, memoria => memoria.Skill.Effects.Any(eff => eff is Counter));

            // �⏕�����I�v�V����
            Filters.Add(FilterType.NormalMatchPtUp, memoria => memoria.SupportSkill.Effects.Any(eff => eff is NormalMatchPtUp));
            Filters.Add(FilterType.SpecialMatchPtUp, memoria => memoria.SupportSkill.Effects.Any(eff => eff is SpecialMatchPtUp));
            Filters.Add(FilterType.DamageUp, memoria => memoria.SupportSkill.Effects.Any(eff => eff is DamageUp));
            Filters.Add(FilterType.PowerUp, memoria => memoria.SupportSkill.Effects.Any(eff => eff is PowerUp(Domain.Type.Normal)));
            Filters.Add(FilterType.PowerDown, memoria => memoria.SupportSkill.Effects.Any(eff => eff is PowerDown(Domain.Type.Normal)));
            Filters.Add(FilterType.GuardUp, memoria => memoria.SupportSkill.Effects.Any(eff => eff is GuardUp(Domain.Type.Normal)));
            Filters.Add(FilterType.GuardDown, memoria => memoria.SupportSkill.Effects.Any(eff => eff is GuardDown(Domain.Type.Normal)));
            Filters.Add(FilterType.SpPowerUp, memoria => memoria.SupportSkill.Effects.Any(eff => eff is PowerUp(Domain.Type.Special)));
            Filters.Add(FilterType.SpPowerDown, memoria => memoria.SupportSkill.Effects.Any(eff => eff is PowerDown(Domain.Type.Special)));
            Filters.Add(FilterType.SpGuardUp, memoria => memoria.SupportSkill.Effects.Any(eff => eff is GuardUp(Domain.Type.Special)));
            Filters.Add(FilterType.SpGuardDown, memoria => memoria.SupportSkill.Effects.Any(eff => eff is GuardDown(Domain.Type.Special)));
            Filters.Add(FilterType.FirePowerUp, memoria => memoria.SupportSkill.Effects.Any(eff => eff is ElementPowerUp(Element.Fire)));
            Filters.Add(FilterType.WaterPowerUp, memoria => memoria.SupportSkill.Effects.Any(eff => eff is ElementPowerUp(Element.Water)));
            Filters.Add(FilterType.WindPowerUp, memoria => memoria.SupportSkill.Effects.Any(eff => eff is ElementPowerUp(Element.Wind)));
            Filters.Add(FilterType.FirePowerDown, memoria => memoria.SupportSkill.Effects.Any(eff => eff is ElementPowerDown(Element.Fire)));
            Filters.Add(FilterType.WaterPowerDown, memoria => memoria.SupportSkill.Effects.Any(eff => eff is ElementPowerDown(Element.Water)));
            Filters.Add(FilterType.WindPowerDown, memoria => memoria.SupportSkill.Effects.Any(eff => eff is ElementPowerDown(Element.Wind)));
            Filters.Add(FilterType.FireGuardUp, memoria => memoria.SupportSkill.Effects.Any(eff => eff is ElementGuardUp(Element.Fire)));
            Filters.Add(FilterType.WaterGuardUp, memoria => memoria.SupportSkill.Effects.Any(eff => eff is ElementGuardUp(Element.Water)));
            Filters.Add(FilterType.WindGuardUp, memoria => memoria.SupportSkill.Effects.Any(eff => eff is ElementGuardUp(Element.Wind)));
            Filters.Add(FilterType.FireGuardDown, memoria => memoria.SupportSkill.Effects.Any(eff => eff is ElementGuardDown(Element.Fire)));
            Filters.Add(FilterType.WaterGuardDown, memoria => memoria.SupportSkill.Effects.Any(eff => eff is ElementGuardDown(Element.Water)));
            Filters.Add(FilterType.WindGuardDown, memoria => memoria.SupportSkill.Effects.Any(eff => eff is ElementGuardDown(Element.Wind)));
            Filters.Add(FilterType.SupportUp, memoria => memoria.SupportSkill.Effects.Any(eff => eff is SupportUp));
            Filters.Add(FilterType.RecoveryUp, memoria => memoria.SupportSkill.Effects.Any(eff => eff is RecoveryUp));
            Filters.Add(FilterType.MpCostDown, memoria => memoria.SupportSkill.Effects.Any(eff => eff is MpCostDown));
            Filters.Add(FilterType.RangeUp, memoria => memoria.SupportSkill.Effects.Any(eff => eff is RangeUp));

            // Others
            Filters.Add(FilterType.Legendary, memoria => memoria.IsLegendary);
            Filters.Add(FilterType.Ultimate, memoria => memoria.Link.Contains("ultimate"));
        }

        bool ApplyFilter(MemoriaWithConcentration memoriaWith)
        {
            var memoria = memoriaWith.Memoria;
            var p0 = _currentFilters.Where(IsKindFilter).Any(key => Filters[key](memoria));
            var p1 = _currentFilters.Where(IsElementFilter).Any(key => Filters[key](memoria));
            var p2 = _currentFilters.Where(IsRangeFilter).Any(key => Filters[key](memoria));
            var p3 = _currentFilters.Where(IsLevelFilter).Any(key => Filters[key](memoria));
            var p4 = _currentFilters.Where(IsSkillOption).All(key => Filters[key](memoria));
            var p5 = _currentFilters.Where(IsSupportOption).All(key => Filters[key](memoria));
            var p6 = _currentFilters.Where(IsOtherOption).All(key => Filters[key](memoria));
            return p0 && p1 && p2 && p3 && p4 && p5 && p6;
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

        bool IsSkillOption(FilterType filter)
        {
            FilterType[] effectFilters = [
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

        bool IsSupportOption(FilterType type)
        {
            FilterType[] supportFilters = [
                FilterType.NormalMatchPtUp,
                FilterType.SpecialMatchPtUp,
                FilterType.DamageUp,
                FilterType.PowerUp,
                FilterType.PowerDown,
                FilterType.GuardUp,
                FilterType.GuardDown,
                FilterType.SpPowerUp,
                FilterType.SpPowerDown,
                FilterType.SpGuardUp,
                FilterType.SpGuardDown,
                FilterType.FirePowerUp,
                FilterType.WaterPowerUp,
                FilterType.WindPowerUp,
                FilterType.FirePowerDown,
                FilterType.WaterPowerDown,
                FilterType.WindPowerDown,
                FilterType.FireGuardUp,
                FilterType.WaterGuardUp,
                FilterType.WindGuardUp,
                FilterType.FireGuardDown,
                FilterType.WaterGuardDown,
                FilterType.WindGuardDown,
                FilterType.SupportUp,
                FilterType.RecoveryUp,
                FilterType.MpCostDown,
                FilterType.RangeUp,
            ];

            return supportFilters.Contains(type);
        }

        bool IsOtherOption(FilterType type)
        {
            FilterType[] otherFilters = [
                FilterType.Legendary,
                FilterType.Ultimate,
            ];

            return otherFilters.Contains(type);
        }

        private void Sort(int option)
        {
            switch (option)
            {
                case 0:
                    BuilderPageHelpers.Sort(Pool, (a, b) => b.Memoria.Id.CompareTo(a.Memoria.Id));
                    break;
                case 1:
                    BuilderPageHelpers.Sort(Pool, (a, b) => b.Status.Atk.CompareTo(a.Status.Atk));
                    break;
                case 2:
                    BuilderPageHelpers.Sort(Pool, (a, b) => b.Status.SpAtk.CompareTo(a.Status.SpAtk));
                    break;
                case 3:
                    BuilderPageHelpers.Sort(Pool, (a, b) => b.Status.Def.CompareTo(a.Status.Def));
                    break;
                case 4:
                    BuilderPageHelpers.Sort(Pool, (a, b) => b.Status.SpDef.CompareTo(a.Status.SpDef));
                    break;
                case 5:
                    BuilderPageHelpers.Sort(Pool, (a, b) => b.Status.ASA.CompareTo(a.Status.ASA));
                    break;
                case 6:
                    BuilderPageHelpers.Sort(Pool, (a, b) => b.Status.DSD.CompareTo(a.Status.DSD));
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
            else if (LegendaryDeck.Count > 5 || Deck.Count > 20)
            {
                GeneralInfoBar.Title = "�f�b�L��20���܂łł��i���W�F���_���[��5���܂łł��j�B";
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
            if (!unit.IsFront && Switch.IsOn)
            {
                GeneralInfoBar.Title = "�O�q���[�h�Ō�q�Ґ������[�h���邱�Ƃ͂ł��܂���I";
                GeneralInfoBar.IsOpen = true;
                GeneralInfoBar.Severity = InfoBarSeverity.Error;
                await Task.Delay(3000);
                GeneralInfoBar.IsOpen = false;
                return;
            }
            if (unit.IsFront && !Switch.IsOn)
            {
                GeneralInfoBar.Title = "��q���[�h�őO�q�Ґ������[�h���邱�Ƃ͂ł��܂���I";
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
            Pool.Clear();
            foreach (var memoria in OriginalPool
                .Where(m => (unit.IsFront ? Costume.DummyVanguard : Costume.DummyRearguard).CanBeEquipped(m))
                .Where(memoria => !Pool.Contains(memoria))
                .Where(memoria => !Deck.Concat(LegendaryDeck).Select(m => m.Memoria.Name).Contains(memoria.Memoria.Name)))
            {
                Pool.Add(memoria);
            }
            foreach (var memoria in Deck.Concat(LegendaryDeck))
            {
                Pool.Remove(memoria);
            }
            Sort(SortOption.SelectedIndex);
        }

        private void Concentration_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender.As<ComboBox>();
            if (comboBox.AccessKey == string.Empty) return;
            var id = int.Parse(comboBox.AccessKey);
            foreach (var item in LegendaryDeck.ToList())
            {
                if (item.Memoria.Id == id)
                {
                    var newItem = item with { Concentration = comboBox.SelectedIndex };
                    var idx = LegendaryDeck.IndexOf(item);
                    LegendaryDeck[idx] = newItem;
                }
            }
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
            var content = new Grid
            {
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

        private void TargetMember_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox cb) return;
            if (cb.SelectedItem is not string selected) return;

            OriginalPool.Clear();
            Pool.Clear();
            Deck.Clear();
            LegendaryDeck.Clear();

            if (selected == "All")
            {
                foreach (var memoria in Memoria.List.Select(m => new MemoriaWithConcentration(m, 4)))
                {
                    OriginalPool.Add(memoria);
                    Pool.Add(memoria);
                }
                return;
            }
            else
            {
                var path = $@"{Director.ProjectDir()}\{Director.ReadCache().Region}\Members\{selected}\info.json";
                using var sr = new StreamReader(path, Encoding.GetEncoding("UTF-8"));
                var readJson = sr.ReadToEnd();
                var info = MemberInfo.FromJson(readJson);
                if (info.Memorias is null)
                {
                    GeneralInfoBar.Title = "�������A���o�^����Ă��܂���";
                    GeneralInfoBar.IsOpen = true;
                    GeneralInfoBar.Severity = InfoBarSeverity.Error;
                    return;
                }
                // ID���L�[�ɂ����������A�̎������쐬
                // �o��/���o���̂����ЂƂ����o�^����Ă��Ȃ�����
                var idToMemoria = Memoria
                    .List
                    .ToDictionary(m => m.Id);
                Switch.IsOn = info.Position is Front;

                // �������A��ID��ǂݍ���ŁA����ID�ɑΉ�����o��/���o���������A�̃��X�g���擾���A
                // �����\�ȃ������A�݂̂�ǉ�����
                foreach (var memoria in info
                    .Memorias
                    .SelectMany(m => Memoria
                        .List
                        .Where(s => s.Name == idToMemoria[m.Id].Name)
                        .Select(s => new MemoriaWithConcentration(s, m.Concenration)))
                    .Where(m => (info.Position is Front ? Costume.DummyVanguard : Costume.DummyRearguard).CanBeEquipped(m.Memoria)))
                {
                    OriginalPool.Add(memoria);
                    Pool.Add(memoria);
                }
                return;
            }
        }

        private void TargetMemberSelect_Loaded(object sender, RoutedEventArgs e)
        {
            var items = new List<string> { "All" };
            items.AddRange(Directory
                .GetDirectories($@"{Director.ProjectDir()}\{Director.ReadCache().Region}\Members")
                .Select(d => new DirectoryInfo(d).Name));
            TargetMemberSelect.ItemsSource = items;
        }
    }

    public enum FilterType
    {
        // Kinds
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
        // Support Effects
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
        RangeUp,
        // Others
        Legendary,
        Ultimate,
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
        RangeUp,
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
}
