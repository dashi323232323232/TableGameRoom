using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TableGameRoom.Net;

namespace TableGameRoom;

public partial class MainWindow
{
    // ---------- 联机会话状态 ----------
    private LanServer? _lanServer;      // 我是不是会话主机
    private LanClient? _lanClient;      // 我作为客户端
    private SessionState? _session;     // 当前局域网状态
    private string _myNetId = "";       // 我的网络 ID
    private string _myNetName = "";     // 我的显示名
    private string? _myRoomId;          // 我所在房间
    private bool _isSessionHost;
    private CancellationTokenSource? _discoveryCts;
    /// <summary>当前显示的联机页（"" / "entry" / "lobby" / "room" / "game"）。用于判断要不要跟着会话刷新。</summary>
    private string _lanPage = "";
    private readonly List<string> _pendingRequests = [];   // 收到的申请
    private readonly List<InviteInfo> _incomingInvites = [];   // 收到的邀请
    /// <summary>从游戏大厅进入时预选的游戏（创建房间后自动应用）。</summary>
    private string? _pendingRoomGame;

    private static readonly (string Key, string Name)[] LanGames =
    [
        ("blackjack", "21 点"),
        ("poker", "德州扑克"),
        ("three", "炸金花"),
        ("liar", "骗子扑克")
    ];

    /// <summary>各游戏支持的最大人数。</summary>
    private static int GameMaxPlayers(string gameKey) => gameKey switch
    {
        "blackjack" => 7,   // 一张桌子最多 7 个玩家对庄家
        "poker" => 7,       // 德州最多 7 人
        "three" => 7,       // 炸金花最多 7 人
        "liar" => 2,        // 骗子扑克 1v1
        _ => 7
    };

    /// <summary>各游戏支持的最少人数。</summary>
    private static int GameMinPlayers(string gameKey) => gameKey switch
    {
        "blackjack" => 1,   // 21 点可以单人玩
        "poker" => 2,
        "three" => 2,
        "liar" => 2,
        _ => 2
    };

    /// <summary>把玩家 Id 解析成显示名（AI 补位显示成 🤖 AI 1）。</summary>
    private string DisplayName(string playerId)
    {
        if (playerId.StartsWith("ai-")) return $"🤖 AI {playerId.Replace("ai-", "")}";
        return _session?.Players.FirstOrDefault(x => x.Id == playerId)?.Name ?? "?";
    }

    /// <summary>切换某玩家的参战状态。</summary>
    private void SendPlaying(string roomId, string playerId, bool playing)
    {
        SendOrLocal(new NetMessage
        {
            Type = "set_playing",
            RoomId = roomId,
            Data = new Dictionary<string, string>
            {
                ["playerId"] = playerId,
                ["playing"] = playing ? "1" : "0"
            }
        });
    }

    // ==================== 联机入口页（未连接时）====================

    private void ShowLanEntry()
    {
        _currentGame = "";
        _lanPage = "entry";
        Base("联机大厅");
        Header();

        if (_isSessionHost || _lanClient is not null)
        {
            ShowLanConnected();
            return;
        }

        _page.Children.Add(T("把本机接入局域网，之后就能和其他玩家创建、加入房间。", 15, Muted));

        var cards = new Grid { Margin = new Thickness(0, 18, 0, 0) };
        cards.ColumnDefinitions.Add(new ColumnDefinition());
        cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        cards.ColumnDefinitions.Add(new ColumnDefinition());

        // 左：创建房间（局域网会话）
        var left = new StackPanel();
        left.Children.Add(T("创建房间", 24));
        left.Children.Add(T("在本机开启一个局域网会话，会生成一个端口号。", 13, Muted));
        left.Children.Add(T("其他玩家用「加入房间」输入这个端口号即可连入。", 13, Muted));
        var createBtn = B("创建房间", () => CreateLanSession());
        createBtn.FontSize = 17;
        createBtn.Padding = new Thickness(26, 12, 26, 12);
        createBtn.Margin = new Thickness(0, 16, 0, 0);
        createBtn.HorizontalAlignment = HorizontalAlignment.Left;
        left.Children.Add(createBtn);
        var leftCard = new Border
        {
            Padding = new Thickness(26),
            Background = PanelBrush,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Child = left
        };
        Grid.SetColumn(leftCard, 0);
        cards.Children.Add(leftCard);

        // 右：加入房间
        var right = new StackPanel();
        right.Children.Add(T("加入房间", 24));
        right.Children.Add(T("输入房主提供的端口号，连入他的局域网会话。", 13, Muted));
        var joinBox = new TextBox
        {
            Width = 150,
            Margin = new Thickness(0, 14, 0, 0),
            Padding = new Thickness(8, 6, 8, 6),
            Text = "47800"
        };
        right.Children.Add(joinBox);
        right.Children.Add(T("主机 IP（可选，留空则自动发现）", 12, Muted));
        var ipBox = new TextBox
        {
            Width = 190,
            Margin = new Thickness(0, 4, 0, 0),
            Padding = new Thickness(8, 6, 8, 6),
            Text = ""
        };
        right.Children.Add(ipBox);
        var joinBtn = B("加入房间", async () => await JoinLanSession(joinBox.Text, ipBox.Text));
        joinBtn.FontSize = 17;
        joinBtn.Padding = new Thickness(26, 12, 26, 12);
        joinBtn.Margin = new Thickness(0, 12, 0, 0);
        joinBtn.HorizontalAlignment = HorizontalAlignment.Left;
        right.Children.Add(joinBtn);
        right.Children.Add(T("提示：若自动发现失败（防火墙拦截），请填写主机的局域网 IP，如 192.168.1.5。", 11, Muted));
        var rightCard = new Border
        {
            Padding = new Thickness(26),
            Background = PanelBrush,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Child = right
        };
        Grid.SetColumn(rightCard, 2);
        cards.Children.Add(rightCard);

        _page.Children.Add(cards);

        // 提示：我收到的申请 / 邀请
        RenderPendingRequests();
    }

