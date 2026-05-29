using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadSecondScene(string sceneName)
    {
        SceneManager.LoadScene("Chatbot");
    }
}
