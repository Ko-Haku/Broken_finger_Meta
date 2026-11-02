using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

// Pulsanti del controller sinistro (X, Y, click dello stick) e asse X dello stick.
// Con l'hand tracking attivo su Link il controller può risultare "non collegato" per OVRInput anche
// se è in mano: si leggono quindi due fonti, OVRInput (tasti fisici del Touch sinistro, RawButton:
// i pinch della mano non ci finiscono) e i device XR di Unity con la caratteristica Controller+Left
// (le mani tracciate non sono Controller). Il fronte di salita si calcola qui, una volta per frame.
public static class TB_Input
{
    static int frame = -1;
    static bool x, y, stick, xPrima, yPrima, stickPrima;
    static float asseX;
    static readonly List<InputDevice> devices = new List<InputDevice>();

    public static bool XGiu { get { Aggiorna(); return x && !xPrima; } }
    public static bool YGiu { get { Aggiorna(); return y && !yPrima; } }
    public static bool StickGiu { get { Aggiorna(); return stick && !stickPrima; } }
    public static float StickX { get { Aggiorna(); return asseX; } }

    static void Aggiorna()
    {
        if (frame == Time.frameCount) return;
        frame = Time.frameCount;
        xPrima = x; yPrima = y; stickPrima = stick;

        // 1) OVRInput: tasti fisici del Touch sinistro
        x = OVRInput.Get(OVRInput.RawButton.X);
        y = OVRInput.Get(OVRInput.RawButton.Y);
        stick = OVRInput.Get(OVRInput.RawButton.LThumbstick);
        asseX = OVRInput.Get(OVRInput.RawAxis2D.LThumbstick).x;

        // 2) XR di Unity: controller sinistro
        devices.Clear();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left, devices);
        foreach (var d in devices)
        {
            if ((d.characteristics & InputDeviceCharacteristics.HandTracking) != 0) continue;
            if (d.TryGetFeatureValue(CommonUsages.primaryButton, out bool b) && b) x = true;
            if (d.TryGetFeatureValue(CommonUsages.secondaryButton, out b) && b) y = true;
            if (d.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out b) && b) stick = true;
            if (d.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 a) && Mathf.Abs(a.x) > Mathf.Abs(asseX)) asseX = a.x;
        }
    }
}
