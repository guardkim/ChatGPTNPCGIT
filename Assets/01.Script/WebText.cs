using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class WebText : MonoBehaviour
{
    public Text MyText;
    void Start() 
    {
        StartCoroutine(GetText());
    }
 
    IEnumerator GetText()
    {
        string url = "https://openapi.naver.com/v1/search/news.json?query=이스라엘&display=30";
        
        UnityWebRequest www = UnityWebRequest.Get(url);
        www.SetRequestHeader("X-Naver-Client-Id", "");
        www.SetRequestHeader("X-Naver-Client-Secret", "");
        yield return www.SendWebRequest(); 
 
        if(www.isNetworkError || www.isHttpError) 
        {
            Debug.Log(www.error);
        }
        else 
        {
            // Show results as text
            Debug.Log(www.downloadHandler.text);
 
            // Or retrieve results as binary data
            MyText.text = www.downloadHandler.text;
        }
    }
}
