using System.Numerics;
using CheapLoc;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Style;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;
using Orchestrion.Audio;
using Orchestrion.Persistence;

namespace Orchestrion.Windows;

public class MiniPlayerWindow : Window
{
	private const ImGuiWindowFlags BaseFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoTitleBar;
	
	public MiniPlayerWindow() : base("###orchestrion_miniplayer", BaseFlags)
	{
		SizeCondition = ImGuiCond.Once;
		Size = ImGuiHelpers.ScaledVector2(300, 100);
		IsOpen = true;
	}

	public override bool DrawConditions() => Configuration.Instance.ShowMiniPlayer;

	public override void PreDraw()
	{
		ImGui.PushStyleColor(ImGuiCol.ResizeGrip, 0);
		ImGui.PushStyleColor(ImGuiCol.ResizeGripActive, 0);
		ImGui.PushStyleColor(ImGuiCol.ResizeGripHovered, 0);

		if (Configuration.Instance.MiniPlayerLock)
			Flags |= (ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize);
		else
			Flags &= (~ImGuiWindowFlags.NoMove & ~ImGuiWindowFlags.NoResize);
		
		BgAlpha = Configuration.Instance.MiniPlayerOpacity;
		ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
		ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
	}

	public override void PostDraw()
	{
		// PluginLog.Debug("PostDraw");
		ImGui.PopStyleColor(3);
		ImGui.PopStyleVar(2);
	}

	public override void Draw()
	{
		// PluginLog.Debug("Draw");
		try
		{
			Draw1();
		}
		catch (Exception e)
		{
			PluginLog.Error(e, "oh no");
		}
	}

	public void Draw1()
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
		var popInIcon = FontAwesomeIcon.ArrowAltCircleDown;
		var popInSize = Util.GetIconSize(popInIcon);

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
		
		// Draw popin button
		ImGui.SameLine();
		ImGui.SetCursorPosX(avail - popInSize.X - ImGui.GetStyle().FramePadding.X * 2);
		if (ImGuiComponents.IconButton(popInIcon))
		{
			Configuration.Instance.ShowMiniPlayer = false;
			Configuration.Instance.Save();
		}
		if (ImGui.IsItemHovered())
			ImGui.SetTooltip(Loc.Localize("PopInMiniPlayer", "Pop mini-player back into the Orchestrion window"));

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
		var middleButton = PlaylistManager.IsPlaying ? FontAwesomeIcon.Stop : FontAwesomeIcon.Play;
		var backSize = ImGui.CalcTextSize(FontAwesomeIcon.StepBackward.ToIconString());
		var middleItemSize = ImGui.CalcTextSize(middleButton.ToIconString());
		var forwardSize = ImGui.CalcTextSize(FontAwesomeIcon.StepForward.ToIconString());
		ImGui.PopFont();

		var buttonPaddingWidth = ImGui.GetStyle().FramePadding.X;
		var buttonSpacingWidth = ImGui.GetStyle().ItemSpacing.X;

		// We get two sides of one button and one side of another on each side = 3
		var spacingWidth = buttonPaddingWidth * 3 + buttonSpacingWidth;
		var buttonWidth = backSize.X + middleItemSize.X + forwardSize.X + (spacingWidth * 2);
		var buttonsStartX = (avail - buttonWidth) / 2;

		// Place on same line as times
		ImGui.SetCursorPosY(belowBarY);
		ImGui.SetCursorPosX(buttonsStartX);

		ImGui.BeginDisabled(!PlaylistManager.IsPlaying);
		if (ImGuiComponents.IconButton($"##orch_prev", FontAwesomeIcon.Backward))
		{
			PlaylistManager.Next();
		}
		ImGui.SameLine();
		if (PlaylistManager.IsPlaying)
		{
			if (ImGuiComponents.IconButton($"##orch_stop", FontAwesomeIcon.Stop))
				PlaylistManager.Stop();
		}
		else if (ImGuiComponents.IconButton($"##orch_play", FontAwesomeIcon.Play))
		{
			
		}
		ImGui.SameLine();
		if (ImGuiComponents.IconButton($"##orch_next", FontAwesomeIcon.Forward))
		{
			PlaylistManager.Next();
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