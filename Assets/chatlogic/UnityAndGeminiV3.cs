using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.XR.ARFoundation;
using System.Text;
using System;

public class UnityAndDialogflow : MonoBehaviour
{
    [Header("Dialogflow Configuration")]
    private string accessToken; // OAuth Bearer token (set dynamically)
    private string apiEndpoint = "https://dialogflow.googleapis.com/v2/projects/archatbot-444805/agent/sessions/session1:detectIntent"; // Replace with your project ID and session ID
    private string textToSpeechEndpoint = "https://texttospeech.googleapis.com/v1/text:synthesize"; // Google Text-to-Speech API endpoint

    [Header("ChatBot UI")]
    public TMP_InputField inputField; // For user messages
    public TMP_Text uiText;           // To display bot responses
    public TMP_InputField tokenInputField; // Input field for setting the access token
    private AudioSource audioSource;

    [Header("AR Configuration")]
    public ARTrackedImageManager arTrackedImageManager; // Reference to AR Tracked Image Manager
    private Animator arAnimator;

    private GameObject currentTrackedPrefab; // Holds the currently tracked prefab instance

    void Start()
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            Debug.LogWarning("OAuth access token is missing. Please set the token using the token input field.");
        }
        audioSource = gameObject.AddComponent<AudioSource>();

        // Subscribe to ARTrackedImageManager events
        if (arTrackedImageManager != null)
        {
            arTrackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
        }
        else
        {
            Debug.LogError("ARTrackedImageManager is not assigned.");
        }
    }

    void OnDestroy()
    {
        if (arTrackedImageManager != null)
        {
            arTrackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
        }
    }

private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (var addedImage in eventArgs.added)
        {
            Debug.Log($"Image added: {addedImage.referenceImage.name}");

            // Spawn or activate the prefab for the tracked image
            currentTrackedPrefab = addedImage.transform.GetChild(0).gameObject;
            currentTrackedPrefab.SetActive(true);

            // Ensure Animator is properly assigned
            arAnimator = currentTrackedPrefab.GetComponent<Animator>();
            if (arAnimator == null)
            {
                Debug.LogError("Animator not found on the tracked prefab.");
            }
        }

        foreach (var updatedImage in eventArgs.updated)
        {
            // Debug.Log($"Image updated: {updatedImage.referenceImage.name}, tracking state: {updatedImage.trackingState}");

            if (updatedImage.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking)
            {
                currentTrackedPrefab = updatedImage.transform.GetChild(0).gameObject;
                currentTrackedPrefab.SetActive(true);
            }
            else
            {
                currentTrackedPrefab?.SetActive(false);
            }
        }

        foreach (var removedImage in eventArgs.removed)
        {
            Debug.Log($"Image removed: {removedImage.referenceImage.name}");

            // Deactivate the prefab when the image is no longer tracked
            if (currentTrackedPrefab != null)
            {
                currentTrackedPrefab.SetActive(false);
            }
        }
    }


    public void SetAccessToken()
    {
        accessToken = tokenInputField.text.Trim(); // Get the token from the input field and remove extra spaces

        if (string.IsNullOrEmpty(accessToken))
        {
            Debug.LogError("Access token is empty. Please enter a valid token.");
        }
        else
        {
            Debug.Log("Access token set successfully.");
        }
    }

    public void SendChat()
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            Debug.LogError("Access token is not set. Please set the token before sending a message.");
            return;
        }

        string userMessage = inputField.text;
        StartCoroutine(SendMessageToDialogflow(userMessage));
    }

    private IEnumerator SendMessageToDialogflow(string userMessage)
    {
        string url = $"{apiEndpoint}";

        string jsonData = "{\"queryInput\": {\"text\": {\"text\": \"" + userMessage + "\", \"languageCode\": \"en\"}}}";

        byte[] jsonToSend = new UTF8Encoding().GetBytes(jsonData);

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(jsonToSend);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", $"Bearer {accessToken}");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error: {www.error}");
                Debug.LogError($"Response: {www.downloadHandler.text}");
            }
            else
            {
                DialogflowResponse response = JsonUtility.FromJson<DialogflowResponse>(www.downloadHandler.text);

                if (!string.IsNullOrEmpty(response.queryResult.fulfillmentText))
                {
                    string reply = response.queryResult.fulfillmentText;
                    StartCoroutine(SendTextToTextToSpeech(reply));
                }
                else
                {
                    Debug.Log("No response text found.");
                }
            }
        }
    }

    private IEnumerator SendTextToTextToSpeech(string responseText)
    {
        string jsonData = "{\"input\": {\"text\": \"" + responseText.Replace("\"", "\\\"") + "\"}, " +
                          "\"voice\": {\"languageCode\": \"en-US\", \"name\": \"en-US-Standard-B\"}, " +
                          "\"audioConfig\": {\"audioEncoding\": \"LINEAR16\", \"speakingRate\": 1.1, \"pitch\": 0}}";

        using (UnityWebRequest www = new UnityWebRequest(textToSpeechEndpoint, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(new UTF8Encoding().GetBytes(jsonData));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", $"Bearer {accessToken}");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Text-to-Speech Error: {www.error}");
            }
            else
            {
                TextToSpeechResponse response = JsonUtility.FromJson<TextToSpeechResponse>(www.downloadHandler.text);
                if (!string.IsNullOrEmpty(response.audioContent))
                {
                    byte[] audioData = Convert.FromBase64String(response.audioContent);
                    PlayAudio(audioData, responseText);
                }
            }
        }
    }

    private void PlayAudio(byte[] audioData, string responseText)
    {
        AudioClip audioClip = WavUtility.ToAudioClip(audioData);
        audioSource.clip = audioClip;

        if (arAnimator != null)
        {
            Debug.Log("Playing animation: talk");
            arAnimator.SetBool("talk", true);
        }
        else
        {
            Debug.LogError("Animator is not assigned.");
        }

        audioSource.Play();
        StartCoroutine(ResetTalkParameter(audioClip.length));

        uiText.text = responseText;
    }

    private IEnumerator ResetTalkParameter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (arAnimator != null)
        {
            arAnimator.SetBool("talk", false);
        }
    }

    [System.Serializable]
    public class DialogflowResponse
    {
        public QueryResult queryResult;
    }

    [System.Serializable]
    public class QueryResult
    {
        public string fulfillmentText;
    }

    [System.Serializable]
    public class TextToSpeechResponse
    {
        public string audioContent;
    }
}
