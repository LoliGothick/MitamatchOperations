using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using mitama.Algorithm;
using mitama.Domain;
using mitama.Pages.Capture;
using mitama.Pages.Common;
using mitama.Pages.OrderConsole;
using Microsoft.UI.Xaml.Controls.Primitives;
using WinRT;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using mitama.Domain.OrderKinds;
using mitama.Pages.ControlDashboard;
using SimdLinq;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MitamatchOperations.Lib;
using OpenCvSharp.Extensions;
using OpenCvSharp;
using System.Drawing;
using Windows.Storage;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;

namespace mitama.Pages;

internal enum WindowPicker
{
    Main,
}

/// <summary>
/// Control Dashboard Page navigated to within a Main Page.
/// </summary>
public sealed partial class ControlDashboardPage
{
    // �E�B���h�E�L���v�`���̂��߂̃��\�[�X���i��
    private WindowCapture _capture;

    // �E�B���h�E�L���v�`���̂��߂̃��b�`
    private readonly CountdownEvent _captureEvent = new(1);
    // �T�u�X�N���C�o�[�̃X�P�W���[��
    private readonly HistoricalScheduler[] _schedulers = [new(), new(), new(), new(), new()];
    
    // �I�[�_�[�̕\�����s�����߂̃R���N�V����
    private readonly ObservableCollection<TimeTableItem> _reminds = [];
    private readonly ObservableHashSet<ResultItem> _results = [];
    
