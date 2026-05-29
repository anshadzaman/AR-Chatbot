using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.IO;

public class UnityAndSpeechToText : MonoBehaviour
{
    [Header("Speech-to-Text Configuration")]
    public string accessToken; // OAuth token
    private string apiEndpoint = "https://speech.googleapis.com/v1/speech:recognize"; // Google Speech-to-Text endpoint
    private string audioFilePath = "Assets/Recordings/recorded_audio.wav"; // Path where the audio will be saved

    [Header("ChatBot UI")]
    public TMP_Text uiText; // Text to display the result of speech-to-text

    private AudioSource audioSource;
    private AudioClip audioClip;

    void Start()
    {
        // Initialize AudioSource for recording
        audioSource = gameObject.AddComponent<AudioSource>();

        // Validate access token
        if (string.IsNullOrEmpty(accessToken))
        {
            Debug.LogError("OAuth access token is missing. Please input your OAuth token in the inspector.");
        }
    }

    // Start recording when the user presses the button
    public void StartRecording()
    {
        audioSource.clip = Microphone.Start(null, false, 10, 16000); // 10 seconds max recording time, 16000 Hz sample rate
        Debug.Log("Recording started...");
    }

    // Stop recording and save the audio to a file
    public void StopRecording()
    {
        Microphone.End(null); // Stop recording
        SaveRecordingToFile();
        SendAudioToGoogle(); // Send the saved audio for transcription
    }

    // Save the recorded audio to a WAV file
    private void SaveRecordingToFile()
{
    // Ensure the directory exists
    string directoryPath = "Assets/Recordings";
    if (!Directory.Exists(directoryPath))
    {
        Directory.CreateDirectory(directoryPath);
        Debug.Log("Created 'Recordings' directory.");
    }

    // Save the audio to a .wav file
    byte[] audioData = WavUtility.FromAudioClip(audioSource.clip);
    string filePath = Path.Combine(directoryPath, "recorded_audio.wav");
    File.WriteAllBytes(filePath, audioData);
    Debug.Log("Audio saved to file: " + filePath);
}


    // Send the saved audio file to Google Speech-to-Text
    private void SendAudioToGoogle()
    {
        StartCoroutine(SendAudioToGoogleCoroutine());
    }

    private IEnumerator SendAudioToGoogleCoroutine()
    {
        // Load the audio file as byte array
        byte[] audioData = File.ReadAllBytes(audioFilePath);
        string audioBase64 = System.Convert.ToBase64String(audioData);

        // Construct the JSON data
        string jsonData = "{\"config\": {\"encoding\": \"LINEAR16\", \"sampleRateHertz\": 16000, \"languageCode\": \"en-US\"}, \"audio\": {\"content\": \"" + audioBase64 + "\"}}";

        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(jsonData);

        // Create UnityWebRequest for Speech-to-Text API
        using (UnityWebRequest www = new UnityWebRequest(apiEndpoint, "POST"))
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
                Debug.Log("Request complete!");

                // Parse the Speech-to-Text response
                SpeechToTextResponse response = JsonUtility.FromJson<SpeechToTextResponse>(www.downloadHandler.text);

                if (response.results.Length > 0)
                {
                    string transcription = response.results[0].alternatives[0].transcript;
                    Debug.Log("Transcription: " + transcription);

                    // Display the transcription in the UI
                    uiText.text = transcription;
                }
                else
                {
                    Debug.LogError("No transcription results found.");
                }
            }
        }
    }

    [System.Serializable]
    public class SpeechToTextResponse
    {
        public Result[] results;
    }

    [System.Serializable]
    public class Result
    {
        public Alternative[] alternatives;
    }

    [System.Serializable]
    public class Alternative
    {
        public string transcript;
    }
}
