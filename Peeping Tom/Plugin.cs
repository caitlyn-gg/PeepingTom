using Dalamud.Game.Command;
using Dalamud.Plugin;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Game.ClientState.Objects;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using PeepingTom.Resources;
using Lumina.Excel.Sheets;

namespace PeepingTom {
    // ReSharper disable once ClassNeverInstantiated.Global
    public class Plugin : IDalamudPlugin {
        internal static string Name => "Peeping Tom";

        internal Configuration Config { get; }
        internal PluginUi Ui { get; }
        internal TargetWatcher Watcher { get; }
        internal IpcManager IpcManager { get; }

        internal bool InPvp { get; private set; }

        public Plugin(IDalamudPluginInterface pluginInterface, IFramework framework, ICommandManager commandManager) {
            pluginInterface.Create<Service>();
            
            this.Config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Config.Initialize(Service.Interface);
            this.Watcher = new TargetWatcher(this);
            this.Ui = new PluginUi(this);
            this.IpcManager = new IpcManager(this);

            OnLanguageChange(Service.Interface.UiLanguage);
            Service.Interface.LanguageChanged += OnLanguageChange;

            Service.CommandManager.AddHandler("/ppeepingtom", new CommandInfo(this.OnCommand) {
                HelpMessage = "Use with no arguments to show the list. Use with \"c\" or \"config\" to show the config",
            });
            Service.CommandManager.AddHandler("/ptom", new CommandInfo(this.OnCommand) {
                HelpMessage = "Alias for /ppeepingtom",
            });
            Service.CommandManager.AddHandler("/ppeep", new CommandInfo(this.OnCommand) {
                HelpMessage = "Alias for /ppeepingtom",
            });

            Service.ClientState.Login += this.OnLogin;
            Service.ClientState.Logout += this.OnLogout;
            Service.ClientState.TerritoryChanged += this.OnTerritoryChange;
            Service.Interface.UiBuilder.Draw += this.DrawUi;
            Service.Interface.UiBuilder.OpenConfigUi += this.ConfigUi;
            Service.Interface.UiBuilder.OpenMainUi += this.Ui.Open;
        }

        public void Dispose() {
            Service.Interface.UiBuilder.OpenConfigUi -= this.ConfigUi;
            Service.Interface.UiBuilder.OpenMainUi -= this.Ui.Open;
            Service.Interface.UiBuilder.Draw -= this.DrawUi;
            Service.ClientState.TerritoryChanged -= this.OnTerritoryChange;
            Service.ClientState.Logout -= this.OnLogout;
            Service.ClientState.Login -= this.OnLogin;
            Service.CommandManager.RemoveHandler("/ppeep");
            Service.CommandManager.RemoveHandler("/ptom");
            Service.CommandManager.RemoveHandler("/ppeepingtom");
            Service.Interface.LanguageChanged -= OnLanguageChange;
            this.IpcManager.Dispose();
            this.Ui.Dispose();
            this.Watcher.Dispose();
        }

        private static void OnLanguageChange(string langCode) {
            Language.Culture = new CultureInfo(langCode);
        }

        private void OnTerritoryChange(ushort e) {
            try {
                var territory = Service.DataManager.GetExcelSheet<TerritoryType>().GetRow(e);
                this.InPvp = territory.IsPvpZone == true;
            } catch (KeyNotFoundException) {
                Service.Log.Warning("Could not get territory for current zone");
            }
        }

        private void OnCommand(string command, string args) {
            if (args is "config" or "c") {
                this.Ui.SettingsOpen = true;
            } else {
                this.Ui.WantsOpen = true;
            }
        }

        private void OnLogin() {
            if (!this.Config.OpenOnLogin) {
                return;
            }

            this.Ui.WantsOpen = true;
        }

        private void OnLogout(int type, int code) {
            this.Ui.WantsOpen = false;
            this.Watcher.ClearPrevious();
        }

        private void DrawUi() {
            this.Ui.Draw();
        }

        private void ConfigUi() {
            this.Ui.SettingsOpen = true;
        }
    }
}
