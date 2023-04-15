using System.Numerics;
using CheapLoc;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using ImGuiNET;
using Orchestrion.Audio;
using Orchestrion.Persistence;

namespace Orchestrion.Components;

public static class Player
{
	public static void Draw()
	{
		var elapsed = TimeSpan.Zero;
		var total = TimeSpan.Zero;

		if (PlaylistManager.IsPlaying)
		{
			elapsed = PlaylistManager.ElapsedDuration;
			total = PlaylistManager.Duration;
		}
		
		var currentTimeStr = $"{elapsed:mm\\:ss}";
		var totalTimeStr = $"{total:mm\\:ss}";
		var songNameStr = PlaylistManager.IsPlaying ? PlaylistManager.CurrentSong.Name : "None";
		var playlistNameStr = PlaylistManager.IsPlaying ? PlaylistManager.CurrentPlaylist.Name : "N/A";
		var playlistTextStr = "From: " + playlistNameStr;

		var avail = ImGui.GetContentRegionAvail().X;
		var totalTimeSize = ImGui.CalcTextSize(totalTimeStr);
		var songNameWidth = ImGui.CalcTextSize(songNameStr).X;
		var playlistNameWidth = ImGui.CalcTextSize(playlistTextStr).X;

		// These are drawn in middle so calc X position
		var songNameX = (avail - songNameWidth) / 2;
		var playlistTextX = (avail - playlistNameWidth) / 2;

		// Draw song first
		ImGui.SetCursorPosX(songNameX);
		ImGui.Text(songNameStr);
		var fromY = ImGui.GetCursorPosY();
		
		// Draw pop in/out button
		var popIcon = Configuration.Instance.ShowMiniPlayer ? FontAwesomeIcon.ArrowAltCircleDown : FontAwesomeIcon.ArrowUpRightFromSquare;
		var popSize = Util.GetIconSize(popIcon);
		var popStr = 
			Configuration.Instance.ShowMiniPlayer
			? Loc.Localize("PopInMiniPlayer", "Pop mini-player back into the Orchestrion window")
			: Loc.Localize("PopOutMiniPlayer", "Pop mini-player out of the Orchestrion window");

		ImGui.SameLine();
		ImGui.SetCursorPosX(avail - popSize.X - ImGui.GetStyle().FramePadding.X * 2);
		if (ImGuiComponents.IconButton(popIcon))
		{
			Configuration.Instance.ShowMiniPlayer ^= true;
			Configuration.Instance.Save();
		}
		if (ImGui.IsItemHovered())
			ImGui.SetTooltip(popStr);

		// Draw playlist
		ImGui.SetCursorPosY(fromY);
		ImGui.SetCursorPosX(playlistTextX);
		ImGui.Text(playlistTextStr);

		// Draw progress bar
		ImGui.PushStyleColor(ImGuiCol.PlotHistogram, ImGuiColors.DalamudWhite);
		var frac = elapsed.TotalMilliseconds / total.TotalMilliseconds;
		if (elapsed == TimeSpan.Zero && total == TimeSpan.Zero)
			frac = 0;
		ImGui.ProgressBar((float)frac, new Vector2(-1, 8), string.Empty);
		ImGui.PopStyleColor();

		// Draw times
		var belowBarY = ImGui.GetCursorPosY();

		// Draw buttons
		ImGui.PushFont(UiBuilder.IconFont);
		var repeatButton = FontAwesomeIcon.Repeat.ToIconString();
		var shuffleButton = FontAwesomeIcon.Random.ToIconString();
		var middleButton = PlaylistManager.IsPlaying ? FontAwesomeIcon.Stop : FontAwesomeIcon.Play;
		var repeatSize = ImGui.CalcTextSize(repeatButton);
		var shuffleSize = ImGui.CalcTextSize(shuffleButton);
		var backSize = ImGui.CalcTextSize(FontAwesomeIcon.StepBackward.ToIconString());
		var middleItemSize = ImGui.CalcTextSize(middleButton.ToIconString());
		var forwardSize = ImGui.CalcTextSize(FontAwesomeIcon.StepForward.ToIconString());
		ImGui.PopFont();

		var buttonPaddingWidth = ImGui.GetStyle().FramePadding.X;
		var buttonSpacingWidth = ImGui.GetStyle().ItemSpacing.X;

		// We get two sides of two buttons and one side of another on each side = 5
		var spacingWidth = buttonPaddingWidth * 5 + buttonSpacingWidth * 2;
		var buttonTotalWidth = repeatSize.X + backSize.X + middleItemSize.X + forwardSize.X + shuffleSize.X + spacingWidth * 2;
		var buttonsStartX = (avail - buttonTotalWidth) / 2;
		
		// ImGui.Text($"spacingWidth: {spacingWidth} buttonTotalWidth: {buttonTotalWidth} avail: {avail} buttonsStartX: {buttonsStartX}");

		// Place on same line as times
		ImGui.SetCursorPosY(belowBarY);
		ImGui.SetCursorPosX(buttonsStartX);

		ImGui.BeginDisabled(!PlaylistManager.IsPlaying);
		if (ImGuiComponents.IconButton($"##orch_repeat", FontAwesomeIcon.Repeat))
		{
			PlaylistManager.CurrentPlaylist.NextRepeatMode();
		}
		if (ImGui.IsItemHovered())
		{
			var text = PlaylistManager.CurrentPlaylist.RepeatMode switch
			{
				RepeatMode.One => Loc.Localize("RepeatOne", "Repeat One: Repeating current song"),
				RepeatMode.All => Loc.Localize("RepeatAll", "Repeat All: Repeating current playlist"),
				RepeatMode.Once => Loc.Localize("RepeatOnce", "Repeat Once: Playing current playlist through once"),
				_ => throw new ArgumentOutOfRangeException(),
			};
			ImGui.SetTooltip(text);
		}
		ImGui.SameLine();
		if (ImGuiComponents.IconButton($"##orch_prev", FontAwesomeIcon.Backward))
		{
			PlaylistManager.Previous();
		}
		ImGui.SameLine();
		if (PlaylistManager.IsPlaying)
		{
			if (ImGuiComponents.IconButton($"##orch_stop", FontAwesomeIcon.Stop))
				PlaylistManager.Stop();
		}
		else
			ImGuiComponents.IconButton($"##orch_play", FontAwesomeIcon.Play);
		ImGui.SameLine();
		if (ImGuiComponents.IconButton($"##orch_next", FontAwesomeIcon.Forward))
		{
			PlaylistManager.Next();
		}
		ImGui.SameLine();
		if (ImGuiComponents.IconButton($"##orch_shuffle", FontAwesomeIcon.Random))
		{
			PlaylistManager.CurrentPlaylist.NextShuffleMode();
		}
		if (ImGui.IsItemHovered())
		{
			var text = PlaylistManager.CurrentPlaylist.ShuffleMode switch
			{
				ShuffleMode.Off => Loc.Localize("ShuffleOff", "Shuffle Off: Playing songs in order"),
				ShuffleMode.On => Loc.Localize("ShuffleOn", "Shuffle On: Playing songs randomly"),
				_ => throw new ArgumentOutOfRangeException(),
			};
			ImGui.SetTooltip(text);
		}
		ImGui.EndDisabled();

		// Draw times
		// Align vertically in the middle of the buttons
		ImGui.SetCursorPosY(belowBarY + ImGui.GetStyle().FramePadding.Y);
		ImGui.Text(currentTimeStr);
		ImGui.SameLine();
		ImGui.SetCursorPosX(avail - totalTimeSize.X);
		ImGui.Text(totalTimeStr);
	}
}