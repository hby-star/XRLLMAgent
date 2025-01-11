using UnityEditor;
using UnityEngine;

public class MicrophoneSelectorWindow : EditorWindow
{
    public string[] microphoneDevices; // Array to hold available microphones
    public int selectedDeviceIndex = 0; // Default selected microphone index

    [MenuItem("Tools/Microphone Selector")]
    public static void ShowWindow()
    {
        GetWindow<MicrophoneSelectorWindow>("Microphone Selector");
    }

    private void OnEnable()
    {
        RefreshMicrophones();
    }

    private void OnGUI()
    {
        GUILayout.Label("Microphone Selector", EditorStyles.boldLabel);

        if (microphoneDevices == null || microphoneDevices.Length == 0)
        {
            EditorGUILayout.HelpBox("No microphones detected.", MessageType.Warning);
            if (GUILayout.Button("Refresh"))
            {
                RefreshMicrophones();
            }
            return;
        }

        // Dropdown to select a microphone
        selectedDeviceIndex = EditorGUILayout.Popup("Select Microphone", selectedDeviceIndex, microphoneDevices);

        // Display selected microphone
        GUILayout.Label($"Selected Microphone: {microphoneDevices[selectedDeviceIndex]}");

        if (GUILayout.Button("Set Selected Microphone"))
        {
            SetMicrophone(microphoneDevices[selectedDeviceIndex]);
        }

        if (GUILayout.Button("Refresh"))
        {
            RefreshMicrophones();
        }
    }

    private void RefreshMicrophones()
    {
        microphoneDevices = Microphone.devices;
        if (microphoneDevices.Length > 0 && selectedDeviceIndex >= microphoneDevices.Length)
        {
            selectedDeviceIndex = 0; // Reset index if it goes out of bounds
        }
    }

    private void SetMicrophone(string deviceName)
    {
        Debug.Log($"Microphone set to: {deviceName}");
        // Here you can pass the selected microphone to your RecordingManager or other systems
        PlayerPrefs.SetString("SelectedMicrophone", deviceName);
        PlayerPrefs.Save();
    }
}
