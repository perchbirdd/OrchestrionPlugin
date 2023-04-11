using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;
using Orchestrion.Game;
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
            PluginLog.Debug("[UpdateTitle] Updating title bar");
            var songTitle = SongList.Instance.GetSongTitle(currentSong);
            WindowName = $"Orchestrion - [{currentSong}] {songTitle}###Orchestrion";
        }
    }

    public void Dispose()
    {
        Stop();
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

    private void Play(int songId)
    {
        BGMManager.Play(songId);
    }

    private void Stop()
    {
        BGMManager.Stop();
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
                DrawSongList(false);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Favorites"))
            {
                DrawSongList(true);
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
            Stop();
        if (BGMManager.PlayingSongId == 0) ImGui.EndDisabled();
        
        ImGui.SameLine();
        
        ImGui.BeginDisabled(!song.FileExists);
        if (ImGui.Button("Play", new Vector2(width / 2, buttonHeight)))
            Play(_selectedSong);
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

    private int _startTime;
    private int _expiryTime;

    private int _index;
    private readonly List<Tuple<int, int>> _test = new List<Tuple<int, int>>()
    {
        // new(957, 214),
        new(896, 87),
        new(901, 97),
        new(902, 50),
    };

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

        if (ImGui.Button("Play with expiry"))
        {
            Play(_test[_index].Item1);
            _startTime = Environment.TickCount;
            _expiryTime = _startTime + _test[_index].Item2 * 1000;
        }
        
        if (_startTime > 0)
        {
            var timeLeft = _expiryTime - Environment.TickCount;
            if (timeLeft < 0)
            {
                _index++;
                if (_index >= _test.Count)
                    _index = 0;
                Play(_test[_index].Item1);
                _startTime = Environment.TickCount;
                _expiryTime = _startTime + _test[_index].Item2 * 1000;
            }
            else
            {
                var timeSpent = Environment.TickCount - _startTime;
                var ts2 = TimeSpan.FromMilliseconds(timeSpent);
                var len = _test[_index].Item2 * 1000;
                var frac = timeLeft / (float) len;
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, ImGuiColors.DalamudWhite);
                ImGui.ProgressBar(1 - frac, new Vector2(-1, 4));
                ImGui.PopStyleColor();
                var current = $"{ts2:mm\\:ss}";
                var max = $"{TimeSpan.FromMilliseconds(len):mm\\:ss}";
                var x = ImGui.GetCursorPosX();
                ImGui.Text(current);
                ImGui.SameLine();
                ImGui.SetCursorPosX(x);
                RightAlignText(ImGui.GetCursorPosY(), max);
                ImGui.Text(max);
                // ImGui.Text($"Time left: {timeLeft / 1000f:F1} seconds");
            }
        }

        if (ImGui.Button("Stop"))
        {
            Stop();
            _index = 0;
            _startTime = 0;
        }
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

    private void DrawSongList(bool favoritesOnly)
    {
        // to keep the tab bar always visible and not have it get scrolled out
        ImGui.BeginChild("##_songList_internal", ImGuiHelpers.ScaledVector2(-1f, -25f));

        if (ImGui.BeginTable("_songList table", 4, ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("fav", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("id", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("title", ImGuiTableColumnFlags.WidthStretch);
            
            foreach (var s in SongList.Instance.GetSongs())
            {
                var song = s.Value;
                if (!SearchMatches(song))
                    continue;

                var isFavorite = Configuration.Instance.IsFavorite(song.Id);

                if (favoritesOnly && !isFavorite)
                    continue;

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                DrawSongListItem(song);
            }

            ImGui.EndTable();
        }

        ImGui.EndChild();
        DrawFooter();
    }

    private void DrawSongHistory()
    {
        // to keep the tab bar always visible and not have it get scrolled out
        ImGui.BeginChild("##_songList_internal", new Vector2(-1f, -60f));
        
        if (ImGui.BeginTable("_songList table", 4, ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("fav", ImGuiTableColumnFlags.WidthFixed);
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
        var isFavorite = Configuration.Instance.IsFavorite(song.Id);
        var isHistory = timePlayed != default;

        if (isFavorite)
        {
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 3);
            ImGui.Image(_favoriteIcon.ImGuiHandle, new Vector2(13, 13));
        }

        ImGui.TableNextColumn();

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
                Play(_selectedSong);
        }

        if (ImGui.IsItemHovered())
            DrawBgmTooltip(song);

        if (ImGui.BeginPopupContextItem())
        {
            _selectedSong = song.Id;
            if (isHistory) _selectedHistoryEntry = historyIndex;
            
            if (!isFavorite)
            {
                if (ImGui.Selectable("Add to favorites"))
                    Configuration.Instance.AddFavorite(song.Id);
            }
            else
            {
                if (ImGui.Selectable("Remove from favorites"))
                    Configuration.Instance.RemoveFavorite(song.Id);
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
        ImGui.TextWrapped($"Title: {bgm.Name}");
        ImGui.TextWrapped(string.IsNullOrEmpty(bgm.Locations) ? "Location: Unknown" : $"Location: {bgm.Locations}");
        if (!string.IsNullOrEmpty(bgm.AdditionalInfo))
            ImGui.TextWrapped($"Info: {bgm.AdditionalInfo}");
        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
        if (!bgm.FileExists)
            ImGui.TextWrapped("This song is unavailable; the track is not present in the current game files.");
        ImGui.PopStyleColor();
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