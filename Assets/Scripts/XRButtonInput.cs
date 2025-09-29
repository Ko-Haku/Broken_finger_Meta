using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

public class XRButtonInput : MonoBehaviour
{
    private InputDevice rightController;

    void Start()
    {
        InitializeRightController();
    }

    void InitializeRightController()
    {
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, devices);

        if (devices.Count > 0)
        {
            rightController = devices[0];
            Debug.Log("Controller destro trovato: " + rightController.name);
        }
        else
        {
            Debug.LogWarning("Controller destro NON trovato.");
        }
    }

    void Update()
    {
        // Riconnessione se il device è invalidato (es. visore si spegne e riaccende)
        if (!rightController.isValid)
        {
            InitializeRightController();
            return;
        }

        // Pulsante A → primaryButton
        if (rightController.TryGetFeatureValue(CommonUsages.primaryButton, out bool isA) && isA)
        {
            Debug.Log("Pulsante A premuto (primaryButton)");
        }

        // Pulsante B → secondaryButton
        if (rightController.TryGetFeatureValue(CommonUsages.secondaryButton, out bool isB) && isB)
        {
            Debug.Log("Pulsante B premuto (secondaryButton)");
        }
    }
}