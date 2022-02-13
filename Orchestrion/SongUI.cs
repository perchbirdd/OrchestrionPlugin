using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Interface;
using Dalamud.Logging;

namespace Orchestrion;

public struct SongReplacement
{
    /// <summary>
    /// The ID of the song to replace.
    /// </summary>
    public int TargetSongId;

    /// <summary>
    /// The ID of the replacement track to play. -1 means "ignore this song" and continue playing
    /// what was previously playing.
    /// </summary>
    public int ReplacementId;
}

public struct SongHistoryEntry
{
    public int Id;
    public DateTime TimePlayed;
}

public class SongUI : IDisposable
{
    private static float Scale => ImGui.GetIO().FontGlobalScale;

    private const string NoChange = "Do not change BGM";
    private const string SecAgo = "s ago";
    private const string MinAgo = "m ago";
    
    private int selectedSong;
    private int selectedHistoryEntry;
    private string searchText = string.Empty;
    private ImGuiScene.TextureWrap favoriteIcon;
    private ImGuiScene.TextureWrap settingsIcon;

    private readonly OrchestrionPlugin orch;
    private readonly List<SongHistoryEntry> songHistory = new();
    private readonly List<int> removalList = new();
    private SongReplacement tmpReplacement;

    private bool visible;
    public bool Visible
    {
        get => visible;
        set => visible = value;
    }

    private bool settingsVisible;
    public bool SettingsVisible
    {
        get => settingsVisible;
        set => settingsVisible = value;
    }

    public SongUI(OrchestrionPlugin orch)
    {
        this.orch = orch;

        ResetReplacement();
    }

    public void Dispose()
    {
        Stop();
        favoriteIcon?.Dispose();
        settingsIcon?.Dispose();
    }

    private void ResetReplacement()
    {
        var id = SongList.GetFirstReplacementCandidateId();
        tmpReplacement = new SongReplacement
        {
            TargetSongId = id,
            ReplacementId = -1,
        };
    }

    private void Play(int songId)
    {
        orch.PlaySong(songId);
    }

    private void Stop()
    {
        orch.StopSong();
    }

    public void AddSongToHistory(int id)
    {
        // Don't add silence
        if (id == 1 || !SongList.TryGetSong(id, out _))
            return;

        var newEntry = new SongHistoryEntry
        {
            Id = id,
            TimePlayed = DateTime.Now
        };

        var currentIndex = songHistory.Count - 1;

        // Check if we have history, if yes, then check if ID is the same as previous, if not, add to history
        if (currentIndex < 0 || songHistory[currentIndex].Id != id)
        {
            songHistory.Add(newEntry);
            PluginLog.Debug($"Added {id} to history. There are now {currentIndex + 1} songs in history.");
        }
    }

    public void Draw()
    {
        DrawMainWindow();
        DrawSettings();
    }

    private void DrawMainWindow()
    {
        // temporary bugfix for a race condition where it was possible that
        // we would attempt to load the icon before the ImGuiScene was created in dalamud
        // which would fail and lead to this icon being null
        // Hopefully later the UIBuilder API can add an event to notify when it is ready
        if (favoriteIcon == null)
        {
            favoriteIcon = LoadUIImage("favoriteIcon.png");
            settingsIcon = LoadUIImage("settings.png");
        }

        if (!Visible) return;

        var windowTitle = new StringBuilder("Orchestrion");
        if (orch.Configuration.ShowSongInTitleBar)
        {
            // TODO: subscribe to the event so this only has to be constructed on change?
            var songId = orch.CurrentSong;
            if (SongList.TryGetSong(songId, out var song))
                windowTitle.Append($" - [{song.Id}] {song.Name}");
        }

        windowTitle.Append("###Orchestrion");

        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, ScaledVector2(370, 150));
        ImGui.SetNextWindowSize(ScaledVector2(370, 440), ImGuiCond.FirstUseEver);
        // these flags prevent the entire window from getting a secondary scrollbar sometimes, and also keep it from randomly moving slightly with the scrollwheel
        if (ImGui.Begin(windowTitle.ToString(), ref visible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Search: ");
            ImGui.SameLine();
            ImGui.InputText("##searchbox", ref searchText, 32);

            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetWindowSize().X - (32 * ImGuiHelpers.GlobalScale));
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (1 * ImGuiHelpers.GlobalScale));
            if (ImGui.ImageButton(settingsIcon.ImGuiHandle, ScaledVector2(16, 16)))
                settingsVisible = true;

            ImGui.Separator();