    // �X�P�W���[����i�߂邽�߂̃^�C�}�[
    private static readonly Lazy<DispatcherTimer> Timer = new(() => 
    {
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };
        timer.Start();
        return timer;
    });

    // for Template Matching
    private readonly AKAZE akaze = AKAZE.Create();
    private (Order Order, Mat Discripters)[] Templates = [];

    // �Ȃ�₩���Ŏg����ԕϐ�
    private int _cursor = 4;
    private List<TimeTableItem> _deck = [];
    private DateTime _nextTimePoint;
    private DateTime? _firstTimePoint;
    private readonly string _user = Director.ReadCache().User;
    private readonly string _project = Director.ReadCache().Region;
    private bool _picFlag = true;
    private OpOrderStatus _orderStat = new None();
    private Order? _opOrderInfo;
    private readonly LimitedContainer<FailSafe> failSafe = new(3);
    // for Debug
    private int _debugCounter = 0;

    public bool IsCtrlKeyPressed { get; set; }

    private void Grid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Control) IsCtrlKeyPressed = true;
        else switch (IsCtrlKeyPressed)
        {
            case true when e.Key == VirtualKey.Q:
                ManualTrigger();
                break;
            case true when e.Key == VirtualKey.C:
                _orderStat = new None();
                break;
        }
    }

    private void Grid_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Control) IsCtrlKeyPressed = false;
    }

    public ControlDashboardPage()
    {
        Task.Run(async () =>
        {
            // �e���v���[�g�}�b�`���O�̂��߂̏���
            Templates = await Task.WhenAll(Order
                .List
                .Where(order => order.HasTemplate)
                .Select(async order =>
                {
                    var file = await StorageFile.GetFileFromApplicationUriAsync(order.TemplateUri);
                    var image = new Bitmap((await FileIO.ReadBufferAsync(file)).AsStream());
                    var descriptors = new Mat();
                    akaze.DetectAndCompute(image.ToMat(), null, out _, descriptors);
                    return (order, descriptors);
                }));
        });

        // ��ʂ̏�����
        InitializeComponent();

        // �S�̂̃E�B���h�E�L���v�`��
        Observable.Interval(TimeSpan.FromMilliseconds(200), _schedulers[0])
            .Subscribe(delegate
            {
                Task.Run(async () =>
                {
                    _captureEvent.Reset(1);
                    await _capture!.SnapShot();
                    _captureEvent.Signal();
                });
            });

        // ����̃I�[�_�[����ǎ��
        Observable.Interval(TimeSpan.FromMilliseconds(200), _schedulers[1])
            .Subscribe(async delegate
            {
                _captureEvent.Wait();
                // �I�[�_�[����ǎ��
                var cap = await _capture!.CaptureOrderInfo();
                var info = Order.List
                        .Select(order => (order, Algo.LevenshteinRate(order.Name, cap)))
                        .Where(item => item.Item2 < 0.6)
                        .ToArray();

                if (info.Length <= 0) return;
                // �ǎ�ꂽ���߁A�I�[�_�[�����X�g�A����
                var res = info.MinBy(item => item.Item2);
                _opOrderInfo = res.order;
            });

        // �����̃I�[�_�[����ǎ��
        Observable.Interval(TimeSpan.FromMilliseconds(200), _schedulers[2])
            .Subscribe(async delegate
            {
                _captureEvent.Wait();
                switch (await Analyze(await _capture!.TryCaptureOrderInfo()))
                {
                    case SuccessResult(var user, var order):
                    {
                        if (_reminds.Count == 0) break;
                        var ordered = Order.List.MinBy(o => Algo.LevenshteinRate(o.Name, order));
                        if (_deck.Select(e => e.Order.Index).ToArray().Contains(ordered.Index)
                            && !_results.Select(r => r.Order.Index).ToArray().Contains(ordered.Index))
                        {
                            Update(user, ordered);
                        }
                        break;
                    }
                    case FailureResult:
                    {
                        break;
                    }
                }
            });

        // ����̃I�[�_�[�� ����/����/�I�� ���X�L��������
        Observable.Interval(TimeSpan.FromMilliseconds(200), _schedulers[3])
            .Subscribe(async delegate { await OrderScan(); });

        // �����I�[�_�[�̔����O�ʒm���o��
        Observable.Interval(TimeSpan.FromMilliseconds(200), _schedulers[4])
            .Subscribe(delegate
            {
                if (_reminds.Count > 0 && _nextTimePoint - DateTime.Now <= new TimeSpan(0, 0, 0, 10))
                {
                    if (_reminds.First().Start == 15u * 60u) return;
                    InfoBar.IsOpen = true;
                    var flag = InfoBar.Severity == InfoBarSeverity.Warning;
                    InfoBar.Severity = _nextTimePoint - DateTime.Now >= new TimeSpan() ? InfoBarSeverity.Warning : InfoBarSeverity.Error;
                    InfoBar.Title =
                        $"{_reminds.First().Pic} ����� {_reminds.First().Order.Name} �����܂ł��� {(_nextTimePoint - DateTime.Now).Seconds} �b";
                    if (_picFlag && _user == _reminds.First().Pic)
                    {
                        _picFlag = false;
                        PlayAlert(ElementSoundKind.Hide, ElementSoundKind.Invoke, ElementSoundKind.Show, ElementSoundKind.Invoke);
                    }
                    if (flag && InfoBar.Severity == InfoBarSeverity.Error) PlayAlert(ElementSoundKind.Hide);
                }
                else
                {
                    InfoBar.IsOpen = false;
                }
            });

        Timer.Value.Tick += async delegate
        {
            if (_capture != null)
            {
                foreach (var scheduler in _schedulers)
                {
                    scheduler.AdvanceBy(TimeSpan.FromMilliseconds(40));
                }
            }
            else if (InitBar.AccessKey != "SUCCESS")
            {
                switch (Search.WindowHandleFromCaption("Assaultlily"))
                {
                    case Ok<IntPtr, string>(var handle):
                        {
                            _capture = await Init(handle);
                            InitBar.AccessKey = "SUCCESS";
                            InitBar.IsOpen = false;
                            break;
                        }
                    case Err<IntPtr, string>:
                        {
                            if (InitBar.AccessKey != "ERROR")
                            {
                                InitBar.IsOpen = true;
                                InitBar.AccessKey = "ERROR";
                                InitBar.Severity = InfoBarSeverity.Error;
                                InitBar.Title = "���X�o���̃E�B���h�E��������܂���ł����A�N�����čőO�ʂ̏�Ԃɂ��Ă�������";
                                var menu = new MenuFlyout { Placement = FlyoutPlacementMode.Bottom };
                                foreach (var (caption, handle) in Search.GetWindowList())
                                {
                                    if (caption == string.Empty) continue;
                                    var item = new MenuFlyoutItem
                                    {
                                        Text = caption,
                                        Command = new Defer(async delegate
                                        {
                                            _capture = await Init(handle);
                                        })
                                    };
                                    menu.Items.Add(item);
                                }
                                InitBar.Content = new DropDownButton
                                {
                                    Content = "�܂��͉�ʂ�I������",
                                    Flyout = menu
                                };
                            }
                            break;
                        }
                }
            }
        };
    }

    private async Task<WindowCapture> Init(IntPtr handle)
    {
        InitBar.AccessKey = "SUCCESS";
        InitBar.IsOpen = false;
        InitBar.Content = null;

        _ = MitamatchOperations.MLOrderModel.Predict(new()
        {
            ImageSource = File.ReadAllBytes((await Package.Current.InstalledLocation.GetFileAsync(@"Assets\ML\DataSet\wait_or_active\active\0001.png")).Path)
        });
        _ = MitamatchOperations.MLActivatingModel.Predict(new()
        {
            ImageSource = File.ReadAllBytes((await Package.Current.InstalledLocation.GetFileAsync(@"Assets\ML\DataSet\is_activating\True\0001.png")).Path)
        });

        // IO�̃^�C�~���O�����炷���߂� 40 ms �����炷
        _schedulers[3].AdvanceBy(TimeSpan.FromMilliseconds(40));
        _schedulers[2].AdvanceBy(TimeSpan.FromMilliseconds(80));
        _schedulers[1].AdvanceBy(TimeSpan.FromMilliseconds(120));
        _schedulers[0].AdvanceBy(TimeSpan.FromMilliseconds(160));

        string[] paths = [
            "wait_or_active\\wait",
            "wait_or_active\\active",
            "wait_or_active\\nothing",
            "is_activating\\True",
            "is_activating\\False"
        ];
        string[] orders = [
            "�ፖ�̑���",
            "���̐_��",
            "�����̐_��",
            "���C�̗E��",
            "�쒹�̗E��",
            "���΂̗E��",
            "�V���̌䏂",
            "���_�̌䏂",
            "�鐝�̌䏂",
            "�o���̑�V��",
            "��V���̊o���W�Q",
            "�x���̏j��",
            "�W���̏j��",
            "�x���̔���",
            "�W���̔���",
            "���k�̈�",
            "�Í��Ɖ�",
            "���萅��",
            "��済Е�",
            "���w�Η�",
            "�V����g",
            "���؉���",
        ];

        // �f�o�b�O�p�̃f�B���N�g�����폜
        if (Directory.Exists(@$"{Director.MitamatchDir()}\Debug\dataset"))
        {
            Directory.Delete(@$"{Director.MitamatchDir()}\Debug\dataset", true);
        }

        foreach (var path in paths)
        {
            if (!Directory.Exists(@$"{Director.MitamatchDir()}\Debug\dataset\{path}"))
            {
                Director.CreateDirectory(@$"{Director.MitamatchDir()}\Debug\dataset\{path}");
            }
        }
        foreach (var path in orders)
        {
            if (!Directory.Exists(@$"{Director.MitamatchDir()}\Debug\dataset\order_classification\{path}"))
            {
                Director.CreateDirectory(@$"{Director.MitamatchDir()}\Debug\dataset\order_classification\{path}");
            }
        }

        return new WindowCapture(handle);
    }

    private Task OrderScan()
    {
        switch (_orderStat)
        {
            // ����I�[�_�[������
            case Active(var order, var point):
                {
                    if (order is not null)
                    {
                        var spend = (DateTime.Now - point).Seconds + (DateTime.Now - point).Minutes * 60;
                        OpponentInfoBar.IsOpen = true;
                        OpponentInfoBar.Title = $"����I�[�_�[: {order.Value.Name} => �c�� {order.Value.ActiveTime - spend - 3} �b";
                        // �I�[�_�[���I���1�b�O�ɂ͕\������߂�
                        if (order.Value.ActiveTime - spend < 1)
                        {
                            // ���̃I�[�_�[���m�ɂނ��ď�����
                            _orderStat = new None();
                            _opOrderInfo = null;
                            OpponentInfoBar.IsOpen = false;
                        }
                    }
                    _captureEvent.Wait();
                    switch (_capture!.CaptureOpponentsOrder())
                    {
                        // �I�[�_�[�����������m
                        case WaitStat(var image):
                            {
                                image.Save($"{Director.MitamatchDir()}\\Debug\\dataset\\wait_or_active\\wait\\debug{_debugCounter++}.png");
                                failSafe.Add(new Waiting_());
                                if (failSafe.Length == 3 && failSafe.GetItems().All(stat => stat is Waiting_))
                                {
                                    _orderStat = new Waiting();
                                    OpponentInfoBar.IsOpen = true;
                                    var waitingFor = _opOrderInfo != null ? $"for {_opOrderInfo?.Name}..." : "...";
                                    OpponentInfoBar.Title = $@"Waiting {waitingFor}";
                                }
                                break;
                            }
                        case ActiveStat(var image):
                            {
                                image.Save($"{Director.MitamatchDir()}\\Debug\\dataset\\wait_or_active\\active\\debug{_debugCounter++}.png");
                                if (_opOrderInfo is null)
                                {
                                    var result = PredictOrder(image);
                                    image.Save($"{Director.MitamatchDir()}\\Debug\\dataset\\order_classification\\{result.Name}\\debug{_debugCounter++}.png");
                                    _opOrderInfo = result;
                                    _orderStat = (Active)_orderStat with { Order = result };
                                }
                                break;
                            }
                        case Nothing(var image):
                            {
                                image.Save($"{Director.MitamatchDir()}\\Debug\\dataset\\wait_or_active\\nothing\\debug{_debugCounter++}.png");
                                break;
                            }
                        default:
                            break;
                    }
                    break;
                }
            // ����I�[�_�[�������ł��������ł��Ȃ�
            case None:
                {
                    OpponentInfoBar.IsOpen = false;
                    _captureEvent.Wait();
                    switch (_capture!.CaptureOpponentsOrder())
                    {
                        // �I�[�_�[�����������m
                        case WaitStat(var image):
                            {
                                image.Save($"{Director.MitamatchDir()}\\Debug\\dataset\\wait_or_active\\wait\\debug{_debugCounter++}.png");
                                if (failSafe.GetItems().All(stat => stat is Waiting_))
                                {
                                    _orderStat = new Waiting();
                                }
                                else
                                {
                                    failSafe.Add(new Waiting_());
                                }
                                break;
                            }
                        default:
                            break;
                    }
                    break;
                }
            // ����I�[�_�[������
            case Waiting:
                {
                    OpponentInfoBar.IsOpen = true;
                    var waitingFor = _opOrderInfo != null ? $"for {_opOrderInfo?.Name}..." : "...";
                    OpponentInfoBar.Title = $@"Waiting {waitingFor}";

                    _captureEvent.Wait();
                    switch (_capture!.CaptureOpponentsOrder())
                    {
                        case ActiveStat(var image):
                            {
                                image.Save($"{Director.MitamatchDir()}\\Debug\\dataset\\wait_or_active\\active\\debug{_debugCounter++}.png");
                                failSafe.Add(new Active_());
                                if (failSafe.Length == 3 && failSafe.GetItems().All(stat => stat is Active_))
                                {
                                    _orderStat = new None();
                                }
                                break;
                            }
                        case Nothing(var image):
                            {
                                image.Save($"{Director.MitamatchDir()}\\Debug\\dataset\\wait_or_active\\nothing\\debug{_debugCounter++}.png");
                                // MP�񕜒��ŃI�[�_�[�A�C�R���������Ă��Ȃ��\��������
                                // �������́A�t�F�C�Y�J�ڒ��ł���\��������
                                break;
                            }
                        default:
                            break;
                    }
                    switch (_capture!.IsActivating())
                    {
                        case ActiveStat(var image):
                            {
                                image.Save($"{Director.MitamatchDir()}\\Debug\\dataset\\is_activating\\True\\debug{_debugCounter++}.png");
                                if (_opOrderInfo?.ActiveTime == 0)
                                {
                                    _orderStat = new None();
                                    _opOrderInfo = null;
                                }
                                else
                                {
                                    _orderStat = new Active(_opOrderInfo, DateTime.Now);
                                    _opOrderInfo = null;
                                }
                                break;
                            }
                        case Nothing(var image):
                            {
                                image.Save($"{Director.MitamatchDir()}\\Debug\\dataset\\is_activating\\False\\debug{_debugCounter++}.png");
                                break;
                            }
                        default:
                            break;
                    }
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(_orderStat));
        }
        return Task.CompletedTask;
    }

    private void ReCalcTimeTable()
    {
        if (_deck.Count == 0) return;
        var table = _deck.ToArray();
        _deck.Clear();
        var first = table.First();
        var previous = first with
        {
            Start = 15 * 60 - first.Delay,
            End = 15 * 60 - first.Order.PrepareTime - first.Order.ActiveTime
        };
        _deck.Add(previous);

        foreach (var item in table.Skip(1))
        {
            var prepareTime = previous.Order.Index switch
            {
                52 => 5, // ���M�I���}�b�`�X�L���������ԒZ�kLv.3
                _ => item.Order.PrepareTime
            };
            previous = item with
            {
                Start = previous.End - item.Delay,
                End = (previous.End - item.Delay) - prepareTime - item.Order.ActiveTime
            };
            _deck.Add(previous);
        }
    }

    /// <summary>
    /// Updates the time table.
    /// </summary>
    /// <param name="user">The user who prepared the order.</param>
    /// <param name="ordered">The order in which it is prepared.</param>
    private void Update(string user, Order ordered)
    {
        var now = DateTime.Now;

        // �^�C���e�[�u���Čv�Z
        if (_reminds.First().Order != ordered)
        {
            var idx = _deck.IndexOf(ordered);
            _deck.Insert(_cursor - _reminds.Count, _deck[idx]);
            _deck.RemoveAt(idx + 1);

            ReCalcTimeTable();
            var remaining = _reminds.Count;
            _reminds.Clear();
            foreach (var item in _deck.GetRange(_cursor - remaining, remaining))
            {
                _reminds.Add(item);
            }
        }

        if (ordered.Kind is Elemental)
        {
            var originalRemindsCount = _reminds.Count;
            foreach (var item in _deck
                         .GetRange(_cursor - _reminds.Count, _deck.Count - _cursor + _reminds.Count)
                         .Where(item => item.Order != ordered)
                         .Where(item => item.Order.Kind is Elemental)
                         .Where(item => item.Order.Kind.As<Elemental>().Element == ordered.Kind.As<Elemental>().Element)
                         .ToArray())
            {
                _deck.Remove(item);
                _reminds.Remove(item);
            }

            if (_cursor <= _deck.Count)
            {
                var remaining = _reminds.Count;
                _reminds.Clear();
                foreach (var item in _deck.GetRange(_cursor - originalRemindsCount, remaining))
                {
                    _reminds.Add(item);
                }

                ReCalcTimeTable();
            }
        }

        // �����v�Z
        _firstTimePoint ??= now;
        var totalTime = ordered.PrepareTime + ordered.ActiveTime;
        _nextTimePoint = now + new TimeSpan(0, 0, totalTime / 60, totalTime % 60);
        var span = now - _firstTimePoint;
        var deviation = span.Value.Minutes * 60 + span.Value.Seconds - (15 * 60 - _reminds.First().Start);

        // �����ς݃I�[�_�[����菜���A
        _reminds.Remove(ordered);
        // ���̃I�[�_�[������Βǉ�����
        if (_deck.Count > _cursor && _reminds.Count < 4) _reminds.Add(_deck[_cursor++]);

        // �����t���I�[�_�[������΃`�F�b�N����
        if (_reminds.Count > 0 && _reminds.First().Conditional && deviation >= 30)
        {
            var skip = _reminds.First();
            _reminds.RemoveAt(0);
            if (_deck.Count > _cursor) _reminds.Add(_deck[_cursor++]);
            ConditionalOrderInfo.IsOpen = true;
            ConditionalOrderInfo.Severity = InfoBarSeverity.Warning;
            ConditionalOrderInfo.Title = $"{skip.Order.Name} �̓X�L�b�v���Ă�������";
        }
        else
        {
            ConditionalOrderInfo.IsOpen = false;
        }

        // �������ʂɒǉ�
        _results.Add(new ResultItem(user, ordered, deviation, now));
        // ��ʍX�V
        RemainderBoard.ItemsSource = _reminds;
        ResultBoard.ItemsSource = _results.Distinct().OrderByDescending(r => r.ActivatedAt).ToList();
        RemainderBoard.SelectedIndex = 0;
    }

    private static Task<AnalyzeResult> Analyze(string raw)
    {
        var orderedRegex = MyRegex();
        var match = orderedRegex.Match(raw);

        return Task.FromResult<AnalyzeResult>(match.Success
            ? new SuccessResult(match.Groups[1].Value, match.Groups[2].Value)
            : new FailureResult(raw));
    }

    private abstract record AnalyzeResult;

    // ReSharper disable once NotAccessedPositionalProperty.Local
    private record SuccessResult(string User, string Order) : AnalyzeResult;

    // ReSharper disable once NotAccessedPositionalProperty.Local
    private record FailureResult(string Raw) : AnalyzeResult;

    private void LoadComboBox_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox box) return;

        if (!Directory.Exists(Director.DeckDir(_project)))
        {
            Director.CreateDirectory(Director.DeckDir(_project));
        }
        var decks = Directory.GetFiles(Director.DeckDir(_project), "*.json").Select(path =>
        {
            using var sr = new StreamReader(path, Encoding.GetEncoding("UTF-8"));
            var json = sr.ReadToEnd();
            return JsonSerializer.Deserialize<DeckJson>(json);
        }).ToList();

        box.ItemsSource = decks;
    }

    private void LoadButton_OnClick(object sender, RoutedEventArgs e)
    {
        var deck = DeckLoadBox.SelectedItem.As<DeckJson>();

        _deck = deck.Items.Select(item => (TimeTableItem)item).ToList();

        // Initialisation
        _reminds.Clear();
        _results.Clear();
        _cursor = 4;
        _firstTimePoint = null;

        foreach (var item in _deck.GetRange(0, 4))
        {
            _reminds.Add(item);
        }

        RemainderBoard.SelectedIndex = 0;

        if (((Button)sender).Parent is StackPanel { Parent: FlyoutPresenter { Parent: Popup popup } })
        {
            popup.IsOpen = false;
        }

        using var onClose = new Defer(async delegate
        {
            TeachingInfoBar.IsOpen = true;
            await Task.Delay(2000);
            TeachingInfoBar.IsOpen = false;
        });
    }

    private void ManualTriggerButton_OnClick(object sender, RoutedEventArgs e)
    {
        ManualTrigger();
    }

    private void ManualTrigger()
    {
        if (_reminds.Count == 0) return;
        Update(_reminds.First().Pic, _reminds.First().Order);
    }

    private static void PlayAlert(ElementSoundKind soundKind)
    {
        using var onClose = new Defer(async delegate
        {
            ElementSoundPlayer.State = ElementSoundPlayerState.On;
            ElementSoundPlayer.Play(soundKind);
            await Task.Delay(400);
            ElementSoundPlayer.Play(soundKind);
            await Task.Delay(400);
            ElementSoundPlayer.Play(soundKind);
            await Task.Delay(800);
            ElementSoundPlayer.State = ElementSoundPlayerState.Off;
        });
    }

    private static void PlayAlert(params ElementSoundKind[] soundKinds)
    {
        using var onClose = new Defer(async delegate
        {
            ElementSoundPlayer.State = ElementSoundPlayerState.On;
            foreach (var soundKind in soundKinds)
            {
                ElementSoundPlayer.Play(soundKind);
                await Task.Delay(400);
            }
            await Task.Delay(400);
            ElementSoundPlayer.State = ElementSoundPlayerState.Off;
        });
    }

    private void DeckLoadBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        LoadButton.IsEnabled = true;
    }
    
    private void CounterButton_OnClick(object sender, RoutedEventArgs e)
    {
        var newWindow = new System.Windows.Window();

        var counterView = new CounterView();
        newWindow.Content = counterView;

        // �E�B���h�E��\��
        newWindow.Activate();
    }

    [GeneratedRegex(@"(.+)���I�[�_�[(.+)������")]
    private static partial Regex MyRegex();

    private Order PredictOrder(Bitmap image)
    {
        var descriptors = new Mat();
        akaze.DetectAndCompute(image.ToMat(), null, out _, descriptors);

        return Templates.MinBy(template => {
            var (_, train) = template;
            var matcher = new BFMatcher(NormTypes.Hamming);
            var matches = matcher.Match(descriptors, train);
            var sum = matches.Sum(x => x.Distance);
            return sum / matches.Length;
        }).Order;
    }
}

internal record ResultItem(string Pic, Order Order, int Deviation, DateTime ActivatedAt)
{
    public string DeviationFmt => $"({Deviation})";
    bool IEquatable<ResultItem>.Equals(ResultItem other) => Order.Index == other?.Order.Index;

}

internal abstract record OpOrderStatus;
internal record Waiting : OpOrderStatus;
internal record Active(Order? Order, DateTime Point): OpOrderStatus;
internal record None : OpOrderStatus;

internal abstract record FailSafe;
internal record Waiting_ : FailSafe;
internal record Active_ : FailSafe;
internal record None_ : FailSafe;