    private void RenderPendingRequests()
    {
        if (_incomingInvites.Count == 0 && _pendingRequests.Count == 0) return;
        _page.Children.Add(T("待处理", 18));
        foreach (var inv in _incomingInvites.ToList())
        {
            var row = new WrapPanel();
            row.Children.Add(T($"来自 {inv.FromName} 的邀请（房间：{inv.RoomName}）", 14, Accent));
            row.Children.Add(B("接受并加入", () => AcceptInvite(inv)));
            row.Children.Add(B("拒绝", () => { _incomingInvites.Remove(inv); ShowLanEntry(); }));
            _page.Children.Add(row);
        }
        foreach (var req in _pendingRequests.ToList())
        {
            var row = new WrapPanel();
            row.Children.Add(T(req, 14, Muted));
            _page.Children.Add(row);
        }
    }

    private void AcceptInvite(InviteInfo inv)
    {
        _incomingInvites.Remove(inv);
        if (_lanClient is null && _lanServer is null)
        {
            Msg("未连接到局域网。");
            ShowLanEntry();
            return;
        }
        SendOrLocal(new NetMessage { Type = "join_room", RoomId = inv.RoomId });
        _myRoomId = inv.RoomId;
        _currentGame = "lan-room";
        Msg("已接受邀请，正在进入房间…");
        ShowLanRoomOrLobby();
    }

    /// <summary>有房间则进房间页，否则回联机大厅。</summary>
    private void ShowLanRoomOrLobby()
    {
        var room = _session?.Rooms.FirstOrDefault(r => r.Id == _myRoomId);
        if (room is not null) { _currentGame = "lan-room"; ShowRoomPage(); }
        else { _currentGame = ""; ShowLanConnected(); }
    }

    /// <summary>邀请信息（带房间 ID 与来源）。</summary>
    private sealed record InviteInfo(string RoomId, string FromName, string RoomName);

    // ==================== 创建 / 加入局域网会话 ====================