            if (ImGui.BeginTabBar("##songlist tabs"))
            {
                if (ImGui.BeginTabItem("All songs"))
                {
                    DrawSonglist(false);
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Favorites"))
                {
                    DrawSonglist(true);
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

        ImGui.End();

        ImGui.PopStyleVar();
    }

    private void DrawFooter(bool isHistory = false)
    {
        ImGui.Separator();
        ImGui.Columns(2, "footer columns", false);
        ImGui.SetColumnWidth(-1, ImGui.GetWindowSize().X - (100 * ImGuiHelpers.GlobalScale));
        
        var songId = isHistory ? songHistory[selectedHistoryEntry].Id : selectedSong;
        if (SongList.TryGetSong(songId, out var song))
        {
            ImGui.TextWrapped(song.Locations);
            ImGui.TextWrapped(song.AdditionalInfo);    
        }
        
        ImGui.NextColumn();
        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetWindowSize().X - (100 * ImGuiHelpers.GlobalScale));
        ImGui.SetCursorPosY(ImGui.GetWindowSize().Y - (30 * ImGuiHelpers.GlobalScale));
        if (ImGui.Button("Stop"))
            Stop();
        ImGui.SameLine();
        if (ImGui.Button("Play"))
            Play(selectedSong);
        ImGui.Columns(1);
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
        foreach (var replacement in orch.Configuration.SongReplacements.Values)
        {
            ImGui.Spacing();
            SongList.TryGetSong(replacement.TargetSongId, out var target);

            var targetText = $"{replacement.TargetSongId} - {target.Name}";
            string replText = replacement.ReplacementId == -1 ? NoChange : $"{replacement.ReplacementId} - {SongList.GetSong(replacement.ReplacementId).Name}";
            
            ImGui.TextWrapped($"{targetText}");
            if (ImGui.IsItemHovered())
                DrawBgmTooltip(target);

            ImGui.Text($"will be replaced with");
            ImGui.TextWrapped($"{replText}");
            if (ImGui.IsItemHovered() && replacement.ReplacementId != -1)
                DrawBgmTooltip(SongList.GetSong(replacement.ReplacementId));

            // Delete button in top right of area
            RightAlignButton(ImGui.GetCursorPosY(), "Delete");
            if (ImGui.Button($"Delete##{replacement.TargetSongId}"))
                removalList.Add(replacement.TargetSongId);

            ImGui.Separator();
        }

        if (removalList.Count > 0)
        {
            foreach (var toRemove in removalList)
                orch.Configuration.SongReplacements.Remove(toRemove);
            removalList.Clear();
            orch.Configuration.Save();
        }
    }

    private void DrawDebug()
    {
        var addr = BGMAddressResolver.BGMManager;
        if (addr == IntPtr.Zero) return;
        var addrStr = $"{addr.ToInt64():X}";
        ImGui.Text(addrStr);
        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            ImGui.SetClipboardText(addrStr);
    }

    private void RightAlignButton(float y, string text)
    {
        var style = ImGui.GetStyle();
        var padding = style.WindowPadding.X + style.FramePadding.X * 2 + style.ScrollbarSize;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetWindowWidth() - ImGui.CalcTextSize(text).X - padding);
        ImGui.SetCursorPosY(y);
    }

