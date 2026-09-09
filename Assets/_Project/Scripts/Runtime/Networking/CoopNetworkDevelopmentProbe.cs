using System;
using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Players.Identity;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>仅显式命令行参数启用的双进程测试。正式构建不包含测试行为。</summary>
    public sealed class CoopNetworkDevelopmentProbe : MonoBehaviour, ICommandSource
    {
        public CoopSessionController Session;
        public MonoBehaviour CaptureComponent;
        private bool _active, _host, _started, _toggled, _returned;
        private float _start, _playingAt, _nextLog;
        private uint _sequence;
        private bool _captured, _skillSent, _downed;
        private bool _relayTest, _reconnectTest, _dropped, _rejoined, _reverseRoles;
        private NetworkPlayerSnapshotChannel _players;
        private NetworkWorldSnapshotChannel _world;
        private void Start()
        {
            var args = Environment.GetCommandLineArgs();
            _host = Array.IndexOf(args, "-ds-network-host") >= 0;
            _active = _host || Array.IndexOf(args, "-ds-network-client") >= 0;
            if (!_active) return;
            if (!Debug.isDebugBuild) { _active = false; return; }
            Application.runInBackground = true; _start = Time.unscaledTime;
            _players = Session.GetComponent<NetworkPlayerSnapshotChannel>();
            _world = Session.GetComponent<NetworkWorldSnapshotChannel>();
            Session.SetDevelopmentInput(this);
            bool relay = Array.IndexOf(args,"-ds-relay-test") >= 0;
            _relayTest = relay; _reconnectTest = Array.IndexOf(args,"-ds-reconnect-test") >= 0;
            if (!_host && Array.IndexOf(args,"-ds-weak-network-test") >= 0) Session.Config.Port = 7778;
            _reverseRoles = Array.IndexOf(args,"-ds-reverse-roles") >= 0;
            if (relay && Session.TransportComponent is IRelaySelection selected)
                selected.SelectRelay(true,"ws://127.0.0.1:8765","deepsleep-test-room");
            if (_host) Session.Create(_reverseRoles ? PlayerRole.Harness : PlayerRole.DeepSeek); else Session.Join(relay ? "deepsleep-test-room" : "127.0.0.1");
        }
        private void Update()
        {
            if (!_active) return;
            if (!_host && _reconnectTest && _dropped && !_rejoined && Session.Phase == SessionPhase.Disconnected && Time.unscaledTime-_playingAt > 11.5f)
            { _rejoined = Session.Join(_relayTest ? "deepsleep-test-room" : "127.0.0.1"); Debug.Log("[NET_TEST] REJOIN_REQUEST " + _rejoined); }
            if (Session.Phase == SessionPhase.Lobby && !Session.LocalReady) Session.SetReady(true);
            if (Session.Phase == SessionPhase.Playing)
            {
                if (!_started) { _playingAt = Time.unscaledTime; _started = true; Debug.Log("[NET_TEST] CONNECTED_AND_PLAYING"); }
                float elapsed = Time.unscaledTime - _playingAt;
                if (elapsed > 3 && !_toggled) { _toggled = true; Session.SetLocalAi(true); }
                if (elapsed > 5 && !_returned) { _returned = true; Session.SetLocalAi(false); }
                if (_host && !_reverseRoles && elapsed > 6 && !_downed)
                {
                    _downed = true;
                    Session.Harness.transform.position = Session.DeepSeek.transform.position + Vector3.right;
                    _players.Harness.Body.position = Session.Harness.transform.position;
                    var health = _players.Harness.LocalHud.Health;
                    var hit = new DeepSleep.Runtime.Combat.Damage.DamagePacket(health.MaximumHealth,
                        Session.Harness.transform.position, Vector2.zero, null);
                    health.TryReceiveDamage(hit); Session.SetLocalAi(true);
                    Debug.Log("[NET_TEST] FORCE_DOWN_HS_FOR_REVIVE_TEST");
                }
                if (!_host && _reconnectTest && elapsed > 10 && !_dropped)
                { _dropped = true; Session.InterruptConnectionForDevelopmentTest(); Debug.Log("[NET_TEST] SIMULATED_CONNECTION_LOSS"); return; }
                if (elapsed > 7 && !_captured)
                {
                    _captured = true;
                    (CaptureComponent as INetworkTestCapture)?.Capture(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
                        "..", _host ? "host-frame.png" : "client-frame.png")));
                }
                if (Time.unscaledTime >= _nextLog)
                {
                    _nextLog = Time.unscaledTime + 1;
                    _players.Harness.TryRead(out var hud);
                    Debug.Log("[NET_TEST] host=" + _host + " ai=" + Session.LocalAi + " guest=" + Session.GuestControl +
                        " seq=" + Session.LastGuestCommand + " ds=" + Session.DeepSeek.transform.position.ToString("F2") +
                        " hs=" + Session.Harness.transform.position.ToString("F2") + " snapshots=" + _players.ReceivedSequence +
                        " entities=" + _world.VisibleEntities + " hsHP=" + hud.Health + " revive=" + hud.ReviveProgress);
                }
                if (!_host && elapsed > 14) { Debug.Log("[NET_TEST] CLIENT_LEAVE"); Session.Leave(); Application.Quit(); }
                if (_host && elapsed > 17) { Debug.Log("[NET_TEST] HOST_END guest=" + Session.GuestControl); Session.Leave(); Application.Quit(); }
            }
            if (Time.unscaledTime - _start > 35) { Debug.LogError("[NET_TEST] TIMEOUT " + Session.Status); Application.Quit(1); }
        }
        public bool TryGetCommand(uint simulationTick, out PlayerCommand command)
        {
            if (!_active || !Debug.isDebugBuild) { command = default; return false; }
            bool skill = _started && Time.unscaledTime - _playingAt > 1 && !_skillSent;
            if (skill) _skillSent = true;
            command = new PlayerCommand(++_sequence, simulationTick, new Vector2(0.15f, 0),
                new AimIntent(AimReference.WorldPosition, new Vector2(4, 0)),
                skill ? CommandButtonState.Pressed : 0, 0,
                Session.LocalRole == PlayerRole.Harness && _skillSent ? CommandButtonState.Held : 0, 0, 0, 0);
            return true;
        }
    }
}