    private void CreateLanSession()
    {
        _myNetName = Environment.UserName;
        _lanServer = new LanServer();
        _lanServer.Start($"{_myNetName} 的局域网", _myNetName);
        _isSessionHost = true;
        _myNetId = _lanServer.HostPlayerId;
        _session = _lanServer.Snapshot();

        _lanServer.MessageReceived += OnServerMessage;
        _lanServer.PlayerDisconnected += id => { _session = _lanServer.Snapshot(); };
        // 主机本地也要跟着刷新快照，否则看不到别人加入 / 房间变化
        // 用 BeginInvoke 延迟：BroadcastSession 常在 UI 事件处理过程中被调用，同步重入会把页面重建两次
        _lanServer.SessionChanged += () =>
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (_lanServer is null) return;
                _session = _lanServer.Snapshot();
                if (_lanPage is "lobby" or "room" or "game") RefreshLanView();
            });
        };

        _discoveryCts = new CancellationTokenSource();
        LanDiscovery.StartResponder(_lanServer.Port, _discoveryCts.Token);

        ShowLanConnected();
    }

    private async Task JoinLanSession(string portText, string ipText = "")
    {
        if (!int.TryParse(portText.Trim(), out var port))
        {
            Msg("端口号必须是数字。");
            return;
        }

        _myNetName = Environment.UserName;
        _lanClient = new LanClient();
        _lanClient.Received += OnClientMessage;
        _lanClient.Disconnected += OnLanDisconnected;

        // 优先用用户填写的 IP；否则 UDP 自动发现；最后回退本机（同机测试）
        string host;
        if (!string.IsNullOrWhiteSpace(ipText))
        {
            host = ipText.Trim();
        }
        else
        {
            host = await LanDiscovery.FindHostAsync(port) ?? "127.0.0.1";
        }

        var ok = await _lanClient.ConnectAsync(host, port, _myNetName);
        if (!ok)
        {
            _lanClient.Dispose();
            _lanClient = null;
            Msg($"无法连接到 {host}:{port}。\n\n请确认：\n1. 端口号正确\n2. 对方已创建房间\n3. 防火墙已放行（UDP 47899 + TCP {port}）\n4. 若自动发现失败，请手动填写主机 IP");
            return;
        }

        _myNetId = _lanClient.PlayerId;   // 若 welcome 还没到则为空，welcome 到达后会补上
        _isSessionHost = false;
        ShowLanConnected();
        // 稍等 welcome 到达后刷新
        await Task.Delay(300);
        ShowLanConnected();
    }

    private void OnClientMessage(NetMessage msg)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (msg.Type)
            {
                case "welcome":
                    if (msg.PlayerId is not null)
                    {
                        _myNetId = msg.PlayerId;
                        if (msg.Session is not null) { _session = msg.Session; }
                        ShowLanConnected();
                    }
                    break;
                case "session":
                    if (msg.Session is not null)
                    {
                        _session = msg.Session;
                        // 同步自己的房间归属（客户端由主机广播决定）
                        var myRoom = _session.Rooms.FirstOrDefault(r => r.Members.Any(m => m.PlayerId == _myNetId));
                        _myRoomId = myRoom?.Id;

                        // 房间处于对局中且当前不在对局页 → 进入对局页
                        // （不要用"状态刚变成 playing"来判定：多次广播会导致错过那一次跃变）
                        if (myRoom?.State == "playing" && _lanPage != "game")
                        {
                            _lanRoomForGame = myRoom.Id;
                            ShowLanGame();
                            return;
                        }

                        RefreshLanView();
                    }
                    break;
                case "invite":
                {
                    var roomId = msg.RoomId ?? "";
                    var roomName = _session?.Rooms.FirstOrDefault(r => r.Id == roomId)?.Name ?? "未知房间";
                    _incomingInvites.Add(new InviteInfo(roomId, msg.PlayerName ?? "玩家", roomName));
                    // 已连入局域网时留在当前页并刷新，不要跳回未连接入口
                    if (_session is not null) ShowLanConnected();
                    else ShowLanEntry();
                    break;
                }
                case "request":
                    _pendingRequests.Add($"{msg.PlayerName} 申请加入房间");
                    ShowLanEntry();
                    break;
                case "bj_state":
                    if (msg.RoomId is not null && msg.Data is not null)
                        ApplyBjState(msg.RoomId, msg.Data);
                    break;
                case "liar_state":
                    if (msg.RoomId is not null && msg.Data is not null)
                        ApplyLiarState(msg.RoomId, msg.Data);
                    break;
                case "three_state":
                    if (msg.RoomId is not null && msg.Data is not null)
                        ApplyThreeState(msg.RoomId, msg.Data);
                    break;
                case "poker_state":
                    if (msg.RoomId is not null && msg.Data is not null)
                        ApplyPokerState(msg.RoomId, msg.Data);
                    break;
                case "error":
                    Msg(msg.Text ?? "操作失败");
                    break;
            }
        });
    }

    private void OnServerMessage(string playerId, NetMessage msg)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var isLocal = playerId == _myNetId;
            switch (msg.Type)
            {
                case "create_room":
                    // 优先用消息里指定的玩法（安卓端建房时会带 GameKey）；
                    // 没有则回退到 UI 上预选的玩法。
                    CreateRoomAsHost(playerId, msg.GameKey);
                    break;
                case "join_room":
                    JoinRoomAsHost(playerId, msg.RoomId ?? "");
                    break;
                case "leave_room":
                    LeaveRoomAsHost(playerId);
                    break;
                case "invite":
                    if (msg.RoomId is not null && msg.Text is not null)
                    {
                        // 房主邀请某玩家
                        _lanServer?.SendTo(msg.Text, new NetMessage
                        {
                            Type = "invite",
                            PlayerName = _myNetName,
                            RoomId = msg.RoomId
                        });
                    }
                    break;
                case "set_playing":
                    if (msg.RoomId is not null && msg.Data is not null)
                    {
                        var room = _lanServer?.State.Rooms.FirstOrDefault(r => r.Id == msg.RoomId);
                        if (room is null) break;
                        var target = msg.Data.GetValueOrDefault("playerId");
                        // 房主可以改任何人，普通玩家只能改自己
                        if (room.HostId != playerId && target != playerId) break;
                        var playing = msg.Data.GetValueOrDefault("playing") == "1";
                        var member = room.Members.FirstOrDefault(m => m.PlayerId == target);
                        if (member is not null)
                        {
                            if (member.Busted && playing) { member.Busted = false; }
                            member.Playing = playing;
                            // 勾选人数超过当前桌位时自动扩桌
                            var pc = room.Members.Count(m => m.Playing);
                            if (pc > room.Seats) room.Seats = Math.Min(10, pc);
                            _lanServer?.BroadcastSession();
                        }
                    }
                    break;
                case "set_chips":
                    // 房主修改某玩家筹码（-1 表示无限）
                    if (msg.RoomId is not null && msg.Data is not null)
                    {
                        var room = _lanServer?.State.Rooms.FirstOrDefault(r => r.Id == msg.RoomId);
                        if (room is null || room.HostId != playerId) break;
                        var target = msg.Data.GetValueOrDefault("playerId");
                        if (!int.TryParse(msg.Data.GetValueOrDefault("chips"), out var chips)) break;
                        var member = room.Members.FirstOrDefault(m => m.PlayerId == target);
                        if (member is null) break;
                        member.Unlimited = chips < 0;
                        if (chips >= 0)
                        {
                            member.Chips = chips;
                            member.Busted = false;
                            if (!member.Playing && chips > 0) member.Playing = true;
                        }
                        _lanServer?.BroadcastSession();
                    }
                    break;
                case "set_game":
                    if (msg.RoomId is not null)
                    {
                        var room = _lanServer?.State.Rooms.FirstOrDefault(r => r.Id == msg.RoomId);
                        if (room is not null && room.HostId == playerId)
                        {
                            if (msg.GameKey is not null)
                            {
                                room.GameKey = msg.GameKey;
                                // 换游戏后桌位不能超过新游戏上限，也不能低于参战真人数
                                var max = GameMaxPlayers(msg.GameKey);
                                var pc = room.Members.Count(m => m.Playing);
                                room.Seats = Math.Clamp(room.Seats, Math.Max(2, Math.Min(pc, max)), max);

                                // 换游戏 = 放弃当前对局，房间回到等待状态，
                                // 否则会卡在"对局进行中"再也开不了新局
                                room.State = "waiting";
                                _lanBj = null;
                                _lanPoker = null;
                                _lanThree = null;
                                _lanLiar = null;
                                _lanRoomForGame = null;
                            }
                            if (msg.Data is not null)
                            {
                                if (msg.Data.TryGetValue("seats", out var s) && int.TryParse(s, out var sv))
                                {
                                    var max = GameMaxPlayers(room.GameKey);
                                    var pc = room.Members.Count(m => m.Playing);
                                    // 桌位不能少于参战真人数（否则有真人没位置），也不能超上限
                                    room.Seats = Math.Clamp(sv, Math.Max(2, Math.Min(pc, max)), max);
                                }
                                if (msg.Data.TryGetValue("tier", out var t) && int.TryParse(t, out var tv)) room.TierIndex = Math.Clamp(tv, 0, 6);
                                if (msg.Data.TryGetValue("turnSeconds", out var ts) && int.TryParse(ts, out var tsv)) room.TurnSeconds = Math.Clamp(tsv, 0, 300);
                            }
                            _lanServer?.BroadcastSession();
                        }
                    }
                    break;
                case "transfer_host":
                    if (msg.RoomId is not null && msg.Text is not null)
                    {
                        var room = _lanServer?.State.Rooms.FirstOrDefault(r => r.Id == msg.RoomId);
                        if (room is not null && room.HostId == playerId)
                        {
                            room.HostId = msg.Text;
                            _lanServer?.BroadcastSession();
                        }
                    }
                    break;
                case "kick_player":
                    if (msg.RoomId is not null && msg.Text is not null)
                    {
                        var room = _lanServer?.State.Rooms.FirstOrDefault(r => r.Id == msg.RoomId);
                        if (room is not null && room.HostId == playerId)
                        {
                            room.Members.RemoveAll(m => m.PlayerId == msg.Text);
                            _lanServer?.BroadcastSession();
                        }
                    }
                    break;
                case "close_room":
                    if (msg.RoomId is not null)
                    {
                        var room = _lanServer?.State.Rooms.FirstOrDefault(r => r.Id == msg.RoomId);
                        if (room is not null && room.HostId == playerId)
                        {
                            _lanServer?.State.Rooms.Remove(room);
                            _lanServer?.BroadcastSession();
                        }
                    }
                    break;
                case "start_game":
                    if (msg.RoomId is not null)
                    {
                        var room = _lanServer?.State.Rooms.FirstOrDefault(r => r.Id == msg.RoomId);
                        if (room is not null && room.HostId == playerId)
                        {
                            room.State = "playing";
                            if (room.GameKey == "blackjack") _lanBj = new LanBjState { RoomId = room.Id };
                            else if (room.GameKey == "liar") _lanLiar = new LanLiarState { RoomId = room.Id };
                            _lanServer?.BroadcastSession();
                            // 主机本地同步进入对局
                            if (playerId == _myNetId) { _lanRoomForGame = room.Id; ShowLanGame(); }
                        }
                    }
                    break;
                case "bj_bet":
                    if (msg.RoomId is not null && msg.Data is not null)
                        LanBjBet(playerId, msg.RoomId, msg.Data);
                    break;
                case "bj_deal":
                    if (msg.RoomId is not null)
                        LanBjDeal(playerId, msg.RoomId);
                    break;
                case "bj_hit":
                    if (msg.RoomId is not null)
                        LanBjHit(playerId, msg.RoomId);
                    break;
                case "bj_stand":
                    if (msg.RoomId is not null)
                        LanBjStand(playerId, msg.RoomId);
                    break;
                case "bj_double":
                    if (msg.RoomId is not null)
                        LanBjDouble(playerId, msg.RoomId);
                    break;
                case "bj_reset":
                    if (msg.RoomId is not null)
                        LanBjReset(playerId, msg.RoomId);
                    break;
                case "liar_start":
                    if (msg.RoomId is not null)
                        LanLiarStart(playerId, msg.RoomId);
                    break;
                case "liar_play":
                    if (msg.RoomId is not null && msg.Data is not null)
                        LanLiarPlay(playerId, msg.RoomId, msg.Data);
                    break;
                case "liar_challenge":
                    if (msg.RoomId is not null)
                        LanLiarChallenge(playerId, msg.RoomId);
                    break;
                case "liar_pass":
                    if (msg.RoomId is not null)
                        LanLiarPass(playerId, msg.RoomId);
                    break;
                case "three_start":
                    if (msg.RoomId is not null)
                        LanThreeStart(playerId, msg.RoomId);
                    break;
                case "three_see":
                    if (msg.RoomId is not null) LanThreeAction(playerId, msg.RoomId, "see");
                    break;
                case "three_call":
                    if (msg.RoomId is not null) LanThreeAction(playerId, msg.RoomId, "call");
                    break;
                case "three_raise":
                    if (msg.RoomId is not null) LanThreeAction(playerId, msg.RoomId, "raise");
                    break;
                case "three_fold":
                    if (msg.RoomId is not null) LanThreeAction(playerId, msg.RoomId, "fold");
                    break;
                case "three_showdown":
                    if (msg.RoomId is not null) LanThreeAction(playerId, msg.RoomId, "showdown");
                    break;
                case "poker_start":
                    if (msg.RoomId is not null) LanPokerStart(playerId, msg.RoomId);
                    break;
                case "poker_call":
                    if (msg.RoomId is not null) LanPokerAction(playerId, msg.RoomId, "call");
                    break;
                case "poker_raise":
                    if (msg.RoomId is not null) LanPokerAction(playerId, msg.RoomId, "raise");
                    break;
                case "poker_fold":
                    if (msg.RoomId is not null) LanPokerAction(playerId, msg.RoomId, "fold");
                    break;
            }

            // 远程玩家的操作：主机本地也要刷新界面
            if (!isLocal && _lanServer is not null)
            {
                _session = _lanServer.Snapshot();
                RefreshLanView();
            }
        });
    }

    private void CreateRoomAsHost(string playerId, string? gameKey = null)
    {
        if (_lanServer is null) return;
        var player = _lanServer.State.Players.FirstOrDefault(p => p.Id == playerId);
        var name = player?.Name ?? "玩家";

        // 玩法优先级：消息里指定的 > UI 预选的 > 默认 21 点
        var key = !string.IsNullOrWhiteSpace(gameKey) ? gameKey! : (_pendingRoomGame ?? "blackjack");
        if (key is not ("blackjack" or "poker" or "three" or "liar")) key = "blackjack";   // 未知玩法回退

        var room = new RoomInfo
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Name = $"{name} 的房间",
            HostId = playerId,
            Members = [new RoomMember { PlayerId = playerId, Playing = true, Unlimited = true }],
            GameKey = key,
            State = "waiting",
            Seats = Math.Clamp(3, 2, GameMaxPlayers(key))
        };
        _pendingRoomGame = null;
        _lanServer.State.Rooms.Add(room);
        _lanServer.BroadcastSession();
    }

    private void JoinRoomAsHost(string playerId, string roomId)
    {
        var room = _lanServer?.State.Rooms.FirstOrDefault(r => r.Id == roomId);
        if (room is null) return;
        if (room.IsFull) { _lanServer?.SendTo(playerId, new NetMessage { Type = "error", Text = "房间已满（最多 10 人）" }); return; }
        if (room.State == "playing") { _lanServer?.SendTo(playerId, new NetMessage { Type = "error", Text = "对局进行中，只能观战（暂不支持）" }); return; }
        if (room.Members.All(m => m.PlayerId != playerId))
            room.Members.Add(new RoomMember { PlayerId = playerId, Playing = true });
        _lanServer?.BroadcastSession();
    }

    private void LeaveRoomAsHost(string playerId)
    {
        if (_lanServer is null) return;
        foreach (var room in _lanServer.State.Rooms.ToList())
        {
            if (room.Members.All(m => m.PlayerId != playerId)) continue;
            room.Members.RemoveAll(m => m.PlayerId == playerId);
            if (room.HostId == playerId)
            {
                if (room.Members.Count > 0) room.HostId = room.Members[0].PlayerId;
                else _lanServer.State.Rooms.Remove(room);
            }
        }
        _lanServer.BroadcastSession();
    }

    private void OnLanDisconnected()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _lanClient = null;
            _session = null;
            _myRoomId = null;
            _currentGame = "";
            // 清理残留的对局状态，避免重连后看到旧数据
            _lanBj = null;
            _lanThree = null;
            _lanPoker = null;
            _lanLiar = null;
            Msg("与主机的连接已断开，联机会话结束。可以重新加入。");
            ShowLanEntry();
        });
    }

    // ==================== 已连接局域网：房间列表 ====================

    private void RefreshLanView()
    {
        if (_currentGame == "lan-room") { ShowRoomPage(); return; }
        if (_currentGame == "lan-game") { ShowLanGame(); return; }
        ShowLanConnected();
    }

    private void ShowLanConnected()
    {
        _lanPage = "lobby";
        Base("联机大厅");
        Header();

        var session = _session;
        if (session is null)
        {
            _page.Children.Add(T("未连接到局域网。", 15, Muted));
            _page.Children.Add(B("返回", () => ShowModeSelect()));
            return;
        }

        // 顶部信息
        var info = new WrapPanel();
        info.Children.Add(T($"局域网：{session.SessionName}   端口：{session.Port}   在线：{session.Players.Count} 人", 15, Accent));
        info.Children.Add(B("断开连接", () => DisconnectLan()));
        _page.Children.Add(info);

        // 收到的邀请（在最显眼位置）
        if (_incomingInvites.Count > 0)
        {
            foreach (var inv in _incomingInvites.ToList())
            {
                var invRow = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
                invRow.Children.Add(T($"📨 {inv.FromName} 邀请你加入「{inv.RoomName}」", 15, Accent));
                invRow.Children.Add(B("接受并加入", () => AcceptInvite(inv)));
                invRow.Children.Add(B("拒绝", () => { _incomingInvites.Remove(inv); ShowLanConnected(); }));
                _page.Children.Add(invRow);
            }
        }

        // 我的名字
        var me = session.Players.FirstOrDefault(p => p.Id == _myNetId);
        _page.Children.Add(T($"我是：{me?.Name ?? _myNetName}{(_isSessionHost ? "（局域网主机）" : "")}", 14, Muted));

        // 主区域：左=操作，右=房间列表
        var grid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });

        // 左：创建房间 + 玩家列表
        var left = new StackPanel();

        if (_myRoomId is null)
        {
            var createRow = new WrapPanel();
            var createBtn = B("创建房间", () => SendOrLocal(new NetMessage { Type = "create_room" }));
            createBtn.FontSize = 17;
            createBtn.Padding = new Thickness(22, 10, 22, 10);
            createRow.Children.Add(createBtn);
            left.Children.Add(createRow);
            left.Children.Add(T("创建后会直接出现在右侧房间列表里，不需要端口号。", 12, Muted));
        }
        else
        {
            var room = session.Rooms.FirstOrDefault(r => r.Id == _myRoomId);
            if (room is not null)
            {
                left.Children.Add(T($"你在房间：{room.Name}", 18, Accent));
                var btns = new WrapPanel();
                btns.Children.Add(B("进入房间", () => { _currentGame = "lan-room"; ShowRoomPage(); }));
                btns.Children.Add(B("退出房间", () => LeaveRoom()));
                left.Children.Add(btns);
            }
            else
            {
                _myRoomId = null;
            }
        }

        // 局域网玩家列表
        left.Children.Add(T("局域网玩家", 17));
        foreach (var p in session.Players)
        {
            var row = new WrapPanel();
            var tag = p.Id == _myNetId ? "（我）" : "";
            row.Children.Add(T($"● {p.Name}{tag}", 14));
            if (p.Id != _myNetId)
            {
                row.Children.Add(B("邀请到我的房间", () => InvitePlayer(p.Id)));
            }
            left.Children.Add(row);
        }

        var leftCard = new Border
        {
            Padding = new Thickness(20),
            Background = PanelBrush,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Child = left
        };
        Grid.SetColumn(leftCard, 0);
        grid.Children.Add(leftCard);

        // 右：房间列表 + 搜索
        var right = new StackPanel();
        right.Children.Add(T("房间列表", 17));

        var searchBox = new TextBox
        {
            Width = 280,
            Margin = new Thickness(0, 6, 0, 8),
            Padding = new Thickness(8, 6, 8, 6),
            Text = ""
        };
        var searchRow = new WrapPanel();
        searchRow.Children.Add(searchBox);
        right.Children.Add(searchRow);

        var listHost = new StackPanel();
        void RenderRooms()
        {
            listHost.Children.Clear();
            var keyword = searchBox.Text.Trim();
            var rooms = session.Rooms
                .Where(r => keyword.Length == 0 || r.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (rooms.Count == 0)
            {
                listHost.Children.Add(T(keyword.Length == 0 ? "暂无房间，点左边「创建房间」开一个。" : "没有匹配的房间。", 13, Muted));
                return;
            }

            foreach (var room in rooms)
            {
                var hostName = session.Players.FirstOrDefault(p => p.Id == room.HostId)?.Name ?? "?";
                var line = new StackPanel();
                line.Children.Add(T($"{room.Name}", 15));
                line.Children.Add(T($"房主 {hostName} · {room.Members.Count}/10 人 · {(room.State == "playing" ? "对局中" : "等待中")}", 12, Muted));

                var btnRow = new WrapPanel();
                if (_myRoomId == room.Id)
                    btnRow.Children.Add(T("已在此房间", 12, Accent));
                else
                    btnRow.Children.Add(B("加入", () => SendOrLocal(new NetMessage { Type = "join_room", RoomId = room.Id })));

                line.Children.Add(btnRow);

                listHost.Children.Add(new Border
                {
                    Margin = new Thickness(0, 4, 0, 4),
                    Padding = new Thickness(12, 8, 12, 8),
                    Background = Brushes.White,
                    BorderBrush = PanelBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Child = line
                });
            }
        }

        searchBox.TextChanged += (_, _) => RenderRooms();
        RenderRooms();
        right.Children.Add(listHost);

        var rightCard = new Border
        {
            Padding = new Thickness(18),
            Background = PanelBrush,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Child = right
        };
        Grid.SetColumn(rightCard, 2);
        grid.Children.Add(rightCard);

        _page.Children.Add(grid);
    }

    private void InvitePlayer(string playerId)
    {
        if (_myRoomId is null) { Msg("请先创建或加入一个房间。"); return; }
        SendOrLocal(new NetMessage { Type = "invite", RoomId = _myRoomId, Text = playerId });
        Msg("邀请已发送。");
    }

    /// <summary>房主设置某玩家筹码：可填金额或点「无限」。</summary>
    private void ShowChipsEditor(string roomId, string targetPlayerId, string playerName, RoomMember member)
    {
        var panel = new StackPanel { Margin = new Thickness(0) };
        panel.Children.Add(T($"设置「{playerName}」的筹码", 18));
        panel.Children.Add(T("输入金额，或点「无限」给无限筹码。", 13, Muted));

        var input = new TextBox
        {
            Width = 200,
            Margin = new Thickness(0, 10, 0, 0),
            Padding = new Thickness(8, 6, 8, 6),
            Text = member.Unlimited ? "10000" : member.Chips.ToString(),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        panel.Children.Add(input);

        var quick = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        foreach (var v in new[] { 1000, 5000, 10000, 50000, 100000 })
        {
            var amount = v;
            quick.Children.Add(B($"${amount:N0}", () => input.Text = amount.ToString()));
        }
        panel.Children.Add(quick);

        var buttons = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(B("确定", () =>
        {
            if (!int.TryParse(input.Text.Trim(), out var chips) || chips < 0)
            {
                Msg("请输入有效的金额（0 或正整数）。");
                return;
            }
            SendOrLocal(new NetMessage
            {
                Type = "set_chips",
                RoomId = roomId,
                Data = new Dictionary<string, string>
                {
                    ["playerId"] = targetPlayerId,
                    ["chips"] = chips.ToString()
                }
            });
            ShowRoomPage();
        }));
        buttons.Children.Add(B("∞ 无限", () =>
        {
            SendOrLocal(new NetMessage
            {
                Type = "set_chips",
                RoomId = roomId,
                Data = new Dictionary<string, string>
                {
                    ["playerId"] = targetPlayerId,
                    ["chips"] = "-1"
                }
            });
            ShowRoomPage();
        }));
        buttons.Children.Add(B("取消", () => ShowRoomPage()));
        panel.Children.Add(buttons);

        var dialog = new Window
        {
            Title = "设置筹码",
            Width = 420,
            Height = 320,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = Brushes.White,
            FontFamily = FontFamily,
            Content = new Border { Padding = new Thickness(24), Child = panel }
        };
        dialog.ShowDialog();
    }

    private void LeaveRoom()
    {
        SendOrLocal(new NetMessage { Type = "leave_room", RoomId = _myRoomId });
        _myRoomId = null;
        _currentGame = "";
        ShowLanConnected();
    }

    private void DisconnectLan()
    {
        _lanServer?.Dispose();
        _lanServer = null;
        _lanClient?.Dispose();
        _lanClient = null;
        _discoveryCts?.Cancel();
        _session = null;
        _myRoomId = null;
        _isSessionHost = false;
        _currentGame = "";
        ShowLanEntry();
    }

    /// <summary>本地直接执行 or 发给主机。</summary>
    private void SendOrLocal(NetMessage msg)
    {
        if (_isSessionHost && _lanServer is not null)
        {
            OnServerMessage(_myNetId, msg);
            // 主机自己的状态不会通过 BroadcastSession 回来，这里主动同步
            _session = _lanServer.Snapshot();

            // 主机本地操作后同步自己的房间归属
            if (msg.Type == "create_room")
            {
                var mine = _session.Rooms.FirstOrDefault(r => r.HostId == _myNetId);
                if (mine is not null) _myRoomId = mine.Id;
            }
            else if (msg.Type == "join_room" && msg.RoomId is not null)
            {
                _myRoomId = msg.RoomId;
            }
            else if (msg.Type == "leave_room" || msg.Type == "close_room")
            {
                _myRoomId = null;
            }

            RefreshLanView();
        }
        else if (_lanClient is not null)
        {
            msg.PlayerId = _myNetId;
            msg.PlayerName = _myNetName;
            _lanClient.Send(msg);
        }
    }

    // ==================== 房间页 ====================

    private void ShowRoomPage()
    {
        _currentGame = "lan-room";
        _lanPage = "room";
        Base("房间");
        Header();

        var session = _session;
        var room = session?.Rooms.FirstOrDefault(r => r.Id == _myRoomId);
        if (room is null) { ShowLanConnected(); return; }

        var isHost = room.HostId == _myNetId;
        var hostName = session!.Players.FirstOrDefault(p => p.Id == room.HostId)?.Name ?? "?";

        _page.Children.Add(T($"{room.Name}", 24));
        _page.Children.Add(T($"房主：{hostName}{(isHost ? "（我）" : "")} · {room.Members.Count}/10 人 · {(room.State == "playing" ? "对局中" : "等待中")}", 14, Muted));

        // 参战 / 观战分组一览
        var playingNames = room.Members.Where(m => m.Playing)
            .Select(m => session.Players.FirstOrDefault(p => p.Id == m.PlayerId)?.Name ?? "?")
            .ToList();
        var specNames = room.Members.Where(m => !m.Playing)
            .Select(m => session.Players.FirstOrDefault(p => p.Id == m.PlayerId)?.Name ?? "?")
            .ToList();
        if (playingNames.Count > 0)
            _page.Children.Add(T($"⚔ 参战（{playingNames.Count}）：{string.Join("、", playingNames)}", 13, Accent));
        if (specNames.Count > 0)
            _page.Children.Add(T($"👁 观战（{specNames.Count}）：{string.Join("、", specNames)}", 13, Muted));

        var btns = new WrapPanel();
        btns.Children.Add(B("返回联机大厅", () => { _currentGame = ""; ShowLanConnected(); }));
        if (isHost)
            btns.Children.Add(B("取消房间（解散）", () => CloseRoom()));
        btns.Children.Add(B("退出房间", () => LeaveRoom()));
        _page.Children.Add(btns);

        var grid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });

        // 左：玩家列表（10 位）+ 参战勾选 + 右键转让
        var left = new StackPanel();
        left.Children.Add(T("玩家列表", 17));
        for (var i = 0; i < 10; i++)
        {
            if (i < room.Members.Count)
            {
                var member = room.Members[i];
                var player = session.Players.FirstOrDefault(p => p.Id == member.PlayerId);
                var name = player?.Name ?? "?";
                var isRoomHost = member.PlayerId == room.HostId;
                var tag = isRoomHost ? " 👑" : "";
                var meTag = member.PlayerId == _myNetId ? "（我）" : "";

                var row = new WrapPanel { Margin = new Thickness(0, 2, 0, 2) };
                var busted = member.Busted ? " 💀出局" : "";
                var chipsText = member.Unlimited ? "∞" : $"${member.Chips:N0}";

                // 头像按钮：房主点击可修改该玩家筹码
                var avatar = new Button
                {
                    Content = isRoomHost ? "👑" : "👤",
                    FontSize = 13,
                    Width = 30,
                    Height = 26,
                    Padding = new Thickness(0),
                    Margin = new Thickness(0, 0, 4, 0),
                    Background = member.Unlimited ? new SolidColorBrush(Color.FromRgb(255, 244, 214)) : PanelBrush,
                    BorderBrush = PanelBorder,
                    BorderThickness = new Thickness(1),
                    Cursor = isHost ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
                    ToolTip = isHost ? "点击设置该玩家筹码" : null
                };
                var targetId = member.PlayerId;
                if (isHost)
                {
                    avatar.Click += (_, _) => ShowChipsEditor(room.Id, targetId, name, member);
                }
                row.Children.Add(avatar);

                var label = T($"{i + 1}. {name}{meTag}{tag}  {chipsText}{busted}", 14);
                row.Children.Add(label);

                // 参战勾选（房主可改任何人，普通玩家只能改自己）
                var check = new CheckBox
                {
                    Content = "参战",
                    IsChecked = member.Playing,
                    Margin = new Thickness(10, 0, 0, 0),
                    IsEnabled = isHost || member.PlayerId == _myNetId,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var playTargetId = member.PlayerId;
                // 用 Checked/Unchecked 而不是 Click：UIAutomation 的 TogglePattern 只触发这两个
                check.Checked += (_, _) => SendPlaying(room.Id, playTargetId, true);
                check.Unchecked += (_, _) => SendPlaying(room.Id, playTargetId, false);
                row.Children.Add(check);

                // 右键菜单：转让房主 / 移出房间（房主专用）
                if (isHost && !isRoomHost)
                {
                    var menu = new ContextMenu();
                    var transferItem = new MenuItem { Header = "👑 转让房主给他" };
                    transferItem.Click += (_, _) => TransferHost(room.Id, targetId);
                    menu.Items.Add(transferItem);

                    var kickItem = new MenuItem { Header = "🚪 移出房间" };
                    kickItem.Click += (_, _) =>
                    {
                        SendOrLocal(new NetMessage
                        {
                            Type = "kick_player",
                            RoomId = room.Id,
                            Text = targetId
                        });
                    };
                    menu.Items.Add(kickItem);

                    row.ContextMenu = menu;
                    // 同时保留一个可见的「…」按钮，方便不熟悉右键的用户
                    var moreBtn = B("更多 ▾", () => { menu.PlacementTarget = row; menu.IsOpen = true; });
                    moreBtn.FontSize = 12;
                    moreBtn.Padding = new Thickness(8, 4, 8, 4);
                    row.Children.Add(moreBtn);
                }

                left.Children.Add(row);
            }
            else
            {
                left.Children.Add(T($"{i + 1}. ○ 空位", 14, Muted));
            }
        }

        // 邀请区
        left.Children.Add(T("邀请玩家", 17));
        var inviteRow = new WrapPanel();
        var inviteSearch = new TextBox { Width = 160, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 5, 8, 5) };
        inviteRow.Children.Add(inviteSearch);
        inviteRow.Children.Add(B("搜索并邀请", () =>
        {
            var kw = inviteSearch.Text.Trim();
            if (kw.Length == 0) { Msg("请输入玩家名字。"); return; }
            var found = session.Players
                .Where(p => p.Id != _myNetId && p.Name.Contains(kw, StringComparison.OrdinalIgnoreCase))
                .Where(p => room.Members.All(m => m.PlayerId != p.Id))
                .ToList();
            if (found.Count == 0) { Msg("没有找到可邀请的玩家。"); return; }
            foreach (var p in found)
                SendOrLocal(new NetMessage { Type = "invite", RoomId = room.Id, Text = p.Id });
            Msg($"已向 {found.Count} 位玩家发送邀请。");
        }));
        left.Children.Add(inviteRow);

        var leftCard = new Border
        {
            Padding = new Thickness(20),
            Background = PanelBrush,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Child = left
        };
        Grid.SetColumn(leftCard, 0);
        grid.Children.Add(leftCard);

        // 右：游戏设置
        var right = new StackPanel();
        right.Children.Add(T("游戏设置", 17));

        right.Children.Add(T("选择游戏", 14, Muted));
        var gameRow = new WrapPanel();
        foreach (var g in LanGames)
        {
            var key = g.Key;
            var chip = B(g.Name, () =>
            {
                if (!isHost) { Msg("只有房主可以修改设置。"); return; }
                SendOrLocal(new NetMessage { Type = "set_game", RoomId = room.Id, GameKey = key });
            });
            if (room.GameKey == key)
            {
                chip.Background = new SolidColorBrush(Color.FromRgb(203, 223, 241));
                chip.FontWeight = FontWeights.Bold;
            }
            if (!isHost) chip.IsEnabled = false;
            gameRow.Children.Add(chip);
        }
        right.Children.Add(gameRow);

        // ---- 本局人数：范围 = 参战真人数 ~ 游戏上限 ----
        var playingCount = room.Members.Count(m => m.Playing);
        var gameMax = GameMaxPlayers(room.GameKey);
        var gameMin = Math.Max(2, GameMinPlayers(room.GameKey));

        // 人数不能低于参战真人数（否则有人没位置）
        var minSeats = Math.Max(gameMin, playingCount);
        if (room.Seats < minSeats) room.Seats = minSeats;
        if (room.Seats > gameMax) room.Seats = gameMax;

        var aiFill = Math.Max(0, room.Seats - playingCount);
        var fillText = aiFill > 0 ? $"（AI 补位 {aiFill} 名）" : "（全部真人）";
        right.Children.Add(T($"本局人数：{room.Seats} 人 {fillText}", 14, Muted));
        right.Children.Add(T($"已勾选参战真人：{playingCount} 人 · 本游戏支持 {gameMin}–{gameMax} 人", 12, Muted));

        var seatRow = new WrapPanel();
        for (var n = minSeats; n <= gameMax; n++)
        {
            var value = n;
            var fill = Math.Max(0, value - playingCount);
            var label = fill > 0 ? $"{value}（+{fill}AI）" : $"{value}";
            var chip = B(label, () =>
            {
                if (!isHost) { Msg("只有房主可以修改设置。"); return; }
                SendOrLocal(new NetMessage
                {
                    Type = "set_game",
                    RoomId = room.Id,
                    Data = new Dictionary<string, string> { ["seats"] = value.ToString() }
                });
            });
            if (room.Seats == value) { chip.Background = new SolidColorBrush(Color.FromRgb(203, 223, 241)); chip.FontWeight = FontWeights.Bold; }
            if (!isHost) chip.IsEnabled = false;
            seatRow.Children.Add(chip);
        }
        right.Children.Add(seatRow);

        if (playingCount < gameMin)
        {
            right.Children.Add(T($"⚠ 本游戏至少需要 {gameMin} 人，当前只有 {playingCount} 人参战，会自动用 AI 补足。", 12, new SolidColorBrush(Color.FromRgb(184, 134, 11))));
        }

        right.Children.Add(T($"下注档位：{BetTiers[Math.Clamp(room.TierIndex, 0, 6)].Name}", 14, Muted));
        var tierRow = new WrapPanel();
        for (var i = 0; i < BetTiers.Length; i++)
        {
            var index = i;
            var chip = B(BetTiers[i].Name, () =>
            {
                if (!isHost) { Msg("只有房主可以修改设置。"); return; }
                SendOrLocal(new NetMessage
                {
                    Type = "set_game",
                    RoomId = room.Id,
                    Data = new Dictionary<string, string> { ["tier"] = index.ToString() }
                });
            });
            if (room.TierIndex == index) { chip.Background = new SolidColorBrush(Color.FromRgb(203, 223, 241)); chip.FontWeight = FontWeights.Bold; }
            if (!isHost) chip.IsEnabled = false;
            tierRow.Children.Add(chip);
        }
        right.Children.Add(tierRow);

        // 行动倒计时设置
        right.Children.Add(T($"行动倒计时：{room.TurnSeconds} 秒", 14, Muted));
        var timerRow = new WrapPanel();
        foreach (var sec in new[] { 15, 30, 60, 0 })
        {
            var v = sec;
            var label = v == 0 ? "不限" : $"{v} 秒";
            var chip = B(label, () =>
            {
                if (!isHost) { Msg("只有房主可以修改设置。"); return; }
                SendOrLocal(new NetMessage
                {
                    Type = "set_game",
                    RoomId = room.Id,
                    Data = new Dictionary<string, string> { ["turnSeconds"] = v.ToString() }
                });
            });
            if (room.TurnSeconds == v) { chip.Background = new SolidColorBrush(Color.FromRgb(203, 223, 241)); chip.FontWeight = FontWeights.Bold; }
            if (!isHost) chip.IsEnabled = false;
            timerRow.Children.Add(chip);
        }
        right.Children.Add(timerRow);

        right.Children.Add(T($"每人初始筹码：${room.Chips:N0}", 13, Muted));

        // 开始游戏
        var startBtn = B(room.State == "playing" ? "对局进行中…" : "开始游戏", () =>
        {
            if (!isHost) { Msg("只有房主可以开始游戏。"); return; }
            if (room.State == "playing") { Msg("当前已有对局进行中。"); return; }
            StartLanGame(room);
        });
        startBtn.FontSize = 18;
        startBtn.Padding = new Thickness(26, 12, 26, 12);
        startBtn.Margin = new Thickness(0, 14, 0, 0);
        startBtn.IsEnabled = isHost && room.State != "playing";
        right.Children.Add(startBtn);

        var rightCard = new Border
        {
            Padding = new Thickness(20),
            Background = PanelBrush,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Child = right
        };
        Grid.SetColumn(rightCard, 2);
        grid.Children.Add(rightCard);

        _page.Children.Add(grid);
    }

    private void TransferHost(string roomId, string targetId)
    {
        SendOrLocal(new NetMessage { Type = "transfer_host", RoomId = roomId, Text = targetId });
        Msg("房主已转让。");
    }

    private void CloseRoom()
    {
        if (_myRoomId is null) return;
        SendOrLocal(new NetMessage { Type = "close_room", RoomId = _myRoomId });
        _myRoomId = null;
        _currentGame = "";
        ShowLanConnected();
    }

    // ==================== 联机对局（当前实现：轮盘）====================

    private void StartLanGame(RoomInfo room)
    {
        SendOrLocal(new NetMessage { Type = "start_game", RoomId = room.Id });
        // 房主本地直接进入对局
        if (_isSessionHost)
        {
            _lanRoomForGame = room.Id;
            ShowLanGame();
        }
    }

    private string? _lanRoomForGame;

    private void ShowLanGame()
    {
        _lanPage = "game";
        var room = _session?.Rooms.FirstOrDefault(r => r.Id == (_lanRoomForGame ?? _myRoomId));
        if (room is null) { ShowLanConnected(); return; }

        // 已实现的联机游戏
        if (room.GameKey == "blackjack")
        {
            ShowLanBlackjack(room);
            return;
        }
        if (room.GameKey == "liar")
        {
            ShowLanLiar(room);
            return;
        }
        if (room.GameKey == "three")
        {
            ShowLanThree(room);
            return;
        }
        if (room.GameKey == "poker")
        {
            ShowLanPoker(room);
            return;
        }

        _currentGame = "lan-game";
        Base($"{LanGames.FirstOrDefault(g => g.Key == room.GameKey).Name} · 联机对局");
        Header();
        _page.Children.Add(T($"房间：{room.Name} · {room.Members.Count}/10 人", 16, Accent));
        _page.Children.Add(T("该游戏的联机对局正在接入中。", 15, Muted));
        _page.Children.Add(T("目前已完整支持：21 点、德州扑克、炸金花、骗子扑克。", 13, Muted));
        _page.Children.Add(B("返回房间", () => { _currentGame = "lan-room"; ShowRoomPage(); }));
    }
}
