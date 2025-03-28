﻿using ImGuiNET;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Utility;
using PeepingTom.Ipc;
using PeepingTom.Resources;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace PeepingTom {
    internal class PluginUi : IDisposable {
        private Plugin Plugin { get; }

        private ulong? PreviousFocus { get; set; } = new();

        private bool _wantsOpen;

        public bool WantsOpen {
            get => this._wantsOpen;
            set => this._wantsOpen = value;
        }

        public bool Visible { get; private set; }

        private bool _settingsOpen;

        public bool SettingsOpen {
            get => this._settingsOpen;
            set => this._settingsOpen = value;
        }

        private FileDialogManager FileDialogManager { get; }

        public PluginUi(Plugin plugin) {
            this.Plugin = plugin;
            this.FileDialogManager = new FileDialogManager();
        }

        public void Dispose() {
            this.WantsOpen = false;
            this.SettingsOpen = false;
        }

        public void Draw() {
            if (this.Plugin.InPvp) {
                return;
            }

            if (this.SettingsOpen) {
                this.ShowSettings();
            }

            var inCombat = Service.Condition[ConditionFlag.InCombat];
            var inInstance = Service.Condition[ConditionFlag.BoundByDuty]
                             || Service.Condition[ConditionFlag.BoundByDuty56]
                             || Service.Condition[ConditionFlag.BoundByDuty95];
            var inCutscene = Service.Condition[ConditionFlag.WatchingCutscene]
                             || Service.Condition[ConditionFlag.WatchingCutscene78]
                             || Service.Condition[ConditionFlag.OccupiedInCutSceneEvent];

            // FIXME: this could just be a boolean expression
            var shouldBeShown = this.WantsOpen;
            if (inCombat && !this.Plugin.Config.ShowInCombat) {
                shouldBeShown = false;
            } else if (inInstance && !this.Plugin.Config.ShowInInstance) {
                shouldBeShown = false;
            } else if (inCutscene && !this.Plugin.Config.ShowInCutscenes) {
                shouldBeShown = false;
            }

            this.Visible = shouldBeShown;

            if (shouldBeShown) {
                this.ShowMainWindow();
            }

            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoBackground
                                           | ImGuiWindowFlags.NoTitleBar
                                           | ImGuiWindowFlags.NoNav
                                           | ImGuiWindowFlags.NoNavInputs
                                           | ImGuiWindowFlags.NoFocusOnAppearing
                                           | ImGuiWindowFlags.NoNavFocus
                                           | ImGuiWindowFlags.NoInputs
                                           | ImGuiWindowFlags.NoMouseInputs
                                           | ImGuiWindowFlags.NoSavedSettings
                                           | ImGuiWindowFlags.NoDecoration
                                           | ImGuiWindowFlags.NoScrollWithMouse;
            ImGuiHelpers.ForceNextWindowMainViewport();
            if (!ImGui.Begin("Peeping Tom targeting indicator dummy window", flags)) {
                ImGui.End();
                return;
            }

            if (this.Plugin.Config.MarkTargeted) {
                this.MarkPlayer(this.GetCurrentTarget(), this.Plugin.Config.TargetedColour, this.Plugin.Config.TargetedSize);
            }

            if (!this.Plugin.Config.MarkTargeting) {
                goto EndDummy;
            }

            var player = Service.ClientState.LocalPlayer;
            if (player == null) {
                goto EndDummy;
            }

            var targeting = this.Plugin.Watcher.CurrentTargeters
                .Select(targeter => Service.ObjectTable.FirstOrDefault(obj => obj.GameObjectId == targeter.GameObjectId))
                .Where(targeter => targeter is IPlayerCharacter)
                .Cast<IPlayerCharacter>()
                .ToArray();
            foreach (var targeter in targeting) {
                this.MarkPlayer(targeter, this.Plugin.Config.TargetingColour, this.Plugin.Config.TargetingSize);
            }

            EndDummy:
            ImGui.End();
        }

        private void ShowSettings() {
            this.FileDialogManager.Draw();

            ImGui.SetNextWindowSize(new Vector2(700, 250));
            var windowTitle = string.Format(Language.SettingsTitle, Plugin.Name);
            if (!ImGui.Begin($"{windowTitle}###ptom-settings", ref this._settingsOpen, ImGuiWindowFlags.NoResize)) {
                ImGui.End();
                return;
            }

            if (ImGui.BeginTabBar("##settings-tabs")) {
                if (ImGui.BeginTabItem($"{Language.SettingsMarkersTab}###markers-tab")) {
                    var markTargeted = this.Plugin.Config.MarkTargeted;
                    if (ImGui.Checkbox(Language.SettingsMarkersMarkTarget, ref markTargeted)) {
                        this.Plugin.Config.MarkTargeted = markTargeted;
                        this.Plugin.Config.Save();
                    }

                    var targetedColour = this.Plugin.Config.TargetedColour;
                    if (ImGui.ColorEdit4(Language.SettingsMarkersMarkTargetColour, ref targetedColour)) {
                        this.Plugin.Config.TargetedColour = targetedColour;
                        this.Plugin.Config.Save();
                    }

                    var targetedSize = this.Plugin.Config.TargetedSize;
                    if (ImGui.DragFloat(Language.SettingsMarkersMarkTargetSize, ref targetedSize, 0.01f, 0f, 15f)) {
                        targetedSize = Math.Max(0f, targetedSize);
                        this.Plugin.Config.TargetedSize = targetedSize;
                        this.Plugin.Config.Save();
                    }

                    ImGui.Spacing();

                    var markTargeting = this.Plugin.Config.MarkTargeting;
                    if (ImGui.Checkbox(Language.SettingsMarkersMarkTargeting, ref markTargeting)) {
                        this.Plugin.Config.MarkTargeting = markTargeting;
                        this.Plugin.Config.Save();
                    }

                    var targetingColour = this.Plugin.Config.TargetingColour;
                    if (ImGui.ColorEdit4(Language.SettingsMarkersMarkTargetingColour, ref targetingColour)) {
                        this.Plugin.Config.TargetingColour = targetingColour;
                        this.Plugin.Config.Save();
                    }

                    var targetingSize = this.Plugin.Config.TargetingSize;
                    if (ImGui.DragFloat(Language.SettingsMarkersMarkTargetingSize, ref targetingSize, 0.01f, 0f, 15f)) {
                        targetingSize = Math.Max(0f, targetingSize);
                        this.Plugin.Config.TargetingSize = targetingSize;
                        this.Plugin.Config.Save();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem($"{Language.SettingsFilterTab}###filters-tab")) {
                    var showParty = this.Plugin.Config.LogParty;
                    if (ImGui.Checkbox(Language.SettingsFilterLogParty, ref showParty)) {
                        this.Plugin.Config.LogParty = showParty;
                        this.Plugin.Config.Save();
                    }

                    var logAlliance = this.Plugin.Config.LogAlliance;
                    if (ImGui.Checkbox(Language.SettingsFilterLogAlliance, ref logAlliance)) {
                        this.Plugin.Config.LogAlliance = logAlliance;
                        this.Plugin.Config.Save();
                    }

                    var logInCombat = this.Plugin.Config.LogInCombat;
                    if (ImGui.Checkbox(Language.SettingsFilterLogCombat, ref logInCombat)) {
                        this.Plugin.Config.LogInCombat = logInCombat;
                        this.Plugin.Config.Save();
                    }

                    var logSelf = this.Plugin.Config.LogSelf;
                    if (ImGui.Checkbox(Language.SettingsFilterLogSelf, ref logSelf)) {
                        this.Plugin.Config.LogSelf = logSelf;
                        this.Plugin.Config.Save();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem($"{Language.SettingsBehaviourTab}###behaviour-tab")) {
                    var focusTarget = this.Plugin.Config.FocusTargetOnHover;
                    if (ImGui.Checkbox(Language.SettingsBehaviourFocusHover, ref focusTarget)) {
                        this.Plugin.Config.FocusTargetOnHover = focusTarget;
                        this.Plugin.Config.Save();
                    }

                    var openExamine = this.Plugin.Config.OpenExamine;
                    if (ImGui.Checkbox(Language.SettingsBehaviourExamineEnabled, ref openExamine)) {
                        this.Plugin.Config.OpenExamine = openExamine;
                        this.Plugin.Config.Save();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem($"{Language.SettingsSoundTab}###sound-tab")) {
                    var playSound = this.Plugin.Config.PlaySoundOnTarget;
                    if (ImGui.Checkbox(Language.SettingsSoundEnabled, ref playSound)) {
                        this.Plugin.Config.PlaySoundOnTarget = playSound;
                        this.Plugin.Config.Save();
                    }

                    ImGui.TextUnformatted(Language.SettingsSoundPath);
                    Vector2 buttonSize;
                    ImGui.PushFont(UiBuilder.IconFont);
                    try {
                        buttonSize = ImGuiHelpers.GetButtonSize(FontAwesomeIcon.Folder.ToIconString());
                    } finally {
                        ImGui.PopFont();
                    }

                    var path = this.Plugin.Config.SoundPath ?? "";
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - buttonSize.X);
                    if (ImGui.InputText("###sound-path", ref path, 1_000)) {
                        path = path.Trim();
                        this.Plugin.Config.SoundPath = path.Length == 0 ? null : path;
                        this.Plugin.Config.Save();
                    }

                    ImGui.SameLine();

                    ImGui.PushFont(UiBuilder.IconFont);
                    try {
                        if (ImGui.Button(FontAwesomeIcon.Folder.ToIconString())) {
                            this.FileDialogManager.OpenFileDialog(
                                Language.SettingsSoundPath,
                                ".wav,.mp3,.aif,.aiff,.wma,.aac",
                                (selected, selectedPath) => {
                                    if (!selected) {
                                        return;
                                    }

                                    path = selectedPath.Trim();
                                    this.Plugin.Config.SoundPath = path.Length == 0 ? null : path;
                                    this.Plugin.Config.Save();
                                }
                            );
                        }
                    } finally {
                        ImGui.PopFont();
                    }

                    ImGui.Text(Language.SettingsSoundPathHelp);

                    var volume = this.Plugin.Config.SoundVolume * 100f;
                    if (ImGui.DragFloat(Language.SettingsSoundVolume, ref volume, .1f, 0f, 100f, "%.1f%%")) {
                        this.Plugin.Config.SoundVolume = Math.Max(0f, Math.Min(1f, volume / 100f));
                        this.Plugin.Config.Save();
                    }

                    var devices = DirectSoundOut.Devices.ToList();
                    var soundDevice = devices.FirstOrDefault(d => d.Guid == this.Plugin.Config.SoundDeviceNew);
                    var name = soundDevice != null ? soundDevice.Description : Language.SettingsSoundInvalidDevice;

                    if (ImGui.BeginCombo($"{Language.SettingsSoundOutputDevice}###sound-output-device-combo", name)) {
                        for (var deviceNum = 0; deviceNum < devices.Count; deviceNum++) {
                            var info = devices[deviceNum];
                            if (!ImGui.Selectable($"{info.Description}##{deviceNum}")) {
                                continue;
                            }

                            this.Plugin.Config.SoundDeviceNew = info.Guid;
                            this.Plugin.Config.Save();
                        }

                        ImGui.EndCombo();
                    }

                    var soundCooldown = this.Plugin.Config.SoundCooldown;
                    if (ImGui.DragFloat(Language.SettingsSoundCooldown, ref soundCooldown, .01f, 0f, 30f)) {
                        soundCooldown = Math.Max(0f, soundCooldown);
                        this.Plugin.Config.SoundCooldown = soundCooldown;
                        this.Plugin.Config.Save();
                    }

                    var playWhenClosed = this.Plugin.Config.PlaySoundWhenClosed;
                    if (ImGui.Checkbox(Language.SettingsSoundPlayWhenClosed, ref playWhenClosed)) {
                        this.Plugin.Config.PlaySoundWhenClosed = playWhenClosed;
                        this.Plugin.Config.Save();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem($"{Language.SettingsWindowTab}###window-tab")) {
                    var openOnLogin = this.Plugin.Config.OpenOnLogin;
                    if (ImGui.Checkbox(Language.SettingsWindowOpenLogin, ref openOnLogin)) {
                        this.Plugin.Config.OpenOnLogin = openOnLogin;
                        this.Plugin.Config.Save();
                    }

                    var allowMovement = this.Plugin.Config.AllowMovement;
                    if (ImGui.Checkbox(Language.SettingsWindowAllowMovement, ref allowMovement)) {
                        this.Plugin.Config.AllowMovement = allowMovement;
                        this.Plugin.Config.Save();
                    }

                    var allowResizing = this.Plugin.Config.AllowResize;
                    if (ImGui.Checkbox(Language.SettingsWindowAllowResize, ref allowResizing)) {
                        this.Plugin.Config.AllowResize = allowResizing;
                        this.Plugin.Config.Save();
                    }

                    ImGui.Spacing();

                    var showInCombat = this.Plugin.Config.ShowInCombat;
                    if (ImGui.Checkbox(Language.SettingsWindowShowCombat, ref showInCombat)) {
                        this.Plugin.Config.ShowInCombat = showInCombat;
                        this.Plugin.Config.Save();
                    }

                    var showInInstance = this.Plugin.Config.ShowInInstance;
                    if (ImGui.Checkbox(Language.SettingsWindowShowInstance, ref showInInstance)) {
                        this.Plugin.Config.ShowInInstance = showInInstance;
                        this.Plugin.Config.Save();
                    }

                    var showInCutscenes = this.Plugin.Config.ShowInCutscenes;
                    if (ImGui.Checkbox(Language.SettingsWindowShowCutscene, ref showInCutscenes)) {
                        this.Plugin.Config.ShowInCutscenes = showInCutscenes;
                        this.Plugin.Config.Save();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem($"{Language.SettingsHistoryTab}###history-tab")) {
                    var keepHistory = this.Plugin.Config.KeepHistory;
                    if (ImGui.Checkbox(Language.SettingsHistoryEnabled, ref keepHistory)) {
                        this.Plugin.Config.KeepHistory = keepHistory;
                        this.Plugin.Config.Save();
                    }

                    var historyWhenClosed = this.Plugin.Config.HistoryWhenClosed;
                    if (ImGui.Checkbox(Language.SettingsHistoryRecordClosed, ref historyWhenClosed)) {
                        this.Plugin.Config.HistoryWhenClosed = historyWhenClosed;
                        this.Plugin.Config.Save();
                    }

                    var numHistory = this.Plugin.Config.NumHistory;
                    if (ImGui.InputInt(Language.SettingsHistoryAmount, ref numHistory)) {
                        numHistory = Math.Max(0, Math.Min(50, numHistory));
                        this.Plugin.Config.NumHistory = numHistory;
                        this.Plugin.Config.Save();
                    }

                    var showTimestamps = this.Plugin.Config.ShowTimestamps;
                    if (ImGui.Checkbox(Language.SettingsHistoryTimestamps, ref showTimestamps)) {
                        this.Plugin.Config.ShowTimestamps = showTimestamps;
                        this.Plugin.Config.Save();
                    }

                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem($"{Language.SettingsAdvancedTab}###advanced-tab")) {
                    var pollFrequency = this.Plugin.Config.PollFrequency;
                    if (ImGui.DragInt(Language.SettingsAdvancedPollFrequency, ref pollFrequency, .1f, 1, 1600)) {
                        this.Plugin.Config.PollFrequency = pollFrequency;
                        this.Plugin.Config.Save();
                    }

                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            ImGui.End();
        }

        private void ShowMainWindow() {
            var targeting = this.Plugin.Watcher.CurrentTargeters;
            var previousTargeters = this.Plugin.Config.KeepHistory ? this.Plugin.Watcher.PreviousTargeters : null;

            // to prevent looping over a subset of the actors repeatedly when multiple people are targeting,
            // create a dictionary for O(1) lookups by actor id
            Dictionary<ulong, IGameObject>? objects = null;
            if (targeting.Count + (previousTargeters?.Count ?? 0) > 1) {
                var dict = new Dictionary<ulong, IGameObject>();
                foreach (var obj in Service.ObjectTable) {
                    if (dict.ContainsKey(obj.GameObjectId) || obj.ObjectKind != ObjectKind.Player) {
                        continue;
                    }

                    dict.Add(obj.GameObjectId, obj);
                }

                objects = dict;
            }

            var flags = ImGuiWindowFlags.None;
            if (!this.Plugin.Config.AllowMovement) {
                flags |= ImGuiWindowFlags.NoMove;
            }

            if (!this.Plugin.Config.AllowResize) {
                flags |= ImGuiWindowFlags.NoResize;
            }

            ImGui.SetNextWindowSize(new Vector2(290, 195), ImGuiCond.FirstUseEver);
            if (!ImGui.Begin(Plugin.Name, ref this._wantsOpen, flags)) {
                ImGui.End();
                return;
            }

            {
                ImGui.Text(Language.MainTargetingYou);
                ImGui.SameLine();
                HelpMarker(this.Plugin.Config.OpenExamine
                    ? Language.MainHelpExamine
                    : Language.MainHelpNoExamine);

                var height = ImGui.GetContentRegionAvail().Y;
                height -= ImGui.GetStyle().ItemSpacing.Y;

                var anyHovered = false;
                if (ImGui.BeginListBox("##targeting", new Vector2(-1, height))) {
                    // add the two first players for testing
                    // foreach (var p in this.Plugin.Interface.ClientState.Actors
                    //     .Where(actor => actor is PlayerCharacter)
                    //     .Skip(1)
                    //     .Select(actor => actor as PlayerCharacter)
                    //     .Take(2)) {
                    //     this.AddEntry(new Targeter(p), p, ref anyHovered);
                    // }

                    foreach (var targeter in targeting) {
                        IGameObject? obj = null;
                        objects?.TryGetValue(targeter.GameObjectId, out obj);
                        this.AddEntry(targeter, obj, ref anyHovered);
                    }

                    if (this.Plugin.Config.KeepHistory) {
                        // get a list of the previous targeters that aren't currently targeting
                        var previous = (previousTargeters ?? new List<Targeter>())
                            .Where(old => targeting.All(actor => actor.GameObjectId != old.GameObjectId))
                            .Take(this.Plugin.Config.NumHistory);
                        // add previous targeters to the list
                        foreach (var oldTargeter in previous) {
                            IGameObject? obj = null;
                            objects?.TryGetValue(oldTargeter.GameObjectId, out obj);
                            this.AddEntry(oldTargeter, obj, ref anyHovered, ImGuiSelectableFlags.Disabled);
                        }
                    }

                    ImGui.EndListBox();
                }

                var previousFocus = this.PreviousFocus;
                if (this.Plugin.Config.FocusTargetOnHover && !anyHovered && previousFocus != null) {
                    if (previousFocus == uint.MaxValue) {
                        Service.TargetManager.FocusTarget = null;
                    } else {
                        var actor = Service.ObjectTable.FirstOrDefault(a => a.GameObjectId == previousFocus);
                        // either target the actor if still present or target nothing
                        Service.TargetManager.FocusTarget = actor;
                    }

                    this.PreviousFocus = null;
                }

                ImGui.End();
            }
        }

        private static void HelpMarker(string text) {
            ImGui.TextDisabled("(?)");
            if (!ImGui.IsItemHovered()) {
                return;
            }

            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20f);
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }

        private void AddEntry(Targeter targeter, IGameObject? obj, ref bool anyHovered, ImGuiSelectableFlags flags = ImGuiSelectableFlags.None) {
            ImGui.BeginGroup();

            ImGui.Selectable(targeter.Name.TextValue, false, flags);

            if (this.Plugin.Config.ShowTimestamps) {
                var time = DateTime.UtcNow - targeter.When >= TimeSpan.FromDays(1)
                    ? targeter.When.ToLocalTime().ToString("dd/MM")
                    : targeter.When.ToLocalTime().ToString("t");
                var windowWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;
                ImGui.SameLine(windowWidth - ImGui.CalcTextSize(time).X);

                if (flags.HasFlag(ImGuiSelectableFlags.Disabled)) {
                    ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int) ImGuiCol.TextDisabled]);
                }

                ImGui.TextUnformatted(time);

                if (flags.HasFlag(ImGuiSelectableFlags.Disabled)) {
                    ImGui.PopStyleColor();
                }
            }

            ImGui.EndGroup();

            var hover = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
            var left = hover && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
            var right = hover && ImGui.IsMouseClicked(ImGuiMouseButton.Right);

            obj ??= Service.ObjectTable.FirstOrDefault(a => a.GameObjectId == targeter.GameObjectId);

            // don't count as hovered if the actor isn't here (clears focus target when hovering missing actors)
            if (obj != null) {
                anyHovered |= hover;
            }

            if (this.Plugin.Config.FocusTargetOnHover && hover && obj != null) {
                this.PreviousFocus ??= Service.TargetManager.FocusTarget?.GameObjectId ?? uint.MaxValue;
                Service.TargetManager.FocusTarget = obj;
            }

            if (left) {
                if (this.Plugin.Config.OpenExamine && ImGui.GetIO().KeyAlt) {
                    if (obj != null) {
                        unsafe {
                            AgentInspect.Instance()->ExamineCharacter(obj.EntityId);
                        }
                    } else {
                        var error = string.Format(Language.ExamineErrorToast, targeter.Name);
                        Service.ToastGui.ShowError(error);
                    }
                } else {
                    var payload = new PlayerPayload(targeter.Name.TextValue, targeter.HomeWorldId);
                    Payload[] payloads = [payload];
                    Service.ChatGui.Print(new XivChatEntry {
                        Message = new SeString(payloads),
                    });
                }
            } else if (right && obj != null) {
                Service.TargetManager.Target = obj;
            }
        }

        private void MarkPlayer(IGameObject? player, Vector4 colour, float size) {
            if (player == null) {
                return;
            }

            if (!Service.GameGui.WorldToScreen(player.Position, out var screenPos)) {
                return;
            }

            ImGui.PushClipRect(ImGuiHelpers.MainViewport.Pos, ImGuiHelpers.MainViewport.Pos + ImGuiHelpers.MainViewport.Size, false);

            ImGui.GetWindowDrawList().AddCircleFilled(
                ImGuiHelpers.MainViewport.Pos + new Vector2(screenPos.X, screenPos.Y),
                size,
                ImGui.GetColorU32(colour),
                100
            );

            ImGui.PopClipRect();
        }

        private IPlayerCharacter? GetCurrentTarget() {
            var player = Service.ClientState.LocalPlayer;
            if (player == null) {
                return null;
            }

            var targetId = player.TargetObjectId;
            if (targetId <= 0) {
                return null;
            }

            return Service.ObjectTable
                .Where(actor => actor.GameObjectId == targetId && actor is IPlayerCharacter)
                .Select(actor => actor as IPlayerCharacter)
                .FirstOrDefault();
        }

        public void Open() {
            WantsOpen = true;
        }
    }
}
