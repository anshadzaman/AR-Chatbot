using System.Collections;
using UnityEngine;
using TMPro;   
using System.IO;
using UnityEngine.Android;
using UnityEngine.Networking;
using System.Text;
using System;
using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;

public class UnityAndGoogleCloudIntegration : MonoBehaviour
{
    [Header("Google Cloud Configuration")]
    public TMP_InputField accessTokenInputField; // User input field for OAuth token
    private string speechToTextEndpoint = "https://speech.googleapis.com/v1/speech:recognize";
    private string dialogflowEndpoint = "https://dialogflow.googleapis.com/v2/projects/archatbot-444805/agent/sessions/{session-id}:detectIntent";
    private string textToSpeechEndpoint = "https://texttospeech.googleapis.com/v1/text:synthesize";
    private string audioFilePath;

    [Header("ChatBot UI")]
    public TMP_Text uiText; // UI Text to display the transcription and bot response

    [Header("Debug Console")]
    public TMP_Text debugConsole; // Live debug console for displaying logs

    [Header("Status Indicators")]
    public TMP_Text statusText; // Text to display status messages (recording, error, etc.)

    private AudioSource audioSource;
    private string accessToken; // Access token to be set by user input
 
    [Header("AR Configuration")]
    public ARTrackedImageManager arTrackedImageManager; // Reference to AR Tracked Image Manager
    private Animator animator;

    private GameObject currentTrackedPrefab;

