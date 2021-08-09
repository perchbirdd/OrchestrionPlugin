using System;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Hooking;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Orchestrion
{
    public unsafe struct TooltipOptions
    {
        public byte* Text;
        public long TypeSpecificId;
        public int Unknown1;
        public short Unknown2;
        public byte Type;

        public string GetText()
        {
            if (Text == null) return "";
            var text2 = Text;
            var len = 0;
            while (*(text2 + len) != 0) len++;
            return Encoding.UTF8.GetString(Text, len);
        }

        public override string ToString()
        {
            return $"AtkTooltipOptions: TypeSpecificId: {(ulong) TypeSpecificId:X} | Unknown1: {Unknown1:X} | Unknown2: {Unknown2} | Type: {Type} | \"{GetText()}\"";
        }
    }
    
    public unsafe class NativeUIUtil
    {
        private const int NodeId = 60;
        private const int DtrUnknownId = 47;
        private DalamudPluginInterface _pi;
        private bool _initialized;
        private AtkTooltipManager* _tooltipManager;
        private AtkUnitBase* _dtr;
        private AtkTextNode* _musicNode;
        private AtkCollisionNode* _collNode;

        private IntPtr _lastTextPtr;
        private TooltipOptions* _lastTooltipOptions;

        public NativeUIUtil(DalamudPluginInterface pi)
        {
            _pi = pi;
            PluginLog.Debug("--- Orchestrion Native UI utils init ---");
            if (!FFXIVClientStructs.Resolver.Initialized)
                FFXIVClientStructs.Resolver.Initialize();

            var atkStage = (AtkStage*) pi.Framework.Gui.GetBaseUIObject();
            _tooltipManager = &atkStage->TooltipManager;
            PluginLog.Debug($"Tooltipmanager: {(ulong) _tooltipManager:X}");

            var addPtr = pi.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 41 3B ED");
            AddTooltipHook = new Hook<AtkAddTooltip>(addPtr, AddTooltipDetour);
            AddTooltipHook.Enable();
            
            var rmPtr = pi.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 48 8B CB 48 83 C4 38");
            RemoveTooltipHook = new Hook<AtkRemoveTooltip>(rmPtr, RemoveTooltipDetour);
            RemoveTooltipHook.Enable();

            PluginLog.Debug("--- Orchestrion Native UI utils init complete ---");
        }
        
        private delegate void AtkAddTooltip(IntPtr tooltipMgr, byte type, ushort ownerId, IntPtr targetNode, TooltipOptions* args);
        private Hook<AtkAddTooltip> AddTooltipHook;
        private void AddTooltipDetour(IntPtr tooltipMgr, byte type, ushort ownerId, IntPtr targetNode, TooltipOptions* args)
        {
            var mbase = _pi.TargetModuleScanner.Module.BaseAddress.ToInt64();
            PluginLog.Debug($"Tooltip: mgr {tooltipMgr.ToInt64():X} | type {type} | owner {ownerId} | targetNode {targetNode.ToInt64():X}");
            if (args != null)
                PluginLog.Debug($"{args->ToString()}");
            AddTooltipHook.Original(tooltipMgr, type, ownerId, targetNode, args);
        }
        
        private delegate void AtkRemoveTooltip(IntPtr tooltipMgr, IntPtr targetNode);
        private Hook<AtkRemoveTooltip> RemoveTooltipHook;
        private void RemoveTooltipDetour(IntPtr tooltipMgr, IntPtr targetNode)
        {
            PluginLog.Debug($"=== Removing Tooltip from: {targetNode.ToInt64():X}");
            RemoveTooltipHook.Original(tooltipMgr, targetNode);
        }

        private void SetTooltip(string text)
        {
            AllocateTooltipOptions(text);
            PluginLog.Debug($"Allocated tooltip args");
            _tooltipManager->AddTooltip(1, DtrUnknownId, (AtkResNode*) _musicNode, _lastTooltipOptions);
            _tooltipManager->AddTooltip(1, DtrUnknownId, (AtkResNode*) _collNode, _lastTooltipOptions);
            PluginLog.Debug($"Called AddTooltip");
        }

        private void AllocateTooltipOptions(string text)
        {
            Marshal.FreeHGlobal(_lastTextPtr);
            PluginLog.Debug($"Allocating text: {text}");
            var tmptxt = Encoding.UTF8.GetBytes(text + "\0");
            _lastTextPtr = Marshal.AllocHGlobal(tmptxt.Length);
            Marshal.Copy(tmptxt, 0, _lastTextPtr, tmptxt.Length);
            PluginLog.Debug($"_lastTextPtr @ {_lastTextPtr.ToInt64():X}");
            
            if (_lastTooltipOptions == null)
            {
                _lastTooltipOptions = (TooltipOptions*)Marshal.AllocHGlobal(24);
                PluginLog.Debug($"Allocated 24 bytes for tooltip args.");
            }

            _lastTooltipOptions->Text = (byte*) _lastTextPtr;
            _lastTooltipOptions->TypeSpecificId = 0;
            _lastTooltipOptions->Unknown1 = -1;
            _lastTooltipOptions->Unknown2 = 0;
            _lastTooltipOptions->Type = 0;

            PluginLog.Debug($"_lastTooltipOptions @ {(ulong) _lastTooltipOptions:X}");
        }

        private void Init()
        {
            _dtr = (AtkUnitBase*) _pi.Framework.Gui.GetUiObjectByName("_DTR", 1).ToPointer();
            if (_dtr == null) return;
            PluginLog.Debug($"DTR @ {(ulong) _dtr:X}");
            
            // Create text node for jello world
            PluginLog.Debug("Creating our text node.");
            _musicNode = CreateTextNode();
            _musicNode->AtkResNode.NodeID = NodeId;
            PluginLog.Debug("Text node created.");
            
            PluginLog.Debug("Creating our collision node.");
            _collNode = CreateCollisionNode();
            _collNode->AtkResNode.NodeID = NodeId + 1;
            PluginLog.Debug("Collision node created.");
            
            PluginLog.Debug("Finding last sibling node to add to DTR");
            
            var lastChild = _dtr->RootNode->ChildNode;
            while (lastChild->PrevSiblingNode != null) lastChild = lastChild->PrevSiblingNode;
            PluginLog.Debug($"Found last sibling: {(ulong) lastChild:X}");
            lastChild->PrevSiblingNode = (AtkResNode*) _musicNode;
            _musicNode->AtkResNode.ParentNode = lastChild->ParentNode;
            _musicNode->AtkResNode.NextSiblingNode = lastChild;
            
            _musicNode->AtkResNode.PrevSiblingNode = (AtkResNode*)_collNode;
            _collNode->AtkResNode.ParentNode = lastChild->ParentNode;
            _collNode->AtkResNode.NextSiblingNode = (AtkResNode*) _musicNode;
            
            _dtr->RootNode->ChildCount = (ushort) (_dtr->RootNode->ChildCount + 2);
            PluginLog.Debug("Set last sibling of DTR and updated child count");
            
            _dtr->UldManager.UpdateDrawNodeList();
            PluginLog.Debug("Updated node draw list");
            
            SetTooltip("Jello world");
            PluginLog.Debug("Set tooltip");
            
            _initialized = true;
        }

        private AtkTextNode* CreateTextNode()
        {
            var newTextNode = (AtkTextNode*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkTextNode), 8);
            if (newTextNode == null)
            {
                PluginLog.Debug("Failed to allocate memory for text node");
                return null;
            }
            IMemorySpace.Memset(newTextNode, 0, (ulong)sizeof(AtkTextNode));
            newTextNode->Ctor();

            newTextNode->AtkResNode.Type = NodeType.Text;
            newTextNode->AtkResNode.Flags = (short)(NodeFlags.UnkFlag | NodeFlags.HasCollision |NodeFlags.Focusable | NodeFlags.Enabled | NodeFlags.RespondToMouse | NodeFlags.Visible | NodeFlags.AnchorLeft | NodeFlags.AnchorTop);
            newTextNode->AtkResNode.DrawFlags = 12;
            newTextNode->AtkResNode.SetWidth(120);
            newTextNode->AtkResNode.SetHeight(22);
            newTextNode->AtkResNode.SetPositionFloat(-200, 2);

            newTextNode->LineSpacing = 12;
            newTextNode->AlignmentFontType = 5;
            newTextNode->FontSize = 14;
            newTextNode->TextFlags = (byte)(TextFlags.Edge);
            newTextNode->TextFlags2 = 0;
            
            newTextNode->SetText("♪ And Love You Shall Find");

            newTextNode->TextColor.R = 255;
            newTextNode->TextColor.G = 255;
            newTextNode->TextColor.B = 255;
            newTextNode->TextColor.A = 255;

            newTextNode->EdgeColor.R = 142;
            newTextNode->EdgeColor.G = 106;
            newTextNode->EdgeColor.B = 12;
            newTextNode->EdgeColor.A = 255;
            
            return newTextNode;
        }
        
        private AtkCollisionNode* CreateCollisionNode()
        {
            var node = (AtkCollisionNode*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkCollisionNode), 8);
            if (node == null)
            {
                PluginLog.Debug("Failed to allocate memory for collision node");
                return null;
            }
            IMemorySpace.Memset(node, 0, (ulong)sizeof(AtkCollisionNode));
            node->Ctor();
            
            /*
             *  AnchorTop = 1,
                AnchorLeft = 2,
                AnchorBottom = 4,
                AnchorRight = 8,
                Visible = 16, // 0x00000010
                Enabled = 32, // 0x00000020
                Clip = 64, // 0x00000040
                Fill = 128, // 0x00000080
                HasCollision = 256, // 0x00000100
                RespondToMouse = 512, // 0x00000200
                Focusable = 1024, // 0x00000400
                Droppable = 2048, // 0x00000800
                IsTopNode = 4096, // 0x00001000
                UnkFlag = 8192, // 0x00002000
             */
        
            node->AtkResNode.Type = NodeType.Collision;
            node->AtkResNode.Flags = (short)(NodeFlags.UnkFlag | NodeFlags.HasCollision | NodeFlags.Focusable | NodeFlags.Enabled | NodeFlags.RespondToMouse | NodeFlags.Visible | NodeFlags.AnchorTop | NodeFlags.AnchorLeft);
            node->AtkResNode.ToggleVisibility(true);
            node->AtkResNode.SetWidth(120);
            node->AtkResNode.SetHeight(22);
            node->AtkResNode.SetPositionFloat(-200, 2);

            return node;
        }

        public void Update(string text = null)
        {
            if (!_initialized)
                Init();
            
            if (_dtr == null || _musicNode == null) return;
            var collisionNode = _dtr->UldManager.NodeList[1];
            var xPos = collisionNode->Width;
            _musicNode->AtkResNode.SetPositionFloat(xPos * -1f + _musicNode->AtkResNode.Width, 2);

            // TODO: WIP text truncation
            // if (text != null)
            // {
            //     var len = text.Length;
            //     fixed (byte* textPtr = Encoding.UTF8.GetBytes(text))
            //     {
            //         ushort w = ushort.MaxValue, h = 0;
            //         while (w > 120)
            //             musicNode->GetTextDrawSize(&w, &h, textPtr, 0, len--);
            //     }
            //     var settableText = text;
            //     musicNode->SetText($"{text}");
            // }

            if (text != null)
            {
                _musicNode->SetText(text);
                // SetTooltip(text);
            }
        }

        public void Dispose()
        {
            PluginLog.Log("Disabling addHook...");
            AddTooltipHook?.Disable();
            PluginLog.Log("Disposing addHook...");
            AddTooltipHook?.Dispose();
            PluginLog.Log("Disabling rmHook...");
            RemoveTooltipHook?.Disable();
            PluginLog.Log("Disposing rmHook...");
            RemoveTooltipHook?.Dispose();
            PluginLog.Log("Freeing last text...");
            Marshal.FreeHGlobal(_lastTextPtr);
            PluginLog.Log("Freeing last tooltip...");
            Marshal.FreeHGlobal((IntPtr) _lastTooltipOptions);
            
            if (_musicNode == null) return;
            PluginLog.Log("Calling RemoveTooltip");
            _tooltipManager->RemoveTooltip((AtkResNode*) _musicNode);
            PluginLog.Log("Unlinking Text node...");
            var relNode = _musicNode->AtkResNode.NextSiblingNode;
            relNode->PrevSiblingNode = null;
            PluginLog.Log("Destroying Text node...");
            _musicNode->AtkResNode.Destroy(true);
            PluginLog.Log("Destroying Collision node...");
            _musicNode->AtkResNode.Destroy(true);
            PluginLog.Log("Setting Text node null...");
            _musicNode = null;
            PluginLog.Log("Decrementing dtr->RootNode->ChildCount by 1...");
            _dtr->RootNode->ChildCount = (ushort) (_dtr->RootNode->ChildCount - 2);
            PluginLog.Log("Calling UpdateDrawNodeList()...");
            _dtr->UldManager.UpdateDrawNodeList();
            
            PluginLog.Log("Dispose done!");
        }
    }
}