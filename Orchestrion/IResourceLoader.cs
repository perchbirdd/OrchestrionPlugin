using System;

namespace Orchestrion
{
    interface IResourceLoader
    {
        ImGuiScene.TextureWrap LoadUIImage(string path);
    }
}
