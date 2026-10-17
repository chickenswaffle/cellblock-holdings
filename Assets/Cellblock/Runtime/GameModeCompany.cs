namespace Cellblock.Unity
{
    public sealed partial class GameController
    {
        public bool StartModeCompany(string type,string location){return CreateCompany("county",false,type,location);}
#if UNITY_EDITOR
        public void PreviewModeCompany(string type,string location)
        {
            if(!CreateCompany("county",true,type,location))throw new System.InvalidOperationException("Could not preserve active company before mode preview.");
            hud.CloseOverlays();Recenter();CameraPosition(true);
        }
#endif
    }
}
