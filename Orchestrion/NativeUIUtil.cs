using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Orchestrion
{
    public unsafe class NativeUIUtil
    {
        private const int NodeId = 60;
        private DalamudPluginInterface _pi;
        private bool _initialized;

        public NativeUIUtil(DalamudPluginInterface pi)
        {
            _pi = pi;
            if (!FFXIVClientStructs.Resolver.Initialized)
                FFXIVClientStructs.Resolver.Initialize();
        }

        private AtkUnitBase* GetDTR()
        {
            return (AtkUnitBase*) _pi.Framework.Gui.GetUiObjectByName("_DTR", 1).ToPointer();
        }
        
        private AtkTextNode* GetTextNode()
        {
            var dtr = GetDTR();
            if (dtr == null) return null;
            return dtr->UldManager.NodeListCount > 17 ? (AtkTextNode*)dtr->UldManager.NodeList[17] : null;
        }
        
        private void Init()
        {
            var dtr = GetDTR();
            if (dtr == null || dtr->UldManager.NodeListCount < 17) return;
            PluginLog.Debug($"DTR @ {(ulong) dtr:X}");
            
            // Create text node for jello world
            PluginLog.Debug("Creating our text node.");
            var musicNode = CreateTextNode();
            musicNode->AtkResNode.NodeID = NodeId;
            PluginLog.Debug("Text node created.");
            
            PluginLog.Debug("Finding last sibling node to add to DTR");
            var lastChild = dtr->RootNode->ChildNode;
            while (lastChild->PrevSiblingNode != null) lastChild = lastChild->PrevSiblingNode;
            PluginLog.Debug($"Found last sibling: {(ulong) lastChild:X}");
            lastChild->PrevSiblingNode = (AtkResNode*) musicNode;
            musicNode->AtkResNode.ParentNode = lastChild->ParentNode;
            musicNode->AtkResNode.NextSiblingNode = lastChild;

            dtr->RootNode->ChildCount = (ushort) (dtr->RootNode->ChildCount + 1);
            PluginLog.Debug("Set last sibling of DTR and updated child count");
            
            dtr->UldManager.UpdateDrawNodeList();
            PluginLog.Debug("Updated node draw list");
            
            _initialized = true;
        }
        
        public void Update(string text = null)
        {
            if (!_initialized)
                Init();

            var dtr = GetDTR();
            var musicNode = GetTextNode();
            
            if (dtr == null || musicNode == null) return;
            var collisionNode = dtr->UldManager.NodeList[1];
            var xPos = collisionNode->Width;
            musicNode->AtkResNode.SetPositionFloat(xPos * -1f + musicNode->AtkResNode.Width, 2);

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
                musicNode->SetText(text);
                // SetTooltip(text);
            }
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
            newTextNode->AtkResNode.Flags = (short)(NodeFlags.AnchorLeft | NodeFlags.AnchorTop);
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

        public void Dispose()
        {
            var dtr = GetDTR();
            var musicNode = GetTextNode();
            if (dtr == null || musicNode == null) return;
            
            PluginLog.Log("Unlinking Text node...");
            var relNode = musicNode->AtkResNode.NextSiblingNode;
            relNode->PrevSiblingNode = null;
            PluginLog.Log("Destroying Text node...");
            musicNode->AtkResNode.Destroy(true);
            PluginLog.Log("Decrementing dtr->RootNode->ChildCount by 1...");
            dtr->RootNode->ChildCount = (ushort) (dtr->RootNode->ChildCount - 1);
            PluginLog.Log("Calling UpdateDrawNodeList()...");
            dtr->UldManager.UpdateDrawNodeList();
            PluginLog.Log("Dispose done!");
        }
    }
}