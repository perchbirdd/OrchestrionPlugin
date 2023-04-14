using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;
using Orchestrion.Audio;
using Orchestrion.Persistence;
using Orchestrion.Struct;

namespace Orchestrion.Windows.MainWindow;

public partial class MainWindow : Window, IDisposable
{
    private const string NoChange = "Do not change BGM";
    private const string SecAgo = "s ago";
    private const string MinAgo = "m ago";
    private const string BaseName = "Orchestrion###Orchestrion";

    private readonly OrchestrionPlugin _orch;

    private string _searchText = string.Empty;
    private int _selectedSong;
    private bool _bgmTooltipLock;

    public MainWindow(OrchestrionPlugin orch) : base(BaseName, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        _orch = orch;
        
        BGMManager.OnSongChanged += UpdateTitle;
        ResetReplacement();

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(370, 400),
            MaximumSize = ImGuiHelpers.MainViewport.Size,
        };
    }
    
    private void UpdateTitle(int oldSong, int currentSong, bool playedByOrchestrion)
    {
        var currentChanged = oldSong != currentSong;
        if (!currentChanged) return;

        AddSongToHistory(currentSong);
        
        if (Configuration.Instance.ShowSongInTitleBar)
        {
            if (currentSong == 0)
                WindowName = BaseName;
            else
            {
                PluginLog.Debug("[UpdateTitle] Updating title bar");
                var songTitle = SongList.Instance.GetSongTitle(currentSong);
                WindowName = $"Orchestrion - [{currentSong}] {songTitle}###Orchestrion";   
            }
        }
    }

    public void Dispose()
    {
        BGMManager.Stop();
    }
    
    private void ResetReplacement()
    {
        var id = SongList.Instance.GetFirstReplacementCandidateId();
        _tmpReplacement = new SongReplacementEntry
        {
            TargetSongId = id,
            ReplacementId = SongReplacementEntry.NoChangeId,
        };
    }

    public override void PreDraw()
    {
        ImGui.PushStyleColor(ImGuiCol.ResizeGrip, 0);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripActive, 0);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripHovered, 0);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor(3);
    }

    public override void Draw()
    {
        _bgmTooltipLock = false;
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Search: ");
        ImGui.SameLine();
        ImGui.InputText("##searchbox", ref _searchText, 32);

        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetWindowSize().X - (35 * ImGuiHelpers.GlobalScale));
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (1 * ImGuiHelpers.GlobalScale));
        
        if (ImGuiComponents.IconButton("##orchsettings", FontAwesomeIcon.Cog))
            _orch.OpenSettingsWindow();

        if (ImGui.BeginTabBar("##orchtabs"))
        {
            DrawTab("All Songs", DrawSongList);
            DrawTab("Playlists", DrawPlaylistsTab);
            DrawTab("History", DrawSongHistory);
            DrawTab("Replacements", DrawReplacements);
#if DEBUG
            DrawTab("Debug", DrawDebug);
#endif
            ImGui.EndTabBar();
        }

        DrawNewPlaylistModal();
    }

    private void DrawTab(string name, Action render)
    {
        if (ImGui.BeginTabItem(name))
        {
            render();
            ImGui.EndTabItem();
        }
    }
    
    private void DrawFooter(bool isHistory = false)
    {
        var songId = _selectedSong;

        if (isHistory && _songHistory.Count > _selectedHistoryEntry)
            songId = _songHistory[_selectedHistoryEntry].Id;
        else if (isHistory)
            songId = 0;
        
        if (SongList.Instance.TryGetSong(songId, out var song))
        {
            // ImGui.TextWrapped(song.Locations);
            // ImGui.TextWrapped(song.AdditionalInfo);
        }

        var width = ImGui.GetContentRegionAvail().X - ImGui.GetStyle().WindowPadding.X;
        var buttonHeight = ImGui.CalcTextSize("Stop").Y + ImGui.GetStyle().FramePadding.Y * 2f;

        ImGui.SetCursorPosY(ImGui.GetWindowSize().Y - buttonHeight - ImGui.GetStyle().WindowPadding.Y);
        
        if (BGMManager.PlayingSongId == 0) ImGui.BeginDisabled();
        if (ImGui.Button("Stop", new Vector2(width / 2, buttonHeight)))
            BGMManager.Stop();
        if (BGMManager.PlayingSongId == 0) ImGui.EndDisabled();
        
        ImGui.SameLine();
        
        ImGui.BeginDisabled(!song.FileExists);
        if (ImGui.Button("Play", new Vector2(width / 2, buttonHeight)))
            BGMManager.Play(_selectedSong);
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            DrawBgmTooltip(song);
    }

    private bool SearchMatches(Song song)
    {
        var matchesSearch = _searchText.Length != 0
                            && (song.Name.ToLower().Contains(_searchText.ToLower())
                                || song.Locations.ToLower().Contains(_searchText.ToLower())
                                || song.AdditionalInfo.ToLower().Contains(_searchText.ToLower())
                                || song.Id.ToString().Contains(_searchText));
        var searchEmpty = _searchText.Length == 0;
        return matchesSearch || searchEmpty;
    }

    private void DrawSongListItem(Song song, int historyIndex = 0, DateTime timePlayed = default)
    {
        var isHistory = timePlayed != default;
        
        ImGui.Text(song.Id.ToString());
        ImGui.TableNextColumn();

        bool selected;

        if (isHistory)
            selected = _selectedHistoryEntry == historyIndex;
        else
            selected = _selectedSong == song.Id;

        if (ImGui.Selectable($"{song.Name}##{song.Id}{timePlayed}", selected, ImGuiSelectableFlags.AllowDoubleClick | ImGuiSelectableFlags.SpanAllColumns))
        {
            _selectedSong = song.Id;
            if (isHistory) _selectedHistoryEntry = historyIndex;
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                BGMManager.Play(_selectedSong);
        }

        if (ImGui.IsItemHovered())
            DrawBgmTooltip(song);
        
        if (ImGui.BeginPopupContextItem())
        {
            _selectedSong = song.Id;
            if (isHistory) _selectedHistoryEntry = historyIndex;

            DrawCopyContentSubmenu(song);
            ImGui.Separator();
            DrawPlaylistAddSubmenu(song);

            ImGui.EndPopup();
        }

        ImGui.TableNextColumn();
        if (!isHistory) return;
        
        var deltaTime = DateTime.Now - timePlayed;
        var unit = deltaTime.TotalMinutes >= 1 ? (int)deltaTime.TotalMinutes : (int)deltaTime.TotalSeconds;
        var label = deltaTime.TotalMinutes >= 1 ? MinAgo : SecAgo;
        ImGui.Text($"{unit}{label}");
    }

    private void DrawPlaylistAddSubmenu(Song song)
    {
        if (ImGui.BeginMenu($"Add to..."))
        {
            foreach (var p in Configuration.Instance.Playlists.Values)
                if (ImGui.MenuItem(p.Name))
                    p.AddSong(song.Id);
                
            ImGui.Separator();
                
            if (ImGui.MenuItem("New playlist..."))
            {
                PluginLog.Debug("Opening new playlist popup...");
                _newPlaylistSong = song.Id;
                _newPlaylistModal = true;
            }

            ImGui.EndMenu();
        }
    }

    private void DrawCopyContentSubmenu(Song song)
    {
        if (ImGui.BeginMenu("Copy"))
        {
            if (ImGui.MenuItem("Song ID"))
                ImGui.SetClipboardText(song.Id.ToString());
            if (ImGui.MenuItem("Song Name"))
                ImGui.SetClipboardText(song.Name);
            if (ImGui.MenuItem("Song Location"))
                ImGui.SetClipboardText(song.Locations);
            if (ImGui.MenuItem("Song Info"))
                ImGui.SetClipboardText(song.AdditionalInfo);
            if (ImGui.MenuItem("Duration"))
                ImGui.SetClipboardText($"{song.Duration:mm\\:ss}");
            if (ImGui.MenuItem("Song File Path"))
                ImGui.SetClipboardText(song.FilePath);
            ImGui.Separator();
            if (ImGui.MenuItem("All"))
            {
                var text = $"Song ID: {song.Id}\n" +
                           $"Song Name: {song.Name}\n" +
                           $"Song Location: {song.Locations}\n" +
                           $"Song Info: {song.AdditionalInfo}\n" +
                           $"Duration: {song.Duration:mm\\:ss}\n" +
                           $"Song File Path: {song.FilePath}";
                ImGui.SetClipboardText(text);
            }
            ImGui.EndMenu();
        }
    }
    
    private static string BuildCopyString(HashSet<int> songs, Func<Song, string> field)
    {
        var sb = new StringBuilder();
        foreach (var song in songs)
            sb.Append($"{field.Invoke(SongList.Instance.GetSong(song))}, ");
        var text = sb.ToString();
        return text[^2..];
    }

    private void DrawBgmTooltip(Song bgm)
    {
        if (bgm.Id == 0) return;
        if (_bgmTooltipLock) return;
        _bgmTooltipLock = true;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(450 * ImGuiHelpers.GlobalScale);
        ImGui.TextColored(new Vector4(0, 1, 0, 1), "Song Info");
        ImGui.TextColored(ImGuiColors.DalamudGrey, "Title: ");
        ImGui.SameLine();
        ImGui.TextWrapped(bgm.Name);
        ImGui.TextColored(ImGuiColors.DalamudGrey, "Location: ");
        ImGui.SameLine();
        ImGui.TextWrapped(string.IsNullOrEmpty(bgm.Locations) ? "Unknown" : bgm.Locations);
        if (!string.IsNullOrEmpty(bgm.AdditionalInfo))
        {
            ImGui.TextColored(ImGuiColors.DalamudGrey, "Info: ");
            ImGui.SameLine();
            ImGui.TextWrapped(bgm.AdditionalInfo);
        }
        ImGui.TextColored(ImGuiColors.DalamudGrey, "Duration: ");
        ImGui.SameLine();
        ImGui.TextWrapped($"{bgm.Duration:mm\\:ss}");
        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
        if (!bgm.FileExists)
            ImGui.TextWrapped("This song is unavailable; the track is not present in the current game files.");
        ImGui.PopStyleColor();
        if (Configuration.Instance.ShowFilePaths)
        {
            ImGui.TextColored(ImGuiColors.DalamudGrey, "File Path: ");
            ImGui.SameLine();
            ImGui.TextWrapped(bgm.FilePath);
        }
        
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    private static void RightAlignButton(float y, string text)
    {
        var style = ImGui.GetStyle();
        var padding = style.WindowPadding.X + style.FramePadding.X * 2 + style.ScrollbarSize;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetWindowWidth() - ImGui.CalcTextSize(text).X - padding);
        ImGui.SetCursorPosY(y);
    }
    
    private static void RightAlignText(float y, string text)
    {
        var style = ImGui.GetStyle();
        var padding = style.WindowPadding.X + style.ScrollbarSize;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetWindowWidth() - ImGui.CalcTextSize(text).X - padding);
        ImGui.SetCursorPosY(y);
    }
    
    private static void RightAlignButtons(float y, string[] texts)
    {
        var style = ImGui.GetStyle();
        var padding = style.WindowPadding.X + style.FramePadding.X * 2 + style.ScrollbarSize;

        var cursor = ImGui.GetCursorPosX() + ImGui.GetWindowWidth();
        foreach (var text in texts)
        {
            cursor -= ImGui.CalcTextSize(text).X + padding;
        }
        
        ImGui.SetCursorPosX(cursor);
        ImGui.SetCursorPosY(y);
    }

    private ImGuiScene.TextureWrap LoadUIImage(string imageFile)
    {
        var path = Path.Combine(Path.GetDirectoryName(DalamudApi.PluginInterface.AssemblyLocation.FullName)!, imageFile);
        return DalamudApi.PluginInterface.UiBuilder.LoadImage(path);
    }
}