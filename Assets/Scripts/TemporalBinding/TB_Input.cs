using UnityEngine;

// Pulsanti del controller sinistro letti solo se il controller è davvero collegato:
// con l'hand tracking OVRInput può mappare i pinch su pulsanti e farli scattare da soli.
public static class TB_Input
{
    static bool Sinistro => OVRInput.IsControllerConnected(OVRInput.Controller.LTouch);

    public static bool XGiu => Sinistro && OVRInput.GetDown(OVRInput.Button.Three, OVRInput.Controller.LTouch);
    public static bool YGiu => Sinistro && OVRInput.GetDown(OVRInput.Button.Four, OVRInput.Controller.LTouch);
    public static float StickX => Sinistro ? OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch).x : 0f;
}
