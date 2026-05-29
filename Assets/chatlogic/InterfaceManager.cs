using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

public class InterfaceManager : MonoBehaviour
{
    public Toggle toggleText;            // Assign the Text Toggle in Inspector
    public Toggle toggleVoice;           // Assign the Voice Toggle in Inspector
    public GameObject panelText;         // Assign Panel_TextInterface
    public GameObject panelVoice;        // Assign Panel_VoiceInterface
    public GameObject panelSelection;    // Assign Panel_SelectionScreen
    public GameObject panelIntro;        // Assign your new panel here
    public ARTrackedImageManager trackedImageManager; // Assign AR Tracked Image Manager in Inspector
    private bool isARObjectSpawned = false;

    void Start()
    {
        // Disable all panels except the intro panel at the start
        panelIntro.SetActive(false); // Hidden initially until AR is spawned
        panelText.SetActive(false);
        panelVoice.SetActive(false);
        panelSelection.SetActive(false);

        // Subscribe to the tracked image events
        if (trackedImageManager != null)
        {
            trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from events to avoid memory leaks
        if (trackedImageManager != null)
        {
            trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
        }
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (var trackedImage in eventArgs.added)
        {
            if (trackedImage.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking && !isARObjectSpawned)
            {
                isARObjectSpawned = true;
                OnARObjectSpawned();
            }
        }

        foreach (var updatedImage in eventArgs.updated)
        {
            if (updatedImage.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking && !isARObjectSpawned)
            {
                isARObjectSpawned = true;
                OnARObjectSpawned();
            }
        }
    }

    private void OnARObjectSpawned()
    {
        // Activate the intro panel
        panelIntro.SetActive(true);

        // Ensure other panels are hidden
        panelSelection.SetActive(false);
        panelText.SetActive(false);
        panelVoice.SetActive(false);
    }

    // Called when the button in the intro panel is clicked
    public void OnIntroContinueButtonClick()
    {
        // Hide the intro panel and show the selection panel
        panelIntro.SetActive(false);
        panelSelection.SetActive(true);
    }

    public void OnContinueButtonClick()
    {
        panelSelection.SetActive(false);

        if (toggleText.isOn)
        {
            panelText.SetActive(true);
        }
        else if (toggleVoice.isOn)
        {
            panelVoice.SetActive(true);
        }
    }

    public void OnBackButtonClick()
    {
        panelSelection.SetActive(true);
        panelText.SetActive(false);
        panelVoice.SetActive(false);
    }
}
