using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.Style;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using Orchestrion.Audio;
using Orchestrion.BGMSystem;
using Orchestrion.Game;
using Orchestrion.Persistence;
using Orchestrion.Struct;

namespace Orchestrion.Windows;

public class MainWindow : Window, IDisposable
{
    private const string NoChange = "Do not change BGM";
    private const string SecAgo = "s ago";
    private const string MinAgo = "m ago";

    private readonly OrchestrionPlugin _orch;
    
    private readonly List<SongHistoryEntry> _songHistory = new();
    private readonly List<int> _removalList = new();

    private readonly ImGuiScene.TextureWrap _favoriteIcon;
    private string _searchText = string.Empty;
    private int _selectedHistoryEntry;
    private int _selectedSong;
    private SongReplacementEntry _tmpReplacement;
    private bool _bgmTooltipLock;
    
    // Playlist creation
    private Playlist _newPlaylist;
    private string _newPlaylistName = "";
    private int _newPlaylistSong = 0;
    
    public MainWindow(OrchestrionPlugin orch) : base("Orchestrion###Orchestrion")
    {
        _orch = orch;
        _favoriteIcon = LoadUIImage("favoriteIcon.png");
        
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
                WindowName = "Orchestrion";
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
        _favoriteIcon?.Dispose();
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

        if (ImGui.BeginTabBar("##_songList tabs"))
        {
            if (ImGui.BeginTabItem("All songs"))
            {
                DrawSongList();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Playlists"))
            {
                DrawPlaylists();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("History"))
            {
                DrawSongHistory();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Replacements"))
            {
                DrawReplacements();
                ImGui.EndTabItem();
            }
#if DEBUG
            if (ImGui.BeginTabItem("Debug"))
            {
                DrawDebug();
                ImGui.EndTabItem();
            }
#endif
            ImGui.EndTabBar();
        }

        DrawNewPlaylistModal();
    }

    private void DrawNewPlaylistModal()
    {
        if (_newPlaylistSong != 0)
            ImGui.OpenPopup("Create New Playlist");

        var a = true;
        if (ImGui.BeginPopupModal($"Create New Playlist", ref a, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text("Enter a name for your playlist:");
            ImGui.InputText("##newplaylistname", ref _newPlaylistName, 64);
            var invalid = string.IsNullOrWhiteSpace(_newPlaylistName) 
                          || string.IsNullOrEmpty(_newPlaylistName) 
                          || Configuration.Instance.Playlists.ContainsKey(_newPlaylistName);
            ImGui.BeginDisabled(invalid);
            if (ImGui.Button("Create"))
            {
                Configuration.Instance.Playlists.Add(_newPlaylistName, new Playlist(_newPlaylistName, new List<int> {_newPlaylistSong}));
                Configuration.Instance.Save();
                _newPlaylistName = "";
                _newPlaylistSong = 0;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndDisabled();
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                _newPlaylistName = "";
                _newPlaylistSong = 0;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
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

    private void DrawReplacements()
    {
        ImGui.BeginChild("##replacementlist");
        DrawCurrentReplacement();
        DrawReplacementList();
        ImGui.EndChild();
    }

    private void DrawReplacementList()
    {
        foreach (var replacement in Configuration.Instance.SongReplacements.Values)
        {
            ImGui.Spacing();
            SongList.Instance.TryGetSong(replacement.TargetSongId, out var target);

            var targetText = $"{replacement.TargetSongId} - {target.Name}";
            var replText = replacement.ReplacementId == SongReplacementEntry.NoChangeId ? NoChange : $"{replacement.ReplacementId} - {SongList.Instance.GetSong(replacement.ReplacementId).Name}";
            
            ImGui.TextWrapped($"{targetText}");
            if (ImGui.IsItemHovered())
                DrawBgmTooltip(target);

            ImGui.Text($"will be replaced with");
            ImGui.TextWrapped($"{replText}");
            if (ImGui.IsItemHovered() && replacement.ReplacementId != SongReplacementEntry.NoChangeId)
                DrawBgmTooltip(SongList.Instance.GetSong(replacement.ReplacementId));

            // Buttons in bottom right of area
            RightAlignButtons(ImGui.GetCursorPosY(), new[] {"Edit", "Delete"});
            if (ImGui.Button($"Edit##{replacement.TargetSongId}"))
            {
                _removalList.Add(replacement.TargetSongId);
                _tmpReplacement.TargetSongId = replacement.TargetSongId;
                _tmpReplacement.ReplacementId = replacement.ReplacementId;
            }
            ImGui.SameLine();
            if (ImGui.Button($"Delete##{replacement.TargetSongId}"))
                _removalList.Add(replacement.TargetSongId);

            ImGui.Separator();
        }

        if (_removalList.Count > 0)
        {
            foreach (var toRemove in _removalList)
                Configuration.Instance.SongReplacements.Remove(toRemove);
            _removalList.Clear();
            Configuration.Instance.Save();
        }
    }

    private void DrawDebug()
    {
        var addr = BGMAddressResolver.BGMSceneManager;
        if (addr == IntPtr.Zero) return;
        var addrStr = $"{addr.ToInt64():X}";
        ImGui.Text(addrStr);
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            ImGui.SetClipboardText(addrStr);
        ImGui.Text($"streaming enabled: {BGMAddressResolver.StreamingEnabled}");
        ImGui.Text($"PlayingScene: {BGMManager.PlayingScene}");
        ImGui.Text($"PlayingSongId: {BGMManager.PlayingSongId}");
        // ImGui.Text($"OldScene: {BGMManager.OldScene}");
        // ImGui.Text($"OldSongId: {BGMManager.OldSongId}");
        // ImGui.Text($"OldSecondScene: {BGMManager.OldSecondScene}");
        // ImGui.Text($"OldSecondSongId: {BGMManager.OldSecondSongId}");
        ImGui.Text($"Audible: {BGMManager.CurrentAudibleSong}");
    }

    private void DrawCurrentReplacement()
    {
        ImGui.Spacing();

        var targetText = $"{SongList.Instance.GetSong(_tmpReplacement.TargetSongId).Id} - {SongList.Instance.GetSong(_tmpReplacement.TargetSongId).Name}";
        string replacementText;
        if (_tmpReplacement.ReplacementId == SongReplacementEntry.NoChangeId)
            replacementText = NoChange;
        else
            replacementText = $"{SongList.Instance.GetSong(_tmpReplacement.ReplacementId).Id} - {SongList.Instance.GetSong(_tmpReplacement.ReplacementId).Name}";

        // This fixes the ultra-wide combo boxes, I guess
        var width = ImGui.GetWindowWidth() * 0.60f;

        if (ImGui.BeginCombo("Target Song", targetText))
        {
            foreach (var song in SongList.Instance.GetSongs().Values)
            {
                if (!SearchMatches(song)) continue;
                if (Configuration.Instance.SongReplacements.ContainsKey(song.Id)) continue;
                var tmpText = $"{song.Id} - {song.Name}";
                var tmpTextSize = ImGui.CalcTextSize(tmpText);
                var isSelected = _tmpReplacement.TargetSongId == song.Id;
                if (ImGui.Selectable(tmpText, isSelected, ImGuiSelectableFlags.None, new Vector2(width, tmpTextSize.Y)))
                    _tmpReplacement.TargetSongId = song.Id;
                if (ImGui.IsItemHovered())
                    DrawBgmTooltip(song);
            }

            ImGui.EndCombo();
        }

        ImGui.Spacing();

        if (ImGui.BeginCombo("Replacement Song", replacementText))
        {
            if (ImGui.Selectable(NoChange))
                _tmpReplacement.ReplacementId = SongReplacementEntry.NoChangeId;

            foreach (var song in SongList.Instance.GetSongs().Values)
            {
                if (!SearchMatches(song)) continue;
                var tmpText = $"{song.Id} - {song.Name}";
                var tmpTextSize = ImGui.CalcTextSize(tmpText);
                var isSelected = _tmpReplacement.ReplacementId == song.Id;
                if (ImGui.Selectable(tmpText, isSelected, ImGuiSelectableFlags.None, new Vector2(width, tmpTextSize.Y)))
                    _tmpReplacement.ReplacementId = song.Id;
                if (ImGui.IsItemHovered())
                    DrawBgmTooltip(song);
            }

            ImGui.EndCombo();
        }

        ImGui.Spacing();
        ImGui.Spacing();

        RightAlignButton(ImGui.GetCursorPosY(), "Add as song replacement");
        if (ImGui.Button("Add as song replacement"))
        {
            Configuration.Instance.SongReplacements.Add(_tmpReplacement.TargetSongId, _tmpReplacement);
            Configuration.Instance.Save();
            ResetReplacement();
        }

        ImGui.Separator();
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

    private void DrawSongList()
    {
        // to keep the tab bar always visible and not have it get scrolled out
        ImGui.BeginChild("##_songList_internal", ImGuiHelpers.ScaledVector2(-1f, -25f));

        if (ImGui.BeginTable("_songList table", 4, ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("id", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("title", ImGuiTableColumnFlags.WidthStretch);
            
            foreach (var s in SongList.Instance.GetSongs())
            {
                var song = s.Value;
                if (!SearchMatches(song)) continue;

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                DrawSongListItem(song);
            }

            ImGui.EndTable();
        }

        ImGui.EndChild();
        DrawFooter();
    }

    private void DrawPlayingUI(TimeSpan elapsed, TimeSpan total)
    {
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, ImGuiColors.DalamudWhite);
        var frac = elapsed.TotalMilliseconds / total.TotalMilliseconds;
        if (elapsed == TimeSpan.Zero && total == TimeSpan.Zero)
            frac = 0;
        ImGui.ProgressBar((float)frac, new Vector2(-1, 4));
        ImGui.PopStyleColor();
        var current = $"{elapsed:mm\\:ss}";
        var max = $"{total:mm\\:ss}";
        var x = ImGui.GetCursorPosX();
        ImGui.Text(current);
        ImGui.SameLine();
        ImGui.SetCursorPosX(x);
        RightAlignText(ImGui.GetCursorPosY(), max);
        ImGui.Text(max);
    }
    
    private void DrawPlaylists()
    {
        var elapsed = TimeSpan.Zero;
        var total = TimeSpan.Zero;

        if (PlaylistManager.IsPlaying)
        {
            elapsed = PlaylistManager.ElapsedDuration;
            total = PlaylistManager.Duration;
        }

        DrawPlayingUI(elapsed, total);

        ImGui.BeginChild("##playlist_internal", ImGuiHelpers.ScaledVector2(-1f, -25f));
        foreach (var playlist in Configuration.Instance.Playlists)
        {
            var pName = playlist.Value.Name;
            var v = DalamudApi.PluginInterface.UiBuilder.GetGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.Axis18));
            ImGui.PushFont(v.ImFont);
            var collHdr = ImGui.CollapsingHeader($"{pName}##pnameheader");
            ImGui.PopFont();
            ImGui.SameLine();
            if (ImGuiComponents.IconButton($"##{pName}_prev", FontAwesomeIcon.EllipsisV))
            {
                ImGui.OpenPopup($"##{pName}_popup");
            }

            if (collHdr)
            {
                if (ImGui.BeginPopup($"##{pName}_popup"))
                {
                    if (ImGuiComponents.IconButton($"##{pName}_edit", FontAwesomeIcon.Edit))
                    {
                        // _playlistEdit = playlist.Value;
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SameLine();
                    if (ImGuiComponents.IconButton($"##{pName}_del", FontAwesomeIcon.Trash))
                    {
                        Configuration.Instance.Playlists.Remove(playlist.Key);
                        Configuration.Instance.Save();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }
                
                if (ImGuiComponents.IconButton($"##{pName}_prev", FontAwesomeIcon.Backward))
                {
                    PlaylistManager.Next();
                }
                ImGui.SameLine();
                if (PlaylistManager.CurrentPlaylist?.Name == pName)
                {
                    if (ImGuiComponents.IconButton($"##{pName}_stop", FontAwesomeIcon.Stop))
                        PlaylistManager.Stop();
                }
                else if (ImGuiComponents.IconButton($"##{pName}_play", FontAwesomeIcon.Play))
                {
                    PlaylistManager.Play(playlist.Value.Name);
                }
                ImGui.SameLine();
                if (ImGuiComponents.IconButton($"##{pName}_next", FontAwesomeIcon.Forward))
                {
                    PlaylistManager.Next();
                }
                ImGui.SameLine();
                if (ImGuiComponents.IconButton($"##{pName}_del", FontAwesomeIcon.Trash))
                {
                    Configuration.Instance.Playlists.Remove(playlist.Key);
                    Configuration.Instance.Save();
                }
                
                if (ImGui.Button($"Repeat: {playlist.Value.RepeatMode}##{pName}_repeat"))
                {
                    // cycle through repeat modes
                    playlist.Value.RepeatMode = (RepeatMode)(((int)playlist.Value.RepeatMode + 1) % 3);
                }
                ImGui.SameLine();
                if (ImGui.Button($"Shuffle: {playlist.Value.ShuffleMode}##{pName}_shuffle"))
                {
                    // cycle through shuffle modes
                    playlist.Value.ShuffleMode = (ShuffleMode)(((int)playlist.Value.ShuffleMode + 1) % 2);
                }

                if (ImGui.BeginTable($"playlisttable{playlist.Value.Name}", 4, ImGuiTableFlags.SizingFixedFit))
                {
                    ImGui.TableSetupColumn("id", ImGuiTableColumnFlags.WidthFixed);
                    ImGui.TableSetupColumn("title", ImGuiTableColumnFlags.WidthStretch);

                    foreach (var s in playlist.Value.Songs)
                    {
                        if (!SongList.Instance.TryGetSong(s, out var song)) continue;
                        if (!SearchMatches(song)) continue;

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();

                        DrawSongListItem(song);
                    }

                    ImGui.EndTable();
                }
            }
        }
        ImGui.EndChild();
        DrawFooter();
    }
/*
    private unsafe void DrawPlaylists2()
    {
        var elapsed = TimeSpan.Zero;
        var total = TimeSpan.Zero;

        if (PlaylistManager.IsPlaying)
        {
            elapsed = PlaylistManager.ElapsedDuration;
            total = PlaylistManager.Duration;
        }

        DrawPlayingUI(elapsed, total);

        ImGui.BeginChild("##playlist_internal", ImGuiHelpers.ScaledVector2(-1f, -25f));
        foreach (var playlist in Configuration.Instance.Playlists)
        {
            var pName = playlist.Value.Name;
            var v = DalamudApi.PluginInterface.UiBuilder.GetGameFontHandle(new GameFontStyle(GameFontFamilyAndSize.Axis18));
            ImGui.PushFont(v.ImFont);
            ImGui.Text(pName);
            ImGui.PopFont();

            // ImGui.SameLine();
            // if (ImGuiComponents.IconButton($"##{pName}_prev", FontAwesomeIcon.FastBackward))
            ImGui.PushFont(OrchestrionPlugin.SymbolsFont);
            if (ImGui.Button($"{Icons.More}##{pName}_more"))
            {
                ImGui.OpenPopup($"##playlist_{pName}_more");
            }

            // ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
            if (ImGui.Button($"{Icons.Previous}##{pName}_prev"))
            {
                PlaylistManager.Next();
            }
            ImGui.SameLine();
            if (PlaylistManager.CurrentPlaylist?.Name == pName)
            {
                // if (ImGuiComponents.IconButton($"##{pName}_stop", FontAwesomeIcon.Stop))
                if (ImGui.Button($"{Icons.Stop}##{pName}_stop"))
                    PlaylistManager.Stop();
            }
            // else if (ImGuiComponents.IconButton($"##{pName}_play", FontAwesomeIcon.Play))
            else if (ImGui.Button($"{Icons.Play}##{pName}_play"))
            {
                PlaylistManager.Play(playlist.Value.Name);
            }
            ImGui.SameLine();
            // if (ImGuiComponents.IconButton($"##{pName}_next", FontAwesomeIcon.FastForward))
            if (ImGui.Button($"{Icons.Next}##{pName}_next"))
            {
                PlaylistManager.Next();
            }
            ImGui.SameLine();
            if (ImGui.Button($"{Icons.Trash}##{pName}_del"))
            {
                Configuration.Instance.Playlists.Remove(playlist.Key);
                Configuration.Instance.Save();
            }
            ImGui.PopFont();
            // ImGui.PopStyleVar();
            
            if (ImGui.BeginPopup($"##playlist_{pName}_more"))
            {
                if (ImGui.MenuItem("Edit"))
                {
                    // _playlistEdit = playlist.Value;
                    // _playlistEditName = playlist.Value.Name;
                }
                if (ImGui.MenuItem("Delete"))
                {
                    Configuration.Instance.Playlists.Remove(playlist.Key);
                    Configuration.Instance.Save();
                }
                ImGui.EndPopup();
            }
            
            // var repeatModeStr = playlist.Value.RepeatMode == 
            
            if (ImGui.Button($"Repeat: {playlist.Value.RepeatMode}##{pName}_repeat"))
            {
                // cycle through repeat modes
                playlist.Value.RepeatMode = (RepeatMode)(((int)playlist.Value.RepeatMode + 1) % 3);
            }
            ImGui.SameLine();
            if (ImGui.Button($"Shuffle: {playlist.Value.ShuffleMode}##{pName}_shuffle"))
            {
                // cycle through shuffle modes
                playlist.Value.ShuffleMode = (ShuffleMode)(((int)playlist.Value.ShuffleMode + 1) % 2);
            }
            // ImGuiComponents.IconButton(FontAwesomeIcon.Random, ImGuiColors.DalamudRed, ImGuiColors.HealerGreen);

            if (ImGui.BeginTable($"playlisttable{playlist.Value.Name}", 4, ImGuiTableFlags.SizingFixedFit))
            {
                ImGui.TableSetupColumn("id", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("title", ImGuiTableColumnFlags.WidthStretch);

                foreach (var s in playlist.Value.Songs)
                {
                    if (!SongList.Instance.TryGetSong(s, out var song)) continue;
                    if (!SearchMatches(song)) continue;

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    DrawSongListItem(song);
                }

                ImGui.EndTable();
            }
        }
        ImGui.EndChild();
        DrawFooter();
    }
*/
    private void DrawSongHistory()
    {
        // to keep the tab bar always visible and not have it get scrolled out
        ImGui.BeginChild("##_songList_internal", new Vector2(-1f, -60f));
        
        if (ImGui.BeginTable("_songList table", 4, ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("id", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("title", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("time", ImGuiTableColumnFlags.WidthFixed);
            
            // going from the end of the list
            for (int i = _songHistory.Count - 1; i >= 0; i--)
            {
                var songHistoryEntry = _songHistory[i];
                var song = SongList.Instance.GetSong(songHistoryEntry.Id);

                if (!SearchMatches(song))
                    continue;

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                DrawSongListItem(song, i, songHistoryEntry.TimePlayed);
            }

            ImGui.EndTable();
        }

        ImGui.EndChild();
        DrawFooter(true);
    }

    // Uses 2 columns, 3 if timePlayed is specified
    private void DrawSongListItem(Song song, int historyIndex = 0, DateTime timePlayed = default)
    {
        var isHistory = timePlayed != default;
        
        ImGui.Text(song.Id.ToString());
        ImGui.TableNextColumn();

        var selected = false;

        if (isHistory)
            selected = _selectedHistoryEntry == historyIndex;
        else
            selected = _selectedSong == song.Id;

        if (ImGui.Selectable($"{song.Name}##{song.Id}{timePlayed}", selected, ImGuiSelectableFlags.AllowDoubleClick))
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

            if (ImGui.BeginMenu($"Add to..."))
            {
                foreach (var p in Configuration.Instance.Playlists)
                {
                    if (ImGui.MenuItem(p.Value.Name))
                    {
                        p.Value.Songs.Add(song.Id);
                        Configuration.Instance.Save();
                    }
                }
                
                ImGui.Separator();
                
                if (ImGui.MenuItem("New playlist..."))
                {
                    PluginLog.Debug("Opening new playlist popup...");
                    _newPlaylistSong = song.Id;
                }

                ImGui.EndMenu();
            }

            ImGui.EndPopup();
        }

        ImGui.TableNextColumn();
        if (!isHistory) return;
        
        var deltaTime = DateTime.Now - timePlayed;
        var unit = deltaTime.TotalMinutes >= 1 ? (int)deltaTime.TotalMinutes : (int)deltaTime.TotalSeconds;
        var label = deltaTime.TotalMinutes >= 1 ? MinAgo : SecAgo;
        ImGui.Text($"{unit}{label}");
        // ImGui.TableNextColumn();
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
    
    public void AddSongToHistory(int id)
    {
        // Don't add silence
        if (id == 1 || !SongList.Instance.TryGetSong(id, out _))
            return;

        var newEntry = new SongHistoryEntry
        {
            Id = id,
            TimePlayed = DateTime.Now
        };

        var currentIndex = _songHistory.Count - 1;

        // Check if we have history, if yes, then check if ID is the same as previous, if not, add to history
        if (currentIndex < 0 || _songHistory[currentIndex].Id != id)
        {
            _songHistory.Add(newEntry);
            PluginLog.Verbose($"[AddSongToHistory] Added {id} to history. There are now {currentIndex + 1} songs in history.");
        }
    }

    private void RightAlignButton(float y, string text)
    {
        var style = ImGui.GetStyle();
        var padding = style.WindowPadding.X + style.FramePadding.X * 2 + style.ScrollbarSize;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetWindowWidth() - ImGui.CalcTextSize(text).X - padding);
        ImGui.SetCursorPosY(y);
    }
    
    private void RightAlignText(float y, string text)
    {
        var style = ImGui.GetStyle();
        var padding = style.WindowPadding.X + style.ScrollbarSize;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetWindowWidth() - ImGui.CalcTextSize(text).X - padding);
        ImGui.SetCursorPosY(y);
    }
    
    private void RightAlignButtons(float y, string[] texts)
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