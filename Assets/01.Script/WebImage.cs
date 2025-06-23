using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class WebImage : MonoBehaviour
{
    public RawImage myImage;
    void Start() 
    {
        StartCoroutine(GetTexture());
    }

    IEnumerator GetTexture()
    {
        
        UnityWebRequest www = UnityWebRequestTexture.GetTexture("https://contents.kyobobook.co.kr/sih/fit-in/458x0/pdt/9788925566894.jpg");
        yield return www.SendWebRequest();

        if (www.isNetworkError || www.isHttpError)
        {
            Debug.Log(www.error);
        }
        else
        {
            Texture myTexture = ((DownloadHandlerTexture)www.downloadHandler).texture;
            myImage.texture = myTexture;
        }
    }
}