    private void DrawCurrentReplacement()
    {
        ImGui.Spacing();

        var targetText = $"{SongList.GetSong(tmpReplacement.TargetSongId).Id} - {SongList.GetSong(tmpReplacement.TargetSongId).Name}";
        string replacementText;
        if (tmpReplacement.ReplacementId == -1)
            replacementText = NoChange;
        else
            replacementText = $"{SongList.GetSong(tmpReplacement.ReplacementId).Id} - {SongList.GetSong(tmpReplacement.ReplacementId).Name}";

        // This fixes the ultra-wide combo boxes, I guess
        var width = ImGui.GetWindowWidth() * 0.60f;

        if (ImGui.BeginCombo("Target Song", targetText))
        {
            foreach (var song in SongList.GetSongs().Values)
            {
                if (!SearchMatches(song)) continue;
                if (orch.Configuration.SongReplacements.ContainsKey(song.Id)) continue;
                var tmpText = $"{song.Id} - {song.Name}";
                var tmpTextSize = ImGui.CalcTextSize(tmpText);
                var isSelected = tmpReplacement.TargetSongId == song.Id;
                if (ImGui.Selectable(tmpText, isSelected, ImGuiSelectableFlags.None, new Vector2(width, tmpTextSize.Y)))
                    tmpReplacement.TargetSongId = song.Id;
                if (ImGui.IsItemHovered())
                    DrawBgmTooltip(song);
            }

            ImGui.EndCombo();
        }

        ImGui.Spacing();

        if (ImGui.BeginCombo("Replacement Song", replacementText))
        {
            if (ImGui.Selectable(NoChange))
                tmpReplacement.ReplacementId = -1;

            foreach (var song in SongList.GetSongs().Values)
            {
                if (!SearchMatches(song)) continue;
                var tmpText = $"{song.Id} - {song.Name}";
                var tmpTextSize = ImGui.CalcTextSize(tmpText);
                var isSelected = tmpReplacement.ReplacementId == song.Id;
                if (ImGui.Selectable(tmpText, isSelected, ImGuiSelectableFlags.None, new Vector2(width, tmpTextSize.Y)))
                    tmpReplacement.ReplacementId = song.Id;
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
            orch.Configuration.SongReplacements.Add(tmpReplacement.TargetSongId, tmpReplacement);
            orch.Configuration.Save();
            ResetReplacement();
        }

        ImGui.Separator();
    }

    private bool SearchMatches(Song song)
    {
        var matchesSearch = searchText.Length != 0
                            && (song.Name.ToLower().Contains(searchText.ToLower())
                                || song.Locations.ToLower().Contains(searchText.ToLower())
                                || song.AdditionalInfo.ToLower().Contains(searchText.ToLower())
                                || song.Id.ToString().Contains(searchText));
        var searchEmpty = searchText.Length == 0;
        return matchesSearch || searchEmpty;
    }

    private void DrawSonglist(bool favoritesOnly)
    {
        // to keep the tab bar always visible and not have it get scrolled out
        ImGui.BeginChild("##songlist_internal", ScaledVector2(-1f, -60f));

        if (ImGui.BeginTable("songlist table", 4, ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("fav", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("id", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("title", ImGuiTableColumnFlags.WidthStretch);
            
            foreach (var s in SongList.GetSongs())
            {
                var song = s.Value;
                if (!SearchMatches(song))
                    continue;

                bool isFavorite = SongList.IsFavorite(song.Id);

                if (favoritesOnly && !isFavorite)
                    continue;

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                if (isFavorite)
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 3);
                    ImGui.Image(favoriteIcon.ImGuiHandle, new Vector2(13, 13));
                }

                ImGui.TableNextColumn();

                ImGui.Text(song.Id.ToString());

                ImGui.TableNextColumn();

                if (ImGui.Selectable($"{song.Name}##{song.Id}", selectedSong == song.Id, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    selectedSong = song.Id;
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        Play(selectedSong);
                }

                if (ImGui.BeginPopupContextItem())
                {
                    if (!isFavorite)
                    {
                        if (ImGui.Selectable("Add to favorites"))
                            SongList.AddFavorite(song.Id);
                    }
                    else
                    {
                        if (ImGui.Selectable("Remove from favorites"))
                            SongList.RemoveFavorite(song.Id);
                    }

                    ImGui.EndPopup();
                }

                ImGui.NextColumn();
            }

            ImGui.EndTable();
        }

        ImGui.EndChild();
        DrawFooter();
    }

    private void DrawSongHistory()
    {
        // to keep the tab bar always visible and not have it get scrolled out
        ImGui.BeginChild("##songlist_internal", new Vector2(-1f, -60f));
        
        if (ImGui.BeginTable("songlist table", 4, ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("fav", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("id", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("title", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("time", ImGuiTableColumnFlags.WidthFixed);

            var now = DateTime.Now;

            // going from the end of the list
            for (int i = songHistory.Count - 1; i >= 0; i--)
            {
                var songHistoryEntry = songHistory[i];
                var song = SongList.GetSong(songHistoryEntry.Id);

                if (!SearchMatches(song))
                    continue;

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                var isFavorite = SongList.IsFavorite(song.Id);

                if (isFavorite)
                {
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 3);
                    ImGui.Image(favoriteIcon.ImGuiHandle, new Vector2(13, 13));
                }

                ImGui.TableNextColumn();

                ImGui.Text(song.Id.ToString());

                ImGui.TableNextColumn();
                if (ImGui.Selectable($"{song.Name}##{i}", selectedHistoryEntry == i, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    selectedSong = song.Id;
                    selectedHistoryEntry = i;
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        Play(selectedSong);
                }

                if (ImGui.BeginPopupContextItem())
                {
                    if (!isFavorite)
                    {
                        if (ImGui.Selectable("Add to favorites"))
                            SongList.AddFavorite(song.Id);
                    }
                    else
                    {
                        if (ImGui.Selectable("Remove from favorites"))
                            SongList.RemoveFavorite(song.Id);
                    }

                    ImGui.EndPopup();
                }

                ImGui.TableNextColumn();

                var deltaTime = now - songHistoryEntry.TimePlayed;
                var unit = deltaTime.TotalMinutes >= 1 ? (int)deltaTime.TotalMinutes : (int)deltaTime.TotalSeconds;
                var label = deltaTime.TotalMinutes >= 1 ? MinAgo : SecAgo;
                ImGui.Text($"{unit}{label}");
            }

            ImGui.EndTable();
        }

        ImGui.EndChild();
        DrawFooter(true);
    }

    private void DrawSettings()
    {
        if (!settingsVisible)
            return;

        ImGui.SetNextWindowSize(ScaledVector2(490, 175), ImGuiCond.Always);
        if (ImGui.Begin("Orchestrion Settings", ref settingsVisible, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse))
        {
            ImGui.SetNextItemOpen(true, ImGuiCond.Appearing);
            if (ImGui.CollapsingHeader("Display##orch options"))
            {
                ImGui.Spacing();

                var showSongInTitlebar = orch.Configuration.ShowSongInTitleBar;
                if (ImGui.Checkbox("Show current song in player title bar", ref showSongInTitlebar))
                {
                    orch.Configuration.ShowSongInTitleBar = showSongInTitlebar;
                    orch.Configuration.Save();
                }

                var showSongInChat = orch.Configuration.ShowSongInChat;
                if (ImGui.Checkbox("Show \"Now playing\" messages in game chat when the current song changes", ref showSongInChat))
                {
                    orch.Configuration.ShowSongInChat = showSongInChat;
                    orch.Configuration.Save();
                }

                var showNative = orch.Configuration.ShowSongInNative;
                if (ImGui.Checkbox("Show current song in the \"server info\" UI element in-game", ref showNative))
                {
                    orch.Configuration.ShowSongInNative = showNative;
                    orch.Configuration.Save();
                }

                if (!showNative)
                    ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0.5f);

                var showIdNative = orch.Configuration.ShowIdInNative;
                if (ImGui.Checkbox("Show song ID in the \"server info\" UI element in-game", ref showIdNative) && showNative)
                {
                    orch.Configuration.ShowIdInNative = showIdNative;
                    orch.Configuration.Save();
                }

                if (!showNative)
                    ImGui.PopStyleVar();

                ImGui.TreePop();
            }

            // if (showDebugOptions && AllowDebug)
            // {
            //     ImGui.SetNextItemOpen(true, ImGuiCond.Appearing);
            //     if (ImGui.CollapsingHeader("Debug##orch options"))
            //     {
            //         ImGui.Spacing();
            //
            //         int targetPriority = orch.Configuration.TargetPriority;
            //
            //         ImGui.SetNextItemWidth(100.0f);
            //         if (ImGui.SliderInt("BGM priority", ref targetPriority, 0, 11))
            //         {
            //             // stop the current song so it doesn't get 'stuck' on in case we switch to a lower priority
            //             Stop();
            //
            //             orch.Configuration.TargetPriority = targetPriority;
            //             orch.Configuration.Save();
            //
            //             // don't (re)start a song here for now
            //         }
            //         ImGui.SameLine();
            //         HelpMarker("Songs play at various priority levels, from 0 to 11.\n" +
            //             "Songs at lower numbers will override anything playing at a higher number, with 0 winning out over everything else.\n" +
            //             "You can experiment with changing this value if you want the game to be able to play certain music even when Orchestrion is active.\n" +
            //             "(Usually) zone music is 10-11, mount music is 6, GATEs are 4.  There is a lot of variety in this, however.\n" +
            //             "The old Orchestrion used to play at level 3 (it now uses 0 by default).");
            //
            //         ImGui.Spacing();
            //         if (ImGui.Button("Dump priority info"))
            //             orch.DumpDebugInformation();
            //
            //         ImGui.TreePop();
            //     }
            // }
        }

        ImGui.End();
    }

    private void DrawBgmTooltip(Song bgm)
    {
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(400f);
        ImGui.TextColored(new Vector4(0, 1, 0, 1), "Song Info");
        ImGui.TextWrapped($"Title: {bgm.Name}");
        ImGui.TextWrapped(string.IsNullOrEmpty(bgm.Locations) ? "Location: Unknown" : $"Location: {bgm.Locations}");
        if (!string.IsNullOrEmpty(bgm.AdditionalInfo))
            ImGui.TextWrapped($"Info: {bgm.AdditionalInfo}");
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    // static void HelpMarker(string desc)
    // {
    //     ImGui.TextDisabled("(?)");
    //     if (ImGui.IsItemHovered())
    //     {
    //         ImGui.BeginTooltip();
    //         ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
    //         ImGui.TextUnformatted(desc);
    //         ImGui.PopTextWrapPos();
    //         ImGui.EndTooltip();
    //     }
    // }

    private ImGuiScene.TextureWrap LoadUIImage(string imageFile)
    {
        var path = Path.Combine(Path.GetDirectoryName(orch.PluginInterface.AssemblyLocation.FullName), imageFile);
        return orch.PluginInterface.UiBuilder.LoadImage(path);
    }

    private static Vector2 ScaledVector2(float x, float y)
    {
        return new Vector2(x * Scale, y * Scale);
    }
}