    void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioFilePath = Path.Combine(Application.persistentDataPath, "recorded_audio.wav");

        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
        }
        
        // Subscribe to ARTrackedImageManager events
        if (arTrackedImageManager != null)
        {
            arTrackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
        }
        else
        {
            Debug.LogError("ARTrackedImageManager is not assigned.");
        }
        
        Application.logMessageReceived += HandleLog;
        Debug.Log("Waiting for the user to input OAuth access token.");
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
        
        if (arTrackedImageManager != null)
        {
            arTrackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
        }
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (debugConsole != null)
        {
            debugConsole.text += logString + "\n";
            if (debugConsole.text.Length > 5000)
            {
                debugConsole.text = debugConsole.text.Substring(debugConsole.text.Length - 5000);
            }
        }
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        // Process newly added images
        foreach (var addedImage in eventArgs.added)
        {
            Debug.Log($"Image added: {addedImage.referenceImage.name}");

            // Spawn or activate the prefab for the tracked image
            currentTrackedPrefab = addedImage.transform.GetChild(0).gameObject;
            currentTrackedPrefab.SetActive(true);

            // Ensure Animator is properly assigned
            animator = currentTrackedPrefab.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("Animator not found on the tracked prefab.");
            }
        }

        // Process updated images
        foreach (var updatedImage in eventArgs.updated)
        {
            // Check if tracking state has changed to avoid frequent updates
            if (updatedImage.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking &&
                currentTrackedPrefab != updatedImage.transform.GetChild(0).gameObject)
            {
                Debug.Log($"Image updated: {updatedImage.referenceImage.name}, tracking state: {updatedImage.trackingState}");

                currentTrackedPrefab = updatedImage.transform.GetChild(0).gameObject;
                currentTrackedPrefab.SetActive(true);
            }
            else if (updatedImage.trackingState != UnityEngine.XR.ARSubsystems.TrackingState.Tracking)
            {
                if (currentTrackedPrefab != null && currentTrackedPrefab.activeSelf)
                {
                    Debug.Log($"Image lost: {updatedImage.referenceImage.name}");
                    currentTrackedPrefab.SetActive(false);
                }
            }
        }

        // Process removed images
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
        accessToken = accessTokenInputField.text.Trim();
        if (string.IsNullOrEmpty(accessToken))
        {
            Debug.LogError("OAuth access token is missing. Please input your OAuth token in the text field.");
            UpdateStatus("Error 1: Missing Access Token", Color.red);
        }
        else
        {
            Debug.Log("OAuth access token is set.");
            UpdateStatus("Access Token Set", Color.green);
        }
    }

    public void StartRecording()
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            Debug.LogError("Access token is not set. Please set the token before starting the recording.");
            UpdateStatus("Error 1: Missing Access Token", Color.red);
            return;
        }

        if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            audioSource.clip = Microphone.Start(null, false, 10, 16000);
            Debug.Log("Recording started...");
            UpdateStatus("Recording Started", Color.green);
        }
        else
        {
            Debug.LogError("Microphone permission is not granted.");
            UpdateStatus("Error 1: Microphone Permission Denied", Color.red);
        }
    }

    public void StopRecording()
    {
        if (Microphone.IsRecording(null))
        {
            Microphone.End(null);
            Debug.Log("Recording stopped.");
            SaveRecordingToFile();
            StartCoroutine(SendAudioToSpeechToText());
            UpdateStatus("Recording Stopped",Color.black);
        }
        else
        {
            Debug.LogError("No recording is active.");
            UpdateStatus("Error 1: No Recording Active", Color.red);
        }
    }

    private void SaveRecordingToFile()
    {
        if (!Directory.Exists(Path.GetDirectoryName(audioFilePath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(audioFilePath));
        }

        byte[] audioData = WavUtility.FromAudioClip(audioSource.clip);
        File.WriteAllBytes(audioFilePath, audioData);
        Debug.Log($"Audio saved to file: {audioFilePath}");
    }

    private IEnumerator SendAudioToSpeechToText()
    {
        byte[] audioData = File.ReadAllBytes(audioFilePath);
        string audioBase64 = Convert.ToBase64String(audioData);

        string jsonData = "{\"config\": {\"encoding\": \"LINEAR16\", \"sampleRateHertz\": 16000, \"languageCode\": \"en-US\"}, " +
                          $"\"audio\": {{\"content\": \"{audioBase64}\"}} }}";

        Debug.Log("Sending audio to Speech-to-Text API...");
        using (UnityWebRequest www = new UnityWebRequest(speechToTextEndpoint, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(new UTF8Encoding().GetBytes(jsonData));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", $"Bearer {accessToken}");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Speech-to-Text Error: {www.error}");
                UpdateStatus("Error 1: Speech-to-Text Failed", Color.red);
            }
            else if (!string.IsNullOrEmpty(www.downloadHandler.text))
            {
                SpeechToTextResponse response = JsonUtility.FromJson<SpeechToTextResponse>(www.downloadHandler.text);
                if (response.results.Length > 0)
                {
                    string transcription = response.results[0].alternatives[0].transcript;
                    Debug.Log("Transcription: " + transcription);
                    StartCoroutine(SendTextToDialogflow(transcription));
                }
                else
                {
                    Debug.LogError("No transcription results found.");
                    UpdateStatus("Error 1: No Transcription Results", Color.red);
                }
            }
            else
            {
                Debug.LogError("Empty response from Speech-to-Text API.");
                UpdateStatus("Error 1: Empty Response from STT", Color.red);
            }
        }
    }

    private IEnumerator SendTextToDialogflow(string transcription)
    {
        string sessionId = Guid.NewGuid().ToString();
        string url = dialogflowEndpoint.Replace("{session-id}", sessionId);

        string jsonData = $"{{\"queryInput\": {{\"text\": {{\"text\": \"{transcription}\", \"languageCode\": \"en\"}}}}}}";

        Debug.Log("Sending transcription to Dialogflow...");
        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(new UTF8Encoding().GetBytes(jsonData));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", $"Bearer {accessToken}");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Dialogflow Error: {www.error}");
                UpdateStatus("Error 2: Dialogflow Failed", Color.red);
            }
            else if (!string.IsNullOrEmpty(www.downloadHandler.text))
            {
                DialogflowResponse response = JsonUtility.FromJson<DialogflowResponse>(www.downloadHandler.text);
                if (response.queryResult != null)
                {
                    string botResponse = response.queryResult.fulfillmentText;
                    Debug.Log("Dialogflow Bot Response: " + botResponse);
                    StartCoroutine(SendTextToTextToSpeech(botResponse));
                }
                else
                {
                    Debug.LogError("No response from Dialogflow.");
                    UpdateStatus("Error 2: No Response from Dialogflow", Color.red);
                }
            }
            else
            {
                Debug.LogError("Empty response from Dialogflow.");
                UpdateStatus("Error 2: Empty Response from Dialogflow", Color.red);
            }
        }
    }

    private IEnumerator SendTextToTextToSpeech(string responseText)
    {
        string jsonData = "{\"input\": {\"text\": \"" + responseText.Replace("\"", "\\\"") + "\"}, " +
                          "\"voice\": {\"languageCode\": \"en-US\", \"name\": \"en-US-Standard-B\"}, " +
                          "\"audioConfig\": {\"audioEncoding\": \"LINEAR16\", \"speakingRate\": 1.1, \"pitch\": 0}}";

        Debug.Log($"Sending Text-to-Speech request: {jsonData}");

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
                UpdateStatus("Error 3: Text-to-Speech Failed", Color.red);
            }
            else if (!string.IsNullOrEmpty(www.downloadHandler.text))
            {
                TextToSpeechResponse response = JsonUtility.FromJson<TextToSpeechResponse>(www.downloadHandler.text);
                if (!string.IsNullOrEmpty(response.audioContent))
                {
                    byte[] audioData = Convert.FromBase64String(response.audioContent);
                    PlayAudio(audioData);
                }
                else
                {
                    Debug.LogError("No audio content found in Text-to-Speech response.");
                    UpdateStatus("Error 3: No Audio Content in TTS Response", Color.red);
                }
            }
            else
            {
                Debug.LogError("Empty response from Text-to-Speech API.");
                UpdateStatus("Error 3: Empty Response from TTS", Color.red);
            }
        }
    }

    private void UpdateStatus(string statusMessage, Color textColor)
    {
        if (statusText != null)
        {
            statusText.text = statusMessage;
            statusText.color = textColor;
        }
    }

    private void PlayAudio(byte[] audioData)
    {
        // Assume WAV format for simplicity
        AudioClip audioClip = WavUtility.ToAudioClip(audioData);
        audioSource.clip = audioClip;
        audioSource.Play();

        // Set the "talking" animator parameter to true
        if (animator != null)
        {
            animator.SetBool("talk", true);
        }

        Debug.Log("Playing synthesized audio...");

        // Start a coroutine to wait for the audio to finish playing
        StartCoroutine(WaitForAudioToFinish(audioClip.length));
    }

    private IEnumerator WaitForAudioToFinish(float clipLength)
    {
        yield return new WaitForSeconds(clipLength);

        // Set the "talking" animator parameter to false after the audio finishes
        if (animator != null)
        {
            animator.SetBool("talk", false);
        }

        Debug.Log("Audio playback finished.");
    }
}


// JSON response classes
[Serializable]
public class SpeechToTextResponse
{
    public Result[] results;
}

[Serializable]
public class Result
{
    public Alternative[] alternatives;
}

[Serializable]
public class Alternative
{
    public string transcript;
}

[Serializable]
public class DialogflowResponse
{
    public QueryResult queryResult;
}

[Serializable]
public class QueryResult
{
    public string fulfillmentText;
}

[Serializable]
public class TextToSpeechResponse
{
    public string audioContent;
}